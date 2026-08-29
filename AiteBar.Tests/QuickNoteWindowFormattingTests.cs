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
    public Task Formatting_IsOneUndoUnitAndPreservesTheSelectedTextAndCaretRange() =>
        QuickNoteWindowCloseTests.RunStaAsync(async () =>
        {
            string tempRoot = Path.Combine(Path.GetTempPath(), "AiteBarTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempRoot);
            try
            {
                var settings = new AppSettingsService(Path.Combine(tempRoot, "buttons.json"), Path.Combine(tempRoot, "settings.json"));
                settings.UpdateSettings(s => s.QuickNotePinned = true);
                using var window = new QuickNoteWindow(new NoOpQuickNotePersistence(), settings)
                {
                    WindowStartupLocation = WindowStartupLocation.Manual,
                    Left = -2000,
                    Top = -2000,
                    ShowActivated = false
                };
                var closed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                window.Closed += (_, _) => closed.TrySetResult();
                try
                {
                    window.Show();
                    await System.Windows.Threading.Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                    window.TxtNote.IsUndoEnabled = false;
                    var run = new Run("alpha beta gamma");
                    window.TxtNote.Document.Blocks.Clear();
                    window.TxtNote.Document.Blocks.Add(new Paragraph(run));
                    window.TxtNote.IsUndoEnabled = true;
                    TextPointer start = run.ContentStart.GetPositionAtOffset(6)!;
                    TextPointer end = run.ContentStart.GetPositionAtOffset(10)!;
                    window.TxtNote.Selection.Select(start, end);

                    typeof(QuickNoteWindow).GetMethod("ToggleFormatting", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                        .Invoke(window, [TextElement.FontWeightProperty, FontWeights.Bold, FontWeights.Normal]);

                    Assert.Equal("beta", window.TxtNote.Selection.Text);
                    Assert.Equal(FontWeights.Bold, window.TxtNote.Selection.GetPropertyValue(TextElement.FontWeightProperty));
                    Assert.True(window.TxtNote.CanUndo);
                    window.TxtNote.Undo();
                    Assert.Equal(FontWeights.Normal, new TextRange(start, end).GetPropertyValue(TextElement.FontWeightProperty));
                    Assert.False(window.TxtNote.CanUndo);
                    window.TxtNote.Redo();
                    Assert.Equal(FontWeights.Bold, new TextRange(start, end).GetPropertyValue(TextElement.FontWeightProperty));
                }
                finally
                {
                    window.Close();
                    await closed.Task.WaitAsync(TimeSpan.FromSeconds(5));
                }
            }
            finally
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ListFormatting_KeepsLinePositionsAndUsesCompactIndent(bool numbered)
    {
        RunSta(() =>
        {
            string tempRoot = Path.Combine(Path.GetTempPath(), "AiteBarTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempRoot);
            try
            {
                var settings = new AppSettingsService(Path.Combine(tempRoot, "buttons.json"), Path.Combine(tempRoot, "settings.json"));
                using var window = new QuickNoteWindow(new NoOpQuickNotePersistence(), settings);
                window.EnsureDocumentLoadedForFirstPaint();
                var editor = window.TxtNote;
                editor.Document.Blocks.Clear();
                foreach (string line in new[] { "Before the list", "First item", "Second item", "After the list" })
                    editor.Document.Blocks.Add(new Paragraph(new Run(line)));
                var root = Assert.IsAssignableFrom<FrameworkElement>(window.Content);
                root.Measure(new Size(460, 320));
                root.Arrange(new Rect(0, 0, 460, 320));
                root.UpdateLayout();
                window.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
                root.UpdateLayout();
                editor.IsUndoEnabled = false;
                editor.IsUndoEnabled = true;
                var paragraphs = QuickNoteTaskListController.EnumerateAllParagraphs(editor.Document).ToArray();
                Rect[] originalRects = paragraphs.Select(p => p.ContentStart.GetCharacterRect(LogicalDirection.Forward)).ToArray();
                editor.Selection.Select(paragraphs[1].ContentStart, paragraphs[2].ContentEnd);
                var toolbar = Assert.IsType<System.Windows.Controls.StackPanel>(window.FindName("FormattingToolbar"));
                var button = Assert.IsType<System.Windows.Controls.Button>(toolbar.Children[numbered ? 3 : 2]);
                button.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                root.UpdateLayout();
                for (int replay = 0; replay < 2; replay++)
                {
                    var list = Assert.Single(editor.Document.Blocks.OfType<System.Windows.Documents.List>());
                    Assert.Equal(2, list.ListItems.Count);
                    Assert.Equal(new Thickness(0), list.Margin);
                    Assert.Equal(new Thickness(24, 0, 0, 0), list.Padding);
                    Assert.Equal(6, list.MarkerOffset);
                    paragraphs = QuickNoteTaskListController.EnumerateAllParagraphs(editor.Document).ToArray();
                    for (int i = 0; i < paragraphs.Length; i++)
                    {
                        Rect current = paragraphs[i].ContentStart.GetCharacterRect(LogicalDirection.Forward);
                        Assert.InRange(Math.Abs(current.Y - originalRects[i].Y), 0, 1);
                        Assert.InRange(Math.Abs(current.X - originalRects[i].X - (i is 1 or 2 ? 24 : 0)), 0, 1);
                    }
                    editor.Undo();
                    Assert.Empty(editor.Document.Blocks.OfType<System.Windows.Documents.List>());
                    Assert.True(editor.CanRedo);
                    editor.Redo();
                    root.UpdateLayout();
                }
                string? renderDirectory = Environment.GetEnvironmentVariable("AITEBAR_QUICKNOTE_RENDER_DIR");
                if (!string.IsNullOrWhiteSpace(renderDirectory))
                {
                    Directory.CreateDirectory(renderDirectory);
                    var bitmap = new RenderTargetBitmap(460, 320, 96, 96, PixelFormats.Pbgra32);
                    bitmap.Render(root);
                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(bitmap));
                    using var stream = File.Create(Path.Combine(renderDirectory, $"quicknote-list-{(numbered ? "numbered" : "bullets")}.png"));
                    encoder.Save(stream);
                }
            }
            finally { Directory.Delete(tempRoot, recursive: true); }
        });
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void TaskCheckbox_DeleteUndoRedo_RestoresTemplateAndClickBehavior(bool isChecked, bool nested)
    {
        RunSta(() =>
        {
            string tempRoot = Path.Combine(Path.GetTempPath(), "AiteBarTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempRoot);
            try
            {
                var settings = new AppSettingsService(Path.Combine(tempRoot, "buttons.json"), Path.Combine(tempRoot, "settings.json"));
                using var window = new QuickNoteWindow(new NoOpQuickNotePersistence(), settings);
                window.EnsureDocumentLoadedForFirstPaint();
                var root = Assert.IsAssignableFrom<FrameworkElement>(window.Content);
                root.Measure(new Size(580, 430));
                root.Arrange(new Rect(0, 0, 580, 430));
                root.UpdateLayout();
                window.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
                var editor = window.TxtNote;
                editor.IsUndoEnabled = false;
                editor.Document.Blocks.Clear();
                var paragraph = new Paragraph(new Run("Keep this task") { TextDecorations = TextDecorations.Underline });
                editor.Document.Blocks.Add(nested ? new Section(paragraph) : paragraph);
                QuickNoteDocumentFormatting.ToggleTaskParagraph(paragraph, null, QuickNoteThemeCatalog.Find(null));
                QuickNoteDocumentFormatting.ApplyTaskFormattingToParagraph(paragraph, isChecked, QuickNoteThemeCatalog.Find(null));
                window.ConnectTaskItemEvents(editor.Document);
                editor.IsUndoEnabled = true;
                var original = Assert.IsType<InlineUIContainer>(paragraph.Inlines.FirstInline);
                editor.Selection.Select(original.ElementStart, original.ElementEnd);
                editor.Selection.Text = string.Empty;
                Assert.IsType<Run>(paragraph.Inlines.FirstInline);

                for (int cycle = 0; cycle < 2; cycle++)
                {
                    Assert.True(editor.CanUndo);
                    editor.Undo();
                    paragraph = Assert.Single(QuickNoteTaskListController.EnumerateAllParagraphs(editor.Document));
                    Assert.True(paragraph.Inlines.FirstInline is InlineUIContainer,
                        string.Join(" | ", paragraph.Inlines.Select(i => i.GetType().Name + ":" + (i is Run r ? r.Text : ""))));
                    var restored = Assert.IsType<InlineUIContainer>(paragraph.Inlines.FirstInline);
                    var checkbox = Assert.IsType<System.Windows.Controls.CheckBox>(restored.Child);
                    checkbox.ApplyTemplate();
                    var border = Assert.IsType<System.Windows.Controls.Border>(checkbox.Template.FindName("BoxBorder", checkbox));
                    var glyph = Assert.IsType<System.Windows.Shapes.Path>(checkbox.Template.FindName("CheckGlyph", checkbox));
                    Assert.Equal(isChecked, checkbox.IsChecked);
                    Assert.Equal(isChecked ? Visibility.Visible : Visibility.Collapsed, glyph.Visibility);
                    if (!isChecked) Assert.Equal(Colors.Transparent, Assert.IsType<SolidColorBrush>(border.Background).Color);
                    Assert.True(editor.CanRedo);
                    editor.Redo();
                    paragraph = Assert.Single(QuickNoteTaskListController.EnumerateAllParagraphs(editor.Document));
                    Assert.IsType<Run>(paragraph.Inlines.FirstInline);
                }

                editor.Undo();
                paragraph = Assert.Single(QuickNoteTaskListController.EnumerateAllParagraphs(editor.Document));
                var finalCheckbox = Assert.IsType<System.Windows.Controls.CheckBox>(Assert.IsType<InlineUIContainer>(paragraph.Inlines.FirstInline).Child);
                finalCheckbox.ClickMode = System.Windows.Controls.ClickMode.Press;
                finalCheckbox.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left)
                {
                    RoutedEvent = Mouse.MouseDownEvent
                });
                Assert.Equal(!isChecked, finalCheckbox.IsChecked);
                var run = Assert.IsType<Run>(paragraph.Inlines.LastInline);
                Assert.Equal("Keep this task", run.Text);
                Assert.Contains(run.TextDecorations, d => d.Location == TextDecorationLocation.Underline);
                Assert.Equal(!isChecked, run.TextDecorations.Any(d => d.Location == TextDecorationLocation.Strikethrough));
            }
            finally { Directory.Delete(tempRoot, recursive: true); }
        });
    }

    [Theory]
    [InlineData(MouseButton.Left)]
    [InlineData(MouseButton.Right)]
    public void TaskCheckbox_MouseRouteIsNotConsumedAsImageSelection(MouseButton button)
    {
        RunSta(() =>
        {
            string tempRoot = Path.Combine(Path.GetTempPath(), "AiteBarTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempRoot);
            try
            {
                var settings = new AppSettingsService(Path.Combine(tempRoot, "buttons.json"), Path.Combine(tempRoot, "settings.json"));
                using var window = new QuickNoteWindow(new NoOpQuickNotePersistence(), settings);
                window.EnsureDocumentLoadedForFirstPaint();
                var paragraph = new Paragraph(new Run("Task to complete"));
                window.TxtNote.Document.Blocks.Clear();
                window.TxtNote.Document.Blocks.Add(paragraph);
                QuickNoteDocumentFormatting.ToggleTaskParagraph(paragraph, null, QuickNoteThemeCatalog.Find(null));
                window.ConnectTaskItemEvents(window.TxtNote.Document);
                var container = Assert.IsType<InlineUIContainer>(paragraph.Inlines.FirstInline);
                var checkbox = Assert.IsType<System.Windows.Controls.CheckBox>(container.Child);
                checkbox.ApplyTemplate();
                var border = Assert.IsType<System.Windows.Controls.Border>(checkbox.Template.FindName("BoxBorder", checkbox));
                window.TxtNote.Selection.Select(paragraph.ContentEnd, paragraph.ContentEnd);

                // Press mode exercises ButtonBase's native mouse-to-toggle route without
                // depending on the user's physical mouse button state in a headless test.
                checkbox.ClickMode = System.Windows.Controls.ClickMode.Press;
                for (int click = 0; click < 2; click++)
                {
                    var args = new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, button)
                    {
                        RoutedEvent = Mouse.PreviewMouseDownEvent
                    };
                    border.RaiseEvent(args);
                    Assert.False(args.Handled);
                    Assert.False(window.ImageInteractionController.HasSelectedImage);
                    Assert.True(window.TxtNote.Selection.IsEmpty);
                    args.RoutedEvent = Mouse.MouseDownEvent;
                    border.RaiseEvent(args);
                    bool expected = button == MouseButton.Left && click == 0;
                    Assert.Equal(expected, checkbox.IsChecked);
                    Assert.Equal(QuickNoteTags.Task(expected), container.Tag);
                    var run = Assert.IsType<Run>(container.NextInline);
                    Assert.Equal(expected, run.TextDecorations?.Any(d => d.Location == TextDecorationLocation.Strikethrough) == true);
                }
            }
            finally { Directory.Delete(tempRoot, recursive: true); }
        });
    }

    [Fact]
    public void Footer_ReportsLineCountForTextAndImageOnlyDocuments()
    {
        RunSta(() =>
        {
            const string text = "one two three\nfour five";
            var editor = new System.Windows.Controls.RichTextBox(new FlowDocument(new Paragraph(new Run(text))));
            var stats = new System.Windows.Controls.TextBlock();
            using var controller = new QuickNoteFooterStatsController(editor, stats);
            controller.UpdateUi();
            Assert.Equal(LocalizationService.Format("QuickNote_Stats", text.Length, 2), stats.Text);
            var bitmap = BitmapSource.Create(1, 1, 96, 96, PixelFormats.Bgra32, null, new byte[] { 0, 0, 0, 255 }, 4);
            Assert.True(QuickNoteImageHelper.TryCreateInlineImage(bitmap, out var image));
            editor.Document = new FlowDocument(new Paragraph(image!));
            controller.ScheduleUpdate();
            controller.UpdateUi();
            Assert.Equal(LocalizationService.Format("QuickNote_Stats", 1, 1), stats.Text);
        });
    }

    [Theory]
    [InlineData("dark", 460, 320)]
    [InlineData("sage", 460, 320)]
    [InlineData("dark", 580, 430)]
    [InlineData("sage", 580, 430)]
    [InlineData("mauve", 460, 320)]
    [InlineData("mauve", 580, 430)]
    [InlineData("lemon", 460, 320)]
    [InlineData("lemon", 580, 430)]
    [InlineData("lavender", 460, 320)]
    [InlineData("lavender", 580, 430)]
    [InlineData("sky", 460, 320)]
    [InlineData("sky", 580, 430)]
    [InlineData("stone", 460, 320)]
    [InlineData("stone", 580, 430)]
    public void CompactWindow_AllToolbarButtonsFitAndRender(string theme, int width, int height)
    {
        RunSta(() =>
        {
            string tempRoot = Path.Combine(Path.GetTempPath(), "AiteBarTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempRoot);
            try
            {
                var settings = new AppSettingsService(Path.Combine(tempRoot, "buttons.json"), Path.Combine(tempRoot, "settings.json"));
                settings.UpdateSettings(s => s.QuickNoteThemeId = theme);
                using var window = new QuickNoteWindow(new NoOpQuickNotePersistence(), settings);
                var document = window.TxtNote.Document;
                document.Blocks.Clear();
                document.Blocks.Add(new Paragraph(new Run("A place for the next idea") { FontSize = 20, FontWeight = FontWeights.SemiBold }));
                var text = new Paragraph(new Run("Capture a thought. Keep the details close."));
                document.Blocks.Add(text);
                var task = new Paragraph(new Run("Review the release notes"));
                QuickNoteDocumentFormatting.ToggleTaskParagraph(task, null, QuickNoteThemeCatalog.Find(theme));
                document.Blocks.Add(task);
                document.Blocks.Add(QuickNoteDocumentFormatting.CreateCodeBlockElement("await SaveNoteAsync();", QuickNoteThemeCatalog.Find(theme)));
                document.Blocks.Add(QuickNoteDocumentFormatting.CreateQuoteBlockElement("Keep only what matters.", QuickNoteThemeCatalog.Find(theme)));
                window.EnsureDocumentLoadedForFirstPaint();
                var root = Assert.IsAssignableFrom<FrameworkElement>(window.Content);
                root.Measure(new Size(width, height));
                root.Arrange(new Rect(0, 0, width, height));
                root.UpdateLayout();
                window.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
                window.SaveNowAsync().GetAwaiter().GetResult();
                root.UpdateLayout();
                Section code = Assert.Single(document.Blocks.OfType<Section>(), QuickNoteDocumentFormatting.IsCodeBlock);
                Assert.Equal(new Thickness(0, 0, 0, 8), code.Padding);
                Assert.Equal(new Thickness(0, 0, 0, 8), Assert.IsType<BlockUIContainer>(code.Blocks.FirstBlock).Padding);
                Assert.Equal(QuickNoteThemeCatalog.CodeBackground, BrushToHex(code.Background));
                Assert.Equal(QuickNoteThemeCatalog.CodeText, BrushToHex(code.Foreground));
                Assert.Equal(new Thickness(0, 8, 0, 8), Assert.Single(document.Blocks.OfType<Section>(), QuickNoteDocumentFormatting.IsQuoteBlock).Padding);
                var palette = Assert.IsType<System.Windows.Controls.Primitives.UniformGrid>(window.FindName("ThemePalette"));
                Assert.Equal(7, palette.Columns);
                Assert.Equal(7, palette.Children.Count);
                foreach (System.Windows.Controls.Button swatch in palette.Children)
                {
                    Assert.Null(swatch.FocusVisualStyle);
                    Assert.Equal(new Thickness(0), swatch.Margin);
                    Assert.Equal(42, swatch.Width);
                    Assert.Equal(48, swatch.Height);
                    MultiTrigger focus = Assert.Single(swatch.Template.Triggers.OfType<MultiTrigger>());
                    Assert.Contains(focus.Conditions.Cast<Condition>(), condition =>
                        condition.Property == KeyboardFocusVisualService.ShowKeyboardFocusCueProperty && Equals(condition.Value, true));
                }
                int selectedIndex = QuickNoteThemeCatalog.Themes.ToList().FindIndex(t => t.Id == QuickNoteThemeCatalog.Find(theme).Id);
                Assert.Single(palette.Children.OfType<System.Windows.Controls.Button>(), button => button.Content != null);
                Assert.Equal("\uE73E", ((System.Windows.Controls.Button)palette.Children[selectedIndex]).Content);
                palette.Measure(new Size(294, 48));
                palette.Arrange(new Rect(0, 0, 294, 48));
                palette.UpdateLayout();
                var paletteBitmap = new RenderTargetBitmap(294, 48, 96, 96, PixelFormats.Pbgra32);
                paletteBitmap.Render(palette);
                for (int i = 0; i < 7; i++)
                {
                    var pixel = new byte[4];
                    paletteBitmap.CopyPixels(new Int32Rect(i * 42 + 21, 8, 1, 1), pixel, 4, 0);
                    var expected = (Color)ColorConverter.ConvertFromString(QuickNoteThemeCatalog.GetSwatchColor(QuickNoteThemeCatalog.Themes[i]));
                    Assert.Equal(new byte[] { expected.B, expected.G, expected.R, 255 }, pixel);
                }
                var toolbar = Assert.IsType<System.Windows.Controls.StackPanel>(window.FindName("FormattingToolbar"));
                Assert.Equal(15, toolbar.Children.Count);
                Assert.Equal(28, toolbar.ActualHeight);
                Assert.Equal(toolbar.ActualHeight, Assert.IsType<System.Windows.Controls.Border>(toolbar.Parent).ActualHeight);
                var footer = Assert.IsType<System.Windows.Controls.Border>(window.FindName("FooterBar"));
                var status = Assert.IsType<System.Windows.Controls.TextBlock>(window.FindName("TxtSaveStatus"));
                Assert.Equal(status.ActualHeight + 4, footer.ActualHeight);
                Assert.InRange(footer.ActualHeight, 16, 20);
                foreach (System.Windows.Controls.Button button in toolbar.Children)
                {
                    Point origin = button.TranslatePoint(new Point(), root);
                    Assert.InRange(origin.X, 0, width - button.ActualWidth);
                    Assert.InRange(origin.Y, 0, height - button.ActualHeight);
                    Assert.False(string.IsNullOrWhiteSpace(System.Windows.Automation.AutomationProperties.GetName(button)));
                }
                var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
                bitmap.Render(root);
                Assert.Equal(width, bitmap.PixelWidth);
                string? renderDirectory = Environment.GetEnvironmentVariable("AITEBAR_QUICKNOTE_RENDER_DIR");
                if (!string.IsNullOrWhiteSpace(renderDirectory))
                {
                    Directory.CreateDirectory(renderDirectory);
                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(bitmap));
                    using var stream = File.Create(Path.Combine(renderDirectory, $"quicknote-{theme}-{width}x{height}.png"));
                    encoder.Save(stream);
                    var paletteEncoder = new PngBitmapEncoder();
                    paletteEncoder.Frames.Add(BitmapFrame.Create(paletteBitmap));
                    using var paletteStream = File.Create(Path.Combine(renderDirectory, $"quicknote-palette-{theme}.png"));
                    paletteEncoder.Save(paletteStream);
                }
            }
            finally { Directory.Delete(tempRoot, recursive: true); }
        });
    }

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
                    Tag = "link:https://example.com",
                    FontFamily = new FontFamily("Consolas"),
                    FontSize = 17,
                    FontStyle = FontStyles.Italic,
                    FontWeight = FontWeights.Bold,
                    FontStretch = FontStretches.Condensed,
                    Background = Brushes.LightYellow
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
                Assert.All(links, link =>
                {
                    Assert.Equal(hyperlink.FontFamily, link.FontFamily);
                    Assert.Equal(hyperlink.FontSize, link.FontSize);
                    Assert.Equal(hyperlink.FontStyle, link.FontStyle);
                    Assert.Equal(hyperlink.FontWeight, link.FontWeight);
                    Assert.Equal(hyperlink.FontStretch, link.FontStretch);
                    Assert.Equal(BrushToHex(hyperlink.Background), BrushToHex(link.Background));
                });
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

                Assert.Equal(QuickNoteThemeCatalog.Find("dark").Text, BrushToHex(plainRun.Foreground));
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
    public void ApplyTheme_RecolorsNestedQuoteLinkUsingDocumentThemeRoles()
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
                var quote = QuickNoteDocumentFormatting.CreateQuoteBlockElement("", QuickNoteThemeCatalog.Find("dark"));
                var paragraph = Assert.IsType<Paragraph>(quote.Blocks.FirstBlock);
                paragraph.Inlines.Clear();
                var linkRun = new Run("quoted link") { Foreground = Brushes.Magenta };
                var link = new Hyperlink(new Bold(linkRun))
                {
                    NavigateUri = new Uri("https://example.com"),
                    Foreground = Brushes.Magenta
                };
                paragraph.Inlines.Add(link);
                window.TxtNote.Document.Blocks.Clear();
                window.TxtNote.Document.Blocks.Add(quote);

                var theme = QuickNoteThemeCatalog.Find("rose");
                typeof(QuickNoteWindow).GetField("_theme", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                    .SetValue(window, theme);
                typeof(QuickNoteWindow).GetMethod("ApplyTheme", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                    .Invoke(window, [theme]);

                Assert.Equal(theme.Link, BrushToHex(link.Foreground));
                Assert.Equal(theme.Link, BrushToHex(linkRun.Foreground));
            }
            finally
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        });
    }

    [Theory]
    [InlineData("before after", 7, 0, "before ", "after")]
    [InlineData("after", 0, 0, "", "after")]
    [InlineData("before", 6, 0, "before", "")]
    [InlineData("", 0, 0, "", "")]
    [InlineData("before REPLACEafter", 7, 7, "before ", "after")]
    public void InsertImage_IsolatesImageAndPreservesTextAcrossUndoAndReload(string text, int offset, int count, string before, string after)
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
                var run = new Run(text) { FontWeight = FontWeights.Bold };
                window.TxtNote.IsUndoEnabled = false;
                window.TxtNote.Document.Blocks.Clear();
                window.TxtNote.Document.Blocks.Add(new Paragraph(run));
                var root = (FrameworkElement)window.Content;
                root.Measure(new Size(460, 320));
                root.Arrange(new Rect(0, 0, 460, 320));
                root.UpdateLayout();
                TextPointer caret = run.ContentStart.GetPositionAtOffset(offset)!;
                window.TxtNote.Selection.Select(caret, caret.GetPositionAtOffset(count)!);
                window.TxtNote.IsUndoEnabled = true;

                var pixels = new byte[80 * 48 * 4];
                for (int i = 0; i < pixels.Length; i += 4)
                {
                    pixels[i] = 180;
                    pixels[i + 1] = 140;
                    pixels[i + 2] = 40;
                    pixels[i + 3] = 255;
                }
                BitmapSource bitmap = BitmapSource.Create(80, 48, 96, 96, PixelFormats.Bgra32, null, pixels, 80 * 4);
                Assert.True(QuickNoteImageHelper.TryCreateInlineImage(bitmap, out InlineUIContainer? image));
                bool inserted = (bool)typeof(QuickNoteWindow)
                    .GetMethods(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .Single(method => method.Name == "InsertImage" && method.GetParameters().SingleOrDefault()?.ParameterType == typeof(InlineUIContainer))
                    .Invoke(window, [image])!;

                Assert.True(inserted);
                Assert.True(window.TxtNote.Selection.IsEmpty);
                Verify(window.TxtNote.Document);
                Assert.NotSame(image!.Parent, window.TxtNote.CaretPosition.Paragraph);
                // This offscreen test has no dispatcher loop to run the debounced footer update.
                typeof(QuickNoteWindow).GetMethod("UpdateFooterStats", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                    .Invoke(window, null);
                root.UpdateLayout();
                string? directory = Environment.GetEnvironmentVariable("AITEBAR_QUICKNOTE_RENDER_DIR");
                if (!string.IsNullOrWhiteSpace(directory) && text == "before after")
                {
                    Directory.CreateDirectory(directory);
                    var preview = new RenderTargetBitmap(460, 320, 96, 96, PixelFormats.Pbgra32);
                    preview.Render(root);
                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(preview));
                    using var stream = File.Create(Path.Combine(directory, "quicknote-image-block.png"));
                    encoder.Save(stream);
                }
                Assert.True(window.TxtNote.CanUndo);
                window.TxtNote.Undo();
                Assert.Empty(QuickNoteImageHelper.EnumerateImageContainers(window.TxtNote.Document.Blocks));
                Assert.Equal(text, new TextRange(window.TxtNote.Document.ContentStart, window.TxtNote.Document.ContentEnd).Text.TrimEnd('\r', '\n'));
                window.TxtNote.Redo();
                Verify(window.TxtNote.Document);
                var reloaded = new FlowDocument();
                QuickNoteDocumentCodec.Deserialize(QuickNoteDocumentCodec.Serialize(window.TxtNote.Document, true), reloaded, true);
                Verify(reloaded);

                void Verify(FlowDocument document)
                {
                    var paragraphs = document.Blocks.Cast<Paragraph>().ToArray();
                    int imageIndex = before.Length == 0 ? 0 : 1;
                    Assert.Equal(imageIndex + 2, paragraphs.Length);
                    var embedded = Assert.Single(QuickNoteImageHelper.EnumerateImageContainers(document.Blocks));
                    Assert.Same(paragraphs[imageIndex], embedded.Parent);
                    Assert.Single(paragraphs[imageIndex].Inlines);
                    Assert.Equal(after, new TextRange(paragraphs[^1].ContentStart, paragraphs[^1].ContentEnd).Text);
                    if (before.Length != 0)
                    {
                        Assert.Equal(before, new TextRange(paragraphs[0].ContentStart, paragraphs[0].ContentEnd).Text);
                        Assert.Equal(FontWeights.Bold, new TextRange(paragraphs[0].ContentStart, paragraphs[0].ContentEnd).GetPropertyValue(TextElement.FontWeightProperty));
                    }
                }
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

    [Fact]
    public void TaskItem_PreservesHyperlinkAndFormattingWhenCheckedAndUnchecked()
    {
        RunSta(() =>
        {
            var theme = QuickNoteThemeCatalog.Find("dark");
            var paragraph = new Paragraph();
            var hyperlink = new Hyperlink(new Run("Visit Google"))
            {
                NavigateUri = new Uri("https://google.com")
            };
            paragraph.Inlines.Add(hyperlink);

            QuickNoteDocumentFormatting.ToggleTaskParagraph(paragraph, null, theme);
            Assert.True(QuickNoteDocumentFormatting.IsTaskParagraph(paragraph, out bool isChecked, out _, out _));
            Assert.False(isChecked);

            // Verify hyperlink retains link color when unchecked
            QuickNoteDocumentFormatting.ApplyTaskFormattingToParagraph(paragraph, false, theme);
            Assert.Equal(theme.Link, BrushToHex(hyperlink.Foreground));

            // Verify hyperlink becomes muted and struck-through when checked
            QuickNoteDocumentFormatting.ApplyTaskFormattingToParagraph(paragraph, true, theme);
            Assert.Equal(theme.MutedText, BrushToHex(hyperlink.Foreground));
            Assert.Contains(hyperlink.TextDecorations, d => d.Location == TextDecorationLocation.Strikethrough);

            // Verify unchecked restores link color
            QuickNoteDocumentFormatting.ApplyTaskFormattingToParagraph(paragraph, false, theme);
            Assert.Equal(theme.Link, BrushToHex(hyperlink.Foreground));
            Assert.DoesNotContain(hyperlink.TextDecorations, d => d.Location == TextDecorationLocation.Strikethrough);
            Assert.Contains(hyperlink.TextDecorations, d => d.Location == TextDecorationLocation.Underline);
        });
    }

    [Fact]
    public void TaskItem_UncheckPreservesUserAppliedStrikethrough()
    {
        RunSta(() =>
        {
            var theme = QuickNoteThemeCatalog.Find("dark");
            var paragraph = new Paragraph();
            var runWithUserStrikethrough = new Run("already struck")
            {
                TextDecorations = TextDecorations.Strikethrough
            };
            paragraph.Inlines.Add(runWithUserStrikethrough);

            QuickNoteDocumentFormatting.ToggleTaskParagraph(paragraph, null, theme);

            // Check task
            QuickNoteDocumentFormatting.ApplyTaskFormattingToParagraph(paragraph, true, theme);
            Assert.Equal(TextDecorations.Strikethrough, paragraph.TextDecorations);

            // Uncheck task - paragraph strikethrough removed, but run's user strikethrough preserved!
            QuickNoteDocumentFormatting.ApplyTaskFormattingToParagraph(paragraph, false, theme);
            Assert.Null(paragraph.TextDecorations);
            Assert.Contains(runWithUserStrikethrough.TextDecorations, d => d.Location == TextDecorationLocation.Strikethrough);
        });
    }

    [Fact]
    public void TaskItem_DeepNestedHyperlinkBold_RetainsLinkColorWhenUnchecked()
    {
        RunSta(() =>
        {
            var theme = QuickNoteThemeCatalog.Find("dark");
            var paragraph = new Paragraph();
            var bold = new Bold(new Run("Bold link text"));
            var hyperlink = new Hyperlink(bold)
            {
                NavigateUri = new Uri("https://example.com")
            };
            paragraph.Inlines.Add(hyperlink);

            QuickNoteDocumentFormatting.ToggleTaskParagraph(paragraph, null, theme);

            // Unchecked state
            QuickNoteDocumentFormatting.ApplyTaskFormattingToParagraph(paragraph, false, theme);
            var innerRun = (Run)bold.Inlines.FirstInline!;
            Assert.Equal(theme.Link, BrushToHex(innerRun.Foreground));
            Assert.Equal(theme.Link, BrushToHex(hyperlink.Foreground));

            // Checked state
            QuickNoteDocumentFormatting.ApplyTaskFormattingToParagraph(paragraph, true, theme);
            Assert.Equal(theme.MutedText, BrushToHex(innerRun.Foreground));
            Assert.Equal(theme.MutedText, BrushToHex(hyperlink.Foreground));
        });
    }

    [Fact]
    public void TaskItem_StrikethroughOnInlineCode_PreservesCodeTagAndStyling()
    {
        RunSta(() =>
        {
            var theme = QuickNoteThemeCatalog.Find("dark");
            var paragraph = new Paragraph();
            var codeSpan = new Span(new Run("int x = 42;"))
            {
                Tag = QuickNoteTags.Code,
                TextDecorations = TextDecorations.Strikethrough
            };
            paragraph.Inlines.Add(codeSpan);

            // Turn into task
            QuickNoteDocumentFormatting.ToggleTaskParagraph(paragraph, null, theme);

            // Verify Tag is preserved as code (not overwritten by strikethrough tag)
            Assert.Equal(QuickNoteTags.Code, codeSpan.Tag);

            // Verify code formatting is retained in unchecked state
            QuickNoteDocumentFormatting.ApplyTaskFormattingToParagraph(paragraph, false, theme);
            Assert.Equal(QuickNoteTags.Code, codeSpan.Tag);
            Assert.Contains(codeSpan.TextDecorations, d => d.Location == TextDecorationLocation.Strikethrough);
            Assert.Equal(QuickNoteDocumentFormatting.GetCodeText(theme), BrushToHex(codeSpan.Foreground));

            // Check task
            QuickNoteDocumentFormatting.ApplyTaskFormattingToParagraph(paragraph, true, theme);
            Assert.Equal(theme.MutedText, BrushToHex(codeSpan.Foreground));

            // Uncheck task - Tag is still code and strikethrough remains
            QuickNoteDocumentFormatting.ApplyTaskFormattingToParagraph(paragraph, false, theme);
            Assert.Equal(QuickNoteTags.Code, codeSpan.Tag);
            Assert.Contains(codeSpan.TextDecorations, d => d.Location == TextDecorationLocation.Strikethrough);
            Assert.Equal(QuickNoteDocumentFormatting.GetCodeText(theme), BrushToHex(codeSpan.Foreground));
        });
    }

    [Fact]
    public void TaskItem_CheckedTask_ReappliedFormatting_UncheckRemovesStrikethrough()
    {
        RunSta(() =>
        {
            var themeDark = QuickNoteThemeCatalog.Find("dark");
            var themeRose = QuickNoteThemeCatalog.Find("rose");
            var paragraph = new Paragraph();
            var run = new Run("Simple task text");
            paragraph.Inlines.Add(run);

            // Turn into task and check it
            QuickNoteDocumentFormatting.ToggleTaskParagraph(paragraph, null, themeDark);
            QuickNoteDocumentFormatting.ApplyTaskFormattingToParagraph(paragraph, true, themeDark);

            Assert.Contains(run.TextDecorations, d => d.Location == TextDecorationLocation.Strikethrough);

            // Re-apply formatting multiple times (simulating theme switch or RTF reload)
            QuickNoteDocumentFormatting.ApplyTaskFormattingToParagraph(paragraph, true, themeDark);
            QuickNoteDocumentFormatting.ApplyTaskFormattingToParagraph(paragraph, true, themeRose);
            QuickNoteDocumentFormatting.ApplyTaskFormattingToParagraph(paragraph, true, themeDark);

            // Strikethrough must still be recognized as task-strikethrough, not user-strikethrough
            Assert.False(QuickNoteDocumentFormatting.GetIsUserStrikethrough(run));
            Assert.True(QuickNoteDocumentFormatting.GetIsTaskStrikethrough(run));

            // Now uncheck the task
            QuickNoteDocumentFormatting.ApplyTaskFormattingToParagraph(paragraph, false, themeDark);

            // Strikethrough should be completely removed from the run
            Assert.True(run.TextDecorations == null || !run.TextDecorations.Any(d => d.Location == TextDecorationLocation.Strikethrough));
            Assert.Null(paragraph.TextDecorations);
        });
    }

    [Fact]
    public void TaskItem_NestedInlineUserStrikethrough_PreservedWhenConvertedToTaskAndUnchecked()
    {
        RunSta(() =>
        {
            var theme = QuickNoteThemeCatalog.Find("dark");
            var paragraph = new Paragraph();
            var bold = new Bold();
            var runWithStrike = new Run("struck inside bold")
            {
                TextDecorations = TextDecorations.Strikethrough
            };
            bold.Inlines.Add(runWithStrike);
            paragraph.Inlines.Add(bold);

            var normalRun = new Run(" normal text");
            paragraph.Inlines.Add(normalRun);

            // Convert to task (initially unchecked)
            QuickNoteDocumentFormatting.ToggleTaskParagraph(paragraph, null, theme);

            // Check that the nested run was marked with IsUserStrikethrough
            Assert.True(QuickNoteDocumentFormatting.GetIsUserStrikethrough(runWithStrike));
            Assert.False(QuickNoteDocumentFormatting.GetIsUserStrikethrough(normalRun));

            // Check the task
            QuickNoteDocumentFormatting.ApplyTaskFormattingToParagraph(paragraph, true, theme);
            Assert.Contains(runWithStrike.TextDecorations, d => d.Location == TextDecorationLocation.Strikethrough);
            Assert.Contains(normalRun.TextDecorations, d => d.Location == TextDecorationLocation.Strikethrough);

            // Uncheck the task
            QuickNoteDocumentFormatting.ApplyTaskFormattingToParagraph(paragraph, false, theme);

            // Strikethrough on nested run remains preserved!
            Assert.Contains(runWithStrike.TextDecorations, d => d.Location == TextDecorationLocation.Strikethrough);

            // Normal run has NO strikethrough!
            Assert.True(normalRun.TextDecorations == null || !normalRun.TextDecorations.Any(d => d.Location == TextDecorationLocation.Strikethrough));
        });
    }

    [Fact]
    public void TaskItem_RtfRoundTrip_PreservesUserStrikethroughOnUncheck()
    {
        RunSta(() =>
        {
            var theme = QuickNoteThemeCatalog.Find("dark");
            var sourceDoc = new FlowDocument();
            var paragraph = new Paragraph();
            var runStruck = new Run("user struck")
            {
                TextDecorations = TextDecorations.Strikethrough
            };
            var runNormal = new Run(" normal part");
            paragraph.Inlines.Add(runStruck);
            paragraph.Inlines.Add(runNormal);
            sourceDoc.Blocks.Add(paragraph);

            // Make it a checked task
            QuickNoteDocumentFormatting.ToggleTaskParagraph(paragraph, null, theme);
            QuickNoteDocumentFormatting.ApplyTaskFormattingToParagraph(paragraph, true, theme);

            // Export to RTF export doc
            FlowDocument exportDoc = QuickNoteRtfAdapter.CreateExportDocument(sourceDoc);

            // Simulate RTF restore into a new FlowDocument
            var targetDoc = new FlowDocument();
            foreach (Block b in exportDoc.Blocks.ToList())
            {
                exportDoc.Blocks.Remove(b);
                targetDoc.Blocks.Add(b);
            }
            QuickNoteRtfAdapter.RestoreTaskItems(targetDoc, theme);

            // The restored paragraph should be a checked task
            var restoredPara = Assert.IsType<Paragraph>(targetDoc.Blocks.FirstBlock);
            Assert.True(QuickNoteDocumentFormatting.IsTaskParagraph(restoredPara, out bool isChecked, out _, out _));
            Assert.True(isChecked);

            // Find the runs in the restored paragraph (after the checkbox container)
            var inlines = restoredPara.Inlines.Skip(1).ToList();
            var restoredStruck = (Run)inlines[0];
            var restoredNormal = (Run)inlines[1];

            Assert.Equal("user struck", restoredStruck.Text);
            Assert.Equal(" normal part", restoredNormal.Text);

            // When unchecked, user strikethrough must be preserved, normal strikethrough removed
            QuickNoteDocumentFormatting.ApplyTaskFormattingToParagraph(restoredPara, false, theme);

            Assert.Contains(restoredStruck.TextDecorations, d => d.Location == TextDecorationLocation.Strikethrough);
            Assert.True(restoredNormal.TextDecorations == null || !restoredNormal.TextDecorations.Any(d => d.Location == TextDecorationLocation.Strikethrough));
        });
    }

    [Fact]
    public void TaskItem_RtfRoundTrip_PreservesParagraphUserStrikethroughOnUncheck()
    {
        RunSta(() =>
        {
            var theme = QuickNoteThemeCatalog.Find("dark");
            var sourceDoc = new FlowDocument();
            var paragraph = new Paragraph(new Run("paragraph strike"))
            {
                TextDecorations = TextDecorations.Strikethrough
            };
            sourceDoc.Blocks.Add(paragraph);

            QuickNoteDocumentFormatting.ToggleTaskParagraph(paragraph, null, theme);
            QuickNoteDocumentFormatting.ApplyTaskFormattingToParagraph(paragraph, true, theme);

            FlowDocument exportDoc = QuickNoteRtfAdapter.CreateExportDocument(sourceDoc);
            var targetDoc = new FlowDocument();
            foreach (Block block in exportDoc.Blocks.ToList())
            {
                exportDoc.Blocks.Remove(block);
                targetDoc.Blocks.Add(block);
            }

            QuickNoteRtfAdapter.RestoreTaskItems(targetDoc, theme);
            var restoredParagraph = Assert.IsType<Paragraph>(targetDoc.Blocks.FirstBlock);
            QuickNoteDocumentFormatting.ApplyTaskFormattingToParagraph(restoredParagraph, false, theme);

            Assert.Contains(restoredParagraph.TextDecorations, decoration =>
                decoration.Location == TextDecorationLocation.Strikethrough);
        });
    }

    [Fact]
    public void RtfExport_PreservesFormattingAppliedToHyperlink()
    {
        RunSta(() =>
        {
            var hyperlink = new Hyperlink(new Run("styled link"))
            {
                NavigateUri = new Uri("https://example.com"),
                FontFamily = new FontFamily("Consolas"),
                FontSize = 17,
                FontStyle = FontStyles.Italic,
                FontWeight = FontWeights.Bold,
                FontStretch = FontStretches.Condensed,
                Foreground = Brushes.DarkOrange,
                Background = Brushes.LightYellow
            };
            var sourceDoc = new FlowDocument(new Paragraph(hyperlink));

            FlowDocument exportDoc = QuickNoteRtfAdapter.CreateExportDocument(sourceDoc);
            var exportedLink = Assert.IsType<Hyperlink>(Assert.IsType<Paragraph>(exportDoc.Blocks.FirstBlock).Inlines.FirstInline);

            Assert.Equal(hyperlink.NavigateUri, exportedLink.NavigateUri);
            Assert.Equal(hyperlink.FontFamily, exportedLink.FontFamily);
            Assert.Equal(hyperlink.FontSize, exportedLink.FontSize);
            Assert.Equal(hyperlink.FontStyle, exportedLink.FontStyle);
            Assert.Equal(hyperlink.FontWeight, exportedLink.FontWeight);
            Assert.Equal(hyperlink.FontStretch, exportedLink.FontStretch);
            Assert.Equal(BrushToHex(hyperlink.Foreground), BrushToHex(exportedLink.Foreground));
            Assert.Equal(BrushToHex(hyperlink.Background), BrushToHex(exportedLink.Background));
        });
    }

    [Fact]
    public void ClearSelectedFormatting_MixedCodeAndQuoteAndLink_ClearsAll()
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

                window.TxtNote.Document.Blocks.Clear();

                var theme = QuickNoteThemeCatalog.Find("dark");

                // Code block section
                var codeSection = QuickNoteDocumentFormatting.CreateCodeBlockElement("var x = 1;", theme);
                window.TxtNote.Document.Blocks.Add(codeSection);

                // Quote block section
                var quoteSection = QuickNoteDocumentFormatting.CreateQuoteBlockElement("Quoted text", theme);
                window.TxtNote.Document.Blocks.Add(quoteSection);

                // Paragraph with Link
                var linkParagraph = new Paragraph(new Hyperlink(new Run("Link text"))
                {
                    NavigateUri = new Uri("https://example.com")
                });
                window.TxtNote.Document.Blocks.Add(linkParagraph);

                // Select all
                window.TxtNote.Selection.Select(window.TxtNote.Document.ContentStart, window.TxtNote.Document.ContentEnd);

                // Clear all formatting
                window.ClearSelectedFormatting();

                // Verify no sections (code/quote) remain, all converted to standard paragraphs
                Assert.Empty(window.TxtNote.Document.Blocks.OfType<Section>());
                Assert.Empty(window.TxtNote.Document.Blocks.OfType<Paragraph>().SelectMany(p => p.Inlines.OfType<Hyperlink>()));
            }
            finally
            {
                Directory.Delete(tempRoot, recursive: true);
            }
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
        public void OpenConflictCopy()
        {
        }
    }
}
