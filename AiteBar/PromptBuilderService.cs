using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace AiteBar;

public enum PromptBuilderCategory
{
    Programming = 0,
    Images = 1,
    Texts = 2,
    Video = 3,
    Analysis = 4,
    Music = 5,
    Ideas = 6,
    Paintings = 7,
    Animation = 8,
    Icons = 9,
    Graphics = 10,
    ArtNude = 11
}

public enum PaintingStyle
{
    Auto,
    Impressionism,
    PostImpressionism,
    ArtNouveau,
    JapaneseWoodblock,
    Renaissance,
    Baroque,
    Surrealism,
    Cubism,
    Abstract,
    Watercolor,
    OilPaint,
    Romanticism,
    PreRaphaelite,
    Realism,
    Expressionism,
    Fauvism,
    Symbolism,
    ArtDeco,
    InkWash,
    PencilDrawing,
    Gouache,
    Tempera,
    Pastel,
    CharcoalDrawing,
    EtchingEngraving,
    Acrylic,
    Fresco,
    ColoredPencil,
    Linocut,
    Woodcut,
    ScreenPrint,
    MarkerRendering,
    MixedMediaCollage
}

public enum PhotoStyle
{
    Auto,
    ClassicStudioPortrait,
    CinematicPortrait,
    EditorialPortrait,
    HardFlashPortrait,
    EnvironmentalPortrait,
    MonochromePortrait,
    LuxuryEditorialFashion,
    StreetFashion,
    Y2KFashion,
    HighFlashFashion,
    AvantGardeFashion,
    CleanBeauty,
    GlossyBeautyAd,
    ColorGelBeauty,
    MacroBeauty,
    PremiumSkincareBeauty,
    CleanStudioProduct,
    LuxuryProductPhoto,
    TechProductPhoto,
    DarkPremiumProduct,
    SplashProductAd,
    EditorialFood,
    DarkMoodyFood,
    BrightCommercialFood,
    FineDiningFood,
    OverheadTabletopFood,
    CleanArchitecture,
    LuxuryInterior,
    MinimalModernInterior,
    NightExteriorArchitecture,
    BrutalistArchitecture,
    DocumentaryReportage,
    StreetCandid,
    FlashStreet,
    GrittyUrban,
    BlackAndWhiteReportage,
    EpicCinematicLandscape,
    NaturalTravel,
    GoldenHourLandscape,
    MoodyWeatherLandscape,
    AdventureTravel,
    StudioAutomotive,
    NeonNightAutomotive,
    LuxuryAutomotive,
    RollingShotAutomotive,
    OffRoadAutomotive,
    ScientificMacro,
    LuxuryDetailMacro,
    NatureMacro,
    JewelryMacro,
    AbstractTextureMacro,
    FineArtConceptual,
    SurrealConceptual,
    DarkPsychologicalConceptual,
    DreamlikeConceptual,
    FuturisticConceptual,
    PremiumCommercialAd,
    FmcgAdvertisingPhoto,
    TechCampaignPhoto,
    LuxuryAdPhoto,
    BoldBillboardPhoto,
    AnnieLeibovitz,
    PeterLindbergh,
    HelmutNewton,
    RichardAvedon,
    SteveMcCurry,
    HenriCartierBresson,
    SebastiaoSalgado,
    GregoryCrewdson,
    DavidLaChapelle,
    IrvingPenn,
    EllenVonUnwerth,
    MarioTestino,
    TimWalker,
    PaoloRoversi,
    AndreasGursky,
    CindySherman,
    DaidoMoriyama,
    VivianMaier,
    SlimAarons,
    FanHo
}

public enum PhotoSection
{
    All,
    Portrait,
    Fashion,
    Beauty,
    Product,
    Food,
    ArchitectureInterior,
    StreetReportage,
    LandscapeTravel,
    Automotive,
    Macro,
    Conceptual,
    Advertising,
    Photographers
}


public enum ArtNudeStyle
{
    Auto,
    FineArtNude,
    Boudoir,
    SensualPortrait,
    LingerieEditorial,
    SwimwearGlamour,
    HelmutNewton,
    EllenVonUnwerth,
    AcademicNude,
    MythologicalNude,
    ArtNouveauNude,
    JapaneseShungas,
    PinupClassic,
    PinupAirbrush,
    PinupPulpComic,
    FemmeFatale,
    BedroomMorning,
    CandlelitIntimacy
}

