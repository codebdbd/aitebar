using System;
using System.Collections.Generic;
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
    public const double OtherScriptCharsPerToken = 2.0;
    public const double CjkCharsPerToken = 1.0;

    public string GetSystemPrompt(TextProcessingMode mode) => mode switch
    {
        TextProcessingMode.Proofread => ProofreadPrompt,
        TextProcessingMode.Typography => TypographyPrompt,
        TextProcessingMode.Cleanup => CleanupPrompt,
        TextProcessingMode.LiteraryEdit => LiteraryEditPrompt,
        _ => throw new ArgumentOutOfRangeException(nameof(mode))
    };

    public AiChatRequest BuildRequest(TextProcessingMode mode, string text, int? maxOutputTokens = null)
    {
        string systemPrompt = mode == TextProcessingMode.Proofread
            ? ProofreadPrompt
            : GetSystemPrompt(mode) + LanguagePreservationInstruction + ProtectedMarkerInstruction;
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
            Temperature = mode switch
            {
                TextProcessingMode.Proofread => 0.0,
                TextProcessingMode.Typography => 0.25,
                TextProcessingMode.Cleanup => 0.1,
                TextProcessingMode.LiteraryEdit => 0.4,
                _ => throw new ArgumentOutOfRangeException(nameof(mode))
            }
        };
    }

    public string CleanResponse(string rawResponse, string? fallbackText = null)
    {
        if (string.IsNullOrWhiteSpace(rawResponse))
        {
            return string.Empty;
        }

        string cleaned = ClosedReasoningBlockRegex().Replace(rawResponse, string.Empty);
        cleaned = OrphanReasoningCloseTagRegex().Replace(cleaned, string.Empty);

        Match unclosedReasoning = ReasoningOpenTagRegex().Match(cleaned);
        if (unclosedReasoning.Success)
        {
            string visiblePrefix = cleaned[..unclosedReasoning.Index].Trim();
            string reasoningTail = cleaned[(unclosedReasoning.Index + unclosedReasoning.Length)..];
            string? recovered = RecoverExplicitFinalAnswer(reasoningTail, fallbackText);
            if (!string.IsNullOrWhiteSpace(recovered))
            {
                return recovered.Trim();
            }
            if (!string.IsNullOrWhiteSpace(fallbackText))
            {
                return fallbackText.Trim();
            }
            return visiblePrefix;
        }

        return cleaned.Trim();
    }

    internal static bool ViolatesContentPreservation(
        string input,
        string output,
        IEnumerable<string>? protectedFragments = null,
        double minimumWordOverlap = 0.35)
    {
        string comparableInput = input ?? string.Empty;
        string comparableOutput = output ?? string.Empty;
        if (protectedFragments != null)
        {
            foreach (string fragment in protectedFragments.Where(value => !string.IsNullOrEmpty(value)))
            {
                comparableInput = comparableInput.Replace(fragment, string.Empty, StringComparison.Ordinal);
                comparableOutput = comparableOutput.Replace(fragment, string.Empty, StringComparison.Ordinal);
            }
        }

        comparableInput = comparableInput.Trim();
        comparableOutput = comparableOutput.Trim();
        if (comparableInput.Length == 0 || comparableOutput.Length == 0)
        {
            return false;
        }

        TextScript inputScript = GetDominantScript(comparableInput);
        TextScript outputScript = GetDominantScript(comparableOutput);
        if (inputScript != TextScript.None &&
            outputScript != TextScript.None &&
            inputScript != outputScript)
        {
            return true;
        }

        HashSet<string> inputWords = GetContentWords(comparableInput);
        HashSet<string> outputWords = GetContentWords(comparableOutput);
        if (inputWords.Count < 4 || outputWords.Count < 4)
        {
            return false;
        }

        int sharedWords = inputWords.Count(outputWords.Contains);
        double overlap = (2.0 * sharedWords) / (inputWords.Count + outputWords.Count);
        return overlap < Math.Clamp(minimumWordOverlap, 0.0, 1.0);
    }

    internal static double GetMinimumWordOverlap(TextProcessingMode mode) => mode switch
    {
        TextProcessingMode.LiteraryEdit => 0.15,
        TextProcessingMode.Proofread or TextProcessingMode.Typography or TextProcessingMode.Cleanup => 0.35,
        _ => throw new ArgumentOutOfRangeException(nameof(mode))
    };

    private static HashSet<string> GetContentWords(string text)
    {
        var words = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var word = new System.Text.StringBuilder();
        foreach (char character in text)
        {
            if (char.IsLetterOrDigit(character))
            {
                word.Append(char.ToLowerInvariant(character));
                continue;
            }
            if (word.Length > 0)
            {
                words.Add(word.ToString());
                word.Clear();
            }
        }
        if (word.Length > 0)
        {
            words.Add(word.ToString());
        }
        return words;
    }

    private static TextScript GetDominantScript(string text)
    {
        int latin = 0;
        int cyrillic = 0;
        int cjk = 0;
        int other = 0;
        foreach (char character in text)
        {
            if (character is >= '\u0400' and <= '\u052F')
            {
                cyrillic++;
            }
            else if (character is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '\u00C0' and <= '\u024F')
            {
                latin++;
            }
            else if (IsCjkCharacter(character))
            {
                cjk++;
            }
            else if (char.IsLetter(character))
            {
                other++;
            }
        }

        int total = latin + cyrillic + cjk + other;
        int maximum = Math.Max(Math.Max(latin, cyrillic), Math.Max(cjk, other));
        if (total < 6 || maximum < 6 || maximum < total * 0.7)
        {
            return TextScript.None;
        }
        if (maximum == latin) return TextScript.Latin;
        if (maximum == cyrillic) return TextScript.Cyrillic;
        if (maximum == cjk) return TextScript.Cjk;
        return TextScript.Other;
    }

    internal static string HideReasoningFromStreamingPreview(string rawResponse)
    {
        if (string.IsNullOrEmpty(rawResponse))
        {
            return string.Empty;
        }

        string cleaned = ClosedReasoningBlockRegex().Replace(rawResponse, string.Empty);
        cleaned = OrphanReasoningCloseTagRegex().Replace(cleaned, string.Empty);
        Match reasoningStart = ReasoningOpenTagRegex().Match(cleaned);
        return reasoningStart.Success ? cleaned[..reasoningStart.Index] : cleaned;
    }

    private static string? RecoverExplicitFinalAnswer(string reasoningTail, string? fallbackText)
    {
        if (string.IsNullOrWhiteSpace(fallbackText))
        {
            return null;
        }

        const int maxRecoveryLength = 4096;
        string normalizedFallback = fallbackText.Trim();
        if (normalizedFallback.Length > maxRecoveryLength)
        {
            return null;
        }

        string? bestCandidate = null;
        int bestDistance = int.MaxValue;
        foreach (Match match in ExplicitFinalAnswerRegex().Matches(reasoningTail))
        {
            string candidate = NormalizeRecoveredCandidate(match.Groups["answer"].Value);
            if (string.IsNullOrWhiteSpace(candidate) || candidate.Length > maxRecoveryLength)
            {
                continue;
            }

            int distance = CalculateEditDistance(normalizedFallback, candidate);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestCandidate = candidate;
            }
        }

        if (bestCandidate == null)
        {
            return null;
        }

        int longestLength = Math.Max(normalizedFallback.Length, bestCandidate.Length);
        int maximumDistance = Math.Max(3, (int)Math.Ceiling(longestLength * 0.1));
        return longestLength > 0 && bestDistance <= maximumDistance
            ? bestCandidate
            : null;
    }

    private static string NormalizeRecoveredCandidate(string candidate)
    {
        string normalized = candidate.Trim();
        if (normalized.EndsWith("✅", StringComparison.Ordinal))
        {
            normalized = normalized[..^1].TrimEnd();
        }
        if (normalized.Length >= 2 &&
            ((normalized[0] == '"' && normalized[^1] == '"') ||
             (normalized[0] == '\'' && normalized[^1] == '\'')))
        {
            normalized = normalized[1..^1];
        }
        return normalized.Trim();
    }

    private static int CalculateEditDistance(string source, string target)
    {
        int[] previous = new int[target.Length + 1];
        int[] current = new int[target.Length + 1];
        for (int column = 0; column <= target.Length; column++)
        {
            previous[column] = column;
        }

        for (int row = 1; row <= source.Length; row++)
        {
            current[0] = row;
            for (int column = 1; column <= target.Length; column++)
            {
                int substitutionCost = source[row - 1] == target[column - 1] ? 0 : 1;
                current[column] = Math.Min(
                    Math.Min(current[column - 1] + 1, previous[column] + 1),
                    previous[column - 1] + substitutionCost);
            }
            (previous, current) = (current, previous);
        }

        return previous[target.Length];
    }

    public static int EstimateTokens(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0;
        }

        int cyrillicLetters = 0;
        int latinLetters = 0;
        int cjkCharacters = 0;
        int otherScriptLetters = 0;
        foreach (char character in text)
        {
            if (IsCjkCharacter(character))
            {
                cjkCharacters++;
            }
            else if (character is >= '\u0400' and <= '\u052F')
            {
                cyrillicLetters++;
            }
            else if (character is >= 'A' and <= 'Z' or >= 'a' and <= 'z')
            {
                latinLetters++;
            }
            else if (char.IsLetter(character))
            {
                otherScriptLetters++;
            }
        }

        int remainingCharacters =
            text.Length - cyrillicLetters - latinLetters - cjkCharacters - otherScriptLetters;
        double estimatedTokens =
            (latinLetters / LatinCharsPerToken) +
            (cyrillicLetters / CyrillicCharsPerToken) +
            (cjkCharacters / CjkCharsPerToken) +
            (otherScriptLetters / OtherScriptCharsPerToken) +
            (remainingCharacters / LatinCharsPerToken);
        return (int)Math.Ceiling(estimatedTokens);
    }

    private static bool IsCjkCharacter(char character) =>
        character is >= '\u3400' and <= '\u4DBF' or
            >= '\u4E00' and <= '\u9FFF' or
            >= '\uF900' and <= '\uFAFF' or
            >= '\u3040' and <= '\u30FF' or
            >= '\uAC00' and <= '\uD7AF';

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
        ```[\s\S]*?```|`[^`\r\n]+`|data:[^\s<>"']+|https?://[^\s<>"']+|www\.[^\s<>"']+|[\w.!#$%&'*+/=?^`{|}~-]+@[\w-]+(?:\.[\w-]+)+|(?:[A-Za-z]:\\|\\\\)[^\s<>:"|?*]+|(?<!\w)/(?:[^\s/]+/)*[^\s/]+|(?<![\w-])--?[A-Za-z0-9][A-Za-z0-9_-]*(?:[=:][^\s<>"']+)?|</?[A-Za-z][^>\r\n]*>|\$\{[^}\r\n]+\}|\{\{[^}\r\n]+\}\}|%[A-Za-z_][A-Za-z0-9_]*%|\b[0-9a-fA-F]{8}(?:-[0-9a-fA-F]{4}){3}-[0-9a-fA-F]{12}\b|\bv?\d+(?:\.\d+){1,3}(?:[-+][A-Za-z0-9.-]+)?\b
        """,
        RegexOptions.CultureInvariant)]
    private static partial Regex TechnicalFragmentRegex();

    [GeneratedRegex(
        @"<\s*(?<reasoningTag>think|thinking|analysis|reasoning)\b[^>]*>[\s\S]*?<\s*/\s*\k<reasoningTag>\s*>",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ClosedReasoningBlockRegex();

    [GeneratedRegex(
        @"<\s*(?:think|thinking|analysis|reasoning)\b[^>]*>",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ReasoningOpenTagRegex();

    [GeneratedRegex(
        @"<\s*/\s*(?:think|thinking|analysis|reasoning)\s*>",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex OrphanReasoningCloseTagRegex();

    [GeneratedRegex(
        @"(?im)^\s*(?:\[(?:final\s+)?output(?:\s+generation)?\]|(?:final\s+)?output(?:\s+generation)?|final\s+string|answer|result|ответ|результат)\s*(?:->|:)\s*(?<answer>[^\r\n]+?)\s*$",
        RegexOptions.CultureInvariant)]
    private static partial Regex ExplicitFinalAnswerRegex();

    private const string ProtectedMarkerInstruction =
        """

        PROTECTED TOKENS:
        Tokens matching __AITEBAR_PROTECTED_...__ represent protected technical content.
        Copy every protected token exactly once, unchanged and in its original position; do not translate, edit, split, reorder, omit, or duplicate it.
        """;

    private const string LanguagePreservationInstruction =
        """

        LANGUAGE AND CONTENT:
        Preserve the language of every input segment and never translate any part of the text.
        Preserve meaning, names, facts, and content order; if the requested transformation would require rewriting, keep the affected fragment unchanged.
        """;

    private const string ProofreadPrompt =
        "Correct only spelling, grammar, and punctuation errors and return only the corrected text without changing its language, meaning, wording, structure, or technical content.";

    private const string TypographyPrompt =
        """
        Apply typography only, using the conventions of each language present in the text.
        Normalize quotation marks, hyphens and dashes, ellipses, punctuation spacing, non-breaking spaces, ranges, initials, abbreviations, percentages, currencies, degrees, units, and repeated spaces.
        Do not correct spelling or grammar, rewrite wording, improve style, add or remove content, reorder sentences, change paragraph boundaries, translate, or alter meaning.
        Preserve URLs, email addresses, file paths, file names, commands, code, HTML/XML tags, Markdown syntax, variables, templates, version numbers, product codes, identifiers, and all other technical content.
        Return only the typographically formatted text with no explanation, heading, change list, or wrapper.
        """;

    private const string CleanupPrompt =
        """
        Remove only clear copy/paste and document-extraction artifacts.
        Join accidental line breaks inside sentences, restore words split by line-break hyphenation, remove repeated spaces, excessive blank lines, invisible or control characters, obvious standalone page numbers, and repeated headers or footers only when the same non-content line occurs more than twice in equivalent positions.
        Preserve genuine paragraphs, lists, document structure, and all meaningful content.
        Do not correct spelling, grammar, punctuation, or typography; do not rewrite, shorten, expand, translate, or alter meaning.
        Preserve URLs, email addresses, file paths, file names, commands, code, tags, variables, version numbers, product codes, identifiers, and all other technical content.
        If a fragment is not clearly an artifact, keep it unchanged.
        Return only the cleaned text with no explanation, heading, removal list, or wrapper.
        """;

    private const string LiteraryEditPrompt =
        """
        Edit the text for clarity, fluency, rhythm, and literary quality while preserving its original language, meaning, facts, names, narrative perspective, intended tone, and paragraph structure.
        You may rewrite awkward sentences, improve word choice, and remove unintentional repetition, but do not invent information, add new ideas, omit meaningful content, or change the author's position.
        Preserve URLs, email addresses, file paths, file names, commands, code, tags, variables, version numbers, product codes, identifiers, and all other technical content.
        Return only the edited text with no explanation, heading, commentary, change list, or wrapper.
        """;

    private enum TextScript
    {
        None,
        Latin,
        Cyrillic,
        Cjk,
        Other
    }
}

public sealed record ProtectedText(
    string Text,
    IReadOnlyDictionary<string, string> Fragments);
