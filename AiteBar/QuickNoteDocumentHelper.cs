using System;
using System.Windows.Documents;
using System.Text.RegularExpressions;

namespace AiteBar;

internal static class QuickNoteDocumentHelper
{
    private static readonly Regex VisualListMarkerRegex =
        new(@"^(?:[•◦▪]|\d+[.)])\t", RegexOptions.Compiled | RegexOptions.Multiline);

    public static int GetTextOffset(FlowDocument document, TextPointer pointer)
    {
        return NormalizeLineEndings(new TextRange(document.ContentStart, pointer).Text).Length;
    }

    public static TextPointer? GetTextPointerAtOffset(FlowDocument document, int offset)
    {
        if (offset <= 0)
        {
            return GetTextStartPointer(document);
        }

        int documentLength = GetTextOffset(document, document.ContentEnd);
        if (offset >= documentLength)
        {
            return document.ContentEnd;
        }

        var leafInlines = GetLeafInlines(document);
        if (leafInlines.Count == 0)
        {
            return document.ContentEnd;
        }

        var startOffsetsCache = new System.Collections.Generic.Dictionary<Inline, int>();
        int GetStartOffset(Inline inline)
        {
            if (!startOffsetsCache.TryGetValue(inline, out int val))
            {
                val = GetTextOffset(document, inline.ContentStart);
                startOffsetsCache[inline] = val;
            }
            return val;
        }

        // Binary search to find the first inline that ends at or after the target offset
        int low = 0;
        int high = leafInlines.Count - 1;
        int targetIndex = -1;

        while (low <= high)
        {
            int mid = low + (high - low) / 2;
            Inline inline = leafInlines[mid];
            int startOffset = GetStartOffset(inline);
            int len = GetLeafInlineLength(inline);

            if (startOffset + len >= offset)
            {
                targetIndex = mid;
                high = mid - 1;
            }
            else
            {
                low = mid + 1;
            }
        }

        if (targetIndex == -1)
        {
            return document.ContentEnd;
        }

        Inline targetInline = leafInlines[targetIndex];
        int targetStartOffset = GetStartOffset(targetInline);

        if (offset <= targetStartOffset)
        {
            return targetInline.ContentStart;
        }

        int lenOfTarget = GetLeafInlineLength(targetInline);
        int localOffset = Math.Clamp(offset - targetStartOffset, 0, lenOfTarget);

        if (targetInline is Run run)
        {
            int rawOffset = MapNormalizedOffsetToRaw(run.Text, localOffset);
            return run.ContentStart.GetPositionAtOffset(rawOffset, LogicalDirection.Forward);
        }

        return targetInline.ContentStart.GetPositionAtOffset(localOffset, LogicalDirection.Forward);
    }

    private static System.Collections.Generic.List<Inline> GetLeafInlines(FlowDocument document)
    {
        var list = new System.Collections.Generic.List<Inline>();
        AccumulateLeafInlines(document.Blocks, list);
        return list;
    }

    private static void AccumulateLeafInlines(BlockCollection blocks, System.Collections.Generic.List<Inline> list)
    {
        foreach (var block in blocks)
        {
            if (block is Paragraph p)
            {
                AccumulateLeafInlines(p.Inlines, list);
            }
            else if (block is System.Windows.Documents.List fl)
            {
                foreach (var item in fl.ListItems)
                {
                    AccumulateLeafInlines(item.Blocks, list);
                }
            }
            else if (block is Section s)
            {
                AccumulateLeafInlines(s.Blocks, list);
            }
        }
    }

    private static void AccumulateLeafInlines(InlineCollection inlines, System.Collections.Generic.List<Inline> list)
    {
        foreach (var inline in inlines)
        {
            if (inline is Span span)
            {
                AccumulateLeafInlines(span.Inlines, list);
            }
            else
            {
                list.Add(inline);
            }
        }
    }

    private static int GetLeafInlineLength(Inline inline)
    {
        if (inline is Run run)
        {
            return NormalizeLineEndings(run.Text).Length;
        }
        if (inline is LineBreak)
        {
            return 1;
        }
        return 0;
    }

