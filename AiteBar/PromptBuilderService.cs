namespace AiteBar;

public enum PromptBuilderCategory
{
    Programming = 0,
    Images = 1,
    Texts = 2,
    VideoAudio = 3,
    AnalysisIdeas = 4
}

public sealed class PromptBuilderService
{
    public const int MaxInputLength = 50_000;
    private const int ContextReservePercent = 15;
    private readonly TextProcessingService _responseCleaner = new();

    private const string BaseInstruction = """
        You are a senior prompt architect. Convert the user's brief into one complete, professional, ready-to-use prompt for another AI system.

        Return only the finished prompt. Do not greet the user, explain your work, offer alternatives, ask questions, or continue a dialogue. One request must always produce one final prompt.

        Preserve the language of the user's brief unless the user explicitly requests another language. Keep the result proportional to the complexity of the user's request. Do not add sections, requirements, or technical details that are unnecessary for completing the task.

        Infer only details that do not restrict the user's choices or materially change the requested result. If an essential fact is missing and cannot be inferred safely, use a concise square-bracket placeholder instead of asking a question. Do not invent names, credentials, measurements, sources, or business facts.

        Structure the finished prompt as needed with a clear role, objective, context, requirements, constraints, workflow, output format, and quality criteria. Include only sections that materially improve execution. Preserve every explicit requirement from the brief, resolve ambiguity conservatively, and make the prompt as ready to use as the available information allows.
        """;

    public string GetSystemPrompt(PromptBuilderCategory category) =>
        BaseInstruction + "\n\nCATEGORY GUIDANCE:\n" + (category switch
        {
            PromptBuilderCategory.Programming =>
                "Create prompts for software engineering: applications, websites, bots, scripts, debugging, code review, architecture, testing, security, and performance. Specify the target platform and stack when known, functional and non-functional requirements, interfaces, edge cases, error handling, acceptance criteria, and the expected form of code or technical explanation. Require production-quality, maintainable output and prohibit destructive assumptions.",
            PromptBuilderCategory.Images =>
                "Create prompts for image generation or editing: illustrations, photography, logos, posters, branding, interfaces, and design. Specify subject, composition, framing, perspective, visual hierarchy, style, lighting, palette, materials, mood, background, aspect ratio, fidelity, and negative constraints when relevant. For edits, explicitly preserve every element the user did not request to change.",
            PromptBuilderCategory.Texts =>
                "Create prompts for articles, letters, documents, translations, scripts, rewriting, and editorial work. Specify audience, purpose, tone, language, structure, length, terminology, factual boundaries, formatting, and quality checks. Preserve meaning and source facts when editing or translating, and prohibit fabricated citations or unsupported claims.",
            PromptBuilderCategory.VideoAudio =>
                "Create prompts for video, animation, music, voice-over, podcasts, sound design, and storyboards. First determine whether the task concerns video, animation, music, speech, podcasting, or sound design, and include only parameters relevant to the detected medium. Specify duration, format, structure, timing, style, pacing, technical delivery parameters, and continuity constraints only when they materially help that medium.",
            PromptBuilderCategory.AnalysisIdeas =>
                "Create prompts for research, comparison, learning, business problems, planning, decision support, and ideation. For analysis tasks, define the objective, scope, assumptions, evidence standards, method, evaluation criteria, risks, and actionable output; require uncertainty and missing evidence to be stated rather than invented. For ideation tasks, request diverse and original options within the user's constraints, evaluate feasibility and trade-offs, and identify the strongest candidates without forcing academic analysis or unsupported facts.",
            _ => throw new ArgumentOutOfRangeException(nameof(category))
        });

    public AiChatRequest BuildRequest(
        PromptBuilderCategory category,
        string brief,
        int? maxOutputTokens = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(brief);
        if (brief.Length > MaxInputLength)
        {
            throw new ArgumentOutOfRangeException(
                nameof(brief),
                brief.Length,
                $"The brief cannot exceed {MaxInputLength} characters.");
        }

        string systemPrompt = GetSystemPrompt(category);
        int inputTokens = TextProcessingService.EstimateTokens(systemPrompt) +
            TextProcessingService.EstimateTokens(brief);
        int outputBudget = Math.Clamp(Math.Max(2048, inputTokens * 2), 2048, 8192);
        if (maxOutputTokens.HasValue)
        {
            outputBudget = Math.Min(outputBudget, Math.Max(1, maxOutputTokens.Value));
        }

        int requiredContextTokens = inputTokens + outputBudget;
        requiredContextTokens += (int)Math.Ceiling(requiredContextTokens * (ContextReservePercent / 100.0));

        return new AiChatRequest
        {
            Messages =
            [
                new AiChatMessage("system", systemPrompt),
                new AiChatMessage("user", brief.Trim())
            ],
            RequiredCapabilities = AiCapabilities.Text,
            RequireFreeModel = true,
            RequireWritingModel = true,
            RequiredContextTokens = requiredContextTokens,
            MaxOutputTokens = outputBudget,
            Temperature = 0.25
        };
    }

    public string CleanResponse(string rawResponse) =>
        _responseCleaner.CleanResponse(rawResponse);

    internal static string HideReasoningFromStreamingPreview(string rawResponse) =>
        TextProcessingService.HideReasoningFromStreamingPreview(rawResponse);

    internal static bool IsSuitableForWritingModel(AiModelDescriptor model) =>
        TextProcessingService.IsSuitableForWritingModel(model);
}
