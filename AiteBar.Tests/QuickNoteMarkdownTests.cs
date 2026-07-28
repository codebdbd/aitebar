using System.Threading;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using AiteBar;

namespace AiteBar.Tests;

public sealed class QuickNoteMarkdownTests
{
    [Fact]
    public void ToggleListMarkers_AddsAndRemovesBulletMarkersForSelectedLines()
    {
        QuickNoteTextEdit added = QuickNoteMarkdown.ToggleListMarkers("one\ntwo\nthree", 0, 7, numbered: false);

        Assert.Equal("- one\n- two\nthree", added.Text);

        QuickNoteTextEdit removed = QuickNoteMarkdown.ToggleListMarkers(added.Text, 0, 11, numbered: false);

        Assert.Equal("one\ntwo\nthree", removed.Text);
    }

    [Fact]
    public void ToggleListMarkers_AddsAndRemovesNumberedMarkersForSelectedLines()
    {
        QuickNoteTextEdit added = QuickNoteMarkdown.ToggleListMarkers("one\ntwo", 0, 7, numbered: true);

        Assert.Equal("1. one\n2. two", added.Text);

        QuickNoteTextEdit removed = QuickNoteMarkdown.ToggleListMarkers(added.Text, 0, added.Text.Length, numbered: true);

        Assert.Equal("one\ntwo", removed.Text);
    }

    [Fact]
    public void ClearLineMarkers_RemovesBulletAndNumberedMarkers()
    {
        QuickNoteTextEdit edit = QuickNoteMarkdown.ClearLineMarkers("- one\n2. two\nplain", 0, 12);

        Assert.Equal("one\ntwo\nplain", edit.Text);
    }

    [Fact]
    public void NormalizeUrlForOpen_TrimsTrailingPunctuationAndAddsHttpsForWww()
    {
        Assert.Equal("https://example.com/path", QuickNoteMarkdown.NormalizeUrlForOpen("https://example.com/path)."));
        Assert.Equal("https://www.example.com", QuickNoteMarkdown.NormalizeUrlForOpen("www.example.com,"));
    }

    [Fact]
    public void MatchUrls_FindsHttpHttpsAndWwwUrls()
    {
        var matches = QuickNoteMarkdown.MatchUrls("See http://a.test, https://b.test/path and www.c.test.")
            .Select(match => match.Value)
            .ToArray();

        Assert.Equal(["http://a.test,", "https://b.test/path", "www.c.test."], matches);
    }

    [Fact]
    public void ToggleListMarkers_PreservesIndentation()
    {
        QuickNoteTextEdit edit = QuickNoteMarkdown.ToggleListMarkers("  one\n  two", 0, 10, numbered: false);

        Assert.Equal("  - one\n  - two", edit.Text);
    }

    [Fact]
    public void ToggleListMarkers_MapsCaretAfterInsertedMarkers()
    {
        QuickNoteTextEdit edit = QuickNoteMarkdown.ToggleListMarkers("one\ntwo", 7, 7, numbered: false);

        Assert.Equal("one\n- two", edit.Text);
        Assert.Equal(9, edit.CaretOffset);
    }

    [Fact]
    public void GetToggleListMarkerRangeEdit_ReplacesSelectedLinesAsSingleSegment()
    {
        QuickNoteRangeEdit edit = QuickNoteMarkdown.GetToggleListMarkerRangeEdit("head\none\ntwo\ntail", 5, 12, numbered: false);

        Assert.Equal(5, edit.StartOffset);
        Assert.Equal("one\ntwo".Length, edit.RemoveLength);
        Assert.Equal("- one\n- two", edit.InsertText);
        Assert.True(edit.CaretOffset > edit.StartOffset);
    }

    [Fact]
    public void GetToggleListMarkerRangeEdit_PreservesOriginalSelectionInsteadOfSelectingWholeLine()
    {
        QuickNoteRangeEdit edit = QuickNoteMarkdown.GetToggleListMarkerRangeEdit("head\none two\ntail", 6, 8, numbered: false);

        Assert.Equal(5, edit.StartOffset);
        Assert.Equal("one two".Length, edit.RemoveLength);
        Assert.Equal("- one two", edit.InsertText);
        Assert.Equal(8, edit.CaretOffset);
        Assert.Equal(2, edit.SelectionLength);
    }

