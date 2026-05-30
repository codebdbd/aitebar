using System.Threading;
using System.Windows.Documents;
using AiteBar;

namespace AiteBar.Tests;

public sealed class QuickNoteDocumentHelperTests
{
    [Fact]
    public void NormalizeLineEndings_ConvertsWindowsAndClassicMacEndings()
    {
        Assert.Equal("one\ntwo\nthree", QuickNoteDocumentHelper.NormalizeLineEndings("one\r\ntwo\rthree"));
    }

    [Fact]
    public void GetTextPointerAtOffset_RoundTripsOffsetsAcrossFormattedRunsAndLineBreaks()
    {
        RunSta(() =>
        {
            var paragraph = new Paragraph();
            paragraph.Inlines.Add(new Run("one"));
            paragraph.Inlines.Add(new LineBreak());
            paragraph.Inlines.Add(new Bold(new Run("two")));
            paragraph.Inlines.Add(new Run(" three"));
            var document = new FlowDocument(paragraph);
            string text = QuickNoteDocumentHelper.NormalizeLineEndings(
                new TextRange(document.ContentStart, document.ContentEnd).Text).TrimEnd('\n');

            for (int offset = 0; offset <= text.Length; offset++)
            {
                TextPointer pointer = QuickNoteDocumentHelper.GetTextPointerAtOffset(document, offset)!;
                Assert.Equal(offset, QuickNoteDocumentHelper.GetTextOffset(document, pointer));
            }
        });
    }

    [Fact]
    public void GetTextPointerAtOffset_ClampsNegativeAndPastEndOffsets()
    {
        RunSta(() =>
        {
            var document = new FlowDocument(new Paragraph(new Run("note")));

            TextPointer negative = QuickNoteDocumentHelper.GetTextPointerAtOffset(document, -20)!;
            TextPointer pastEnd = QuickNoteDocumentHelper.GetTextPointerAtOffset(document, 500)!;

            Assert.Equal(0, QuickNoteDocumentHelper.GetTextOffset(document, negative));
            Assert.Equal(0, pastEnd.CompareTo(document.ContentEnd));
        });
    }

    private static void RunSta(Action action)
    {
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                exception = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (exception != null)
        {
            throw exception;
        }
    }
}
