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
    Graphics = 10
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
    JapaneseShunga,
    AcademicNude,
    MythologicalNude,
    ArtNouveauNude,
    PencilDrawing,
    Gouache,
    Tempera,
    Pastel,
    CharcoalDrawing,
    EtchingEngraving,
    PinupClassic1940s,
    PinupGlamour1950s,
    PinupRockabilly,
    PinupNautical,
    PinupTiki,
    PinupCalendarAdvertising,
    PinupAirbrush,
    PinupTattooFlash,
    PinupPulpComic,
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

public enum ThemeSection
{
    All,
    Horror,
    SciFi,
    Space,
    FairyTales,
    Professions,
    Sports,
    War
}

public enum ThemeStyle
{
    Auto,
    JapaneseHorror,
    LovecraftianHorror,
    GothicOccult,
    CursedHouse,
    AbandonedHospital,
    ForestNightmare,
    OccultRitual,
    SpaceStation,
    DerelictSpaceship,
    PlanetaryColony,
    AsteroidMine,
    FirstContact,
    OrbitalLaboratory,
    NuclearRuinedCity,
    UrbanCombatZone,
    BunkerCommand,
    BattlefieldAftermath,
    EvacuationUnderFire,
    ReconInRuins,
    EnchantedForest,
    WitchHut,
    RoyalCastle,
    UnderwaterKingdom,
    VillageAtForestEdge,
    SpiritLake,
    CyberpunkMegacity,
    RobotJunkyard,
    AndroidFactory,
    UndergroundTechCity,
    PostApocalypticWasteland,
    PortalAnomaly,
    StadiumFinal,
    TrainingMontage,
    BoxingRing,
    StreetBasketball,
    PitLane,
    ExtremeOutdoor,
    SurgeonOperation,
    FirefighterRescue,
    DetectiveCrimeScene,
    ScientistLaboratory,
    PilotCockpit,
    MinerUnderground
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

public enum ProgrammingTaskType
{
    Auto, NewFeature, BugFix, Refactoring, CodeReview, Architecture, Testing,
    Performance, Security, ApiIntegration, Database, UiUx, DevOpsDeployment,
    Documentation, ExistingCodeAnalysis, MigrationDependencies
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

public enum VisualTargetModel { Universal, GptImage, Flux, NanoBanana, Midjourney, StableDiffusion }

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

public enum PaintingStyleSection { All, Classical, Modern, Decorative, Eastern, Techniques, FiguresAndPinup, Artists, Landscape, Portrait, Printmaking }

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

public enum IconPlatform { Auto, MacOS, IOS, Windows11, AndroidMaterialYou, CrossPlatform }

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
public sealed record ThemeSectionDefinition(ThemeSection Section, string LocalizationKey, string PromptDescriptor);
public sealed record ThemeStyleDefinition(ThemeStyle Style, string LocalizationKey, string PromptDescriptor);
public sealed record TextPromptTypeDefinition(TextPromptType Type, string LocalizationKey, string PromptDescriptor);
public sealed record TextPromptToneDefinition(TextPromptTone Tone, string LocalizationKey, string PromptDescriptor);
public sealed record AnimationStyleDefinition(AnimationStyle Style, string LocalizationKey, string PromptDescriptor);
public sealed record AnimationStyleSectionDefinition(AnimationStyleSection Section, string LocalizationKey, string PromptDescriptor);
public sealed record PaintingStyleSectionDefinition(PaintingStyleSection Section, string LocalizationKey, string PromptDescriptor);
public sealed record PaintingArtistDefinition(PaintingArtist Artist, string LocalizationKey, string PromptDescriptor);
public sealed record AnalysisDirectionDefinition(AnalysisDirection Direction, string LocalizationKey, string OutcomeLocalizationKey, string PromptDescriptor);
public sealed record VideoDirectionDefinition(VideoDirection Direction, string LocalizationKey, string PromptDescriptor);
public sealed record ProgrammingTaskTypeDefinition(ProgrammingTaskType Type, string LocalizationKey, string PromptDescriptor);
public sealed record ProgrammingProjectTypeDefinition(ProgrammingProjectType Type, string LocalizationKey, string PromptDescriptor);
public sealed record ProgrammingPromptStyleDefinition(ProgrammingPromptStyle Style, string LocalizationKey, string PromptDescriptor);
public sealed record VisualTargetModelDefinition(VisualTargetModel Model, string LocalizationKey, string PromptDescriptor);
public sealed record IconPlatformDefinition(IconPlatform Platform, string LocalizationKey, string PromptDescriptor);
public sealed record IconStyleDefinition(IconStyle Style, string LocalizationKey, string PromptDescriptor);
public sealed record GraphicTypeDefinition(GraphicType Type, string LocalizationKey, string PromptDescriptor);
public sealed record GraphicStyleDefinition(GraphicStyle Style, string LocalizationKey, string PromptDescriptor);

public sealed class PromptBuilderService
{
    public const int MaxInputLength = 50_000;
    private const int ContextReservePercent = 15;
    private readonly TextProcessingService _responseCleaner = new();

    public static readonly IReadOnlyList<PaintingStyleDefinition> PaintingStyles =
    [
        new(PaintingStyle.Auto, "PaintingStyle_Auto", "Select the most fitting fine-art approach for the brief."),
        new(PaintingStyle.Impressionism, "PaintingStyle_Impressionism", "Impressionist oil painting, luminous broken brushwork, plein-air color, inspired by Claude Monet and Pierre-Auguste Renoir."),
        new(PaintingStyle.PostImpressionism, "PaintingStyle_PostImpressionism", "Post-Impressionist oil painting, expressive textured brushstrokes, saturated color, inspired by Vincent van Gogh and Paul Cezanne."),
        new(PaintingStyle.ArtNouveau, "PaintingStyle_ArtNouveau", "Art Nouveau painting, elegant flowing linework, ornamental botanical motifs, refined decorative composition, inspired by Alphonse Mucha and Gustav Klimt."),
        new(PaintingStyle.JapaneseWoodblock, "PaintingStyle_JapaneseWoodblock", "Japanese ukiyo-e woodblock print, flat planes of color, refined contour lines, deliberate negative space, inspired by Katsushika Hokusai."),
        new(PaintingStyle.Renaissance, "PaintingStyle_Renaissance", "Renaissance oil painting, balanced classical composition, soft chiaroscuro, precise naturalistic detail, inspired by Leonardo da Vinci and Raphael."),
        new(PaintingStyle.Baroque, "PaintingStyle_Baroque", "Baroque oil painting, dramatic directional light, rich depth, theatrical composition, inspired by Caravaggio and Rembrandt."),
        new(PaintingStyle.Surrealism, "PaintingStyle_Surrealism", "Surrealist painting, dreamlike symbolic juxtaposition, meticulous rendered detail, inspired by Rene Magritte and Salvador Dali."),
        new(PaintingStyle.Cubism, "PaintingStyle_Cubism", "Cubist painting, fractured geometric planes, multiple viewpoints, bold spatial abstraction, inspired by Pablo Picasso and Georges Braque."),
        new(PaintingStyle.Abstract, "PaintingStyle_Abstract", "Abstract modern painting, confident shape relationships, expressive color rhythm, inspired by Wassily Kandinsky and Piet Mondrian."),
        new(PaintingStyle.Watercolor, "PaintingStyle_Watercolor", "Fine watercolor painting, transparent layered washes, luminous paper texture, delicate pigment blooms."),
        new(PaintingStyle.OilPaint, "PaintingStyle_OilPaint", "Museum-quality oil painting, tactile impasto and glazed layers, rich pigment, visible confident brushwork."),
        new(PaintingStyle.Romanticism, "PaintingStyle_Romanticism", "Romantic painting, sublime landscape or emotional human drama, luminous atmosphere, inspired by J. M. W. Turner and Caspar David Friedrich."),
        new(PaintingStyle.PreRaphaelite, "PaintingStyle_PreRaphaelite", "Pre-Raphaelite painting, jewel-like detail, poetic narrative, botanical richness, inspired by John William Waterhouse and John Everett Millais."),
        new(PaintingStyle.Realism, "PaintingStyle_Realism", "Realist painting, psychologically observant figures, honest material detail, restrained natural light, inspired by Ilya Repin and Edward Hopper."),
        new(PaintingStyle.Expressionism, "PaintingStyle_Expressionism", "Expressionist painting, emotionally charged line, distorted color, energetic brushwork, inspired by Edvard Munch and Ernst Ludwig Kirchner."),
        new(PaintingStyle.Fauvism, "PaintingStyle_Fauvism", "Fauvist painting, liberated saturated color, simplified forms, decorative energy, inspired by Henri Matisse."),
        new(PaintingStyle.Symbolism, "PaintingStyle_Symbolism", "Symbolist painting, mysterious allegorical imagery, dreamlike mood, rich metaphorical detail, inspired by Odilon Redon and Arnold Bocklin."),
        new(PaintingStyle.ArtDeco, "PaintingStyle_ArtDeco", "Art Deco painting, polished geometric elegance, glamorous silhouettes, streamlined luxury, inspired by Tamara de Lempicka."),
        new(PaintingStyle.InkWash, "PaintingStyle_InkWash", "Chinese ink-wash and sumi-e painting, expressive calligraphic brushwork, restrained tonal washes, contemplative negative space."),
        new(PaintingStyle.JapaneseShunga, "PaintingStyle_JapaneseShunga", "Traditional Japanese Edo-period woodblock figure study, elegant contour lines, flat patterned color, refined domestic composition, respectful fine-art printmaking."),
        new(PaintingStyle.AcademicNude, "PaintingStyle_AcademicNude", "Academic classical figure study of an adult model, anatomically studied pose, soft studio light, respectful fine-art composition."),
        new(PaintingStyle.MythologicalNude, "PaintingStyle_MythologicalNude", "Classical mythological figure painting, idealized adult figures, flowing drapery, harmonious anatomy, luminous landscape, museum-quality oil technique."),
        new(PaintingStyle.ArtNouveauNude, "PaintingStyle_ArtNouveauNude", "Art Nouveau classical figure study of an adult model, graceful flowing contours, botanical ornament, decorative gold and jewel-tone palette, elegant poster composition."),
        new(PaintingStyle.PencilDrawing, "PaintingStyle_PencilDrawing", "Masterful graphite pencil drawing, precise tonal modelling, expressive line weight, visible paper grain, refined hatching and cross-hatching."),
        new(PaintingStyle.Gouache, "PaintingStyle_Gouache", "Refined gouache painting, opaque matte color layers, controlled edges, poster-like clarity, and velvety surface texture."),
        new(PaintingStyle.Tempera, "PaintingStyle_Tempera", "Tempera painting, smooth luminous layers, crisp contours, restrained glazing, and classical panel-painting clarity."),
        new(PaintingStyle.Pastel, "PaintingStyle_Pastel", "Soft pastel painting, powdery blended color, tactile paper tooth, luminous highlights, and delicate atmospheric transitions."),
        new(PaintingStyle.CharcoalDrawing, "PaintingStyle_CharcoalDrawing", "Expressive charcoal drawing, rich dark values, smoky transitions, bold gesture, and textured paper grain."),
        new(PaintingStyle.EtchingEngraving, "PaintingStyle_EtchingEngraving", "Fine etching or engraving, incisive linework, controlled hatching, printmaking texture, and high-contrast tonal structure."),
        new(PaintingStyle.PinupClassic1940s, "PaintingStyle_PinupClassic1940s", "Classic 1940s pin-up illustration of an adult model, polished editorial drawing, elegant pose, warm restrained palette, refined period print texture."),
        new(PaintingStyle.PinupGlamour1950s, "PaintingStyle_PinupGlamour1950s", "1950s glamour pin-up illustration of an adult model, poised studio portrait, polished mid-century color, confident silhouette, sophisticated magazine-ad composition."),
        new(PaintingStyle.PinupRockabilly, "PaintingStyle_PinupRockabilly", "Rockabilly pin-up illustration of an adult model, 1950s rebel fashion, bold tattoo-inspired accents, high-contrast retro palette, lively vintage poster energy."),
        new(PaintingStyle.PinupNautical, "PaintingStyle_PinupNautical", "Nautical pin-up illustration of an adult model, classic sailor-inspired wardrobe, harbor or deck setting, crisp retro color, playful mid-century advertising composition."),
        new(PaintingStyle.PinupTiki, "PaintingStyle_PinupTiki", "Tiki tropical pin-up illustration of an adult model, stylized mid-century resort atmosphere, lush palms, carved decor, warm sunset palette, polished poster design."),
        new(PaintingStyle.PinupCalendarAdvertising, "PaintingStyle_PinupCalendarAdvertising", "Vintage advertising-calendar pin-up illustration of an adult model, polished commercial composition, rich print color, clean reserved layout; do not add lettering unless the brief supplies the exact text."),
        new(PaintingStyle.PinupAirbrush, "PaintingStyle_PinupAirbrush", "1970s-1980s airbrush pin-up illustration of an adult model, smooth luminous gradients, glossy chrome-like highlights, dramatic color transitions, polished retro commercial art."),
        new(PaintingStyle.PinupTattooFlash, "PaintingStyle_PinupTattooFlash", "Pin-up tattoo-flash illustration of an adult model, bold ink contours, limited saturated palette, decorative hearts, roses or banners only when relevant to the brief, crisp screen-print finish."),
        new(PaintingStyle.PinupPulpComic, "PaintingStyle_PinupPulpComic", "Pulp-comic pin-up illustration of an adult model, dramatic ink contours, halftone print texture, bold vintage color, playful narrative tension, polished cover-art composition."),
        new(PaintingStyle.Acrylic, "PaintingStyle_Acrylic", "Acrylic painting, clean opaque modern paint layers, crisp shape definition, confident color blocking, and versatile contemporary surface finish."),
        new(PaintingStyle.Fresco, "PaintingStyle_Fresco", "Fresco painting, monumental wall-painting surface, matte mineral color, architectural scale, and time-worn classical texture."),
        new(PaintingStyle.ColoredPencil, "PaintingStyle_ColoredPencil", "Colored pencil drawing, layered dry detail, controlled hatching, rich paper tooth, and precise luminous color build-up."),
        new(PaintingStyle.Linocut, "PaintingStyle_Linocut", "Linocut print, bold relief print shapes, simplified carving marks, high-contrast ink rhythm, and graphic poster-like impact."),
        new(PaintingStyle.Woodcut, "PaintingStyle_Woodcut", "Woodcut print, carved high-contrast print texture, forceful black shapes, tactile grain, and expressive traditional printmaking character."),
        new(PaintingStyle.ScreenPrint, "PaintingStyle_ScreenPrint", "Screen print, flat layered poster color, clean registration, graphic edge control, and bold edition-print energy."),
        new(PaintingStyle.MarkerRendering, "PaintingStyle_MarkerRendering", "Marker rendering, vivid illustration marker finish, saturated strokes, controlled gradients, and polished concept-art clarity."),
        new(PaintingStyle.MixedMediaCollage, "PaintingStyle_MixedMediaCollage", "Mixed-media collage, layered cut-paper and painted textures, assembled surfaces, tactile overlap, and intentional contemporary composition.")
    ];

    public static readonly IReadOnlyList<PaintingStyleSectionDefinition> PaintingStyleSections =
    [
        new(PaintingStyleSection.All, "PaintingSection_All", "any fine-art painting approach"),
        new(PaintingStyleSection.Artists, "PaintingSection_Artists", "artist references"),
        new(PaintingStyleSection.Classical, "PaintingSection_Classical", "classical and academic painting"),
        new(PaintingStyleSection.Modern, "PaintingSection_Modern", "modern and avant-garde painting"),
        new(PaintingStyleSection.Eastern, "PaintingSection_Eastern", "Eastern painting and printmaking"),
        new(PaintingStyleSection.Techniques, "PaintingSection_Techniques", "material and drawing techniques"),
        new(PaintingStyleSection.FiguresAndPinup, "PaintingSection_FiguresAndPinup", "figure-study and pin-up illustration")
    ];

