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
        Assert.Equal(3, (int)PromptBuilderCategory.Video);
        Assert.Equal(4, (int)PromptBuilderCategory.Analysis);
        Assert.Equal(5, (int)PromptBuilderCategory.Music);
        Assert.Equal(6, (int)PromptBuilderCategory.Ideas);
    }

    [Fact]
    public void OldCategoryValues_MigrateCorrectly()
    {
        // Старое значение 3 (VideoAudio) -> новое Video (3)
        Assert.Equal(3, (int)PromptBuilderCategory.Video);
        // Старое значение 4 (AnalysisIdeas) -> новое Analysis (4)
        Assert.Equal(4, (int)PromptBuilderCategory.Analysis);
        // Новые категории получают новые значения 5 и 6
        Assert.Equal(5, (int)PromptBuilderCategory.Music);
        Assert.Equal(6, (int)PromptBuilderCategory.Ideas);
    }

    [Theory]
    [InlineData(PromptBuilderCategory.Programming, "Return only the finished prompt")]
    [InlineData(PromptBuilderCategory.Images, "Return only the finished image prompt")]
    [InlineData(PromptBuilderCategory.Texts, "Return only the finished prompt")]
    [InlineData(PromptBuilderCategory.Video, "Return only the finished video prompt")]
    [InlineData(PromptBuilderCategory.Music, "Return only the finished style description in English")]
    [InlineData(PromptBuilderCategory.Analysis, "Return only the finished prompt")]
    [InlineData(PromptBuilderCategory.Ideas, "Return only the finished prompt")]
    public void GetSystemPrompt_EnforcesOneShotProfessionalOutput(
        PromptBuilderCategory category,
        string expectedReturnPhrase)
    {
        string prompt = _service.GetSystemPrompt(category);

        Assert.Contains(expectedReturnPhrase, prompt);
        Assert.DoesNotContain("CATEGORY GUIDANCE", prompt);
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
        Assert.Equal(0.20, request.Temperature);
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
    public void ImagesInstruction_DoesNotContainRoleOrTaskStructure()
    {
        string prompt = _service.GetSystemPrompt(PromptBuilderCategory.Images);

        Assert.DoesNotContain("Role:", prompt);
        Assert.DoesNotContain("Objective:", prompt);
        Assert.DoesNotContain("Requirements:", prompt);
        Assert.Contains("describe the visible result directly", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Do not include:\n- a role;", prompt);

        // Фраза "You are an artist" должна присутствовать только как отрицательный пример в запретах
        int artistIndex = prompt.IndexOf("You are an artist", StringComparison.OrdinalIgnoreCase);
        Assert.True(artistIndex >= 0);
        int doNotIncludeIndex = prompt.IndexOf("Do not include:");
        Assert.True(artistIndex > doNotIncludeIndex);
    }

    [Fact]
    public void ImagesInstruction_ExplicitlyRequiresPreservationForEditing()
    {
        string prompt = _service.GetSystemPrompt(PromptBuilderCategory.Images);

        Assert.Contains("preserve all unrelated elements, including identity, facial features, expression, pose, body proportions, clothing, composition, background, lighting, colors, and image dimensions", prompt);
        Assert.Contains("what must remain unchanged", prompt);
    }

    [Fact]
    public void MusicInstruction_ProducesEnglishOnlyStyleDescription()
    {
        string prompt = _service.GetSystemPrompt(PromptBuilderCategory.Music);

        Assert.Contains("Return only the finished style description in English", prompt);
        Assert.Contains("Write one compact natural-language paragraph", prompt);
        Assert.Contains("Suno Styles field", prompt);
        Assert.DoesNotContain("You are a musician", prompt, StringComparison.OrdinalIgnoreCase);

        // "lyrics" и "song titles" должны присутствовать только в секции Do not include (запреты)
        int doNotIncludeIndex = prompt.IndexOf("Do not include:");
        Assert.True(doNotIncludeIndex >= 0);

        int lyricsIndex = prompt.IndexOf("lyrics", StringComparison.OrdinalIgnoreCase);
        Assert.True(lyricsIndex >= 0);
        Assert.True(lyricsIndex > doNotIncludeIndex);

        int songTitlesIndex = prompt.IndexOf("song titles", StringComparison.OrdinalIgnoreCase);
        Assert.True(songTitlesIndex >= 0);
        Assert.True(songTitlesIndex > doNotIncludeIndex);
    }

    [Fact]
    public void VideoInstruction_DescribesMovementAndContinuity()
    {
        string prompt = _service.GetSystemPrompt(PromptBuilderCategory.Video);

        Assert.Contains("action and movement over time", prompt);
        Assert.Contains("camera movement", prompt);
        Assert.Contains("preserve the source image's identity, appearance, composition, clothing, proportions, background, lighting, and visual style", prompt);
        Assert.Contains("what must change and what must remain unchanged", prompt);
        Assert.DoesNotContain("You are a video director", prompt, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ProgrammingInstruction_IncludesRequirementsAcceptanceCriteria()
    {
        string prompt = _service.GetSystemPrompt(PromptBuilderCategory.Programming);

        Assert.Contains("acceptance criteria", prompt);
        Assert.Contains("error handling", prompt);
        Assert.Contains("edge cases", prompt);
        Assert.Contains("preserve the current architecture and existing behavior", prompt);
        Assert.Contains("analyze the supplied code before making changes", prompt);
    }

    [Fact]
    public void AnalysisInstruction_RequiresCommonCriteriaAndTradeOffs()
    {
        string prompt = _service.GetSystemPrompt(PromptBuilderCategory.Analysis);

        Assert.Contains("common criteria and equivalent treatment of every option", prompt);
        Assert.Contains("trade-offs, risks, constraints, and the reasoning behind the recommendation", prompt);
        Assert.Contains("separate:\n- confirmed facts;", prompt);
        Assert.Contains("missing evidence and uncertainty to be stated explicitly", prompt);
    }

    [Fact]
    public void IdeasInstruction_RequiresGenuinelyDifferentIdeas()
    {
        string prompt = _service.GetSystemPrompt(PromptBuilderCategory.Ideas);

        Assert.Contains("Require genuinely different ideas rather than minor variations of one concept", prompt);
        Assert.Contains("practical ideas;", prompt);
        Assert.Contains("original ideas;", prompt);
        Assert.Contains("ambitious ideas", prompt);

        // "academic research task" должна присутствовать только в запрете
        int doNotAutoAssignIndex = prompt.IndexOf("Do not automatically assign a role");
        Assert.True(doNotAutoAssignIndex >= 0);
        int academicResearchIndex = prompt.IndexOf("academic research task", StringComparison.OrdinalIgnoreCase);
        Assert.True(academicResearchIndex >= 0);
        Assert.True(academicResearchIndex > doNotAutoAssignIndex);
    }

    [Fact]
    public void GetProfile_HasIndividualTemperatureAndTokenRanges()
    {
        // Programming
        AiChatRequest programmingRequest = _service.BuildRequest(
            PromptBuilderCategory.Programming, "test", maxOutputTokens: 1);
        Assert.Equal(0.20, programmingRequest.Temperature);

        // Images
        AiChatRequest imagesRequest = _service.BuildRequest(
            PromptBuilderCategory.Images, "test", maxOutputTokens: 1);
        Assert.Equal(0.25, imagesRequest.Temperature);

        // Music
        AiChatRequest musicRequest = _service.BuildRequest(
            PromptBuilderCategory.Music, "test", maxOutputTokens: 1);
        Assert.Equal(0.30, musicRequest.Temperature);

        // Ideas
        AiChatRequest ideasRequest = _service.BuildRequest(
            PromptBuilderCategory.Ideas, "test", maxOutputTokens: 1);
        Assert.Equal(0.35, ideasRequest.Temperature);
    }

    [Fact]
    public void CleanResponse_RemovesReasoningWithoutChangingPromptFormatting()
    {
        string result = _service.CleanResponse("<think>analysis</think>\n```text\nReady prompt\n```");

        Assert.Equal("```text\nReady prompt\n```", result);
    }

    [Fact]
    public void GetSystemPrompt_OutOfRange_ThrowsArgumentOutOfRange()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            _service.GetSystemPrompt((PromptBuilderCategory)999));
    }

    [Fact]
    public void BuildRequest_OutOfRange_ThrowsArgumentOutOfRange()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            _service.BuildRequest((PromptBuilderCategory)999, "test"));
    }
}
