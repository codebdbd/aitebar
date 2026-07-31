using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Threading;

namespace AiteBar.Tests;

[Collection("WpfTestCollection")]
public sealed class ZenEditorWindowBehaviorTests
{
    [Fact]
    public async Task ParagraphEditor_PreservesPlainTextAndAppliesTypographySpacing()
    {
        await RunStaAsync(() =>
        {
            var editor = new ZenParagraphEditor
            {
                EditorLineHeight = 30,
                ParagraphSpacing = 15,
                Text = "Первый абзац\nВторой абзац\n"
            };

            Assert.Equal("Первый абзац\nВторой абзац\n", editor.Text);
            Paragraph[] paragraphs = editor.Document.Blocks.OfType<Paragraph>().ToArray();
            Assert.Equal(3, paragraphs.Length);
            Assert.All(paragraphs, paragraph =>
            {
                Assert.Equal(30, paragraph.LineHeight);
                Assert.Equal(15, paragraph.Margin.Bottom);
                Assert.Equal(System.Windows.TextAlignment.Left, paragraph.TextAlignment);
            });

            editor.CaretIndex = editor.Text.Length;
            Assert.Equal(editor.Text.Length, editor.CaretIndex);
            editor.Select(0, 6);
            Assert.Equal("Первый", editor.SelectedText);

            editor.Measure(new System.Windows.Size(760, 600));
            editor.Arrange(new System.Windows.Rect(0, 0, 760, 600));
            editor.UpdateLayout();
            System.Windows.Rect firstLine = editor.GetRectFromCharacterIndex(0);
            System.Windows.Rect secondLine =
                editor.GetRectFromCharacterIndex("Первый абзац\n".Length);
            Assert.InRange(secondLine.Top - firstLine.Top, 44.5, 45.5);
        });
    }