public enum TextPromptType
{
    Auto, AnalyticalArticle, BusinessEmail, BlogPost, Instruction, ProductCard,
    CommercialProposal, LandingPage, PressRelease, EducationalText, SocialPost,
    SalesCopy, Editing, Summary, Script, SeoArticle, TechnicalDocumentation, CreativeWriting
}

public enum TextPromptTone { Neutral, Expert, Friendly, Premium, Bold, Formal }

public enum VideoDirection
{
    Auto, Advertising, CinematicScene, ProductVideo, FashionBeauty, Documentary,
    SocialVertical, MusicVideo, Interview, Explainer, ArchitectureInterior,
    TravelLandscape, Action, Timelapse, Macro, StopMotion, ProductAnimation3D, LoopAnimation
}

public enum ProgrammingProjectType
{
    Auto,
    Website,
    LandingPage,
    OnlineStore,
    Dashboard,
    WebApp,
    HtmlGame,
    MiniUtility,
    TelegramBot,
    AutomationScript,
    Parser,
    ApiBackend,
    DesktopApp
}

public enum ProgrammingPromptStyle
{
    Auto,
    MinimalistWebsite,
    CorporateWebsite,
    PremiumShowcase,
    EditorialStudio,
    BrightPromo,
    DarkTech,
    SaasDashboard,
    AnalyticalDashboard,
    ExecutiveDashboard,
    MobileFirstApp,
    ProductiveWebApp,
    CommunityPlatform,
    RetroArcadeGame,
    PuzzleGame,
    NeonActionGame,
    CartoonGame,
    PhysicsToyGame,
    LightweightUtility,
    PowerUserTool,
    ConversationalBot,
    SalesBot,
    ContentParser,
    DataCollector,
    DeveloperApi,
    StartupMvp,
    NativeDesktop,
    CrossPlatformDesktop
}

public enum VisualTargetModel { Universal, GptImage, Flux, NanoBanana, Midjourney, StableDiffusion, GrokImagine }

public enum AnalysisDirection
{
    Auto, Comparison, Recommendation, MarketCompetition, ResearchFactCheck,
    RiskAssessment, Strategy, ProductAnalysis, DataAnalysis, ScenarioPlanning,
    RootCause, SolutionOptions
}

public enum AnimationStyle
{
    Auto,
    Pixar,
    Disney,
    AnimeShonen,
    AnimeGhibli,
    AnimeCyberpunk,
    AnimeShojo,
    AnimePinup,
    AnimeChibi,
    AnimeCinematic,
    AnimeHorror,
    AnimeIsekai,
    AnimeMagicalGirl,
    AnimeMecha,
    AnimeRetro80s,
    AnimeRetro90s,
    AnimeSamurai,
    AnimeSeinen,
    AnimeSliceOfLife,
    AnimeSports,
    AdventureTime,
    Arcane,
    FamilyGuy,
    GravityFalls,
    LooneyTunes,
    RickAndMorty,
    Simpsons,
    SouthPark,
    SpiderVerse,
    TomAndJerry,
    ComicAmerican,
    ComicEuropean,
    ComicManga,
    StopMotion,
    Claymation,
    ClassicFairytale,
    FamilyThreeDimensional,
    PainterlyFairytale,
    TvCartoon,
    MinimalistTvCartoon,
    TeenCartoon,
    RubberHose,
    SlapstickCartoon,
    TheatricalRetroCartoon,
    LigneClaire,
    FrancoBelgianAdventure,
    EuropeanAuteur,
    WatercolorStorybook,
    GouachePencil,
    PaperCutout,
    CrayonPastel,
    TextileStopMotion,
    LowPolyCartoon,
    PopArtCartoon,
    GraphicNovelNoir,
    FlatIllustration,
    MemphisCartoon,
    PsychedelicCartoon,
    InkWashAnimation,
    SketchAnimation,
    CartoonPinup,
    CartoonRockabilly,
    CartoonTiki,
    CartoonArtDeco
}

public enum AnimationStyleSection
{
    All,
    Brands,
    Anime,
    TwoDimensionalTelevision,
    ThreeDimensionalStopMotion,
    ComicsGraphics,
    BookIllustration,
    Retro,
    Experimental
}

public enum PaintingStyleSection { All, Classical, Modern, Decorative, Eastern, Techniques, Artists, Landscape, Portrait, Printmaking }

