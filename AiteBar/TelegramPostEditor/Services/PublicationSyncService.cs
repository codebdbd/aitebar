using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AiteBar.TelegramPostEditor.Models;

namespace AiteBar.TelegramPostEditor.Services;

public sealed record ServerPairingResult(string DeviceId, string AccessToken);

internal sealed record ServerPublicationReceipt(string ServerPublicationId, List<ServerPublicationStatus>? Targets);

internal sealed record ServerPublicationStatus(
    string TargetId,
    string ChannelId,
    int Revision,
    DateTime ScheduledAtUtc,
    string Status,
    List<long>? TelegramMessageIds,
    string? LastErrorCode,
    string? LastErrorDetails);

[SupportedOSPlatform("windows6.1")]
public sealed class PublicationSyncService
{
    private sealed record QueueTarget(string ChannelId, DateTime ScheduledAtUtc, object Content, List<string> MediaIds, object Options);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly HttpClient _httpClient;
    private readonly TelegramSecretStore _secretStore;
    private readonly PostMediaStore _mediaStore;

    public PublicationSyncService(
        HttpClient httpClient,
        TelegramSecretStore secretStore,
        PostMediaStore mediaStore)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _secretStore = secretStore ?? throw new ArgumentNullException(nameof(secretStore));
        _mediaStore = mediaStore ?? throw new ArgumentNullException(nameof(mediaStore));
    }

    public async Task<ServerPairingResult> PairAsync(
        string serverBaseUrl,
        string pairingCode,
        string deviceName,
        CancellationToken cancellationToken = default)
    {
        string baseUrl = ValidateServerUrl(serverBaseUrl);
        var payload = new { pairingCode = pairingCode.Trim(), deviceName };
        using var content = new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json");
        using HttpResponseMessage response = await _httpClient.PostAsync($"{baseUrl}/api/v1/pair/confirm", content, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        string body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        using JsonDocument doc = JsonDocument.Parse(body);
        string deviceId = doc.RootElement.GetProperty("deviceId").GetString()!;
        string token = doc.RootElement.GetProperty("accessToken").GetString()!;

        _secretStore.SaveServerToken(deviceId, token);
        return new ServerPairingResult(deviceId, token);
    }

    public async Task SyncChannelAsync(
        string serverBaseUrl,
        string deviceId,
        ChannelAccount channel,
        BotAccount bot,
        bool shareBotTokenWithServer,
        CancellationToken cancellationToken = default)
    {
        string baseUrl = ValidateServerUrl(serverBaseUrl);
        string? botToken = shareBotTokenWithServer
            ? _secretStore.ReadBotToken(bot.Id) ?? throw new InvalidOperationException("The bot token is missing. Reconnect the bot first.")
            : null;

        var payload = new
        {
            botId = GetServerId(deviceId, bot.Id),
            botDisplayName = bot.DisplayName,
            botToken,
            telegramChatId = channel.TelegramChatId,
            displayName = channel.DisplayName,
            username = channel.Username,
            timezone = channel.Timezone
        };

        string channelId = Uri.EscapeDataString(GetServerId(deviceId, channel.Id));
        using HttpRequestMessage req = CreateAuthenticatedRequest(HttpMethod.Put, $"{baseUrl}/api/v1/channels/{channelId}", deviceId);
        req.Content = new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json");
        using HttpResponseMessage resp = await _httpClient.SendAsync(req, cancellationToken).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
    }

    public async Task<string> QueuePostOnServerAsync(
        string serverBaseUrl,
        string deviceId,
        PostDocument document,
        IReadOnlyList<(ChannelAccount Channel, DateTime ScheduledUtc)> schedule,
        CancellationToken cancellationToken = default)
    {
        var receipt = await SubmitScheduledAsync(serverBaseUrl, deviceId, document, schedule, cancellationToken)
            .ConfigureAwait(false);
        return receipt.ServerPublicationId;
    }

    internal async Task<ServerPublicationReceipt> SubmitScheduledAsync(
        string serverBaseUrl,
        string deviceId,
        PostDocument document,
        IReadOnlyList<(ChannelAccount Channel, DateTime ScheduledUtc)> schedule,
        CancellationToken cancellationToken = default)
    {
        string baseUrl = ValidateServerUrl(serverBaseUrl);
        var targetsPayload = new List<QueueTarget>();
        var uploaded = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach ((ChannelAccount channel, DateTime scheduledUtc) in schedule.OrderBy(slot => slot.Channel.Id, StringComparer.Ordinal)
                     .ThenBy(slot => slot.ScheduledUtc))
        {
            document.ChannelOverrides.TryGetValue(channel.Id, out ChannelPostOverride? channelOverride);
            List<PostMediaAttachment> attachments = channelOverride?.Media ?? document.Media;
            var uploadedMediaIds = new List<string>();

            foreach (PostMediaAttachment att in attachments.OrderBy(a => a.SortOrder))
            {
                PostMediaItem item = _mediaStore.GetById(att.MediaId)
                    ?? throw new InvalidOperationException($"Media '{att.MediaId}' was not found locally.");
                if (!uploaded.TryGetValue(item.Id, out string? serverMediaId))
                {
                    serverMediaId = await UploadMediaAsync(baseUrl, deviceId, item, cancellationToken).ConfigureAwait(false);
                    uploaded.Add(item.Id, serverMediaId);
                }
                uploadedMediaIds.Add(serverMediaId);
            }

            TelegramSerializedContent serialized = TelegramDocumentSerializer.SerializeForTelegram(document, channelOverride);
            List<InlineUrlButton> buttons = channelOverride?.Buttons ?? document.Buttons;

            targetsPayload.Add(new QueueTarget(
                GetServerId(deviceId, channel.Id),
                scheduledUtc.ToUniversalTime(),
                new
                {
                    format = "telegram-document-v1",
                    text = serialized.Text,
                    entities = serialized.Entities.Select(e => new { type = e.Type, offset = e.Offset, length = e.Length, url = e.Url, language = e.Language }).ToArray(),
                    buttons = buttons.Select(b => new { text = b.Text, url = b.Url }).ToArray()
                },
                uploadedMediaIds,
                new
                {
                    disableNotification = channelOverride?.DisableNotification ?? document.DisableNotification,
                    disableWebPagePreview = channelOverride?.DisableWebPagePreview ?? document.DisableWebPagePreview
                }));
        }

        byte[] delivery = JsonSerializer.SerializeToUtf8Bytes(new { document.Id, Targets = targetsPayload }, JsonOptions);
        string idempotencyKey = Convert.ToHexString(SHA256.HashData(delivery));
        var bodyObj = new
        {
            clientPostId = document.Id,
            revision = document.Revision,
            targets = targetsPayload.Select(target => new
            {
                targetId = Guid.NewGuid().ToString("N"),
                target.ChannelId,
                target.ScheduledAtUtc,
                target.Content,
                target.MediaIds,
                target.Options
            }).ToArray()
        };

        using HttpRequestMessage req = CreateAuthenticatedRequest(HttpMethod.Post, $"{baseUrl}/api/v1/publications", deviceId);
        req.Headers.Add("Idempotency-Key", idempotencyKey);
        req.Content = new StringContent(JsonSerializer.Serialize(bodyObj, JsonOptions), Encoding.UTF8, "application/json");

        using HttpResponseMessage resp = await _httpClient.SendAsync(req, cancellationToken).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();

        string json = await resp.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return JsonSerializer.Deserialize<ServerPublicationReceipt>(json, JsonOptions)
            ?? throw new InvalidDataException("Server returned no publication receipt.");
    }

    internal async Task<ServerPublicationStatus> GetTargetStatusAsync(
        string serverBaseUrl, string deviceId, string targetId, CancellationToken cancellationToken = default)
    {
        string baseUrl = ValidateServerUrl(serverBaseUrl);
        string id = Uri.EscapeDataString(targetId);
        using HttpRequestMessage request = CreateAuthenticatedRequest(HttpMethod.Get, $"{baseUrl}/api/v1/publications/{id}", deviceId);
        using HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        string json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        ServerPublicationStatus? status = JsonSerializer.Deserialize<ServerPublicationStatus>(json, JsonOptions);
        if (status == null || !string.Equals(status.TargetId, targetId, StringComparison.Ordinal))
            throw new InvalidDataException("Server returned an unexpected publication target.");
        return status;
    }

    internal async Task<ServerPublicationStatus> RescheduleTargetAsync(
        string serverBaseUrl, string deviceId, string targetId, DateTime scheduledUtc, CancellationToken cancellationToken = default)
    {
        string baseUrl = ValidateServerUrl(serverBaseUrl);
        string id = Uri.EscapeDataString(targetId);
        using HttpRequestMessage request = CreateAuthenticatedRequest(HttpMethod.Patch, $"{baseUrl}/api/v1/publications/{id}", deviceId);
        request.Content = new StringContent(JsonSerializer.Serialize(new { scheduledAtUtc = scheduledUtc.ToUniversalTime() }, JsonOptions),
            Encoding.UTF8, "application/json");
        using HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await GetTargetStatusAsync(baseUrl, deviceId, targetId, cancellationToken).ConfigureAwait(false);
    }

    internal static IReadOnlyList<PublicationTarget> ReadScheduledTargets(
        ServerPublicationReceipt receipt, string deviceId, string postId, IReadOnlyList<ChannelAccount> channels)
    {
        var channelsByServerId = channels.ToDictionary(channel => GetServerId(deviceId, channel.Id), StringComparer.Ordinal);
        if (receipt.Targets == null || receipt.Targets.Count != channelsByServerId.Count ||
            receipt.Targets.Select(target => target.ChannelId).Distinct(StringComparer.Ordinal).Count() != channelsByServerId.Count ||
            receipt.Targets.Select(target => target.TargetId).Distinct(StringComparer.Ordinal).Count() != channelsByServerId.Count)
            throw new InvalidDataException("Server did not acknowledge all publication targets.");

        return receipt.Targets.Select(status =>
        {
            if (string.IsNullOrWhiteSpace(status.TargetId) || !channelsByServerId.TryGetValue(status.ChannelId, out var channel))
                throw new InvalidDataException("Server returned an unexpected publication target.");

            var target = new PublicationTarget
            {
                Id = status.TargetId,
                PostId = postId,
                ChannelId = channel.Id,
                ServerId = status.TargetId
            };
            ApplyServerStatus(target, status);
            return target;
        }).ToList();
    }

    internal static void ApplyServerStatus(PublicationTarget target, ServerPublicationStatus status)
    {
        target.Status = Enum.TryParse(status.Status, out WorkflowStatus workflow) && Enum.IsDefined(workflow)
            ? workflow
            : WorkflowStatus.UNKNOWN_RESULT;
        target.PostRevision = status.Revision;
        target.ScheduledUtc = status.ScheduledAtUtc.ToUniversalTime();
        target.TelegramMessageIds = status.TelegramMessageIds ?? [];
        target.LastErrorCode = status.LastErrorCode;
        target.LastErrorDetails = status.LastErrorDetails;
        target.LastSyncUtc = DateTime.UtcNow;
    }

    public static IReadOnlyList<DateTime> DistributeSlots(
        DateTime startLocal,
        int count,
        TimeSpan interval,
        TimeZoneInfo channelTimeZone)
    {
        if (count <= 0) return [];
        var utcTimes = new List<DateTime>(count);
        for (int i = 0; i < count; i++)
        {
            DateTime localSlot = DateTime.SpecifyKind(startLocal.Add(interval * i), DateTimeKind.Unspecified);
            if (channelTimeZone.IsInvalidTime(localSlot))
            {
                localSlot = localSlot.AddHours(1);
            }
            utcTimes.Add(TimeZoneInfo.ConvertTimeToUtc(localSlot, channelTimeZone));
        }
        return utcTimes;
    }

    private async Task<string> UploadMediaAsync(string baseUrl, string deviceId, PostMediaItem item, CancellationToken cancellationToken)
    {
        string path = _mediaStore.ResolveAbsolutePath(item.RelativePath);
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(item.Id), "mediaId");
        form.Add(new StringContent(item.Sha256), "sha256");
        form.Add(new StringContent(((int)item.Kind).ToString()), "kind");

        await using FileStream fs = File.OpenRead(path);
        var streamContent = new StreamContent(fs);
        streamContent.Headers.ContentType = new MediaTypeHeaderValue(item.MimeType);
        form.Add(streamContent, "file", item.FileName);

        using HttpRequestMessage req = CreateAuthenticatedRequest(HttpMethod.Post, $"{baseUrl}/api/v1/media", deviceId);
        req.Content = form;
        using HttpResponseMessage resp = await _httpClient.SendAsync(req, cancellationToken).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        using JsonDocument response = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
        return response.RootElement.GetProperty("mediaId").GetString() ?? throw new InvalidDataException("Server returned no media ID.");
    }

    private HttpRequestMessage CreateAuthenticatedRequest(HttpMethod method, string url, string deviceId)
    {
        string token = _secretStore.ReadServerToken(deviceId)
            ?? throw new InvalidOperationException("Server access token is missing. Pair device first.");
        var req = new HttpRequestMessage(method, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return req;
    }

    private static string GetServerId(string deviceId, string localId) => $"{deviceId}:{localId}";

    internal static string ValidateServerUrl(string serverBaseUrl)
    {
        if (!Uri.TryCreate(serverBaseUrl, UriKind.Absolute, out Uri? uri))
        {
            throw new ArgumentException("Invalid server URL.", nameof(serverBaseUrl));
        }

        if (uri.Scheme != Uri.UriSchemeHttps && (uri.Scheme != Uri.UriSchemeHttp || !uri.IsLoopback))
        {
            throw new InvalidOperationException("HTTPS is required for non-local server connections.");
        }

        if (uri.UserInfo.Length > 0 || uri.Query.Length > 0 || uri.Fragment.Length > 0)
        {
            throw new ArgumentException("Server URL must not contain credentials, query parameters, or a fragment.", nameof(serverBaseUrl));
        }

        return uri.AbsoluteUri.TrimEnd('/');
    }
}