    [Fact]
    public async Task Constructor_CreatesCompleteNonEmptyContextMenu()
    {
        await RunStaAsync(() =>
        {
            string root = Path.Combine(
                Path.GetTempPath(),
                "AiteBarTests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);

            try
            {
                var window = new ZenEditorWindow(new ZenEditorStore(root));
                window.Editor.SetValue(TextBlock.LineHeightProperty, 30d);
                ContextMenu menu = Assert.IsType<ContextMenu>(window.Editor.ContextMenu);
                Assert.Equal(17, menu.Items.Count);
                Assert.Same(window.FindResource("DarkContextMenu"), menu.Style);
                Assert.Equal(
                    30d,
                    window.Editor.GetValue(TextBlock.LineHeightProperty));
                Assert.True(double.IsNaN(
                    (double)menu.GetValue(TextBlock.LineHeightProperty)));
                Assert.Equal(
                    System.Windows.LineStackingStrategy.MaxHeight,
                    menu.GetValue(TextBlock.LineStackingStrategyProperty));

                Separator[] separators = menu.Items.OfType<Separator>().ToArray();
                Assert.Equal(4, separators.Length);
                Assert.All(separators, separator =>
                {
                    Assert.Same(window.FindResource("DarkMenuSeparator"), separator.Style);
                    Assert.Equal(0, separator.Height);
                    Assert.Equal(System.Windows.Visibility.Collapsed, separator.Visibility);
                });

                MenuItem[] commands = menu.Items.OfType<MenuItem>().ToArray();
                Assert.Equal(13, commands.Length);
                Assert.All(commands, command =>
                {
                    Assert.False(string.IsNullOrWhiteSpace(command.Header?.ToString()));
                    Assert.Same(window.FindResource("DarkMenuItem"), command.Style);
                    Assert.IsType<TextBlock>(command.Icon);
                });
                AssertIconFont(commands, "Ctrl+Z", "Segoe UI");
                AssertIconFont(commands, "Ctrl+Y", "Segoe UI");
                AssertIconFont(
                    commands,
                    "Ctrl+X",
                    FontHelper.Resolve(FontHelper.MaterialKey).Source);
                AssertIconFont(
                    commands,
                    "Ctrl+C",
                    FontHelper.Resolve(FontHelper.FluentKey).Source);
                AssertIconFont(
                    commands,
                    "Ctrl+V",
                    FontHelper.Resolve(FontHelper.MaterialKey).Source);
                AssertIconFont(
                    commands,
                    "Ctrl+A",
                    FontHelper.Resolve(FontHelper.MaterialKey).Source);

                MenuItem themes = commands.Single(command => command.Items.Count == 5);
                Assert.Equal(5, themes.Items.Count);
                Assert.All(themes.Items.OfType<MenuItem>(), theme =>
                {
                    Assert.False(string.IsNullOrWhiteSpace(theme.Header?.ToString()));
                    Assert.Same(window.FindResource("DarkMenuItem"), theme.Style);
                    Assert.IsType<TextBlock>(theme.Icon);
                });

                MenuItem formatting = commands.Single(command => command.Items.Count == 3);
                MenuItem[] formattingCommands = formatting.Items
                    .OfType<MenuItem>()
                    .ToArray();
                Assert.Equal(3, formattingCommands.Length);
                Assert.Equal(
                    ["Ctrl+B", "Ctrl+I", "Ctrl+U"],
                    formattingCommands.Select(command => command.InputGestureText));
                Assert.All(formattingCommands, command =>
                {
                    Assert.True(command.IsCheckable);
                    Assert.Same(window.FindResource("DarkMenuItem"), command.Style);
                    Assert.IsType<TextBlock>(command.Icon);
                });

                FrameworkElement searchOverlay = Assert.IsAssignableFrom<FrameworkElement>(
                    window.FindName("SearchOverlay"));
                Assert.Equal(Visibility.Collapsed, searchOverlay.Visibility);

                window.Close();
            }
            finally
            {
                Directory.Delete(root, recursive: true);
            }
        });
    }

    [Fact]
    public async Task DeletedDocumentPicker_UsesRestoreModeTitleAndSelection()
    {
        await RunStaAsync(() =>
        {
            ZenEditorTheme theme = ZenEditorThemeCatalog.Get(null);
            var summary = new ZenEditorDocumentSummary(
                Guid.NewGuid(),
                "Удалённый документ",
                DateTime.UtcNow,
                IsCurrent: false);
            var picker = new ZenEditorDocumentPicker(
                [summary],
                theme,
                restoreMode: true);

            Assert.Equal(
                LocalizationService.Get("ZenEditor_RecentlyDeleted"),
                picker.Title);
            ListBox list = Assert.IsType<ListBox>(picker.FindName("DocumentList"));
            Assert.Single(list.Items);

            picker.Close();
        });
    }

    [Fact]
    public async Task ParagraphEditor_RoundTripsBoldItalicAndUnderlineRanges()
    {
        await RunStaAsync(() =>
        {
            var editor = new ZenParagraphEditor { Text = "Жирный обычный" };
            editor.Select(0, 6);
            editor.Selection.ApplyPropertyValue(
                TextElement.FontWeightProperty,
                System.Windows.FontWeights.Bold);
            editor.Selection.ApplyPropertyValue(
                TextElement.FontStyleProperty,
                System.Windows.FontStyles.Italic);
            editor.Selection.ApplyPropertyValue(
                Inline.TextDecorationsProperty,
                TextDecorations.Underline);

            ZenEditorTextStyle style = Assert.Single(editor.CaptureTextStyles());
            Assert.Equal(new ZenEditorTextStyle(0, 6, true, true, true), style);

            var restored = new ZenParagraphEditor { Text = editor.Text };
            restored.ApplyTextStyles([style]);
            restored.Select(0, 6);

            Assert.Equal(
                System.Windows.FontWeights.Bold,
                restored.Selection.GetPropertyValue(TextElement.FontWeightProperty));
            Assert.Equal(
                System.Windows.FontStyles.Italic,
                restored.Selection.GetPropertyValue(TextElement.FontStyleProperty));
            var decorations = Assert.IsType<TextDecorationCollection>(
                restored.Selection.GetPropertyValue(Inline.TextDecorationsProperty));
            Assert.Contains(
                decorations,
                decoration => decoration.Location == TextDecorationLocation.Underline);
            Assert.Equal("Жирный обычный", restored.Text);
        });
    }

    [Fact]
    public async Task ParagraphEditor_CommonEditsDoNotReadTheWholeLargeDocument()
    {
        await RunStaAsync(() =>
        {
            string original = new('а', 2_000_000);
            var editor = new ZenParagraphEditor { Text = original };
            editor.CaretIndex = original.Length;

            int readsBeforeTyping = editor.FullDocumentReadCount;
            editor.CaretPosition.InsertTextInRun("б");

            Assert.Equal($"{original}б", editor.Text);
            Assert.Equal(readsBeforeTyping, editor.FullDocumentReadCount);
            ZenEditorTextChange insertion = Assert.Single(editor.LastPlainTextChanges);
            Assert.Equal(new ZenEditorTextChange(original.Length, 1, 0), insertion);
            editor.CaretIndex = editor.Text.Length;
            Assert.Equal(editor.Text.Length, editor.CaretIndex);

            editor.CaretPosition.DeleteTextInRun(-1);

            Assert.Equal(original, editor.Text);
            Assert.Equal(readsBeforeTyping, editor.FullDocumentReadCount);
            ZenEditorTextChange deletion = Assert.Single(editor.LastPlainTextChanges);
            Assert.Equal(new ZenEditorTextChange(original.Length, 0, 1), deletion);
            Assert.Equal(original.Length, editor.CaretIndex);

            var paragraphEditor = new ZenParagraphEditor { Text = "ПервыйВторой" };
            paragraphEditor.CaretIndex = 6;
            TextPointer afterBreak = paragraphEditor.CaretPosition.InsertParagraphBreak();
            paragraphEditor.CaretPosition = afterBreak;

            Assert.Equal("Первый\nВторой", paragraphEditor.Text);
            Assert.Equal(2, paragraphEditor.Document.Blocks.OfType<Paragraph>().Count());
            Assert.Equal(7, paragraphEditor.CaretIndex);
        });
    }

    [Fact]
    public async Task ParagraphEditor_ReportsInsertedFormattingForIncrementalStyleUpdate()
    {
        await RunStaAsync(() =>
        {
            var editor = new ZenParagraphEditor { Text = "Жирный текст" };
            editor.Select(0, 6);
            editor.Selection.ApplyPropertyValue(
                TextElement.FontWeightProperty,
                System.Windows.FontWeights.Bold);
            IReadOnlyList<ZenEditorTextStyle> previousStyles = editor.CaptureTextStyles();
            editor.CaretIndex = 3;

            editor.CaretPosition.InsertTextInRun("X");

            Assert.True(editor.CanTransformLastTextStyles);
            Assert.Equal(
                new ZenEditorTextStyle(3, 1, true, false, false),
                editor.LastInsertedTextStyle);
            ZenEditorTextChange change = Assert.Single(editor.LastPlainTextChanges);
            IReadOnlyList<ZenEditorTextStyle> incremental =
                ZenEditorTextHelper.ApplyTextChangeToStyles(
                    previousStyles,
                    change,
                    editor.LastInsertedTextStyle,
                    editor.Text.Length);
            Assert.Equal(editor.CaptureTextStyles(), incremental);
        });
    }

    [Fact]
    public async Task ParagraphEditor_CapturesStylesWithOneInlineVisitPerNode()
    {
        await RunStaAsync(() =>
        {
            const int paragraphCount = 2_000;
            var editor = new ZenParagraphEditor
            {
                Text = string.Join('\n', Enumerable.Repeat("абзац", paragraphCount))
            };
            foreach (Paragraph paragraph in editor.Document.Blocks.OfType<Paragraph>())
            {
                Run run = Assert.IsType<Run>(Assert.Single(paragraph.Inlines));
                run.FontWeight = FontWeights.Bold;
            }

            IReadOnlyList<ZenEditorTextStyle> styles = editor.CaptureTextStyles();

            Assert.Equal(paragraphCount, editor.LastStyleCaptureInlineCount);
            Assert.Equal(paragraphCount, styles.Count);
            Assert.Equal(
                editor.Text.Length,
                styles[^1].Start + styles[^1].Length);
        });
    }

    private static void AssertIconFont(
        IEnumerable<MenuItem> commands,
        string gesture,
        string expectedFont)
    {
        MenuItem command = commands.Single(item => item.InputGestureText == gesture);
        TextBlock icon = Assert.IsType<TextBlock>(command.Icon);
        Assert.Equal(expectedFont, icon.FontFamily.Source);
    }

    private static Task RunStaAsync(Action action)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            SynchronizationContext.SetSynchronizationContext(
                new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));

            try
            {
                action();
                completion.SetResult();
            }
            catch (Exception ex)
            {
                completion.SetException(ex);
            }
            finally
            {
                Dispatcher.CurrentDispatcher.InvokeShutdown();
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return completion.Task;
    }
}