    public static readonly IReadOnlyList<PaintingArtistDefinition> PaintingArtists =
    [
        new(PaintingArtist.Auto, "PaintingArtist_Auto", "Select the most fitting painterly reference for the brief."),
        new(PaintingArtist.LeonardoDaVinci, "PaintingArtist_LeonardoDaVinci", "Use Leonardo da Vinci as a stylistic orientation: balanced High Renaissance composition, sfumato transitions, anatomical precision, and calm psychological depth."),
        new(PaintingArtist.Michelangelo, "PaintingArtist_Michelangelo", "Use Michelangelo as a stylistic orientation: sculptural anatomy, monumental figures, heroic tension, and powerful fresco-like volume."),
        new(PaintingArtist.Raphael, "PaintingArtist_Raphael", "Use Raphael as a stylistic orientation: harmonious Renaissance balance, graceful figures, lucid composition, and serene clarity."),
        new(PaintingArtist.Botticelli, "PaintingArtist_Botticelli", "Use Sandro Botticelli as a stylistic orientation: elegant linear rhythm, idealized figures, flowing drapery, and lyrical decorative composition."),
        new(PaintingArtist.Caravaggio, "PaintingArtist_Caravaggio", "Use Caravaggio as a stylistic orientation: intense chiaroscuro, dramatic realism, close-cropped staging, and charged emotional contrast."),
        new(PaintingArtist.Rembrandt, "PaintingArtist_Rembrandt", "Use Rembrandt as a stylistic orientation: golden-brown depth, introspective realism, expressive faces, and sculpted light emerging from shadow."),
        new(PaintingArtist.Vermeer, "PaintingArtist_Vermeer", "Use Johannes Vermeer as a stylistic orientation: quiet domestic intimacy, pearly daylight, precise interiors, and refined light geometry."),
        new(PaintingArtist.Goya, "PaintingArtist_Goya", "Use Francisco Goya as a stylistic orientation: dark psychological drama, stark contrast, human intensity, and unsettling narrative force."),
        new(PaintingArtist.Monet, "PaintingArtist_Monet", "Use Claude Monet as a stylistic orientation: shifting light, color patches, atmospheric softness, and luminous broken brushwork."),
        new(PaintingArtist.Renoir, "PaintingArtist_Renoir", "Use Pierre-Auguste Renoir as a stylistic orientation: warm flesh tones, convivial light, soft edges, and sensuous painterly color."),
        new(PaintingArtist.Degas, "PaintingArtist_Degas", "Use Edgar Degas as a stylistic orientation: off-center composition, observed movement, pastel nuance, and candid figure studies."),
        new(PaintingArtist.Manet, "PaintingArtist_Manet", "Use Edouard Manet as a stylistic orientation: bold tonal contrast, direct modernity, simplified forms, and confident painterly economy."),
        new(PaintingArtist.Pissarro, "PaintingArtist_Pissarro", "Use Camille Pissarro as a stylistic orientation: airy rural atmosphere, broken color, observational naturalism, and lived-in landscape rhythm."),
        new(PaintingArtist.VanGogh, "PaintingArtist_VanGogh", "Use Vincent van Gogh as a stylistic orientation: urgent directional brushstrokes, saturated color, emotional turbulence, and radiant texture."),
        new(PaintingArtist.Cezanne, "PaintingArtist_Cezanne", "Use Paul Cezanne as a stylistic orientation: structural brush planes, measured form-building, earthy color relations, and solid geometric simplification."),
        new(PaintingArtist.Gauguin, "PaintingArtist_Gauguin", "Use Paul Gauguin as a stylistic orientation: flat symbolic color, bold contours, decorative simplification, and exoticized dreamlike stillness."),
        new(PaintingArtist.Seurat, "PaintingArtist_Seurat", "Use Georges Seurat as a stylistic orientation: optical color division, pointillist marks, ordered calm, and highly controlled light structure."),
        new(PaintingArtist.Picasso, "PaintingArtist_Picasso", "Use Pablo Picasso as a stylistic orientation: cubist geometry, fractured planes, multiple viewpoints, and assertive spatial abstraction."),
        new(PaintingArtist.Matisse, "PaintingArtist_Matisse", "Use Henri Matisse as a stylistic orientation: liberated color, simplified contours, ornamental flatness, and confident decorative harmony."),
        new(PaintingArtist.Klimt, "PaintingArtist_Klimt", "Use Gustav Klimt as a stylistic orientation: gilded ornament, patterned surfaces, sensual figures, and luxurious symbolic flatness."),
        new(PaintingArtist.Schiele, "PaintingArtist_Schiele", "Use Egon Schiele as a stylistic orientation: angular figures, nervous line, psychological tension, and stark expressive distortion."),
        new(PaintingArtist.Kandinsky, "PaintingArtist_Kandinsky", "Use Wassily Kandinsky as a stylistic orientation: abstract color energy, dynamic geometry, rhythmic marks, and musical visual movement."),
        new(PaintingArtist.Mondrian, "PaintingArtist_Mondrian", "Use Piet Mondrian as a stylistic orientation: rectilinear structure, primary-color balance, disciplined abstraction, and purified geometry."),
        new(PaintingArtist.Chagall, "PaintingArtist_Chagall", "Use Marc Chagall as a stylistic orientation: floating dream imagery, poetic symbolism, jewel-toned color, and folkloric tenderness."),
        new(PaintingArtist.Klee, "PaintingArtist_Klee", "Use Paul Klee as a stylistic orientation: playful symbol systems, delicate line, color blocks, and lyrical abstraction."),
        new(PaintingArtist.Dali, "PaintingArtist_Dali", "Use Salvador Dali as a stylistic orientation: hyper-detailed surrealism, uncanny symbolism, dream logic, and polished illusionistic surfaces."),
        new(PaintingArtist.Magritte, "PaintingArtist_Magritte", "Use Rene Magritte as a stylistic orientation: clear realist rendering, conceptual paradox, calm enigma, and unexpected symbolic juxtapositions."),
        new(PaintingArtist.Munch, "PaintingArtist_Munch", "Use Edvard Munch as a stylistic orientation: existential emotion, wavering contours, symbolic color, and anxious atmospheric distortion."),
        new(PaintingArtist.Hopper, "PaintingArtist_Hopper", "Use Edward Hopper as a stylistic orientation: solitary mood, architectural stillness, cinematic light, and emotionally charged emptiness."),
        new(PaintingArtist.FridaKahlo, "PaintingArtist_FridaKahlo", "Use Frida Kahlo as a stylistic orientation: frontal symbolism, intimate self-revelation, vivid Mexican color, and emotionally charged iconography."),
        new(PaintingArtist.TamaraDeLempicka, "PaintingArtist_TamaraDeLempicka", "Use Tamara de Lempicka as a stylistic orientation: sleek Art Deco glamour, polished volumes, elegant geometry, and lacquered modern luxury."),
        new(PaintingArtist.GeorgiaOKeeffe, "PaintingArtist_GeorgiaOKeeffe", "Use Georgia O'Keeffe as a stylistic orientation: enlarged organic forms, desert clarity, smooth tonal transitions, and meditative simplification."),
        new(PaintingArtist.Basquiat, "PaintingArtist_Basquiat", "Use Jean-Michel Basquiat as a stylistic orientation: raw neo-expressionist energy, graffiti-like marks, fractured symbols, and urgent urban color."),
        new(PaintingArtist.Hokusai, "PaintingArtist_Hokusai", "Use Katsushika Hokusai as a stylistic orientation: ukiyo-e line precision, bold wave-like movement, flat color planes, and graphic compositional clarity."),
        new(PaintingArtist.Hiroshige, "PaintingArtist_Hiroshige", "Use Utagawa Hiroshige as a stylistic orientation: lyrical landscape printmaking, atmospheric weather, elegant framing, and flat tonal gradation."),
        new(PaintingArtist.JMWTurner, "PaintingArtist_JMWTurner", "Use J. M. W. Turner as a stylistic orientation: atmospheric light, storm luminosity, dissolving form, and radiant landscape drama."),
        new(PaintingArtist.CasparDavidFriedrich, "PaintingArtist_CasparDavidFriedrich", "Use Caspar David Friedrich as a stylistic orientation: contemplative sublime landscape, quiet symbolism, crisp silhouettes, and meditative northern atmosphere."),
        new(PaintingArtist.AlphonseMucha, "PaintingArtist_AlphonseMucha", "Use Alphonse Mucha as a stylistic orientation: ornamental poster linework, floral framing, elegant idealization, and flowing Art Nouveau rhythm."),
        new(PaintingArtist.JohnWilliamWaterhouse, "PaintingArtist_JohnWilliamWaterhouse", "Use John William Waterhouse as a stylistic orientation: poetic Pre-Raphaelite narrative, luminous figures, botanical detail, and wistful romantic atmosphere."),
        new(PaintingArtist.GeorgesBraque, "PaintingArtist_GeorgesBraque", "Use Georges Braque as a stylistic orientation: analytical cubist structure, muted fractured planes, and disciplined spatial construction."),
        new(PaintingArtist.IlyaRepin, "PaintingArtist_IlyaRepin", "Use Ilya Repin as a stylistic orientation: dramatic realist human presence, psychological intensity, and richly observed social detail."),
        new(PaintingArtist.AmedeoModigliani, "PaintingArtist_AmedeoModigliani", "Use Amedeo Modigliani as a stylistic orientation: elongated portrait elegance, simplified volumes, and restrained melancholic stylization."),
        new(PaintingArtist.JoanMiro, "PaintingArtist_JoanMiro", "Use Joan Miro as a stylistic orientation: playful biomorphic abstraction, floating symbols, primary accents, and lyrical surreal rhythm.")
    ];

    public static IEnumerable<PaintingStyleDefinition> GetPaintingStyles(PaintingStyleSection section) =>
        section == PaintingStyleSection.Artists
            ? PaintingStyles.Where(style => style.Style == PaintingStyle.Auto)
            : PaintingStyles.Where(style => style.Style == PaintingStyle.Auto || section == PaintingStyleSection.All || GetPaintingStyleSection(style.Style) == section);

    private static PaintingStyleSection GetPaintingStyleSection(PaintingStyle style) => style switch
    {
        PaintingStyle.Renaissance or PaintingStyle.Baroque or PaintingStyle.Romanticism or PaintingStyle.PreRaphaelite or PaintingStyle.Realism => PaintingStyleSection.Classical,
        PaintingStyle.Impressionism or PaintingStyle.PostImpressionism or PaintingStyle.ArtNouveau or PaintingStyle.Surrealism or PaintingStyle.Cubism or PaintingStyle.Abstract or PaintingStyle.Expressionism or PaintingStyle.Fauvism or PaintingStyle.Symbolism or PaintingStyle.ArtDeco => PaintingStyleSection.Modern,
        PaintingStyle.InkWash or PaintingStyle.JapaneseWoodblock or PaintingStyle.JapaneseShunga or PaintingStyle.EtchingEngraving or PaintingStyle.Linocut or PaintingStyle.Woodcut or PaintingStyle.ScreenPrint => PaintingStyleSection.Eastern,
        PaintingStyle.Watercolor or PaintingStyle.OilPaint or PaintingStyle.PencilDrawing or PaintingStyle.Gouache or PaintingStyle.Tempera or PaintingStyle.Pastel or PaintingStyle.CharcoalDrawing or PaintingStyle.Acrylic or PaintingStyle.Fresco or PaintingStyle.ColoredPencil or PaintingStyle.MarkerRendering or PaintingStyle.MixedMediaCollage => PaintingStyleSection.Techniques,
        PaintingStyle.AcademicNude or PaintingStyle.MythologicalNude or PaintingStyle.ArtNouveauNude or PaintingStyle.PinupClassic1940s or PaintingStyle.PinupGlamour1950s or PaintingStyle.PinupRockabilly or PaintingStyle.PinupNautical or PaintingStyle.PinupTiki or PaintingStyle.PinupCalendarAdvertising or PaintingStyle.PinupAirbrush or PaintingStyle.PinupTattooFlash or PaintingStyle.PinupPulpComic => PaintingStyleSection.FiguresAndPinup,
        _ => PaintingStyleSection.All
    };

    public static readonly IReadOnlyList<PhotoSectionDefinition> PhotoSections =
    [
        new(PhotoSection.All, "PhotoSection_All", "any photographic direction"),
        new(PhotoSection.Portrait, "PhotoSection_Portrait", "portrait photography"),
        new(PhotoSection.Fashion, "PhotoSection_Fashion", "fashion photography"),
        new(PhotoSection.Beauty, "PhotoSection_Beauty", "beauty photography"),
        new(PhotoSection.Product, "PhotoSection_Product", "product photography"),
        new(PhotoSection.Food, "PhotoSection_Food", "food photography"),
        new(PhotoSection.ArchitectureInterior, "PhotoSection_ArchitectureInterior", "architecture and interior photography"),
        new(PhotoSection.StreetReportage, "PhotoSection_StreetReportage", "street and reportage photography"),
        new(PhotoSection.LandscapeTravel, "PhotoSection_LandscapeTravel", "landscape and travel photography"),
        new(PhotoSection.Automotive, "PhotoSection_Automotive", "automotive photography"),
        new(PhotoSection.Macro, "PhotoSection_Macro", "macro photography"),
        new(PhotoSection.Conceptual, "PhotoSection_Conceptual", "conceptual photography"),
        new(PhotoSection.Advertising, "PhotoSection_Advertising", "advertising photography"),
        new(PhotoSection.Photographers, "PhotoSection_Photographers", "photographic author references")
    ];

    public static readonly IReadOnlyList<PhotoStyleDefinition> PhotoStyles =
    [
        new(PhotoStyle.Auto, "PhotoStyle_Auto", "Naturalistic premium photography with accurate skin tones, balanced exposure, realistic dynamic range, and detailed shadows and highlights."),
        new(PhotoStyle.ClassicStudioPortrait, "PhotoStyle_ClassicStudioPortrait", "Classic studio portrait photography, controlled key light, clean facial modelling, polished pose, and timeless portrait discipline."),
        new(PhotoStyle.CinematicPortrait, "PhotoStyle_CinematicPortrait", "Cinematic portrait photography, motivated film lighting, dramatic tonal depth, controlled atmosphere, and screen-like presence."),
        new(PhotoStyle.EditorialPortrait, "PhotoStyle_EditorialPortrait", "Editorial portrait photography, magazine-quality composition, refined styling, purposeful attitude, and polished visual narrative."),
        new(PhotoStyle.HardFlashPortrait, "PhotoStyle_HardFlashPortrait", "Hard-flash portrait photography, direct flash pop, crisp shadows, fashion-forward immediacy, and bold contemporary edge."),
        new(PhotoStyle.EnvironmentalPortrait, "PhotoStyle_EnvironmentalPortrait", "Environmental portrait photography, authentic location context, natural presence, and balanced subject-background storytelling."),
        new(PhotoStyle.MonochromePortrait, "PhotoStyle_MonochromePortrait", "Black-and-white portrait photography, rich monochrome tonal scale, sculpted light, and emotionally precise facial presence."),
        new(PhotoStyle.LuxuryEditorialFashion, "PhotoStyle_LuxuryEditorialFashion", "Luxury editorial fashion photography, premium styling, elongated pose discipline, glossy magazine polish, and restrained elegance."),
        new(PhotoStyle.StreetFashion, "PhotoStyle_StreetFashion", "Street-fashion photography, urban spontaneity, trend-led styling, natural attitude, and contemporary city energy."),
        new(PhotoStyle.Y2KFashion, "PhotoStyle_Y2KFashion", "Y2K fashion photography, glossy millennium-era styling, playful futurist attitude, flash-led pop energy, and trend nostalgia."),
        new(PhotoStyle.HighFlashFashion, "PhotoStyle_HighFlashFashion", "High-flash fashion photography, direct flash contrast, stylized confidence, clean skin detail, and campaign-ready sharpness."),
        new(PhotoStyle.AvantGardeFashion, "PhotoStyle_AvantGardeFashion", "Avant-garde fashion photography, experimental pose language, dramatic styling, conceptual edge, and high-fashion visual tension."),
        new(PhotoStyle.CleanBeauty, "PhotoStyle_CleanBeauty", "Clean beauty photography, soft flattering light, refined skin texture, fresh makeup detail, and premium cosmetics clarity."),
        new(PhotoStyle.GlossyBeautyAd, "PhotoStyle_GlossyBeautyAd", "Glossy beauty advertising photography, polished cosmetic finish, luminous skin, precise product glamour, and commercial campaign sheen."),
        new(PhotoStyle.ColorGelBeauty, "PhotoStyle_ColorGelBeauty", "Color-gel beauty photography, stylized colored light, sculpted facial geometry, bold cosmetic mood, and controlled editorial drama."),
        new(PhotoStyle.MacroBeauty, "PhotoStyle_MacroBeauty", "Macro beauty photography, extreme cosmetic detail, tactile skin or product texture, shallow focus, and luxury close-up precision."),
        new(PhotoStyle.PremiumSkincareBeauty, "PhotoStyle_PremiumSkincareBeauty", "Premium skincare photography, luminous dewy skin, high-end wellness polish, soft modern light, and restrained luxury clarity."),
        new(PhotoStyle.CleanStudioProduct, "PhotoStyle_CleanStudioProduct", "Clean studio product photography, precise reflections, uncluttered set design, accurate material rendering, and catalog-ready clarity."),
        new(PhotoStyle.LuxuryProductPhoto, "PhotoStyle_LuxuryProductPhoto", "Luxury product photography, rich controlled highlights, tactile premium materials, dark refined atmosphere, and elevated commercial polish."),
        new(PhotoStyle.TechProductPhoto, "PhotoStyle_TechProductPhoto", "Tech product photography, sleek precision, modern studio lighting, crisp industrial detail, and premium device-market presentation."),
        new(PhotoStyle.DarkPremiumProduct, "PhotoStyle_DarkPremiumProduct", "Dark premium product photography, low-key lighting, rich shadow separation, dramatic reflections, and sophisticated luxury-market tension."),
        new(PhotoStyle.SplashProductAd, "PhotoStyle_SplashProductAd", "Dynamic splash product advertising photography, energetic motion cues, controlled liquid or particle drama, and bold campaign impact."),
        new(PhotoStyle.EditorialFood, "PhotoStyle_EditorialFood", "Editorial food photography, chef-table authenticity, appetizing texture, natural plating realism, and magazine-quality composition."),
        new(PhotoStyle.DarkMoodyFood, "PhotoStyle_DarkMoodyFood", "Dark moody food photography, low-key restaurant atmosphere, rich texture, dramatic highlights, and intimate culinary depth."),
        new(PhotoStyle.BrightCommercialFood, "PhotoStyle_BrightCommercialFood", "Bright commercial food photography, vivid appetizing color, clean highlights, accessible mass-market polish, and ad-ready freshness."),
        new(PhotoStyle.FineDiningFood, "PhotoStyle_FineDiningFood", "Fine-dining food photography, elegant plating, restrained luxury light, delicate garnish detail, and upscale culinary precision."),
        new(PhotoStyle.OverheadTabletopFood, "PhotoStyle_OverheadTabletopFood", "Overhead tabletop food photography, top-down composition, curated tabletop styling, and clear ingredient or serving layout."),
        new(PhotoStyle.CleanArchitecture, "PhotoStyle_CleanArchitecture", "Clean architectural photography, precise verticals, balanced daylight, calm geometry, and professional structural clarity."),
        new(PhotoStyle.LuxuryInterior, "PhotoStyle_LuxuryInterior", "Luxury interior photography, premium materials, warm controlled ambience, polished spatial depth, and aspirational design-market presentation."),
        new(PhotoStyle.MinimalModernInterior, "PhotoStyle_MinimalModernInterior", "Minimal modern interior photography, restrained palette, clean spatial rhythm, contemporary styling, and uncluttered design clarity."),
        new(PhotoStyle.NightExteriorArchitecture, "PhotoStyle_NightExteriorArchitecture", "Night exterior architecture photography, controlled building illumination, atmospheric urban context, and elegant structural silhouette."),
        new(PhotoStyle.BrutalistArchitecture, "PhotoStyle_BrutalistArchitecture", "Brutalist architecture photography, severe massing, concrete texture, stark geometry, and powerful monumental mood."),
        new(PhotoStyle.DocumentaryReportage, "PhotoStyle_DocumentaryReportage", "Documentary reportage photography, observed reality, available light, candid decisive moments, and truthful real-world texture."),
        new(PhotoStyle.StreetCandid, "PhotoStyle_StreetCandid", "Street photography, candid human moments, authentic urban context, and natural observational immediacy."),
        new(PhotoStyle.FlashStreet, "PhotoStyle_FlashStreet", "Flash street photography, direct on-camera flash, gritty nightlife energy, raw immediacy, and contemporary urban attitude."),
        new(PhotoStyle.GrittyUrban, "PhotoStyle_GrittyUrban", "Gritty urban photography, rough city texture, hard realism, contrasty atmosphere, and lived-in street presence."),
        new(PhotoStyle.BlackAndWhiteReportage, "PhotoStyle_BlackAndWhiteReportage", "Black-and-white reportage photography, journalistic clarity, decisive framing, and classic documentary tonal strength."),
        new(PhotoStyle.EpicCinematicLandscape, "PhotoStyle_EpicCinematicLandscape", "Epic cinematic landscape photography, sweeping depth, dramatic atmosphere, broad environmental scale, and filmic tonal drama."),
        new(PhotoStyle.NaturalTravel, "PhotoStyle_NaturalTravel", "Natural travel photography, honest environmental beauty, balanced exposure, immersive place sense, and realistic destination atmosphere."),
        new(PhotoStyle.GoldenHourLandscape, "PhotoStyle_GoldenHourLandscape", "Golden-hour landscape photography, warm directional light, long shadows, atmospheric depth, and luminous natural color."),
        new(PhotoStyle.MoodyWeatherLandscape, "PhotoStyle_MoodyWeatherLandscape", "Moody weather landscape photography, cloud drama, cool atmosphere, shifting visibility, and emotionally charged natural light."),
        new(PhotoStyle.AdventureTravel, "PhotoStyle_AdventureTravel", "Adventure travel photography, active outdoor energy, expansive location storytelling, and rugged experiential atmosphere."),
        new(PhotoStyle.StudioAutomotive, "PhotoStyle_StudioAutomotive", "Studio automotive photography, sculpted body reflections, premium surface control, and showroom-grade vehicle presentation."),
        new(PhotoStyle.NeonNightAutomotive, "PhotoStyle_NeonNightAutomotive", "Neon-night automotive photography, wet street reflections, saturated practical lights, urban speed mood, and dramatic vehicle presence."),
        new(PhotoStyle.LuxuryAutomotive, "PhotoStyle_LuxuryAutomotive", "Luxury automotive photography, premium brand polish, restrained environment, elegant body lines, and aspirational commercial finish."),
        new(PhotoStyle.RollingShotAutomotive, "PhotoStyle_RollingShotAutomotive", "Rolling-shot automotive photography, motion-blurred surroundings, sharp vehicle subject, controlled speed energy, and dynamic road presence."),
        new(PhotoStyle.OffRoadAutomotive, "PhotoStyle_OffRoadAutomotive", "Off-road automotive photography, rugged terrain context, dust or mud realism, adventurous atmosphere, and powerful capability emphasis."),
        new(PhotoStyle.ScientificMacro, "PhotoStyle_ScientificMacro", "Scientific macro photography, clean focus priority, precise specimen detail, neutral rendering, and analytical clarity."),
        new(PhotoStyle.LuxuryDetailMacro, "PhotoStyle_LuxuryDetailMacro", "Luxury detail macro photography, premium texture close-up, refined highlights, shallow focus elegance, and high-end material emphasis."),
        new(PhotoStyle.NatureMacro, "PhotoStyle_NatureMacro", "Nature macro photography, organic micro-detail, delicate depth of field, natural color fidelity, and intimate biological texture."),
        new(PhotoStyle.JewelryMacro, "PhotoStyle_JewelryMacro", "Jewelry macro photography, gemstone brilliance, precious-metal reflections, precision focus, and boutique luxury polish."),
        new(PhotoStyle.AbstractTextureMacro, "PhotoStyle_AbstractTextureMacro", "Abstract texture macro photography, graphic micro-surface detail, shallow-focus abstraction, and tactile visual intrigue."),
        new(PhotoStyle.FineArtConceptual, "PhotoStyle_FineArtConceptual", "Fine-art conceptual photography, symbolic staging, deliberate composition, restrained narrative ambiguity, and gallery-like visual intent."),
        new(PhotoStyle.SurrealConceptual, "PhotoStyle_SurrealConceptual", "Surreal conceptual photography, uncanny juxtapositions, dream logic, polished visual control, and imaginative symbolic tension."),
        new(PhotoStyle.DarkPsychologicalConceptual, "PhotoStyle_DarkPsychologicalConceptual", "Dark psychological conceptual photography, oppressive mood, inner-tension symbolism, dramatic light, and emotionally loaded staging."),
        new(PhotoStyle.DreamlikeConceptual, "PhotoStyle_DreamlikeConceptual", "Dreamlike conceptual photography, soft atmosphere, poetic ambiguity, luminous haze, and floating emotionally suggestive imagery."),
        new(PhotoStyle.FuturisticConceptual, "PhotoStyle_FuturisticConceptual", "Futuristic conceptual photography, speculative design cues, sleek atmosphere, forward-looking visual logic, and cinematic contemporary imagination."),
        new(PhotoStyle.PremiumCommercialAd, "PhotoStyle_PremiumCommercialAd", "Premium commercial advertising photography, high-end art direction, product or message clarity, polished lighting, and broad campaign readiness."),
        new(PhotoStyle.FmcgAdvertisingPhoto, "PhotoStyle_FmcgAdvertisingPhoto", "FMCG advertising photography, bright accessible appeal, direct product emphasis, clean promotional energy, and broad-market clarity."),
        new(PhotoStyle.TechCampaignPhoto, "PhotoStyle_TechCampaignPhoto", "Tech campaign photography, sleek innovation cues, modern controlled light, product-led confidence, and polished launch-campaign presence."),
        new(PhotoStyle.LuxuryAdPhoto, "PhotoStyle_LuxuryAdPhoto", "Luxury advertising photography, aspirational restraint, tactile premium cues, elegant set control, and high-status commercial finish."),
        new(PhotoStyle.BoldBillboardPhoto, "PhotoStyle_BoldBillboardPhoto", "Bold billboard-style advertising photography, oversized focal clarity, decisive campaign impact, and unmistakable visual readability at distance."),
        new(PhotoStyle.AnnieLeibovitz, "PhotoStyle_AnnieLeibovitz", "Use Annie Leibovitz as a photographic orientation: staged portraiture, sculpted magazine light, celebrity polish, and cinematic editorial presence."),
        new(PhotoStyle.PeterLindbergh, "PhotoStyle_PeterLindbergh", "Use Peter Lindbergh as a photographic orientation: black-and-white fashion realism, natural faces, wind and texture, and understated supermodel presence."),
        new(PhotoStyle.HelmutNewton, "PhotoStyle_HelmutNewton", "Use Helmut Newton as a photographic orientation: provocative fashion tension, hard controlled light, glossy erotic confidence, and sharp luxury attitude."),
        new(PhotoStyle.RichardAvedon, "PhotoStyle_RichardAvedon", "Use Richard Avedon as a photographic orientation: clean background portraiture, graphic pose language, crisp expression, and stripped-down studio authority."),
        new(PhotoStyle.SteveMcCurry, "PhotoStyle_SteveMcCurry", "Use Steve McCurry as a photographic orientation: saturated documentary color, vivid human presence, expressive eyes, and strong travel portrait immediacy."),
        new(PhotoStyle.HenriCartierBresson, "PhotoStyle_HenriCartierBresson", "Use Henri Cartier-Bresson as a photographic orientation: decisive moment timing, elegant street composition, natural black-and-white observation, and human spontaneity."),
        new(PhotoStyle.SebastiaoSalgado, "PhotoStyle_SebastiaoSalgado", "Use Sebastiao Salgado as a photographic orientation: monumental black-and-white documentary scale, dense tonal depth, social gravity, and epic human realism."),
        new(PhotoStyle.GregoryCrewdson, "PhotoStyle_GregoryCrewdson", "Use Gregory Crewdson as a photographic orientation: large-scale cinematic staging, eerie suburban atmosphere, elaborate light design, and suspended narrative tension."),
        new(PhotoStyle.DavidLaChapelle, "PhotoStyle_DavidLaChapelle", "Use David LaChapelle as a photographic orientation: hyper-saturated color, surreal glossy excess, pop spectacle, and theatrical commercial fantasy."),
        new(PhotoStyle.IrvingPenn, "PhotoStyle_IrvingPenn", "Use Irving Penn as a photographic orientation: minimal studio discipline, exacting portrait structure, quiet elegance, and refined tonal restraint."),
        new(PhotoStyle.EllenVonUnwerth, "PhotoStyle_EllenVonUnwerth", "Use Ellen von Unwerth as a photographic orientation: playful fashion energy, flirtatious spontaneity, lively motion, and confident sensual glamour."),
        new(PhotoStyle.MarioTestino, "PhotoStyle_MarioTestino", "Use Mario Testino as a photographic orientation: glossy commercial fashion, expensive sunlit polish, clean celebrity styling, and upbeat luxury appeal."),
        new(PhotoStyle.TimWalker, "PhotoStyle_TimWalker", "Use Tim Walker as a photographic orientation: fantastical fashion staging, whimsical scale shifts, storybook surrealism, and dreamlike editorial theater."),
        new(PhotoStyle.PaoloRoversi, "PhotoStyle_PaoloRoversi", "Use Paolo Roversi as a photographic orientation: soft luminous fashion portraiture, airy film atmosphere, muted romance, and delicate temporal fragility."),
        new(PhotoStyle.AndreasGursky, "PhotoStyle_AndreasGursky", "Use Andreas Gursky as a photographic orientation: distant monumental framing, system-like repetition, architectural scale, and cool contemporary order."),
        new(PhotoStyle.CindySherman, "PhotoStyle_CindySherman", "Use Cindy Sherman as a photographic orientation: character-based self-staging, conceptual identity play, controlled artificiality, and psychologically loaded personas."),
        new(PhotoStyle.DaidoMoriyama, "PhotoStyle_DaidoMoriyama", "Use Daido Moriyama as a photographic orientation: rough high-contrast street grain, restless framing, urban grit, and raw nocturnal immediacy."),
        new(PhotoStyle.VivianMaier, "PhotoStyle_VivianMaier", "Use Vivian Maier as a photographic orientation: observant street candor, everyday human detail, direct framing, and quietly intimate city life."),
        new(PhotoStyle.SlimAarons, "PhotoStyle_SlimAarons", "Use Slim Aarons as a photographic orientation: sunlit leisure luxury, resort affluence, pastel lifestyle polish, and effortless elite glamour."),
        new(PhotoStyle.FanHo, "PhotoStyle_FanHo", "Use Fan Ho as a photographic orientation: graphic shafts of light, foggy urban depth, silhouetted figures, and poetic black-and-white geometry.")
    ];

