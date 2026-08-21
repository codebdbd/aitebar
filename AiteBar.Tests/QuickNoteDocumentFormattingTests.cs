using System.Threading;
using System.Windows;
using System.Windows.Documents;
using AiteBar;

namespace AiteBar.Tests;

public sealed class QuickNoteDocumentFormattingTests
{
    [Fact]
    public void CodeBlock_IsNativeEditableFlowDocumentSection()
    {
        RunSta(() =>
        {
            Section section = QuickNoteDocumentFormatting.CreateCodeBlockElement("first\nsecond", QuickNoteThemeCatalog.Find(null));
            Assert.Equal(2, section.Blocks.OfType<Paragraph>().Count());
            Assert.DoesNotContain(section.Blocks, static block => block is BlockUIContainer);

            Paragraph line = Assert.IsType<Paragraph>(section.Blocks.FirstBlock);
            line.Inlines.Add(new Run(" edited"));
            Assert.Equal("first edited", new TextRange(line.ContentStart, line.ContentEnd).Text.Trim());
        });
    }

    [Theory]
    [InlineData(1, 32)]
    [InlineData(3, 22)]
    [InlineData(0, 14)]
    public void HeadingSizes_AreVisualValues(int level, double size) => Assert.Equal(size, QuickNoteDocumentFormatting.GetHeadingFontSizeForLevel(level));

    [Fact]
    public void LinkOpening_AllowsOnlySafeHttpUrl()
    {
        Assert.True(QuickNoteDocumentFormatting.IsSafeLinkForOpen("https://example.com", QuickNoteDocumentFormatting.LinkType.Url));
        Assert.False(QuickNoteDocumentFormatting.IsSafeLinkForOpen("file:///C:/note.txt", QuickNoteDocumentFormatting.LinkType.Url));
    }

    [Fact]
    public void ClearListMarkers_MapsSelectionWithoutCountingMarkersAfterCaret()
    {
        const string text = "•\tfirst\n•\tsecond";
        int caret = text.IndexOf("first", StringComparison.Ordinal) + 2;
        QuickNoteRangeEdit edit = QuickNoteDocumentFormatting.GetClearLineMarkerRangeEdit(text, caret, text.Length);

        Assert.Equal(2, edit.CaretOffset);
        Assert.Equal("first\nsecond", edit.InsertText);
    }

    private static void RunSta(Action action)
    {
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception ex) { exception = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (exception is not null) throw exception;
    }
}