public enum PaintingArtist
{
    Auto,
    LeonardoDaVinci,
    Michelangelo,
    Raphael,
    Botticelli,
    Caravaggio,
    Rembrandt,
    Vermeer,
    Goya,
    Monet,
    Renoir,
    Degas,
    Manet,
    Pissarro,
    VanGogh,
    Cezanne,
    Gauguin,
    Seurat,
    Picasso,
    Matisse,
    Klimt,
    Schiele,
    Kandinsky,
    Mondrian,
    Chagall,
    Klee,
    Dali,
    Magritte,
    Munch,
    Hopper,
    FridaKahlo,
    TamaraDeLempicka,
    GeorgiaOKeeffe,
    Basquiat,
    Hokusai,
    Hiroshige,
    JMWTurner,
    CasparDavidFriedrich,
    AlphonseMucha,
    JohnWilliamWaterhouse,
    GeorgesBraque,
    IlyaRepin,
    AmedeoModigliani,
    JoanMiro
}

public enum IconStyle
{
    Auto, Flat, GradientFlat, Monochrome, Line, Glyph, Filled, Duotone, Isometric,
    Glassmorphism, Neumorphism, ThreeDimensional, ClayThreeDimensional, PixelArt,
    Retro, HandDrawn, Mascot
}

public enum GraphicType { Auto, Poster, AdvertisingLayout, Banner, Cover, Sticker, StickerPack, VectorIllustration, Infographic, UiElement, Logo, Icon }

public enum GraphicStyle
{
    Auto,
    SwissStyle,
    Bauhaus,
    Constructivism,
    ArtDecoPoster,
    SovietAgitation,
    WartimePropaganda,
    Advertising1950s,
    Psychedelic1960s,
    PunkGrunge,
    CinemaPoster,
    JapanesePoster,
    Glossy1980sAd,
    LuxuryFashionAd,
    TechAd,
    FmcgCommercial,
    PremiumMinimalAd,
    BoldCommercial,
    Y2KPromo,
    CleanCommercialBanner,
    TechLandingBanner,
    SaleBanner,
    PremiumBrandBanner,
    EventPromoBanner,
    GamingBanner,
    ClassicBookCover,
    EditorialMagazineCover,
    RetroAlbumCover,
    ModernAlbumCover,
    FilmSeriesCover,
    TypographicCover,
    ContourSticker,
    MemeSticker,
    GraffitiSticker,
    VinylCutSticker,
    HolographicSticker,
    MessengerStickerPack,
    ChibiStickerPack,
    ReactionStickerPack,
    BrandStickerPack,
    MemeStickerPack,
    FlatVector,
    EditorialIllustration,
    IsometricIllustration,
    HandDrawnIllustration,
    ComicPulpIllustration,
    CollageIllustration,
    MemphisIllustration,
    PixelArtIllustration,
    CorporateInfographic,
    EditorialInfographic,
    TechDashboardInfographic,
    EducationalInfographic,
    DataPosterInfographic,
    FlatUi,
    GlassmorphismUi,
    NeumorphismUi,
    MaterialUi,
    GamingUi,
    SaasMinimalUi,
    MinimalLogo,
    GeometricLogo,
    EmblemLogo,
    WordmarkLogo,
    MascotLogo,
    RetroLogo
}

public sealed record PaintingStyleDefinition(PaintingStyle Style, string LocalizationKey, string PromptDescriptor);
public sealed record PhotoSectionDefinition(PhotoSection Section, string LocalizationKey, string PromptDescriptor);
public sealed record PhotoStyleDefinition(PhotoStyle Style, string LocalizationKey, string PromptDescriptor);
public sealed record ArtNudeStyleDefinition(ArtNudeStyle Style, string LocalizationKey, string PromptDescriptor);
public sealed record TextPromptTypeDefinition(TextPromptType Type, string LocalizationKey, string PromptDescriptor);
public sealed record TextPromptToneDefinition(TextPromptTone Tone, string LocalizationKey, string PromptDescriptor);
public sealed record AnimationStyleDefinition(AnimationStyle Style, string LocalizationKey, string PromptDescriptor);
public sealed record AnimationStyleSectionDefinition(AnimationStyleSection Section, string LocalizationKey, string PromptDescriptor);
public sealed record PaintingStyleSectionDefinition(PaintingStyleSection Section, string LocalizationKey, string PromptDescriptor);
public sealed record PaintingArtistDefinition(PaintingArtist Artist, string LocalizationKey, string PromptDescriptor);
public sealed record AnalysisDirectionDefinition(AnalysisDirection Direction, string LocalizationKey, string OutcomeLocalizationKey, string PromptDescriptor);
public sealed record VideoDirectionDefinition(VideoDirection Direction, string LocalizationKey, string PromptDescriptor);
public sealed record ProgrammingProjectTypeDefinition(ProgrammingProjectType Type, string LocalizationKey, string PromptDescriptor);
public sealed record ProgrammingPromptStyleDefinition(ProgrammingPromptStyle Style, string LocalizationKey, string PromptDescriptor);
public sealed record VisualTargetModelDefinition(VisualTargetModel Model, string LocalizationKey, string PromptDescriptor);
public sealed record IconStyleDefinition(IconStyle Style, string LocalizationKey, string PromptDescriptor);
public sealed record GraphicTypeDefinition(GraphicType Type, string LocalizationKey, string PromptDescriptor);
public sealed record GraphicStyleDefinition(GraphicStyle Style, string LocalizationKey, string PromptDescriptor);


