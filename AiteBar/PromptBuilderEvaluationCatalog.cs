namespace AiteBar;

public sealed record PromptBuilderEvaluationScenario(
    string Id,
    string Title,
    PromptBuilderCategory Category,
    string Brief,
    IReadOnlyList<string> RequiredSystemPromptFragments,
    VisualTargetModel VisualTarget = VisualTargetModel.Universal,
    PhotoSection PhotoSection = PhotoSection.All,
    PaintingStyle PaintingStyle = PaintingStyle.Auto,
    AnimationStyle AnimationStyle = AnimationStyle.Auto,
    PhotoStyle PhotoStyle = PhotoStyle.Auto,
    TextPromptType TextType = TextPromptType.Auto,
    TextPromptTone TextTone = TextPromptTone.Neutral,
    AnalysisDirection AnalysisDirection = AnalysisDirection.Auto,
    VideoDirection VideoDirection = VideoDirection.Auto,
    ProgrammingProjectType ProgrammingProjectType = ProgrammingProjectType.Auto,
    ProgrammingPromptStyle ProgrammingStyle = ProgrammingPromptStyle.Auto);

public static class PromptBuilderEvaluationCatalog
{
    public static readonly IReadOnlyList<PromptBuilderEvaluationScenario> Scenarios =
    [
        new(
            "gpt-image-photo-natural-light",
            "GPT Image: natural portrait without HDR",
            PromptBuilderCategory.Images,
            "A young woman shaving her legs beside a creek at sunset",
            ["direct natural language", "Do not default to moody underexposure", "negative prompts"],
            VisualTarget: VisualTargetModel.GptImage,
            PhotoSection: PhotoSection.Portrait,
            PhotoStyle: PhotoStyle.EnvironmentalPortrait),
        new(
            "flux-product-materials",
            "FLUX: product photography with material detail",
            PromptBuilderCategory.Images,
            "A brushed steel watch on black volcanic stone",
            ["precise, visually specific natural-language description", "Luxury product photography", "empty quality tags"],
            VisualTarget: VisualTargetModel.Flux,
            PhotoSection: PhotoSection.Product,
            PhotoStyle: PhotoStyle.LuxuryProductPhoto),
        new(
            "nano-banana-edit-preservation",
            "Nano Banana: edit preserves unmentioned elements",
            PromptBuilderCategory.Images,
            "Replace the jacket with a red raincoat; preserve the person and the street",
            ["prioritizes the requested subject", "preserve the unmentioned identity", "negative prompts"],
            VisualTarget: VisualTargetModel.NanoBanana,
            PhotoSection: PhotoSection.StreetReportage,
            PhotoStyle: PhotoStyle.DocumentaryReportage),
        new(
            "painting-no-frame",
            "Painting: scene, not a framed object",
            PromptBuilderCategory.Paintings,
            "A woman reading under an apple tree",
            ["Impressionist oil painting", "not a photograph of an artwork", "Do not add a frame"],
            VisualTarget: VisualTargetModel.Flux,
            PaintingStyle: PaintingStyle.Impressionism),
        new(
            "programming-html-game",
            "Programming: HTML game prompt",
            PromptBuilderCategory.Programming,
            "A browser game where a small spaceship dodges asteroids and collects energy orbs",
            ["self-contained HTML browser game", "retro arcade game style", "gameplay loop"],
            ProgrammingProjectType: ProgrammingProjectType.HtmlGame,
            ProgrammingStyle: ProgrammingPromptStyle.RetroArcadeGame),
        new(
            "analytics-comparison",
            "Analytics: options comparison",
            PromptBuilderCategory.Analysis,
            "Choose between PostgreSQL and SQLite for an offline-first desktop application",
            ["a table with common criteria", "trade-offs and risks", "a concise conclusion"],
            AnalysisDirection: AnalysisDirection.Comparison),
        new(
            "text-business-email",
            "Text: concise business email",
            PromptBuilderCategory.Texts,
            "Ask a client to approve the revised project scope",
            ["Concise business email", "formal, professional, and respectful tone", "prohibit invented facts"],
            TextType: TextPromptType.BusinessEmail,
            TextTone: TextPromptTone.Formal),
        new(
            "video-product",
            "Video: controlled product film",
            PromptBuilderCategory.Video,
            "A perfume bottle slowly rotates on a wet black stone surface",
            ["Premium product film", "camera movement", "Avoid excessive motion"],
            VideoDirection: VideoDirection.ProductVideo),
        new(
            "music-suno-style",
            "Music: Suno style description",
            PromptBuilderCategory.Music,
            "uplifting late-night club track with female vocals",
            ["Suno Styles field", "Do not include:"])
    ];
}
