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
        Assert.Equal(7, (int)PromptBuilderCategory.Paintings);
        Assert.Equal(8, (int)PromptBuilderCategory.Animation);
        Assert.Equal(9, (int)PromptBuilderCategory.Icons);
        Assert.Equal(10, (int)PromptBuilderCategory.Graphics);
    }

    [Fact]
    public void OldCategoryValues_MigrateCorrectly()
    {
        // Старое значение 3 (VideoAudio) -> новое Video (3)
        Assert.Equal(3, (int)PromptBuilderCategory.Video);
        // Старое значение 4 (AnalysisIdeas) -> новое Analysis (4)
        Assert.Equal(4, (int)PromptBuilderCategory.Analysis);
        // Legacy Ideas is migrated into the unified analytics mode.
        Assert.Equal(5, (int)PromptBuilderCategory.Music);
        Assert.Equal(6, (int)PromptBuilderCategory.Ideas);
    }

    [Theory]
    [InlineData(PromptBuilderCategory.Programming, "Return only the finished prompt")]
    [InlineData(PromptBuilderCategory.Images, "Return only one finished prompt as a natural-language paragraph")]
    [InlineData(PromptBuilderCategory.Texts, "Return only the finished prompt")]
    [InlineData(PromptBuilderCategory.Video, "Return only the finished video prompt in English")]
    [InlineData(PromptBuilderCategory.Music, "Return only the finished style description in English")]
    [InlineData(PromptBuilderCategory.Analysis, "Return only the finished prompt")]
    [InlineData(PromptBuilderCategory.Paintings, "Return only one finished natural-language prompt")]
    [InlineData(PromptBuilderCategory.Animation, "Return only one finished natural-language prompt")]
    [InlineData(PromptBuilderCategory.Icons, "application icon")]
    [InlineData(PromptBuilderCategory.Graphics, "graphic-design asset")]
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
    public void ImagesInstruction_ProducesEnglishCreativeDirectorPromptWithoutLegacySyntax()
    {
        string prompt = _service.GetSystemPrompt(PromptBuilderCategory.Images);

        Assert.Contains("polished English prompt", prompt);
        Assert.Contains("expert art director", prompt);
        Assert.Contains("Apply this selected photo direction: {photoStyle}", prompt);
        Assert.Contains("negative-prompt section, placeholders", prompt);
        Assert.DoesNotContain("Preserve the language of the user's brief", prompt);
    }

    [Fact]
    public void ImagesInstruction_ExplicitlyRequiresPreservationForEditing()
    {
        string prompt = _service.GetSystemPrompt(PromptBuilderCategory.Images);

        Assert.Contains("preserve the unmentioned identity, proportions, pose, composition, and scene continuity", prompt);
        Assert.Contains("do not write a separate negative prompt", prompt);
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
        Assert.Contains("Translate the user's intent into fluent English internally", prompt);
        Assert.DoesNotContain("Preserve the language of the user's brief", prompt);
        Assert.DoesNotContain("You are a video director", prompt, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildRequest_VideoAppliesSelectedDirection()
    {
        AiChatRequest request = _service.BuildRequest(
            PromptBuilderCategory.Video,
            "A watch rotates on a pedestal",
            videoDirection: VideoDirection.ProductVideo);

        Assert.Contains("Premium product film", request.Messages[0].Content);
        Assert.DoesNotContain("{videoDirection}", request.Messages[0].Content);
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
    public void BuildRequest_ProgrammingAppliesSelectedTaskType()
    {
        AiChatRequest request = _service.BuildRequest(
            PromptBuilderCategory.Programming,
            "Fix an intermittent login error",
            programmingTaskType: ProgrammingTaskType.BugFix);

        Assert.Contains("Require reproducible steps", request.Messages[0].Content);
        Assert.DoesNotContain("{programmingTaskType}", request.Messages[0].Content);
    }

    [Fact]
    public void AnalysisInstruction_RequiresEvidenceBoundariesWithoutForcingOtherDirectionContracts()
    {
        string prompt = _service.GetSystemPrompt(PromptBuilderCategory.Analysis).ReplaceLineEndings("\n");

        Assert.Contains("separate:\n- confirmed facts;", prompt);
        Assert.Contains("missing evidence and uncertainty to be stated explicitly", prompt);
        Assert.DoesNotContain("For comparisons, require common criteria", prompt);
    }

    [Fact]
    public void LegacyIdeasCategory_UsesUnifiedAnalyticsInstruction()
    {
        string prompt = _service.GetSystemPrompt(PromptBuilderCategory.Ideas);

        Assert.Equal(_service.GetSystemPrompt(PromptBuilderCategory.Analysis), prompt);
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

        AiChatRequest analyticsRequest = _service.BuildRequest(
            PromptBuilderCategory.Analysis, "test", maxOutputTokens: 1);
        Assert.Equal(0.25, analyticsRequest.Temperature);
    }

    [Fact]
    public void BuildRequest_AnalyticsAppliesSelectedDirection()
    {
        AiChatRequest request = _service.BuildRequest(
            PromptBuilderCategory.Analysis,
            "Choose a database for a desktop application",
            analysisDirection: AnalysisDirection.Recommendation);

        Assert.Contains("conditions that would change the recommendation", request.Messages[0].Content);
        Assert.DoesNotContain("{analysisDirection}", request.Messages[0].Content);
    }

    [Fact]
    public void BuildRequest_AnalyticsComparisonRequiresAnExplicitResultContract()
    {
        AiChatRequest request = _service.BuildRequest(
            PromptBuilderCategory.Analysis,
            "Compare two CRM systems",
            analysisDirection: AnalysisDirection.Comparison);

        Assert.Contains("a table with common criteria", request.Messages[0].Content);
        Assert.Contains("a concise conclusion", request.Messages[0].Content);
    }

    [Fact]
    public void BuildRequest_IconsAppliesPlatformAndStyleWithoutPhotoContract()
    {
        AiChatRequest request = _service.BuildRequest(
            PromptBuilderCategory.Icons,
            "Water tracker with a drop and checkmark",
            iconPlatform: IconPlatform.MacOS,
            iconStyle: IconStyle.Flat);

        Assert.Contains("macOS app icon", request.Messages[0].Content);
        Assert.Contains("flat icon design", request.Messages[0].Content);
        Assert.Contains("recognition at small sizes", request.Messages[0].Content);
        Assert.DoesNotContain("photo direction", request.Messages[0].Content);
    }

    [Fact]
    public void BuildRequest_GraphicsAppliesTypeAndStyle()
    {
        AiChatRequest request = _service.BuildRequest(
            PromptBuilderCategory.Graphics,
            "Friendly plant-care sticker pack",
            graphicType: GraphicType.StickerPack,
            graphicStyle: GraphicStyle.Bold);

        Assert.Contains("cohesive sticker pack", request.Messages[0].Content);
        Assert.Contains("bold graphic design", request.Messages[0].Content);
        Assert.DoesNotContain("{graphicType}", request.Messages[0].Content);
        Assert.DoesNotContain("{graphicStyle}", request.Messages[0].Content);
    }

    [Fact]
    public void IconsAndGraphics_UseSeparateGenerationContracts()
    {
        AiChatRequest iconRequest = _service.BuildRequest(
            PromptBuilderCategory.Icons,
            "Water tracker",
            iconPlatform: IconPlatform.IOS,
            iconStyle: IconStyle.Glyph);
        AiChatRequest graphicRequest = _service.BuildRequest(
            PromptBuilderCategory.Graphics,
            "Water tracker",
            graphicType: GraphicType.Poster,
            graphicStyle: GraphicStyle.Editorial);

        Assert.Contains("application icon", iconRequest.Messages[0].Content);
        Assert.Contains("iOS and iPadOS app icon", iconRequest.Messages[0].Content);
        Assert.Contains("graphic-design asset", graphicRequest.Messages[0].Content);
        Assert.Contains("poster composition", graphicRequest.Messages[0].Content);
        Assert.NotEqual(iconRequest.Messages[0].Content, graphicRequest.Messages[0].Content);
    }

    [Theory]
    [InlineData(PromptBuilderCategory.Programming, "alternative prompt version")]
    [InlineData(PromptBuilderCategory.Texts, "alternative prompt version")]
    [InlineData(PromptBuilderCategory.Video, "retry")]
    [InlineData(PromptBuilderCategory.Analysis, "alternative prompt version")]
    [InlineData(PromptBuilderCategory.Music, "alternative style version")]
    public void BuildRequest_RepeatCreatesCategoryAppropriateAlternative(
        PromptBuilderCategory category,
        string expectedDirective)
    {
        AiChatRequest request = _service.BuildRequest(category, "test", createAlternative: true);

        Assert.Contains(expectedDirective, request.Messages[0].Content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CleanResponse_RemovesReasoningWithoutChangingPromptFormatting()
    {
        string result = _service.CleanResponse("<think>analysis</think>\n```text\nReady prompt\n```");

        Assert.Equal("```text\nReady prompt\n```", result);
    }

    [Fact]
    public void BuildRequest_PaintingsAppliesSelectedStyleAndAlternativeDirection()
    {
        AiChatRequest request = _service.BuildRequest(
            PromptBuilderCategory.Paintings,
            "a woman by a stream",
            createAlternative: true,
            paintingStyle: PaintingStyle.Impressionism);

        Assert.Contains("Impressionist oil painting", request.Messages[0].Content);
        Assert.Contains("This is a retry", request.Messages[0].Content);
        Assert.Equal("a woman by a stream", request.Messages[1].Content);
        Assert.Equal(0.65, request.Temperature);
    }

    [Fact]
    public void PaintingsInstruction_ForbidsUnrequestedFramesAndPresentationContext()
    {
        string prompt = _service.GetSystemPrompt(PromptBuilderCategory.Paintings);

        Assert.Contains("Do not add a frame, border, mat, canvas edge, easel, gallery wall, museum display", prompt);
        Assert.Contains("not a photograph of an artwork", prompt);
    }

    [Fact]
    public void PaintingCatalog_UsesNonExplicitFigureStudyAndPencilDirections()
    {
        Assert.Contains(PromptBuilderService.PaintingStyles, style => style.Style == PaintingStyle.JapaneseShunga && style.PromptDescriptor.Contains("woodblock figure study"));
        Assert.Contains(PromptBuilderService.PaintingStyles, style => style.Style == PaintingStyle.AcademicNude && style.PromptDescriptor.Contains("adult model"));
        Assert.Contains(PromptBuilderService.PaintingStyles, style => style.Style == PaintingStyle.PencilDrawing && style.PromptDescriptor.Contains("graphite pencil drawing"));
    }

    [Fact]
    public void BuildRequest_AnimationAppliesSelectedStyleAndAlternativeDirection()
    {
        AiChatRequest request = _service.BuildRequest(
            PromptBuilderCategory.Animation,
            "a fox detective in a city",
            createAlternative: true,
            animationStyle: AnimationStyle.AnimeCyberpunk);

        Assert.Contains("Cyberpunk anime", request.Messages[0].Content);
        Assert.Contains("This is a retry", request.Messages[0].Content);
        Assert.Equal("a fox detective in a city", request.Messages[1].Content);
        Assert.Equal(0.65, request.Temperature);
    }

    [Fact]
    public void BuildRequest_PhotoAppliesSelectedStyle()
    {
        AiChatRequest request = _service.BuildRequest(
            PromptBuilderCategory.Images,
            "a watch on a table",
            photoStyle: PhotoStyle.Product);

        Assert.Contains("Premium product photography", request.Messages[0].Content);
        Assert.DoesNotContain("{photoStyle}", request.Messages[0].Content);
    }

    [Fact]
    public void BuildRequest_VisualPromptAppliesSelectedTargetModel()
    {
        AiChatRequest request = _service.BuildRequest(
            PromptBuilderCategory.Images,
            "a ceramic cup on a table",
            visualTarget: VisualTargetModel.Flux);

        Assert.Contains("precise, visually specific natural-language description", request.Messages[0].Content);
        Assert.DoesNotContain("{visualTarget}", request.Messages[0].Content);
    }

    [Fact]
    public void BuildRequest_TextAppliesSelectedTypeAndTone()
    {
        AiChatRequest request = _service.BuildRequest(
            PromptBuilderCategory.Texts,
            "launch a product",
            textType: TextPromptType.LandingPage,
            textTone: TextPromptTone.Premium);

        Assert.Contains("Landing page copy", request.Messages[0].Content);
        Assert.Contains("refined premium tone", request.Messages[0].Content);
        Assert.DoesNotContain("{textType}", request.Messages[0].Content);
        Assert.DoesNotContain("{textTone}", request.Messages[0].Content);
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