public sealed partial class PromptBuilderService
{
    public const int MaxInputLength = 50_000;
    private const int ContextReservePercent = 15;
    private readonly TextProcessingService _responseCleaner = new();


    private sealed record CategoryProfile(
        string SystemPrompt,
        int MinOutputTokens,
        int MaxOutputTokens,
        double Temperature);

    public string GetSystemPrompt(PromptBuilderCategory category) =>
        category switch
        {
            PromptBuilderCategory.Programming => ProgrammingInstruction,
            PromptBuilderCategory.Images => ImagesInstruction,
            PromptBuilderCategory.Texts => TextsInstruction,
            PromptBuilderCategory.Video => VideoInstruction,
            PromptBuilderCategory.Music => MusicInstruction,
            PromptBuilderCategory.Analysis => AnalysisInstruction,
            PromptBuilderCategory.Ideas => ImagesInstruction,
            PromptBuilderCategory.Paintings => PaintingsInstruction,
            PromptBuilderCategory.Animation => AnimationInstruction,
            PromptBuilderCategory.Icons => GraphicsInstruction,
            PromptBuilderCategory.Graphics => GraphicsInstruction,
            PromptBuilderCategory.ArtNude => ArtNudeInstruction,
            _ => throw new ArgumentOutOfRangeException(nameof(category))
        };

    private static CategoryProfile GetProfile(
        PromptBuilderCategory category) =>
        category switch
        {
            PromptBuilderCategory.Programming => new(
                ProgrammingInstruction,
                2048,
                8192,
                0.20),

            PromptBuilderCategory.Images => new(
                ImagesInstruction,
                512,
                2048,
                0.25),

            PromptBuilderCategory.Paintings => new(
                PaintingsInstruction,
                512,
                2048,
                0.65),

            PromptBuilderCategory.Animation => new(
                AnimationInstruction,
                512,
                2048,
                0.65),

            PromptBuilderCategory.Icons => new(
                GraphicsInstruction,
                512,
                2048,
                0.45),

            PromptBuilderCategory.Graphics => new(
                GraphicsInstruction,
                512,
                2048,
                0.45),

            PromptBuilderCategory.Texts => new(
                TextsInstruction,
                1024,
                4096,
                0.20),

            PromptBuilderCategory.Video => new(
                VideoInstruction,
                512,
                2048,
                0.25),

            PromptBuilderCategory.Music => new(
                MusicInstruction,
                256,
                1024,
                0.30),

            PromptBuilderCategory.Analysis => new(
                AnalysisInstruction,
                2048,
                8192,
                0.25),

            PromptBuilderCategory.Ideas => GetProfile(PromptBuilderCategory.Images),

            PromptBuilderCategory.ArtNude => new(
                ArtNudeInstruction,
                512,
                2048,
                0.55),

            _ => throw new ArgumentOutOfRangeException(nameof(category))
        };

