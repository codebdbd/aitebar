using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Runtime.CompilerServices;

namespace AiteBar;

internal sealed class AiProviderClient
{
    internal static readonly TimeSpan StreamInactivityTimeout = TimeSpan.FromSeconds(30);
    internal static readonly TimeSpan RequestTimeout = TimeSpan.FromMinutes(5);
    private static readonly HttpClient SharedHttpClient = CreateHttpClient();
    private readonly HttpClient _httpClient;
    private readonly IAiCredentialStore _credentialStore;

    public AiProviderClient(IAiCredentialStore credentialStore)
        : this(SharedHttpClient, credentialStore)
    {
    }

    internal AiProviderClient(HttpClient httpClient, IAiCredentialStore credentialStore)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _credentialStore = credentialStore ?? throw new ArgumentNullException(nameof(credentialStore));
    }

    public async Task<AiConnectionCheckResult> CheckConnectionAsync(
        AiConnectionSettings connection,
        CancellationToken cancellationToken)
    {
        try
        {
            IReadOnlyList<AiModelDescriptor> models = await GetModelsAsync(connection, cancellationToken).ConfigureAwait(false);
            return new AiConnectionCheckResult(true, AiConnectionState.Available, models.Count);
        }
        catch (AiProviderHttpException ex)
        {
            return new AiConnectionCheckResult(
                false,
                MapConnectionState(ex.StatusCode),
                0,
                ex.Message);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new AiConnectionCheckResult(false, AiConnectionState.Unavailable, 0, ex.Message);
        }
    }

    public async Task<IReadOnlyList<AiModelDescriptor>> GetModelsAsync(
        AiConnectionSettings connection,
        CancellationToken cancellationToken)
    {
        (AiProviderDefinition provider, string apiKey) = ResolveConnection(connection);
        Uri uri = BuildModelsUri(provider, apiKey);
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        ApplyAuthentication(request, provider, apiKey);
        using HttpResponseMessage response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken).ConfigureAwait(false);
        using JsonDocument document = await ReadJsonResponseAsync(response, cancellationToken).ConfigureAwait(false);

        return ParseOpenAiModels(provider, document.RootElement);
    }

    public async Task<AiProviderResponse> GenerateAsync(
        AiConnectionSettings connection,
        AiModelDescriptor model,
        AiChatRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(request);
        if (request.Messages.Count == 0)
        {
            throw new ArgumentException("At least one AI chat message is required.", nameof(request));
        }

        (AiProviderDefinition provider, string apiKey) = ResolveConnection(connection);
        return await GenerateOpenAiCompatibleAsync(provider, apiKey, model, request, cancellationToken).ConfigureAwait(false);
    }

    public async Task<AiProviderStream> GenerateStreamingAsync(
        AiConnectionSettings connection,
        AiModelDescriptor model,
        AiChatRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(request);
        if (request.Messages.Count == 0)
        {
            throw new ArgumentException("At least one AI chat message is required.", nameof(request));
        }

        (AiProviderDefinition provider, string apiKey) = ResolveConnection(connection);
        return await StartOpenAiStreamAsync(provider, apiKey, model, request, cancellationToken).ConfigureAwait(false);
    }

    private async Task<AiProviderStream> StartOpenAiStreamAsync(
        AiProviderDefinition provider,
        string apiKey,
        AiModelDescriptor model,
        AiChatRequest request,
        CancellationToken cancellationToken)
    {
        if (provider.ChatCompletionsUri == null)
        {
            throw new InvalidOperationException($"Provider '{provider.Id}' does not define a chat endpoint.");
        }

        var payload = new Dictionary<string, object?>
        {
            ["model"] = model.ModelId,
            ["messages"] = request.Messages.Select(message => new { role = message.Role, content = message.Content }).ToArray(),
            ["max_tokens"] = Math.Clamp(request.MaxOutputTokens, 1, 32768),
            ["stream"] = true
        };
        if (request.Temperature is double temperature)
        {
            payload["temperature"] = Math.Clamp(temperature, 0d, 2d);
        }

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, provider.ChatCompletionsUri)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };
        ApplyAuthentication(httpRequest, provider, apiKey);
        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(
                httpRequest,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            httpRequest.Dispose();
        }
        await EnsureSuccessfulResponseAsync(response, cancellationToken).ConfigureAwait(false);
        return new AiProviderStream(
            provider.Id,
            model.ModelId,
            ReadOpenAiStreamAsync(response, cancellationToken));
    }

    private static async IAsyncEnumerable<string> ReadOpenAiStreamAsync(
        HttpResponseMessage response,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using (response)
        await using (Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false))
        using (var reader = new StreamReader(stream))
        {
            while (await ReadLineWithInactivityTimeoutAsync(
                       reader,
                       StreamInactivityTimeout,
                       cancellationToken).ConfigureAwait(false) is string line)
            {
                string? content = ParseOpenAiStreamData(line);
                if (!string.IsNullOrEmpty(content))
                {
                    yield return content;
                }
            }
        }
    }

    internal static string? ParseOpenAiStreamData(string line)
    {
        if (!line.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }
        string data = line[5..].TrimStart();
        if (data.Length == 0 || string.Equals(data, "[DONE]", StringComparison.Ordinal))
        {
            return null;
        }
        using JsonDocument document = JsonDocument.Parse(data);
        JsonElement root = document.RootElement;
        return root.TryGetProperty("choices", out JsonElement choices) &&
               choices.ValueKind == JsonValueKind.Array &&
               choices.GetArrayLength() > 0 &&
               choices[0].TryGetProperty("delta", out JsonElement delta) &&
               delta.TryGetProperty("content", out JsonElement content)
            ? ReadTextContent(content)
            : null;
    }

    internal static async Task<string?> ReadLineWithInactivityTimeoutAsync(
        TextReader reader,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reader);
        if (timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout));
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);
        try
        {
            return await reader.ReadLineAsync(timeoutCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException("AI provider stream stopped sending data.");
        }
    }

    private async Task<AiProviderResponse> GenerateOpenAiCompatibleAsync(
        AiProviderDefinition provider,
        string apiKey,
        AiModelDescriptor model,
        AiChatRequest request,
        CancellationToken cancellationToken)
    {
        if (provider.ChatCompletionsUri == null)
        {
            throw new InvalidOperationException($"Provider '{provider.Id}' does not define a chat endpoint.");
        }

        var payload = new Dictionary<string, object?>
        {
            ["model"] = model.ModelId,
            ["messages"] = request.Messages.Select(message => new { role = message.Role, content = message.Content }).ToArray(),
            ["max_tokens"] = Math.Clamp(request.MaxOutputTokens, 1, 32768),
            ["stream"] = false
        };
        if (request.Temperature is double temperature)
        {
            payload["temperature"] = Math.Clamp(temperature, 0d, 2d);
        }

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, provider.ChatCompletionsUri)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };
        ApplyAuthentication(httpRequest, provider, apiKey);
        using HttpResponseMessage response = await _httpClient.SendAsync(
            httpRequest,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken).ConfigureAwait(false);
        using JsonDocument document = await ReadJsonResponseAsync(response, cancellationToken).ConfigureAwait(false);
        JsonElement root = document.RootElement;

        string content = root.TryGetProperty("choices", out JsonElement choices) &&
                         choices.ValueKind == JsonValueKind.Array &&
                         choices.GetArrayLength() > 0 &&
                         choices[0].TryGetProperty("message", out JsonElement message) &&
                         message.TryGetProperty("content", out JsonElement contentElement)
            ? ReadTextContent(contentElement)
            : string.Empty;

        int? promptTokens = null;
        int? completionTokens = null;
        if (root.TryGetProperty("usage", out JsonElement usage))
        {
            promptTokens = ReadNullableInt(usage, "prompt_tokens");
            completionTokens = ReadNullableInt(usage, "completion_tokens");
        }

        return new AiProviderResponse(content, provider.Id, model.ModelId, promptTokens, completionTokens);
    }


    internal static IReadOnlyList<AiModelDescriptor> ParseOpenAiModels(
        AiProviderDefinition provider,
        JsonElement root)
    {
        if (!root.TryGetProperty("data", out JsonElement data) || data.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return data.EnumerateArray()
            .Select(item =>
            {
                string? id = ReadString(item, "id");
                return string.IsNullOrWhiteSpace(id)
                    ? null
                    : new AiModelDescriptor(
                        provider.Id,
                        id,
                        ReadString(item, "name") ?? id,
                        ResolveCapabilities(item),
                        ReadNullableInt(item, "context_length") ?? ReadNullableInt(item, "max_context_length"),
                        provider.DefaultCostStatus,
                        ReadBoolean(item, "deprecated"));
            })
            .Where(model => model != null)
            .Cast<AiModelDescriptor>()
            .ToArray();
    }

    private (AiProviderDefinition Provider, string ApiKey) ResolveConnection(AiConnectionSettings connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        if (!AiProviderCatalog.TryGet(connection.ProviderId, out AiProviderDefinition provider))
        {
            throw new InvalidOperationException($"Unknown AI provider '{connection.ProviderId}'.");
        }
        string? apiKey = _credentialStore.Read(connection.CredentialTarget);
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new AiProviderHttpException(HttpStatusCode.Unauthorized, "The API key is missing.");
        }
        return (provider, apiKey);
    }

    private static Uri BuildModelsUri(AiProviderDefinition provider, string apiKey)
    {
        _ = apiKey;
        return provider.ModelsUri;
    }

    private static void ApplyAuthentication(
        HttpRequestMessage request,
        AiProviderDefinition provider,
        string apiKey)
    {
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        if (!provider.ApiKeyInQuery)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        }
    }

    private static async Task<JsonDocument> ReadJsonResponseAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        await EnsureSuccessfulResponseAsync(response, cancellationToken).ConfigureAwait(false);
        Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("AI provider returned invalid JSON.", ex);
        }
    }

    private static async Task EnsureSuccessfulResponseAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (!response.IsSuccessStatusCode)
        {
            TimeSpan? retryAfter = response.Headers.RetryAfter?.Delta;
            string message = $"AI provider returned HTTP {(int)response.StatusCode}.";
            
            // Try to read and parse error message from response body
            try
            {
                string errorContent = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(errorContent))
                {
                    using JsonDocument errorDoc = JsonDocument.Parse(errorContent);
                    // Try common error structures
                    string? extractedMessage = null;
                    
                    // OpenAI-compatible format: { "error": { "message": "..." } }
                    if (errorDoc.RootElement.TryGetProperty("error", out JsonElement errorProp))
                    {
                        extractedMessage = ReadString(errorProp, "message");
                    }
                    
                    // Gemini format: { "error": { "message": "..." } } or similar
                    if (string.IsNullOrWhiteSpace(extractedMessage))
                    {
                        extractedMessage = ReadString(errorDoc.RootElement, "message");
                    }
                    
                    if (!string.IsNullOrWhiteSpace(extractedMessage))
                    {
                        message = extractedMessage;
                    }
                    else
                    {
                        // If we can't parse the message, include the raw content (truncated if too long)
                        const int maxLength = 500;
                        string truncated = errorContent.Length > maxLength 
                            ? errorContent.Substring(0, maxLength) + "..." 
                            : errorContent;
                        message = $"AI provider returned HTTP {(int)response.StatusCode}. Response: {truncated}";
                    }
                }
            }
            catch
            {
                // If reading/parsing fails, just use the original message
            }
            
            response.Dispose();
            throw new AiProviderHttpException(response.StatusCode, message, retryAfter);
        }
    }

    private static AiCapabilities ResolveCapabilities(JsonElement item)
    {
        AiCapabilities capabilities = AiCapabilities.Text;
        if (ContainsString(item, "capabilities", "streaming") || ReadBoolean(item, "streaming"))
        {
            capabilities |= AiCapabilities.Streaming;
        }
        if (ContainsString(item, "capabilities", "tool-calling") ||
            ContainsString(item, "supported_parameters", "tools") ||
            ReadNestedBoolean(item, "capabilities", "tools"))
        {
            capabilities |= AiCapabilities.Tools;
        }
        if (ContainsString(item, "capabilities", "structured-outputs") ||
            ContainsString(item, "supported_parameters", "response_format") ||
            ReadNestedBoolean(item, "capabilities", "structured_outputs"))
        {
            capabilities |= AiCapabilities.StructuredOutput;
        }
        if (ReadNestedBoolean(item, "capabilities", "vision") ||
            ContainsString(item, "supported_input_modalities", "image"))
        {
            capabilities |= AiCapabilities.Vision;
        }
        if (ReadNestedBoolean(item, "capabilities", "reasoning"))
        {
            capabilities |= AiCapabilities.Reasoning;
        }
        return capabilities;
    }


    private static bool ContainsString(JsonElement item, string propertyName, string value)
    {
        if (!item.TryGetProperty(propertyName, out JsonElement array))
        {
            return false;
        }
        if (array.ValueKind == JsonValueKind.Array)
        {
            return array.EnumerateArray().Any(element =>
                element.ValueKind == JsonValueKind.String &&
                string.Equals(element.GetString(), value, StringComparison.OrdinalIgnoreCase));
        }
        if (array.ValueKind == JsonValueKind.Object && array.TryGetProperty(value, out JsonElement objectValue))
        {
            return objectValue.ValueKind == JsonValueKind.True;
        }
        return false;
    }

    private static bool ReadNestedBoolean(JsonElement item, string objectName, string propertyName) =>
        item.TryGetProperty(objectName, out JsonElement nested) && ReadBoolean(nested, propertyName);

    private static bool ReadBoolean(JsonElement item, string propertyName) =>
        item.TryGetProperty(propertyName, out JsonElement value) && value.ValueKind == JsonValueKind.True;

    private static string? ReadString(JsonElement item, string propertyName) =>
        item.TryGetProperty(propertyName, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static int? ReadNullableInt(JsonElement item, string propertyName) =>
        item.TryGetProperty(propertyName, out JsonElement value) && value.TryGetInt32(out int number)
            ? number
            : null;

    private static string ReadTextContent(JsonElement content)
    {
        if (content.ValueKind == JsonValueKind.String)
        {
            return content.GetString() ?? string.Empty;
        }
        if (content.ValueKind == JsonValueKind.Array)
        {
            return string.Join(string.Empty, content.EnumerateArray()
                .Where(part => part.TryGetProperty("text", out _))
                .Select(part => part.GetProperty("text").GetString() ?? string.Empty));
        }
        return string.Empty;
    }

    private static AiConnectionState MapConnectionState(HttpStatusCode statusCode) => statusCode switch
    {
        HttpStatusCode.Unauthorized => AiConnectionState.InvalidCredential,
        HttpStatusCode.Forbidden => AiConnectionState.PermissionDenied,
        HttpStatusCode.TooManyRequests => AiConnectionState.CoolingDown,
        HttpStatusCode.PaymentRequired => AiConnectionState.QuotaExhausted,
        _ => AiConnectionState.Unavailable
    };

    private static HttpClient CreateHttpClient() => new()
    {
        Timeout = RequestTimeout
    };
}
