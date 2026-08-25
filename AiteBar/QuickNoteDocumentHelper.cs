using System;
using System.Text.RegularExpressions;
using System.Windows.Documents;

namespace AiteBar;

internal static class QuickNoteDocumentHelper
{
    private static readonly Regex VisualListMarkerRegex =
        new(@"^(?:(?:[•◦▪])|\d+[.)])(?:\t|\s+)", RegexOptions.Compiled | RegexOptions.Multiline);

    private static readonly Regex BulletListMarkerRegex =
        new(@"^\s*(?:[•◦▪])(?:\t|\s+)", RegexOptions.Compiled);

    private static readonly Regex NumberedListMarkerRegex =
        new(@"^\s*\d+[.)](?:\t|\s+)", RegexOptions.Compiled);

    private static readonly Regex PlainListMarkerRegex =
        new(@"^\s*(?:(?:[•◦▪])|\d+[.)])(?:\t|\s+)", RegexOptions.Compiled);

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

        var startOffsets = new int[leafInlines.Count];
        int currentOffset = 0;
        TextPointer currentPointer = GetTextStartPointer(document);
        for (int i = 0; i < leafInlines.Count; i++)
        {
            Inline inline = leafInlines[i];
            currentOffset += NormalizeLineEndings(new TextRange(currentPointer, inline.ContentStart).Text).Length;
            startOffsets[i] = currentOffset;
            currentOffset += GetLeafInlineLength(inline);
            currentPointer = inline.ContentEnd;
        }

        // Binary search to find the first inline that ends at or after the target offset
        int low = 0;
        int high = leafInlines.Count - 1;
        int targetIndex = -1;

        while (low <= high)
        {
            int mid = low + (high - low) / 2;
            int startOffset = startOffsets[mid];
            int len = GetLeafInlineLength(leafInlines[mid]);

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
        int targetStartOffset = startOffsets[targetIndex];

        if (offset <= targetStartOffset)
        {
            return targetInline.ContentStart;
        }

        int lenOfTarget = GetLeafInlineLength(targetInline);
        int localOffset = Math.Clamp(offset - targetStartOffset, 0, lenOfTarget);

        if (targetInline is Run run)
        {
            int rawOffset = MapNormalizedOffsetToRaw(run.Text, localOffset);
            TextPointer candidate = run.ContentStart.GetPositionAtOffset(rawOffset, LogicalDirection.Forward);
            return AdvanceToTextOffset(document, candidate, offset);
        }

        TextPointer inlineCandidate = targetInline.ContentStart.GetPositionAtOffset(localOffset, LogicalDirection.Forward);
        return AdvanceToTextOffset(document, inlineCandidate, offset);
    }

    private static TextPointer AdvanceToTextOffset(FlowDocument document, TextPointer pointer, int targetOffset)
    {
        var sb = new System.Text.StringBuilder();
        int initialOffset = GetTextOffset(document, pointer);
        sb.AppendLine($"AdvanceToTextOffset: Target={targetOffset}, InitialPointerOffset={initialOffset}");
        
        int currentOffset = initialOffset;
        int step = 0;
        while (pointer.CompareTo(document.ContentEnd) < 0 && currentOffset < targetOffset)
        {
            TextPointer? next = pointer.GetNextInsertionPosition(LogicalDirection.Forward)
                ?? pointer.GetNextContextPosition(LogicalDirection.Forward);
            if (next == null)
            {
                sb.AppendLine($"  Step {step}: next is null");
                break;
            }
            int diff = NormalizeLineEndings(new TextRange(pointer, next).Text).Length;
            int nextOffsetDirect = GetTextOffset(document, next);
            sb.AppendLine($"  Step {step}: pointerOffset={currentOffset} -> nextOffsetDirect={nextOffsetDirect}, diff={diff}, text='{new TextRange(pointer, next).Text}'");
            currentOffset += diff;
            pointer = next;
            step++;
        }

        int finalOffset = GetTextOffset(document, pointer);
        if (finalOffset != targetOffset)
        {
            throw new Exception($"AdvanceToTextOffset Mismatch! Target={targetOffset}, FinalOffset={finalOffset}. Trace:\n{sb.ToString()}");
        }

        return pointer;
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
        if (inline is LineBreak || inline is InlineUIContainer)
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
                int caret = closestRight;
                return (caret, caret);
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

    public static bool HasBulletListMarker(string text) =>
        BulletListMarkerRegex.IsMatch(NormalizeLineEndings(text).TrimEnd('\n'));

    public static bool HasNumberedListMarker(string text) =>
        NumberedListMarkerRegex.IsMatch(NormalizeLineEndings(text).TrimEnd('\n'));

    public static bool HasPlainListMarker(string text) =>
        PlainListMarkerRegex.IsMatch(NormalizeLineEndings(text).TrimEnd('\n'));

    public static string RemovePlainListMarker(string text) =>
        PlainListMarkerRegex.Replace(NormalizeLineEndings(text).TrimEnd('\n'), string.Empty);
}