    public static IEnumerable<PhotoStyleDefinition> GetPhotoStyles(PhotoSection section) =>
        section == PhotoSection.All
            ? PhotoStyles.Where(style => style.Style == PhotoStyle.Auto)
            : PhotoStyles.Where(style => style.Style == PhotoStyle.Auto || GetPhotoStyleSection(style.Style) == section);

    private static PhotoSection GetPhotoStyleSection(PhotoStyle style) => style switch
    {
        PhotoStyle.ClassicStudioPortrait or PhotoStyle.CinematicPortrait or PhotoStyle.EditorialPortrait or PhotoStyle.HardFlashPortrait or PhotoStyle.EnvironmentalPortrait or PhotoStyle.MonochromePortrait => PhotoSection.Portrait,
        PhotoStyle.LuxuryEditorialFashion or PhotoStyle.StreetFashion or PhotoStyle.Y2KFashion or PhotoStyle.HighFlashFashion or PhotoStyle.AvantGardeFashion => PhotoSection.Fashion,
        PhotoStyle.CleanBeauty or PhotoStyle.GlossyBeautyAd or PhotoStyle.ColorGelBeauty or PhotoStyle.MacroBeauty or PhotoStyle.PremiumSkincareBeauty => PhotoSection.Beauty,
        PhotoStyle.CleanStudioProduct or PhotoStyle.LuxuryProductPhoto or PhotoStyle.TechProductPhoto or PhotoStyle.DarkPremiumProduct or PhotoStyle.SplashProductAd => PhotoSection.Product,
        PhotoStyle.EditorialFood or PhotoStyle.DarkMoodyFood or PhotoStyle.BrightCommercialFood or PhotoStyle.FineDiningFood or PhotoStyle.OverheadTabletopFood => PhotoSection.Food,
        PhotoStyle.CleanArchitecture or PhotoStyle.LuxuryInterior or PhotoStyle.MinimalModernInterior or PhotoStyle.NightExteriorArchitecture or PhotoStyle.BrutalistArchitecture => PhotoSection.ArchitectureInterior,
        PhotoStyle.DocumentaryReportage or PhotoStyle.StreetCandid or PhotoStyle.FlashStreet or PhotoStyle.GrittyUrban or PhotoStyle.BlackAndWhiteReportage => PhotoSection.StreetReportage,
        PhotoStyle.EpicCinematicLandscape or PhotoStyle.NaturalTravel or PhotoStyle.GoldenHourLandscape or PhotoStyle.MoodyWeatherLandscape or PhotoStyle.AdventureTravel => PhotoSection.LandscapeTravel,
        PhotoStyle.StudioAutomotive or PhotoStyle.NeonNightAutomotive or PhotoStyle.LuxuryAutomotive or PhotoStyle.RollingShotAutomotive or PhotoStyle.OffRoadAutomotive => PhotoSection.Automotive,
        PhotoStyle.ScientificMacro or PhotoStyle.LuxuryDetailMacro or PhotoStyle.NatureMacro or PhotoStyle.JewelryMacro or PhotoStyle.AbstractTextureMacro => PhotoSection.Macro,
        PhotoStyle.FineArtConceptual or PhotoStyle.SurrealConceptual or PhotoStyle.DarkPsychologicalConceptual or PhotoStyle.DreamlikeConceptual or PhotoStyle.FuturisticConceptual => PhotoSection.Conceptual,
        PhotoStyle.PremiumCommercialAd or PhotoStyle.FmcgAdvertisingPhoto or PhotoStyle.TechCampaignPhoto or PhotoStyle.LuxuryAdPhoto or PhotoStyle.BoldBillboardPhoto => PhotoSection.Advertising,
        PhotoStyle.AnnieLeibovitz or PhotoStyle.PeterLindbergh or PhotoStyle.HelmutNewton or PhotoStyle.RichardAvedon or PhotoStyle.SteveMcCurry or PhotoStyle.HenriCartierBresson or PhotoStyle.SebastiaoSalgado or PhotoStyle.GregoryCrewdson or PhotoStyle.DavidLaChapelle or PhotoStyle.IrvingPenn or PhotoStyle.EllenVonUnwerth or PhotoStyle.MarioTestino or PhotoStyle.TimWalker or PhotoStyle.PaoloRoversi or PhotoStyle.AndreasGursky or PhotoStyle.CindySherman or PhotoStyle.DaidoMoriyama or PhotoStyle.VivianMaier or PhotoStyle.SlimAarons or PhotoStyle.FanHo => PhotoSection.Photographers,
        _ => PhotoSection.All
    };

    public static readonly IReadOnlyList<IconPlatformDefinition> IconPlatforms =
    [
        new(IconPlatform.Auto, "IconPlatform_Auto", "Choose the platform treatment that best fits the brief."),
        new(IconPlatform.MacOS, "IconPlatform_MacOS", "macOS app icon: a rounded-square tile, elegant restrained depth or flat treatment, a centered distinctive symbol, clean safe padding, and no tiny detail."),
        new(IconPlatform.IOS, "IconPlatform_IOS", "iOS and iPadOS app icon: a squircle composition, one centered memorable symbol, balanced color fields, generous safe padding, and instant recognition at small size."),
        new(IconPlatform.Windows11, "IconPlatform_Windows11", "Windows 11 app icon: simple modern geometry, a high-contrast recognizable glyph, clean silhouette, optional transparent background when appropriate, and readability from 16 pixels."),
        new(IconPlatform.AndroidMaterialYou, "IconPlatform_AndroidMaterialYou", "Android Material You adaptive icon: separate foreground symbol and background field, bold simple shapes, safe-zone aware composition, and no fragile fine detail."),
        new(IconPlatform.CrossPlatform, "IconPlatform_CrossPlatform", "cross-platform app icon: a square scalable silhouette, generous safe padding, strong contrast, and recognizability from 16 pixels to a store listing."),
    ];

    public static readonly IReadOnlyList<IconStyleDefinition> IconStyles =
    [
        new(IconStyle.Auto, "IconStyle_Auto", "Choose the clearest style for the app purpose and target platform."),
        new(IconStyle.Flat, "IconStyle_Flat", "flat icon design with simple filled geometry and a restrained palette."),
        new(IconStyle.GradientFlat, "IconStyle_GradientFlat", "flat icon design with a subtle controlled gradient and clean geometry."),
        new(IconStyle.Monochrome, "IconStyle_Monochrome", "single-color icon design with maximum silhouette clarity."),
        new(IconStyle.Line, "IconStyle_Line", "precise line icon with consistent stroke weight and strong small-size legibility."),
        new(IconStyle.Glyph, "IconStyle_Glyph", "solid glyph icon with a bold centered silhouette."),
        new(IconStyle.Filled, "IconStyle_Filled", "filled icon with compact simple forms and clear negative space."),
        new(IconStyle.Duotone, "IconStyle_Duotone", "two-tone icon with disciplined color contrast and simple layers."),
        new(IconStyle.Isometric, "IconStyle_Isometric", "clean isometric icon with minimal planes and no clutter."),
        new(IconStyle.Glassmorphism, "IconStyle_Glassmorphism", "restrained glassmorphism icon with readable translucent layers and controlled highlights."),
        new(IconStyle.Neumorphism, "IconStyle_Neumorphism", "subtle neumorphic icon with soft depth while preserving contrast and a clear silhouette."),
        new(IconStyle.ThreeDimensional, "IconStyle_ThreeDimensional", "polished three-dimensional app icon with simple forms and controlled studio-like light."),
        new(IconStyle.ClayThreeDimensional, "IconStyle_ClayThreeDimensional", "friendly clay-style three-dimensional icon with tactile rounded forms."),
        new(IconStyle.PixelArt, "IconStyle_PixelArt", "purposeful pixel-art icon with a limited palette and a crisp grid-aligned silhouette."),
        new(IconStyle.Retro, "IconStyle_Retro", "retro app icon with simple era-appropriate geometry and no imitation of a specific brand."),
        new(IconStyle.HandDrawn, "IconStyle_HandDrawn", "clean hand-drawn icon with deliberate simplified contours."),
        new(IconStyle.Mascot, "IconStyle_Mascot", "friendly mascot icon with one expressive, highly recognizable character silhouette."),
    ];

    public static readonly IReadOnlyList<GraphicTypeDefinition> GraphicTypes =
    [
        new(GraphicType.Auto, "GraphicType_Auto", "Choose the most useful graphic asset type from the brief."),
        new(GraphicType.Poster, "GraphicType_Poster", "a poster or placard composition with a strong focal point, deliberate hierarchy, and purposeful graphic impact."),
        new(GraphicType.AdvertisingLayout, "GraphicType_AdvertisingLayout", "a commercial advertising layout with product or message priority, practical hierarchy, and campaign-ready composition."),
        new(GraphicType.Banner, "GraphicType_Banner", "a wide banner composition with a clear focal area, readable hierarchy, and campaign-ready emphasis."),
        new(GraphicType.Cover, "GraphicType_Cover", "a cover design with a decisive front-facing composition, strong hierarchy, and medium-specific visual identity."),
        new(GraphicType.Sticker, "GraphicType_Sticker", "a single sticker with a clear silhouette, bold contour, and production-ready visual separation."),
        new(GraphicType.StickerPack, "GraphicType_StickerPack", "a cohesive sticker pack with distinct readable stickers, shared palette, and consistent character or contour language."),
        new(GraphicType.VectorIllustration, "GraphicType_VectorIllustration", "an illustration asset with intentional shapes, controlled detail, and a strong editorial or graphic silhouette."),
        new(GraphicType.Infographic, "GraphicType_Infographic", "an infographic layout with a clear visual hierarchy, structured data presentation, and no invented claims."),
        new(GraphicType.UiElement, "GraphicType_UiElement", "a production-ready UI graphic element with clear hierarchy, functional readability, and interface-ready polish."),
        new(GraphicType.Logo, "GraphicType_Logo", "a scalable logo mark with memorable silhouette, disciplined geometry, and no invented lettering unless requested."),
        new(GraphicType.Icon, "GraphicType_Icon", "a square icon asset with one dominant symbol, edge-to-edge background when present, and maximum silhouette clarity."),
    ];

