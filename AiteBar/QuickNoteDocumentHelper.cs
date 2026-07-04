using System;
using System.Windows.Documents;

namespace AiteBar;

internal static class QuickNoteDocumentHelper
{
    public static int GetTextOffset(FlowDocument document, TextPointer pointer)
    {
        return NormalizeLineEndings(new TextRange(document.ContentStart, pointer).Text).Length;
    }

    public static TextPointer? GetTextPointerAtOffset(FlowDocument document, int offset)
    {
        if (offset <= 0)
        {
            return GetStartInsertionPosition(document);
        }

        int documentLength = GetTextOffset(document, document.ContentEnd);
        if (offset >= documentLength)
        {
            return document.ContentEnd;
        }

        TextPointer? pointer = document.ContentStart.GetInsertionPosition(LogicalDirection.Forward);
        TextPointer? best = pointer ?? document.ContentStart;

        while (pointer != null && pointer.CompareTo(document.ContentEnd) <= 0)
        {
            int currentOffset = GetTextOffset(document, pointer);
            if (currentOffset >= offset)
            {
                return pointer;
            }

            best = pointer;
            pointer = pointer.GetNextInsertionPosition(LogicalDirection.Forward);
        }

        return best ?? document.ContentEnd;
    }

    private static TextPointer GetStartInsertionPosition(FlowDocument document)
    {
        TextPointer? pointer = document.ContentStart.GetInsertionPosition(LogicalDirection.Forward);
        while (pointer != null && pointer.CompareTo(document.ContentEnd) <= 0)
        {
            if (GetTextOffset(document, pointer) >= 0)
            {
                return pointer;
            }

            pointer = pointer.GetNextInsertionPosition(LogicalDirection.Forward);
        }

        return document.ContentStart;
    }

    public static string NormalizeLineEndings(string text) => text.Replace("\r\n", "\n").Replace('\r', '\n');
}
