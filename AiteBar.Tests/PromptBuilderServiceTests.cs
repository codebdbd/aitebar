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
        Assert.Equal(11, (int)PromptBuilderCategory.ArtNude);
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
    [InlineData(PromptBuilderCategory.Music, "Return only one finished English style prompt that can be pasted directly into Suno")]
    [InlineData(PromptBuilderCategory.Analysis, "Return only the finished prompt")]
    [InlineData(PromptBuilderCategory.Paintings, "Return only one finished natural-language prompt")]
    [InlineData(PromptBuilderCategory.Animation, "Return only one finished natural-language prompt")]
    [InlineData(PromptBuilderCategory.Graphics, "graphic-design asset")]
    [InlineData(PromptBuilderCategory.ArtNude, "Return only one finished prompt as a natural-language paragraph")]
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
        Assert.Contains("Apply this selected photo section: {photoSection}", prompt);
        Assert.Contains("Apply this selected photo style: {photoStyle}", prompt);
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
    public void BuildRequest_ImagesPhotographersUsesGenericVisualQualities()
    {
        AiChatRequest request = _service.BuildRequest(
            PromptBuilderCategory.Images,
            "Portrait of a woman by the window",
            photoSection: PhotoSection.Photographers,
            photoStyle: PhotoStyle.AnnieLeibovitz);

        Assert.Contains("photographic author references", request.Messages[0].Content);
        Assert.Contains("staged portraiture", request.Messages[0].Content);
        Assert.DoesNotContain("Annie Leibovitz", request.Messages[0].Content);
        Assert.DoesNotContain("{photoStyle}", request.Messages[0].Content);
    }

    [Fact]
    public void GetPhotoStyles_PhotographersSectionReturnsPhotographerReferences()
    {
        PhotoStyle[] styles = PromptBuilderService.GetPhotoStyles(PhotoSection.Photographers)
            .Select(item => item.Style)
            .ToArray();

        Assert.Contains(PhotoStyle.Auto, styles);
        Assert.Contains(PhotoStyle.AnnieLeibovitz, styles);
        Assert.Contains(PhotoStyle.FanHo, styles);
        Assert.DoesNotContain(PhotoStyle.CleanBeauty, styles);
    }

    [Fact]
    public void MusicInstruction_ProducesSunoStylePromptFromVibeOrScene()
    {
        string prompt = _service.GetSystemPrompt(PromptBuilderCategory.Music);

        Assert.Contains("Return only one finished English style prompt that can be pasted directly into Suno", prompt);
        Assert.Contains("Treat the user's brief as a vibe, scene, situation, or emotional starting point", prompt);
        Assert.Contains("Write one compact but information-dense natural-language paragraph", prompt);
        Assert.Contains("Suno Styles field", prompt);
        Assert.Contains("Strongly favor creative but coherent blends", prompt);
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
    public void ProgrammingInstruction_UsesSoftwareTypeAndStyleInsteadOfEngineeringDiagnostics()
    {
        string prompt = _service.GetSystemPrompt(PromptBuilderCategory.Programming);

        Assert.Contains("Apply this selected software type: {programmingProjectType}", prompt);
        Assert.Contains("Apply this selected product style: {programmingStyle}", prompt);
        Assert.Contains("acceptance criteria", prompt);
        Assert.Contains("edge cases", prompt);
        Assert.Contains("product-building or code-generation task", prompt);
        Assert.DoesNotContain("{programmingTaskType}", prompt);
    }

    [Fact]
    public void BuildRequest_ProgrammingAppliesSelectedTypeAndStyle()
    {
        AiChatRequest request = _service.BuildRequest(
            PromptBuilderCategory.Programming,
            "Create a portfolio website for a motion designer",
            programmingProjectType: ProgrammingProjectType.Website,
            programmingStyle: ProgrammingPromptStyle.EditorialStudio);

        Assert.Contains("general website or multi-page web presence", request.Messages[0].Content);
        Assert.Contains("editorial studio style", request.Messages[0].Content);
        Assert.DoesNotContain("{programmingProjectType}", request.Messages[0].Content);
        Assert.DoesNotContain("{programmingStyle}", request.Messages[0].Content);
    }

    [Fact]
    public void ProgrammingStyles_AreFilteredBySelectedType()
    {
        Assert.Contains(PromptBuilderService.GetProgrammingStyles(ProgrammingProjectType.HtmlGame), style => style.Style == ProgrammingPromptStyle.RetroArcadeGame);
        Assert.DoesNotContain(PromptBuilderService.GetProgrammingStyles(ProgrammingProjectType.HtmlGame), style => style.Style == ProgrammingPromptStyle.CorporateWebsite);
        Assert.Contains(PromptBuilderService.GetProgrammingStyles(ProgrammingProjectType.Dashboard), style => style.Style == ProgrammingPromptStyle.SaasDashboard);
        Assert.Contains(PromptBuilderService.GetProgrammingStyles(ProgrammingProjectType.ApiBackend), style => style.Style == ProgrammingPromptStyle.DeveloperApi);
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
    public void LegacyIdeas_FallsBackToImagesPrompt()
    {
        string prompt = _service.GetSystemPrompt(PromptBuilderCategory.Ideas);

        Assert.Contains("Turn the user's brief into one polished English prompt", prompt);
        Assert.Contains("{photoSection}", prompt);
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

        AiChatRequest artNudeRequest = _service.BuildRequest(
            PromptBuilderCategory.ArtNude, "test", maxOutputTokens: 1);
        Assert.Equal(0.55, artNudeRequest.Temperature);
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
    public void BuildRequest_GraphicsIconTypeUsesFormerIconStylesAndFullBleedContract()
    {
        AiChatRequest request = _service.BuildRequest(
            PromptBuilderCategory.Graphics,
            "Water tracker with a drop and checkmark",
            graphicType: GraphicType.Icon,
            iconStyle: IconStyle.Flat);

        Assert.Contains("square icon asset", request.Messages[0].Content);
        Assert.Contains("flat icon design", request.Messages[0].Content);
        Assert.Contains("95-98% of the available composition", request.Messages[0].Content);
        Assert.Contains("no rounded app-tile container", request.Messages[0].Content);
        Assert.DoesNotContain("photo direction", request.Messages[0].Content);
    }

    [Theory]
    [InlineData(PromptBuilderCategory.Images)]
    [InlineData(PromptBuilderCategory.Paintings)]
    [InlineData(PromptBuilderCategory.Animation)]
    [InlineData(PromptBuilderCategory.ArtNude)]
    [InlineData(PromptBuilderCategory.Graphics)]
    public void BuildRequest_GrokImagine_DoesNotAddInterfaceFormatParameters(PromptBuilderCategory category)
    {
        AiChatRequest request = _service.BuildRequest(
            category,
            "A red fox in a snowy forest",
            visualTarget: VisualTargetModel.GrokImagine,
            graphicType: GraphicType.Poster);

        string prompt = request.Messages[0].Content;

        Assert.Contains("creative scene and editing constraints", prompt);
        Assert.Contains("interface supplies those settings", prompt);
        Assert.DoesNotContain("--ar", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("4:5", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("16:9", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("1:1", prompt, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildRequest_GraphicsAppliesTypeAndStyle()
    {
        AiChatRequest request = _service.BuildRequest(
            PromptBuilderCategory.Graphics,
            "Avant-garde theatre poster",
            graphicType: GraphicType.Poster,
            graphicStyle: GraphicStyle.Constructivism);

        Assert.Contains("poster or placard composition", request.Messages[0].Content);
        Assert.Contains("Constructivist poster design", request.Messages[0].Content);
        Assert.DoesNotContain("{graphicType}", request.Messages[0].Content);
        Assert.DoesNotContain("{graphicStyle}", request.Messages[0].Content);
    }

    [Fact]
    public void Graphics_UsesIconStylesOnlyForIconType()
    {
        AiChatRequest iconRequest = _service.BuildRequest(
            PromptBuilderCategory.Graphics,
            "Water tracker",
            graphicType: GraphicType.Icon,
            iconStyle: IconStyle.Glyph);
        AiChatRequest graphicRequest = _service.BuildRequest(
            PromptBuilderCategory.Graphics,
            "premium headphone ad",
            graphicType: GraphicType.AdvertisingLayout,
            graphicStyle: GraphicStyle.LuxuryFashionAd);

        Assert.Contains("square icon asset", iconRequest.Messages[0].Content);
        Assert.Contains("solid glyph icon", iconRequest.Messages[0].Content);
        Assert.Contains("graphic-design asset", graphicRequest.Messages[0].Content);
        Assert.Contains("commercial advertising layout", graphicRequest.Messages[0].Content);
        Assert.Contains("luxury fashion advertising style", graphicRequest.Messages[0].Content);
        Assert.NotEqual(iconRequest.Messages[0].Content, graphicRequest.Messages[0].Content);
    }

    [Fact]
    public void GraphicsStyles_AreFilteredBySelectedType()
    {
        Assert.Contains(PromptBuilderService.GetGraphicStyles(GraphicType.Poster), style => style.Style == GraphicStyle.SwissStyle);
        Assert.DoesNotContain(PromptBuilderService.GetGraphicStyles(GraphicType.Poster), style => style.Style == GraphicStyle.TechAd);
        Assert.Contains(PromptBuilderService.GetGraphicStyles(GraphicType.UiElement), style => style.Style == GraphicStyle.GlassmorphismUi);
        Assert.Contains(PromptBuilderService.GetGraphicStyles(GraphicType.StickerPack), style => style.Style == GraphicStyle.ChibiStickerPack);
    }

    [Theory]
    [InlineData(PromptBuilderCategory.Programming, "alternative prompt version")]
    [InlineData(PromptBuilderCategory.Texts, "alternative prompt version")]
    [InlineData(PromptBuilderCategory.Video, "retry")]
    [InlineData(PromptBuilderCategory.Analysis, "alternative prompt version")]
    [InlineData(PromptBuilderCategory.Ideas, "retry")]
    [InlineData(PromptBuilderCategory.Music, "alternative style version")]
    public void BuildRequest_RepeatCreatesCategoryAppropriateAlternative(
        PromptBuilderCategory category,
        string expectedDirective)
    {
        AiChatRequest request = _service.BuildRequest(category, "test", createAlternative: true);

        Assert.Contains(expectedDirective, request.Messages[0].Content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CleanResponse_PreservesPromptMarkupExactly()
    {
        string result = _service.CleanResponse("<analysis><field>value</field></analysis>\n```text\nReady prompt\n```");

        Assert.Equal("<analysis><field>value</field></analysis>\n```text\nReady prompt\n```", result);
    }

    [Fact]
    public void BuildRequest_PaintingsAppliesSelectedStyleAndAlternativeDirection()
    {
        AiChatRequest request = _service.BuildRequest(
            PromptBuilderCategory.Paintings,
            "a woman by a stream",
            createAlternative: true,
            paintingStyle: PaintingStyle.Impressionism,
            paintingArtist: PaintingArtist.Monet);

        Assert.Contains("Impressionist oil painting", request.Messages[0].Content);
        Assert.Contains("luminous broken brushwork", request.Messages[0].Content);
        Assert.DoesNotContain("Claude Monet", request.Messages[0].Content);
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
    public void PaintingCatalog_UsesPencilAndMediumDirections()
    {
        Assert.Contains(PromptBuilderService.PaintingStyles, style => style.Style == PaintingStyle.PencilDrawing && style.PromptDescriptor.Contains("graphite pencil drawing"));
        Assert.Contains(PromptBuilderService.PaintingStyles, style => style.Style == PaintingStyle.Gouache && style.PromptDescriptor.Contains("opaque matte color layers"));
        Assert.Contains(PromptBuilderService.PaintingStyles, style => style.Style == PaintingStyle.Acrylic && style.PromptDescriptor.Contains("clean opaque modern paint layers"));
        Assert.Contains(PromptBuilderService.PaintingStyles, style => style.Style == PaintingStyle.Fresco && style.PromptDescriptor.Contains("monumental wall-painting surface"));
        Assert.Contains(PromptBuilderService.PaintingStyles, style => style.Style == PaintingStyle.ColoredPencil && style.PromptDescriptor.Contains("layered dry detail"));
        Assert.Contains(PromptBuilderService.PaintingStyles, style => style.Style == PaintingStyle.Pastel && style.PromptDescriptor.Contains("powdery blended color"));
        Assert.Contains(PromptBuilderService.PaintingStyles, style => style.Style == PaintingStyle.EtchingEngraving && style.PromptDescriptor.Contains("incisive linework"));
        Assert.Contains(PromptBuilderService.PaintingStyles, style => style.Style == PaintingStyle.Linocut && style.PromptDescriptor.Contains("bold relief print shapes"));
        Assert.Contains(PromptBuilderService.PaintingStyles, style => style.Style == PaintingStyle.ScreenPrint && style.PromptDescriptor.Contains("flat layered poster color"));
        Assert.Contains(PromptBuilderService.PaintingStyles, style => style.Style == PaintingStyle.MixedMediaCollage && style.PromptDescriptor.Contains("layered cut-paper and painted textures"));
    }

    [Fact]
    public void ArtNudeCatalog_ExposesComprehensiveAestheticStyles()
    {
        Assert.Equal(Enum.GetValues<ArtNudeStyle>(), PromptBuilderService.ArtNudeStyles.Select(style => style.Style));
        Assert.Contains(PromptBuilderService.ArtNudeStyles, style => style.Style == ArtNudeStyle.FineArtNude && style.PromptDescriptor.Contains("chiaroscuro"));
        Assert.Contains(PromptBuilderService.ArtNudeStyles, style => style.Style == ArtNudeStyle.Boudoir && style.PromptDescriptor.Contains("bedroom"));
        Assert.Contains(PromptBuilderService.ArtNudeStyles, style => style.Style == ArtNudeStyle.HelmutNewton && style.PromptDescriptor.Contains("Helmut Newton"));
        Assert.Contains(PromptBuilderService.ArtNudeStyles, style => style.Style == ArtNudeStyle.EllenVonUnwerth && style.PromptDescriptor.Contains("Ellen von Unwerth"));
        Assert.Contains(PromptBuilderService.ArtNudeStyles, style => style.Style == ArtNudeStyle.JapaneseShungas && style.PromptDescriptor.Contains("shunga"));
        Assert.Contains(PromptBuilderService.ArtNudeStyles, style => style.Style == ArtNudeStyle.PinupClassic && style.PromptDescriptor.Contains("pin-up"));
    }

    [Fact]
    public void PaintingArtistCatalog_CoversExpandedArtistReferencesWithDeterministicDescriptors()
    {
        Assert.Equal(Enum.GetValues<PaintingArtist>(), PromptBuilderService.PaintingArtists.Select(artist => artist.Artist));
        Assert.Equal(43, PromptBuilderService.PaintingArtists.Count(artist => artist.Artist != PaintingArtist.Auto));
        Assert.Contains(PromptBuilderService.PaintingArtists, artist => artist.Artist == PaintingArtist.Picasso && artist.PromptDescriptor.Contains("cubist geometry"));
        Assert.Contains(PromptBuilderService.PaintingArtists, artist => artist.Artist == PaintingArtist.Monet && artist.PromptDescriptor.Contains("color patches"));
        Assert.Contains(PromptBuilderService.PaintingArtists, artist => artist.Artist == PaintingArtist.Hokusai && artist.PromptDescriptor.Contains("ukiyo-e line precision"));
        Assert.Contains(PromptBuilderService.PaintingArtists, artist => artist.Artist == PaintingArtist.TamaraDeLempicka && artist.PromptDescriptor.Contains("Art Deco glamour"));
        Assert.Contains(PromptBuilderService.PaintingArtists, artist => artist.Artist == PaintingArtist.JMWTurner && artist.PromptDescriptor.Contains("storm luminosity"));
        Assert.Contains(PromptBuilderService.PaintingArtists, artist => artist.Artist == PaintingArtist.AlphonseMucha && artist.PromptDescriptor.Contains("ornamental poster linework"));
        Assert.Contains(PromptBuilderService.PaintingArtists, artist => artist.Artist == PaintingArtist.GeorgesBraque && artist.PromptDescriptor.Contains("analytical cubist structure"));
        Assert.Contains(PromptBuilderService.PaintingArtists, artist => artist.Artist == PaintingArtist.JoanMiro && artist.PromptDescriptor.Contains("playful biomorphic abstraction"));
    }

    [Fact]
    public void PaintingSections_ExposeDenseGroupedBuckets()
    {
        Assert.Contains(PromptBuilderService.GetPaintingStyles(PaintingStyleSection.Classical), style => style.Style == PaintingStyle.Romanticism);
        Assert.Contains(PromptBuilderService.GetPaintingStyles(PaintingStyleSection.Classical), style => style.Style == PaintingStyle.Realism);
        Assert.Contains(PromptBuilderService.GetPaintingStyles(PaintingStyleSection.Modern), style => style.Style == PaintingStyle.ArtNouveau);
        Assert.Contains(PromptBuilderService.GetPaintingStyles(PaintingStyleSection.Modern), style => style.Style == PaintingStyle.Cubism);
        Assert.Contains(PromptBuilderService.GetPaintingStyles(PaintingStyleSection.Eastern), style => style.Style == PaintingStyle.Woodcut);
        Assert.Contains(PromptBuilderService.GetPaintingStyles(PaintingStyleSection.Eastern), style => style.Style == PaintingStyle.ScreenPrint);
        Assert.Contains(PromptBuilderService.GetPaintingStyles(PaintingStyleSection.Techniques), style => style.Style == PaintingStyle.Acrylic);
    }

    [Fact]
    public void PaintingsInstruction_TreatsArtistReferencesAsOrientationRatherThanIdentityGuarantee()
    {
        string prompt = _service.GetSystemPrompt(PromptBuilderCategory.Paintings);

        Assert.Contains("Apply this chosen artist orientation: {paintingArtist}", prompt);
        Assert.Contains("Treat artist references as strong stylistic orientation only", prompt);
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
    public void AnimationCatalog_CoversEveryStyleWithSpecificModernAndAnimeDirections()
    {
        Assert.Equal(
            Enum.GetValues<AnimationStyle>(),
            PromptBuilderService.AnimationStyles.Select(style => style.Style));
        Assert.Contains(PromptBuilderService.AnimationStyles, style => style.Style == AnimationStyle.Simpsons && style.PromptDescriptor.Contains("yellow-skinned"));
        Assert.Contains(PromptBuilderService.AnimationStyles, style => style.Style == AnimationStyle.Arcane && style.PromptDescriptor.Contains("painterly 3D"));
        Assert.Contains(PromptBuilderService.AnimationStyles, style => style.Style == AnimationStyle.SpiderVerse && style.PromptDescriptor.Contains("comic-book 3D"));
        Assert.Contains(PromptBuilderService.AnimationStyles, style => style.Style == AnimationStyle.AnimeMecha && style.PromptDescriptor.Contains("mechanical design"));
        Assert.Contains(PromptBuilderService.AnimationStyles, style => style.Style == AnimationStyle.AnimeIsekai && style.PromptDescriptor.Contains("magical world"));
        Assert.Contains(PromptBuilderService.AnimationStyles, style => style.Style == AnimationStyle.AnimePinup && style.PromptDescriptor.Contains("adult character"));
        Assert.Contains(PromptBuilderService.AnimationStyles, style => style.Style == AnimationStyle.TvCartoon && style.PromptDescriptor.Contains("thick clean outlines"));
        Assert.Contains(PromptBuilderService.AnimationStyles, style => style.Style == AnimationStyle.LigneClaire && style.PromptDescriptor.Contains("uniform clean contour lines"));
        Assert.Contains(PromptBuilderService.AnimationStyles, style => style.Style == AnimationStyle.TextileStopMotion && style.PromptDescriptor.Contains("felt, fabric, yarn"));
        Assert.Contains(PromptBuilderService.AnimationStyles, style => style.Style == AnimationStyle.PopArtCartoon && style.PromptDescriptor.Contains("no lettering"));
    }

    [Fact]
    public void AnimationSections_PartitionEveryStyleAndConstrainAutoPrompt()
    {
        AnimationStyle[] categorized = PromptBuilderService.AnimationStyleSections
            .Where(section => section.Section != AnimationStyleSection.All)
            .SelectMany(section => PromptBuilderService.GetAnimationStyles(section.Section))
            .Where(style => style.Style != AnimationStyle.Auto)
            .Select(style => style.Style)
            .ToArray();

        Assert.Equal(Enum.GetValues<AnimationStyle>().Length - 1, categorized.Length);
        Assert.Equal(categorized.Length, categorized.Distinct().Count());
        Assert.Equal(Enum.GetValues<AnimationStyle>(), PromptBuilderService.GetAnimationStyles(AnimationStyleSection.All).Select(style => style.Style));

        AiChatRequest request = _service.BuildRequest(
            PromptBuilderCategory.Animation,
            "a fox detective in a city",
            animationSection: AnimationStyleSection.Anime);

        Assert.Contains("within anime visual languages", request.Messages[0].Content);
    }

    [Fact]
    public void BuildRequest_PhotoAppliesSelectedStyle()
    {
        AiChatRequest request = _service.BuildRequest(
            PromptBuilderCategory.Images,
            "a watch on a table",
            photoSection: PhotoSection.Product,
            photoStyle: PhotoStyle.CleanStudioProduct);

        Assert.Contains("product photography", request.Messages[0].Content);
        Assert.Contains("Clean studio product photography", request.Messages[0].Content);
        Assert.DoesNotContain("{photoStyle}", request.Messages[0].Content);
    }

    [Fact]
    public void PhotoStyles_AllSectionExposesTheEntireCatalog()
    {
        Assert.Equal(
            PromptBuilderService.PhotoStyles.Select(style => style.Style),
            PromptBuilderService.GetPhotoStyles(PhotoSection.All).Select(style => style.Style));
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
    public void ArtNudeInstruction_ProducesDirectArtisticPromptWithoutCensorshipEvasion()
    {
        string prompt = _service.GetSystemPrompt(PromptBuilderCategory.ArtNude);

        Assert.Contains("polished English prompt for Grok Imagine, Flux, or SDXL", prompt);
        Assert.Contains("artistic nude, boudoir, sensual portraiture", prompt);
        Assert.Contains("Avoid euphemistic evasion or self-censorship", prompt);
        Assert.Contains("Apply this selected art-nude style: {artNudeStyle}", prompt);
        Assert.Contains("Apply this target-model profile: {visualTarget}", prompt);
    }

    [Fact]
    public void BuildRequest_ArtNudeAppliesSelectedStyleAndVisualTarget()
    {
        AiChatRequest request = _service.BuildRequest(
            PromptBuilderCategory.ArtNude,
            "sensual morning silhouette by the window",
            artNudeStyle: ArtNudeStyle.FineArtNude,
            visualTarget: VisualTargetModel.GrokImagine);

        Assert.Contains("Fine-art nude study", request.Messages[0].Content);
        Assert.Contains("Grok Imagine", request.Messages[0].Content);
        Assert.DoesNotContain("{artNudeStyle}", request.Messages[0].Content);
        Assert.DoesNotContain("{visualTarget}", request.Messages[0].Content);
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
