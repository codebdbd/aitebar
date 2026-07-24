using System;
using System.IO;
using System.Text.RegularExpressions;

namespace AiteBar;

public sealed partial class TextProcessingService
{
    public const int MaxInputLength = 50_000;
    public const int ContextReservePercent = 15;
    public const double LatinCharsPerToken = 4.0;
    public const double CyrillicCharsPerToken = 2.8;
    public const double MixedCharsPerToken = 3.5;

    public string GetSystemPrompt(TextProcessingMode mode) => mode switch
    {
        TextProcessingMode.Proofread => ProofreadPrompt,
        TextProcessingMode.Typography => TypographyPrompt,
        TextProcessingMode.Cleanup => CleanupPrompt,
        _ => throw new ArgumentOutOfRangeException(nameof(mode))
    };

    public AiChatRequest BuildRequest(TextProcessingMode mode, string text, int? maxOutputTokens = null)
    {
        string systemPrompt = GetSystemPrompt(mode) + ProtectedMarkerInstruction;
        int estimated = EstimateTokens(systemPrompt) + EstimateTokens(text);
        int outputBudget = Math.Max(estimated, text.Length / 2);
        if (maxOutputTokens.HasValue)
        {
            outputBudget = Math.Min(outputBudget, maxOutputTokens.Value);
        }

        outputBudget = Math.Clamp(outputBudget, 1024, 32768);
        int requiredContextTokens = estimated + outputBudget;
        requiredContextTokens += (int)Math.Ceiling(requiredContextTokens * (ContextReservePercent / 100.0));

        return new AiChatRequest
        {
            Messages =
            [
                new AiChatMessage("system", systemPrompt),
                new AiChatMessage("user", text)
            ],
            RequiredCapabilities = AiCapabilities.Text,
            RequireFreeModel = true,
            RequireWritingModel = true,
            RequiredContextTokens = requiredContextTokens,
            MaxOutputTokens = outputBudget,
            Temperature = 0.3
        };
    }

    public string CleanResponse(string rawResponse)
    {
        if (string.IsNullOrWhiteSpace(rawResponse))
        {
            return string.Empty;
        }

        return rawResponse.Trim();
    }

    public static int EstimateTokens(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0;
        }

        int cyrillicLetters = 0;
        int latinLetters = 0;
        foreach (char character in text)
        {
            if (character is >= '\u0400' and <= '\u052F')
            {
                cyrillicLetters++;
            }
            else if (character is >= 'A' and <= 'Z' or >= 'a' and <= 'z')
            {
                latinLetters++;
            }
        }