    public static readonly IReadOnlyList<GraphicStyleDefinition> GraphicStyles =
    [
        new(GraphicStyle.Auto, "GraphicStyle_Auto", "Choose the clearest visual style for the requested asset."),
        new(GraphicStyle.SwissStyle, "GraphicStyle_SwissStyle", "Swiss modernist poster design with strict grid logic, typographic hierarchy, restrained geometry, and high-clarity composition."),
        new(GraphicStyle.Bauhaus, "GraphicStyle_Bauhaus", "Bauhaus graphic design with geometric reduction, primary color accents, functional composition, and disciplined modernist balance."),
        new(GraphicStyle.Constructivism, "GraphicStyle_Constructivism", "Constructivist poster design with dynamic diagonals, bold graphic contrast, militant geometry, and agitational energy."),
        new(GraphicStyle.ArtDecoPoster, "GraphicStyle_ArtDecoPoster", "Art Deco poster style with elegant symmetry, streamlined glamour, decorative geometry, and polished period sophistication."),
        new(GraphicStyle.SovietAgitation, "GraphicStyle_SovietAgitation", "Soviet agitation poster style with bold red-black contrast, simplified figures, directive composition, and propagandistic urgency."),
        new(GraphicStyle.WartimePropaganda, "GraphicStyle_WartimePropaganda", "wartime propaganda poster style with persuasive urgency, bold symbolic imagery, high contrast, and mobilizing composition."),
        new(GraphicStyle.Advertising1950s, "GraphicStyle_Advertising1950s", "1950s advertising illustration style with cheerful mid-century polish, optimistic composition, bright period color, and clean commercial clarity."),
        new(GraphicStyle.Psychedelic1960s, "GraphicStyle_Psychedelic1960s", "1960s psychedelic poster style with swirling lettering logic, saturated color vibration, warped contours, and concert-poster energy."),
        new(GraphicStyle.PunkGrunge, "GraphicStyle_PunkGrunge", "punk and grunge poster style with raw collage texture, torn edges, xerox energy, aggressive contrast, and anti-polished attitude."),
        new(GraphicStyle.CinemaPoster, "GraphicStyle_CinemaPoster", "cinema poster style with dramatic focal montage, star-driven hierarchy, atmospheric lighting, and theatrical key art tension."),
        new(GraphicStyle.JapanesePoster, "GraphicStyle_JapanesePoster", "Japanese poster design with precise asymmetry, refined negative space, graphic restraint, and high-impact compositional clarity."),
        new(GraphicStyle.Glossy1980sAd, "GraphicStyle_Glossy1980sAd", "1980s glossy commercial-ad style with polished product glamour, bright highlights, magazine-layout confidence, and aspirational sheen."),
        new(GraphicStyle.LuxuryFashionAd, "GraphicStyle_LuxuryFashionAd", "luxury fashion advertising style with restrained elegance, premium whitespace, editorial sophistication, and glossy brand control."),
        new(GraphicStyle.TechAd, "GraphicStyle_TechAd", "technology advertising style with sleek precision, modern gradients or light accents, product clarity, and clean premium communication."),
        new(GraphicStyle.FmcgCommercial, "GraphicStyle_FmcgCommercial", "fast-moving consumer-goods ad style with bright appetite or utility cues, bold callout hierarchy, and accessible mass-market clarity."),
        new(GraphicStyle.PremiumMinimalAd, "GraphicStyle_PremiumMinimalAd", "premium minimal advertising style with sparse composition, elevated restraint, tactile focus, and luxury-brand simplicity."),
        new(GraphicStyle.BoldCommercial, "GraphicStyle_BoldCommercial", "bold commercial design with oversized hierarchy, decisive contrast, promotional energy, and direct market-facing emphasis."),
        new(GraphicStyle.Y2KPromo, "GraphicStyle_Y2KPromo", "Y2K promotional style with glossy futurist accents, chrome-like highlights, digital-era typography cues, and turn-of-millennium attitude."),
        new(GraphicStyle.CleanCommercialBanner, "GraphicStyle_CleanCommercialBanner", "clean commercial banner style with crisp hierarchy, direct promotional focus, and balanced space for campaign messaging."),
        new(GraphicStyle.TechLandingBanner, "GraphicStyle_TechLandingBanner", "tech landing-page banner style with sleek gradients, product-led focal clarity, modern startup polish, and interface-friendly balance."),
        new(GraphicStyle.SaleBanner, "GraphicStyle_SaleBanner", "sale-banner style with immediate retail emphasis, bold hierarchy, price-led energy, and unmistakable campaign urgency."),
        new(GraphicStyle.PremiumBrandBanner, "GraphicStyle_PremiumBrandBanner", "premium brand banner style with restrained luxury, elegant spacing, controlled palette, and campaign-ready sophistication."),
        new(GraphicStyle.EventPromoBanner, "GraphicStyle_EventPromoBanner", "event-promo banner style with headline-driven hierarchy, energetic focal rhythm, and strong campaign visibility."),
        new(GraphicStyle.GamingBanner, "GraphicStyle_GamingBanner", "gaming banner style with dramatic contrast, high-energy composition, stylized intensity, and promotional splash-screen impact."),
        new(GraphicStyle.ClassicBookCover, "GraphicStyle_ClassicBookCover", "classic book-cover design with literary hierarchy, restrained illustration or symbolism, and timeless shelf presence."),
        new(GraphicStyle.EditorialMagazineCover, "GraphicStyle_EditorialMagazineCover", "editorial magazine-cover style with strong coverline hierarchy, fashion or culture polish, and sharp focal presentation."),
        new(GraphicStyle.RetroAlbumCover, "GraphicStyle_RetroAlbumCover", "retro album-cover style with period graphic identity, bold music-market personality, and collectible sleeve character."),
        new(GraphicStyle.ModernAlbumCover, "GraphicStyle_ModernAlbumCover", "modern album-cover style with strong singular concept, contemporary visual confidence, and streaming-era recognizability."),
        new(GraphicStyle.FilmSeriesCover, "GraphicStyle_FilmSeriesCover", "film or series cover style with cinematic mood, franchise-ready hierarchy, and strong entertainment-market positioning."),
        new(GraphicStyle.TypographicCover, "GraphicStyle_TypographicCover", "typographic cover design with text-led hierarchy, disciplined composition, and concept-driven graphic restraint."),
        new(GraphicStyle.ContourSticker, "GraphicStyle_ContourSticker", "contour-cut sticker style with bold outer shape, punchy silhouette, and clean decal readability."),
        new(GraphicStyle.MemeSticker, "GraphicStyle_MemeSticker", "meme-sticker style with exaggerated reaction energy, simple readable forms, and instant social-media legibility."),
        new(GraphicStyle.GraffitiSticker, "GraphicStyle_GraffitiSticker", "graffiti sticker style with street-art attitude, bold marks, sticker-bomb energy, and rough expressive contour."),
        new(GraphicStyle.VinylCutSticker, "GraphicStyle_VinylCutSticker", "vinyl-cut sticker style with production-friendly silhouette, minimal color separation, and crisp decal practicality."),
        new(GraphicStyle.HolographicSticker, "GraphicStyle_HolographicSticker", "holographic sticker look with iridescent sheen, collectible appeal, and glossy decorative finish."),
        new(GraphicStyle.MessengerStickerPack, "GraphicStyle_MessengerStickerPack", "messenger sticker-pack style with clear emotional readability, repeatable character logic, and app-ready communication."),
        new(GraphicStyle.ChibiStickerPack, "GraphicStyle_ChibiStickerPack", "chibi sticker-pack style with cute exaggerated proportions, expressive faces, and highly readable small-format silhouettes."),
        new(GraphicStyle.ReactionStickerPack, "GraphicStyle_ReactionStickerPack", "reaction sticker-pack style with distinct emotional beats, clear body language, and conversational messaging utility."),
        new(GraphicStyle.BrandStickerPack, "GraphicStyle_BrandStickerPack", "brand sticker-pack style with consistent identity, campaign-friendly cohesion, and repeatable promotional character."),
        new(GraphicStyle.MemeStickerPack, "GraphicStyle_MemeStickerPack", "meme sticker-pack style with internet-native humor, sharp readable poses, and a deliberately punchy expressive set."),
        new(GraphicStyle.FlatVector, "GraphicStyle_FlatVector", "flat vector illustration with simplified shapes, clean color blocks, and a modern editorial or product-friendly finish."),
        new(GraphicStyle.EditorialIllustration, "GraphicStyle_EditorialIllustration", "editorial illustration style with concept-driven metaphor, smart composition, and publication-ready visual clarity."),
        new(GraphicStyle.IsometricIllustration, "GraphicStyle_IsometricIllustration", "isometric illustration style with consistent axonometric perspective, modular forms, and clean technical clarity."),
        new(GraphicStyle.HandDrawnIllustration, "GraphicStyle_HandDrawnIllustration", "hand-drawn illustration style with expressive contour, human touch, and reproducible artisanal line character."),
        new(GraphicStyle.ComicPulpIllustration, "GraphicStyle_ComicPulpIllustration", "comic and pulp illustration style with bold ink logic, dramatic staging, and graphic narrative energy."),
        new(GraphicStyle.CollageIllustration, "GraphicStyle_CollageIllustration", "collage illustration style with cut-paper layering, assembled textures, and contemporary editorial experimentation."),
        new(GraphicStyle.MemphisIllustration, "GraphicStyle_MemphisIllustration", "Memphis-inspired illustration style with playful geometry, bright pattern accents, and postmodern decorative rhythm."),
        new(GraphicStyle.PixelArtIllustration, "GraphicStyle_PixelArtIllustration", "pixel-art illustration style with disciplined grid logic, limited palette, and crisp retro digital readability."),
        new(GraphicStyle.CorporateInfographic, "GraphicStyle_CorporateInfographic", "corporate infographic style with sober hierarchy, clear segmentation, and presentation-ready professional structure."),
        new(GraphicStyle.EditorialInfographic, "GraphicStyle_EditorialInfographic", "editorial infographic style with magazine-like pacing, visual storytelling, and refined information hierarchy."),
        new(GraphicStyle.TechDashboardInfographic, "GraphicStyle_TechDashboardInfographic", "tech dashboard infographic style with system clarity, modular panels, precise metrics framing, and interface-oriented structure."),
        new(GraphicStyle.EducationalInfographic, "GraphicStyle_EducationalInfographic", "educational infographic style with stepwise clarity, friendly explanation cues, and accessible information flow."),
        new(GraphicStyle.DataPosterInfographic, "GraphicStyle_DataPosterInfographic", "data-poster infographic style with large-scale hierarchy, chart-led composition, and exhibition-style analytical presence."),
        new(GraphicStyle.FlatUi, "GraphicStyle_FlatUi", "flat UI graphic style with disciplined interface geometry, minimal depth, and crisp functional clarity."),
        new(GraphicStyle.GlassmorphismUi, "GraphicStyle_GlassmorphismUi", "glassmorphism UI style with translucent layers, controlled blur cues, and interface-friendly depth."),
        new(GraphicStyle.NeumorphismUi, "GraphicStyle_NeumorphismUi", "neumorphism UI style with soft extruded depth, subtle highlights, and tactile interface surfaces."),
        new(GraphicStyle.MaterialUi, "GraphicStyle_MaterialUi", "material-inspired UI style with clear elevation logic, structured color roles, and practical component clarity."),
        new(GraphicStyle.GamingUi, "GraphicStyle_GamingUi", "gaming UI style with stylized intensity, high-contrast readability, and HUD-like interface energy."),
        new(GraphicStyle.SaasMinimalUi, "GraphicStyle_SaasMinimalUi", "SaaS minimal UI style with restrained polish, dashboard clarity, and product-led interface simplicity."),
        new(GraphicStyle.MinimalLogo, "GraphicStyle_MinimalLogo", "minimal logo style with reduced geometry, essential silhouette, and maximum brand clarity."),
        new(GraphicStyle.GeometricLogo, "GraphicStyle_GeometricLogo", "geometric logo style with disciplined shape logic, balanced proportions, and clean scalable construction."),
        new(GraphicStyle.EmblemLogo, "GraphicStyle_EmblemLogo", "emblem logo style with enclosed mark structure, heritage cues, and badge-like identity presence."),
        new(GraphicStyle.WordmarkLogo, "GraphicStyle_WordmarkLogo", "wordmark logo style with typography-led identity, disciplined lettering structure, and brand-first clarity."),
        new(GraphicStyle.MascotLogo, "GraphicStyle_MascotLogo", "mascot logo style with one memorable character-driven mark and strong commercial recognizability."),
        new(GraphicStyle.RetroLogo, "GraphicStyle_RetroLogo", "retro logo style with period-aware graphic flavor, nostalgic identity cues, and original brand-safe construction."),
    ];

    public static IEnumerable<GraphicStyleDefinition> GetGraphicStyles(GraphicType type)
    {
        if (type == GraphicType.Auto)
        {
            return GraphicStyles.Where(item => item.Style == GraphicStyle.Auto);
        }

        return GraphicStyles.Where(item => item.Style == GraphicStyle.Auto || GetGraphicStyleType(item.Style) == type);
    }

    private static GraphicType GetGraphicStyleType(GraphicStyle style) => style switch
    {
        GraphicStyle.SwissStyle or GraphicStyle.Bauhaus or GraphicStyle.Constructivism or GraphicStyle.ArtDecoPoster or GraphicStyle.SovietAgitation or GraphicStyle.WartimePropaganda or GraphicStyle.Advertising1950s or GraphicStyle.Psychedelic1960s or GraphicStyle.PunkGrunge or GraphicStyle.CinemaPoster or GraphicStyle.JapanesePoster => GraphicType.Poster,
        GraphicStyle.Glossy1980sAd or GraphicStyle.LuxuryFashionAd or GraphicStyle.TechAd or GraphicStyle.FmcgCommercial or GraphicStyle.PremiumMinimalAd or GraphicStyle.BoldCommercial or GraphicStyle.Y2KPromo => GraphicType.AdvertisingLayout,
        GraphicStyle.CleanCommercialBanner or GraphicStyle.TechLandingBanner or GraphicStyle.SaleBanner or GraphicStyle.PremiumBrandBanner or GraphicStyle.EventPromoBanner or GraphicStyle.GamingBanner => GraphicType.Banner,
        GraphicStyle.ClassicBookCover or GraphicStyle.EditorialMagazineCover or GraphicStyle.RetroAlbumCover or GraphicStyle.ModernAlbumCover or GraphicStyle.FilmSeriesCover or GraphicStyle.TypographicCover => GraphicType.Cover,
        GraphicStyle.ContourSticker or GraphicStyle.MemeSticker or GraphicStyle.GraffitiSticker or GraphicStyle.VinylCutSticker or GraphicStyle.HolographicSticker => GraphicType.Sticker,
        GraphicStyle.MessengerStickerPack or GraphicStyle.ChibiStickerPack or GraphicStyle.ReactionStickerPack or GraphicStyle.BrandStickerPack or GraphicStyle.MemeStickerPack => GraphicType.StickerPack,
        GraphicStyle.FlatVector or GraphicStyle.EditorialIllustration or GraphicStyle.IsometricIllustration or GraphicStyle.HandDrawnIllustration or GraphicStyle.ComicPulpIllustration or GraphicStyle.CollageIllustration or GraphicStyle.MemphisIllustration or GraphicStyle.PixelArtIllustration => GraphicType.VectorIllustration,
        GraphicStyle.CorporateInfographic or GraphicStyle.EditorialInfographic or GraphicStyle.TechDashboardInfographic or GraphicStyle.EducationalInfographic or GraphicStyle.DataPosterInfographic => GraphicType.Infographic,
        GraphicStyle.FlatUi or GraphicStyle.GlassmorphismUi or GraphicStyle.NeumorphismUi or GraphicStyle.MaterialUi or GraphicStyle.GamingUi or GraphicStyle.SaasMinimalUi => GraphicType.UiElement,
        GraphicStyle.MinimalLogo or GraphicStyle.GeometricLogo or GraphicStyle.EmblemLogo or GraphicStyle.WordmarkLogo or GraphicStyle.MascotLogo or GraphicStyle.RetroLogo => GraphicType.Logo,
        _ => GraphicType.Auto
    };

    public static readonly IReadOnlyList<ThemeSectionDefinition> ThemeSections =
    [
        new(ThemeSection.All, "ThemeSection_All", "any scene-driven genre or thematic world"),
        new(ThemeSection.Horror, "ThemeSection_Horror", "horror scenes and genre references"),
        new(ThemeSection.Space, "ThemeSection_Space", "space exploration, cosmic worlds, and interstellar scenes"),
        new(ThemeSection.War, "ThemeSection_War", "war, military conflict, survival, and battlefield aftermath scenes"),
        new(ThemeSection.FairyTales, "ThemeSection_FairyTales", "fairy-tale, folklore, and myth-inspired worlds"),
        new(ThemeSection.SciFi, "ThemeSection_SciFi", "science-fiction cities, robots, laboratories, portals, and speculative future worlds"),
        new(ThemeSection.Sports, "ThemeSection_Sports", "sports, competition, training, and athletic-event scenes"),
        new(ThemeSection.Professions, "ThemeSection_Professions", "profession-driven scenes with clear workplace action, tools, and role-specific pressure")
    ];

    public static readonly IReadOnlyList<ThemeStyleDefinition> ThemeStyles =
    [
        new(ThemeStyle.Auto, "ThemeStyle_Auto", "Select the most fitting thematic scene treatment for the brief."),
        new(ThemeStyle.JapaneseHorror, "ThemeStyle_JapaneseHorror", "Japanese horror atmosphere: fragile silence, uncanny domestic detail, cold dread, and restrained supernatural tension."),
        new(ThemeStyle.LovecraftianHorror, "ThemeStyle_LovecraftianHorror", "Lovecraftian cosmic horror: forbidden scale, ancient unknown presence, fragile human perspective, and mind-bending unease."),
        new(ThemeStyle.GothicOccult, "ThemeStyle_GothicOccult", "gothic occult horror: candlelit darkness, ritual motifs, decayed grandeur, and aristocratic supernatural menace."),
        new(ThemeStyle.CursedHouse, "ThemeStyle_CursedHouse", "cursed-house horror: familiar domestic space turned hostile, lingering presence, wrong silence, and creeping dread in every room."),
        new(ThemeStyle.AbandonedHospital, "ThemeStyle_AbandonedHospital", "abandoned-hospital horror: cold corridors, failing fluorescent light, medical remnants, and an oppressive sense that something still moves inside."),
        new(ThemeStyle.ForestNightmare, "ThemeStyle_ForestNightmare", "forest nightmare: disorienting trees, unseen watchers, ritual traces, wet darkness, and primal fear away from civilization."),
        new(ThemeStyle.OccultRitual, "ThemeStyle_OccultRitual", "occult ritual scene: forbidden symbols, ceremonial arrangement, charged stillness, and imminent supernatural consequence."),
        new(ThemeStyle.SpaceStation, "ThemeStyle_SpaceStation", "space-station scene: pressurized corridors, modular engineering, artificial light, orbital isolation, and mission-critical atmosphere."),
        new(ThemeStyle.DerelictSpaceship, "ThemeStyle_DerelictSpaceship", "derelict-spaceship scene: abandoned decks, damaged systems, drifting debris, emergency shadows, and the tension of entering something lost."),
        new(ThemeStyle.PlanetaryColony, "ThemeStyle_PlanetaryColony", "planetary colony scene: frontier survival, modular habitats, harsh alien climate, practical infrastructure, and fragile human persistence."),
        new(ThemeStyle.AsteroidMine, "ThemeStyle_AsteroidMine", "asteroid-mine scene: industrial excavation, low-gravity machinery, dust, exposed metal, and dangerous resource extraction at the edge of space."),
        new(ThemeStyle.FirstContact, "ThemeStyle_FirstContact", "first-contact scene: cautious encounter, scientific curiosity, communication uncertainty, and the tension of meeting non-human intelligence."),
        new(ThemeStyle.OrbitalLaboratory, "ThemeStyle_OrbitalLaboratory", "orbital-laboratory scene: sterile research modules, technical instrumentation, controlled experiment space, and high-risk scientific containment."),
        new(ThemeStyle.NuclearRuinedCity, "ThemeStyle_NuclearRuinedCity", "post-nuclear ruined city: ash, broken concrete, toxic weather, hollow urban scale, and survival among the remains of civilization."),
        new(ThemeStyle.UrbanCombatZone, "ThemeStyle_UrbanCombatZone", "urban combat zone: damaged streets, barricades, smoke, shattered facades, and close-quarters military danger in a city environment."),
        new(ThemeStyle.BunkerCommand, "ThemeStyle_BunkerCommand", "military bunker command scene: reinforced underground space, maps and monitors, emergency planning, and pressure under imminent threat."),
        new(ThemeStyle.BattlefieldAftermath, "ThemeStyle_BattlefieldAftermath", "battlefield aftermath: silence after violence, wreckage, smoke, wounded terrain, and the weight of what has just happened."),
        new(ThemeStyle.EvacuationUnderFire, "ThemeStyle_EvacuationUnderFire", "evacuation under fire: urgent movement, fear, protective action, collapsing safety, and human survival under active attack."),
        new(ThemeStyle.ReconInRuins, "ThemeStyle_ReconInRuins", "recon-in-ruins scene: cautious movement, tactical observation, broken structures, concealment, and the tension of unseen enemy presence."),
        new(ThemeStyle.EnchantedForest, "ThemeStyle_EnchantedForest", "enchanted-forest scene: living trees, magical pathways, old folklore atmosphere, and a sense that the landscape itself is conscious."),
        new(ThemeStyle.WitchHut, "ThemeStyle_WitchHut", "witch-hut scene: hidden woodland dwelling, strange tools, herbal ritual detail, and intimate magical unease."),
        new(ThemeStyle.RoyalCastle, "ThemeStyle_RoyalCastle", "royal-castle scene: halls of power, courtly grandeur, old stone, banners, and a fairy-tale sense of rule, intrigue, and ceremony."),
        new(ThemeStyle.UnderwaterKingdom, "ThemeStyle_UnderwaterKingdom", "underwater-kingdom scene: submerged palatial architecture, aquatic light, drifting currents, and mythic marine wonder."),
        new(ThemeStyle.VillageAtForestEdge, "ThemeStyle_VillageAtForestEdge", "village-at-forest-edge scene: folklore settlement, timber homes, communal life, nearby wilderness, and quiet anticipation of magic or danger."),
        new(ThemeStyle.SpiritLake, "ThemeStyle_SpiritLake", "spirit-lake scene: reflective sacred water, mist, folklore presence, and a threshold feeling between the human world and the supernatural."),
        new(ThemeStyle.CyberpunkMegacity, "ThemeStyle_CyberpunkMegacity", "cyberpunk megacity: vertical density, neon infrastructure, surveillance, social stratification, and relentless technological urban pressure."),
        new(ThemeStyle.RobotJunkyard, "ThemeStyle_RobotJunkyard", "robot junkyard: piles of broken machines, scavenged parts, abandoned artificial bodies, and industrial decay with mechanical memory."),
        new(ThemeStyle.AndroidFactory, "ThemeStyle_AndroidFactory", "android-factory scene: assembly lines, synthetic bodies, precision engineering, controlled repetition, and uneasy mass production of intelligence."),
        new(ThemeStyle.UndergroundTechCity, "ThemeStyle_UndergroundTechCity", "underground tech city: hidden infrastructure, enclosed futuristic districts, layered transit, artificial climate, and secretive advanced civilization."),
        new(ThemeStyle.PostApocalypticWasteland, "ThemeStyle_PostApocalypticWasteland", "post-apocalyptic sci-fi wasteland: ruined networks, improvised survival technology, hostile emptiness, and a future built from collapse."),
        new(ThemeStyle.PortalAnomaly, "ThemeStyle_PortalAnomaly", "portal-anomaly scene: unstable dimensional rupture, distorted physics, scientific alarm, and the pull of another reality breaking through."),
        new(ThemeStyle.StadiumFinal, "ThemeStyle_StadiumFinal", "stadium final: peak competitive pressure, crowd scale, broadcast drama, and decisive championship intensity."),
        new(ThemeStyle.TrainingMontage, "ThemeStyle_TrainingMontage", "training-montage scene: repetitive discipline, sweat, focused preparation, physical progression, and the build-up before a decisive event."),
        new(ThemeStyle.BoxingRing, "ThemeStyle_BoxingRing", "boxing-ring scene: direct confrontation, ring tension, impact, endurance, and the intimate brutality of combat sport."),
        new(ThemeStyle.StreetBasketball, "ThemeStyle_StreetBasketball", "street-basketball scene: asphalt court rhythm, local competition, improvisation, and raw urban athletic style."),
        new(ThemeStyle.PitLane, "ThemeStyle_PitLane", "motorsport pit-lane scene: speed engineering, team choreography, machine precision, and race-day urgency."),
        new(ThemeStyle.ExtremeOutdoor, "ThemeStyle_ExtremeOutdoor", "extreme outdoor sport: exposed risk, raw landscape, high-adrenaline movement, and physical endurance."),
        new(ThemeStyle.SurgeonOperation, "ThemeStyle_SurgeonOperation", "surgeon-in-operation scene: sterile precision, surgical lighting, coordinated teamwork, focused hands, and life-or-death concentration."),
        new(ThemeStyle.FirefighterRescue, "ThemeStyle_FirefighterRescue", "firefighter-rescue scene: smoke, urgency, heavy protective gear, active hazard, and decisive lifesaving action."),
        new(ThemeStyle.DetectiveCrimeScene, "ThemeStyle_DetectiveCrimeScene", "detective-at-crime-scene scene: forensic detail, guarded perimeter, investigative focus, and the tension of assembling hidden truth."),
        new(ThemeStyle.ScientistLaboratory, "ThemeStyle_ScientistLaboratory", "scientist-in-laboratory scene: experimental setup, analytical observation, technical instruments, and concentrated research intensity."),
        new(ThemeStyle.PilotCockpit, "ThemeStyle_PilotCockpit", "pilot-in-cockpit scene: instrument glow, flight-control focus, enclosed high-responsibility space, and split-second navigational decisions."),
        new(ThemeStyle.MinerUnderground, "ThemeStyle_MinerUnderground", "miner-underground scene: confined industrial depth, heavy equipment, dust, pressure, and physically demanding work below the surface.")
    ];

