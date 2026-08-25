namespace AiteBar.Tests;

public sealed class TextProcessingModelEligibilityTests
{
    [Theory]
    [InlineData("openai/whisper-large-v3", "Whisper Large V3")]
    [InlineData("provider/prompt-guard", "Prompt Guard")]
    [InlineData("provider/text-embedding-3", "Text Embedding 3")]
    [InlineData("provider/safety-gpt", "Safety GPT")]
    [InlineData("openai/gpt-4-image", "GPT-4 Image")]
    [InlineData("openai/gpt-5-image-preview", "GPT-5 Image Preview")]
    [InlineData("imagen-3.0-generate-002", "Imagen 3")]
    [InlineData("veo-3.0-generate-preview", "Veo 3")]
    [InlineData("provider/generate-image", "Image Generation")]
    [InlineData("provider/generate-video", "Video Generation")]
    [InlineData("ALLaM-2-7b", "ALLaM 2 7B")]
    [InlineData("jais-30b-chat", "Jais 30B Chat")]
    [InlineData("lyria-3-clip", "Lyria 3 Clip")]
    [InlineData("mistral-embed", "Mistral Embed")]
    [InlineData("mistral-ocr-latest", "Mistral OCR")]
    [InlineData("labs/leanstral-2402", "Labs Leanstral")]
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
            "gpt-4o-mini",
            "GPT-4o Mini",
            AiCapabilities.Text | AiCapabilities.Vision,
            128_000,
            AiCostStatus.VerifiedFree);

        Assert.True(TextProcessingWindow.IsEligibleModel(model));
    }

    [Theory]
    [InlineData("groq", "llama-3.3-70b-versatile", TextProcessingModelTier.CertifiedAutomatic)]
    [InlineData("cerebras", "qwen-3-32b", TextProcessingModelTier.CertifiedAutomatic)]
    [InlineData("mistral", "mistral-small-latest", TextProcessingModelTier.CertifiedAutomatic)]
    [InlineData("groq", "new-general-chat-model", TextProcessingModelTier.ManualOnly)]
    [InlineData("groq", "ALLaM-2-7b", TextProcessingModelTier.Unsupported)]
    [InlineData("groq", "whisper-large-v3", TextProcessingModelTier.Unsupported)]
    public void ModelPolicy_ClassifiesTextProcessingModels(
        string providerId,
        string modelId,
        TextProcessingModelTier expected)
    {
        AiModelDescriptor model = Model(providerId, modelId, modelId, 32_000);

        Assert.Equal(expected, TextProcessingModelPolicy.Classify(model));
    }

    [Fact]
    public void GatewayPolicy_AutomaticUsesOnlyCertifiedModels_WhileManualAllowsUnknownModels()
    {
        AiModelDescriptor certified = Model("groq", "llama-3.3-70b-versatile", "Llama", 32_000);
        AiModelDescriptor unknown = Model("groq", "new-general-chat-model", "New Chat", 32_000);
        AiModelDescriptor narrowLanguage = Model("groq", "ALLaM-2-7b", "ALLaM", 32_000);

        IReadOnlyList<AiModelDescriptor> automatic =
            AiGateway.ApplyTextProcessingModelPolicy([narrowLanguage, unknown, certified], false);
        IReadOnlyList<AiModelDescriptor> manual =
            AiGateway.ApplyTextProcessingModelPolicy([narrowLanguage, unknown, certified], true);

        Assert.Equal([certified], automatic);
        Assert.Equal([unknown, certified], manual);
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
        Assert.Equal(TextProcessingModelTier.CertifiedAutomatic, groq.Tier);
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

    [Fact]
    public void GetCertifiedModelRank_RanksLlama33BeforeGptOss120b()
    {
        AiModelDescriptor llama = Model("cerebras", "llama-3.3-70b", "Llama 3.3", 128_000);
        AiModelDescriptor gptOss = Model("cerebras", "gpt-oss-120b", "GPT OSS", 128_000);

        int llamaRank = TextProcessingModelPolicy.GetCertifiedModelRank(llama);
        int gptOssRank = TextProcessingModelPolicy.GetCertifiedModelRank(gptOss);

        Assert.True(llamaRank < gptOssRank, $"Expected llamaRank ({llamaRank}) < gptOssRank ({gptOssRank})");
    }

    [Fact]
    public void OrderRoutes_PrioritizesLlama33OverGptOssAndRotatesOnRotationOffset()
    {
        var conn = new AiConnectionSettings { Id = "conn1", ProviderId = "cerebras", IsEnabled = true };
        var settings = new AiSettings { Connections = [conn] };

        AiModelDescriptor llama = Model("cerebras", "llama-3.3-70b", "Llama 3.3", 128_000);
        AiModelDescriptor gptOss = Model("cerebras", "gpt-oss-120b", "GPT OSS", 128_000);
        AiModelDescriptor qwen = Model("cerebras", "qwen3-32b", "Qwen 3", 128_000);

        var candidateLlama = new AiRouteCandidate(conn, llama, 0);
        var candidateGptOss = new AiRouteCandidate(conn, gptOss, 1);
        var candidateQwen = new AiRouteCandidate(conn, qwen, 2);

        var requestBase = new AiChatRequest();
        var ordered0 = AiModelSelectionPolicy.OrderRoutes(settings, requestBase, [candidateGptOss, candidateQwen, candidateLlama]);
        Assert.Equal("llama-3.3-70b", ordered0[0].Model.ModelId);

        var requestRepeat = new AiChatRequest { RotationOffset = 1 };
        var ordered1 = AiModelSelectionPolicy.OrderRoutes(settings, requestRepeat, [candidateGptOss, candidateQwen, candidateLlama]);
        Assert.NotEqual("llama-3.3-70b", ordered1[0].Model.ModelId);
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
