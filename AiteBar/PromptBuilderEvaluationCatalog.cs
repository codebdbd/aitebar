namespace AiteBar;

public sealed record PromptBuilderEvaluationScenario(
    string Id,
    string Title,
    PromptBuilderCategory Category,
    string Brief,
    IReadOnlyList<string> RequiredSystemPromptFragments,
    VisualTargetModel VisualTarget = VisualTargetModel.Universal,
    PaintingStyle PaintingStyle = PaintingStyle.Auto,
    AnimationStyle AnimationStyle = AnimationStyle.Auto,
    PhotoStyle PhotoStyle = PhotoStyle.Auto,
    TextPromptType TextType = TextPromptType.Auto,
    TextPromptTone TextTone = TextPromptTone.Neutral,
    AnalysisDirection AnalysisDirection = AnalysisDirection.Auto,
    VideoDirection VideoDirection = VideoDirection.Auto,
    ProgrammingTaskType ProgrammingTaskType = ProgrammingTaskType.Auto);

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
            PhotoStyle: PhotoStyle.Portrait),
        new(
            "flux-product-materials",
            "FLUX: product photography with material detail",
            PromptBuilderCategory.Images,
            "A brushed steel watch on black volcanic stone",
            ["precise, visually specific natural-language description", "Premium product photography", "empty quality tags"],
            VisualTarget: VisualTargetModel.Flux,
            PhotoStyle: PhotoStyle.Product),
        new(
            "nano-banana-edit-preservation",
            "Nano Banana: edit preserves unmentioned elements",
            PromptBuilderCategory.Images,
            "Replace the jacket with a red raincoat; preserve the person and the street",
            ["prioritizes the requested subject", "preserve the unmentioned identity", "negative prompts"],
            VisualTarget: VisualTargetModel.NanoBanana,
            PhotoStyle: PhotoStyle.Documentary),
        new(
            "painting-no-frame",
            "Painting: scene, not a framed object",
            PromptBuilderCategory.Paintings,
            "A woman reading under an apple tree",
            ["Impressionist oil painting", "not a photograph of an artwork", "Do not add a frame"],
            VisualTarget: VisualTargetModel.Flux,
            PaintingStyle: PaintingStyle.Impressionism),
        new(
            "programming-bugfix",
            "Programming: reproducible bug fix",
            PromptBuilderCategory.Programming,
            "Login sometimes fails after the application wakes from sleep",
            ["Require reproducible steps", "root-cause analysis", "regression coverage"],
            ProgrammingTaskType: ProgrammingTaskType.BugFix),
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
