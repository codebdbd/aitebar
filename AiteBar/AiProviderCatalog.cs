namespace AiteBar;

internal enum AiProviderProtocol
{
    OpenAiCompatible
}

internal sealed record AiProviderDefinition(
    string Id,
    string DisplayName,
    Uri ModelsUri,
    Uri? ChatCompletionsUri,
    Uri DocumentationUri,
    AiProviderProtocol Protocol,
    AiCostStatus DefaultCostStatus,
    bool ApiKeyInQuery = false,
    bool RequiresCustomBaseUrl = false);

internal static class AiProviderCatalog
{
    public const string CredentialTargetPrefix = "AiteBar/AI/";

    public static IReadOnlyList<AiProviderDefinition> All { get; } =
    [
        new(
            "cerebras",
            "Cerebras",
            new Uri("https://api.cerebras.ai/v1/models"),
            new Uri("https://api.cerebras.ai/v1/chat/completions"),
            new Uri("https://cloud.cerebras.ai/"),
            AiProviderProtocol.OpenAiCompatible,
            AiCostStatus.FreeTierAvailable),
        new(
            "groq",
            "Groq",
            new Uri("https://api.groq.com/openai/v1/models"),
            new Uri("https://api.groq.com/openai/v1/chat/completions"),
            new Uri("https://console.groq.com/keys"),
            AiProviderProtocol.OpenAiCompatible,
            AiCostStatus.FreeTierAvailable),
        new(
            "mistral",
            "Mistral AI",
            new Uri("https://api.mistral.ai/v1/models"),
            new Uri("https://api.mistral.ai/v1/chat/completions"),
            new Uri("https://console.mistral.ai/api-keys"),
            AiProviderProtocol.OpenAiCompatible,
            AiCostStatus.FreeTierAvailable),
        new(
            "codestral",
            "Mistral Codestral",
            new Uri("https://codestral.mistral.ai/v1/models"),
            new Uri("https://codestral.mistral.ai/v1/chat/completions"),
            new Uri("https://console.mistral.ai/"),
            AiProviderProtocol.OpenAiCompatible,
            AiCostStatus.FreeTierAvailable),
        new(
            "gemini",
            "Google AI Studio",
            new Uri("https://generativelanguage.googleapis.com/v1beta/openai/models"),
            new Uri("https://generativelanguage.googleapis.com/v1beta/openai/chat/completions"),
            new Uri("https://aistudio.google.com/apikey"),
            AiProviderProtocol.OpenAiCompatible,
            AiCostStatus.FreeTierAvailable),
        new(
            "nvidia",
            "NVIDIA NIM",
            new Uri("https://integrate.api.nvidia.com/v1/models"),
            new Uri("https://integrate.api.nvidia.com/v1/chat/completions"),
            new Uri("https://build.nvidia.com/settings/api-keys"),
            AiProviderProtocol.OpenAiCompatible,
            AiCostStatus.FreeTierAvailable),
        new(
            "openrouter",
            "OpenRouter",
            new Uri("https://openrouter.ai/api/v1/models"),
            new Uri("https://openrouter.ai/api/v1/chat/completions"),
            new Uri("https://openrouter.ai/keys"),
            AiProviderProtocol.OpenAiCompatible,
            AiCostStatus.FreeTierAvailable),
        new(
            "sambanova",
            "SambaNova",
            new Uri("https://api.sambanova.ai/v1/models"),
            new Uri("https://api.sambanova.ai/v1/chat/completions"),
            new Uri("https://cloud.sambanova.ai/"),
            AiProviderProtocol.OpenAiCompatible,
            AiCostStatus.FreeTierAvailable),
        new(
            "huggingface",
            "HuggingFace Router",
            new Uri("https://router.huggingface.co/v1/models"),
            new Uri("https://router.huggingface.co/v1/chat/completions"),
            new Uri("https://huggingface.co/settings/tokens"),
            AiProviderProtocol.OpenAiCompatible,
            AiCostStatus.FreeTierAvailable),
        new(
            "scaleway",
            "Scaleway",
            new Uri("https://api.scaleway.ai/v1/models"),
            new Uri("https://api.scaleway.ai/v1/chat/completions"),
            new Uri("https://console.scaleway.com/"),
            AiProviderProtocol.OpenAiCompatible,
            AiCostStatus.FreeTierAvailable),
        new(
            "ovhcloud",
            "OVHcloud AI",
            new Uri("https://oai.endpoints.kepler.ai.cloud.ovh.net/v1/models"),
            new Uri("https://oai.endpoints.kepler.ai.cloud.ovh.net/v1/chat/completions"),
            new Uri("https://endpoints.ai.cloud.ovh.net/"),
            AiProviderProtocol.OpenAiCompatible,
            AiCostStatus.FreeTierAvailable),
        new(
            "novita",
            "Novita AI",
            new Uri("https://api.novita.ai/v3/openai/models"),
            new Uri("https://api.novita.ai/v3/openai/chat/completions"),
            new Uri("https://novita.ai/"),
            AiProviderProtocol.OpenAiCompatible,
            AiCostStatus.FreeTierAvailable),
        new(
            "siliconflow",
            "SiliconFlow",
            new Uri("https://api.siliconflow.com/v1/models"),
            new Uri("https://api.siliconflow.com/v1/chat/completions"),
            new Uri("https://cloud.siliconflow.cn/"),
            AiProviderProtocol.OpenAiCompatible,
            AiCostStatus.FreeTierAvailable),
        new(
            "ollama_cloud",
            "Ollama Cloud",
            new Uri("https://ollama.com/v1/models"),
            new Uri("https://ollama.com/v1/chat/completions"),
            new Uri("https://ollama.com/settings/keys"),
            AiProviderProtocol.OpenAiCompatible,
            AiCostStatus.FreeTierAvailable),
        new(
            "custom",
            "Custom (OpenAI-compatible)",
            new Uri("https://localhost/v1/models"),
            new Uri("https://localhost/v1/chat/completions"),
            new Uri("https://platform.openai.com/docs/api-reference"),
            AiProviderProtocol.OpenAiCompatible,
            AiCostStatus.FreeTierAvailable,
            RequiresCustomBaseUrl: true)
    ];