    public static IEnumerable<ThemeStyleDefinition> GetThemeStyles(ThemeSection section) =>
        ThemeStyles.Where(style => style.Style == ThemeStyle.Auto || section == ThemeSection.All || GetThemeSection(style.Style) == section);

    private static ThemeSection GetThemeSection(ThemeStyle style) => style switch
    {
        ThemeStyle.JapaneseHorror or ThemeStyle.LovecraftianHorror or ThemeStyle.GothicOccult or ThemeStyle.CursedHouse or ThemeStyle.AbandonedHospital or ThemeStyle.ForestNightmare or ThemeStyle.OccultRitual => ThemeSection.Horror,
        ThemeStyle.SpaceStation or ThemeStyle.DerelictSpaceship or ThemeStyle.PlanetaryColony or ThemeStyle.AsteroidMine or ThemeStyle.FirstContact or ThemeStyle.OrbitalLaboratory => ThemeSection.Space,
        ThemeStyle.NuclearRuinedCity or ThemeStyle.UrbanCombatZone or ThemeStyle.BunkerCommand or ThemeStyle.BattlefieldAftermath or ThemeStyle.EvacuationUnderFire or ThemeStyle.ReconInRuins => ThemeSection.War,
        ThemeStyle.EnchantedForest or ThemeStyle.WitchHut or ThemeStyle.RoyalCastle or ThemeStyle.UnderwaterKingdom or ThemeStyle.VillageAtForestEdge or ThemeStyle.SpiritLake => ThemeSection.FairyTales,
        ThemeStyle.CyberpunkMegacity or ThemeStyle.RobotJunkyard or ThemeStyle.AndroidFactory or ThemeStyle.UndergroundTechCity or ThemeStyle.PostApocalypticWasteland or ThemeStyle.PortalAnomaly => ThemeSection.SciFi,
        ThemeStyle.StadiumFinal or ThemeStyle.TrainingMontage or ThemeStyle.BoxingRing or ThemeStyle.StreetBasketball or ThemeStyle.PitLane or ThemeStyle.ExtremeOutdoor => ThemeSection.Sports,
        ThemeStyle.SurgeonOperation or ThemeStyle.FirefighterRescue or ThemeStyle.DetectiveCrimeScene or ThemeStyle.ScientistLaboratory or ThemeStyle.PilotCockpit or ThemeStyle.MinerUnderground => ThemeSection.Professions,
        _ => ThemeSection.All
    };

    public static readonly IReadOnlyList<TextPromptTypeDefinition> TextPromptTypes =
    [
        new(TextPromptType.Auto, "TextType_Auto", "Infer the most useful format from the brief."),
        new(TextPromptType.AnalyticalArticle, "TextType_AnalyticalArticle", "Analytical article with a clear thesis, evidence-based reasoning, balanced counterpoints, and a practical conclusion."),
        new(TextPromptType.BusinessEmail, "TextType_BusinessEmail", "Concise business email with a clear subject, context, request or decision, and an actionable next step."),
        new(TextPromptType.BlogPost, "TextType_BlogPost", "Engaging blog post with a strong opening, logical sections, useful examples, and a memorable conclusion."),
        new(TextPromptType.Instruction, "TextType_Instruction", "Step-by-step instruction with prerequisites, ordered actions, expected outcomes, and concise warnings where needed."),
        new(TextPromptType.ProductCard, "TextType_ProductCard", "Product card focused on customer benefits, accurate characteristics, practical use cases, and a concise call to action."),
        new(TextPromptType.CommercialProposal, "TextType_CommercialProposal", "Commercial proposal that frames the client's problem, value, scope, proof, terms, and next action."),
        new(TextPromptType.LandingPage, "TextType_LandingPage", "Landing page copy with a clear value proposition, benefit hierarchy, proof points, objection handling, and calls to action."),
        new(TextPromptType.PressRelease, "TextType_PressRelease", "Press release with a newsworthy lead, verified facts, quotable context, and a concise boilerplate."),
        new(TextPromptType.EducationalText, "TextType_EducationalText", "Educational explanation that builds from simple concepts to application with intuitive examples."),
        new(TextPromptType.SocialPost, "TextType_SocialPost", "Social media post with a strong hook, short readable paragraphs, a concrete takeaway, and a fitting call to action."),
        new(TextPromptType.SalesCopy, "TextType_SalesCopy", "Persuasive sales copy focused on the audience's problem, outcome, proof, and a specific call to action."),
        new(TextPromptType.Editing, "TextType_Editing", "Careful editorial rewrite that preserves meaning while improving clarity, structure, grammar, and tone."),
        new(TextPromptType.Summary, "TextType_Summary", "Faithful concise summary that retains decisions, facts, constraints, and next actions."),
        new(TextPromptType.Script, "TextType_Script", "Script with scene or beat structure, natural dialogue when appropriate, pacing, and clear transitions."),
        new(TextPromptType.SeoArticle, "TextType_SeoArticle", "Useful SEO article with search intent alignment, natural semantic coverage, clear headings, and no keyword stuffing."),
        new(TextPromptType.TechnicalDocumentation, "TextType_TechnicalDocumentation", "Precise technical documentation with scope, prerequisites, ordered procedures, examples, expected results, and troubleshooting."),
        new(TextPromptType.CreativeWriting, "TextType_CreativeWriting", "Polished creative writing with a coherent voice, vivid imagery, purposeful pacing, and emotional resonance.")
    ];

    public static readonly IReadOnlyList<TextPromptToneDefinition> TextPromptTones =
    [
        new(TextPromptTone.Neutral, "TextTone_Neutral", "Use a clear, neutral, and direct tone."),
        new(TextPromptTone.Expert, "TextTone_Expert", "Use a confident expert tone: precise, useful, and never needlessly jargon-heavy."),
        new(TextPromptTone.Friendly, "TextTone_Friendly", "Use a warm, helpful, conversational tone."),
        new(TextPromptTone.Premium, "TextTone_Premium", "Use a refined premium tone: restrained, elegant, and confident."),
        new(TextPromptTone.Bold, "TextTone_Bold", "Use an energetic, bold, decisive tone without hype or empty claims."),
        new(TextPromptTone.Formal, "TextTone_Formal", "Use a formal, professional, and respectful tone.")
    ];

    public static readonly IReadOnlyList<AnalysisDirectionDefinition> AnalysisDirections =
    [
        new(AnalysisDirection.Auto, "AnalysisDirection_Auto", "AnalysisDirection_AutoOutcome", "Choose the single most useful output contract for the brief and state that structure explicitly in the finished prompt."),
        new(AnalysisDirection.Comparison, "AnalysisDirection_Comparison", "AnalysisDirection_ComparisonOutcome", "Create a comparison prompt. Require exactly: the comparison scope and assumptions; a table with common criteria and equivalent evidence for every option; trade-offs and risks; a concise conclusion that names the best option only when the evidence supports it."),
        new(AnalysisDirection.Recommendation, "AnalysisDirection_Recommendation", "AnalysisDirection_RecommendationOutcome", "Create a decision prompt. Require exactly: decision context and constraints; considered options; evaluation criteria; trade-offs and risks; a clear recommendation with reasoning; and conditions that would change the recommendation."),
        new(AnalysisDirection.MarketCompetition, "AnalysisDirection_MarketCompetition", "AnalysisDirection_MarketCompetitionOutcome", "Create a market-and-competition research prompt. Require exactly: market scope; audience and needs; competitor comparison; positioning and differentiation; opportunities and risks; and prioritized practical next steps. Clearly separate supplied facts from research that still requires validation."),
        new(AnalysisDirection.ResearchFactCheck, "AnalysisDirection_ResearchFactCheck", "AnalysisDirection_ResearchFactCheckOutcome", "Create a research and fact-checking prompt. Require exactly: questions and scope; confirmed claims with sources; unverified or disputed claims; missing evidence; source-quality limitations; and a conclusion limited to what the evidence supports."),
        new(AnalysisDirection.RiskAssessment, "AnalysisDirection_RiskAssessment", "AnalysisDirection_RiskAssessmentOutcome", "Create a risk-assessment prompt. Require exactly: scope and assumptions; a risk register with likelihood and impact; early warning signs; mitigations and owners when known; residual risks; and decision thresholds or next actions."),
        new(AnalysisDirection.Strategy, "AnalysisDirection_Strategy", "AnalysisDirection_StrategyOutcome", "Create a strategy prompt. Require exactly: objective and success metrics; current constraints; strategic choices; prioritized initiatives; sequencing and dependencies; risks; and review checkpoints. Do not present wishes as a strategy without trade-offs."),
        new(AnalysisDirection.ProductAnalysis, "AnalysisDirection_ProductAnalysis", "AnalysisDirection_ProductAnalysisOutcome", "Create a product-analysis prompt. Require exactly: user problem and segment; current value proposition; journey or friction points; alternatives; evidence and assumptions; prioritized improvements; success metrics; and risks or open questions."),
        new(AnalysisDirection.DataAnalysis, "AnalysisDirection_DataAnalysis", "AnalysisDirection_DataAnalysisOutcome", "Create a data-analysis prompt. Require exactly: data scope and quality checks; method and assumptions; key patterns and anomalies; limitations; conclusions traceable to the supplied data; and recommended follow-up analyses. Never infer causal claims without supporting evidence."),
        new(AnalysisDirection.ScenarioPlanning, "AnalysisDirection_ScenarioPlanning", "AnalysisDirection_ScenarioPlanningOutcome", "Create a scenario-planning prompt. Require exactly: driving uncertainties; 3 to 4 plausible distinct scenarios; signals to monitor; implications and risks for each scenario; robust actions; and a review cadence. Do not treat scenarios as forecasts."),
        new(AnalysisDirection.RootCause, "AnalysisDirection_RootCause", "AnalysisDirection_RootCauseOutcome", "Create a root-cause-analysis prompt. Require exactly: problem symptoms and impact; evidence; causal hypotheses; validation steps; most likely root causes with confidence; corrective actions; and prevention or monitoring measures. Keep symptoms separate from causes."),
        new(AnalysisDirection.SolutionOptions, "AnalysisDirection_SolutionOptions", "AnalysisDirection_SolutionOptionsOutcome", "Create a solution-options prompt. Require exactly: problem and constraints; several genuinely distinct options; for each option, benefits, limitations, complexity, risks, and suitable conditions; comparison criteria; and a recommended shortlist. Do not produce cosmetic variations of one idea." )
    ];

    public static readonly IReadOnlyList<VideoDirectionDefinition> VideoDirections =
    [
        new(VideoDirection.Auto, "VideoDirection_Auto", "Infer the most effective video direction from the brief."),
        new(VideoDirection.Advertising, "VideoDirection_Advertising", "Premium advertising spot with a clear visual idea, controlled camera movement, product or message emphasis, and a decisive final frame."),
        new(VideoDirection.CinematicScene, "VideoDirection_CinematicScene", "Cinematic narrative shot with motivated camera movement, atmospheric lighting, visual tension, and coherent screen direction."),
        new(VideoDirection.ProductVideo, "VideoDirection_ProductVideo", "Premium product film with precise product choreography, clean reflections, tactile material detail, and controlled studio or contextual lighting."),
        new(VideoDirection.FashionBeauty, "VideoDirection_FashionBeauty", "Fashion and beauty film with elegant posing, refined grooming detail, deliberate movement, flattering light, and editorial camera work."),
        new(VideoDirection.Documentary, "VideoDirection_Documentary", "Observational documentary footage with natural behavior, available or motivated light, restrained handheld or static camera work, and authentic context."),
        new(VideoDirection.SocialVertical, "VideoDirection_SocialVertical", "Short vertical social video with an immediate visual hook, readable subject framing, energetic but controlled pacing, and a clear final beat."),
        new(VideoDirection.MusicVideo, "VideoDirection_MusicVideo", "Stylized music-video sequence with rhythm-aware movement, expressive performance or visual motif, and purposeful camera choreography."),
        new(VideoDirection.Interview, "VideoDirection_Interview", "Professional interview or talking-head shot with stable eye-line, natural gestures, soft key light, clean sound-stage composition, and restrained camera motion."),
        new(VideoDirection.Explainer, "VideoDirection_Explainer", "Clear explanatory video with one understandable visual action at a time, logical progression, legible framing, and unobtrusive camera movement."),
        new(VideoDirection.ArchitectureInterior, "VideoDirection_ArchitectureInterior", "Architecture and interior film with smooth stabilized camera movement, accurate verticals, measured pacing, natural light behavior, and spatial continuity."),
        new(VideoDirection.TravelLandscape, "VideoDirection_TravelLandscape", "Travel or landscape film with expansive environmental depth, atmospheric movement, controlled reveal, and a patient cinematic pace."),
        new(VideoDirection.Action, "VideoDirection_Action", "Dynamic action sequence with clear subject readability, motivated energetic camera movement, physical continuity, and no chaotic unrelated motion."),
        new(VideoDirection.Timelapse, "VideoDirection_Timelapse", "Time-lapse sequence showing a clearly defined gradual process, stable composition, smooth temporal acceleration, and recognizable environmental change."),
        new(VideoDirection.Macro, "VideoDirection_Macro", "Macro film with precise close focus, subtle controlled movement, tactile surface detail, shallow depth of field, and carefully shaped light."),
        new(VideoDirection.StopMotion, "VideoDirection_StopMotion", "Handcrafted stop-motion sequence with tactile miniature materials, intentional incremental motion, practical lighting, and consistent set continuity."),
        new(VideoDirection.ProductAnimation3D, "VideoDirection_ProductAnimation3D", "Polished 3D product animation with physically plausible materials, precise motion design, clean camera paths, and controlled lighting."),
        new(VideoDirection.LoopAnimation, "VideoDirection_LoopAnimation", "Seamless short looping animation with a clear repeating action, matching first and last frames, stable composition, and no visible jump." )
    ];