    public AiChatRequest BuildRequest(
        PromptBuilderCategory category,
        string brief,
        int? maxOutputTokens = null,
        bool createAlternative = false,
        PhotoSection photoSection = PhotoSection.All,
        PaintingStyle paintingStyle = PaintingStyle.Auto,
        PaintingArtist paintingArtist = PaintingArtist.Auto,
        AnimationStyle animationStyle = AnimationStyle.Auto,
        PhotoStyle photoStyle = PhotoStyle.Auto,
        TextPromptType textType = TextPromptType.Auto,
        TextPromptTone textTone = TextPromptTone.Neutral,
        AnalysisDirection analysisDirection = AnalysisDirection.Auto,
        VideoDirection videoDirection = VideoDirection.Auto,
        ProgrammingProjectType programmingProjectType = ProgrammingProjectType.Auto,
        ProgrammingPromptStyle programmingStyle = ProgrammingPromptStyle.Auto,
        VisualTargetModel visualTarget = VisualTargetModel.Universal,
        IconStyle iconStyle = IconStyle.Auto,
        GraphicType graphicType = GraphicType.Auto,
        GraphicStyle graphicStyle = GraphicStyle.Auto,
        AnimationStyleSection animationSection = AnimationStyleSection.All,
        PaintingStyleSection paintingSection = PaintingStyleSection.All,
        ArtNudeStyle artNudeStyle = ArtNudeStyle.Auto,
        int rotationOffset = 0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(brief);

        if (brief.Length > MaxInputLength)
        {
            throw new ArgumentOutOfRangeException(
                nameof(brief),
                brief.Length,
                $"The brief cannot exceed {MaxInputLength} characters.");
        }

        string normalizedBrief = brief.Trim();
        CategoryProfile profile = GetProfile(category);
        string systemPrompt = profile.SystemPrompt;
        if (category == PromptBuilderCategory.Images)
        {
            PhotoSectionDefinition section = PhotoSections.FirstOrDefault(item => item.Section == photoSection) ?? PhotoSections[0];
            PhotoStyleDefinition style = (GetPhotoStyles(photoSection).FirstOrDefault(item => item.Style == photoStyle) ?? GetPhotoStyles(photoSection).First());
            string styleDescriptor = style.Style == PhotoStyle.Auto && photoSection != PhotoSection.All
                ? photoSection == PhotoSection.Photographers
                    ? "Select the most fitting photographer reference for the brief."
                    : $"Select the most fitting style within {section.PromptDescriptor}."
                : style.PromptDescriptor;
            styleDescriptor = SanitizeVisualDescriptor(styleDescriptor);
            systemPrompt = systemPrompt.Replace("{photoSection}", section.PromptDescriptor, StringComparison.Ordinal)
                .Replace("{photoStyle}", styleDescriptor, StringComparison.Ordinal);
        }
        if (category == PromptBuilderCategory.Texts)
        {
            TextPromptTypeDefinition type = TextPromptTypes.FirstOrDefault(item => item.Type == textType) ?? TextPromptTypes[0];
            TextPromptToneDefinition tone = TextPromptTones.FirstOrDefault(item => item.Tone == textTone) ?? TextPromptTones[0];
            systemPrompt = systemPrompt.Replace("{textType}", type.PromptDescriptor, StringComparison.Ordinal)
                .Replace("{textTone}", tone.PromptDescriptor, StringComparison.Ordinal);
        }
        if (category == PromptBuilderCategory.Analysis)
        {
            AnalysisDirectionDefinition direction = AnalysisDirections.FirstOrDefault(item => item.Direction == analysisDirection) ?? AnalysisDirections[0];
            systemPrompt = systemPrompt.Replace("{analysisDirection}", direction.PromptDescriptor, StringComparison.Ordinal);
        }
        if (category == PromptBuilderCategory.Video)
        {
            VideoDirectionDefinition direction = VideoDirections.FirstOrDefault(item => item.Direction == videoDirection) ?? VideoDirections[0];
            systemPrompt = systemPrompt.Replace("{videoDirection}", direction.PromptDescriptor, StringComparison.Ordinal);
        }
        if (category == PromptBuilderCategory.Programming)
        {
            ProgrammingProjectTypeDefinition type = ProgrammingProjectTypes.FirstOrDefault(item => item.Type == programmingProjectType) ?? ProgrammingProjectTypes[0];
            ProgrammingPromptStyleDefinition style = GetProgrammingStyles(programmingProjectType).FirstOrDefault(item => item.Style == programmingStyle) ?? GetProgrammingStyles(programmingProjectType).First();
            string styleDescriptor = style.Style == ProgrammingPromptStyle.Auto && programmingProjectType != ProgrammingProjectType.Auto
                ? $"Select the most fitting product style within {type.PromptDescriptor}."
                : style.PromptDescriptor;
            systemPrompt = systemPrompt.Replace("{programmingProjectType}", type.PromptDescriptor, StringComparison.Ordinal)
                .Replace("{programmingStyle}", styleDescriptor, StringComparison.Ordinal);
        }
        if (category is PromptBuilderCategory.Images or PromptBuilderCategory.Paintings or PromptBuilderCategory.Animation or PromptBuilderCategory.Icons or PromptBuilderCategory.Graphics or PromptBuilderCategory.ArtNude)
        {
            VisualTargetModelDefinition target = VisualTargetModels.FirstOrDefault(item => item.Model == visualTarget) ?? VisualTargetModels[0];
            systemPrompt = systemPrompt.Replace("{visualTarget}", target.PromptDescriptor, StringComparison.Ordinal);
        }
        if (category == PromptBuilderCategory.ArtNude)
        {
            ArtNudeStyleDefinition style = ArtNudeStyles.FirstOrDefault(item => item.Style == artNudeStyle) ?? ArtNudeStyles[0];
            systemPrompt = systemPrompt.Replace("{artNudeStyle}", SanitizeVisualDescriptor(style.PromptDescriptor), StringComparison.Ordinal);
        }
        if (category == PromptBuilderCategory.Paintings)
        {
            PaintingStyleDefinition style = PaintingStyles.FirstOrDefault(item => item.Style == paintingStyle) ?? PaintingStyles[0];
            PaintingArtistDefinition artist = PaintingArtists.FirstOrDefault(item => item.Artist == paintingArtist) ?? PaintingArtists[0];
            string descriptor = style.Style == PaintingStyle.Auto && paintingSection != PaintingStyleSection.All
                ? $"Select the most fitting style within {PaintingStyleSections.First(item => item.Section == paintingSection).PromptDescriptor}."
                : style.PromptDescriptor;
            artist = paintingSection == PaintingStyleSection.Artists
                ? artist
                : PaintingArtists[0];
            systemPrompt = systemPrompt.Replace("{paintingStyle}", SanitizeVisualDescriptor(descriptor), StringComparison.Ordinal);
            systemPrompt = systemPrompt.Replace("{paintingArtist}", SanitizeVisualDescriptor(artist.PromptDescriptor), StringComparison.Ordinal);
        }
        if (category == PromptBuilderCategory.Animation)
        {
            AnimationStyleDefinition style = AnimationStyles.FirstOrDefault(item => item.Style == animationStyle) ?? AnimationStyles[0];
            string descriptor = style.Style == AnimationStyle.Auto && animationSection != AnimationStyleSection.All
                ? $"Select the most fitting style within {AnimationStyleSections.First(item => item.Section == animationSection).PromptDescriptor}."
                : style.PromptDescriptor;
            systemPrompt = systemPrompt.Replace("{animationStyle}", SanitizeVisualDescriptor(descriptor), StringComparison.Ordinal);
        }
        if (category is PromptBuilderCategory.Icons or PromptBuilderCategory.Graphics)
        {
            GraphicType effectiveType = category == PromptBuilderCategory.Icons ? GraphicType.Icon : graphicType;
            GraphicTypeDefinition type = GraphicTypes.FirstOrDefault(item => item.Type == effectiveType) ?? GraphicTypes[0];
            string styleDescriptor;
            if (effectiveType == GraphicType.Icon)
            {
                IconStyleDefinition style = IconStyles.FirstOrDefault(item => item.Style == iconStyle) ?? IconStyles[0];
                styleDescriptor = style.PromptDescriptor;
            }
            else
            {
                IReadOnlyList<GraphicStyleDefinition> availableStyles = [.. GetGraphicStyles(effectiveType)];
                GraphicStyleDefinition style = availableStyles.FirstOrDefault(item => item.Style == graphicStyle) ?? availableStyles[0];
                styleDescriptor = style.Style == GraphicStyle.Auto && effectiveType != GraphicType.Auto
                    ? $"Select the most fitting style within {type.PromptDescriptor}."
                    : style.PromptDescriptor;
            }

            systemPrompt = systemPrompt.Replace("{graphicType}", type.PromptDescriptor, StringComparison.Ordinal)
                .Replace("{graphicStyle}", SanitizeVisualDescriptor(styleDescriptor), StringComparison.Ordinal);
        }
        if (createAlternative)
        {
            systemPrompt += category switch
            {
                PromptBuilderCategory.Images or PromptBuilderCategory.Paintings or PromptBuilderCategory.Animation or PromptBuilderCategory.Ideas or PromptBuilderCategory.Icons or PromptBuilderCategory.Graphics or PromptBuilderCategory.ArtNude => "\n\nThis is a retry. Keep every explicit core requirement from the brief and selected options, but create a clearly different art-directed interpretation through unrequested composition, palette, shape language, and finish.",
                PromptBuilderCategory.Video => "\n\nThis is a retry. Keep every explicit core requirement from the brief and the selected video direction, but create a distinct treatment through unrequested shot design, camera path, pacing, lighting, and scene progression.",
                PromptBuilderCategory.Programming => "\n\nThis is an alternative prompt version. Preserve the requested software type, supplied technical facts, and selected product style, but vary the unrequested structure, implementation framing, and feature organization. Do not invent a stack, architecture, or dependencies.",
                PromptBuilderCategory.Analysis => "\n\nThis is an alternative prompt version. Preserve the brief and selected output contract, but vary the unrequested criteria, hypotheses, evidence plan, or prioritization so the downstream analysis offers a genuinely useful second perspective.",
                PromptBuilderCategory.Texts => "\n\nThis is an alternative prompt version. Preserve the brief, selected format, and tone, but vary the unrequested outline, examples, and rhetorical approach.",
                PromptBuilderCategory.Music => "\n\nThis is an alternative style version. Preserve the requested genre, mood, and explicit musical constraints, but vary compatible unrequested arrangement, instrumentation, rhythm, and production details.",
                _ => string.Empty
            };
        }

        int inputTokens =
            TextProcessingService.EstimateTokens(systemPrompt) +
            TextProcessingService.EstimateTokens(normalizedBrief);

        int suggestedOutputTokens = Math.Max(
            profile.MinOutputTokens,
            inputTokens * 2);

        int outputBudget = Math.Clamp(
            suggestedOutputTokens,
            profile.MinOutputTokens,
            profile.MaxOutputTokens);

        if (maxOutputTokens.HasValue)
        {
            outputBudget = Math.Min(
                outputBudget,
                Math.Max(1, maxOutputTokens.Value));
        }

        int requiredContextTokens = inputTokens + outputBudget;

        requiredContextTokens += (int)Math.Ceiling(
            requiredContextTokens *
            (ContextReservePercent / 100.0));

        return new AiChatRequest
        {
            Messages =
            [
                new AiChatMessage("system", systemPrompt),
                new AiChatMessage("user", normalizedBrief)
            ],
            RequiredCapabilities = AiCapabilities.Text,
            RequireFreeModel = true,
            RequireWritingModel = true,
            IsAlternative = createAlternative,
            RotationOffset = rotationOffset,
            RequiredContextTokens = requiredContextTokens,
            MaxOutputTokens = outputBudget,
            Temperature = profile.Temperature
        };
    }