    [Fact]
    public void GetToggleListMarkerRangeEdit_DoesNotIncludeNextLineWhenSelectionEndsAfterLineBreak()
    {
        QuickNoteRangeEdit edit = QuickNoteMarkdown.GetToggleListMarkerRangeEdit("one\ntwo\nthree", 0, "one\ntwo\n".Length, numbered: false);

        Assert.Equal(0, edit.StartOffset);
        Assert.Equal("one\ntwo".Length, edit.RemoveLength);
        Assert.Equal("- one\n- two", edit.InsertText);
        Assert.Equal(2, edit.CaretOffset);
        Assert.Equal("one\n- two".Length, edit.SelectionLength);
    }

    [Fact]
    public void GetClearLineMarkerRangeEdit_ReplacesSelectedLinesAsSingleSegment()
    {
        QuickNoteRangeEdit edit = QuickNoteMarkdown.GetClearLineMarkerRangeEdit("head\n- one\n- two\ntail", 5, 16);

        Assert.Equal(5, edit.StartOffset);
        Assert.Equal("- one\n- two".Length, edit.RemoveLength);
        Assert.Equal("one\ntwo", edit.InsertText);
        Assert.Equal(5, edit.CaretOffset);
        Assert.Equal("one\ntwo".Length, edit.SelectionLength);
    }

    [Fact]
    public void GetClearLineMarkerRangeEdit_PreservesPartialSelectionAfterRemovingMarkers()
    {
        QuickNoteRangeEdit edit = QuickNoteMarkdown.GetClearLineMarkerRangeEdit("head\n- one two\ntail", 7, 10);

        Assert.Equal(5, edit.StartOffset);
        Assert.Equal("- one two".Length, edit.RemoveLength);
        Assert.Equal("one two", edit.InsertText);
        Assert.Equal(5, edit.CaretOffset);
        Assert.Equal(3, edit.SelectionLength);
    }

    [Fact]
    public void ToMarkdown_EscapesLiteralMarkdownCharacters()
    {
        string markdown = RunSta(() =>
        {
            var document = new FlowDocument(new Paragraph(new Run("literal **stars** and `code`")));
            return QuickNoteMarkdown.ToMarkdown(document);
        });

        Assert.Equal(@"literal \*\*stars\*\* and \`code\`", markdown);
    }

    [Fact]
    public void ToMarkdown_EscapesAngleBracketsToPreventHtmlUnderline()
    {
        string markdown = RunSta(() =>
        {
            var document = new FlowDocument(new Paragraph(new Run("plain <u>not underline</u> text")));
            return QuickNoteMarkdown.ToMarkdown(document);
        });

        Assert.Equal(@"plain \<u\>not underline\</u\> text", markdown);
    }

    [Fact]
    public void LoadMarkdown_DoesNotTreatEscapedAngleBracketsAsHtmlUnderline()
    {
        string visibleText = RunSta(() =>
        {
            var document = new FlowDocument();
            QuickNoteMarkdown.LoadMarkdown(document, @"plain \<u\>not underline\<\/u\> text");
            return new TextRange(document.ContentStart, document.ContentEnd).Text.TrimEnd();
        });

        Assert.Equal("plain <u>not underline</u> text", visibleText);
    }

    [Fact]
    public void LoadMarkdown_RendersSupportedInlineFormatting()
    {
        string markdown = RunSta(() =>
        {
            var document = new FlowDocument();
            QuickNoteMarkdown.LoadMarkdown(document, "plain **bold** *italic* `code`");
            return QuickNoteMarkdown.ToMarkdown(document);
        });

        Assert.Equal("plain **bold** *italic* `code`", markdown);
    }

    [Theory]
    [InlineData("***both***")]
    [InlineData("<u>~~under-strike~~</u>")]
    [InlineData("**bold `code`**")]
    [InlineData("[**bold link**](https://example.com)")]
    [InlineData("***bold italic `code`***")]
    [InlineData("~~`code strike`~~")]
    [InlineData("<u>~~`code strike underline`~~</u>")]
    [InlineData("[~~`formatted code link`~~](https://example.com)")]
    public void LoadMarkdown_PreservesNestedFormattingRoundTrip(string source)
    {
        string markdown = RunSta(() =>
        {
            var document = new FlowDocument();
            QuickNoteMarkdown.LoadMarkdown(document, source);
            return QuickNoteMarkdown.ToMarkdown(document);
        });

        Assert.Equal(source, markdown);
    }

