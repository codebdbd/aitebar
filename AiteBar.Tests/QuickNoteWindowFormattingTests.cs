using System.IO;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

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

                window.ClearSelectedFormatting(ClearFormattingScope.All);

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

                window.ClearSelectedFormatting(ClearFormattingScope.All);

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

                window.ClearSelectedFormatting(ClearFormattingScope.All);

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

                window.ClearSelectedFormatting(ClearFormattingScope.All);

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
    public void ClearSelectedFormatting_ConvertsCodeBlockToPlainText()
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
                Section codeBlock = QuickNoteDocumentFormatting.CreateCodeBlockElement("first\nsecond", QuickNoteThemeCatalog.Find(null));
                window.TxtNote.Document.Blocks.Clear();
                window.TxtNote.Document.Blocks.Add(codeBlock);
                window.TxtNote.Selection.Select(codeBlock.ContentStart, codeBlock.ContentEnd);

                window.ClearSelectedFormatting(ClearFormattingScope.All);

                Assert.DoesNotContain(window.TxtNote.Document.Blocks, QuickNoteDocumentFormatting.IsCodeBlock);
                Paragraph[] paragraphs = window.TxtNote.Document.Blocks.OfType<Paragraph>().ToArray();
                Assert.Equal(2, paragraphs.Length);
                Assert.Equal("first\nsecond", QuickNoteDocumentHelper.NormalizeLineEndings(new TextRange(
                    window.TxtNote.Document.ContentStart,
                    window.TxtNote.Document.ContentEnd).Text).Trim());
                Assert.All(paragraphs, paragraph => Assert.Equal(QuickNoteFonts.Default.Source, paragraph.FontFamily.Source));
            }
            finally
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        });
    }

    [Fact]
    public void ApplyTheme_RecolorsRtfLoadedPlainTextButKeepsCodeTextColor()
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
                var plainRun = new Run("plain") { Foreground = System.Windows.Media.Brushes.Black };
                Section codeBlock = QuickNoteDocumentFormatting.CreateCodeBlockElement("code", QuickNoteThemeCatalog.Find("dark"));

                window.TxtNote.Document.Blocks.Clear();
                window.TxtNote.Document.Blocks.Add(new Paragraph(plainRun));
                window.TxtNote.Document.Blocks.Add(codeBlock);

                typeof(QuickNoteWindow)
                    .GetMethod("ApplyTheme", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                    .Invoke(window, [QuickNoteThemeCatalog.Find("dark")]);

                Assert.Equal("#F1F1F1", BrushToHex(plainRun.Foreground));
                Paragraph codeParagraph = codeBlock.Blocks.OfType<Paragraph>().Single();
                Run codeRun = codeParagraph.Inlines.OfType<Run>().Single();
                Assert.Equal(QuickNoteDocumentFormatting.CodeForeground, BrushToHex(codeRun.Foreground));
            }
            finally
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        });
    }

    [Fact]
    public void InsertImage_LeavesCollapsedCaretAndPreservesSurroundingText()
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
                var run = new Run("before after");
                window.TxtNote.Document.Blocks.Clear();
                window.TxtNote.Document.Blocks.Add(new Paragraph(run));
                TextPointer caret = run.ContentStart.GetPositionAtOffset("before ".Length)!;
                window.TxtNote.Selection.Select(caret, caret);

                BitmapSource bitmap = BitmapSource.Create(1, 1, 96, 96, PixelFormats.Bgra32, null, new byte[] { 0, 128, 255, 255 }, 4);
                Assert.True(QuickNoteImageHelper.TryCreateInlineImage(bitmap, out InlineUIContainer? image));
                bool inserted = (bool)typeof(QuickNoteWindow)
                    .GetMethods(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .Single(method => method.Name == "InsertImage" && method.GetParameters().SingleOrDefault()?.ParameterType == typeof(InlineUIContainer))
                    .Invoke(window, [image])!;

                Assert.True(inserted);
                Assert.True(window.TxtNote.Selection.IsEmpty);
                Assert.Equal("before  after", new TextRange(window.TxtNote.Document.ContentStart, window.TxtNote.Document.ContentEnd).Text.Trim());
                Assert.Contains(window.TxtNote.Document.Blocks.OfType<Paragraph>().SelectMany(paragraph => paragraph.Inlines.OfType<InlineUIContainer>()), static _ => true);
            }
            finally
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        });
    }

    [Fact]
    public void DeleteSelectedImage_RemovesOnlyTheImageAndLeavesEditorEditable()
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
                var paragraph = new Paragraph(new Run("before "));
                BitmapSource bitmap = BitmapSource.Create(1, 1, 96, 96, PixelFormats.Bgra32, null, new byte[] { 0, 128, 255, 255 }, 4);
                Assert.True(QuickNoteImageHelper.TryCreateInlineImage(bitmap, out InlineUIContainer? image));
                paragraph.Inlines.Add(image!);
                paragraph.Inlines.Add(new Run(" after"));
                window.TxtNote.Document.Blocks.Clear();
                window.TxtNote.Document.Blocks.Add(paragraph);
                using var interaction = new QuickNoteImageInteractionController(window.TxtNote);
                Assert.True(interaction.TrySelectFromMouseInput(Assert.IsAssignableFrom<System.Windows.Controls.Image>(image!.Child)));
                bool handled = interaction.TryDeleteSelected();

                Assert.True(handled);
                Assert.True(window.TxtNote.Selection.IsEmpty);
                Assert.Equal("before  after", new TextRange(window.TxtNote.Document.ContentStart, window.TxtNote.Document.ContentEnd).Text.Trim());
                Assert.DoesNotContain(paragraph.Inlines.OfType<InlineUIContainer>(), static _ => true);
            }
            finally
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        });
    }

    [Fact]
    public void DeleteSelectedNestedImage_RemovesItWithoutDefaultWpfDeletion()
    {
        RunSta(() =>
        {
            string tempRoot = Path.Combine(Path.GetTempPath(), "AiteBarTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempRoot);

            try
            {
                var settingsService = new AppSettingsService(Path.Combine(tempRoot, "buttons.json"), Path.Combine(tempRoot, "settings.json"));
                using var window = new QuickNoteWindow(new NoOpQuickNotePersistence(), settingsService);
                Assert.True(QuickNoteImageHelper.TryCreateInlineImage(BitmapSource.Create(1, 1, 96, 96, PixelFormats.Bgra32, null, new byte[] { 0, 128, 255, 255 }, 4), out InlineUIContainer? image));
                var bold = new Bold(new Run("before "));
                bold.Inlines.Add(image!);
                bold.Inlines.Add(new Run(" after"));
                window.TxtNote.Document.Blocks.Clear();
                window.TxtNote.Document.Blocks.Add(new Paragraph(bold));
                using var interaction = new QuickNoteImageInteractionController(window.TxtNote);
                Assert.True(interaction.TrySelectFromMouseInput(Assert.IsAssignableFrom<System.Windows.Controls.Image>(image!.Child)));
                bool handled = interaction.TryDeleteSelected();

                Assert.True(handled);
                Assert.Empty(QuickNoteImageHelper.EnumerateImageContainers(window.TxtNote.Document.Blocks));
                Assert.Equal("before  after", new TextRange(window.TxtNote.Document.ContentStart, window.TxtNote.Document.ContentEnd).Text.Trim());
            }
            finally
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        });
    }

    [Fact]
    public void DeleteSelectedImageContainer_RemovesItWithoutTextSelection()
    {
        RunSta(() =>
        {
            string tempRoot = Path.Combine(Path.GetTempPath(), "AiteBarTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempRoot);

            try
            {
                var settingsService = new AppSettingsService(Path.Combine(tempRoot, "buttons.json"), Path.Combine(tempRoot, "settings.json"));
                using var window = new QuickNoteWindow(new NoOpQuickNotePersistence(), settingsService);
                Assert.True(QuickNoteImageHelper.TryCreateInlineImage(BitmapSource.Create(1, 1, 96, 96, PixelFormats.Bgra32, null, new byte[] { 0, 128, 255, 255 }, 4), out InlineUIContainer? image));
                window.TxtNote.Document.Blocks.Clear();
                window.TxtNote.Document.Blocks.Add(new Paragraph(image!));
                window.TxtNote.Selection.Select(window.TxtNote.Document.ContentEnd, window.TxtNote.Document.ContentEnd);
                using var interaction = new QuickNoteImageInteractionController(window.TxtNote);
                Assert.True(interaction.TrySelectFromMouseInput(Assert.IsAssignableFrom<System.Windows.Controls.Image>(image!.Child)));
                bool handled = interaction.TryDeleteSelected();

                Assert.True(handled);
                Assert.Empty(QuickNoteImageHelper.EnumerateImageContainers(window.TxtNote.Document.Blocks));
                Assert.True(window.TxtNote.Selection.IsEmpty);
            }
            finally
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        });
    }

    [Fact]
    public void ClickNewImageContainer_AllowsImmediateDeleteWithoutReload()
    {
        RunSta(() =>
        {
            string tempRoot = Path.Combine(Path.GetTempPath(), "AiteBarTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempRoot);

            try
            {
                var settingsService = new AppSettingsService(Path.Combine(tempRoot, "buttons.json"), Path.Combine(tempRoot, "settings.json"));
                using var window = new QuickNoteWindow(new NoOpQuickNotePersistence(), settingsService);
                Assert.True(QuickNoteImageHelper.TryCreateInlineImage(BitmapSource.Create(1, 1, 96, 96, PixelFormats.Bgra32, null, new byte[] { 0, 128, 255, 255 }, 4), out InlineUIContainer? image));
                window.TxtNote.Document.Blocks.Clear();
                window.TxtNote.Document.Blocks.Add(new Paragraph(image!));

                using var interaction = new QuickNoteImageInteractionController(window.TxtNote);
                Assert.True(interaction.UpdateCursorFromMouseInput(Assert.IsAssignableFrom<System.Windows.Controls.Image>(image!.Child)));
                Assert.Equal(System.Windows.Input.Cursors.Hand, window.TxtNote.Cursor);
                Assert.True(interaction.TrySelectFromMouseInput(Assert.IsAssignableFrom<System.Windows.Controls.Image>(image!.Child)));
                bool handled = interaction.TryDeleteSelected();

                Assert.True(handled);
                Assert.Empty(QuickNoteImageHelper.EnumerateImageContainers(window.TxtNote.Document.Blocks));
            }
            finally
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        });
    }

    [Fact]
    public void BackspaceBeforeImage_DoesNotDeleteTheFollowingImage()
    {
        RunSta(() =>
        {
            string tempRoot = Path.Combine(Path.GetTempPath(), "AiteBarTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempRoot);

            try
            {
                var settingsService = new AppSettingsService(Path.Combine(tempRoot, "buttons.json"), Path.Combine(tempRoot, "settings.json"));
                using var window = new QuickNoteWindow(new NoOpQuickNotePersistence(), settingsService);
                Assert.True(QuickNoteImageHelper.TryCreateInlineImage(BitmapSource.Create(1, 1, 96, 96, PixelFormats.Bgra32, null, new byte[] { 0, 128, 255, 255 }, 4), out InlineUIContainer? image));
                var paragraph = new Paragraph(new Run("before "));
                paragraph.Inlines.Add(image!);
                window.TxtNote.Document.Blocks.Clear();
                window.TxtNote.Document.Blocks.Add(paragraph);
                window.TxtNote.Selection.Select(image!.ElementStart, image.ElementStart);
                using var interaction = new QuickNoteImageInteractionController(window.TxtNote);
                bool handled = interaction.TryDeleteSelected();

                Assert.False(handled);
                Assert.Single(QuickNoteImageHelper.EnumerateImageContainers(window.TxtNote.Document.Blocks));
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

    private static string BrushToHex(System.Windows.Media.Brush brush)
    {
        var color = Assert.IsType<System.Windows.Media.SolidColorBrush>(brush).Color;
        return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
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
