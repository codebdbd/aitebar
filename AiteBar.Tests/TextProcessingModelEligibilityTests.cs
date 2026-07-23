namespace AiteBar.Tests;

public sealed class TextProcessingModelEligibilityTests
{
    [Theory]
    [InlineData("openai/whisper-large-v3", "Whisper Large V3")]
    [InlineData("provider/prompt-guard", "Prompt Guard")]
    [InlineData("provider/text-embedding-3", "Text Embedding 3")]
    [InlineData("provider/safety-gpt", "Safety GPT")]
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
}
