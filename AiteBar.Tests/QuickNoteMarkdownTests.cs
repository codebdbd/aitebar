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