    public static readonly IReadOnlyList<ProgrammingTaskTypeDefinition> ProgrammingTaskTypes =
    [
        new(ProgrammingTaskType.Auto, "ProgrammingTask_Auto", "Infer the most useful software-development task type from the brief."),
        new(ProgrammingTaskType.NewFeature, "ProgrammingTask_NewFeature", "Define the feature scope, user flows, data handling, edge cases, non-functional requirements, deliverables, and acceptance criteria."),
        new(ProgrammingTaskType.BugFix, "ProgrammingTask_BugFix", "Require reproducible steps, observed and expected behavior, root-cause analysis grounded in the code, a minimal safe fix, regression coverage, and verification steps."),
        new(ProgrammingTaskType.Refactoring, "ProgrammingTask_Refactoring", "Require a behavior-preserving refactoring plan, code smells or duplication supported by evidence, incremental changes, compatibility constraints, and regression tests."),
        new(ProgrammingTaskType.CodeReview, "ProgrammingTask_CodeReview", "Request concrete evidence-based findings ordered by severity, with file and location, impact, explanation, and a practical correction; do not invent speculative issues."),
        new(ProgrammingTaskType.Architecture, "ProgrammingTask_Architecture", "Request viable architecture options, constraints, trade-offs, boundaries, data flow, failure modes, security considerations, rollout plan, and a justified recommendation."),
        new(ProgrammingTaskType.Testing, "ProgrammingTask_Testing", "Specify the test target, behavior to prove, critical paths, edge cases, failure conditions, fixture strategy, and the most appropriate test levels."),
        new(ProgrammingTaskType.Performance, "ProgrammingTask_Performance", "Require a measurable performance goal, baseline, likely bottlenecks, profiling or measurement method, safe optimizations, trade-offs, and post-change verification."),
        new(ProgrammingTaskType.Security, "ProgrammingTask_Security", "Identify the threat surface, trust boundaries, evidence-based vulnerabilities, severity, remediation, secure defaults, and verification without weakening protections."),
        new(ProgrammingTaskType.ApiIntegration, "ProgrammingTask_ApiIntegration", "Specify contracts, authentication, request and response handling, retries, timeouts, error mapping, idempotency where relevant, observability, and integration tests."),
        new(ProgrammingTaskType.Database, "ProgrammingTask_Database", "Specify data model, integrity constraints, query patterns, migrations, transactions, indexing, rollback, compatibility, and data-validation strategy."),
        new(ProgrammingTaskType.UiUx, "ProgrammingTask_UiUx", "Specify user goals, interaction states, accessibility, responsive behavior, loading and error states, visual consistency, and acceptance criteria."),
        new(ProgrammingTaskType.DevOpsDeployment, "ProgrammingTask_DevOpsDeployment", "Specify build and release flow, environments, configuration and secrets, deployment steps, rollback, monitoring, alerts, and verification."),
        new(ProgrammingTaskType.Documentation, "ProgrammingTask_Documentation", "Specify audience, scope, prerequisites, accurate examples, procedures, expected outcomes, limitations, and maintenance ownership."),
        new(ProgrammingTaskType.ExistingCodeAnalysis, "ProgrammingTask_ExistingCodeAnalysis", "Require inspection of the supplied code before conclusions, trace claims to concrete files or constructs, identify dependencies and risks, and separate observations from recommendations."),
        new(ProgrammingTaskType.MigrationDependencies, "ProgrammingTask_MigrationDependencies", "Specify current and target versions, compatibility risks, breaking changes, staged migration, rollback, dependency locks, and validation tests." )
    ];

    public static readonly IReadOnlyList<ProgrammingProjectTypeDefinition> ProgrammingProjectTypes =
    [
        new(ProgrammingProjectType.Auto, "ProgrammingProjectType_Auto", "Infer the most useful software product type from the brief."),
        new(ProgrammingProjectType.Website, "ProgrammingProjectType_Website", "Create a general website or multi-page web presence with clear structure, navigation, sections, and implementation priorities."),
        new(ProgrammingProjectType.LandingPage, "ProgrammingProjectType_LandingPage", "Create a conversion-focused landing page with a clear hero section, value proposition, trust blocks, calls to action, and responsive structure."),
        new(ProgrammingProjectType.OnlineStore, "ProgrammingProjectType_OnlineStore", "Create an online store with catalog flow, product pages, cart and checkout logic, filters, trust signals, and mobile-friendly commerce behavior."),
        new(ProgrammingProjectType.Dashboard, "ProgrammingProjectType_Dashboard", "Create a dashboard with data blocks, filters, charts or tables, state handling, role-appropriate hierarchy, and practical interaction flow."),
        new(ProgrammingProjectType.WebApp, "ProgrammingProjectType_WebApp", "Create an interactive web application with screens, user flows, state changes, validation, empty states, and implementation structure."),
        new(ProgrammingProjectType.HtmlGame, "ProgrammingProjectType_HtmlGame", "Create a self-contained HTML browser game with gameplay loop, controls, score or win condition, visual feedback, and lightweight implementation structure."),
        new(ProgrammingProjectType.MiniUtility, "ProgrammingProjectType_MiniUtility", "Create a small practical utility with one clear job, concise interface or CLI flow, validation, edge handling, and straightforward implementation steps."),
        new(ProgrammingProjectType.TelegramBot, "ProgrammingProjectType_TelegramBot", "Create a Telegram bot with command flow, user scenarios, message states, error handling, and deployable implementation structure."),
        new(ProgrammingProjectType.AutomationScript, "ProgrammingProjectType_AutomationScript", "Create an automation script with clear inputs, processing steps, file or service interactions, safety checks, logging, and expected outputs."),
        new(ProgrammingProjectType.Parser, "ProgrammingProjectType_Parser", "Create a parser or scraper with source structure, extraction rules, normalization, retries, throttling, storage format, and failure handling."),
        new(ProgrammingProjectType.ApiBackend, "ProgrammingProjectType_ApiBackend", "Create an API or backend service with routes, contracts, validation, business flow, persistence assumptions, error model, and deployment-ready structure."),
        new(ProgrammingProjectType.DesktopApp, "ProgrammingProjectType_DesktopApp", "Create a desktop application with window structure, user flows, state handling, file or system interactions, and platform-appropriate UX.")
    ];

    public static readonly IReadOnlyList<ProgrammingPromptStyleDefinition> ProgrammingPromptStyles =
    [
        new(ProgrammingPromptStyle.Auto, "ProgrammingPromptStyle_Auto", "Select the most fitting implementation and product style for the chosen software type."),
        new(ProgrammingPromptStyle.MinimalistWebsite, "ProgrammingPromptStyle_MinimalistWebsite", "Use a restrained minimalist structure, clean spacing, concise sections, and no unnecessary visual or technical complexity."),
        new(ProgrammingPromptStyle.CorporateWebsite, "ProgrammingPromptStyle_CorporateWebsite", "Use a professional corporate structure with trust-building sections, sober hierarchy, and implementation choices suitable for a business website."),
        new(ProgrammingPromptStyle.PremiumShowcase, "ProgrammingPromptStyle_PremiumShowcase", "Use a premium showcase style with polished presentation, strong visual hierarchy, refined interaction details, and product-led emphasis."),
        new(ProgrammingPromptStyle.EditorialStudio, "ProgrammingPromptStyle_EditorialStudio", "Use an editorial studio style with strong typography, modular sections, visual storytelling, and portfolio-like presentation."),
        new(ProgrammingPromptStyle.BrightPromo, "ProgrammingPromptStyle_BrightPromo", "Use a bright promotional style with punchy CTA logic, campaign energy, bold section rhythm, and immediate readability."),
        new(ProgrammingPromptStyle.DarkTech, "ProgrammingPromptStyle_DarkTech", "Use a dark tech product style with modern app framing, technical confidence, focused feature communication, and polished contemporary implementation."),
        new(ProgrammingPromptStyle.SaasDashboard, "ProgrammingPromptStyle_SaasDashboard", "Use a SaaS dashboard style with product metrics, cards, filters, productivity patterns, and clean admin-oriented interaction."),
        new(ProgrammingPromptStyle.AnalyticalDashboard, "ProgrammingPromptStyle_AnalyticalDashboard", "Use an analytical dashboard style with dense information layout, comparison logic, drill-down thinking, and serious data readability."),
        new(ProgrammingPromptStyle.ExecutiveDashboard, "ProgrammingPromptStyle_ExecutiveDashboard", "Use an executive dashboard style with summary-first hierarchy, key KPIs, high-level trends, and decision-oriented clarity."),
        new(ProgrammingPromptStyle.MobileFirstApp, "ProgrammingPromptStyle_MobileFirstApp", "Use a mobile-first product style with compact screens, touch-oriented interaction, simplified flows, and responsive priorities."),
        new(ProgrammingPromptStyle.ProductiveWebApp, "ProgrammingPromptStyle_ProductiveWebApp", "Use a productivity-app style with task flow, states, shortcuts or efficiencies where relevant, and practical UX decisions."),
        new(ProgrammingPromptStyle.CommunityPlatform, "ProgrammingPromptStyle_CommunityPlatform", "Use a community-platform style with profiles, feed or interaction loops, moderation-aware structure, and social usability."),
        new(ProgrammingPromptStyle.RetroArcadeGame, "ProgrammingPromptStyle_RetroArcadeGame", "Use a retro arcade game style with a simple addictive loop, score-driven progression, readable rules, and lightweight browser implementation."),
        new(ProgrammingPromptStyle.PuzzleGame, "ProgrammingPromptStyle_PuzzleGame", "Use a puzzle-game style with clear mechanics, escalating challenge, hint or reset behavior, and deterministic play rules."),
        new(ProgrammingPromptStyle.NeonActionGame, "ProgrammingPromptStyle_NeonActionGame", "Use a neon action-game style with fast feedback, punchy controls, visible danger states, and energetic game feel."),
        new(ProgrammingPromptStyle.CartoonGame, "ProgrammingPromptStyle_CartoonGame", "Use a playful cartoon game style with friendly rules, exaggerated feedback, readable visuals, and approachable mechanics."),
        new(ProgrammingPromptStyle.PhysicsToyGame, "ProgrammingPromptStyle_PhysicsToyGame", "Use a physics-toy style with object interaction, experimentation, playful systems, and responsive motion behavior."),
        new(ProgrammingPromptStyle.LightweightUtility, "ProgrammingPromptStyle_LightweightUtility", "Use a lightweight utility style with one fast workflow, minimal dependencies, direct controls, and low-friction operation."),
        new(ProgrammingPromptStyle.PowerUserTool, "ProgrammingPromptStyle_PowerUserTool", "Use a power-user tool style with batch operations, useful options, structured outputs, and efficiency-focused decisions."),
        new(ProgrammingPromptStyle.ConversationalBot, "ProgrammingPromptStyle_ConversationalBot", "Use a conversational bot style with natural dialog flow, clear commands, fallback messages, and user-friendly interactions."),
        new(ProgrammingPromptStyle.SalesBot, "ProgrammingPromptStyle_SalesBot", "Use a sales or lead bot style with onboarding questions, qualification logic, clear calls to action, and handoff-ready message flow."),
        new(ProgrammingPromptStyle.ContentParser, "ProgrammingPromptStyle_ContentParser", "Use a content-parser style with robust extraction rules, structured fields, normalization, and source variability handling."),
        new(ProgrammingPromptStyle.DataCollector, "ProgrammingPromptStyle_DataCollector", "Use a data-collector style with scheduled retrieval, deduplication, storage logic, observability, and operational safety."),
        new(ProgrammingPromptStyle.DeveloperApi, "ProgrammingPromptStyle_DeveloperApi", "Use a developer-oriented API style with explicit contracts, validation, stable naming, error semantics, and maintainable service structure."),
        new(ProgrammingPromptStyle.StartupMvp, "ProgrammingPromptStyle_StartupMvp", "Use a startup MVP style with the shortest path to a usable result, lean architecture, clear scope boundaries, and practical trade-offs."),
        new(ProgrammingPromptStyle.NativeDesktop, "ProgrammingPromptStyle_NativeDesktop", "Use a native desktop-app style with platform-friendly UX, window behavior, local-file or system integration, and practical reliability."),
        new(ProgrammingPromptStyle.CrossPlatformDesktop, "ProgrammingPromptStyle_CrossPlatformDesktop", "Use a cross-platform desktop style with portable architecture, shared UI logic, distribution awareness, and pragmatic compatibility choices.")
    ];

    public static IEnumerable<ProgrammingPromptStyleDefinition> GetProgrammingStyles(ProgrammingProjectType type)
    {
        static bool IsOneOf(ProgrammingPromptStyle value, params ProgrammingPromptStyle[] allowed) => allowed.Contains(value);

        return ProgrammingPromptStyles.Where(item =>
            item.Style == ProgrammingPromptStyle.Auto ||
            type == ProgrammingProjectType.Auto && item.Style == ProgrammingPromptStyle.Auto ||
            type == ProgrammingProjectType.Website && IsOneOf(item.Style, ProgrammingPromptStyle.MinimalistWebsite, ProgrammingPromptStyle.CorporateWebsite, ProgrammingPromptStyle.PremiumShowcase, ProgrammingPromptStyle.EditorialStudio, ProgrammingPromptStyle.BrightPromo, ProgrammingPromptStyle.DarkTech) ||
            type == ProgrammingProjectType.LandingPage && IsOneOf(item.Style, ProgrammingPromptStyle.MinimalistWebsite, ProgrammingPromptStyle.BrightPromo, ProgrammingPromptStyle.PremiumShowcase, ProgrammingPromptStyle.DarkTech, ProgrammingPromptStyle.StartupMvp) ||
            type == ProgrammingProjectType.OnlineStore && IsOneOf(item.Style, ProgrammingPromptStyle.CorporateWebsite, ProgrammingPromptStyle.PremiumShowcase, ProgrammingPromptStyle.MobileFirstApp, ProgrammingPromptStyle.BrightPromo) ||
            type == ProgrammingProjectType.Dashboard && IsOneOf(item.Style, ProgrammingPromptStyle.SaasDashboard, ProgrammingPromptStyle.AnalyticalDashboard, ProgrammingPromptStyle.ExecutiveDashboard) ||
            type == ProgrammingProjectType.WebApp && IsOneOf(item.Style, ProgrammingPromptStyle.MobileFirstApp, ProgrammingPromptStyle.ProductiveWebApp, ProgrammingPromptStyle.CommunityPlatform, ProgrammingPromptStyle.StartupMvp) ||
            type == ProgrammingProjectType.HtmlGame && IsOneOf(item.Style, ProgrammingPromptStyle.RetroArcadeGame, ProgrammingPromptStyle.PuzzleGame, ProgrammingPromptStyle.NeonActionGame, ProgrammingPromptStyle.CartoonGame, ProgrammingPromptStyle.PhysicsToyGame) ||
            type == ProgrammingProjectType.MiniUtility && IsOneOf(item.Style, ProgrammingPromptStyle.LightweightUtility, ProgrammingPromptStyle.PowerUserTool) ||
            type == ProgrammingProjectType.TelegramBot && IsOneOf(item.Style, ProgrammingPromptStyle.ConversationalBot, ProgrammingPromptStyle.SalesBot) ||
            type == ProgrammingProjectType.AutomationScript && IsOneOf(item.Style, ProgrammingPromptStyle.LightweightUtility, ProgrammingPromptStyle.PowerUserTool, ProgrammingPromptStyle.StartupMvp) ||
            type == ProgrammingProjectType.Parser && IsOneOf(item.Style, ProgrammingPromptStyle.ContentParser, ProgrammingPromptStyle.DataCollector) ||
            type == ProgrammingProjectType.ApiBackend && IsOneOf(item.Style, ProgrammingPromptStyle.DeveloperApi, ProgrammingPromptStyle.StartupMvp) ||
            type == ProgrammingProjectType.DesktopApp && IsOneOf(item.Style, ProgrammingPromptStyle.NativeDesktop, ProgrammingPromptStyle.CrossPlatformDesktop));
    }

    public static readonly IReadOnlyList<VisualTargetModelDefinition> VisualTargetModels =
    [
        new(VisualTargetModel.Universal, "VisualTarget_Universal", "Use a compact, fluent natural-language visual prompt that is compatible with modern image models."),
        new(VisualTargetModel.GptImage, "VisualTarget_GptImage", "Use direct natural language with clear subject, composition, and lighting. Avoid keyword lists, negative prompts, and implementation commentary."),
        new(VisualTargetModel.Flux, "VisualTarget_Flux", "Use a precise, visually specific natural-language description with clear subject relationships, composition, material detail, lighting, and style. Avoid negative prompts and empty quality tags."),
        new(VisualTargetModel.NanoBanana, "VisualTarget_NanoBanana", "Use direct natural language that prioritizes the requested subject, action, spatial relationships, and preservation of unmentioned elements for editing tasks. Avoid negative prompts and irrelevant stylistic filler.")
        ,new(VisualTargetModel.Midjourney, "VisualTarget_Midjourney", "Use one concise, vivid visual phrase. Keep only image-defining details, use concrete nouns and modifiers, avoid explanations and long instructions, and append only a suitable --ar aspect-ratio parameter.")
        ,new(VisualTargetModel.StableDiffusion, "VisualTarget_StableDiffusion", "Use a compact, precise positive image prompt with concrete subjects, composition, lighting, materials, and style terms. Do not add a negative prompt unless the user explicitly asks for one.")
    ];

