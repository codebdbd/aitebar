namespace AiteBar;

public enum PromptBuilderCategory
{
    Programming = 0,
    Images = 1,
    Texts = 2,
    Video = 3,
    Analysis = 4,
    Music = 5,
    Ideas = 6
}

public sealed class PromptBuilderService
{
    public const int MaxInputLength = 50_000;
    private const int ContextReservePercent = 15;
    private readonly TextProcessingService _responseCleaner = new();

    private const string ProgrammingInstruction = """
        Convert the user's brief into one complete, professional, ready-to-use prompt for an AI system that works with software development.

        Return only the finished prompt. Do not greet the user, explain your work, offer alternatives, ask questions, or continue a dialogue.

        Preserve the language of the user's brief unless the user explicitly requests another language.

        Silently determine the type of programming task: new development, modification of an existing project, debugging, code review, refactoring, architecture, testing, security analysis, performance optimization, deployment, documentation, or technical explanation.

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
        Convert the user's brief into one ready-to-use prompt for an image generation or image editing model.

        Return only the finished image prompt. Do not greet the user, explain your work, ask questions, offer alternatives, or continue a dialogue.

        Preserve the language of the user's brief unless the user explicitly requests another language.

        Do not include:
        - a role;
        - an objective section;
        - a task description;
        - headings;
        - bullet points;
        - workflow instructions;
        - acceptance criteria;
        - explanations;
        - instructions about how the model should think;
        - introductory phrases such as "You are an artist".

        Silently determine the visual task type: image generation, image editing, face or object replacement, logo, icon, poster, cover, advertising image, interface design, illustration, photography, or another visual task.

        For image generation, describe the visible result directly. Include only details that materially affect the image:
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
        - visible text;
        - aspect ratio.

        Arrange the description in a logical visual order: main subject, action, composition, environment, lighting, style, and constraints.

        Do not invent people, objects, text, symbols, brands, clothing, weapons, decorations, historical details, camera equipment, colors, visual effects, or artistic styles that were not requested or clearly implied.

        For image editing, state exactly:
        - what must be changed;
        - which reference image supplies each replacement element;
        - what must remain unchanged.

        When editing, preserve all unrelated elements, including identity, facial features, expression, pose, body proportions, clothing, composition, background, lighting, colors, and image dimensions unless the user explicitly requests a change.

        When the user supplies exact text that must appear in the image, reproduce it exactly without translation, correction, added punctuation, or alternative wording.

        For logos and icons, prioritize recognizability, simple geometry, clean silhouette, scalability, and readability at the target size. Do not add photorealistic details unless requested.

        Do not add generic filler such as "masterpiece", "award-winning", "best quality", "8K", "trending", or "ultra detailed" unless it is explicitly requested or materially necessary.

        Add negative constraints only when they prevent a likely error in the specific task. Do not generate a large generic negative prompt.

        If an essential visual detail is missing and the image cannot reasonably be created without it, use one concise square-bracket placeholder instead of asking a question.
        """;

    private const string TextsInstruction = """
        Convert the user's brief into one complete, ready-to-use prompt for an AI system that works with text.

        Return only the finished prompt. Do not greet the user, explain your work, offer alternatives, ask questions, or continue a dialogue.

        Preserve the language of the user's brief unless the user explicitly requests another language.

        Silently determine the task type: writing, rewriting, proofreading, grammar correction, translation, summarization, adaptation, shortening, expansion, formatting, document preparation, article writing, correspondence, social media content, script writing, or another text task.

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

        Return only the finished video prompt. Do not greet the user, explain your work, ask questions, offer alternatives, or continue a dialogue.

        Preserve the language of the user's brief unless the user explicitly requests another language.

        Do not include a role, headings, workflow, acceptance criteria, model instructions, or explanations.

        Silently determine the task type: text-to-video, image-to-video, video editing, animation, cinematic shot, advertising video, product demonstration, character animation, environmental animation, or a sequence of scenes.

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

        Silently determine the analytical task type: research, comparison, document analysis, data analysis, root-cause analysis, risk assessment, fact-checking, decision support, audit, forecasting, or evaluation.

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

        For comparisons, require common criteria and equivalent treatment of every option.

        For decision support, require trade-offs, risks, constraints, and the reasoning behind the recommendation.

        For document or data analysis, require conclusions to be traceable to the supplied material.

        For current or time-sensitive research, require recent sources and publication dates when the user requests external research.

        Do not force academic structure onto a simple practical analysis. Keep the prompt proportional to the task.

        If an essential fact is missing and cannot be inferred safely, use a concise square-bracket placeholder instead of asking a question.

        The result must be a prompt for performing the analysis, not the analysis itself.
        """;

    private const string IdeasInstruction = """
        Convert the user's brief into one complete, ready-to-use prompt for an AI system generating ideas, concepts, names, approaches, or possible solutions.

        Return only the finished prompt. Do not greet the user, explain your work, offer alternatives, ask questions, or continue a dialogue.

        Preserve the language of the user's brief unless the user explicitly requests another language.

        Silently determine the ideation task type: product ideas, software features, names, design concepts, content concepts, advertising ideas, business opportunities, technical approaches, story concepts, or problem-solving options.

        Preserve every explicit requirement and limitation from the user's brief.

        Include only relevant elements:
        - ideation goal;
        - problem being solved;
        - intended users or audience;
        - context;
        - constraints;
        - prohibited directions;
        - desired number of ideas;
        - required diversity;
        - expected level of originality;
        - available resources;
        - feasibility limits;
        - evaluation criteria;
        - output format.

        Do not automatically assign a role or create an academic research task.

        Require genuinely different ideas rather than minor variations of one concept.

        When appropriate, request a balanced set of:
        - practical ideas;
        - original ideas;
        - low-complexity ideas;
        - ambitious ideas.

        Do not force these groups when they are irrelevant to the user's request.

        When evaluation is useful, require each idea to include a concise explanation, main advantage, limitation, complexity, and suitable use case.

        When selection is useful, require the strongest candidates to be identified and explain briefly why they are stronger.

        Do not request unsupported market facts, financial projections, user statistics, legal claims, or implementation guarantees.

        Do not expand the task into unrelated industries, audiences, platforms, or technologies.

        If the user requests names, prioritize distinctiveness, pronunciation, meaning, relevance, and suitability for the stated language and audience. Do not invent claims about domain or trademark availability.

        If an essential constraint is missing and cannot be inferred safely, use a concise square-bracket placeholder instead of asking a question.

        The result must be a prompt for generating ideas, not the ideas themselves.
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
            PromptBuilderCategory.Ideas => IdeasInstruction,
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
                0.20),

            PromptBuilderCategory.Ideas => new(
                IdeasInstruction,
                1024,
                4096,
                0.35),

            _ => throw new ArgumentOutOfRangeException(nameof(category))
        };

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

        string normalizedBrief = brief.Trim();
        CategoryProfile profile = GetProfile(category);
        string systemPrompt = profile.SystemPrompt;

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