        double charsPerToken = (cyrillicLetters, latinLetters) switch
        {
            (> 0, 0) => CyrillicCharsPerToken,
            (0, > 0) => LatinCharsPerToken,
            (> 0, > 0) => MixedCharsPerToken,
            _ => LatinCharsPerToken
        };
        return (int)Math.Ceiling(text.Length / charsPerToken);
    }

    public ProtectedText ProtectTechnicalFragments(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return new ProtectedText(
                text ?? string.Empty,
                new Dictionary<string, string>(StringComparer.Ordinal));
        }

        string markerPrefix;
        do
        {
            markerPrefix = $"__AITEBAR_PROTECTED_{Guid.NewGuid():N}_";
        }
        while (text.Contains(markerPrefix, StringComparison.Ordinal));

        var fragments = new Dictionary<string, string>(StringComparer.Ordinal);
        string protectedValue = TechnicalFragmentRegex().Replace(text, match =>
        {
            string marker = $"{markerPrefix}{fragments.Count:D4}__";
            fragments.Add(marker, match.Value);
            return marker;
        });
        return new ProtectedText(protectedValue, fragments);
    }

    public static string RestoreTechnicalFragments(
        string text,
        ProtectedText protectedText,
        bool requireAllMarkers = false)
    {
        string restored = text ?? string.Empty;
        foreach ((string marker, string value) in protectedText.Fragments)
        {
            if (requireAllMarkers)
            {
                int firstIndex = restored.IndexOf(marker, StringComparison.Ordinal);
                if (firstIndex < 0 ||
                    restored.IndexOf(marker, firstIndex + marker.Length, StringComparison.Ordinal) >= 0)
                {
                    throw new InvalidDataException(
                        "The AI response removed or duplicated a protected technical fragment marker.");
                }
            }
            restored = restored.Replace(marker, value, StringComparison.Ordinal);
        }
        return restored;
    }

    internal static bool IsSuitableForWritingModel(AiModelDescriptor model)
    {
        string searchable = $"{model.ModelId} {model.DisplayName}".ToLowerInvariant();
        string[] excludedTerms =
        [
            "whisper", "speech", "audio", "transcrib", "tts",
            "embedding", "rerank", "moderation", "prompt-guard", "prompt guard",
            "safety gpt",
            "nano banana", "imagen", "veo",
            "image generation", "image-generation", "image_generation",
            "image preview", "image-preview", "image_preview",
            "generate-image", "generate_image", "generate image",
            "video generation", "video-generation", "video_generation",
            "generate-video", "generate_video", "generate video"
        ];
        if (excludedTerms.Any(searchable.Contains))
        {
            return false;
        }

        string modelId = model.ModelId.ToLowerInvariant();
        return !modelId.EndsWith("-image", StringComparison.Ordinal) &&
               !modelId.EndsWith("/image", StringComparison.Ordinal) &&
               !modelId.EndsWith("-video", StringComparison.Ordinal) &&
               !modelId.EndsWith("/video", StringComparison.Ordinal);
    }

    [GeneratedRegex(
        """
        ```[\s\S]*?```|`[^`\r\n]+`|https?://[^\s<>"']+|www\.[^\s<>"']+|[\w.!#$%&'*+/=?^`{|}~-]+@[\w-]+(?:\.[\w-]+)+|(?:[A-Za-z]:\\|\\\\)[^\s<>:"|?*]+|(?<!\w)/(?:[^\s/]+/)*[^\s/]+|</?[A-Za-z][^>\r\n]*>|\$\{[^}\r\n]+\}|\{\{[^}\r\n]+\}\}|%[A-Za-z_][A-Za-z0-9_]*%|\b[0-9a-fA-F]{8}(?:-[0-9a-fA-F]{4}){3}-[0-9a-fA-F]{12}\b|\bv?\d+(?:\.\d+){1,3}(?:[-+][A-Za-z0-9.-]+)?\b
        """,
        RegexOptions.CultureInvariant)]
    private static partial Regex TechnicalFragmentRegex();

    private const string ProtectedMarkerInstruction =
        """

        Фрагменты вида __AITEBAR_PROTECTED_...__ являются служебными маркерами защищённого текста.
        Копируй каждый такой маркер в ответ ровно один раз, без любых изменений, пробелов или перестановки символов.
        """;

    private const string ProofreadPrompt =
        """
        Ты выполняешь только проверку орфографии, грамматики и пунктуации.

        Исправь орфографические и грамматические ошибки, согласование, окончания, очевидные опечатки, регистр и пунктуацию.

        Строго запрещено:
        - перефразировать текст;
        - заменять слова синонимами;
        - улучшать стиль;
        - сокращать или дополнять текст;
        - менять порядок предложений;
        - менять структуру абзацев;
        - создавать новые заголовки или списки;
        - переводить текст;
        - выполнять типографическую обработку.

        Сохраняй исходный тип символов:
        - обычные кавычки " не заменяй на типографские;
        - обычный апостроф ' не заменяй;
        - дефис или минус - не заменяй на короткое или длинное тире;
        - три точки ... не заменяй знаком многоточия;
        - обычные пробелы не заменяй неразрывными.

        Не изменяй URL, адреса электронной почты, пути к файлам, имена файлов, команды, программный код, теги, переменные, номера версий, артикулы и идентификаторы, кроме очевидной пунктуации вокруг них.

        Если текст содержит несколько языков, проверяй каждый фрагмент на его языке и ничего не переводи.

        Верни только исправленный текст. Не добавляй пояснений, заголовков, списка исправлений, Markdown-обёртки или фраз вроде "Готово" и "Исправленный текст".
        """;

    private const string TypographyPrompt =
        """
        Ты выполняешь только типографическое оформление текста.

        Определи язык каждого содержательного фрагмента и примени принятые для него типографские правила:
        - правильные внешние и вложенные кавычки;
        - дефис, короткое и длинное тире по назначению;
        - знак многоточия;
        - корректные пробелы возле знаков препинания;
        - неразрывные пробелы;
        - оформление диапазонов;
        - оформление инициалов и сокращений;
        - оформление процентов, валют, градусов и единиц измерения;
        - удаление повторяющихся и лишних пробелов.

        Строго запрещено:
        - менять слова и формулировки;
        - исправлять стиль;
        - перефразировать;
        - сокращать или дополнять текст;
        - менять порядок предложений;
        - перестраивать абзацы;
        - переводить;
        - изменять смысл.

        Не изменяй содержимое URL, адресов электронной почты, путей к файлам, имён файлов, команд, программного кода, HTML/XML-тегов, Markdown-разметки, переменных, шаблонов, номеров версий, артикулов и идентификаторов.

        Если текст смешанный, применяй правила к каждому языковому фрагменту отдельно. Не типографируй технические фрагменты.

        Верни только оформленный текст. Не добавляй пояснений, заголовков, списка изменений, Markdown-обёртки или фраз вроде "Готово" и "Оформленный текст".
        """;

    private const string CleanupPrompt =
        """
        Ты выполняешь только очистку текста от технических артефактов копирования.

        Разрешено:
        - объединять строки, случайно разорванные внутри предложения;
        - восстанавливать слова, разорванные переносом строки;
        - удалять повторяющиеся пробелы;
        - удалять лишние пустые строки;
        - удалять невидимые и служебные символы;
        - удалять очевидные номера страниц;
        - удалять повторяющиеся колонтитулы;
        - удалять явные артефакты копирования;
        - восстанавливать реальные границы абзацев;
        - сохранять настоящие списки и структуру документа.

        Строго запрещено:
        - исправлять орфографию;
        - исправлять грамматику;
        - менять пунктуацию;
        - выполнять типографические замены;
        - перефразировать;
        - сокращать или дополнять;
        - переводить;
        - удалять содержательный текст;
        - менять смысл.

        Не изменяй URL, адреса электронной почты, пути, имена файлов, команды, программный код, теги, переменные, номера версий, артикулы и идентификаторы.

        Если нет уверенности, что фрагмент является техническим мусором, сохрани его.

        Верни только очищенный текст. Не добавляй пояснений, заголовков, списка удалённых элементов, Markdown-обёртки или фраз вроде "Готово" и "Очищенный текст".
        """;
}

public sealed record ProtectedText(
    string Text,
    IReadOnlyDictionary<string, string> Fragments);
