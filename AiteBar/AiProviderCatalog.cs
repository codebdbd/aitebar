namespace AiteBar;

internal enum AiProviderProtocol
{
    OpenAiCompatible,
    OpenRouter,
    Gemini,
    GitHubModels
}

internal sealed record AiProviderDefinition(
    string Id,
    string DisplayName,
    Uri ModelsUri,
    Uri? ChatCompletionsUri,
    Uri DocumentationUri,
    AiProviderProtocol Protocol,
    AiCostStatus DefaultCostStatus,
    bool ApiKeyInQuery = false);

internal static class AiProviderCatalog
{
    public const string CredentialTargetPrefix = "AiteBar/AI/";

    public static IReadOnlyList<AiProviderDefinition> All { get; } =
    [
        new(
            "openrouter",
            "OpenRouter",
            new Uri("https://openrouter.ai/api/v1/models"),
            new Uri("https://openrouter.ai/api/v1/chat/completions"),
            new Uri("https://openrouter.ai/keys"),
            AiProviderProtocol.OpenRouter,
            AiCostStatus.Unknown),
        new(
            "cerebras",
            "Cerebras",
            new Uri("https://api.cerebras.ai/v1/models"),
            new Uri("https://api.cerebras.ai/v1/chat/completions"),
            new Uri("https://cloud.cerebras.ai/"),
            AiProviderProtocol.OpenAiCompatible,
            AiCostStatus.FreeTierAvailable),
        new(
            "gemini",
            "Google Gemini",
            new Uri("https://generativelanguage.googleapis.com/v1beta/models"),
            null,
            new Uri("https://aistudio.google.com/app/apikey"),
            AiProviderProtocol.Gemini,
            AiCostStatus.FreeTierAvailable,
            ApiKeyInQuery: true),
        new(
            "groq",
            "Groq",
            new Uri("https://api.groq.com/openai/v1/models"),
            new Uri("https://api.groq.com/openai/v1/chat/completions"),
            new Uri("https://console.groq.com/keys"),
            AiProviderProtocol.OpenAiCompatible,
            AiCostStatus.FreeTierAvailable),
        new(
            "github",
            "GitHub Models",
            new Uri("https://models.github.ai/catalog/models"),
            new Uri("https://models.github.ai/inference/chat/completions"),
            new Uri("https://github.com/settings/tokens"),
            AiProviderProtocol.GitHubModels,
            AiCostStatus.FreeTierAvailable),
        new(
            "mistral",
            "Mistral AI",
            new Uri("https://api.mistral.ai/v1/models"),
            new Uri("https://api.mistral.ai/v1/chat/completions"),
            new Uri("https://console.mistral.ai/api-keys"),
            AiProviderProtocol.OpenAiCompatible,
            AiCostStatus.FreeTierAvailable)
    ];

    public static IReadOnlyList<string> DefaultProviderOrder { get; } =
        All.Select(provider => provider.Id).ToArray();

    public static bool TryGet(string providerId, out AiProviderDefinition definition)
    {
        definition = All.FirstOrDefault(provider =>
            string.Equals(provider.Id, providerId, StringComparison.OrdinalIgnoreCase))!;
        return definition != null;
    }

    public static string CreateCredentialTarget(string connectionId) =>
        $"{CredentialTargetPrefix}{connectionId}";
}

internal static class AiSettingsNormalizer
{
    public static AiSettings Normalize(AiSettings? settings, out bool changed)
    {
        changed = settings == null;
        settings ??= new AiSettings();

        var providerOrder = new List<string>();
        foreach (string providerId in settings.ProviderOrder ?? [])
        {
            if (AiProviderCatalog.TryGet(providerId, out AiProviderDefinition definition) &&
                !providerOrder.Contains(definition.Id, StringComparer.Ordinal))
            {
                providerOrder.Add(definition.Id);
            }
            else
            {
                changed = true;
            }
        }

        foreach (string providerId in AiProviderCatalog.DefaultProviderOrder)
        {
            if (!providerOrder.Contains(providerId, StringComparer.Ordinal))
            {
                providerOrder.Add(providerId);
                changed = true;
            }
        }

        var connections = new List<AiConnectionSettings>();
        var seenIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (AiConnectionSettings? connection in settings.Connections ?? [])
        {
            if (connection == null ||
                !AiProviderCatalog.TryGet(connection.ProviderId, out AiProviderDefinition provider) ||
                string.IsNullOrWhiteSpace(connection.Id) ||
                !seenIds.Add(connection.Id) ||
                string.IsNullOrWhiteSpace(connection.CredentialTarget) ||
                !connection.CredentialTarget.StartsWith(AiProviderCatalog.CredentialTargetPrefix, StringComparison.Ordinal))
            {
                changed = true;
                continue;
            }

            string displayName = string.IsNullOrWhiteSpace(connection.DisplayName)
                ? provider.DisplayName
                : connection.DisplayName.Trim();
            string quotaScope = string.IsNullOrWhiteSpace(connection.QuotaScopeId)
                ? connection.Id
                : connection.QuotaScopeId.Trim();
            int priority = Math.Max(0, connection.Priority);

            if (!string.Equals(displayName, connection.DisplayName, StringComparison.Ordinal) ||
                !string.Equals(quotaScope, connection.QuotaScopeId, StringComparison.Ordinal) ||
                priority != connection.Priority ||
                !string.Equals(provider.Id, connection.ProviderId, StringComparison.Ordinal))
            {
                changed = true;
            }

            connections.Add(new AiConnectionSettings
            {
                Id = connection.Id,
                ProviderId = provider.Id,
                DisplayName = displayName,
                CredentialTarget = connection.CredentialTarget,
                QuotaScopeId = quotaScope,
                Priority = priority,
                IsEnabled = connection.IsEnabled,
                PreferredModelId = string.IsNullOrWhiteSpace(connection.PreferredModelId)
                    ? null
                    : connection.PreferredModelId.Trim()
            });
        }

        return new AiSettings
        {
            FreeTierOnly = true,
            ProviderOrder = providerOrder,
            Connections = connections
        };
    }
}