    [Fact]
    public void LoadMarkdown_RendersAndSavesHeadings()
    {
        string markdown = RunSta(() =>
        {
            var document = new FlowDocument();
            QuickNoteMarkdown.LoadMarkdown(document, "# Title\n###### Subsection");
            var paragraph = Assert.IsType<Paragraph>(document.Blocks.FirstBlock);
            var firstHeading = Assert.IsType<Span>(paragraph.Inlines.FirstInline);

            Assert.Equal("heading:1", firstHeading.Tag);
            Assert.True(firstHeading.FontSize > 20);

            return QuickNoteMarkdown.ToMarkdown(document);
        });

        Assert.Equal("# Title\n###### Subsection", markdown.Replace("\r\n", "\n"));
    }

    [Fact]
    public void LoadMarkdown_RendersAndSavesBulletListAsFlowDocumentList()
    {
        string markdown = RunSta(() =>
        {
            var document = new FlowDocument();
            QuickNoteMarkdown.LoadMarkdown(document, "- one\n- two");
            var list = Assert.IsType<System.Windows.Documents.List>(document.Blocks.FirstBlock);

            Assert.Equal(TextMarkerStyle.Disc, list.MarkerStyle);
            Assert.Equal(2, list.ListItems.Count);

            return QuickNoteMarkdown.ToMarkdown(document);
        });

        Assert.Equal("- one\n- two", markdown.Replace("\r\n", "\n"));
    }

    [Fact]
    public void LoadMarkdown_PreservesIndentedBulletListMarkers()
    {
        string markdown = RunSta(() =>
        {
            var document = new FlowDocument();
            QuickNoteMarkdown.LoadMarkdown(document, "  - one\n  - two");

            return QuickNoteMarkdown.ToMarkdown(document);
        });

        Assert.Equal("  - one\n  - two", markdown.Replace("\r\n", "\n"));
    }

    [Fact]
    public void LoadMarkdown_RendersAndSavesNumberedListAsFlowDocumentList()
    {
        string markdown = RunSta(() =>
        {
            var document = new FlowDocument();
            QuickNoteMarkdown.LoadMarkdown(document, "1. one\n2. two");
            var list = Assert.IsType<System.Windows.Documents.List>(document.Blocks.FirstBlock);

            Assert.Equal(TextMarkerStyle.Decimal, list.MarkerStyle);
            Assert.Equal(2, list.ListItems.Count);

            return QuickNoteMarkdown.ToMarkdown(document);
        });

        Assert.Equal("1. one\n2. two", markdown.Replace("\r\n", "\n"));
    }

    [Fact]
    public void ToMarkdown_IndentsNestedVisualListsInsideListItems()
    {
        string markdown = RunSta(() =>
        {
            var parent = new System.Windows.Documents.List { MarkerStyle = TextMarkerStyle.Disc };
            var item = new ListItem(new Paragraph(new Run("parent")));
            var child = new System.Windows.Documents.List { MarkerStyle = TextMarkerStyle.Disc };
            child.ListItems.Add(new ListItem(new Paragraph(new Run("child"))));
            item.Blocks.Add(child);
            parent.ListItems.Add(item);
            var document = new FlowDocument(parent);

            return QuickNoteMarkdown.ToMarkdown(document);
        });

        Assert.Equal("- parent\n  - child", markdown.Replace("\r\n", "\n"));
    }

