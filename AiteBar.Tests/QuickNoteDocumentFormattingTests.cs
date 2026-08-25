using System.Threading;
using System.Windows;
using System.Windows.Controls;
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
            Assert.Equal(new Thickness(0), section.Padding);
            Assert.Equal(QuickNoteDocumentFormatting.CodeBackground, ((System.Windows.Media.SolidColorBrush)section.Background).Color.ToString().Replace("#FF", "#"));

            BlockUIContainer header = Assert.IsType<BlockUIContainer>(section.Blocks.FirstBlock);
            Assert.True(QuickNoteDocumentFormatting.IsCodeHeader(header));
            Grid grid = Assert.IsType<Grid>(header.Child);
            Assert.Equal(20, grid.Height);
            Assert.Equal(QuickNoteDocumentFormatting.CodeHeaderBackground, ((System.Windows.Media.SolidColorBrush)grid.Background).Color.ToString().Replace("#FF", "#"));
            TextBlock label = Assert.IsType<TextBlock>(grid.Children[0]);
            Assert.Equal("code", label.Text);
            Button copyButton = Assert.IsType<Button>(grid.Children[1]);
            Assert.Equal(QuickNoteDocumentFormatting.CodeCopyLink, copyButton.Tag);
            Assert.True(copyButton.OverridesDefaultStyle);
            Assert.Equal(System.Windows.Media.Colors.Transparent, ((System.Windows.Media.SolidColorBrush)copyButton.Background).Color);
            Assert.NotNull(copyButton.Template);

            Paragraph firstLine = Assert.IsType<Paragraph>(header.NextBlock);
            Assert.Equal(QuickNoteFonts.Code.Source, firstLine.FontFamily.Source);
            firstLine.Inlines.Add(new Run(" edited"));
            Assert.Equal(
                "first edited\nsecond",
                QuickNoteDocumentHelper.NormalizeLineEndings(QuickNoteDocumentFormatting.GetCodeBlockText(section)));
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
    public void RtfAdapter_LeavesPlainMarkdownFencesAsPlainText()
    {
        RunSta(() =>
        {
            var source = new FlowDocument();
            source.Blocks.Add(new Paragraph(new Run("```code")));
            source.Blocks.Add(new Paragraph(new Run("var answer = 42;")));
            source.Blocks.Add(new Paragraph(new Run("```")));

            FlowDocument exported = QuickNoteRtfAdapter.CreateExportDocument(source);
            QuickNoteRtfAdapter.RestoreCodeBlocksFromFences(exported);

            Assert.DoesNotContain(exported.Blocks, QuickNoteDocumentFormatting.IsCodeBlock);
            Assert.Equal("```code\nvar answer = 42;\n```", QuickNoteDocumentHelper.NormalizeLineEndings(
                new TextRange(exported.ContentStart, exported.ContentEnd).Text).Trim());
        });
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
