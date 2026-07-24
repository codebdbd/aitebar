using System.Net;

namespace AiteBar;

[Flags]
public enum AiCapabilities
{
    None = 0,
    Text = 1,
    Vision = 2,
    Streaming = 4,
    Tools = 8,
    StructuredOutput = 16,
    Reasoning = 32
}

public enum AiCostStatus
{
    Unknown,
    VerifiedFree,
    FreeTierAvailable,
    Paid
}

public enum AiConnectionState
{
    NotChecked,
    Available,
    CoolingDown,
    QuotaExhausted,
    InvalidCredential,
    PermissionDenied,
    Unavailable
}

public sealed class AiSettings
{
    public bool FreeTierOnly { get; set; } = true;
    public List<string> ProviderOrder { get; set; } = [];
    public List<AiConnectionSettings> Connections { get; set; } = [];
}

public sealed class AiConnectionSettings
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string ProviderId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string CredentialTarget { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public string? PreferredModelId { get; set; }
}

public sealed record AiModelDescriptor(
    string ProviderId,
    string ModelId,
    string DisplayName,
    AiCapabilities Capabilities,
    int? ContextLength,
    AiCostStatus CostStatus,
    bool IsDeprecated = false);

public sealed record AiChatMessage(string Role, string Content);

public sealed class AiChatRequest
{
    public IReadOnlyList<AiChatMessage> Messages { get; init; } = [];
    public AiCapabilities RequiredCapabilities { get; init; } = AiCapabilities.Text;
    public bool RequireFreeModel { get; init; }
    public bool RequireWritingModel { get; init; }
    public bool RequireExactModel { get; init; }
    public string? PreferredConnectionId { get; init; }
    public string? PreferredProviderId { get; init; }
    public string? PreferredModelId { get; init; }
    public int? RequiredContextTokens { get; init; }
    public int MaxOutputTokens { get; init; } = 1024;
    public double? Temperature { get; init; }
}

public sealed record AiProviderResponse(
    string Content,
    string ProviderId,
    string ModelId,
    int? PromptTokens,
    int? CompletionTokens);

public sealed record AiGatewayResponse(
    string Content,
    string ProviderId,
    string ConnectionId,
    string ModelId,
    int? PromptTokens,
    int? CompletionTokens);

public sealed record AiConnectionCheckResult(
    bool IsSuccess,
    AiConnectionState State,
    int ModelCount,
    string? ErrorMessage = null);

internal sealed record AiConnectionRuntimeStatus(
    AiConnectionState State,
    DateTimeOffset? CooldownUntil,
    string? LastError,
    DateTimeOffset UpdatedAt);

internal sealed class AiProviderHttpException : Exception
{
    public AiProviderHttpException(
        HttpStatusCode statusCode,
        string message,
        TimeSpan? retryAfter = null)
        : base(message)
    {
        StatusCode = statusCode;
        RetryAfter = retryAfter;
    }

    public HttpStatusCode StatusCode { get; }
    public TimeSpan? RetryAfter { get; }
}

internal sealed class NoAvailableConnectionException : Exception
{
    public NoAvailableConnectionException(string message, Exception? innerException = null)
        : base(message, innerException) { }
}
