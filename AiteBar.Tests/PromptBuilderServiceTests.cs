using AiteBar;

namespace AiteBar.Tests;

public sealed class PromptBuilderServiceTests
{
    private readonly PromptBuilderService _service = new();

    [Fact]
    public void Categories_HaveStableValues()
    {
        Assert.Equal(0, (int)PromptBuilderCategory.Programming);
        Assert.Equal(1, (int)PromptBuilderCategory.Images);
        Assert.Equal(2, (int)PromptBuilderCategory.Texts);
        Assert.Equal(3, (int)PromptBuilderCategory.VideoAudio);
        Assert.Equal(4, (int)PromptBuilderCategory.AnalysisIdeas);
    }

    [Theory]
    [InlineData(PromptBuilderCategory.Programming, "software engineering")]
    [InlineData(PromptBuilderCategory.Images, "image generation")]
    [InlineData(PromptBuilderCategory.Texts, "articles")]
    [InlineData(PromptBuilderCategory.VideoAudio, "video")]
    [InlineData(PromptBuilderCategory.AnalysisIdeas, "research")]
    public void GetSystemPrompt_EnforcesOneShotProfessionalOutput(
        PromptBuilderCategory category,
        string categoryMarker)
    {
        string prompt = _service.GetSystemPrompt(category);

        Assert.Contains("Return only the finished prompt", prompt);
        Assert.Contains("Do not greet", prompt);
        Assert.Contains("ask questions", prompt);
        Assert.Contains("One request must always produce one final prompt", prompt);
        Assert.Contains("square-bracket placeholder", prompt);
        Assert.Contains(categoryMarker, prompt, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildRequest_UsesBriefAsUserMessageAndWritingModel()
    {
        AiChatRequest request = _service.BuildRequest(
            PromptBuilderCategory.Programming,
            "  Создай сайт для портфолио  ");

        Assert.Equal(2, request.Messages.Count);
        Assert.Equal("system", request.Messages[0].Role);
        Assert.Equal("user", request.Messages[1].Role);
        Assert.Equal("Создай сайт для портфолио", request.Messages[1].Content);
        Assert.True(request.RequireWritingModel);
        Assert.True(request.RequireFreeModel);
        Assert.Equal(0.25, request.Temperature);
        Assert.InRange(request.MaxOutputTokens, 2048, 8192);
        Assert.True(request.RequiredContextTokens > request.MaxOutputTokens);
    }

    [Fact]
    public void BuildRequest_RejectsBriefOverServiceLimit()
    {
        string oversizedBrief = new('а', PromptBuilderService.MaxInputLength + 1);

        ArgumentOutOfRangeException error = Assert.Throws<ArgumentOutOfRangeException>(() =>
            _service.BuildRequest(PromptBuilderCategory.Texts, oversizedBrief));

        Assert.Equal("brief", error.ParamName);
    }

    [Fact]
    public void BaseInstruction_KeepsDetailProportionalAndAssumptionsNonRestrictive()
    {
        string prompt = _service.GetSystemPrompt(PromptBuilderCategory.Programming);

        Assert.Contains("proportional to the complexity", prompt);
        Assert.Contains("unnecessary for completing the task", prompt);
        Assert.Contains("do not restrict the user's choices", prompt);
        Assert.Contains("as ready to use as the available information allows", prompt);
    }

    [Fact]
    public void MixedCategories_SelectOnlyRelevantBehavior()
    {
        string mediaPrompt = _service.GetSystemPrompt(PromptBuilderCategory.VideoAudio);
        string analysisPrompt = _service.GetSystemPrompt(PromptBuilderCategory.AnalysisIdeas);

        Assert.Contains("include only parameters relevant to the detected medium", mediaPrompt);
        Assert.Contains("For analysis tasks", analysisPrompt);
        Assert.Contains("For ideation tasks", analysisPrompt);
        Assert.Contains("evaluate feasibility and trade-offs", analysisPrompt);
    }

    [Fact]
    public void CleanResponse_RemovesReasoningWithoutChangingPromptFormatting()
    {
        string result = _service.CleanResponse("<think>analysis</think>\n```text\nReady prompt\n```");

        Assert.Equal("```text\nReady prompt\n```", result);
    }

}
