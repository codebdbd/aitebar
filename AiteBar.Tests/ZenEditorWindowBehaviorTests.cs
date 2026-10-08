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
                Assert.Equal(18, menu.Items.Count);
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
                Assert.Equal(14, commands.Length);
                Assert.All(commands, command =>
                {
                    Assert.False(string.IsNullOrWhiteSpace(command.Header?.ToString()));
                    Assert.Same(window.FindResource("DarkMenuItem"), command.Style);
                    Assert.IsType<CenteredGlyphTextBlock>(command.Icon);
                });

                MenuItem deleteItem = commands.Single(command =>
                    string.Equals(command.Header?.ToString(), LocalizationService.Get("ZenEditor_DeleteDocument"), StringComparison.Ordinal));
                var deleteIcon = Assert.IsType<CenteredGlyphTextBlock>(deleteItem.Icon);
                Assert.Equal(char.ConvertFromUtf32(0xF34D), deleteIcon.Text);
                Assert.Equal(FontHelper.Resolve(FontHelper.FluentKey).Source, deleteIcon.FontFamily.Source);
                Assert.NotNull(deleteItem.Foreground);
                Assert.Same(deleteItem.Foreground, deleteIcon.Foreground);
                AssertIconFont(
                    commands,
                    "Ctrl+Z",
                    FontHelper.Resolve(FontHelper.FluentKey).Source);
                AssertIconFont(
                    commands,
                    "Ctrl+Y",
                    FontHelper.Resolve(FontHelper.FluentKey).Source);
                AssertIconFont(
                    commands,
                    "Ctrl+X",
                    FontHelper.Resolve(FontHelper.FluentKey).Source);
                AssertIconFont(
                    commands,
                    "Ctrl+C",
                    FontHelper.Resolve(FontHelper.FluentKey).Source);
                AssertIconFont(
                    commands,
                    "Ctrl+V",
                    FontHelper.Resolve(FontHelper.FluentKey).Source);
                AssertIconFont(
                    commands,
                    "Ctrl+A",
                    FontHelper.Resolve(FontHelper.FluentKey).Source);

                MenuItem themes = commands.Single(command => command.Items.Count == 5);
                Assert.Equal(5, themes.Items.Count);
                Assert.All(themes.Items.OfType<MenuItem>(), theme =>
                {
                    Assert.False(string.IsNullOrWhiteSpace(theme.Header?.ToString()));
                    Assert.Same(window.FindResource("DarkMenuItem"), theme.Style);
                    Assert.IsType<CenteredGlyphTextBlock>(theme.Icon);
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
                    Assert.IsType<CenteredGlyphTextBlock>(command.Icon);
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

    [Theory]
    [InlineData("ru", "Удалить документ")]
    [InlineData("en", "Delete document")]
    [InlineData("uk", "Видалити документ")]
    [InlineData("de", "Dokument löschen")]
    public void ZenEditor_DeleteDocument_IsLocalizedInSupportedCultures(string cultureName, string expected)
    {
        var culture = System.Globalization.CultureInfo.GetCultureInfo(cultureName);
        string actual = LocalizationService.Get("ZenEditor_DeleteDocument", culture);
        Assert.False(string.IsNullOrWhiteSpace(actual));
        Assert.DoesNotContain("[[", actual);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public async Task DocumentPicker_ConfiguresDeleteStateAndThemeBrushes()
    {
        await RunStaAsync(() =>
        {
            ZenEditorTheme darkTheme = ZenEditorThemeCatalog.Get(ZenEditorThemeCatalog.GraphiteId);
            ZenEditorTheme lightTheme = ZenEditorThemeCatalog.Get(ZenEditorThemeCatalog.PaperId);

            var currentSummary = new ZenEditorDocumentSummary(
                Guid.NewGuid(),
                "Текущий",
                DateTime.UtcNow,
                IsCurrent: true);
            var otherSummary = new ZenEditorDocumentSummary(
                Guid.NewGuid(),
                "Другой",
                DateTime.UtcNow,
                IsCurrent: false);

            var picker = new ZenEditorDocumentPicker([currentSummary, otherSummary], darkTheme);

            Assert.True(picker.Resources.Contains("ZenDeleteButtonHoverBackground"));
            Assert.True(picker.Resources.Contains("ZenDeleteButtonHoverForeground"));
            var darkHoverForeground = Assert.IsType<System.Windows.Media.SolidColorBrush>(picker.Resources["ZenDeleteButtonHoverForeground"]);
            Assert.Equal(System.Windows.Media.Color.FromRgb(0xFF, 0x6B, 0x6B), darkHoverForeground.Color);

            static bool GetCanDelete(object item) =>
                (bool?)item.GetType().GetProperty("CanDelete", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public)?.GetValue(item) ?? false;

            ListBox list = Assert.IsType<ListBox>(picker.FindName("DocumentList"));
            Assert.Equal(2, list.Items.Count);

            Assert.False(GetCanDelete(list.Items[0]!));
            Assert.True(GetCanDelete(list.Items[1]!));

            picker.Close();

            var restorePicker = new ZenEditorDocumentPicker([otherSummary], lightTheme, restoreMode: true);
            Assert.True(restorePicker.Resources.Contains("ZenDeleteButtonHoverForeground"));
            var lightHoverForeground = Assert.IsType<System.Windows.Media.SolidColorBrush>(restorePicker.Resources["ZenDeleteButtonHoverForeground"]);
            Assert.Equal(System.Windows.Media.Color.FromRgb(0xC6, 0x28, 0x28), lightHoverForeground.Color);

            ListBox restoreList = Assert.IsType<ListBox>(restorePicker.FindName("DocumentList"));
            Assert.Single(restoreList.Items);
            Assert.False(GetCanDelete(restoreList.Items[0]!));

            restorePicker.Close();
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

    [Fact]
    public async Task ZenEditorUtility_RestoreExistingWindow_TogglesWindow()
    {
        string root = Path.Combine(Path.GetTempPath(), "aitebar_zen_toggle_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            await RunStaAsync(() =>
            {
                var store = new ZenEditorStore(root);
                var window = new ZenEditorWindow(store);
                var utility = new ZenEditorUtility();

                try
                {
                    // 1. Minimized window: does not close, restores to Normal
                    window.WindowState = WindowState.Minimized;
                    bool handledMinimized = utility.RestoreExistingWindowForTesting(window);
                    Assert.True(handledMinimized);
                    Assert.Equal(WindowState.Normal, window.WindowState);

                    // 2. Active, visible window: closes
                    window.Show();
                    window.Activate();

                    if (window.IsActive)
                    {
                        bool closed = false;
                        window.Closing += (_, _) => closed = true;

                        bool handled = utility.RestoreExistingWindowForTesting(window);
                        Assert.True(handled);
                        Assert.True(closed);
                    }
                }
                finally
                {
                    window.Close();
                }
            });
        }
        finally
        {
            TryDeleteDirectory(root);
        }
    }

    [Fact]
    public async Task SaveNowAsync_WhenWindowClosed_DoesNotThrowObjectDisposedException()
    {
        string root = Path.Combine(
            Path.GetTempPath(),
            "AiteBarTests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            await RunStaAsync(() =>
            {
                var store = new ZenEditorStore(root);
                var window = new ZenEditorWindow(store);
                window.Show();

                // Close window, which disposes _saveGate
                window.Close();

                // SaveNowAsync after close should safely return true without throwing ObjectDisposedException
                bool result = window.SaveNowAsync(force: true).GetAwaiter().GetResult();
                Assert.True(result);
            });
        }
        finally
        {
            TryDeleteDirectory(root);
        }
    }

    [Fact]
    public async Task DeleteCurrentDocumentAsync_WhenCancelled_PreservesDocument()
    {
        string root = Path.Combine(
            Path.GetTempPath(),
            "AiteBarTests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            await RunStaAsync(async () =>
            {
                var store = new ZenEditorStore(root);
                var window = new ZenEditorWindow(store);
                await window.InitializeWindowCoreAsync();

                window.Editor.Text = "Документ не должен быть удален";
                window.ConfirmDeleteDialog = _ => false;

                await window.DeleteCurrentDocumentAsync();

                Assert.Equal("Документ не должен быть удален", window.Editor.Text);
                IReadOnlyList<ZenEditorDocumentSummary> active = await store.ListAsync("Untitled");
                Assert.Single(active);
                IReadOnlyList<ZenEditorDocumentSummary> deleted = await store.ListDeletedAsync("Untitled");
                Assert.Empty(deleted);

                window.Close();
            });
        }
        finally
        {
            TryDeleteDirectory(root);
        }
    }

    [Fact]
    public async Task DeleteCurrentDocumentAsync_WhenConfirmed_DeletesDocumentAndLoadsNext()
    {
        string root = Path.Combine(
            Path.GetTempPath(),
            "AiteBarTests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            await RunStaAsync(async () =>
            {
                var store = new ZenEditorStore(root);
                ZenEditorDocument doc1 = await store.CreateAsync();
                doc1.Text = "Первый документ";
                await store.SaveAsync(doc1, createSnapshot: false);

                ZenEditorDocument doc2 = await store.CreateAsync();
                doc2.Text = "Второй документ";
                await store.SaveAsync(doc2, createSnapshot: false);

                var index = new ZenEditorStoreIndex { ActiveDocumentId = doc2.Id };
                await store.SaveIndexAsync(index);

                var window = new ZenEditorWindow(store);
                await window.InitializeWindowCoreAsync();

                Assert.Equal("Второй документ", window.Editor.Text);

                string? promptedTitle = null;
                window.ConfirmDeleteDialog = title =>
                {
                    promptedTitle = title;
                    return true;
                };

                await window.DeleteCurrentDocumentAsync();

                Assert.Equal("Второй документ", promptedTitle);
                Assert.Equal("Первый документ", window.Editor.Text);

                IReadOnlyList<ZenEditorDocumentSummary> active = await store.ListAsync("Untitled");
                Assert.Single(active);
                Assert.Equal(doc1.Id, active[0].Id);

                IReadOnlyList<ZenEditorDocumentSummary> deleted = await store.ListDeletedAsync("Untitled");
                Assert.Single(deleted);
                Assert.Equal(doc2.Id, deleted[0].Id);

                window.Close();
            });
        }
        finally
        {
            TryDeleteDirectory(root);
        }
    }

    [Fact]
    public async Task DeleteCurrentDocumentAsync_WhenLastDocumentDeleted_CreatesNewEmptyDocument()
    {
        string root = Path.Combine(
            Path.GetTempPath(),
            "AiteBarTests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            await RunStaAsync(async () =>
            {
                var store = new ZenEditorStore(root);
                var window = new ZenEditorWindow(store);
                await window.InitializeWindowCoreAsync();

                window.Editor.Text = "Единственный документ";
                await window.SaveNowAsync(force: true);

                window.ConfirmDeleteDialog = _ => true;
                await window.DeleteCurrentDocumentAsync();

                Assert.Equal(string.Empty, window.Editor.Text);

                IReadOnlyList<ZenEditorDocumentSummary> active = await store.ListAsync("Untitled");
                Assert.Single(active);
                IReadOnlyList<ZenEditorDocumentSummary> deleted = await store.ListDeletedAsync("Untitled");
                Assert.Single(deleted);

                window.Close();
            });
        }
        finally
        {
            TryDeleteDirectory(root);
        }
    }

    [Fact]
    public async Task ExitZones_ConfiguredWithHandCursorAndAccessibility()
    {
        string root = Path.Combine(
            Path.GetTempPath(),
            "AiteBarTests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            await RunStaAsync(async () =>
            {
                var store = new ZenEditorStore(root);
                var window = new ZenEditorWindow(store);
                await window.InitializeWindowCoreAsync();

                Assert.NotNull(window.LeftExitZone);
                Assert.NotNull(window.RightExitZone);

                Assert.Equal(System.Windows.Input.Cursors.Hand, window.LeftExitZone.Cursor);
                Assert.Equal(System.Windows.Input.Cursors.Hand, window.RightExitZone.Cursor);

                Assert.False(window.LeftExitZone.Focusable);
                Assert.False(window.RightExitZone.Focusable);

                Assert.Equal(System.Windows.Media.Brushes.Transparent, window.LeftExitZone.Background);
                Assert.Equal(System.Windows.Media.Brushes.Transparent, window.RightExitZone.Background);

                string expectedName = LocalizationService.Get("Common_Close");
                Assert.Equal(expectedName, System.Windows.Automation.AutomationProperties.GetName(window.LeftExitZone));
                Assert.Equal(expectedName, System.Windows.Automation.AutomationProperties.GetName(window.RightExitZone));

                window.Close();
            });
        }
        finally
        {
            TryDeleteDirectory(root);
        }
    }

    [Fact]
    public async Task ExitZones_Geometry_AdaptsToContainerWidth()
    {
        string root = Path.Combine(
            Path.GetTempPath(),
            "AiteBarTests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            await RunStaAsync(async () =>
            {
                var store = new ZenEditorStore(root);
                var window = new ZenEditorWindow(store);
                await window.InitializeWindowCoreAsync();

                window.EditorHost.Width = 1920;
                window.EditorHost.Height = 1080;
                window.Editor.Width = 760;
                window.UpdateExitZonesGeometry();

                // sideMargin = (1920 - 760) / 2 = 580; safetyMargin = 48 -> 532
                Assert.Equal(Visibility.Visible, window.LeftExitZone.Visibility);
                Assert.Equal(Visibility.Visible, window.RightExitZone.Visibility);
                Assert.Equal(532, window.LeftExitZone.Width);
                Assert.Equal(532, window.RightExitZone.Width);

                window.EditorHost.Width = 800;
                window.Editor.Width = 736;
                window.UpdateExitZonesGeometry();

                Assert.Equal(Visibility.Collapsed, window.LeftExitZone.Visibility);
                Assert.Equal(Visibility.Collapsed, window.RightExitZone.Visibility);

                window.Close();
            });
        }
        finally
        {
            TryDeleteDirectory(root);
        }
    }

    [Fact]
    public async Task ExitZones_Geometry_AdaptsToCustomSafetyMargin()
    {
        string root = Path.Combine(
            Path.GetTempPath(),
            "AiteBarTests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            await RunStaAsync(async () =>
            {
                var store = new ZenEditorStore(root);
                var settings = new AppSettingsService(Path.Combine(root, "settings.json"));
                settings.UpdateSettings(s => s.ZenEditorSideSafetyMargin = 96.0);

                var window = new ZenEditorWindow(store, settingsService: settings);
                await window.InitializeWindowCoreAsync();

                window.EditorHost.Width = 1920;
                window.EditorHost.Height = 1080;
                window.Editor.Width = 760;
                window.UpdateExitZonesGeometry();

                // sideMargin = 580; safetyMargin = 96 -> 484
                Assert.Equal(Visibility.Visible, window.LeftExitZone.Visibility);
                Assert.Equal(Visibility.Visible, window.RightExitZone.Visibility);
                Assert.Equal(484, window.LeftExitZone.Width);
                Assert.Equal(484, window.RightExitZone.Width);

                window.Close();
            });
        }
        finally
        {
            TryDeleteDirectory(root);
        }
    }

    [Fact]
    public async Task ExitZones_DisabledWhenExitOnSideClickIsFalse()
    {
        string root = Path.Combine(
            Path.GetTempPath(),
            "AiteBarTests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            await RunStaAsync(async () =>
            {
                var store = new ZenEditorStore(root);
                var settings = new AppSettingsService(Path.Combine(root, "settings.json"));
                settings.UpdateSettings(s => s.ZenEditorExitOnSideClick = false);

                var window = new ZenEditorWindow(store, settingsService: settings);
                await window.InitializeWindowCoreAsync();

                window.EditorHost.Width = 1920;
                window.EditorHost.Height = 1080;
                window.Editor.Width = 760;
                window.UpdateExitZonesGeometry();

                Assert.Equal(Visibility.Collapsed, window.LeftExitZone.Visibility);
                Assert.Equal(Visibility.Collapsed, window.RightExitZone.Visibility);

                window.Close();
            });
        }
        finally
        {
            TryDeleteDirectory(root);
        }
    }

    [Fact]
    public async Task ExitZones_HandleClick_ClosesUnlessSaveErrorVisible()
    {
        string root = Path.Combine(
            Path.GetTempPath(),
            "AiteBarTests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            await RunStaAsync(async () =>
            {
                var store = new ZenEditorStore(root);
                var window = new ZenEditorWindow(store);
                await window.InitializeWindowCoreAsync();

                int closingCount = 0;
                window.Closing += (_, e) =>
                {
                    closingCount++;
                    e.Cancel = true; // Prevent actual disposal during test
                };

                window.SaveErrorOverlay.Visibility = Visibility.Visible;
                window.HandleExitZoneClick();
                Assert.Equal(0, closingCount);

                window.SaveErrorOverlay.Visibility = Visibility.Collapsed;
                window.HandleExitZoneClick();
                Assert.Equal(1, closingCount);
            });
        }
        finally
        {
            TryDeleteDirectory(root);
        }
    }

    [Fact]
    public async Task ExitZones_MousePress_TriggersClose()
    {
        string root = Path.Combine(
            Path.GetTempPath(),
            "AiteBarTests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            await RunStaAsync(async () =>
            {
                var store = new ZenEditorStore(root);
                var window = new ZenEditorWindow(store);
                await window.InitializeWindowCoreAsync();

                int closingCount = 0;
                window.Closing += (_, e) =>
                {
                    closingCount++;
                    e.Cancel = true;
                };

                // 1. Mouse down on left zone -> closes
                window.TriggerExitZoneMouseDown(window.LeftExitZone);
                Assert.Equal(1, closingCount);

                // 2. Mouse down on right zone -> closes
                window.TriggerExitZoneMouseDown(window.RightExitZone);
                Assert.Equal(2, closingCount);
            });
        }
        finally
        {
            TryDeleteDirectory(root);
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        for (int i = 0; i < 10; i++)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, recursive: true);
                }
                return;
            }
            catch (IOException)
            {
                Thread.Sleep(50);
            }
            catch
            {
                return;
            }
        }
    }

    private static void AssertIconFont(
        IEnumerable<MenuItem> commands,
        string gesture,
        string expectedFont)
    {
        MenuItem command = commands.Single(item => item.InputGestureText == gesture);
        CenteredGlyphTextBlock icon = Assert.IsType<CenteredGlyphTextBlock>(command.Icon);
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

    private static Task RunStaAsync(Func<Task> action)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            SynchronizationContext.SetSynchronizationContext(
                new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));

            _ = Dispatcher.CurrentDispatcher.InvokeAsync(async () =>
            {
                try
                {
                    await action();
                    completion.SetResult();
                }
                catch (Exception ex)
                {
                    completion.SetException(ex);
                }
                finally
                {
                    Dispatcher.CurrentDispatcher.BeginInvokeShutdown(DispatcherPriority.Background);
                }
            });

            Dispatcher.Run();
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return completion.Task;
    }
}
