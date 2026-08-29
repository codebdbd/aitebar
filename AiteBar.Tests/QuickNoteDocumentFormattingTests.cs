using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using AiteBar;

namespace AiteBar.Tests;

public sealed class QuickNoteDocumentFormattingTests
{
    [Theory]
    [InlineData("lemon")]
    [InlineData("dark")]
    public void TaskCheckmark_VisibleStrokeIsCenteredInsideBox(string themeId)
    {
        RunSta(() =>
        {
            var checkbox = (CheckBox)QuickNoteDocumentFormatting.CreateTaskCheckbox(true, null, QuickNoteThemeCatalog.Find(themeId)).Child;
            checkbox.Measure(new Size(40, 30));
            checkbox.Arrange(new Rect(0, 0, 40, 30));
            checkbox.UpdateLayout();
            var border = Assert.IsType<Border>(checkbox.Template.FindName("BoxBorder", checkbox));
            var glyph = Assert.IsType<System.Windows.Shapes.Path>(checkbox.Template.FindName("CheckGlyph", checkbox));
            var pen = new Pen(glyph.Stroke, glyph.StrokeThickness)
            {
                StartLineCap = glyph.StrokeStartLineCap, EndLineCap = glyph.StrokeEndLineCap,
                LineJoin = glyph.StrokeLineJoin
            };
            Rect stroke = glyph.RenderedGeometry.GetRenderBounds(pen);
            Rect bounds = glyph.TransformToAncestor(border).TransformBounds(stroke);
            Assert.InRange(Math.Abs(bounds.Left + bounds.Width / 2 - border.ActualWidth / 2), 0, 0.55);
            Assert.InRange(Math.Abs(bounds.Top + bounds.Height / 2 - border.ActualHeight / 2), 0, 0.55);
            Assert.True(bounds.Left >= 0 && bounds.Top >= 0 && bounds.Right <= border.ActualWidth && bounds.Bottom <= border.ActualHeight);
            string? directory = Environment.GetEnvironmentVariable("AITEBAR_QUICKNOTE_RENDER_DIR");
            if (!string.IsNullOrWhiteSpace(directory))
            {
                System.IO.Directory.CreateDirectory(directory);
                var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap(160, 120, 384, 384, PixelFormats.Pbgra32);
                bitmap.Render(checkbox);
                var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
                encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
                using var stream = System.IO.File.Create(System.IO.Path.Combine(directory, $"quicknote-checkmark-{themeId}.png"));
                encoder.Save(stream);
            }
        });
    }
    [Fact]
    public void CodeBlock_IsNativeEditableFlowDocumentSection()
    {
        RunSta(() =>
        {
            QuickNoteTheme darkTheme = QuickNoteThemeCatalog.Find(null);
            Section section = QuickNoteDocumentFormatting.CreateCodeBlockElement("first\nsecond", darkTheme);
            Assert.Equal(2, section.Blocks.OfType<Paragraph>().Count());
            Assert.Equal(new Thickness(0, 0, 0, 8), section.Padding);
            Assert.Equal(darkTheme.CodeBackground, BrushToHex(section.Background));

            BlockUIContainer header = Assert.IsType<BlockUIContainer>(section.Blocks.FirstBlock);
            Assert.True(QuickNoteDocumentFormatting.IsCodeHeader(header));
            Assert.Equal(new Thickness(0, 0, 0, 8), header.Padding);
            Grid grid = Assert.IsType<Grid>(header.Child);
            Assert.Equal(20, grid.Height);
            Assert.Equal(QuickNoteThemeCatalog.GetCodeHeaderBackground(darkTheme), BrushToHex(grid.Background));
            TextBlock label = Assert.IsType<TextBlock>(grid.Children[0]);
            Assert.Equal("code", label.Text);
            Assert.Equal(darkTheme.CodeText, BrushToHex(label.Foreground));
            Button copyButton = Assert.IsType<Button>(grid.Children[1]);
            Assert.Equal(QuickNoteDocumentFormatting.CodeCopyLink, copyButton.Tag);
            Assert.Equal(darkTheme.CodeText, BrushToHex(copyButton.Foreground));
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
    public void CodeHeader_ReappliesOnlyDocumentThemeRoles()
    {
        RunSta(() =>
        {
            QuickNoteTheme darkTheme = QuickNoteThemeCatalog.Find("dark");
            QuickNoteTheme roseTheme = QuickNoteThemeCatalog.Find("rose");
            Section section = QuickNoteDocumentFormatting.CreateCodeBlockElement("value", darkTheme);
            BlockUIContainer header = Assert.IsType<BlockUIContainer>(section.Blocks.FirstBlock);

            QuickNoteDocumentFormatting.ApplyCodeHeaderTheme(header, roseTheme);
            Assert.Equal(new Thickness(0, 0, 0, 8), header.Padding);

            Grid grid = Assert.IsType<Grid>(header.Child);
            TextBlock label = Assert.IsType<TextBlock>(grid.Children[0]);
            Button copyButton = Assert.IsType<Button>(grid.Children[1]);
            Assert.Equal(QuickNoteThemeCatalog.GetCodeHeaderBackground(roseTheme), BrushToHex(grid.Background));
            Assert.Equal(roseTheme.CodeText, BrushToHex(label.Foreground));
            Assert.Equal(roseTheme.CodeText, BrushToHex(copyButton.Foreground));
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

    [Fact]
    public void QuoteBlock_IsProperlyIdentifiedAndNotTreatedAsCodeBlock()
    {
        RunSta(() =>
        {
            var theme = QuickNoteThemeCatalog.Find(null);
            Section quote = QuickNoteDocumentFormatting.CreateQuoteBlockElement("first quote line\nsecond quote line", theme);

            Assert.True(QuickNoteDocumentFormatting.IsQuoteBlock(quote));
            Assert.Equal(new Thickness(0, 8, 0, 8), quote.Padding);
            Assert.False(QuickNoteDocumentFormatting.IsCodeBlock(quote));
            Assert.False(QuickNoteDocumentFormatting.IsDividerBlock(quote));
            Assert.Equal("quote", quote.Tag);
            Assert.Equal(new Thickness(3, 0, 0, 0), quote.BorderThickness);
            Assert.Equal("first quote line\nsecond quote line", QuickNoteDocumentHelper.NormalizeLineEndings(QuickNoteDocumentFormatting.GetQuoteBlockText(quote)));
        });
    }

    [Fact]
    public void DividerBlock_IsProperlyIdentifiedAndNotTreatedAsCodeBlock()
    {
        RunSta(() =>
        {
            var theme = QuickNoteThemeCatalog.Find(null);
            Section divider = QuickNoteDocumentFormatting.CreateDividerElement(theme);

            Assert.True(QuickNoteDocumentFormatting.IsDividerBlock(divider));
            Assert.False(QuickNoteDocumentFormatting.IsCodeBlock(divider));
            Assert.False(QuickNoteDocumentFormatting.IsQuoteBlock(divider));
            Assert.Equal("divider", divider.Tag);
        });
    }

    [Fact]
    public void InlineTheme_PreservesUserStrikethroughOnHyperlinks()
    {
        RunSta(() =>
        {
            var hyperlink = new Hyperlink(new Run("struck link"))
            {
                NavigateUri = new Uri("https://example.com"),
                TextDecorations = TextDecorations.Strikethrough
            };
            var paragraph = new Paragraph(hyperlink);
            var theme = QuickNoteThemeCatalog.Find("dark");

            QuickNoteDocumentFormatting.ApplyInlineTheme(
                paragraph.Inlines,
                QuickNoteBrush.FromHex(theme.Text),
                QuickNoteBrush.FromHex(theme.CodeBackground),
                QuickNoteBrush.FromHex(theme.CodeText),
                QuickNoteBrush.FromHex(theme.Link));

            Assert.Contains(hyperlink.TextDecorations, decoration =>
                decoration.Location == TextDecorationLocation.Strikethrough);
            Assert.Contains(hyperlink.TextDecorations, decoration =>
                decoration.Location == TextDecorationLocation.Underline);
        });
    }

    [Fact]
    public void RtfAdapter_TaskWithImage_PreservesTaskAndDoesNotMoveSourceControls()
    {
        RunSta(() =>
        {
            var theme = QuickNoteThemeCatalog.Find("dark");
            Assert.True(QuickNoteImageHelper.TryCreateInlineImage(
                System.Windows.Media.Imaging.BitmapSource.Create(
                    1, 1, 96, 96, PixelFormats.Bgra32, null, new byte[] { 0, 128, 255, 255 }, 4),
                out InlineUIContainer? image));

            var task = new Paragraph();
            task.Inlines.Add(QuickNoteDocumentFormatting.CreateTaskCheckbox(false, null, theme));
            task.Inlines.Add(new Run("photo task"));
            task.Inlines.Add(image!);
            var source = new FlowDocument(task);

            FlowDocument exported = QuickNoteRtfAdapter.CreateExportDocument(source);

            Assert.True(QuickNoteDocumentFormatting.IsTaskParagraph(task, out _, out _, out _));
            Assert.Same(image, task.Inlines.LastInline);
            var exportedTask = Assert.IsType<Paragraph>(exported.Blocks.FirstBlock);
            Assert.StartsWith("[ ] photo task", new TextRange(exportedTask.ContentStart, exportedTask.ContentEnd).Text.Trim());
            Assert.DoesNotContain(exportedTask.Inlines, inline => inline is InlineUIContainer);

            QuickNoteRtfAdapter.RestoreEmbeddedImages(exported);
            QuickNoteRtfAdapter.RestoreTaskItems(exported, theme);
            Assert.True(QuickNoteDocumentFormatting.IsTaskParagraph(
                Assert.IsType<Paragraph>(exported.Blocks.FirstBlock), out bool isChecked, out _, out _));
            Assert.False(isChecked);
        });
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

    private static string BrushToHex(System.Windows.Media.Brush brush)
    {
        var color = Assert.IsType<System.Windows.Media.SolidColorBrush>(brush).Color;
        return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
    }
}