    public static readonly IReadOnlyList<AnimationStyleDefinition> AnimationStyles =
    [
        new(AnimationStyle.Auto, "AnimationStyle_Auto", "Select the most fitting animation language for the brief."),
        new(AnimationStyle.Pixar, "AnimationStyle_Pixar", "Premium Pixar-style 3D character animation, appealing rounded forms, expressive faces, tactile materials, cinematic family-film lighting."),
        new(AnimationStyle.Disney, "AnimationStyle_Disney", "Classic Disney-style animated feature illustration, expressive character acting, graceful shapes, warm storybook color and theatrical staging."),
        new(AnimationStyle.AnimeShonen, "AnimationStyle_AnimeShonen", "High-energy shonen anime, dynamic foreshortening, bold action lines, vivid cel shading, dramatic hero framing."),
        new(AnimationStyle.AnimeGhibli, "AnimationStyle_AnimeGhibli", "Hand-painted Japanese fantasy animation, gentle expressive characters, lush environmental detail, soft watercolor-like light, whimsical atmosphere."),
        new(AnimationStyle.AnimeCyberpunk, "AnimationStyle_AnimeCyberpunk", "Cyberpunk anime, neon city atmosphere, sharp graphic silhouettes, saturated night color, high-tech environmental detail."),
        new(AnimationStyle.AnimeShojo, "AnimationStyle_AnimeShojo", "Shojo anime, elegant character design, delicate linework, luminous pastel color, romantic emotional framing."),
        new(AnimationStyle.AnimePinup, "AnimationStyle_AnimePinup", "Anime pin-up illustration of an adult character, confident stylized pose, polished cel shading, expressive clean linework, glamorous color design, tasteful editorial composition."),
        new(AnimationStyle.AnimeChibi, "AnimationStyle_AnimeChibi", "Cute chibi anime, deliberately compact proportions, oversized expressive eyes, simplified clean forms, bright playful color, and instantly readable emotion."),
        new(AnimationStyle.AnimeCinematic, "AnimationStyle_AnimeCinematic", "Contemporary cinematic anime, richly detailed painted backgrounds, volumetric light, nuanced expressions, precise staging, and polished feature-film composition."),
        new(AnimationStyle.AnimeHorror, "AnimationStyle_AnimeHorror", "Psychological horror anime, unsettling composition, controlled shadows, eerie color contrast, restrained surreal detail, and sustained visual tension."),
        new(AnimationStyle.AnimeIsekai, "AnimationStyle_AnimeIsekai", "Isekai fantasy anime, a vivid magical world, adventurous character design, atmospheric environments, clear world-building detail, and luminous fantasy light."),
        new(AnimationStyle.AnimeMagicalGirl, "AnimationStyle_AnimeMagicalGirl", "Magical-girl anime, elegant transformation-inspired costume design, decorative energy motifs, luminous color, graceful poses, and optimistic theatrical staging."),
        new(AnimationStyle.AnimeMecha, "AnimationStyle_AnimeMecha", "Mecha anime, believable mechanical design, articulated hard-surface detail, dramatic scale, clean technical silhouettes, and energetic action framing."),
        new(AnimationStyle.AnimeRetro80s, "AnimationStyle_AnimeRetro80s", "1980s retro anime, hand-painted cel shading, saturated synth-era color, confident ink lines, subtle film grain, and dramatic mechanical or urban detail."),
        new(AnimationStyle.AnimeRetro90s, "AnimationStyle_AnimeRetro90s", "1990s retro anime, clean cel animation, expressive faces, rich hand-painted backgrounds, nostalgic color, and a polished television-animation feel."),
        new(AnimationStyle.AnimeSamurai, "AnimationStyle_AnimeSamurai", "Samurai anime, historical Japanese atmosphere, disciplined swordsmanship poses, ink-inspired textures, dramatic weather, and restrained cinematic framing."),
        new(AnimationStyle.AnimeSeinen, "AnimationStyle_AnimeSeinen", "Seinen anime, mature naturalistic character proportions, grounded drama, nuanced acting, cinematic lighting, and detailed believable environments."),
        new(AnimationStyle.AnimeSliceOfLife, "AnimationStyle_AnimeSliceOfLife", "Slice-of-life anime, everyday human moments, natural gestures, soft ambient light, believable domestic detail, and calm emotional observation."),
        new(AnimationStyle.AnimeSports, "AnimationStyle_AnimeSports", "Sports anime, clear athletic motion, dynamic perspective, intense focused expressions, readable action beats, and energetic speed lines used with restraint."),
        new(AnimationStyle.AdventureTime, "AnimationStyle_AdventureTime", "Adventure Time-style 2D animation, simple rounded characters, bright whimsical fantasy, playful surreal world design, clean contours, and expressive color."),
        new(AnimationStyle.Arcane, "AnimationStyle_Arcane", "Arcane-style painterly 3D animation, textured materials, expressive realistic faces, dramatic color contrast, detailed world-building, and cinematic light."),
        new(AnimationStyle.FamilyGuy, "AnimationStyle_FamilyGuy", "Family Guy-style adult sitcom animation, simple flat 2D character design, suburban setting, clean bold contours, restrained television staging, and dry visual comedy."),
        new(AnimationStyle.GravityFalls, "AnimationStyle_GravityFalls", "Gravity Falls-style mystery adventure animation, warm forest palette, expressive simplified characters, playful supernatural details, and cozy cinematic staging."),
        new(AnimationStyle.LooneyTunes, "AnimationStyle_LooneyTunes", "Looney Tunes-style classic 2D animation, elastic poses, expressive squash and stretch, bold theatrical timing, clean painted backgrounds, and playful slapstick energy."),
        new(AnimationStyle.RickAndMorty, "AnimationStyle_RickAndMorty", "Rick and Morty-style adult sci-fi animation, simple bold 2D linework, strange alien technology, saturated portal color, absurd cosmic scale, and deadpan staging."),
        new(AnimationStyle.Simpsons, "AnimationStyle_Simpsons", "The Simpsons-style 2D television animation, flat saturated color, yellow-skinned cartoon characters, simple clean contours, suburban American setting, and sitcom framing."),
        new(AnimationStyle.SouthPark, "AnimationStyle_SouthPark", "South Park-style cutout animation, deliberately simple paper-collage shapes, flat color, crude handmade texture, minimal depth, and direct comedic staging."),
        new(AnimationStyle.SpiderVerse, "AnimationStyle_SpiderVerse", "Spider-Verse-style comic-book 3D animation, graphic halftone texture, bold line accents, kinetic perspective, offset-print color, and dynamic panel-inspired framing."),
        new(AnimationStyle.TomAndJerry, "AnimationStyle_TomAndJerry", "Tom and Jerry-style classic 2D slapstick animation, expressive elastic motion, polished painted backgrounds, clear chase choreography, and playful visual timing."),
        new(AnimationStyle.ComicAmerican, "AnimationStyle_ComicAmerican", "American superhero comic art, bold ink contours, dramatic perspective, saturated print color, halftone texture."),
        new(AnimationStyle.ComicEuropean, "AnimationStyle_ComicEuropean", "European bande dessinee comic art, clean precise linework, sophisticated color, detailed cinematic environments."),
        new(AnimationStyle.ComicManga, "AnimationStyle_ComicManga", "Black-and-white manga art, expressive screentones, kinetic framing, precise ink linework, controlled contrast."),
        new(AnimationStyle.StopMotion, "AnimationStyle_StopMotion", "Handcrafted stop-motion animation, miniature sets, tactile practical materials, charming frame-by-frame character design."),
        new(AnimationStyle.Claymation, "AnimationStyle_Claymation", "Claymation animation, hand-molded clay characters, soft studio light, tactile fingerprints and handcrafted miniature scenery."),
        new(AnimationStyle.ClassicFairytale, "AnimationStyle_ClassicFairytale", "Classic storybook animation, soft rounded forms, warm color, expressive eyes, graceful romantic staging, and gentle hand-drawn charm."),
        new(AnimationStyle.FamilyThreeDimensional, "AnimationStyle_FamilyThreeDimensional", "Contemporary family 3D animation, appealing rounded characters, soft global illumination, tactile stylized materials, cinematic depth, and welcoming emotion."),
        new(AnimationStyle.PainterlyFairytale, "AnimationStyle_PainterlyFairytale", "Painterly animated fairytale, watercolor-like backgrounds, natural luminous light, hand-painted texture, expressive characters, and gentle cinematic atmosphere."),
        new(AnimationStyle.TvCartoon, "AnimationStyle_TvCartoon", "Modern TV cartoon, thick clean outlines, flat bright color, simplified readable forms, expressive faces, and clear comedic staging."),
        new(AnimationStyle.MinimalistTvCartoon, "AnimationStyle_MinimalistTvCartoon", "Minimalist contemporary cartoon, simple geometric shapes, limited detail, pastel palette, clean composition, and calm friendly expression."),
        new(AnimationStyle.TeenCartoon, "AnimationStyle_TeenCartoon", "Energetic teen cartoon, angular poses, exaggerated expressions, sharp silhouettes, saturated color, and lively graphic motion."),
        new(AnimationStyle.RubberHose, "AnimationStyle_RubberHose", "1930s rubber-hose animation, elastic limbs, pie-cut eyes, bouncy vintage motion, bold ink contours, and aged theatrical cartoon charm."),
        new(AnimationStyle.SlapstickCartoon, "AnimationStyle_SlapstickCartoon", "Classic slapstick cartoon, fast readable action, playful squash and stretch, comic visual gags, expressive poses, and clean theatrical timing."),
        new(AnimationStyle.TheatricalRetroCartoon, "AnimationStyle_TheatricalRetroCartoon", "Retro theatrical cartoon, hand-painted cinema background, limited vintage palette, subtle film grain, poster-like color, and classic animation staging."),
        new(AnimationStyle.LigneClaire, "AnimationStyle_LigneClaire", "Ligne claire animation, uniform clean contour lines, flat even color, precise readable environments, controlled detail, and elegant graphic clarity."),
        new(AnimationStyle.FrancoBelgianAdventure, "AnimationStyle_FrancoBelgianAdventure", "Franco-Belgian adventure animation, expressive ink contour, light watercolor color, detailed travel or adventure setting, and clear optimistic storytelling."),
        new(AnimationStyle.EuropeanAuteur, "AnimationStyle_EuropeanAuteur", "European auteur animation, painterly textured brushwork, distinctive proportions, restrained color, poetic visual metaphor, and artful independent-film composition."),
        new(AnimationStyle.WatercolorStorybook, "AnimationStyle_WatercolorStorybook", "Watercolor storybook animation, visible paper texture, soft bleeding edges, delicate pastel pigment, handmade warmth, and tender illustrative detail."),
        new(AnimationStyle.GouachePencil, "AnimationStyle_GouachePencil", "Gouache-and-pencil animation, matte opaque paint, visible graphite line, layered handmade surface, expressive children’s-book color, and crafted detail."),
        new(AnimationStyle.PaperCutout, "AnimationStyle_PaperCutout", "Paper-cutout animation, layered handcrafted paper shapes, crisp cut edges, decorative depth, tactile shadow, and clear silhouette-based storytelling."),
        new(AnimationStyle.CrayonPastel, "AnimationStyle_CrayonPastel", "Crayon-and-pastel animation, naive hand-drawn line, grainy wax texture, soft chalky color, playful childlike mark-making, and warm paper surface."),
        new(AnimationStyle.TextileStopMotion, "AnimationStyle_TextileStopMotion", "Textile stop-motion animation, felt, fabric, yarn, stitched seams, handcrafted puppet character, miniature set, and soft practical studio light."),
        new(AnimationStyle.LowPolyCartoon, "AnimationStyle_LowPolyCartoon", "Low-poly cartoon 3D, simple faceted geometry, large planes of color, stylized friendly materials, graphic lighting, and clean readable silhouettes."),
        new(AnimationStyle.PopArtCartoon, "AnimationStyle_PopArtCartoon", "Pop-art cartoon, bold ink contours, vibrant contrast, graphic patterns, halftone texture, energetic poster composition, and no lettering unless the brief supplies exact text."),
        new(AnimationStyle.GraphicNovelNoir, "AnimationStyle_GraphicNovelNoir", "Graphic-novel noir animation, expressive ink, deep directional shadow, muted limited palette, dramatic negative space, and cinematic crime-story atmosphere."),
        new(AnimationStyle.FlatIllustration, "AnimationStyle_FlatIllustration", "Flat illustration animation, crisp vector-like shapes, minimal depth, deliberate color blocks, clear interface-grade composition, and clean modern readability."),
        new(AnimationStyle.MemphisCartoon, "AnimationStyle_MemphisCartoon", "Memphis cartoon, playful abstract geometry, bold contrast, lively dots and zigzags, graphic 1980s-inspired pattern, and upbeat editorial energy."),
        new(AnimationStyle.PsychedelicCartoon, "AnimationStyle_PsychedelicCartoon", "Psychedelic cartoon, flowing surreal forms, intense harmonious color, rhythmic organic patterns, imaginative visual transformation, and controlled graphic clarity."),
        new(AnimationStyle.InkWashAnimation, "AnimationStyle_InkWashAnimation", "Ink-wash animation, expressive East Asian brushwork, fluid diluted ink, rice-paper texture, restrained tonal palette, and contemplative motion."),
        new(AnimationStyle.SketchAnimation, "AnimationStyle_SketchAnimation", "Sketch animation, lively pencil line, visible construction marks, evolving hand-drawn gesture, light paper texture, and spontaneous animated-drawing energy."),
        new(AnimationStyle.CartoonPinup, "AnimationStyle_CartoonPinup", "Cartoon pin-up illustration of an adult character, retro glamour, warm poster palette, confident stylized pose, polished linework, and tasteful editorial composition."),
        new(AnimationStyle.CartoonRockabilly, "AnimationStyle_CartoonRockabilly", "Rockabilly cartoon, saturated 1950s diner color, polka dots and cherries only when relevant, playful retro fashion, bold clean contours, and lively poster energy."),
        new(AnimationStyle.CartoonTiki, "AnimationStyle_CartoonTiki", "Tiki tropical cartoon, palms and hibiscus when relevant, sunset orange and turquoise palette, playful mid-century resort mood, and decorative graphic shapes."),
        new(AnimationStyle.CartoonArtDeco, "AnimationStyle_CartoonArtDeco", "Art Deco cartoon, elegant elongated silhouettes, polished geometric ornament, gold accents, refined symmetry, and glamorous theatrical poster composition.")
    ];

    public static readonly IReadOnlyList<AnimationStyleSectionDefinition> AnimationStyleSections =
    [
        new(AnimationStyleSection.All, "AnimationSection_All", "any animation visual language"),
        new(AnimationStyleSection.Brands, "AnimationSection_Brands", "recognizable studio and series-inspired visual languages"),
        new(AnimationStyleSection.Anime, "AnimationSection_Anime", "anime visual languages"),
        new(AnimationStyleSection.TwoDimensionalTelevision, "AnimationSection_TwoDimensionalTelevision", "2D and television cartoon visual languages"),
        new(AnimationStyleSection.ThreeDimensionalStopMotion, "AnimationSection_ThreeDimensionalStopMotion", "stylized 3D and stop-motion visual languages"),
        new(AnimationStyleSection.ComicsGraphics, "AnimationSection_ComicsGraphics", "comic and graphic visual languages"),
        new(AnimationStyleSection.BookIllustration, "AnimationSection_BookIllustration", "storybook and handmade illustration visual languages"),
        new(AnimationStyleSection.Retro, "AnimationSection_Retro", "retro animation visual languages"),
        new(AnimationStyleSection.Experimental, "AnimationSection_Experimental", "experimental animation visual languages")
    ];

    public static IEnumerable<AnimationStyleDefinition> GetAnimationStyles(AnimationStyleSection section) =>
        AnimationStyles.Where(style => style.Style == AnimationStyle.Auto || section == AnimationStyleSection.All || GetAnimationStyleSection(style.Style) == section);

    private static AnimationStyleSection GetAnimationStyleSection(AnimationStyle style) => style switch
    {
        AnimationStyle.Pixar or AnimationStyle.Disney or AnimationStyle.AdventureTime or AnimationStyle.Arcane or
        AnimationStyle.FamilyGuy or AnimationStyle.GravityFalls or AnimationStyle.LooneyTunes or AnimationStyle.RickAndMorty or
        AnimationStyle.Simpsons or AnimationStyle.SouthPark or AnimationStyle.SpiderVerse or AnimationStyle.TomAndJerry => AnimationStyleSection.Brands,
        AnimationStyle.AnimeShonen or AnimationStyle.AnimeGhibli or AnimationStyle.AnimeCyberpunk or AnimationStyle.AnimeShojo or
        AnimationStyle.AnimePinup or AnimationStyle.AnimeChibi or AnimationStyle.AnimeCinematic or AnimationStyle.AnimeHorror or
        AnimationStyle.AnimeIsekai or AnimationStyle.AnimeMagicalGirl or AnimationStyle.AnimeMecha or AnimationStyle.AnimeRetro80s or
        AnimationStyle.AnimeRetro90s or AnimationStyle.AnimeSamurai or AnimationStyle.AnimeSeinen or AnimationStyle.AnimeSliceOfLife or
        AnimationStyle.AnimeSports => AnimationStyleSection.Anime,
        AnimationStyle.TvCartoon or AnimationStyle.MinimalistTvCartoon or AnimationStyle.TeenCartoon or AnimationStyle.RubberHose or
        AnimationStyle.SlapstickCartoon => AnimationStyleSection.TwoDimensionalTelevision,
        AnimationStyle.StopMotion or AnimationStyle.Claymation or AnimationStyle.FamilyThreeDimensional or AnimationStyle.TextileStopMotion or
        AnimationStyle.LowPolyCartoon => AnimationStyleSection.ThreeDimensionalStopMotion,
        AnimationStyle.ComicAmerican or AnimationStyle.ComicEuropean or AnimationStyle.ComicManga or AnimationStyle.LigneClaire or
        AnimationStyle.FrancoBelgianAdventure or AnimationStyle.PopArtCartoon or AnimationStyle.GraphicNovelNoir or AnimationStyle.FlatIllustration => AnimationStyleSection.ComicsGraphics,
        AnimationStyle.ClassicFairytale or AnimationStyle.PainterlyFairytale or AnimationStyle.EuropeanAuteur or AnimationStyle.WatercolorStorybook or
        AnimationStyle.GouachePencil or AnimationStyle.PaperCutout or AnimationStyle.CrayonPastel or AnimationStyle.InkWashAnimation or
        AnimationStyle.SketchAnimation => AnimationStyleSection.BookIllustration,
        AnimationStyle.TheatricalRetroCartoon or AnimationStyle.CartoonPinup or AnimationStyle.CartoonRockabilly or AnimationStyle.CartoonTiki or
        AnimationStyle.CartoonArtDeco => AnimationStyleSection.Retro,
        AnimationStyle.MemphisCartoon or AnimationStyle.PsychedelicCartoon => AnimationStyleSection.Experimental,
        _ => AnimationStyleSection.All
    };

    private const string ProgrammingInstruction = """
        Convert the user's brief into one complete, professional, ready-to-use prompt for an AI system that works with software development.

        Return only the finished prompt. Do not greet the user, explain your work, offer alternatives, ask questions, or continue a dialogue.

        Preserve the language of the user's brief unless the user explicitly requests another language.

        Apply this selected software type: {programmingProjectType}
        Apply this selected product style: {programmingStyle}

        Preserve every explicit requirement from the brief. Do not invent a programming language, framework, platform, architecture, database, API, library, file structure, business rule, credential, measurement, or external dependency that the user did not provide or clearly imply.

        Do not add a decorative role such as "You are a senior developer" unless the user explicitly requests a role or a specific area of expertise is essential to the task.

        Treat the request as a product-building or code-generation task, not as a code review, refactoring brief, or debugging investigation unless the user explicitly supplies that context.

        Describe only the relevant requirements:
        - the intended result;
        - software type and main user scenario;
        - target platform and technology stack when known;
        - required functionality;
        - user interaction;
        - data handling;
        - integrations;
        - error handling;
        - edge cases;
        - non-functional requirements;
        - expected deliverables;
        - acceptance criteria.

        When the chosen type implies interface work such as a website, landing page, dashboard, web app, HTML game, or desktop app, ask for a concrete screen or page structure, key states, and responsive behavior where relevant.

        When the chosen type implies bot, API, parser, script, or utility behavior, ask for inputs, outputs, execution flow, validation, failure handling, and the expected runtime context.

        For security-related tasks, prohibit destructive actions, unsafe assumptions, hidden backdoors, credential exposure, and weakening of existing protections.

        Use headings, lists, code blocks, workflow steps, and acceptance criteria only when they improve execution of the specific task. Keep the prompt proportional to the complexity of the user's brief.

        If an essential fact is missing and cannot be inferred safely, insert a concise square-bracket placeholder such as [target platform] or [path to the project] instead of asking a question.

        The result must be a prompt for creating the requested software artifact, not the implementation itself.
        """;

    private const string ImagesInstruction = """
        Turn the user's brief into one polished English prompt for Flux, GPT-Image, Nano Banana, or another modern image generation or editing model.

        Return only one finished prompt as a natural-language paragraph. Never add headings, lists, a negative-prompt section, placeholders, explanations, questions, role-play, or instructions about reasoning.

        Preserve the user's non-negotiable core: the requested subject, action, objects, setting, reference-image instructions, and explicit style. Translate the working prompt into fluent English.

        Never introduce text into the prompt: do not invent a phrase, sentence, caption, label, slogan, quote, lettering, or a "text" clause. Mention visible text only when the user explicitly supplied the exact wording; reproduce only that wording in quotation marks.

        Act as an expert art director. Expand a short or rough idea into a complete, specific, visually compelling scene. Confidently add harmonious details that make the image feel intentional: a fitting subject appearance, wardrobe, gesture, composition, environment, time of day, lighting, palette, materials, atmosphere, camera framing, lens perspective, and surface detail. Do not invent named brands, celebrities, factual claims, copyrighted characters, or visible wording that the user did not request.

        Apply this selected photo section: {photoSection}
        Apply this selected photo style: {photoStyle}
        Apply this target-model profile: {visualTarget}

        Do not default to moody underexposure, crushed blacks, HDR-like tone mapping, or stylized cinematic grading unless the user explicitly requests that look. Respect an explicitly requested medium or style instead; logos, icons, UI, and illustrations must use the medium that best suits the request rather than photography.

        Describe the visible result directly. Include the details that materially affect the image:
        - main subject;
        - appearance;
        - pose or action;
        - important objects;
        - composition;
        - framing;
        - camera angle;
        - perspective;
        - environment;
        - background;
        - lighting;
        - color palette;
        - materials and textures;
        - atmosphere;
        - visual style;
        - aspect ratio.

        Arrange the description in a logical visual order: main subject, action, composition, environment, lighting, style, and visual finish. Include one suitable aspect ratio: choose 4:5 for a portrait-oriented subject, 16:9 for a wide scene, and 1:1 for a logo, icon, product, or balanced composition unless the brief specifies one.

        For image editing, make the requested change prominent and preserve the unmentioned identity, proportions, pose, composition, and scene continuity.

        When the user supplies exact text that must appear in the image, reproduce it exactly without translation, correction, added punctuation, or alternative wording.

        For logos and icons, prioritize recognizability, simple geometry, clean silhouette, scalability, and readability at the target size. Do not add photorealistic details unless requested.

        Do not add generic filler such as "masterpiece", "award-winning", "best quality", "8K", "trending", or "ultra detailed". Describe desired outcomes positively in the same paragraph; do not write a separate negative prompt.
        """;