    [Fact]
    public void HeadingFontSizes_DecreaseByHeadingLevel()
    {
        Assert.Equal(32, QuickNoteMarkdown.GetHeadingFontSizeForLevel(1));
        Assert.Equal(26, QuickNoteMarkdown.GetHeadingFontSizeForLevel(2));
        Assert.Equal(22, QuickNoteMarkdown.GetHeadingFontSizeForLevel(3));
        Assert.Equal(18, QuickNoteMarkdown.GetHeadingFontSizeForLevel(4));
        Assert.Equal(16, QuickNoteMarkdown.GetHeadingFontSizeForLevel(5));
        Assert.Equal(15, QuickNoteMarkdown.GetHeadingFontSizeForLevel(6));
        Assert.Equal(14, QuickNoteMarkdown.GetHeadingFontSizeForLevel(0));
        Assert.True(QuickNoteMarkdown.GetHeadingFontSizeForLevel(1) > QuickNoteMarkdown.GetHeadingFontSizeForLevel(2));
        Assert.True(QuickNoteMarkdown.GetHeadingFontSizeForLevel(2) > QuickNoteMarkdown.GetHeadingFontSizeForLevel(3));
        Assert.True(QuickNoteMarkdown.GetHeadingFontSizeForLevel(3) > QuickNoteMarkdown.GetHeadingFontSizeForLevel(4));
        Assert.True(QuickNoteMarkdown.GetHeadingFontSizeForLevel(4) > QuickNoteMarkdown.GetHeadingFontSizeForLevel(5));
        Assert.True(QuickNoteMarkdown.GetHeadingFontSizeForLevel(5) > QuickNoteMarkdown.GetHeadingFontSizeForLevel(6));
        Assert.True(QuickNoteMarkdown.GetHeadingFontSizeForLevel(6) > QuickNoteMarkdown.GetHeadingFontSizeForLevel(0));
    }

    [Fact]
    public void ToMarkdown_SavesVisuallyFormattedHeadingWithoutVisibleMarkdownMarker()
    {
        string markdown = RunSta(() =>
        {
            var headingRun = new Run("Visible title")
            {
                FontSize = QuickNoteMarkdown.GetHeadingFontSizeForLevel(1),
                FontWeight = FontWeights.SemiBold
            };
            var paragraph = new Paragraph();
            paragraph.Inlines.Add(headingRun);
            paragraph.Inlines.Add(new LineBreak());
            paragraph.Inlines.Add(new Run("Body"));
            var document = new FlowDocument(paragraph);

            return QuickNoteMarkdown.ToMarkdown(document);
        });

        Assert.Equal("# Visible title\nBody", markdown.Replace("\r\n", "\n"));
    }

    [Fact]
    public void ToMarkdown_DoesNotSaveHeadingWhenLoadedHeadingTextWasResetToBodySize()
    {
        string markdown = RunSta(() =>
        {
            var bodyRun = new Run("Visible title")
            {
                FontSize = QuickNoteMarkdown.GetHeadingFontSizeForLevel(0),
                FontWeight = FontWeights.Normal
            };
            var headingSpan = new Span(bodyRun)
            {
                Tag = "heading:1",
                FontSize = QuickNoteMarkdown.GetHeadingFontSizeForLevel(1),
                FontWeight = FontWeights.SemiBold
            };
            var document = new FlowDocument(new Paragraph(headingSpan));

            return QuickNoteMarkdown.ToMarkdown(document);
        });

        Assert.Equal("Visible title", markdown);
    }

    [Fact]
    public void LoadMarkdown_RendersAndSavesMarkdownLinks()
    {
        string markdown = RunSta(() =>
        {
            var document = new FlowDocument();
            QuickNoteMarkdown.LoadMarkdown(document, "Open [AiteBar](https://example.com/path)");
            var paragraph = Assert.IsType<Paragraph>(document.Blocks.FirstBlock);
            var hyperlink = paragraph.Inlines.OfType<Hyperlink>().Single();

            Assert.Equal(new Uri("https://example.com/path"), hyperlink.NavigateUri);
            Assert.Equal("AiteBar", new TextRange(hyperlink.ContentStart, hyperlink.ContentEnd).Text);

            return QuickNoteMarkdown.ToMarkdown(document);
        });

        Assert.Equal("Open [AiteBar](https://example.com/path)", markdown);
    }

    [Fact]
    public void LoadMarkdown_RendersAndSavesStrikethrough()
    {
        string markdown = RunSta(() =>
        {
            var document = new FlowDocument();
            QuickNoteMarkdown.LoadMarkdown(document, "plain ~~done~~");
            var paragraph = Assert.IsType<Paragraph>(document.Blocks.FirstBlock);
            var strike = paragraph.Inlines.OfType<Span>().Single();

            Assert.Contains(strike.TextDecorations, decoration => decoration.Location == TextDecorationLocation.Strikethrough);

            return QuickNoteMarkdown.ToMarkdown(document);
        });

        Assert.Equal("plain ~~done~~", markdown);
    }

