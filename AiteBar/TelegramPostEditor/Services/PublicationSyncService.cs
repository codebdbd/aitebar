using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.Versioning;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AiteBar.TelegramPostEditor.Models;

namespace AiteBar.TelegramPostEditor.Services;

public sealed record ServerPairingResult(string DeviceId, string AccessToken);

[SupportedOSPlatform("windows6.1")]
public sealed class PublicationSyncService
{
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
        string? botToken = shareBotTokenWithServer ? _secretStore.ReadBotToken(bot.Id) : null;

        var payload = new
        {
            botId = bot.Id,
            botDisplayName = bot.DisplayName,
            botToken,
            telegramChatId = channel.TelegramChatId,
            displayName = channel.DisplayName,
            username = channel.Username,
            timezone = channel.Timezone
        };

        using HttpRequestMessage req = CreateAuthenticatedRequest(HttpMethod.Put, $"{baseUrl}/api/v1/channels/{channel.Id}", deviceId);
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
        string baseUrl = ValidateServerUrl(serverBaseUrl);
        var targetsPayload = new List<object>();

        foreach ((ChannelAccount channel, DateTime scheduledUtc) in schedule)
        {
            document.ChannelOverrides.TryGetValue(channel.Id, out ChannelPostOverride? channelOverride);
            List<PostMediaAttachment> attachments = channelOverride?.Media ?? document.Media;
            var uploadedMediaIds = new List<string>();

            foreach (PostMediaAttachment att in attachments.OrderBy(a => a.SortOrder))
            {
                PostMediaItem item = _mediaStore.GetById(att.MediaId)
                    ?? throw new InvalidOperationException($"Media '{att.MediaId}' was not found locally.");
                await UploadMediaAsync(baseUrl, deviceId, item, cancellationToken).ConfigureAwait(false);
                uploadedMediaIds.Add(item.Id);
            }

            TelegramSerializedContent serialized = TelegramDocumentSerializer.SerializeForTelegram(document, channelOverride);
            List<InlineUrlButton> buttons = channelOverride?.Buttons ?? document.Buttons;

            targetsPayload.Add(new
            {
                targetId = Guid.NewGuid().ToString("N"),
                channelId = channel.Id,
                scheduledAtUtc = scheduledUtc.ToUniversalTime(),
                content = new
                {
                    format = "telegram-document-v1",
                    text = serialized.Text,
                    entities = serialized.Entities.Select(e => new { type = e.Type, offset = e.Offset, length = e.Length, url = e.Url, language = e.Language }).ToArray(),
                    buttons = buttons.Select(b => new { text = b.Text, url = b.Url }).ToArray()
                },
                mediaIds = uploadedMediaIds,
                options = new
                {
                    disableNotification = channelOverride?.DisableNotification ?? document.DisableNotification,
                    disableWebPagePreview = channelOverride?.DisableWebPagePreview ?? document.DisableWebPagePreview
                }
            });
        }

        string idempotencyKey = $"{document.Id}:rev{document.Revision}:{Guid.NewGuid():N}";
        var bodyObj = new
        {
            clientPostId = document.Id,
            revision = document.Revision,
            targets = targetsPayload
        };

        using HttpRequestMessage req = CreateAuthenticatedRequest(HttpMethod.Post, $"{baseUrl}/api/v1/publications", deviceId);
        req.Headers.Add("Idempotency-Key", idempotencyKey);
        req.Content = new StringContent(JsonSerializer.Serialize(bodyObj, JsonOptions), Encoding.UTF8, "application/json");

        using HttpResponseMessage resp = await _httpClient.SendAsync(req, cancellationToken).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();

        string json = await resp.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        using JsonDocument doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("serverPublicationId").GetString() ?? string.Empty;
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

    private async Task UploadMediaAsync(string baseUrl, string deviceId, PostMediaItem item, CancellationToken cancellationToken)
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
    }

    private HttpRequestMessage CreateAuthenticatedRequest(HttpMethod method, string url, string deviceId)
    {
        string token = _secretStore.ReadServerToken(deviceId)
            ?? throw new InvalidOperationException("Server access token is missing. Pair device first.");
        var req = new HttpRequestMessage(method, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return req;
    }

    private static string ValidateServerUrl(string serverBaseUrl)
    {
        if (!Uri.TryCreate(serverBaseUrl, UriKind.Absolute, out Uri? uri))
        {
            throw new ArgumentException("Invalid server URL.", nameof(serverBaseUrl));
        }

        if (uri.Scheme != Uri.UriSchemeHttps && !uri.IsLoopback)
        {
            throw new InvalidOperationException("HTTPS is required for non-local server connections.");
        }

        return serverBaseUrl.TrimEnd('/');
    }
}
