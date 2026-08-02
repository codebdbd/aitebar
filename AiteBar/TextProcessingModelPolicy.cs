namespace AiteBar;

public enum TextProcessingModelTier
{
    Unsupported = 0,
    ManualOnly = 1,
    CertifiedAutomatic = 2
}

internal static class TextProcessingModelPolicy
{
    private static readonly string[] UnsupportedModelMarkers =
    [
        "allam",
        "jais",
        "arabic",
        "lyria",
        "orpheus",
        "whisper",
        "text-to-speech",
        "speech-to-text",
        "tts"
    ];

    private static readonly IReadOnlyDictionary<string, string[]> CertifiedModelMarkers =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["gemini"] =
            [
                "gemini-1.5-flash",
                "gemini-1.5-pro",
                "gemini-2.0-flash",
                "gemini-2.5-flash",
                "gemini-2.5-pro",
                "gemini-3-flash",
                "gemini-3-pro"
            ],
            ["cerebras"] =
            [
                "llama-3.1-70b",
                "llama-3.3-70b",
                "llama-4-maverick",
                "qwen3-32b",
                "qwen-3-32b",
                "gpt-oss-120b"
            ],
            ["groq"] =
            [
                "llama-3.1-70b",
                "llama-3.3-70b",
                "llama-4-maverick",
                "qwen3-32b",
                "qwen-3-32b",
                "gpt-oss-120b"
            ],
            ["mistral"] =
            [
                "mistral-small",
                "mistral-medium",
                "mistral-large",
                "open-mistral-nemo"
            ]
        };

    internal static TextProcessingModelTier Classify(AiModelDescriptor model)
    {
        ArgumentNullException.ThrowIfNull(model);
        if (!TextProcessingService.IsSuitableForWritingModel(model))
        {
            return TextProcessingModelTier.Unsupported;
        }

        string identity = $"{model.ModelId} {model.DisplayName}";
        if (UnsupportedModelMarkers.Any(marker =>
                identity.Contains(marker, StringComparison.OrdinalIgnoreCase)))
        {
            return TextProcessingModelTier.Unsupported;
        }

        if (CertifiedModelMarkers.TryGetValue(model.ProviderId, out string[]? markers) &&
            markers.Any(marker => model.ModelId.Contains(marker, StringComparison.OrdinalIgnoreCase)))
        {
            return TextProcessingModelTier.CertifiedAutomatic;
        }

        return TextProcessingModelTier.ManualOnly;
    }
}
