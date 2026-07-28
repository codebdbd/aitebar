using System.Threading;
using System.Windows;
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
    public void GetTextPointerAtOffset_RoundTripsOffsetsAcrossParagraphBlocks()
    {
        RunSta(() =>
        {
            var document = new FlowDocument();
            document.Blocks.Add(new Paragraph(new Run("one")));
            document.Blocks.Add(new Paragraph(new Run("two")));
            string text = QuickNoteDocumentHelper.NormalizeLineEndings(
                new TextRange(document.ContentStart, document.ContentEnd).Text).TrimEnd('\n');

            for (int offset = 0; offset <= text.Length; offset++)
            {
                TextPointer pointer = QuickNoteDocumentHelper.GetTextPointerAtOffset(document, offset)!;
                Assert.True(QuickNoteDocumentHelper.GetTextOffset(document, pointer) >= offset);
            }
        });
    }

    [Fact]
    public void GetTextPointerAtOffset_RoundTripsOffsetsAcrossListBlocks()
    {
        RunSta(() =>
        {
            var document = new FlowDocument();
            var list = new System.Windows.Documents.List { MarkerStyle = TextMarkerStyle.Disc };
            list.ListItems.Add(new ListItem(new Paragraph(new Run("one"))));
            list.ListItems.Add(new ListItem(new Paragraph(new Run("two"))));
            document.Blocks.Add(list);
            string text = QuickNoteDocumentHelper.NormalizeLineEndings(
                new TextRange(document.ContentStart, document.ContentEnd).Text).TrimEnd('\n');

            for (int offset = 0; offset <= text.Length; offset++)
            {
                TextPointer pointer = QuickNoteDocumentHelper.GetTextPointerAtOffset(document, offset)!;
                Assert.True(QuickNoteDocumentHelper.GetTextOffset(document, pointer) >= offset);
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

    [Fact]
    public void GetTextPointerAtOffset_ReturnsStartOfTextAfterParagraphMovesOutOfList()
    {
        RunSta(() =>
        {
            var paragraph = new Paragraph(new Run("note"));
            var item = new ListItem(paragraph);
            var list = new System.Windows.Documents.List(item);
            var document = new FlowDocument(list);
            item.Blocks.Remove(paragraph);
            document.Blocks.InsertBefore(list, paragraph);
            document.Blocks.Remove(list);

            TextPointer start = QuickNoteDocumentHelper.GetTextPointerAtOffset(document, 0)!;

            Assert.Equal("note", new TextRange(start, paragraph.ContentEnd).Text);
        });
    }

    [Fact]
    public void RemapSelection_PreservesSelectedTextWhenListStructureRemovesHiddenLineBreaks()
    {
        var selection = QuickNoteDocumentHelper.RemapSelection(
            "\n\nformatted item\n\n",
            "formatted item\n",
            2,
            16);

        Assert.Equal((0, 14), selection);
    }

    [Fact]
    public void RemapSelection_UsesNearestMatchingTextWhenContentRepeats()
    {
        var selection = QuickNoteDocumentHelper.RemapSelection(
            "\n\nsame\nsame\n\n",
            "same\nsame\n",
            7,
            11);

        Assert.Equal((5, 9), selection);
    }

    [Theory]
    [InlineData("•\tone", "one")]
    [InlineData("1.\tone\n2.\ttwo", "one\ntwo")]
    public void RemoveVisualListMarkers_RemovesWpfMarkersFromSelectedText(string text, string expected)
    {
        Assert.Equal(expected, QuickNoteDocumentHelper.RemoveVisualListMarkers(text));
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