    [Fact]
    public void GetHeadingRangeEdit_AddsReplacesAndRemovesHeadingMarkers()
    {
        QuickNoteRangeEdit added = QuickNoteMarkdown.GetHeadingRangeEdit("one\ntwo", 0, 7, 2);

        string withHeadings = "one\ntwo".Remove(added.StartOffset, added.RemoveLength).Insert(added.StartOffset, added.InsertText);
        Assert.Equal("## one\n## two", withHeadings);

        QuickNoteRangeEdit replaced = QuickNoteMarkdown.GetHeadingRangeEdit(withHeadings, 0, withHeadings.Length, 4);
        string replacedText = withHeadings.Remove(replaced.StartOffset, replaced.RemoveLength).Insert(replaced.StartOffset, replaced.InsertText);
        Assert.Equal("#### one\n#### two", replacedText);

        QuickNoteRangeEdit removed = QuickNoteMarkdown.GetHeadingRangeEdit(replacedText, 0, replacedText.Length, 0);
        string bodyText = replacedText.Remove(removed.StartOffset, removed.RemoveLength).Insert(removed.StartOffset, removed.InsertText);
        Assert.Equal("one\ntwo", bodyText);
    }

    [Fact]
    public void ToMarkdown_PreservesUnderlineWithHtmlUnderlineMarker()
    {
        string markdown = RunSta(() =>
        {
            var span = new Span(new Run("underlined"))
            {
                TextDecorations = TextDecorations.Underline
            };
            var document = new FlowDocument(new Paragraph(span));
            return QuickNoteMarkdown.ToMarkdown(document);
        });

        Assert.Equal("<u>underlined</u>", markdown);
    }

    [Fact]
    public void LoadMarkdown_RendersHtmlUnderlineMarker()
    {
        string markdown = RunSta(() =>
        {
            var document = new FlowDocument();
            QuickNoteMarkdown.LoadMarkdown(document, "plain <u>underlined</u>");
            return QuickNoteMarkdown.ToMarkdown(document);
        });

        Assert.Equal("plain <u>underlined</u>", markdown);
    }

    [Fact]
    public void LoadMarkdown_DoesNotTreatEscapedMarkersAsFormatting()
    {
        string visibleText = RunSta(() =>
        {
            var document = new FlowDocument();
            QuickNoteMarkdown.LoadMarkdown(document, @"literal \*\*not bold\*\*");
            return new TextRange(document.ContentStart, document.ContentEnd).Text.TrimEnd();
        });

        Assert.Equal("literal **not bold**", visibleText);
    }

    [Theory]
    [InlineData("https://example.com", 0, true)]
    [InlineData("file:///C:/Windows/System32/calc.exe", 0, false)]
    [InlineData("javascript:alert(1)", 0, false)]
    [InlineData("mailto:user@example.com", 1, true)]
    [InlineData("mailto:user@example.com\"&calc.exe", 1, false)]
    [InlineData("tel:+380 (67) 123-45-67", 2, true)]
    [InlineData("tel:../../calc.exe|cmd", 2, false)]
    public void IsSafeLinkForOpen_AllowsOnlyExpectedSchemePayloads(
        string link,
        int type,
        bool expected)
    {
        Assert.Equal(
            expected,
            QuickNoteMarkdown.IsSafeLinkForOpen(link, (QuickNoteMarkdown.LinkType)type));
    }

    [Fact]
    public void ToMarkdown_PreservesMultipleLines()
    {
        string markdown = RunSta(() =>
        {
            var paragraph = new Paragraph();
            paragraph.Inlines.Add(new Run("one"));
            paragraph.Inlines.Add(new LineBreak());
            paragraph.Inlines.Add(new Run("two"));
            var document = new FlowDocument(paragraph);
            return QuickNoteMarkdown.ToMarkdown(document);
        });

        Assert.Equal("one\ntwo", markdown.Replace("\r\n", "\n"));
    }

    private static T RunSta<T>(Func<T> action)
    {
        T? result = default;
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try
            {
                result = action();
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

        return result!;
    }
}