    private static int MapNormalizedOffsetToRaw(string text, int normalizedOffset)
    {
        int rawIdx = 0;
        int normIdx = 0;
        while (normIdx < normalizedOffset && rawIdx < text.Length)
        {
            if (rawIdx <= text.Length - 2 && text[rawIdx] == '\r' && text[rawIdx + 1] == '\n')
            {
                rawIdx += 2;
                normIdx += 1;
            }
            else if (text[rawIdx] == '\r' || text[rawIdx] == '\n')
            {
                rawIdx += 1;
                normIdx += 1;
            }
            else
            {
                rawIdx += 1;
                normIdx += 1;
            }
        }
        return rawIdx;
    }

    private static TextPointer GetTextStartPointer(FlowDocument document)
    {
        TextPointer current = document.ContentStart;
        TextPointer best = current;
        while (current.CompareTo(document.ContentEnd) < 0)
        {
            TextPointer? next = current.GetNextContextPosition(LogicalDirection.Forward);
            if (next == null || GetTextOffset(document, next) > 0)
            {
                break;
            }

            best = next;
            current = next;
        }

        return best;
    }

    public static (int Start, int End) RemapSelection(
        string oldText,
        string newText,
        int selectionStart,
        int selectionEnd,
        string? selectedText = null)
    {
        oldText = NormalizeLineEndings(oldText);
        newText = NormalizeLineEndings(newText);
        int start = Math.Clamp(Math.Min(selectionStart, selectionEnd), 0, oldText.Length);
        int end = Math.Clamp(Math.Max(selectionStart, selectionEnd), 0, oldText.Length);
        selectedText = selectedText == null
            ? oldText[start..end]
            : NormalizeLineEndings(selectedText);

        if (selectedText.Length > 0)
        {
            int closestMatch = FindClosestMatch(newText, selectedText, start);
            if (closestMatch >= 0)
            {
                return (closestMatch, closestMatch + selectedText.Length);
            }
        }
        else
        {
            const int contextLength = 32;
            string leftContext = oldText[Math.Max(0, start - contextLength)..start];
            string rightContext = oldText[start..Math.Min(oldText.Length, start + contextLength)];
            string combinedContext = leftContext + rightContext;
            int closestContext = FindClosestMatch(newText, combinedContext, Math.Max(0, start - leftContext.Length));
            if (closestContext >= 0)
            {
                int caret = closestContext + leftContext.Length;
                return (caret, caret);
            }

            int closestLeft = FindClosestMatch(newText, leftContext, Math.Max(0, start - leftContext.Length));
            if (leftContext.Length > 0 && closestLeft >= 0)
            {
                int caret = closestLeft + leftContext.Length;
                return (caret, caret);
            }

            int closestRight = FindClosestMatch(newText, rightContext, start);
            if (rightContext.Length > 0 && closestRight >= 0)
            {
                return (closestRight, closestRight);
            }
        }

        int clampedStart = Math.Clamp(start, 0, newText.Length);
        int clampedEnd = Math.Clamp(end, clampedStart, newText.Length);
        return (clampedStart, clampedEnd);
    }

    private static int FindClosestMatch(string text, string value, int expectedOffset)
    {
        if (value.Length == 0)
        {
            return -1;
        }

        int closest = -1;
        int closestDistance = int.MaxValue;
        int searchStart = 0;
        while (searchStart <= text.Length - value.Length)
        {
            int match = text.IndexOf(value, searchStart, StringComparison.Ordinal);
            if (match < 0)
            {
                break;
            }

            int distance = Math.Abs(match - expectedOffset);
            if (distance < closestDistance)
            {
                closest = match;
                closestDistance = distance;
            }

            searchStart = match + 1;
        }

        return closest;
    }

    public static string NormalizeLineEndings(string text) => text.Replace("\r\n", "\n").Replace('\r', '\n');

    public static string RemoveVisualListMarkers(string text) =>
        VisualListMarkerRegex.Replace(NormalizeLineEndings(text), string.Empty);
}
