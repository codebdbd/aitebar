using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace AiteBar;

internal static partial class ZenEditorTextHelper
{
    public const string UntitledResourceKey = "ZenEditor_Untitled";
    public const int MaximumTitleLength = 80;

    public static string GetDisplayTitle(string? text, string untitled)
    {
        string firstLine = (text ?? string.Empty)
            .Split(['\r', '\n'], 2, StringSplitOptions.None)[0]
            .Replace('\t', ' ');
        string normalized = RepeatedWhitespaceRegex().Replace(firstLine.Trim(), " ");

        if (string.IsNullOrEmpty(normalized))
        {
            return untitled;
        }

        return normalized.Length <= MaximumTitleLength
            ? normalized
            : string.Concat(normalized.AsSpan(0, MaximumTitleLength - 1), "\u2026");
    }

    public static string CreateExportFileName(string? text, string untitled)
    {
        string title = GetDisplayTitle(text, untitled);
        char[] invalidCharacters = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(title.Length);

        foreach (char character in title)
        {
            builder.Append(invalidCharacters.Contains(character) ? '-' : character);
        }

        string sanitized = builder.ToString().Trim().TrimEnd('.');
        return $"{(string.IsNullOrWhiteSpace(sanitized) ? untitled : sanitized)}.txt";
    }

    public static string NormalizeExportText(string? text)
    {
        string normalized = (text ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n');
        if (normalized.Length == 0)
        {
            return string.Empty;
        }

        string[] lines = normalized.Split('\n', StringSplitOptions.None);
        int firstTextLine = Array.FindIndex(lines, line => !string.IsNullOrWhiteSpace(line));
        if (firstTextLine < 0)
        {
            return string.Join("\r\n", Enumerable.Repeat(string.Empty, lines.Length));
        }

        int lastTextLine = Array.FindLastIndex(lines, line => !string.IsNullOrWhiteSpace(line));
        var result = new StringBuilder(normalized.Length + 16);
        for (int index = 0; index < firstTextLine; index++)
        {
            result.Append("\r\n");
        }

        bool wroteTextLine = false;
        for (int index = firstTextLine; index <= lastTextLine; index++)
        {
            if (string.IsNullOrWhiteSpace(lines[index]))
            {
                continue;
            }

            if (wroteTextLine)
            {
                result.Append("\r\n\r\n");
            }

            result.Append(lines[index]);
            wroteTextLine = true;
        }

        for (int index = lastTextLine + 1; index < lines.Length; index++)
        {
            result.Append("\r\n");
        }

        return result.ToString();
    }

    public static (int Caret, int SelectionStart, int SelectionLength) ClampSelection(
        int textLength,
        int caret,
        int selectionStart,
        int selectionLength)
    {
        int length = Math.Max(0, textLength);
        int safeCaret = Math.Clamp(caret, 0, length);
        int safeStart = Math.Clamp(selectionStart, 0, length);
        int safeLength = Math.Clamp(selectionLength, 0, length - safeStart);
        return (safeCaret, safeStart, safeLength);
    }

    public static ZenEditorTextChange CalculateSingleChange(string? previous, string? current)
    {
        previous ??= string.Empty;
        current ??= string.Empty;
        int prefix = 0;
        int maximumPrefix = Math.Min(previous.Length, current.Length);
        while (prefix < maximumPrefix && previous[prefix] == current[prefix])
        {
            prefix++;
        }

        int suffix = 0;
        while (suffix < previous.Length - prefix
               && suffix < current.Length - prefix
               && previous[^(suffix + 1)] == current[^(suffix + 1)])
        {
            suffix++;
        }

        return new ZenEditorTextChange(
            prefix,
            current.Length - prefix - suffix,
            previous.Length - prefix - suffix);
    }

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex RepeatedWhitespaceRegex();
}
