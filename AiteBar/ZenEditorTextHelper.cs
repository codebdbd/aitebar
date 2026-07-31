using System;
using System.IO;
using System.Linq;
using System.Text;

namespace AiteBar;

internal static partial class ZenEditorTextHelper
{
    public const string UntitledResourceKey = "ZenEditor_Untitled";
    public const int MaximumTitleLength = 80;

    public static string GetDisplayTitle(string? text, string untitled)
    {
        ReadOnlySpan<char> source = (text ?? string.Empty).AsSpan();
        int lineEnd = source.IndexOfAny('\r', '\n');
        ReadOnlySpan<char> firstLine = lineEnd >= 0 ? source[..lineEnd] : source;
        var normalized = new StringBuilder(Math.Min(firstLine.Length, MaximumTitleLength + 1));
        bool pendingSpace = false;

        foreach (char character in firstLine)
        {
            if (char.IsWhiteSpace(character))
            {
                pendingSpace = normalized.Length > 0;
                continue;
            }

            if (pendingSpace)
            {
                normalized.Append(' ');
                pendingSpace = false;
            }

            normalized.Append(character);
            if (normalized.Length > MaximumTitleLength)
            {
                break;
            }
        }

        if (normalized.Length == 0)
        {
            return untitled;
        }

        return normalized.Length <= MaximumTitleLength
            ? normalized.ToString()
            : string.Concat(normalized.ToString(0, MaximumTitleLength - 1), "\u2026");
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

    public static IReadOnlyList<ZenEditorTextStyle> ApplyTextChangeToStyles(
        IReadOnlyList<ZenEditorTextStyle>? styles,
        ZenEditorTextChange change,
        ZenEditorTextStyle? insertedStyle,
        int currentTextLength)
    {
        int currentLength = Math.Max(0, currentTextLength);
        int offset = Math.Clamp(change.Offset, 0, currentLength);
        int addedLength = Math.Clamp(change.AddedLength, 0, currentLength - offset);
        int removedLength = Math.Max(0, change.RemovedLength);
        int removedEnd = offset + removedLength;
        int delta = addedLength - removedLength;
        var transformed = new List<ZenEditorTextStyle>((styles?.Count ?? 0) + 2);

        foreach (ZenEditorTextStyle style in styles ?? [])
        {
            int start = Math.Max(0, style.Start);
            int end = Math.Max(start, style.Start + style.Length);
            if (end <= offset)
            {
                AddClampedStyle(transformed, style with { Start = start, Length = end - start }, currentLength);
                continue;
            }

            if (start >= removedEnd)
            {
                AddClampedStyle(
                    transformed,
                    style with { Start = start + delta, Length = end - start },
                    currentLength);
                continue;
            }

            if (start < offset)
            {
                AddClampedStyle(
                    transformed,
                    style with { Start = start, Length = offset - start },
                    currentLength);
            }

            if (end > removedEnd)
            {
                int shiftedStart = Math.Max(start, removedEnd) + delta;
                AddClampedStyle(
                    transformed,
                    style with { Start = shiftedStart, Length = end + delta - shiftedStart },
                    currentLength);
            }
        }

        if (insertedStyle is { } inserted && addedLength > 0)
        {
            AddClampedStyle(
                transformed,
                inserted with { Start = offset, Length = addedLength },
                currentLength);
        }

        if (transformed.Count < 2)
        {
            return transformed;
        }

        transformed.Sort((left, right) => left.Start.CompareTo(right.Start));
        var merged = new List<ZenEditorTextStyle>(transformed.Count);
        foreach (ZenEditorTextStyle style in transformed)
        {
            if (merged.Count > 0
                && merged[^1].Start + merged[^1].Length == style.Start
                && merged[^1].Bold == style.Bold
                && merged[^1].Italic == style.Italic
                && merged[^1].Underline == style.Underline)
            {
                ZenEditorTextStyle previous = merged[^1];
                merged[^1] = previous with { Length = previous.Length + style.Length };
            }
            else
            {
                merged.Add(style);
            }
        }

        return merged;
    }

    private static void AddClampedStyle(
        List<ZenEditorTextStyle> styles,
        ZenEditorTextStyle style,
        int textLength)
    {
        int start = Math.Clamp(style.Start, 0, textLength);
        int length = Math.Clamp(style.Length, 0, textLength - start);
        if (length > 0 && (style.Bold || style.Italic || style.Underline))
        {
            styles.Add(style with { Start = start, Length = length });
        }
    }
}