    private const string PaintingsInstruction = """
        Turn the user's brief into one polished English prompt for a high-end fine-art painting generator.

        Return only one finished natural-language prompt. Never add headings, lists, explanations, questions, placeholders, a negative-prompt section, or instructions about reasoning.

        Preserve the brief's non-negotiable subject, action, objects, and setting. Translate the working prompt into fluent English. Never invent a phrase, sentence, caption, label, slogan, quote, lettering, or a "text" clause; mention visible text only when the user explicitly supplied the exact wording.

        Act as an exceptional painter and art director. Expand a short idea into a complete, emotionally resonant painted scene with deliberate composition, hierarchy, gesture, setting, light, palette, atmosphere, material texture, brushwork, and a suitable aspect ratio. Add coherent artistic details freely, but never invent brands, celebrities, copyrighted characters, factual claims, or unrequested visible wording.

        Apply this chosen style direction: {paintingStyle}
        Apply this chosen artist orientation: {paintingArtist}
        Apply this target-model profile: {visualTarget}

        Keep the result unmistakably painterly. Treat artist references as strong stylistic orientation only; do not promise or imply an exact identity match. Respect an explicitly requested medium, period, or artist direction when it conflicts with the selected default. Select 4:5 for portrait-oriented scenes, 16:9 for broad narratives, and 1:1 for a balanced composition unless the user specifies another ratio.

        Generate the depicted scene itself as artwork, not a photograph of an artwork or a decorative object. Do not add a frame, border, mat, canvas edge, easel, gallery wall, museum display, caption, signature, or any presentation context unless the user explicitly requests it.
        """;

    private const string IconsInstruction = """
        Turn the user's brief into one polished English prompt for generating an application icon.

        Return only one finished natural-language paragraph. Never add headings, lists, explanations, questions, placeholders, negative prompts, or invented visible text.

        Preserve the app's purpose and any explicit symbol, color, platform, or style requirement. Expand a short idea into an intentional icon direction, but never invent brands, copyrighted characters, slogans, letters, or tiny decorative details.

        Apply this platform treatment: {iconPlatform}
        Apply this icon style: {iconStyle}
        Apply this target-model profile: {visualTarget}

        Prioritize a single memorable symbol, simple geometry, a clean silhouette, clear negative space, balanced visual weight, safe padding, and recognition at small sizes. Specify a square 1:1 composition unless the user explicitly requests another format. The output must describe the icon asset itself, not a mockup, device screen, app-store listing, or photograph of an icon.
        """;

    private const string GraphicsInstruction = """
        Turn the user's brief into one polished English prompt for a graphic-design asset.

        Return only one finished natural-language paragraph. Never add headings, lists, explanations, questions, placeholders, negative prompts, or invented visible text.

        Preserve the requested subject, use case, exact user-supplied wording, and explicit visual constraints. Expand a short idea with coherent composition, hierarchy, palette, shape language, and finish, but never invent brands, factual claims, slogans, captions, or lettering.

        Apply this graphic asset type: {graphicType}
        Apply this graphic style: {graphicStyle}
        Apply this target-model profile: {visualTarget}

        Produce the requested graphic asset itself, not a photograph of it or a scene containing it. Use the format best suited to the asset: 1:1 for stickers, logos, UI elements, and icons; 16:9 for banners; 4:5 for posters; otherwise infer from the brief. Prioritize clear hierarchy, scalable shape design, and practical readability. When the selected type is an icon, require one dominant symbol that fills roughly 95-98% of the square canvas, a full-bleed solid background when a background is present, no rounded app-tile container unless the user explicitly asks for it, and no tiny centered glyph floating inside excessive padding.
        """;

    private const string AnimationInstruction = """
        Turn the user's brief into one polished English prompt for a high-end animation or comic image generator.

        Return only one finished natural-language prompt. Never add headings, lists, explanations, questions, placeholders, a negative-prompt section, or instructions about reasoning.

        Preserve the brief's non-negotiable subject, action, objects, and setting. Translate the working prompt into fluent English. Never invent a phrase, sentence, caption, label, slogan, quote, lettering, or a "text" clause; mention visible text only when the user explicitly supplied the exact wording.

        Act as an animation director and production designer. Expand a short idea into a complete, highly readable image with expressive character design, pose, gesture, composition, scene design, color script, lighting, materials, atmosphere, and a suitable aspect ratio. Add coherent details freely, but never invent brands, celebrities, copyrighted characters, factual claims, or unrequested visible wording.

        Apply this chosen animation direction: {animationStyle}
        Apply this target-model profile: {visualTarget}

        Keep the result clearly animated or sequential-art rather than photorealistic. Respect an explicitly requested medium or style when it conflicts with the selected default. Select 4:5 for a character portrait, 16:9 for a wide cinematic scene, and 1:1 for a balanced composition unless the user specifies another ratio.
        """;

    private const string TextsInstruction = """
        Convert the user's brief into one complete, ready-to-use prompt for an AI system that works with text.

        Return only the finished prompt. Do not greet the user, explain your work, offer alternatives, ask questions, or continue a dialogue.

        Preserve the language of the user's brief unless the user explicitly requests another language.

        Silently determine the task type: writing, rewriting, proofreading, grammar correction, translation, summarization, adaptation, shortening, expansion, formatting, document preparation, article writing, correspondence, social media content, script writing, or another text task.

        Apply this selected text format: {textType}
        Apply this selected tone: {textTone}

        Preserve every explicit requirement from the user's brief. Include only the parameters relevant to the detected task:
        - purpose;
        - intended audience;
        - source material;
        - output language;
        - tone;
        - style;
        - length;
        - structure;
        - terminology;
        - formatting;
        - facts that must be preserved;
        - content that must not be added;
        - expected output format.

        Do not automatically assign a role. Add a role only when the user explicitly requests it or when a narrowly defined professional perspective is essential to the result.

        For rewriting and editing, require preservation of the original meaning, facts, names, numbers, logic, and level of certainty. Do not allow the AI to add new claims or change the author's position.

        For proofreading, require correction only of the requested types of errors. Do not allow stylistic rewriting unless the user requests it.

        For translation, specify the source and target languages when known. Require preservation of meaning, tone, names, numbers, structure, terminology, and formatting. Do not add explanations or translator notes unless requested.

        For summarization, specify what information must be retained, the desired compression level, and the required format when known. Do not allow unsupported conclusions.

        For factual texts, prohibit invented facts, quotations, statistics, citations, links, sources, names, dates, and credentials.

        Use headings and lists only when they improve execution of the specific task. Do not force a universal structure onto simple requests.

        If an essential fact is missing and cannot be inferred safely, use a concise square-bracket placeholder instead of asking a question.

        The result must be a prompt for performing the text task, not the completed text itself.
        """;

    private const string VideoInstruction = """
        Convert the user's brief into one ready-to-use prompt for a video generation or video editing model.

        Return only the finished video prompt in English. Do not greet the user, explain your work, ask questions, offer alternatives, or continue a dialogue.

        Translate the user's intent into fluent English internally, even when the brief is written in another language. Preserve exact visible text only when the user explicitly supplies it for the video.

        Do not include a role, headings, workflow, acceptance criteria, model instructions, or explanations.

        Silently determine the task type: text-to-video, image-to-video, video editing, animation, cinematic shot, advertising video, product demonstration, character animation, environmental animation, or a sequence of scenes.

        Apply this selected video direction: {videoDirection}

        Describe the visible video directly.

        Include only relevant details:
        - subject;
        - initial state;
        - action and movement over time;
        - environment;
        - composition;
        - shot size;
        - camera position;
        - camera movement;
        - subject movement;
        - lighting;
        - visual style;
        - pacing;
        - duration;
        - aspect ratio;
        - transitions;
        - final state;
        - continuity requirements.

        Describe events in chronological order when the scene changes over time.

        For a single shot, avoid adding unnecessary scene divisions or editing instructions.

        For image-to-video, preserve the source image's identity, appearance, composition, clothing, proportions, background, lighting, and visual style. Add only the movement requested by the user.

        For video editing, clearly state what must change and what must remain unchanged.

        Do not invent dialogue, characters, objects, transformations, camera movement, cuts, visual effects, environmental events, or text that were not requested or clearly implied.

        Avoid contradictory instructions such as requiring a static camera and complex camera movement at the same time.

        Avoid excessive motion. Do not animate every object unless the user explicitly requests it.

        When visible text is required, reproduce it exactly as supplied.

        Do not add generic quality filler or a generic negative prompt.

        If an essential detail is missing and the video cannot reasonably be generated without it, use one concise square-bracket placeholder instead of asking a question.
        """;

    private const string MusicInstruction = """
        Convert the user's brief into one finished English music style prompt for the Styles field in Suno.

        Return only one finished English style prompt that can be pasted directly into Suno.

        Do not include:
        - headings;
        - labels such as "Style" or "Prompt";
        - explanations;
        - roles;
        - instructions to the user;
        - alternative versions;
        - song titles;
        - lyrics;
        - lyric themes;
        - verse or chorus text;
        - square-bracket metatags;
        - technical commentary.

        Treat the user's brief as a vibe, scene, situation, or emotional starting point. Your job is to translate that idea into a musically rich production-and-style prompt, not into a plot summary.

        Write one compact but information-dense natural-language paragraph in the style of a strong Suno prompt. The result should feel specific, contemporary, and creatively art-directed.

        Prefer prompts that combine:
        - a clear primary genre;
        - one or two compatible secondary influences;
        - an interesting production angle;
        - distinct rhythmic identity;
        - instrumentation that gives the track character;
        - a memorable vocal and arrangement direction when vocals are implied or requested.

        Strongly favor creative but coherent blends such as:
        - modern genre fusion;
        - regional or ethnic instrumentation integrated into a contemporary production;
        - unusual but usable texture combinations;
        - contrast between acoustic and electronic elements;
        - hybrid groove references;
        - unexpected arrangement or sound-design accents.

        The prompt may include only relevant musical characteristics:
        - primary genre and subgenre;
        - hybrid style influences;
        - mood and energy;
        - BPM or tempo range when useful;
        - rhythmic feel and groove;
        - instrumentation;
        - ethnic or regional timbres when suitable;
        - vocal type, vocal character, and delivery;
        - hook, chorus, drop, breakdown, or arrangement behavior;
        - production style;
        - sound texture and mix character;
        - era influence when it materially helps.

        Do not mechanically include every parameter. Include only details that materially strengthen the musical identity.

        Preserve the user's explicit genre, mood, instrumentation, vocal requirements, tempo, period, and production preferences.

        If the user gives only a minimal vibe or scenario, infer strong compatible musical details and turn it into a convincing style prompt. Do not leave the result generic.

        If the user gives no genre, choose the most fitting genre direction from the vibe and make it musically interesting.

        If the user gives no arrangement guidance, infer useful details such as groove structure, hook behavior, drop energy, percussion shape, bass movement, or vocal phrasing.

        If appropriate, make the result more distinctive through tasteful fusion rather than through randomness. Experimentation should feel intentional and playable, not chaotic.

        Do not retell the user's story as lyrics or as a cinematic synopsis. Convert the story or mood into musical characteristics.

        Do not add lyrics or write what the singer literally says.

        Do not add artist names. When the user references a performer, band, composer, or producer, translate the reference into general musical characteristics such as genre, instrumentation, vocal delivery, rhythm, arrangement, production, and atmosphere.

        Do not use visual terminology, camera terminology, storytelling instructions, or technical specifications unrelated to music.

        Avoid empty promotional words such as "masterpiece", "award-winning", "viral", "perfect", or "best quality".

        The result must contain only the finished style prompt for the Suno Styles field.
        """;

    private const string ThemesInstruction = """
        Turn the user's brief into one polished English prompt for a modern image-generation or image-editing model, focused on recognizable scene archetypes, thematic worlds, and genre mood.

        Return only one finished prompt as a natural-language paragraph. Never add headings, lists, explanations, questions, placeholders, a negative-prompt section, or instructions about reasoning.

        Preserve the brief's non-negotiable subject, action, objects, and setting. Translate the working prompt into fluent English. Never invent visible text, captions, signage, slogans, or lettering unless the user explicitly supplied the exact wording.

        Act as an experienced visual director. Expand a short idea into a coherent scene with clear subject hierarchy, environment, atmosphere, lighting, composition, material detail, and a suitable aspect ratio.

        Apply this selected thematic section: {themeSection}
        Apply this selected thematic style: {themeStyle}
        Apply this target-model profile: {visualTarget}

        Treat the selected section and style as scene-direction references, not as a literal instruction to copy a franchise, copyrighted character, or exact film frame.

        Include only the details that materially improve the prompt:
        - main subject;
        - action or situation;
        - environment;
        - mood and atmosphere;
        - composition;
        - camera framing or viewpoint when useful;
        - lighting;
        - palette;
        - materials and surface detail;
        - visual finish;
        - aspect ratio.

        For image editing, make the requested change clear while preserving every unmentioned important element of the original image.

        Do not add generic filler such as "masterpiece", "award-winning", "best quality", "8K", or "trending". Do not write a separate negative prompt.
        """;

    private const string AnalysisInstruction = """
        Convert the user's brief into one complete, professional, ready-to-use prompt for an AI system performing analysis, research, comparison, evaluation, or decision support.

        Return only the finished prompt. Do not greet the user, explain your work, offer alternatives, ask questions, or continue a dialogue.

        Preserve the language of the user's brief unless the user explicitly requests another language.

        Apply this selected analytical direction: {analysisDirection}

        Preserve every explicit requirement from the user's brief.

        Include only relevant elements:
        - analytical objective;
        - main question;
        - scope;
        - period;
        - objects being analyzed;
        - supplied evidence or data;
        - assumptions;
        - exclusions;
        - evaluation criteria;
        - required method;
        - source requirements;
        - uncertainty handling;
        - risks and limitations;
        - required output structure;
        - expected conclusions or recommendations.

        Do not automatically assign a role. Add a specialist perspective only when it materially determines the analysis method or the user explicitly requests it.

        Require the AI to separate:
        - confirmed facts;
        - source claims;
        - calculations;
        - assumptions;
        - interpretations;
        - estimates;
        - unknown information.

        Require missing evidence and uncertainty to be stated explicitly rather than hidden or replaced with invented information.

        Do not allow fabricated facts, sources, citations, links, quotations, statistics, dates, documents, measurements, or expert opinions.

        For current or time-sensitive research, require recent sources and publication dates when the user requests external research.

        Do not force academic structure onto a simple practical analysis. Keep the prompt proportional to the task.

        The result must be a prompt for performing the analysis, not the analysis itself.
        """;

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
            PromptBuilderCategory.Ideas => ThemesInstruction,
            PromptBuilderCategory.Paintings => PaintingsInstruction,
            PromptBuilderCategory.Animation => AnimationInstruction,
            PromptBuilderCategory.Icons => GraphicsInstruction,
            PromptBuilderCategory.Graphics => GraphicsInstruction,
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

            PromptBuilderCategory.Ideas => new(
                ThemesInstruction,
                512,
                2048,
                0.45),

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
        ProgrammingTaskType programmingTaskType = ProgrammingTaskType.Auto,
        ProgrammingProjectType programmingProjectType = ProgrammingProjectType.Auto,
        ProgrammingPromptStyle programmingStyle = ProgrammingPromptStyle.Auto,
        VisualTargetModel visualTarget = VisualTargetModel.Universal,
        ThemeSection themeSection = ThemeSection.All,
        ThemeStyle themeStyle = ThemeStyle.Auto,
        IconPlatform iconPlatform = IconPlatform.Auto,
        IconStyle iconStyle = IconStyle.Auto,
        GraphicType graphicType = GraphicType.Auto,
        GraphicStyle graphicStyle = GraphicStyle.Auto,
        AnimationStyleSection animationSection = AnimationStyleSection.All,
        PaintingStyleSection paintingSection = PaintingStyleSection.All)
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
        if (category is PromptBuilderCategory.Images or PromptBuilderCategory.Paintings or PromptBuilderCategory.Animation or PromptBuilderCategory.Ideas or PromptBuilderCategory.Icons or PromptBuilderCategory.Graphics)
        {
            VisualTargetModelDefinition target = VisualTargetModels.FirstOrDefault(item => item.Model == visualTarget) ?? VisualTargetModels[0];
            systemPrompt = systemPrompt.Replace("{visualTarget}", target.PromptDescriptor, StringComparison.Ordinal);
        }
        if (category == PromptBuilderCategory.Ideas)
        {
            ThemeSectionDefinition section = ThemeSections.FirstOrDefault(item => item.Section == themeSection) ?? ThemeSections[0];
            ThemeStyleDefinition style = GetThemeStyles(themeSection).FirstOrDefault(item => item.Style == themeStyle) ?? GetThemeStyles(themeSection).First();
            string descriptor = style.Style == ThemeStyle.Auto && themeSection != ThemeSection.All
                ? $"Select the most fitting scene treatment within {section.PromptDescriptor}."
                : style.PromptDescriptor;
            systemPrompt = systemPrompt.Replace("{themeSection}", section.PromptDescriptor, StringComparison.Ordinal)
                .Replace("{themeStyle}", descriptor, StringComparison.Ordinal);
        }
        if (category == PromptBuilderCategory.Paintings)
        {
            PaintingStyleDefinition style = PaintingStyles.FirstOrDefault(item => item.Style == paintingStyle) ?? PaintingStyles[0];
            PaintingArtistDefinition artist = PaintingArtists.FirstOrDefault(item => item.Artist == paintingArtist) ?? PaintingArtists[0];
            string descriptor = style.Style == PaintingStyle.Auto && paintingSection != PaintingStyleSection.All
                ? $"Select the most fitting style within {PaintingStyleSections.First(item => item.Section == paintingSection).PromptDescriptor}."
                : style.PromptDescriptor;
            systemPrompt = systemPrompt.Replace("{paintingStyle}", descriptor, StringComparison.Ordinal);
            systemPrompt = systemPrompt.Replace("{paintingArtist}", artist.PromptDescriptor, StringComparison.Ordinal);
        }
        if (category == PromptBuilderCategory.Animation)
        {
            AnimationStyleDefinition style = AnimationStyles.FirstOrDefault(item => item.Style == animationStyle) ?? AnimationStyles[0];
            string descriptor = style.Style == AnimationStyle.Auto && animationSection != AnimationStyleSection.All
                ? $"Select the most fitting style within {AnimationStyleSections.First(item => item.Section == animationSection).PromptDescriptor}."
                : style.PromptDescriptor;
            systemPrompt = systemPrompt.Replace("{animationStyle}", descriptor, StringComparison.Ordinal);
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
                .Replace("{graphicStyle}", styleDescriptor, StringComparison.Ordinal);
        }
        if (createAlternative)
        {
            systemPrompt += category switch
            {
                PromptBuilderCategory.Images or PromptBuilderCategory.Paintings or PromptBuilderCategory.Animation or PromptBuilderCategory.Ideas or PromptBuilderCategory.Icons or PromptBuilderCategory.Graphics => "\n\nThis is a retry. Keep every explicit core requirement from the brief and selected options, but create a clearly different art-directed interpretation through unrequested composition, palette, shape language, and finish.",
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
            RequiredContextTokens = requiredContextTokens,
            MaxOutputTokens = outputBudget,
            Temperature = profile.Temperature
        };
    }

    public string CleanResponse(string rawResponse) =>
        _responseCleaner.CleanResponse(rawResponse);

    internal static string HideReasoningFromStreamingPreview(string rawResponse) =>
        TextProcessingService.HideReasoningFromStreamingPreview(rawResponse);

    internal static bool IsSuitableForWritingModel(AiModelDescriptor model) =>
        TextProcessingService.IsSuitableForWritingModel(model);
}