    private static string SanitizeVisualDescriptor(string descriptor)
    {
        string result = System.Text.RegularExpressions.Regex.Replace(
            descriptor,
            @"^Use [^:]+ as a (?:stylistic|photographic) orientation:\s*",
            string.Empty,
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        result = System.Text.RegularExpressions.Regex.Replace(
            result,
            @", inspired by [^.]+(?=\.|$)",
            string.Empty,
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        return result
            .Replace("Pixar-style", "family-friendly 3D", StringComparison.OrdinalIgnoreCase)
            .Replace("Disney-style", "storybook feature-animation", StringComparison.OrdinalIgnoreCase)
            .Replace("Adventure Time-style", "whimsical fantasy-cartoon", StringComparison.OrdinalIgnoreCase)
            .Replace("Arcane-style", "painterly dramatic 3D", StringComparison.OrdinalIgnoreCase)
            .Replace("Family Guy-style", "flat adult-sitcom", StringComparison.OrdinalIgnoreCase)
            .Replace("Gravity Falls-style", "mystery-adventure cartoon", StringComparison.OrdinalIgnoreCase)
            .Replace("Looney Tunes-style", "classic theatrical-cartoon", StringComparison.OrdinalIgnoreCase)
            .Replace("Rick and Morty-style", "adult sci-fi cartoon", StringComparison.OrdinalIgnoreCase)
            .Replace("The Simpsons-style", "flat suburban-sitcom", StringComparison.OrdinalIgnoreCase)
            .Replace("South Park-style", "paper-cutout", StringComparison.OrdinalIgnoreCase)
            .Replace("Spider-Verse-style", "comic-book 3D", StringComparison.OrdinalIgnoreCase)
            .Replace("Tom and Jerry-style", "classic slapstick-cartoon", StringComparison.OrdinalIgnoreCase);
    }

    public string CleanResponse(string rawResponse) => (rawResponse ?? string.Empty).Trim();

    public string CleanResponse(string rawResponse, PromptBuilderCategory category)
    {
        string cleaned = CleanResponse(rawResponse);
        if (category == PromptBuilderCategory.Music)
        {
            cleaned = StripAllMarkdown(cleaned);
            cleaned = TrimMusicPromptToLength(cleaned, 1000);
            cleaned = cleaned.Trim();
        }
        return cleaned;
    }

    private static string StripAllMarkdown(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        string result = text;

        result = System.Text.RegularExpressions.Regex.Replace(result, @"```[\s\S]*?```", string.Empty);
        result = System.Text.RegularExpressions.Regex.Replace(result, @"~~~[\s\S]*?~~~", string.Empty);

        result = System.Text.RegularExpressions.Regex.Replace(result, @"^\s{0,3}#{1,6}\s+", string.Empty, System.Text.RegularExpressions.RegexOptions.Multiline);

        result = System.Text.RegularExpressions.Regex.Replace(result, @"^\s{0,3}(?:[-*+]|\d+\.)\s+", string.Empty, System.Text.RegularExpressions.RegexOptions.Multiline);

        result = System.Text.RegularExpressions.Regex.Replace(result, @"^\s{0,3}>{1,}\s?", string.Empty, System.Text.RegularExpressions.RegexOptions.Multiline);

        result = System.Text.RegularExpressions.Regex.Replace(result, @"^[\s]*[-*_]{3,}\s*$", string.Empty, System.Text.RegularExpressions.RegexOptions.Multiline);

        result = System.Text.RegularExpressions.Regex.Replace(result, @"!\[[^\]]*\]\([^)]*\)", string.Empty);
        result = System.Text.RegularExpressions.Regex.Replace(result, @"\[([^\]]*)\]\([^)]*\)", "$1");
        result = System.Text.RegularExpressions.Regex.Replace(result, @"\[([^\]]*)\]\[[^\]]*\]", "$1");

        result = System.Text.RegularExpressions.Regex.Replace(result, @"\*\*\*([^*]+)\*\*\*", "$1");
        result = System.Text.RegularExpressions.Regex.Replace(result, @"___([^_]+)___", "$1");
        result = System.Text.RegularExpressions.Regex.Replace(result, @"\*\*([^*]+)\*\*", "$1");
        result = System.Text.RegularExpressions.Regex.Replace(result, @"__([^_]+)__", "$1");
        result = System.Text.RegularExpressions.Regex.Replace(result, @"(?<!\*)\*(?!\s)([^*\r\n]+?)(?<!\s)\*(?!\*)", "$1");
        result = System.Text.RegularExpressions.Regex.Replace(result, @"(?<!_)_(?!\s)([^_\r\n]+?)(?<!\s)_(?!_)", "$1");

        result = System.Text.RegularExpressions.Regex.Replace(result, @"~~([^~]+)~~", "$1");
        result = System.Text.RegularExpressions.Regex.Replace(result, @"==([^=]+)==", "$1");
        result = System.Text.RegularExpressions.Regex.Replace(result, @"`([^`]+)`", "$1");

        result = System.Text.RegularExpressions.Regex.Replace(result, @"^\s*\|.*?\|\s*$", string.Empty, System.Text.RegularExpressions.RegexOptions.Multiline);

        result = System.Text.RegularExpressions.Regex.Replace(result, @"<[^>]+>", string.Empty);

        result = System.Text.RegularExpressions.Regex.Replace(result, @"[\[\]{}<>\\\|`~$]", string.Empty);

        result = System.Text.RegularExpressions.Regex.Replace(result, @"(\r?\n){3,}", "\n\n");
        result = System.Text.RegularExpressions.Regex.Replace(result, @"[ \t]{2,}", " ");

        return result.Trim();
    }

    private static string TrimMusicPromptToLength(string prompt, int maxLength)
    {
        if (string.IsNullOrEmpty(prompt) || prompt.Length <= maxLength)
        {
            return prompt;
        }

        string trimmed = prompt.Substring(0, maxLength);

        int lastComma = trimmed.LastIndexOf(',');
        int lastPeriod = trimmed.LastIndexOf('.');
        int lastSpace = trimmed.LastIndexOf(' ');

        int cutAt = -1;
        if (lastPeriod > maxLength / 2)
        {
            cutAt = lastPeriod + 1;
        }
        else if (lastComma > maxLength / 2)
        {
            cutAt = lastComma + 1;
        }
        else if (lastSpace > maxLength / 2)
        {
            cutAt = lastSpace;
        }

        if (cutAt > 0)
        {
            trimmed = trimmed.Substring(0, cutAt);
        }

        return trimmed.Trim();
    }

    internal static string HideReasoningFromStreamingPreview(string rawResponse) =>
        TextProcessingService.HideReasoningFromStreamingPreview(rawResponse);

    internal static bool IsSuitableForWritingModel(AiModelDescriptor model) =>
        TextProcessingService.IsSuitableForWritingModel(model);
}
