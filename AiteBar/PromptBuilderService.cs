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
    PencilDrawing
}

public enum PhotoStyle
{
    Auto, Architecture, BeautyPortrait, VintageFilm, Documentary, Food, Cinematic,
    Commercial, Macro, FashionEditorial, NeonNight, Landscape, Portrait, Product,
    Studio, Street, BlackAndWhite
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
    ComicAmerican,
    ComicEuropean,
    ComicManga,
    StopMotion,
    Claymation
}

public enum IconPlatform { Auto, MacOS, IOS, Windows11, AndroidMaterialYou, CrossPlatform }

public enum IconStyle
{
    Auto, Flat, GradientFlat, Monochrome, Line, Glyph, Filled, Duotone, Isometric,
    Glassmorphism, Neumorphism, ThreeDimensional, ClayThreeDimensional, PixelArt,
    Retro, HandDrawn, Mascot
}

public enum GraphicType { Auto, Sticker, StickerPack, Logo, UiElement, VectorIllustration, Poster, Banner, Infographic }

public enum GraphicStyle { Auto, Flat, Gradient, Minimal, Bold, LineArt, Duotone, Isometric, ThreeDimensional, HandDrawn, PixelArt, Retro, Editorial }

public sealed record PaintingStyleDefinition(PaintingStyle Style, string LocalizationKey, string PromptDescriptor);
public sealed record PhotoStyleDefinition(PhotoStyle Style, string LocalizationKey, string PromptDescriptor);
public sealed record TextPromptTypeDefinition(TextPromptType Type, string LocalizationKey, string PromptDescriptor);
public sealed record TextPromptToneDefinition(TextPromptTone Tone, string LocalizationKey, string PromptDescriptor);
public sealed record AnimationStyleDefinition(AnimationStyle Style, string LocalizationKey, string PromptDescriptor);
public sealed record AnalysisDirectionDefinition(AnalysisDirection Direction, string LocalizationKey, string OutcomeLocalizationKey, string PromptDescriptor);
public sealed record VideoDirectionDefinition(VideoDirection Direction, string LocalizationKey, string PromptDescriptor);
public sealed record ProgrammingTaskTypeDefinition(ProgrammingTaskType Type, string LocalizationKey, string PromptDescriptor);
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
        new(PaintingStyle.PencilDrawing, "PaintingStyle_PencilDrawing", "Masterful graphite pencil drawing, precise tonal modelling, expressive line weight, visible paper grain, refined hatching and cross-hatching.")
    ];

    public static readonly IReadOnlyList<PhotoStyleDefinition> PhotoStyles =
    [
        new(PhotoStyle.Auto, "PhotoStyle_Auto", "Naturalistic premium commercial photography with accurate skin tones, balanced exposure, realistic dynamic range, and detailed shadows and highlights."),
        new(PhotoStyle.Architecture, "PhotoStyle_Architecture", "Architectural photography, precise verticals, considered wide-angle perspective, clean geometry, realistic daylight."),
        new(PhotoStyle.BeautyPortrait, "PhotoStyle_BeautyPortrait", "Beauty portrait photography, flattering soft key light, refined skin texture, elegant close framing, clean color."),
        new(PhotoStyle.VintageFilm, "PhotoStyle_VintageFilm", "Authentic vintage analog film photograph, subtle grain, gentle halation, restrained period color, imperfect tactile realism."),
        new(PhotoStyle.Documentary, "PhotoStyle_Documentary", "Observational documentary photography, available natural light, candid decisive moment, unforced real-world texture."),
        new(PhotoStyle.Food, "PhotoStyle_Food", "Premium food and beverage photography, appetizing natural texture, controlled highlights, carefully styled tabletop composition."),
        new(PhotoStyle.Cinematic, "PhotoStyle_Cinematic", "Cinematic still photography, intentional dramatic composition, motivated film lighting, restrained color grade, preserved shadow detail."),
        new(PhotoStyle.Commercial, "PhotoStyle_Commercial", "High-end commercial advertising photography, clear visual hierarchy, polished art direction, clean premium finish."),
        new(PhotoStyle.Macro, "PhotoStyle_Macro", "Macro photography, extreme close focus, intricate tactile detail, shallow depth of field, precise controlled light."),
        new(PhotoStyle.FashionEditorial, "PhotoStyle_FashionEditorial", "Fashion editorial photography, confident styling, sophisticated pose, magazine-quality composition, refined directional light."),
        new(PhotoStyle.NeonNight, "PhotoStyle_NeonNight", "Night photography with neon practical lighting, rich but controlled color, realistic reflections, preserved facial detail."),
        new(PhotoStyle.Landscape, "PhotoStyle_Landscape", "Landscape photography, expansive natural depth, atmospheric perspective, balanced sky and foreground exposure."),
        new(PhotoStyle.Portrait, "PhotoStyle_Portrait", "Natural environmental portrait photography, expressive genuine presence, flattering realistic light, balanced background separation."),
        new(PhotoStyle.Product, "PhotoStyle_Product", "Premium product photography, precise controlled reflections, tactile material definition, uncluttered intentional set design."),
        new(PhotoStyle.Studio, "PhotoStyle_Studio", "Professional studio photography, deliberate multi-light setup, clean background, accurate color and controlled contrast."),
        new(PhotoStyle.Street, "PhotoStyle_Street", "Street photography, candid human moment, authentic urban context, natural available light, decisive composition."),
        new(PhotoStyle.BlackAndWhite, "PhotoStyle_BlackAndWhite", "Fine-art black-and-white photography, rich monochrome tonal scale, sculpted light, detailed highlights and shadows.")
    ];

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
        new(GraphicType.Sticker, "GraphicType_Sticker", "a single sticker with a clear silhouette, bold contour, and transparent or simple background as appropriate."),
        new(GraphicType.StickerPack, "GraphicType_StickerPack", "a cohesive sticker pack with distinct readable stickers, shared palette, and consistent line treatment."),
        new(GraphicType.Logo, "GraphicType_Logo", "a scalable logo mark with simple geometry, memorable silhouette, and no invented lettering."),
        new(GraphicType.UiElement, "GraphicType_UiElement", "a production-ready UI graphic element with clear hierarchy and functional readability."),
        new(GraphicType.VectorIllustration, "GraphicType_VectorIllustration", "a clean vector-style illustration with intentional shapes, paths, hierarchy, and restrained detail."),
        new(GraphicType.Poster, "GraphicType_Poster", "a poster composition with clear hierarchy, focal point, and reserved text space only when requested."),
        new(GraphicType.Banner, "GraphicType_Banner", "a wide banner with a clear focal area and balanced space for any user-supplied copy."),
        new(GraphicType.Infographic, "GraphicType_Infographic", "an infographic layout with a clear visual hierarchy; use only user-supplied text and reserve labeled areas rather than inventing claims."),
    ];

    public static readonly IReadOnlyList<GraphicStyleDefinition> GraphicStyles =
    [
        new(GraphicStyle.Auto, "GraphicStyle_Auto", "Choose the clearest visual style for the requested asset."),
        new(GraphicStyle.Flat, "GraphicStyle_Flat", "flat graphic design with simple geometry and a focused palette."),
        new(GraphicStyle.Gradient, "GraphicStyle_Gradient", "modern controlled gradients with clear hierarchy and no muddy color mixing."),
        new(GraphicStyle.Minimal, "GraphicStyle_Minimal", "minimal graphic design with generous negative space and only essential detail."),
        new(GraphicStyle.Bold, "GraphicStyle_Bold", "bold graphic design with strong contrast and confident shape language."),
        new(GraphicStyle.LineArt, "GraphicStyle_LineArt", "clean line art with consistent stroke logic and readable contours."),
        new(GraphicStyle.Duotone, "GraphicStyle_Duotone", "disciplined duotone graphic design with a limited palette."),
        new(GraphicStyle.Isometric, "GraphicStyle_Isometric", "clean isometric graphic design with consistent perspective."),
        new(GraphicStyle.ThreeDimensional, "GraphicStyle_ThreeDimensional", "polished three-dimensional graphic design with simple controlled forms."),
        new(GraphicStyle.HandDrawn, "GraphicStyle_HandDrawn", "intentional hand-drawn graphic style with clean reproducible contours."),
        new(GraphicStyle.PixelArt, "GraphicStyle_PixelArt", "crisp pixel-art graphic style with a deliberate grid and limited palette."),
        new(GraphicStyle.Retro, "GraphicStyle_Retro", "original retro graphic design without copying a specific brand or artist."),
        new(GraphicStyle.Editorial, "GraphicStyle_Editorial", "refined editorial graphic design with strong composition and restrained accents."),
    ];

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
        new(AnimationStyle.ComicAmerican, "AnimationStyle_ComicAmerican", "American superhero comic art, bold ink contours, dramatic perspective, saturated print color, halftone texture."),
        new(AnimationStyle.ComicEuropean, "AnimationStyle_ComicEuropean", "European bande dessinee comic art, clean precise linework, sophisticated color, detailed cinematic environments."),
        new(AnimationStyle.ComicManga, "AnimationStyle_ComicManga", "Black-and-white manga art, expressive screentones, kinetic framing, precise ink linework, controlled contrast."),
        new(AnimationStyle.StopMotion, "AnimationStyle_StopMotion", "Handcrafted stop-motion animation, miniature sets, tactile practical materials, charming frame-by-frame character design."),
        new(AnimationStyle.Claymation, "AnimationStyle_Claymation", "Claymation animation, hand-molded clay characters, soft studio light, tactile fingerprints and handcrafted miniature scenery.")
    ];

    private const string ProgrammingInstruction = """
        Convert the user's brief into one complete, professional, ready-to-use prompt for an AI system that works with software development.

        Return only the finished prompt. Do not greet the user, explain your work, offer alternatives, ask questions, or continue a dialogue.

        Preserve the language of the user's brief unless the user explicitly requests another language.

        Apply this selected programming task type: {programmingTaskType}

        Preserve every explicit requirement from the brief. Do not invent a programming language, framework, platform, architecture, database, API, library, file structure, business rule, credential, measurement, or external dependency that the user did not provide or clearly imply.

        Do not add a decorative role such as "You are a senior developer" unless the user explicitly requests a role or a specific area of expertise is essential to the task.

        For new development, describe only the relevant requirements:
        - the intended result;
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

        For changes to an existing project, require the AI to analyze the supplied code before making changes, preserve the current architecture and existing behavior unless a change is explicitly requested, avoid unrelated refactoring, and clearly identify every modified file.

        For debugging, include the observed behavior, expected behavior, known error information, reproduction conditions, and the required form of the fix when these details are available.

        For code review, define the review scope and request concrete findings with locations, consequences, priorities, and corrected solutions. Do not request speculative problems without evidence in the supplied code.

        For security-related tasks, prohibit destructive actions, unsafe assumptions, hidden backdoors, credential exposure, and weakening of existing protections.

        Use headings, lists, code blocks, workflow steps, and acceptance criteria only when they improve execution of the specific task. Keep the prompt proportional to the complexity of the user's brief.

        If an essential fact is missing and cannot be inferred safely, insert a concise square-bracket placeholder such as [target platform] or [path to the project] instead of asking a question.

        The result must be a prompt for performing the programming task, not the implementation of the task itself.
        """;

    private const string ImagesInstruction = """
        Turn the user's brief into one polished English prompt for Flux, GPT-Image, Nano Banana, or another modern image generation or editing model.

        Return only one finished prompt as a natural-language paragraph. Never add headings, lists, a negative-prompt section, placeholders, explanations, questions, role-play, or instructions about reasoning.

        Preserve the user's non-negotiable core: the requested subject, action, objects, setting, reference-image instructions, and explicit style. Translate the working prompt into fluent English.

        Never introduce text into the prompt: do not invent a phrase, sentence, caption, label, slogan, quote, lettering, or a "text" clause. Mention visible text only when the user explicitly supplied the exact wording; reproduce only that wording in quotation marks.

        Act as an expert art director. Expand a short or rough idea into a complete, specific, visually compelling scene. Confidently add harmonious details that make the image feel intentional: a fitting subject appearance, wardrobe, gesture, composition, environment, time of day, lighting, palette, materials, atmosphere, camera framing, lens perspective, and surface detail. Do not invent named brands, celebrities, factual claims, copyrighted characters, or visible wording that the user did not request.

        Apply this selected photo direction: {photoStyle}
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
        Apply this target-model profile: {visualTarget}

        Keep the result unmistakably painterly. Respect an explicitly requested medium, period, or artist direction when it conflicts with the selected default. Select 4:5 for portrait-oriented scenes, 16:9 for broad narratives, and 1:1 for a balanced composition unless the user specifies another ratio.

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

        Produce the requested graphic asset itself, not a photograph of it or a scene containing it. Use the format best suited to the asset: 1:1 for stickers, logos, and UI elements; 16:9 for banners; 4:5 for posters; otherwise infer from the brief. Prioritize clear hierarchy, scalable shape design, and practical readability.
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
        Convert the user's brief into one finished English musical style description for the Styles field in Suno.

        Return only the finished style description in English.

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

        Write one compact natural-language paragraph that directly describes the desired music, similar to a detailed Suno style enhancement.

        The description may include only relevant musical characteristics:
        - primary genre;
        - subgenre or genre blend;
        - mood;
        - emotional character;
        - tempo;
        - rhythmic feel;
        - groove;
        - time signature when important;
        - instrumentation;
        - vocal type;
        - vocal delivery;
        - arrangement;
        - dynamics;
        - song development;
        - production style;
        - sound texture;
        - atmosphere;
        - period influence.

        Do not mechanically include every parameter. Include only details that strengthen the musical direction.

        Preserve the user's explicit genre, mood, instrumentation, vocal requirements, tempo, period, and production preferences.

        Infer compatible supporting musical details only when they help turn an incomplete idea into a coherent style description. Do not change the core genre or mood.

        Do not add lyrics or describe what the song is about unless the user explicitly states that this subject must influence the musical character.

        Do not add artist names. When the user references a performer, band, composer, or producer, translate the reference into general musical characteristics such as genre, instrumentation, vocal delivery, rhythm, arrangement, production, and atmosphere.

        Do not use visual terminology, camera terminology, storytelling instructions, or technical specifications unrelated to music.

        Avoid empty promotional words such as "masterpiece", "award-winning", "viral", "perfect", or "best quality".

        The result must contain only the style description that can be pasted directly into the Suno Styles field.
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
            PromptBuilderCategory.Analysis or PromptBuilderCategory.Ideas => AnalysisInstruction,
            PromptBuilderCategory.Paintings => PaintingsInstruction,
            PromptBuilderCategory.Animation => AnimationInstruction,
            PromptBuilderCategory.Icons => IconsInstruction,
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
                IconsInstruction,
                512,
                2048,
                0.35),

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

            PromptBuilderCategory.Analysis or PromptBuilderCategory.Ideas => new(
                AnalysisInstruction,
                2048,
                8192,
                0.25),

            _ => throw new ArgumentOutOfRangeException(nameof(category))
        };

    public AiChatRequest BuildRequest(
        PromptBuilderCategory category,
        string brief,
        int? maxOutputTokens = null,
        bool createAlternative = false,
        PaintingStyle paintingStyle = PaintingStyle.Auto,
        AnimationStyle animationStyle = AnimationStyle.Auto,
        PhotoStyle photoStyle = PhotoStyle.Auto,
        TextPromptType textType = TextPromptType.Auto,
        TextPromptTone textTone = TextPromptTone.Neutral,
        AnalysisDirection analysisDirection = AnalysisDirection.Auto,
        VideoDirection videoDirection = VideoDirection.Auto,
        ProgrammingTaskType programmingTaskType = ProgrammingTaskType.Auto,
        VisualTargetModel visualTarget = VisualTargetModel.Universal,
        IconPlatform iconPlatform = IconPlatform.Auto,
        IconStyle iconStyle = IconStyle.Auto,
        GraphicType graphicType = GraphicType.Auto,
        GraphicStyle graphicStyle = GraphicStyle.Auto)
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
            PhotoStyleDefinition style = PhotoStyles.FirstOrDefault(item => item.Style == photoStyle) ?? PhotoStyles[0];
            systemPrompt = systemPrompt.Replace("{photoStyle}", style.PromptDescriptor, StringComparison.Ordinal);
        }
        if (category == PromptBuilderCategory.Texts)
        {
            TextPromptTypeDefinition type = TextPromptTypes.FirstOrDefault(item => item.Type == textType) ?? TextPromptTypes[0];
            TextPromptToneDefinition tone = TextPromptTones.FirstOrDefault(item => item.Tone == textTone) ?? TextPromptTones[0];
            systemPrompt = systemPrompt.Replace("{textType}", type.PromptDescriptor, StringComparison.Ordinal)
                .Replace("{textTone}", tone.PromptDescriptor, StringComparison.Ordinal);
        }
        if (category is PromptBuilderCategory.Analysis or PromptBuilderCategory.Ideas)
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
            ProgrammingTaskTypeDefinition type = ProgrammingTaskTypes.FirstOrDefault(item => item.Type == programmingTaskType) ?? ProgrammingTaskTypes[0];
            systemPrompt = systemPrompt.Replace("{programmingTaskType}", type.PromptDescriptor, StringComparison.Ordinal);
        }
        if (category is PromptBuilderCategory.Images or PromptBuilderCategory.Paintings or PromptBuilderCategory.Animation or PromptBuilderCategory.Icons or PromptBuilderCategory.Graphics)
        {
            VisualTargetModelDefinition target = VisualTargetModels.FirstOrDefault(item => item.Model == visualTarget) ?? VisualTargetModels[0];
            systemPrompt = systemPrompt.Replace("{visualTarget}", target.PromptDescriptor, StringComparison.Ordinal);
        }
        if (category == PromptBuilderCategory.Paintings)
        {
            PaintingStyleDefinition style = PaintingStyles.FirstOrDefault(item => item.Style == paintingStyle) ?? PaintingStyles[0];
            systemPrompt = systemPrompt.Replace("{paintingStyle}", style.PromptDescriptor, StringComparison.Ordinal);
        }
        if (category == PromptBuilderCategory.Animation)
        {
            AnimationStyleDefinition style = AnimationStyles.FirstOrDefault(item => item.Style == animationStyle) ?? AnimationStyles[0];
            systemPrompt = systemPrompt.Replace("{animationStyle}", style.PromptDescriptor, StringComparison.Ordinal);
        }
        if (category == PromptBuilderCategory.Icons)
        {
            IconPlatformDefinition platform = IconPlatforms.FirstOrDefault(item => item.Platform == iconPlatform) ?? IconPlatforms[0];
            IconStyleDefinition style = IconStyles.FirstOrDefault(item => item.Style == iconStyle) ?? IconStyles[0];
            systemPrompt = systemPrompt.Replace("{iconPlatform}", platform.PromptDescriptor, StringComparison.Ordinal)
                .Replace("{iconStyle}", style.PromptDescriptor, StringComparison.Ordinal);
        }
        if (category == PromptBuilderCategory.Graphics)
        {
            GraphicTypeDefinition type = GraphicTypes.FirstOrDefault(item => item.Type == graphicType) ?? GraphicTypes[0];
            GraphicStyleDefinition style = GraphicStyles.FirstOrDefault(item => item.Style == graphicStyle) ?? GraphicStyles[0];
            systemPrompt = systemPrompt.Replace("{graphicType}", type.PromptDescriptor, StringComparison.Ordinal)
                .Replace("{graphicStyle}", style.PromptDescriptor, StringComparison.Ordinal);
        }
        if (createAlternative)
        {
            systemPrompt += category switch
            {
                PromptBuilderCategory.Images or PromptBuilderCategory.Paintings or PromptBuilderCategory.Animation or PromptBuilderCategory.Icons or PromptBuilderCategory.Graphics => "\n\nThis is a retry. Keep every explicit core requirement from the brief and selected options, but create a clearly different art-directed interpretation through unrequested composition, palette, shape language, and finish.",
                PromptBuilderCategory.Video => "\n\nThis is a retry. Keep every explicit core requirement from the brief and the selected video direction, but create a distinct treatment through unrequested shot design, camera path, pacing, lighting, and scene progression.",
                PromptBuilderCategory.Programming => "\n\nThis is an alternative prompt version. Preserve the task, supplied technical facts, and selected task type, but use a different clear execution structure and verification approach. Do not invent a stack, architecture, or dependencies.",
                PromptBuilderCategory.Analysis or PromptBuilderCategory.Ideas => "\n\nThis is an alternative prompt version. Preserve the brief and selected output contract, but vary the unrequested criteria, hypotheses, evidence plan, or prioritization so the downstream analysis offers a genuinely useful second perspective.",
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