    public static IReadOnlyList<string> DefaultProviderOrder { get; } =
        All.Select(provider => provider.Id).ToArray();

    public static bool TryGet(string providerId, out AiProviderDefinition definition)
    {
        AiProviderDefinition? found = All.FirstOrDefault(provider =>
            string.Equals(provider.Id, providerId, StringComparison.OrdinalIgnoreCase));
        definition = found!;
        return found != null;
    }

    public static string CreateCredentialTarget(string connectionId) =>
        $"{CredentialTargetPrefix}{connectionId}";

    public static bool TryNormalizeCustomBaseUrl(string? rawUrl, out string normalizedBaseUrl)
    {
        normalizedBaseUrl = string.Empty;
        if (string.IsNullOrWhiteSpace(rawUrl))
        {
            return false;
        }

        string trimmed = rawUrl.Trim();
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out Uri? uri))
        {
            return false;
        }

        bool isHttps = string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
        bool isLoopbackHttp = string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
                              uri.IsLoopback;
        if (!isHttps && !isLoopbackHttp)
        {
            return false;
        }

        string clean = uri.GetLeftPart(UriPartial.Path).TrimEnd('/');
        if (clean.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase))
        {
            clean = clean[..^"/chat/completions".Length];
        }
        else if (clean.EndsWith("/models", StringComparison.OrdinalIgnoreCase))
        {
            clean = clean[..^"/models".Length];
        }

        clean = clean.TrimEnd('/');
        if (string.IsNullOrWhiteSpace(clean))
        {
            return false;
        }

        normalizedBaseUrl = clean;
        return true;
    }
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
                !providerOrder.Contains(definition.Id, StringComparer.OrdinalIgnoreCase))
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
            if (!providerOrder.Contains(providerId, StringComparer.OrdinalIgnoreCase))
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

            string? normalizedCustomBaseUrl = null;
            if (provider.RequiresCustomBaseUrl)
            {
                if (!AiProviderCatalog.TryNormalizeCustomBaseUrl(connection.CustomBaseUrl, out string validBaseUrl))
                {
                    changed = true;
                    continue;
                }
                normalizedCustomBaseUrl = validBaseUrl;
                if (!string.Equals(normalizedCustomBaseUrl, connection.CustomBaseUrl, StringComparison.Ordinal))
                {
                    changed = true;
                }
            }
            else if (connection.CustomBaseUrl != null)
            {
                changed = true;
            }

            string displayName = string.IsNullOrWhiteSpace(connection.DisplayName)
                ? provider.DisplayName
                : connection.DisplayName.Trim();

            if (!string.Equals(displayName, connection.DisplayName, StringComparison.Ordinal) ||
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
                IsEnabled = connection.IsEnabled,
                PreferredModelId = string.IsNullOrWhiteSpace(connection.PreferredModelId)
                    ? null
                    : connection.PreferredModelId.Trim(),
                CustomBaseUrl = normalizedCustomBaseUrl
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
