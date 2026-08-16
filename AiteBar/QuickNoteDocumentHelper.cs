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

        // Collect all insertion positions to avoid O(N^2) allocations/regex in the traversal loop
        var positions = new System.Collections.Generic.List<TextPointer>();
        TextPointer? pointer = document.ContentStart.GetInsertionPosition(LogicalDirection.Forward);
        while (pointer != null && pointer.CompareTo(document.ContentEnd) <= 0)
        {
            positions.Add(pointer);
            pointer = pointer.GetNextInsertionPosition(LogicalDirection.Forward);
        }

        if (positions.Count == 0)
        {
            return document.ContentEnd;
        }

        // Binary search to reduce the number of O(N) GetTextOffset calls to O(log N)
        int low = 0;
        int high = positions.Count - 1;
        TextPointer best = positions[high];

        while (low <= high)
        {
            int mid = low + (high - low) / 2;
            int currentOffset = GetTextOffset(document, positions[mid]);
            if (currentOffset == offset)
            {
                return positions[mid];
            }
            if (currentOffset < offset)
            {
                low = mid + 1;
            }
            else
            {
                best = positions[mid];
                high = mid - 1;
            }
        }

        return best;
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
