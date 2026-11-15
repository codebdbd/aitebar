using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using AiteBar.TelegramPostEditor.Models;

namespace AiteBar.TelegramPostEditor.Services;

public sealed record TelegramBotInfo(long Id, string Username, string FirstName);
public sealed record TelegramChatInfo(long Id, string? Title, string? Username, string Type);
public sealed record TelegramChatMemberInfo(string Status, bool CanPostMessages, bool CanEditMessages, bool CanDeleteMessages);

public sealed record TelegramSendResult(
    bool Success,
    IReadOnlyList<long> MessageIds,
    bool IsTransient,
    bool IsAmbiguousResult,
    bool IsPermissionOrAuthError,
    TimeSpan? RetryAfter,
    string? ErrorCode,
    string? ErrorDescription);

public sealed class TelegramBotClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _httpClient;
    private readonly string _baseApiUrl;

    public TelegramBotClient(HttpClient? httpClient = null, string baseApiUrl = "https://api.telegram.org")
    {
        _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(45) };
        _baseApiUrl = baseApiUrl.TrimEnd('/');
    }

    public async Task<TelegramBotInfo> GetMeAsync(string botToken, CancellationToken cancellationToken = default)
    {
        JsonElement result = await SendJsonAsync(botToken, "getMe", new { }, cancellationToken).ConfigureAwait(false);
        return new TelegramBotInfo(
            result.GetProperty("id").GetInt64(),
            result.TryGetProperty("username", out JsonElement u) ? u.GetString() ?? string.Empty : string.Empty,
            result.TryGetProperty("first_name", out JsonElement fn) ? fn.GetString() ?? string.Empty : string.Empty);
    }

    public async Task<TelegramChatInfo> GetChatAsync(string botToken, string chatIdOrUsername, CancellationToken cancellationToken = default)
    {
        JsonElement result = await SendJsonAsync(botToken, "getChat", new { chat_id = chatIdOrUsername }, cancellationToken).ConfigureAwait(false);
        return new TelegramChatInfo(
            result.GetProperty("id").GetInt64(),
            result.TryGetProperty("title", out JsonElement t) ? t.GetString() : null,
            result.TryGetProperty("username", out JsonElement u) ? u.GetString() : null,
            result.TryGetProperty("type", out JsonElement tp) ? tp.GetString() ?? "channel" : "channel");
    }

    public async Task<TelegramChatMemberInfo> GetChatMemberAsync(string botToken, long chatId, long userId, CancellationToken cancellationToken = default)
    {
        JsonElement result = await SendJsonAsync(botToken, "getChatMember", new { chat_id = chatId, user_id = userId }, cancellationToken).ConfigureAwait(false);
        string status = result.TryGetProperty("status", out JsonElement s) ? s.GetString() ?? string.Empty : string.Empty;
        bool isCreator = string.Equals(status, "creator", StringComparison.OrdinalIgnoreCase);
        bool isAdmin = string.Equals(status, "administrator", StringComparison.OrdinalIgnoreCase);
        bool isMember = string.Equals(status, "member", StringComparison.OrdinalIgnoreCase);
        bool canPost = isCreator || (result.TryGetProperty("can_post_messages", out JsonElement cp) && cp.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? cp.GetBoolean()
            : (isAdmin || isMember));
        bool canEdit = isCreator || (result.TryGetProperty("can_edit_messages", out JsonElement ce) && ce.GetBoolean());
        bool canDelete = isCreator || (result.TryGetProperty("can_delete_messages", out JsonElement cd) && cd.GetBoolean());
        return new TelegramChatMemberInfo(status, canPost, canEdit, canDelete);
    }

    public async Task<TelegramSendResult> SendMessageAsync(
        string botToken,
        long chatId,
        TelegramSerializedContent content,
        IReadOnlyList<InlineUrlButton>? buttons = null,
        bool disableWebPagePreview = false,
        bool disableNotification = false,
        CancellationToken cancellationToken = default)
    {
        var payload = new Dictionary<string, object?>
        {
            ["chat_id"] = chatId,
            ["text"] = content.Text,
            ["entities"] = content.Entities.Count > 0 ? content.Entities.Select(MapEntity).ToArray() : null,
            ["disable_notification"] = disableNotification ? true : null,
            ["link_preview_options"] = disableWebPagePreview ? new { is_disabled = true } : null,
            ["reply_markup"] = BuildReplyMarkup(buttons)
        };

        return await ExecuteSendAsync(botToken, "sendMessage", payload, cancellationToken).ConfigureAwait(false);
    }

    public async Task<TelegramSendResult> SendSingleMediaAsync(
        string botToken,
        long chatId,
        MediaKind kind,
        string filePath,
        TelegramSerializedContent? caption,
        IReadOnlyList<InlineUrlButton>? buttons = null,
        bool disableNotification = false,
        CancellationToken cancellationToken = default)
    {
        string method = kind switch
        {
            MediaKind.Photo => "sendPhoto",
            MediaKind.Video => "sendVideo",
            MediaKind.Animation => "sendAnimation",
            _ => "sendDocument"
        };
        string fieldName = kind switch
        {
            MediaKind.Photo => "photo",
            MediaKind.Video => "video",
            MediaKind.Animation => "animation",
            _ => "document"
        };

        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(chatId.ToString()), "chat_id");
        if (caption != null && !string.IsNullOrEmpty(caption.Text))
        {
            form.Add(new StringContent(caption.Text), "caption");
            if (caption.Entities.Count > 0)
            {
                form.Add(new StringContent(JsonSerializer.Serialize(caption.Entities.Select(MapEntity), JsonOptions)), "caption_entities");
            }
        }
        if (disableNotification)
        {
            form.Add(new StringContent("true"), "disable_notification");
        }
        object? markup = BuildReplyMarkup(buttons);
        if (markup != null)
        {
            form.Add(new StringContent(JsonSerializer.Serialize(markup, JsonOptions)), "reply_markup");
        }

        await using FileStream stream = File.OpenRead(filePath);
        var fileContent = new StreamContent(stream);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        form.Add(fileContent, fieldName, Path.GetFileName(filePath));

        return await ExecuteMultipartSendAsync(botToken, method, form, isArrayResult: false, cancellationToken).ConfigureAwait(false);
    }

    public async Task<TelegramSendResult> SendMediaGroupAsync(
        string botToken,
        long chatId,
        IReadOnlyList<(MediaKind Kind, string FilePath)> items,
        TelegramSerializedContent? caption,
        bool disableNotification = false,
        CancellationToken cancellationToken = default)
    {
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(chatId.ToString()), "chat_id");
        if (disableNotification)
        {
            form.Add(new StringContent("true"), "disable_notification");
        }

        var mediaEntries = new List<Dictionary<string, object?>>();
        var streams = new List<FileStream>();
        try
        {
            for (int i = 0; i < items.Count; i++)
            {
                (MediaKind kind, string filePath) = items[i];
                string attachName = $"file{i}";
                string type = kind switch
                {
                    MediaKind.Photo => "photo",
                    MediaKind.Video => "video",
                    _ => "document"
                };

                var entry = new Dictionary<string, object?>
                {
                    ["type"] = type,
                    ["media"] = $"attach://{attachName}"
                };

                if (i == 0 && caption != null && !string.IsNullOrEmpty(caption.Text))
                {
                    entry["caption"] = caption.Text;
                    if (caption.Entities.Count > 0)
                    {
                        entry["caption_entities"] = caption.Entities.Select(MapEntity).ToArray();
                    }
                }

                mediaEntries.Add(entry);

                FileStream fs = File.OpenRead(filePath);
                streams.Add(fs);
                var fileContent = new StreamContent(fs);
                fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                form.Add(fileContent, attachName, Path.GetFileName(filePath));
            }

            form.Add(new StringContent(JsonSerializer.Serialize(mediaEntries, JsonOptions)), "media");
            return await ExecuteMultipartSendAsync(botToken, "sendMediaGroup", form, isArrayResult: true, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            foreach (FileStream fs in streams)
            {
                await fs.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    public Task<TelegramSendResult> EditMessageTextAsync(
        string botToken,
        long chatId,
        long messageId,
        TelegramSerializedContent content,
        IReadOnlyList<InlineUrlButton>? buttons = null,
        bool disableWebPagePreview = false,
        CancellationToken cancellationToken = default)
    {
        var payload = new Dictionary<string, object?>
        {
            ["chat_id"] = chatId,
            ["message_id"] = messageId,
            ["text"] = content.Text,
            ["entities"] = content.Entities.Count > 0 ? content.Entities.Select(MapEntity).ToArray() : null,
            ["link_preview_options"] = disableWebPagePreview ? new { is_disabled = true } : null,
            ["reply_markup"] = BuildReplyMarkup(buttons)
        };

        return ExecuteSendAsync(botToken, "editMessageText", payload, cancellationToken);
    }

    public Task<TelegramSendResult> DeleteMessageAsync(
        string botToken,
        long chatId,
        long messageId,
        CancellationToken cancellationToken = default)
    {
        var payload = new Dictionary<string, object?>
        {
            ["chat_id"] = chatId,
            ["message_id"] = messageId
        };

        return ExecuteSendAsync(botToken, "deleteMessage", payload, cancellationToken);
    }

    private async Task<JsonElement> SendJsonAsync(string botToken, string method, object payload, CancellationToken cancellationToken)
    {
        string url = $"{_baseApiUrl}/bot{botToken}/{method}";
        using var content = new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json");
        using HttpResponseMessage response = await _httpClient.PostAsync(url, content, cancellationToken).ConfigureAwait(false);
        string body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        using JsonDocument doc = JsonDocument.Parse(body);
        JsonElement root = doc.RootElement;
        if (!root.TryGetProperty("ok", out JsonElement okElem) || !okElem.GetBoolean())
        {
            int code = root.TryGetProperty("error_code", out JsonElement ec) ? ec.GetInt32() : (int)response.StatusCode;
            string desc = root.TryGetProperty("description", out JsonElement d) ? d.GetString() ?? "Telegram API error" : "Telegram API error";
            throw new InvalidOperationException($"Telegram API {code}: {desc}");
        }

        return root.GetProperty("result").Clone();
    }

    private async Task<TelegramSendResult> ExecuteSendAsync(string botToken, string method, object payload, CancellationToken cancellationToken)
    {
        string url = $"{_baseApiUrl}/bot{botToken}/{method}";
        using var content = new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json");
        try
        {
            using HttpResponseMessage response = await _httpClient.PostAsync(url, content, cancellationToken).ConfigureAwait(false);
            string body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            return ParseSendResponse(response.StatusCode, body, isArrayResult: false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException)
        {
            // Connection dropped or timed out after request dispatch -> ambiguous result (no blind retry).
            return new TelegramSendResult(false, [], IsTransient: false, IsAmbiguousResult: true, IsPermissionOrAuthError: false, null, "NETWORK_AMBIGUOUS", SanitizeError(ex.Message, botToken));
        }
    }

    private async Task<TelegramSendResult> ExecuteMultipartSendAsync(
        string botToken,
        string method,
        MultipartFormDataContent form,
        bool isArrayResult,
        CancellationToken cancellationToken)
    {
        string url = $"{_baseApiUrl}/bot{botToken}/{method}";
        try
        {
            using HttpResponseMessage response = await _httpClient.PostAsync(url, form, cancellationToken).ConfigureAwait(false);
            string body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            return ParseSendResponse(response.StatusCode, body, isArrayResult);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException)
        {
            return new TelegramSendResult(false, [], IsTransient: false, IsAmbiguousResult: true, IsPermissionOrAuthError: false, null, "NETWORK_AMBIGUOUS", SanitizeError(ex.Message, botToken));
        }
    }

    private static TelegramSendResult ParseSendResponse(HttpStatusCode statusCode, string body, bool isArrayResult)
    {
        using JsonDocument doc = JsonDocument.Parse(body);
        JsonElement root = doc.RootElement;
        if (root.TryGetProperty("ok", out JsonElement okElem) && okElem.GetBoolean())
        {
            var ids = new List<long>();
            if (root.TryGetProperty("result", out JsonElement result))
            {
                if (isArrayResult && result.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement item in result.EnumerateArray())
                    {
                        if (item.TryGetProperty("message_id", out JsonElement mid))
                        {
                            ids.Add(mid.GetInt64());
                        }
                    }
                }
                else if (result.ValueKind == JsonValueKind.Object && result.TryGetProperty("message_id", out JsonElement mid))
                {
                    ids.Add(mid.GetInt64());
                }
            }

            return new TelegramSendResult(true, ids, false, false, false, null, null, null);
        }

        int errorCode = root.TryGetProperty("error_code", out JsonElement ec) ? ec.GetInt32() : (int)statusCode;
        string desc = root.TryGetProperty("description", out JsonElement d) ? d.GetString() ?? "Telegram error" : "Telegram error";
        TimeSpan? retryAfter = null;
        if (root.TryGetProperty("parameters", out JsonElement p) &&
            p.TryGetProperty("retry_after", out JsonElement ra) &&
            ra.TryGetInt32(out int seconds))
        {
            retryAfter = TimeSpan.FromSeconds(Math.Max(1, seconds));
        }

        bool is429 = errorCode == 429;
        bool is5xx = errorCode >= 500;
        bool isAuthOrPerm = errorCode is 401 or 403;

        return new TelegramSendResult(
            Success: false,
            MessageIds: [],
            IsTransient: is429 || is5xx,
            IsAmbiguousResult: false,
            IsPermissionOrAuthError: isAuthOrPerm,
            RetryAfter: retryAfter,
            ErrorCode: errorCode.ToString(),
            ErrorDescription: desc);
    }

    private static object MapEntity(TelegramMessageEntity entity) => new
    {
        type = entity.Type,
        offset = entity.Offset,
        length = entity.Length,
        url = entity.Url,
        language = entity.Language
    };

    private static object? BuildReplyMarkup(IReadOnlyList<InlineUrlButton>? buttons)
    {
        if (buttons == null || buttons.Count == 0)
        {
            return null;
        }

        var rows = buttons
            .Where(b => !string.IsNullOrWhiteSpace(b.Text) && !string.IsNullOrWhiteSpace(b.Url))
            .Select(b => new[] { new { text = b.Text.Trim(), url = b.Url.Trim() } })
            .ToArray();

        return rows.Length > 0 ? new { inline_keyboard = rows } : null;
    }

    private static string SanitizeError(string message, string botToken) =>
        string.IsNullOrEmpty(botToken) ? message : message.Replace(botToken, "***", StringComparison.Ordinal);
}
