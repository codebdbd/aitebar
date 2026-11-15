using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TelegramPostEditor.Server.Services;

public sealed record DispatchOutcome(
    bool Success,
    List<long> MessageIds,
    bool IsTransient,
    bool IsAmbiguousResult,
    TimeSpan? RetryAfter,
    string? ErrorCode,
    string? ErrorDetailsSanitized);

public sealed class TelegramDispatcher
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _httpClient;
    private readonly string _baseApiUrl;

    public TelegramDispatcher(HttpClient httpClient, string baseApiUrl = "https://api.telegram.org")
    {
        _httpClient = httpClient;
        _baseApiUrl = baseApiUrl.TrimEnd('/');
    }

    public async Task<DispatchOutcome> DispatchAsync(
        string botToken,
        long chatId,
        ServerPostContentDto content,
        IReadOnlyList<(string FilePath, int Kind)> mediaFiles,
        ServerTargetOptionsDto options,
        CancellationToken cancellationToken)
    {
        try
        {
            if (mediaFiles.Count == 0)
            {
                return await SendMessageAsync(botToken, chatId, content, options, cancellationToken);
            }

            if (mediaFiles.Count == 1)
            {
                return await SendSingleMediaAsync(botToken, chatId, mediaFiles[0], content, options, cancellationToken);
            }

            return await SendMediaGroupAsync(botToken, chatId, mediaFiles, content, options, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException)
        {
            return new DispatchOutcome(
                false, [], IsTransient: false, IsAmbiguousResult: true, null,
                "NETWORK_AMBIGUOUS", Sanitize(ex.Message, botToken));
        }
    }

    private async Task<DispatchOutcome> SendMessageAsync(
        string botToken,
        long chatId,
        ServerPostContentDto content,
        ServerTargetOptionsDto options,
        CancellationToken cancellationToken)
    {
        var payload = new Dictionary<string, object?>
        {
            ["chat_id"] = chatId,
            ["text"] = content.Text,
            ["entities"] = content.Entities is { Count: > 0 } ? content.Entities : null,
            ["disable_notification"] = options.DisableNotification ? true : null,
            ["link_preview_options"] = options.DisableWebPagePreview ? new { is_disabled = true } : null,
            ["reply_markup"] = BuildMarkup(content.Buttons)
        };

        string url = $"{_baseApiUrl}/bot{botToken}/sendMessage";
        using var reqContent = new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json");
        using HttpResponseMessage response = await _httpClient.PostAsync(url, reqContent, cancellationToken);
        string body = await response.Content.ReadAsStringAsync(cancellationToken);
        return ParseResponse(response.StatusCode, body, isArray: false);
    }

    private async Task<DispatchOutcome> SendSingleMediaAsync(
        string botToken,
        long chatId,
        (string FilePath, int Kind) media,
        ServerPostContentDto content,
        ServerTargetOptionsDto options,
        CancellationToken cancellationToken)
    {
        (string method, string field) = media.Kind switch
        {
            0 => ("sendPhoto", "photo"),
            1 => ("sendVideo", "video"),
            2 => ("sendAnimation", "animation"),
            _ => ("sendDocument", "document")
        };

        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(chatId.ToString()), "chat_id");
        if (!string.IsNullOrEmpty(content.Text))
        {
            form.Add(new StringContent(content.Text), "caption");
            if (content.Entities is { Count: > 0 })
            {
                form.Add(new StringContent(JsonSerializer.Serialize(content.Entities, JsonOptions)), "caption_entities");
            }
        }
        if (options.DisableNotification)
        {
            form.Add(new StringContent("true"), "disable_notification");
        }
        object? markup = BuildMarkup(content.Buttons);
        if (markup != null)
        {
            form.Add(new StringContent(JsonSerializer.Serialize(markup, JsonOptions)), "reply_markup");
        }

        await using FileStream fs = File.OpenRead(media.FilePath);
        var streamContent = new StreamContent(fs);
        streamContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        form.Add(streamContent, field, Path.GetFileName(media.FilePath));

        string url = $"{_baseApiUrl}/bot{botToken}/{method}";
        using HttpResponseMessage response = await _httpClient.PostAsync(url, form, cancellationToken);
        string body = await response.Content.ReadAsStringAsync(cancellationToken);
        return ParseResponse(response.StatusCode, body, isArray: false);
    }

    private async Task<DispatchOutcome> SendMediaGroupAsync(
        string botToken,
        long chatId,
        IReadOnlyList<(string FilePath, int Kind)> mediaFiles,
        ServerPostContentDto content,
        ServerTargetOptionsDto options,
        CancellationToken cancellationToken)
    {
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(chatId.ToString()), "chat_id");
        if (options.DisableNotification)
        {
            form.Add(new StringContent("true"), "disable_notification");
        }

        var entries = new List<Dictionary<string, object?>>();
        var streams = new List<FileStream>();
        try
        {
            for (int i = 0; i < mediaFiles.Count; i++)
            {
                (string path, int kind) = mediaFiles[i];
                string attach = $"file{i}";
                string type = kind switch { 0 => "photo", 1 => "video", _ => "document" };
                var entry = new Dictionary<string, object?> { ["type"] = type, ["media"] = $"attach://{attach}" };
                if (i == 0 && !string.IsNullOrEmpty(content.Text))
                {
                    entry["caption"] = content.Text;
                    if (content.Entities is { Count: > 0 })
                    {
                        entry["caption_entities"] = content.Entities;
                    }
                }
                entries.Add(entry);

                FileStream fs = File.OpenRead(path);
                streams.Add(fs);
                var sc = new StreamContent(fs);
                sc.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                form.Add(sc, attach, Path.GetFileName(path));
            }

            form.Add(new StringContent(JsonSerializer.Serialize(entries, JsonOptions)), "media");
            string url = $"{_baseApiUrl}/bot{botToken}/sendMediaGroup";
            using HttpResponseMessage response = await _httpClient.PostAsync(url, form, cancellationToken);
            string body = await response.Content.ReadAsStringAsync(cancellationToken);
            return ParseResponse(response.StatusCode, body, isArray: true);
        }
        finally
        {
            foreach (FileStream fs in streams)
            {
                await fs.DisposeAsync();
            }
        }
    }

    private static DispatchOutcome ParseResponse(HttpStatusCode statusCode, string body, bool isArray)
    {
        using JsonDocument doc = JsonDocument.Parse(body);
        JsonElement root = doc.RootElement;
        if (root.TryGetProperty("ok", out JsonElement ok) && ok.GetBoolean())
        {
            var ids = new List<long>();
            if (root.TryGetProperty("result", out JsonElement result))
            {
                if (isArray && result.ValueKind == JsonValueKind.Array)
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
            return new DispatchOutcome(true, ids, false, false, null, null, null);
        }

        int code = root.TryGetProperty("error_code", out JsonElement ec) ? ec.GetInt32() : (int)statusCode;
        string desc = root.TryGetProperty("description", out JsonElement d) ? d.GetString() ?? "Telegram API error" : "Telegram API error";
        TimeSpan? retryAfter = null;
        if (root.TryGetProperty("parameters", out JsonElement p) &&
            p.TryGetProperty("retry_after", out JsonElement ra) &&
            ra.TryGetInt32(out int sec))
        {
            retryAfter = TimeSpan.FromSeconds(Math.Max(1, sec));
        }

        bool transient = code == 429 || code >= 500;
        return new DispatchOutcome(false, [], transient, false, retryAfter, code.ToString(), desc);
    }

    private static object? BuildMarkup(List<ServerButtonDto>? buttons)
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

    private static string Sanitize(string message, string token) =>
        string.IsNullOrEmpty(token) ? message : message.Replace(token, "***", StringComparison.Ordinal);
}
