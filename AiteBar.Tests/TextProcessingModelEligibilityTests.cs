namespace AiteBar.Tests;

public sealed class TextProcessingModelEligibilityTests
{
    [Theory]
    [InlineData("openai/whisper-large-v3", "Whisper Large V3")]
    [InlineData("provider/prompt-guard", "Prompt Guard")]
    [InlineData("provider/text-embedding-3", "Text Embedding 3")]
    [InlineData("provider/safety-gpt", "Safety GPT")]
    [InlineData("gemini-2.5-flash-image", "Nano Banana")]
    [InlineData("gemini-3-pro-image-preview", "Gemini 3 Pro Image Preview")]
    [InlineData("imagen-3.0-generate-002", "Imagen 3")]
    [InlineData("veo-3.0-generate-preview", "Veo 3")]
    [InlineData("provider/generate-image", "Image Generation")]
    [InlineData("provider/generate-video", "Video Generation")]
    [InlineData("\u200B", "\u200B")]
    [InlineData("   ", "   ")]
    public void IsEligibleModel_RejectsModelsNotIntendedForWriting(string modelId, string displayName)
    {
        var model = new AiModelDescriptor(
            "provider",
            modelId,
            displayName,
            AiCapabilities.Text,
            32_768,
            AiCostStatus.VerifiedFree);

        Assert.False(TextProcessingWindow.IsEligibleModel(model));
    }

    [Fact]
    public void IsEligibleModel_AcceptsFreeTextGenerationModel()
    {
        var model = new AiModelDescriptor(
            "provider",
            "gpt-4.1-mini",
            "GPT-4.1 Mini",
            AiCapabilities.Text,
            128_000,
            AiCostStatus.VerifiedFree);

        Assert.True(TextProcessingWindow.IsEligibleModel(model));
    }

    [Fact]
    public void IsEligibleModel_AcceptsMultimodalModelThatReturnsText()
    {
        var model = new AiModelDescriptor(
            "provider",
            "gemini-2.5-flash",
            "Gemini 2.5 Flash Vision",
            AiCapabilities.Text | AiCapabilities.Vision,
            128_000,
            AiCostStatus.VerifiedFree);

        Assert.True(TextProcessingWindow.IsEligibleModel(model));
    }

    [Fact]
    public void BuildLogicalModelItems_CollapsesRepeatedConnectionsForSameProviderModel()
    {
        AiModelDescriptor[] routes =
        [
            Model("groq", "llama-3.3-70b", "Llama 3.3 70B", 32_000),
            Model("groq", "llama-3.3-70b", "Llama 3.3 70B", 128_000),
            Model("cerebras", "llama-3.3-70b", "Llama 3.3 70B", 64_000)
        ];

        IReadOnlyList<ModelItem> items = TextProcessingWindow.BuildLogicalModelItems(routes);

        Assert.Equal(2, items.Count);
        ModelItem groq = Assert.Single(items, item => item.ProviderId == "groq");
        Assert.Equal("llama-3.3-70b", groq.ModelId);
        Assert.Equal(128_000, groq.ContextLength);
        Assert.Contains("Groq", groq.FullDisplay, StringComparison.Ordinal);
        Assert.DoesNotContain(items, item => item.FullDisplay.Contains("ключ", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void BuildLogicalModelItems_ListsPreferredLogicalModelFirstWithoutDuplicatingIt()
    {
        AiModelDescriptor preferred = Model("groq", "z-model", "Z Model", 32_000);
        AiModelDescriptor other = Model("groq", "a-model", "A Model", 32_000);
        var preferredIdentities = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "groq\nz-model"
        };

        IReadOnlyList<ModelItem> items = TextProcessingWindow.BuildLogicalModelItems(
            [preferred, preferred, other],
            preferredIdentities);

        Assert.Equal(["z-model", "a-model"], items.Select(item => item.ModelId));
    }

    private static AiModelDescriptor Model(
        string providerId,
        string modelId,
        string displayName,
        int? contextLength) => new(
        providerId,
        modelId,
        displayName,
        AiCapabilities.Text,
        contextLength,
        AiCostStatus.VerifiedFree);
}
