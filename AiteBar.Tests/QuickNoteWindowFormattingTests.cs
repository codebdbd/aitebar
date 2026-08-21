using System.IO;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;

namespace AiteBar.Tests;

[Collection("WpfTestCollection")]
public sealed class QuickNoteWindowFormattingTests
{
    [Fact]
    public void ClearSelectedFormatting_UnwrapsListAndLinkInOneDocumentChange()
    {
        RunSta(() =>
        {
            string tempRoot = Path.Combine(Path.GetTempPath(), "AiteBarTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempRoot);

            try
            {
                var settingsService = new AppSettingsService(
                    Path.Combine(tempRoot, "buttons.json"),
                    Path.Combine(tempRoot, "settings.json"));
                using var window = new QuickNoteWindow(new NoOpQuickNotePersistence(), settingsService);
                var formattedRun = new Run("formatted item");
                var hyperlink = new Hyperlink(new Bold(formattedRun))
                {
                    NavigateUri = new Uri("https://example.com")
                };
                var list = new System.Windows.Documents.List();
                list.ListItems.Add(new ListItem(new Paragraph(hyperlink)));
                window.TxtNote.Document.Blocks.Clear();
                window.TxtNote.Document.Blocks.Add(list);
                window.TxtNote.Selection.Select(
                    formattedRun.ContentStart,
                    formattedRun.ContentEnd);
                Assert.Equal("formatted item", QuickNoteDocumentHelper.RemoveVisualListMarkers(window.TxtNote.Selection.Text));

                int textChangedCount = 0;
                window.TxtNote.TextChanged += (_, _) => textChangedCount++;

                window.ClearSelectedFormatting();

                var paragraph = Assert.IsType<Paragraph>(window.TxtNote.Document.Blocks.FirstBlock);
                Assert.Empty(paragraph.Inlines.OfType<Hyperlink>());
                Assert.Equal("formatted item", window.TxtNote.Selection.Text.Trim());
                Assert.Equal("formatted item", new TextRange(paragraph.ContentStart, paragraph.ContentEnd).Text);
                Assert.Equal(FontWeights.Normal, window.TxtNote.Selection.GetPropertyValue(TextElement.FontWeightProperty));
                Assert.Equal("formatted item", new TextRange(window.TxtNote.Document.ContentStart, window.TxtNote.Document.ContentEnd).Text.Trim());
                Assert.Equal(1, textChangedCount);
            }
            finally
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        });
    }

    [Fact]
    public void ClearSelectedFormatting_PreservesSelectionAcrossMultipleListItems()
    {
        RunSta(() =>
        {
            string tempRoot = Path.Combine(Path.GetTempPath(), "AiteBarTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempRoot);

            try
            {
                var settingsService = new AppSettingsService(
                    Path.Combine(tempRoot, "buttons.json"),
                    Path.Combine(tempRoot, "settings.json"));
                using var window = new QuickNoteWindow(new NoOpQuickNotePersistence(), settingsService);
                var first = new Run("first");
                var second = new Run("second");
                var list = new System.Windows.Documents.List();
                list.ListItems.Add(new ListItem(new Paragraph(first)));
                list.ListItems.Add(new ListItem(new Paragraph(second)));
                window.TxtNote.Document.Blocks.Clear();
                window.TxtNote.Document.Blocks.Add(list);
                window.TxtNote.Selection.Select(first.ContentStart, second.ContentEnd);
                Assert.Equal(
                    "first\nsecond",
                    QuickNoteDocumentHelper.RemoveVisualListMarkers(window.TxtNote.Selection.Text).Trim());

                window.ClearSelectedFormatting();

                Assert.Equal(2, window.TxtNote.Document.Blocks.OfType<Paragraph>().Count());
                Assert.Equal(
                    "first\nsecond",
                    QuickNoteDocumentHelper.NormalizeLineEndings(window.TxtNote.Selection.Text).Trim());
            }
            finally
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        });
    }

    [Fact]
    public void ClearSelectedFormatting_PreservesUnselectedHyperlinkFragments()
    {
        RunSta(() =>
        {
            string tempRoot = Path.Combine(Path.GetTempPath(), "AiteBarTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempRoot);

            try
            {
                var settingsService = new AppSettingsService(
                    Path.Combine(tempRoot, "buttons.json"),
                    Path.Combine(tempRoot, "settings.json"));
                using var window = new QuickNoteWindow(new NoOpQuickNotePersistence(), settingsService);
                var run = new Run("prefix selected suffix");
                var hyperlink = new Hyperlink(run)
                {
                    NavigateUri = new Uri("https://example.com"),
                    Tag = "link:https://example.com"
                };
                var paragraph = new Paragraph(hyperlink);
                window.TxtNote.Document.Blocks.Clear();
                window.TxtNote.Document.Blocks.Add(paragraph);
                window.TxtNote.Selection.Select(
                    run.ContentStart.GetPositionAtOffset("prefix ".Length)!,
                    run.ContentStart.GetPositionAtOffset("prefix selected".Length)!);

                window.ClearSelectedFormatting();

                Hyperlink[] links = paragraph.Inlines.OfType<Hyperlink>().ToArray();
                Assert.Equal(2, links.Length);
                Assert.Equal("prefix ", new TextRange(links[0].ContentStart, links[0].ContentEnd).Text);
                Assert.Equal(" suffix", new TextRange(links[1].ContentStart, links[1].ContentEnd).Text);
                Assert.Equal("selected", window.TxtNote.Selection.Text);
                Assert.Equal(
                    "prefix selected suffix",
                    new TextRange(paragraph.ContentStart, paragraph.ContentEnd).Text);
            }
            finally
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        });
    }

    [Fact]
    public void ClearSelectedFormatting_PreservesNestedFormattingInLinkedFragments()
    {
        RunSta(() =>
        {
            string tempRoot = Path.Combine(Path.GetTempPath(), "AiteBarTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempRoot);

            try
            {
                var settingsService = new AppSettingsService(
                    Path.Combine(tempRoot, "buttons.json"),
                    Path.Combine(tempRoot, "settings.json"));
                using var window = new QuickNoteWindow(new NoOpQuickNotePersistence(), settingsService);
                var prefix = new Run("prefix ");
                var selected = new Run("selected");
                var suffix = new Run(" suffix");
                var strike = new Span(suffix) { TextDecorations = TextDecorations.Strikethrough };
                var hyperlink = new Hyperlink
                {
                    NavigateUri = new Uri("https://example.com"),
                    Tag = "link:https://example.com"
                };
                hyperlink.Inlines.Add(new Bold(prefix));
                hyperlink.Inlines.Add(new Italic(selected));
                hyperlink.Inlines.Add(strike);
                var paragraph = new Paragraph(hyperlink);
                window.TxtNote.Document.Blocks.Clear();
                window.TxtNote.Document.Blocks.Add(paragraph);
                window.TxtNote.Selection.Select(selected.ContentStart, selected.ContentEnd);

                window.ClearSelectedFormatting();

                Hyperlink[] links = paragraph.Inlines.OfType<Hyperlink>().ToArray();
                Assert.Equal(2, links.Length);
                Assert.IsType<Bold>(links[0].Inlines.FirstInline);
                var suffixSpan = Assert.IsType<Span>(links[1].Inlines.FirstInline);
                Assert.Contains(
                    suffixSpan.TextDecorations,
                    decoration => decoration.Location == TextDecorationLocation.Strikethrough);
                Assert.Equal(FontStyles.Normal, window.TxtNote.Selection.GetPropertyValue(TextElement.FontStyleProperty));
                Assert.Equal("prefix selected suffix", new TextRange(paragraph.ContentStart, paragraph.ContentEnd).Text);
                Assert.Equal("prefix selected suffix", new TextRange(window.TxtNote.Document.ContentStart, window.TxtNote.Document.ContentEnd).Text.Trim());
            }
            finally
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        });
    }

    [Fact]
    public void LinkDialog_PreservesSelectedWhitespaceInLinkText()
    {
        Assert.Equal(
            " selected text ",
            QuickNoteLinkDialog.PreserveLinkText(" selected text "));
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
            ExceptionDispatchInfo.Capture(exception).Throw();
        }
    }

    private sealed class NoOpQuickNotePersistence : IQuickNotePersistence
    {
        public string? LastConflictCopyPath => null;
        public bool HasExternalChanges() => false;
        public void Load(FlowDocument document)
        {
        }

        public Task SaveAsync(FlowDocument document) => Task.CompletedTask;
        public Task<string> SaveConflictCopyAsync(FlowDocument document) => Task.FromResult(string.Empty);
        public void OpenInEditor()
        {
        }

        public void OpenConflictCopy()
        {
        }
    }
}
