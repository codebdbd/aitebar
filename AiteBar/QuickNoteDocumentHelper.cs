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
        offset = Math.Max(0, offset);
        TextPointer? pointer = document.ContentStart;
        TextPointer? best = document.ContentStart.GetInsertionPosition(LogicalDirection.Forward);
        int currentOffset = 0;

        while (pointer != null && pointer.CompareTo(document.ContentEnd) < 0)
        {
            TextPointerContext context = pointer.GetPointerContext(LogicalDirection.Forward);
            if (context == TextPointerContext.Text)
            {
                string runText = pointer.GetTextInRun(LogicalDirection.Forward);
                if (offset <= currentOffset + runText.Length)
                {
                    int runOffset = offset - currentOffset;
                    return pointer.GetPositionAtOffset(runOffset, LogicalDirection.Forward) ?? pointer;
                }

                currentOffset += runText.Length;
                pointer = pointer.GetPositionAtOffset(runText.Length, LogicalDirection.Forward);
                best = pointer?.GetInsertionPosition(LogicalDirection.Forward) ?? best;
                continue;
            }

            if (context == TextPointerContext.ElementStart &&
                pointer.GetAdjacentElement(LogicalDirection.Forward) is LineBreak)
            {
                if (offset <= currentOffset)
                {
                    return pointer.GetInsertionPosition(LogicalDirection.Forward) ?? pointer;
                }

                currentOffset++;
                best = pointer.GetNextInsertionPosition(LogicalDirection.Forward) ?? best;
            }

            pointer = pointer.GetNextContextPosition(LogicalDirection.Forward);
        }

        return offset <= currentOffset
            ? best ?? document.ContentEnd
            : document.ContentEnd;
    }

    public static string NormalizeLineEndings(string text) => text.Replace("\r\n", "\n").Replace('\r', '\n');
}
