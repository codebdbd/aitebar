using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.Versioning;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AiteBar.TelegramPostEditor.Models;
using AiteBar.TelegramPostEditor.Persistence;
using Microsoft.Data.Sqlite;

namespace AiteBar.TelegramPostEditor.Services;

[SupportedOSPlatform("windows6.1")]
public sealed class PublicationService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly PostDbMigrator _migrator;
    private readonly PostMediaStore _mediaStore;
    private readonly TelegramBotClient _botClient;
    private readonly TelegramSecretStore _secretStore;
    private readonly TelegramChannelService _channelService;

    public PublicationService(
        PostDbMigrator migrator,
        PostMediaStore mediaStore,
        TelegramBotClient botClient,
        TelegramSecretStore secretStore,
        TelegramChannelService channelService)
    {
        _migrator = migrator ?? throw new ArgumentNullException(nameof(migrator));
        _mediaStore = mediaStore ?? throw new ArgumentNullException(nameof(mediaStore));
        _botClient = botClient ?? throw new ArgumentNullException(nameof(botClient));
        _secretStore = secretStore ?? throw new ArgumentNullException(nameof(secretStore));
        _channelService = channelService ?? throw new ArgumentNullException(nameof(channelService));
        _migrator.EnsureMigrated();
    }

    public async Task<IReadOnlyList<PublicationTarget>> PublishNowAsync(
        PostDocument document,
        IReadOnlyList<ChannelAccount> channels,
        bool isTestRun = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(channels);

        var results = new List<PublicationTarget>();
        foreach (ChannelAccount channel in channels)
        {
            if (isTestRun && !channel.IsTestChannel)
            {
                continue;
            }

            PublicationTarget target = CreateTarget(document, channel, DateTime.UtcNow, WorkflowStatus.SENDING);
            SaveTarget(target);
            RecordEvent(target.Id, "SENDING_STARTED", $"Direct publish to '{channel.DisplayName}' (rev {document.Revision})");

            PublicationTarget completed = await ExecuteTargetSendAsync(document, channel, target, cancellationToken).ConfigureAwait(false);
            results.Add(completed);
        }

        return results;
    }

    public async Task<IReadOnlyList<PublicationTarget>> RetryFailedTargetsAsync(
        PostDocument document,
        IReadOnlyList<PublicationTarget> existingTargets,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(existingTargets);

        IReadOnlyList<ChannelAccount> allChannels = _channelService.GetChannels();
        var updated = new List<PublicationTarget>();

        foreach (PublicationTarget target in existingTargets)
        {
            if (target.Status != WorkflowStatus.FAILED || target.ServerId != null ||
                target.PostId != document.Id || target.PostRevision != document.Revision)
            {
                updated.Add(target);
                continue;
            }

            ChannelAccount? channel = allChannels.FirstOrDefault(c => c.Id == target.ChannelId);
            if (channel == null)
            {
                target.Status = WorkflowStatus.FAILED;
                target.LastErrorCode = "CHANNEL_NOT_FOUND";
                target.LastErrorDetails = "Target channel no longer exists.";
                SaveTarget(target);
                updated.Add(target);
                continue;
            }

            target.Status = WorkflowStatus.SENDING;
            SaveTarget(target);
            RecordEvent(target.Id, "RETRY_STARTED", $"Retrying target for '{channel.DisplayName}'");

            PublicationTarget result = await ExecuteTargetSendAsync(document, channel, target, cancellationToken).ConfigureAwait(false);
            updated.Add(result);
        }

        return updated;
    }

    public async Task<TelegramSendResult> EditPublishedMessageAsync(
        PublicationTarget target,
        PostDocument updatedDocument,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(updatedDocument);

        if (target.Status != WorkflowStatus.SENT || target.TelegramMessageIds.Count == 0)
        {
            return new TelegramSendResult(false, [], false, false, false, null, "NOT_EDITABLE", "Only sent messages with known message_id can be edited.");
        }

        if (target.TelegramMessageIds.Count > 1)
        {
            return new TelegramSendResult(false, [], false, false, false, null, "ALBUM_EDIT_UNSUPPORTED", "Telegram media groups cannot be edited as a single text message.");
        }

        ChannelAccount channel = _channelService.GetChannels().FirstOrDefault(c => c.Id == target.ChannelId)
            ?? throw new KeyNotFoundException($"Channel '{target.ChannelId}' not found.");
        string token = _secretStore.ReadBotToken(channel.BotId)
            ?? throw new InvalidOperationException("Bot token is missing.");

        updatedDocument.ChannelOverrides.TryGetValue(channel.Id, out ChannelPostOverride? channelOverride);
        TelegramSerializedContent content = TelegramDocumentSerializer.SerializeForTelegram(updatedDocument, channelOverride);
        List<InlineUrlButton> buttons = channelOverride?.Buttons ?? updatedDocument.Buttons;

        TelegramSendResult result = await _botClient.EditMessageTextAsync(
            token,
            channel.TelegramChatId,
            target.TelegramMessageIds[0],
            content,
            buttons,
            channelOverride?.DisableWebPagePreview ?? updatedDocument.DisableWebPagePreview,
            cancellationToken).ConfigureAwait(false);

        RecordEvent(
            target.Id,
            result.Success ? "EDITED_IN_TELEGRAM" : "EDIT_FAILED",
            result.Success ? "Message text updated in Telegram." : $"{result.ErrorCode}: {result.ErrorDescription}");

        return result;
    }

    public async Task<bool> DeletePublishedMessagesAsync(PublicationTarget target, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (target.TelegramMessageIds.Count == 0)
        {
            return false;
        }

        ChannelAccount channel = _channelService.GetChannels().FirstOrDefault(c => c.Id == target.ChannelId)
            ?? throw new KeyNotFoundException($"Channel '{target.ChannelId}' not found.");
        string token = _secretStore.ReadBotToken(channel.BotId)
            ?? throw new InvalidOperationException("Bot token is missing.");

        bool allDeleted = true;
        foreach (long messageId in target.TelegramMessageIds)
        {
            TelegramSendResult res = await _botClient.DeleteMessageAsync(token, channel.TelegramChatId, messageId, cancellationToken).ConfigureAwait(false);
            if (!res.Success)
            {
                allDeleted = false;
                RecordEvent(target.Id, "DELETE_FAILED", $"msg {messageId}: {res.ErrorCode} {res.ErrorDescription}");
            }
        }

        if (allDeleted)
        {
            target.Status = WorkflowStatus.CANCELED;
            SaveTarget(target);
            RecordEvent(target.Id, "DELETED_IN_TELEGRAM", "Published message(s) deleted from Telegram.");
        }

        return allDeleted;
    }

    public PublicationTarget CreateTarget(PostDocument document, ChannelAccount channel, DateTime scheduledUtc, WorkflowStatus initialStatus) => new()
    {
        Id = Guid.NewGuid().ToString("N"),
        PostId = document.Id,
        PostRevision = document.Revision,
        ChannelId = channel.Id,
        ScheduledUtc = scheduledUtc,
        Status = initialStatus
    };

    public void SaveTarget(PublicationTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);
        using SqliteConnection connection = _migrator.OpenConnection();
        SaveTarget(connection, null, target);
    }

    internal void SaveTargets(IReadOnlyList<PublicationTarget> targets)
    {
        using SqliteConnection connection = _migrator.OpenConnection();
        using SqliteTransaction transaction = connection.BeginTransaction();
        foreach (PublicationTarget target in targets)
            SaveTarget(connection, transaction, target);
        transaction.Commit();
    }

    private static void SaveTarget(SqliteConnection connection, SqliteTransaction? transaction, PublicationTarget target)
    {
        using SqliteCommand cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = """
            INSERT INTO publication_targets (id, post_id, post_revision, channel_id, scheduled_utc, status, server_id, last_sync_utc, telegram_ids_json, last_error_code, last_error_details)
            VALUES ($id, $postId, $rev, $channelId, $sched, $status, $serverId, $sync, $tgIds, $errCode, $errDetails)
            ON CONFLICT(id) DO UPDATE SET
                post_revision = excluded.post_revision,
                scheduled_utc = excluded.scheduled_utc,
                status = excluded.status,
                server_id = excluded.server_id,
                last_sync_utc = excluded.last_sync_utc,
                telegram_ids_json = excluded.telegram_ids_json,
                last_error_code = excluded.last_error_code,
                last_error_details = excluded.last_error_details;
            """;
        cmd.Parameters.AddWithValue("$id", target.Id);
        cmd.Parameters.AddWithValue("$postId", target.PostId);
        cmd.Parameters.AddWithValue("$rev", target.PostRevision);
        cmd.Parameters.AddWithValue("$channelId", target.ChannelId);
        cmd.Parameters.AddWithValue("$sched", target.ScheduledUtc.ToString("O"));
        cmd.Parameters.AddWithValue("$status", (int)target.Status);
        cmd.Parameters.AddWithValue("$serverId", (object?)target.ServerId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$sync", target.LastSyncUtc.HasValue ? target.LastSyncUtc.Value.ToString("O") : DBNull.Value);
        cmd.Parameters.AddWithValue("$tgIds", JsonSerializer.Serialize(target.TelegramMessageIds, JsonOptions));
        cmd.Parameters.AddWithValue("$errCode", (object?)target.LastErrorCode ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$errDetails", (object?)target.LastErrorDetails ?? DBNull.Value);
        cmd.ExecuteNonQuery();
    }

    public IReadOnlyList<PublicationTarget> GetTargetsForPost(string postId)
    {
        using SqliteConnection connection = _migrator.OpenConnection();
        using SqliteCommand cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT id, post_id, post_revision, channel_id, scheduled_utc, status, server_id, last_sync_utc, telegram_ids_json, last_error_code, last_error_details
            FROM publication_targets
            WHERE post_id = $postId
            ORDER BY scheduled_utc DESC;
            """;
        cmd.Parameters.AddWithValue("$postId", postId);
        return ReadTargets(cmd);
    }

    public IReadOnlyList<PublicationTarget> GetAllTargets(WorkflowStatus? statusFilter = null, string? channelId = null)
    {
        using SqliteConnection connection = _migrator.OpenConnection();
        using SqliteCommand cmd = connection.CreateCommand();
        var where = new List<string>();
        if (statusFilter.HasValue)
        {
            where.Add("status = $status");
            cmd.Parameters.AddWithValue("$status", (int)statusFilter.Value);
        }
        if (!string.IsNullOrWhiteSpace(channelId))
        {
            where.Add("channel_id = $channelId");
            cmd.Parameters.AddWithValue("$channelId", channelId);
        }

        string whereClause = where.Count > 0 ? $"WHERE {string.Join(" AND ", where)}" : string.Empty;
        cmd.CommandText = $"""
            SELECT id, post_id, post_revision, channel_id, scheduled_utc, status, server_id, last_sync_utc, telegram_ids_json, last_error_code, last_error_details
            FROM publication_targets
            {whereClause}
            ORDER BY scheduled_utc ASC;
            """;
        return ReadTargets(cmd);
    }

    public void RecordEvent(string targetId, string eventType, string detailsSanitized)
    {
        using SqliteConnection connection = _migrator.OpenConnection();
        using SqliteCommand cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO publication_events (id, target_id, event_utc, event_type, details_sanitized)
            VALUES ($id, $targetId, $utc, $type, $details);
            """;
        cmd.Parameters.AddWithValue("$id", Guid.NewGuid().ToString("N"));
        cmd.Parameters.AddWithValue("$targetId", targetId);
        cmd.Parameters.AddWithValue("$utc", DateTime.UtcNow.ToString("O"));
        cmd.Parameters.AddWithValue("$type", eventType);
        cmd.Parameters.AddWithValue("$details", detailsSanitized ?? string.Empty);
        cmd.ExecuteNonQuery();
    }

    public IReadOnlyList<PublicationEvent> GetEvents(string? targetId = null, int limit = 200)
    {
        using SqliteConnection connection = _migrator.OpenConnection();
        using SqliteCommand cmd = connection.CreateCommand();
        string whereClause = string.IsNullOrWhiteSpace(targetId) ? string.Empty : "WHERE target_id = $targetId";
        if (!string.IsNullOrWhiteSpace(targetId))
        {
            cmd.Parameters.AddWithValue("$targetId", targetId);
        }

        cmd.CommandText = $"""
            SELECT id, target_id, event_utc, event_type, details_sanitized
            FROM publication_events
            {whereClause}
            ORDER BY event_utc DESC
            LIMIT {Math.Clamp(limit, 1, 2000)};
            """;

        var list = new List<PublicationEvent>();
        using SqliteDataReader reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            list.Add(new PublicationEvent
            {
                Id = reader.GetString(0),
                TargetId = reader.GetString(1),
                EventUtc = DateTime.Parse(reader.GetString(2), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
                EventType = reader.GetString(3),
                DetailsSanitized = reader.GetString(4)
            });
        }
        return list;
    }

    private async Task<PublicationTarget> ExecuteTargetSendAsync(
        PostDocument document,
        ChannelAccount channel,
        PublicationTarget target,
        CancellationToken cancellationToken)
    {
        document.ChannelOverrides.TryGetValue(channel.Id, out ChannelPostOverride? channelOverride);
        List<PostMediaAttachment> attachments = channelOverride?.Media ?? document.Media;
        var resolvedMedia = new List<PostMediaItem>();
        foreach (PostMediaAttachment att in attachments.OrderBy(a => a.SortOrder))
        {
            PostMediaItem? item = _mediaStore.GetById(att.MediaId);
            if (item != null)
            {
                resolvedMedia.Add(item);
            }
        }

        PostValidationSummary validation = TelegramValidationService.Validate(document, resolvedMedia, channelOverride);
        if (!validation.IsValid)
        {
            target.Status = WorkflowStatus.FAILED;
            target.LastErrorCode = "VALIDATION_FAILED";
            target.LastErrorDetails = string.Join("; ", validation.Errors);
            SaveTarget(target);
            RecordEvent(target.Id, "VALIDATION_FAILED", target.LastErrorDetails);
            return target;
        }

        string? botToken = _secretStore.ReadBotToken(channel.BotId);
        if (string.IsNullOrWhiteSpace(botToken))
        {
            target.Status = WorkflowStatus.FAILED;
            target.LastErrorCode = "BOT_TOKEN_MISSING";
            target.LastErrorDetails = "Bot token was not found in Windows Credential Manager.";
            SaveTarget(target);
            RecordEvent(target.Id, "FAILED", target.LastErrorDetails);
            return target;
        }

        TelegramSerializedContent content = TelegramDocumentSerializer.SerializeForTelegram(document, channelOverride);
        List<InlineUrlButton> buttons = channelOverride?.Buttons ?? document.Buttons;
        bool disablePreview = channelOverride?.DisableWebPagePreview ?? document.DisableWebPagePreview;
        bool disableNotify = channelOverride?.DisableNotification ?? document.DisableNotification;

        TelegramSendResult sendResult;
        if (resolvedMedia.Count == 0)
        {
            sendResult = await _botClient.SendMessageAsync(
                botToken, channel.TelegramChatId, content, buttons, disablePreview, disableNotify, cancellationToken).ConfigureAwait(false);
        }
        else if (resolvedMedia.Count == 1)
        {
            string filePath = _mediaStore.ResolveAbsolutePath(resolvedMedia[0].RelativePath);
            sendResult = await _botClient.SendSingleMediaAsync(
                botToken, channel.TelegramChatId, resolvedMedia[0].Kind, filePath, content, buttons, disableNotify, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            var groupItems = resolvedMedia
                .Select(m => (m.Kind, _mediaStore.ResolveAbsolutePath(m.RelativePath)))
                .ToList();
            sendResult = await _botClient.SendMediaGroupAsync(
                botToken, channel.TelegramChatId, groupItems, content, disableNotify, cancellationToken).ConfigureAwait(false);
        }

        if (sendResult.Success)
        {
            target.Status = WorkflowStatus.SENT;
            target.TelegramMessageIds = [.. sendResult.MessageIds];
            target.LastErrorCode = null;
            target.LastErrorDetails = null;
            target.LastSyncUtc = DateTime.UtcNow;
            SaveTarget(target);
            RecordEvent(target.Id, "SENT", $"Published message IDs: {string.Join(", ", target.TelegramMessageIds)}");
        }
        else if (sendResult.IsAmbiguousResult)
        {
            target.Status = WorkflowStatus.UNKNOWN_RESULT;
            target.LastErrorCode = sendResult.ErrorCode;
            target.LastErrorDetails = sendResult.ErrorDescription;
            SaveTarget(target);
            RecordEvent(target.Id, "UNKNOWN_RESULT", "Connection dropped after dispatch. Manual verification required before retry.");
        }
        else
        {
            target.Status = WorkflowStatus.FAILED;
            target.LastErrorCode = sendResult.ErrorCode;
            target.LastErrorDetails = sendResult.ErrorDescription;
            SaveTarget(target);
            RecordEvent(target.Id, "FAILED", $"{sendResult.ErrorCode}: {sendResult.ErrorDescription}");
        }

        return target;
    }

    private static IReadOnlyList<PublicationTarget> ReadTargets(SqliteCommand cmd)
    {
        var list = new List<PublicationTarget>();
        using SqliteDataReader reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            string idsJson = reader.GetString(8);
            List<long> ids = JsonSerializer.Deserialize<List<long>>(idsJson, JsonOptions) ?? [];
            list.Add(new PublicationTarget
            {
                Id = reader.GetString(0),
                PostId = reader.GetString(1),
                PostRevision = reader.GetInt32(2),
                ChannelId = reader.GetString(3),
                ScheduledUtc = DateTime.Parse(reader.GetString(4), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
                Status = (WorkflowStatus)reader.GetInt32(5),
                ServerId = reader.IsDBNull(6) ? null : reader.GetString(6),
                LastSyncUtc = reader.IsDBNull(7) ? null : DateTime.Parse(reader.GetString(7), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
                TelegramMessageIds = ids,
                LastErrorCode = reader.IsDBNull(9) ? null : reader.GetString(9),
                LastErrorDetails = reader.IsDBNull(10) ? null : reader.GetString(10)
            });
        }
        return list;
    }
}
