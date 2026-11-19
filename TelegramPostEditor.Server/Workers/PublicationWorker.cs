using System.Text.Json;
using Microsoft.Data.Sqlite;
using TelegramPostEditor.Server.Persistence;
using TelegramPostEditor.Server.Security;
using TelegramPostEditor.Server.Services;

namespace TelegramPostEditor.Server.Workers;

public sealed class PublicationWorker : BackgroundService
{
    private const int MaxAttempts = 5;
    private static readonly TimeSpan LeaseDuration = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);

    private readonly ServerDbMigrator _migrator;
    private readonly SecretProtector _protector;
    private readonly TelegramDispatcher _dispatcher;
    private readonly string _mediaRoot;
    private readonly ILogger<PublicationWorker> _logger;
    private readonly string _workerId = $"worker-{Environment.MachineName}-{Guid.NewGuid():N}"[..24];

    public PublicationWorker(
        ServerDbMigrator migrator,
        SecretProtector protector,
        TelegramDispatcher dispatcher,
        string mediaRoot,
        ILogger<PublicationWorker> logger)
    {
        _migrator = migrator;
        _protector = protector;
        _dispatcher = dispatcher;
        _mediaRoot = mediaRoot;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        RecoverExpiredLeases();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessOneDueTargetAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled error in PublicationWorker loop.");
            }

            await Task.Delay(PollInterval, stoppingToken);
        }
    }

    public void RecoverExpiredLeases()
    {
        using SqliteConnection conn = _migrator.OpenConnection();
        using SqliteCommand cmd = conn.CreateCommand();
        string now = DateTime.UtcNow.ToString("O");
        cmd.CommandText = """
            UPDATE publication_targets
            SET status = 'UNKNOWN_RESULT',
                lease_owner = NULL,
                lease_until_utc = NULL,
                last_error_code = 'LEASE_EXPIRED_SENDING',
                last_error_details_sanitized = 'Server restarted while target was SENDING. Manual check required to avoid duplicates.',
                updated_utc = $now
            WHERE status = 'SENDING' AND (lease_until_utc IS NULL OR lease_until_utc <= $now);
            """;
        cmd.Parameters.AddWithValue("$now", now);
        cmd.ExecuteNonQuery();
    }

    public async Task<bool> ProcessOneDueTargetAsync(CancellationToken cancellationToken)
    {
        DueTargetRecord? due = TryAcquireNextDueTarget();
        if (due == null)
        {
            return false;
        }

        DateTime startedUtc = DateTime.UtcNow;
        DispatchOutcome outcome;
        try
        {
            string botToken = _protector.Decrypt(due.EncryptedBotToken, due.BotId);
            ServerPostContentDto content = JsonSerializer.Deserialize<ServerPostContentDto>(due.ContentSnapshotJson)
                ?? throw new InvalidOperationException("Invalid content snapshot.");
            List<string> mediaIds = JsonSerializer.Deserialize<List<string>>(due.MediaHashesJson) ?? [];
            ServerTargetOptionsDto options = JsonSerializer.Deserialize<ServerTargetOptionsDto>(due.OptionsJson) ?? new ServerTargetOptionsDto();

            var mediaFiles = new List<(string FilePath, int Kind)>();
            foreach (string mediaId in mediaIds)
            {
                (string relPath, int kind)? info = ResolveMedia(due.DeviceId, mediaId);
                if (info == null)
                {
                    outcome = new DispatchOutcome(false, [], false, false, null, "MEDIA_MISSING", $"Media '{mediaId}' missing on server.");
                    await FinalizeOutcomeAsync(due, startedUtc, outcome);
                    return true;
                }
                string absPath = Path.GetFullPath(Path.Combine(_mediaRoot, info.Value.relPath));
                string relativePath = Path.GetRelativePath(_mediaRoot, absPath);
                if (Path.IsPathRooted(relativePath) || relativePath == ".." ||
                    relativePath.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
                    !File.Exists(absPath))
                {
                    outcome = new DispatchOutcome(false, [], false, false, null, "MEDIA_FILE_MISSING", $"Media file '{mediaId}' missing on disk.");
                    await FinalizeOutcomeAsync(due, startedUtc, outcome);
                    return true;
                }
                mediaFiles.Add((absPath, info.Value.kind));
            }

            outcome = await _dispatcher.DispatchAsync(botToken, due.TelegramChatId, content, mediaFiles, options, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            outcome = new DispatchOutcome(false, [], false, false, null, "DISPATCH_ERROR", ex.Message);
        }

        await FinalizeOutcomeAsync(due, startedUtc, outcome);
        return true;
    }

    private DueTargetRecord? TryAcquireNextDueTarget()
    {
        using SqliteConnection conn = _migrator.OpenConnection();
        using SqliteTransaction tx = conn.BeginTransaction();

        string now = DateTime.UtcNow.ToString("O");
        string leaseUntil = DateTime.UtcNow.Add(LeaseDuration).ToString("O");

        using SqliteCommand selectCmd = conn.CreateCommand();
        selectCmd.Transaction = tx;
        selectCmd.CommandText = """
            SELECT pt.id, pt.device_id, pt.telegram_chat_id, pt.immutable_content_snapshot,
                   pt.media_hashes_json, pt.options_json, pt.attempt_count,
                   b.id, b.encrypted_token
            FROM publication_targets pt
            JOIN channels c ON c.id = pt.channel_id AND c.device_id = pt.device_id
            JOIN bots b ON b.id = c.bot_id AND b.device_id = pt.device_id
            WHERE (pt.status = 'QUEUED_SERVER' AND pt.scheduled_at_utc <= $now)
               OR (pt.status = 'RETRY_WAIT' AND pt.next_attempt_utc <= $now)
            ORDER BY pt.scheduled_at_utc ASC
            LIMIT 1;
            """;
        selectCmd.Parameters.AddWithValue("$now", now);

        DueTargetRecord? record = null;
        using (SqliteDataReader reader = selectCmd.ExecuteReader())
        {
            if (reader.Read())
            {
                record = new DueTargetRecord(
                    reader.GetString(0),
                    reader.GetString(1),
                    reader.GetInt64(2),
                    reader.GetString(3),
                    reader.GetString(4),
                    reader.GetString(5),
                    reader.GetInt32(6),
                    reader.GetString(7),
                    reader.GetString(8));
            }
        }

        if (record == null)
        {
            return null;
        }

        using SqliteCommand updateCmd = conn.CreateCommand();
        updateCmd.Transaction = tx;
        updateCmd.CommandText = """
            UPDATE publication_targets
            SET status = 'SENDING',
                lease_owner = $worker,
                lease_until_utc = $leaseUntil,
                attempt_count = attempt_count + 1,
                updated_utc = $now
            WHERE id = $id AND status IN ('QUEUED_SERVER', 'RETRY_WAIT');
            """;
        updateCmd.Parameters.AddWithValue("$worker", _workerId);
        updateCmd.Parameters.AddWithValue("$leaseUntil", leaseUntil);
        updateCmd.Parameters.AddWithValue("$now", now);
        updateCmd.Parameters.AddWithValue("$id", record.TargetId);

        if (updateCmd.ExecuteNonQuery() == 0)
        {
            tx.Rollback();
            return null;
        }

        tx.Commit();
        return record;
    }

    private (string RelativePath, int Kind)? ResolveMedia(string deviceId, string mediaId)
    {
        using SqliteConnection conn = _migrator.OpenConnection();
        using SqliteCommand cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT relative_path, kind FROM uploaded_media WHERE id = $id AND device_id = $dev;";
        cmd.Parameters.AddWithValue("$id", mediaId);
        cmd.Parameters.AddWithValue("$dev", deviceId);
        using SqliteDataReader reader = cmd.ExecuteReader();
        return reader.Read() ? (reader.GetString(0), reader.GetInt32(1)) : null;
    }

    private Task FinalizeOutcomeAsync(DueTargetRecord due, DateTime startedUtc, DispatchOutcome outcome)
    {
        DateTime now = DateTime.UtcNow;
        string nextStatus;
        string? nextAttemptUtc = null;
        string eventType;
        string details;

        if (outcome.Success)
        {
            nextStatus = "SENT";
            eventType = "SENT";
            details = $"Published message IDs: {string.Join(", ", outcome.MessageIds)}";
        }
        else if (outcome.IsAmbiguousResult)
        {
            nextStatus = "UNKNOWN_RESULT";
            eventType = "UNKNOWN_RESULT";
            details = outcome.ErrorDetailsSanitized ?? "Connection dropped after dispatch.";
        }
        else if (outcome.IsTransient && due.AttemptCount + 1 < MaxAttempts)
        {
            nextStatus = "RETRY_WAIT";
            TimeSpan delay = outcome.RetryAfter ?? ComputeBackoffWithJitter(due.AttemptCount + 1);
            nextAttemptUtc = now.Add(delay).ToString("O");
            eventType = "RETRY_SCHEDULED";
            details = $"Transient error ({outcome.ErrorCode}): {outcome.ErrorDetailsSanitized}. Retry at {nextAttemptUtc}";
        }
        else
        {
            nextStatus = "FAILED";
            eventType = "FAILED";
            details = $"{outcome.ErrorCode}: {outcome.ErrorDetailsSanitized}";
        }

        using SqliteConnection conn = _migrator.OpenConnection();
        using SqliteTransaction tx = conn.BeginTransaction();

        using (SqliteCommand cmd = conn.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = """
                UPDATE publication_targets
                SET status = $status,
                    lease_owner = NULL,
                    lease_until_utc = NULL,
                    next_attempt_utc = $nextAttempt,
                    telegram_message_ids_json = CASE WHEN $hasIds = 1 THEN $idsJson ELSE telegram_message_ids_json END,
                    last_error_code = $errCode,
                    last_error_details_sanitized = $errDetails,
                    updated_utc = $now
                WHERE id = $id;
                """;
            cmd.Parameters.AddWithValue("$status", nextStatus);
            cmd.Parameters.AddWithValue("$nextAttempt", (object?)nextAttemptUtc ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$hasIds", outcome.Success ? 1 : 0);
            cmd.Parameters.AddWithValue("$idsJson", JsonSerializer.Serialize(outcome.MessageIds));
            cmd.Parameters.AddWithValue("$errCode", (object?)outcome.ErrorCode ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$errDetails", (object?)outcome.ErrorDetailsSanitized ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$now", now.ToString("O"));
            cmd.Parameters.AddWithValue("$id", due.TargetId);
            cmd.ExecuteNonQuery();
        }

        using (SqliteCommand attCmd = conn.CreateCommand())
        {
            attCmd.Transaction = tx;
            attCmd.CommandText = """
                INSERT INTO send_attempts (id, target_id, started_utc, finished_utc, success, error_code, details_sanitized)
                VALUES ($id, $targetId, $started, $finished, $success, $errCode, $details);
                """;
            attCmd.Parameters.AddWithValue("$id", Guid.NewGuid().ToString("N"));
            attCmd.Parameters.AddWithValue("$targetId", due.TargetId);
            attCmd.Parameters.AddWithValue("$started", startedUtc.ToString("O"));
            attCmd.Parameters.AddWithValue("$finished", now.ToString("O"));
            attCmd.Parameters.AddWithValue("$success", outcome.Success ? 1 : 0);
            attCmd.Parameters.AddWithValue("$errCode", (object?)outcome.ErrorCode ?? DBNull.Value);
            attCmd.Parameters.AddWithValue("$details", (object?)outcome.ErrorDetailsSanitized ?? DBNull.Value);
            attCmd.ExecuteNonQuery();
        }

        using (SqliteCommand evCmd = conn.CreateCommand())
        {
            evCmd.Transaction = tx;
            evCmd.CommandText = """
                INSERT INTO publication_events (target_id, device_id, event_utc, event_type, details_sanitized)
                VALUES ($targetId, $deviceId, $utc, $type, $details);
                """;
            evCmd.Parameters.AddWithValue("$targetId", due.TargetId);
            evCmd.Parameters.AddWithValue("$deviceId", due.DeviceId);
            evCmd.Parameters.AddWithValue("$utc", now.ToString("O"));
            evCmd.Parameters.AddWithValue("$type", eventType);
            evCmd.Parameters.AddWithValue("$details", details);
            evCmd.ExecuteNonQuery();
        }

        tx.Commit();
        return Task.CompletedTask;
    }

    private static TimeSpan ComputeBackoffWithJitter(int attempt)
    {
        double baseSeconds = Math.Min(300, Math.Pow(2, attempt) * 5);
        int jitter = Random.Shared.Next(1, 6);
        return TimeSpan.FromSeconds(baseSeconds + jitter);
    }

    private sealed record DueTargetRecord(
        string TargetId,
        string DeviceId,
        long TelegramChatId,
        string ContentSnapshotJson,
        string MediaHashesJson,
        string OptionsJson,
        int AttemptCount,
        string BotId,
        string EncryptedBotToken);
}
