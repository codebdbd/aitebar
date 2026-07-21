using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace AiteBar;

public sealed partial class TextProcessingService
{
    public const int MaxInputLength = 50_000;
    public const int ContextReservePercent = 15;
    public const double CharsPerToken = 4.0;

    public string GetSystemPrompt(TextProcessingMode mode) => mode switch
    {
        TextProcessingMode.Proofread => ProofreadPrompt,
        TextProcessingMode.Typography => TypographyPrompt,
        TextProcessingMode.Cleanup => CleanupPrompt,
        _ => throw new ArgumentOutOfRangeException(nameof(mode))
    };

    public AiChatRequest BuildRequest(TextProcessingMode mode, string text, int? maxOutputTokens = null)
    {
        string systemPrompt = GetSystemPrompt(mode);
        int estimated = EstimateTokens(systemPrompt) + EstimateTokens(text);
        int outputBudget = Math.Max(estimated, text.Length / 2);
        if (maxOutputTokens.HasValue)
        {
            outputBudget = Math.Min(outputBudget, maxOutputTokens.Value);
        }

        return new AiChatRequest
        {
            Messages =
            [
                new AiChatMessage("system", systemPrompt),
                new AiChatMessage("user", text)
            ],
            RequiredCapabilities = AiCapabilities.Text,
            MaxOutputTokens = Math.Clamp(outputBudget, 1024, 32768),
            Temperature = 0.3
        };
    }

    public async Task<AiGatewayResponse> ProcessAsync(
        AiGateway gateway,
        TextProcessingMode mode,
        string text,
        string? preferredProviderId,
        string? preferredModelId,
        CancellationToken cancellationToken)
    {
        AiChatRequest request = BuildRequest(mode, text);
        if (preferredProviderId != null || preferredModelId != null)
        {
            request = new AiChatRequest
            {
                Messages = request.Messages,
                RequiredCapabilities = request.RequiredCapabilities,
                PreferredProviderId = preferredProviderId,
                PreferredModelId = preferredModelId,
                MaxOutputTokens = request.MaxOutputTokens,
                Temperature = request.Temperature
            };
        }

        return await gateway.GenerateAsync(request, cancellationToken).ConfigureAwait(false);
    }

    public string CleanResponse(string rawResponse)
    {
        if (string.IsNullOrWhiteSpace(rawResponse))
        {
            return string.Empty;
        }

        string cleaned = rawResponse.Trim();

        cleaned = StripCodeFence(cleaned);
        cleaned = StripServiceLine(cleaned);
        cleaned = StripOuterQuotes(cleaned);

        return cleaned.Trim();
    }

    public bool FitsInContext(string systemPrompt, string text, int? contextLength)
    {
        if (!contextLength.HasValue)
        {
            return true;
        }

        int totalTokens = EstimateTokens(systemPrompt) + EstimateTokens(text);
        int reserve = totalTokens * ContextReservePercent / 100;
        return totalTokens + reserve <= contextLength.Value;
    }

    public static int EstimateTokens(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0;
        }

        return (int)Math.Ceiling(text.Length / CharsPerToken);
    }

    private static string StripCodeFence(string text)
    {
        if (text.StartsWith("```", StringComparison.Ordinal) && text.EndsWith("```", StringComparison.Ordinal) && text.Length > 6)
        {
            string inner = text[3..^3];
            int firstNewline = inner.IndexOf('\n');
            if (firstNewline >= 0 && firstNewline < 80)
            {
                return inner[(firstNewline + 1)..];
            }

            return inner;
        }

        return text;
    }

    private static string StripServiceLine(string text)
    {
        string[] prefixes =
        [
            "Исправленный текст:",
            "Оформленный текст:",
            "Очищенный текст:",
            "Результат:",
            "Corrected text:",
            "Formatted text:",
            "Cleaned text:",
            "Result:",
            "Korrigierter Text:",
            "Formatierter Text:",
            "Bereinigter Text:",
            "Ergebnis:",
            "Виправлений текст:",
            "Оформлений текст:",
            "Очищений текст:",
            "Результат:"
        ];

        foreach (string prefix in prefixes)
        {
            if (text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                string remainder = text[prefix.Length..].TrimStart();
                return remainder;
            }
        }

        return text;
    }

    private static string StripOuterQuotes(string text)
    {
        if (text.Length < 2)
        {
            return text;
        }

        char first = text[0];
        char last = text[^1];

        if ((first == '"' && last == '"') ||
            (first == '\'' && last == '\'') ||
            (first == '«' && last == '»'))
        {
            string inner = text[1..^1];
            if (inner.Contains('\n'))
            {
                return inner;
            }
        }

        return text;
    }

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
