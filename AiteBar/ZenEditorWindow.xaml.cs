using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using FormsScreen = System.Windows.Forms.Screen;

namespace AiteBar;

[SupportedOSPlatform("windows6.1")]
public partial class ZenEditorWindow : DarkWindow
{
    private static readonly TimeSpan AutoSaveDelay = TimeSpan.FromMilliseconds(300);
    private static readonly TimeSpan SelectionCopyDelay = TimeSpan.FromMilliseconds(150);
    private static readonly TimeSpan SnapshotInterval = TimeSpan.FromMinutes(5);
    private static readonly IntPtr HwndTopmost = new(-1);
    private static readonly IntPtr HwndNotTopmost = new(-2);
    private const uint SwpShowWindow = 0x0040;
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoActivate = 0x0010;
    private static FontFamily MaterialMenuIconFont => FontHelper.Resolve(FontHelper.MaterialKey);
    private static readonly FontFamily SegoeMenuIconFont = new("Segoe UI");

    private static class MenuIcons
    {
        public const int NewDocument = 58586; // ic_fluent_document_add_16_regular
        public const int Open = 62849; // ic_fluent_open_16_regular
        public const int Export = 62465; // ic_fluent_document_arrow_up_16_regular
        public const int Undo = 0x21B6; // same Segoe UI glyph as Quick Note
        public const int Redo = 0x21B7; // same Segoe UI glyph as Quick Note
        public const int Cut = 0xE14E; // same Material glyph as TextEditingContextMenu
        public const int Copy = 0xF381; // same Fluent glyph as TextEditingContextMenu
        public const int Paste = 0xE14F; // same Material glyph as TextEditingContextMenu
        public const int SelectAll = 0xE162; // same Material glyph as TextEditingContextMenu
        public const int Theme = 0xF2F5; // same Fluent glyph as Quick Note
        public const int ThemeChoice = 58300; // ic_fluent_color_16_regular
        public const int Search = 63119; // ic_fluent_search_20_regular
        public const int Formatting = 63449; // ic_fluent_text_edit_style_20_regular
        public const int Bold = 63396; // ic_fluent_text_bold_20_regular
        public const int Italic = 63476; // ic_fluent_text_italic_20_regular
        public const int Underline = 63498; // ic_fluent_text_underline_20_regular
        public const int RecentlyDeleted = 62590; // ic_fluent_history_20_regular
    }

    private readonly ZenEditorStore _store;
    private readonly MainWindow? _mainWindow;
    private readonly DispatcherTimer _saveTimer;
    private readonly DispatcherTimer _selectionTimer;
    private readonly SemaphoreSlim _saveGate = new(1, 1);
    private readonly Dictionary<Guid, ZenEditorUndoHistory> _undoHistories = [];
    private ZenEditorStoreIndex _index = new();
    private ZenEditorDocument? _document;
    private ZenEditorTheme _theme = ZenEditorThemeCatalog.Get(null);
    private DateTime _lastSnapshotUtc = DateTime.UtcNow;
    private bool _isLoaded;
    private bool _suppressChanges;
    private bool _dirty;
    private bool _manualScroll;
    private bool _allowClose;
    private bool _closeInProgress;
    private bool _sessionEndingSubscribed;
    private bool _suppressSelectionCopy;
    private bool _suppressSearchChange;
    private int _editVersion;
    private string _previousText = string.Empty;
    private IReadOnlyList<ZenEditorTextStyle> _previousStyles = [];

    public ZenEditorWindow(ZenEditorStore store, MainWindow? mainWindow = null)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _mainWindow = mainWindow;
        InitializeComponent();

        _saveTimer = CreateTimer(AutoSaveDelay, async () => await SaveNowAsync());
        _selectionTimer = CreateTimer(SelectionCopyDelay, CopySelectionToClipboard);
        EditorHost.SizeChanged += (_, _) => UpdateEditorGeometry();
        RefreshContextMenu();
    }

    public void ShowFullScreen()
    {
        _mainWindow?.SetUtilityFullscreenSuppressed(true);
        Show();
        Activate();
    }

    internal void RestoreFromAiteBar()
    {
        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }

        _mainWindow?.SetUtilityFullscreenSuppressed(true);
        ApplyFullScreenBounds();
        Activate();
        Editor.Focus();
    }

    internal static FontFamily CreateThemeFontFamily(ZenEditorTheme theme)
    {
        try
        {
            return new FontFamily(
                new Uri(
                    "pack://application:,,,/AiteBar;component/Resources/ZenEditor/Fonts/",
                    UriKind.Absolute),
                $"./#{theme.FontResourceName}");
        }
        catch
        {
            return new FontFamily(theme.FontResourceName);
        }
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            ZenEditorLoadResult result = await _store.InitializeAsync();
            _index = result.Index;
            _theme = ZenEditorThemeCatalog.Get(_index.ThemeId);
            ApplyTheme(_theme);
            LoadDocumentIntoEditor(result.Document);
            ApplyFullScreenBounds();
            _isLoaded = true;
            if (Application.Current is not null)
            {
                Application.Current.SessionEnding += Application_SessionEnding;
                _sessionEndingSubscribed = true;
            }
            Editor.Focus();

            if (result.WasRecovered)
            {
                new DarkDialog(LocalizationService.Get("ZenEditor_Recovered")) { Owner = this }.ShowDialog();
                Editor.Focus();
            }
        }
        catch (Exception ex)
        {
            Logger.Log(ex);
            ShowSaveError(ex);
        }
    }

    private async void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (_allowClose || !_isLoaded)
        {
            return;
        }

        e.Cancel = true;
        if (_closeInProgress)
        {
            return;
        }

        _closeInProgress = true;
        try
        {
            if (await SaveNowAsync(force: true))
            {
                _allowClose = true;
                Close();
            }
        }
        finally
        {
            _closeInProgress = false;
        }
    }

    private void Window_Closed(object? sender, EventArgs e)
    {
        _mainWindow?.SetUtilityFullscreenSuppressed(false);
        if (_sessionEndingSubscribed && Application.Current is not null)
        {
            Application.Current.SessionEnding -= Application_SessionEnding;
            _sessionEndingSubscribed = false;
        }
        _saveTimer.Stop();
        _selectionTimer.Stop();
        _saveGate.Dispose();
    }

    private void Window_Activated(object sender, EventArgs e)
    {
        SetFullscreenTopmost(isTopmost: true);
        if (_isLoaded)
        {
            Editor.Focus();
        }
    }

    private async void Window_Deactivated(object sender, EventArgs e)
    {
        SetFullscreenTopmost(isTopmost: false);
        if (_isLoaded)
        {
            await SaveNowAsync(force: true);
        }
    }

    private void Window_StateChanged(object sender, EventArgs e)
    {
        _mainWindow?.SetUtilityFullscreenSuppressed(WindowState != WindowState.Minimized);
        if (WindowState == WindowState.Normal && _isLoaded)
        {
            Dispatcher.BeginInvoke(ApplyFullScreenBounds, DispatcherPriority.Loaded);
        }
    }

    private async void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        ModifierKeys modifiers = Keyboard.Modifiers;
        bool control = modifiers.HasFlag(ModifierKeys.Control);
        bool shift = modifiers.HasFlag(ModifierKeys.Shift);
        bool alt = modifiers.HasFlag(ModifierKeys.Alt);

        if (e.Key == Key.Escape && SearchOverlay.Visibility == Visibility.Visible)
        {
            HideSearch();
            e.Handled = true;
            return;
        }

        ZenEditorShortcutAction shortcut =
            ZenEditorShortcutResolver.Resolve(e.Key, modifiers);
        int themeIndex = ZenEditorShortcutResolver.GetThemeIndex(shortcut);
        if (themeIndex >= 0)
        {
            await ExecuteGuardedAsync(
                () => ChangeThemeAsync(ZenEditorThemeCatalog.All[themeIndex]));
            e.Handled = true;
            return;
        }

        switch (shortcut)
        {
            case ZenEditorShortcutAction.PreviousTheme:
                await ExecuteGuardedAsync(() => ChangeThemeAsync(
                    ZenEditorThemeCatalog.GetAdjacent(_theme.Id, -1)));
                e.Handled = true;
                return;
            case ZenEditorShortcutAction.NextTheme:
                await ExecuteGuardedAsync(() => ChangeThemeAsync(
                    ZenEditorThemeCatalog.GetAdjacent(_theme.Id, 1)));
                e.Handled = true;
                return;
            case ZenEditorShortcutAction.OpenSearch:
                ShowSearch();
                e.Handled = true;
                return;
            case ZenEditorShortcutAction.FindNext:
                FindSearchMatch(forward: true);
                e.Handled = true;
                return;
            case ZenEditorShortcutAction.FindPrevious:
                FindSearchMatch(forward: false);
                e.Handled = true;
                return;
        }

        if (control && e.Key == Key.N)
        {
            await ExecuteGuardedAsync(CreateNewDocumentAsync);
            e.Handled = true;
        }
        else if (control && e.Key == Key.O)
        {
            await ExecuteGuardedAsync(OpenDocumentPickerAsync);
            e.Handled = true;
        }
        else if (control && shift && e.Key == Key.S)
        {
            await ExecuteGuardedAsync(ExportCopyAsync);
            e.Handled = true;
        }
        else if (control && e.Key == Key.S)
        {
            await SaveNowAsync(force: true);
            e.Handled = true;
        }
        else if (control && shift && e.Key == Key.Z)
        {
            Redo();
            e.Handled = true;
        }
        else if (control && e.Key == Key.Y)
        {
            Redo();
            e.Handled = true;
        }
        else if (control && e.Key == Key.Z)
        {
            Undo();
            e.Handled = true;
        }
        else if (control && shift && e.Key == Key.V)
        {
            PastePlainText();
            e.Handled = true;
        }
        else if (control && e.Key == Key.V)
        {
            PastePlainText();
            e.Handled = true;
        }
        else if (e.Key == Key.Tab && !control && !alt)
        {
            Editor.SelectedText = "    ";
            Editor.CaretIndex = Editor.SelectionStart + Editor.SelectionLength;
            e.Handled = true;
        }
        else if (shift && e.Key == Key.F10)
        {
            OpenContextMenu();
            e.Handled = true;
        }
        else if (e.Key == Key.Apps)
        {
            OpenContextMenu();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
        }
    }

    private async void Editor_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_suppressChanges || _document is null)
        {
            return;
        }

        string previousText = _previousText;
        string currentText = Editor.Text;
        IReadOnlyList<ZenEditorTextStyle> previousStyles = _previousStyles;
        IReadOnlyList<ZenEditorTextChange> changes = Editor.LastPlainTextChanges;
        ZenEditorTextChange change = changes.Count == 1
            ? changes[0]
            : ZenEditorTextHelper.CalculateSingleChange(previousText, currentText);
        IReadOnlyList<ZenEditorTextStyle> currentStyles =
            changes.Count == 1 && Editor.CanTransformLastTextStyles
                ? ZenEditorTextHelper.ApplyTextChangeToStyles(
                    previousStyles,
                    change,
                    Editor.LastInsertedTextStyle,
                    currentText.Length)
                : Editor.CaptureTextStyles();
        ZenEditorUndoHistory history = GetUndoHistory(_document.Id);
        history.Record(
            previousText,
            currentText,
            previousStyles,
            currentStyles,
            changes.Count > 0 ? changes : [change],
            DateTime.UtcNow);
        _previousText = currentText;
        _previousStyles = currentStyles;
        _dirty = true;
        _manualScroll = false;
        _editVersion++;
        _document.HasEverContainedText |= Editor.Text.Length > 0;
        _document.ModifiedUtc = DateTime.UtcNow;
        UpdateTitle();
        bool needsProtectiveSnapshot =
            change.RemovedLength >= 1_000
            || (change.RemovedLength > 0 && change.AddedLength > 0);
        if (needsProtectiveSnapshot)
        {
            ZenEditorDocument preEditSnapshot = _document.Clone();
            preEditSnapshot.Text = previousText;
            preEditSnapshot.Styles = [.. previousStyles];
            preEditSnapshot.CaretIndex = Math.Clamp(change.Offset, 0, previousText.Length);
            preEditSnapshot.SelectionStart = preEditSnapshot.CaretIndex;
            preEditSnapshot.SelectionLength = 0;
            preEditSnapshot.HasEverContainedText |= previousText.Length > 0;
            try
            {
                await _store.SaveSnapshotAsync(preEditSnapshot);
                _lastSnapshotUtc = DateTime.UtcNow;
                await SaveNowAsync(force: true);
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
                ShowSaveError(ex);
                _saveTimer.Stop();
                _saveTimer.Start();
            }
        }
        else
        {
            _saveTimer.Stop();
            _saveTimer.Start();
        }
        KeepCaretInWorkingZone();
    }

    private void Editor_SelectionChanged(object sender, RoutedEventArgs e)
    {
        if (_suppressChanges || _suppressSelectionCopy || !_isLoaded)
        {
            _selectionTimer.Stop();
            return;
        }

        _selectionTimer.Stop();
        if (Editor.SelectionLength > 0)
        {
            _selectionTimer.Start();
        }
    }

    private void Editor_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _selectionTimer.Stop();
        CopySelectionToClipboard();
    }

    private void Editor_PreviewMouseWheel(object sender, MouseWheelEventArgs e) => _manualScroll = true;

    private void Editor_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        RefreshContextMenu();
    }

    private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_suppressSearchChange &&
            SearchOverlay.Visibility == Visibility.Visible)
        {
            FindSearchMatch(forward: true, restart: true);
        }
    }

    private void SearchTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            FindSearchMatch(
                forward: !Keyboard.Modifiers.HasFlag(ModifierKeys.Shift));
            e.Handled = true;
        }
    }

    private void FindPrevious_Click(object sender, RoutedEventArgs e) =>
        FindSearchMatch(forward: false);

    private void FindNext_Click(object sender, RoutedEventArgs e) =>
        FindSearchMatch(forward: true);

    private void CloseSearch_Click(object sender, RoutedEventArgs e) => HideSearch();

    private async void RetrySave_Click(object sender, RoutedEventArgs e)
    {
        if (await SaveNowAsync(force: true))
        {
            SaveErrorOverlay.Visibility = Visibility.Collapsed;
            Editor.Focus();
        }
    }

    private async void ExportCopy_Click(object sender, RoutedEventArgs e) =>
        await ExecuteGuardedAsync(ExportCopyAsync);

    private void DismissError_Click(object sender, RoutedEventArgs e)
    {
        SaveErrorOverlay.Visibility = Visibility.Collapsed;
        Editor.Focus();
    }

    internal async Task<bool> SaveNowAsync(bool force = false, bool createSnapshot = false)
    {
        _saveTimer.Stop();
        if (_document is null || (!force && !_dirty))
        {
            return true;
        }

        await _saveGate.WaitAsync();
        try
        {
            if (_document is null)
            {
                return true;
            }

            int version = _editVersion;
            Guid documentId = _document.Id;
            CaptureEditorState(_document);
            bool timedSnapshot = DateTime.UtcNow - _lastSnapshotUtc >= SnapshotInterval;
            await _store.SaveAsync(_document, createSnapshot || timedSnapshot);

            _index.ActiveDocumentId = _document.Id;
            await _store.SaveIndexAsync(_index);
            if (timedSnapshot || createSnapshot)
            {
                _lastSnapshotUtc = DateTime.UtcNow;
            }

            if (_document.Id == documentId && _editVersion == version)
            {
                _dirty = false;
            }

            SaveErrorOverlay.Visibility = Visibility.Collapsed;
            return true;
        }
        catch (Exception ex)
        {
            Logger.Log(ex);
            ShowSaveError(ex);
            return false;
        }
        finally
        {
            _saveGate.Release();
        }
    }

    private async Task CreateNewDocumentAsync()
    {
        if (_document is null || !await SaveNowAsync(force: true))
        {
            return;
        }

        if (string.IsNullOrEmpty(_document.Text) && !_document.HasEverContainedText)
        {
            Editor.Focus();
            return;
        }

        ZenEditorDocument created = await _store.CreateAsync();
        _index.ActiveDocumentId = created.Id;
        await _store.SaveIndexAsync(_index);
        LoadDocumentIntoEditor(created);
    }

    private async Task OpenDocumentPickerAsync()
    {
        if (_document is null || !await SaveNowAsync(force: true))
        {
            return;
        }

        while (true)
        {
            IReadOnlyList<ZenEditorDocumentSummary> summaries =
                await _store.ListAsync(LocalizationService.Get("ZenEditor_Untitled"));
            var picker = new ZenEditorDocumentPicker(summaries, _theme) { Owner = this };
            bool accepted = picker.ShowDialog() == true;
            if (!accepted || picker.SelectedDocumentId is not Guid selectedId)
            {
                Editor.Focus();
                return;
            }

            if (picker.DeleteRequested)
            {
                ZenEditorDocumentSummary? summary = summaries.FirstOrDefault(item => item.Id == selectedId);
                string title = summary?.Title ?? LocalizationService.Get("ZenEditor_Untitled");
                bool confirmed = new DarkDialog(
                    LocalizationService.Format("ZenEditor_DeleteConfirm", title),
                    isConfirm: true)
                {
                    Owner = this
                }.ShowDialog() == true;
                if (!confirmed)
                {
                    continue;
                }

                await _store.DeleteAsync(selectedId);
                if (_document.Id == selectedId)
                {
                    IReadOnlyList<ZenEditorDocumentSummary> remaining =
                        await _store.ListAsync(LocalizationService.Get("ZenEditor_Untitled"));
                    ZenEditorDocument next = remaining.Count > 0
                        ? await _store.LoadAsync(remaining[0].Id)
                        : await _store.CreateAsync();
                    _index.ActiveDocumentId = next.Id;
                    await _store.SaveIndexAsync(_index);
                    LoadDocumentIntoEditor(next);
                }
                continue;
            }

            if (selectedId != _document.Id)
            {
                ZenEditorDocument selected = await _store.LoadAsync(selectedId);
                _index.ActiveDocumentId = selected.Id;
                await _store.SaveIndexAsync(_index);
                LoadDocumentIntoEditor(selected);
            }

            Editor.Focus();
            return;
        }
    }

    private async Task OpenRecentlyDeletedAsync()
    {
        if (_document is null || !await SaveNowAsync(force: true))
        {
            return;
        }

        IReadOnlyList<ZenEditorDocumentSummary> deleted =
            await _store.ListDeletedAsync(
                LocalizationService.Get("ZenEditor_Untitled"));
        if (deleted.Count == 0)
        {
            new DarkDialog(LocalizationService.Get("ZenEditor_NoDeletedDocuments"))
            {
                Owner = this
            }.ShowDialog();
            Editor.Focus();
            return;
        }

        var picker = new ZenEditorDocumentPicker(
            deleted,
            _theme,
            restoreMode: true)
        {
            Owner = this
        };
        bool accepted = picker.ShowDialog() == true;
        if (!accepted ||
            !picker.RestoreRequested ||
            picker.SelectedDocumentId is not Guid selectedId)
        {
            Editor.Focus();
            return;
        }

        ZenEditorDocument restored = await _store.RestoreAsync(selectedId);
        _index.ActiveDocumentId = restored.Id;
        await _store.SaveIndexAsync(_index);
        LoadDocumentIntoEditor(restored);
    }

    private async Task ExportCopyAsync()
    {
        if (_document is null || !await SaveNowAsync(force: true))
        {
            return;
        }

        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            AddExtension = true,
            DefaultExt = ".txt",
            Filter = LocalizationService.Get("ZenEditor_TxtFilter"),
            FileName = ZenEditorTextHelper.CreateExportFileName(
                _document.Text,
                LocalizationService.Get("ZenEditor_Untitled")),
            InitialDirectory = Directory.Exists(_index.LastExportDirectory)
                ? _index.LastExportDirectory
                : null,
            OverwritePrompt = true
        };

        if (dialog.ShowDialog(this) != true)
        {
            Editor.Focus();
            return;
        }

        try
        {
            await File.WriteAllTextAsync(
                dialog.FileName,
                ZenEditorTextHelper.NormalizeExportText(_document.Text),
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            _index.LastExportDirectory = Path.GetDirectoryName(dialog.FileName) ?? string.Empty;
            await _store.SaveIndexAsync(_index);
        }
        catch (Exception ex)
        {
            Logger.Log(ex);
            ShowSaveError(ex);
        }
        finally
        {
            Editor.Focus();
        }
    }

    private async Task ChangeThemeAsync(ZenEditorTheme theme)
    {
        _theme = theme;
        _index.ThemeId = theme.Id;
        ApplyTheme(theme);
        await _store.SaveIndexAsync(_index);
        Editor.Focus();
    }

    private void LoadDocumentIntoEditor(ZenEditorDocument document)
    {
        _suppressChanges = true;
        try
        {
            _document = document;
            Editor.Text = document.Text ?? string.Empty;
            Editor.ApplyTextStyles(document.Styles);
            (int caret, int selectionStart, int selectionLength) =
                ZenEditorTextHelper.ClampSelection(
                    Editor.Text.Length,
                    document.CaretIndex,
                    document.SelectionStart,
                    document.SelectionLength);
            Editor.Select(selectionStart, selectionLength);
            Editor.CaretIndex = caret;
            Editor.ScrollToVerticalOffset(Math.Max(0, document.ScrollOffset));
            _dirty = false;
            _manualScroll = false;
            _editVersion = 0;
            _previousText = Editor.Text;
            _previousStyles = Editor.CaptureTextStyles();
            UpdateTitle();
        }
        finally
        {
            _suppressChanges = false;
        }

        Dispatcher.BeginInvoke(() =>
        {
            Editor.Focus();
            Editor.ScrollToVerticalOffset(Math.Max(0, document.ScrollOffset));
        }, DispatcherPriority.Loaded);
    }

    private void CaptureEditorState(ZenEditorDocument document)
    {
        document.Text = Editor.Text ?? string.Empty;
        document.Styles = Editor.CaptureTextStyles().ToList();
        document.CaretIndex = Editor.CaretIndex;
        document.SelectionStart = Editor.SelectionStart;
        document.SelectionLength = Editor.SelectionLength;
        document.ScrollOffset = Editor.VerticalOffset;
        document.HasEverContainedText |= document.Text.Length > 0;
    }

    private void ApplyTheme(ZenEditorTheme theme)
    {
        Brush background = BrushFrom(theme.Background);
        Brush text = BrushFrom(theme.Text);
        Brush selection = BrushFrom(theme.Selection);
        Brush selectionText = BrushFrom(theme.SelectionText);
        Brush caret = BrushFrom(theme.Caret);
        Brush header = BrushFrom(theme.Header);
        Brush separator = BrushFrom(theme.Separator);

        Background = background;
        RootGrid.Background = background;
        Editor.Background = Brushes.Transparent;
        Editor.Foreground = text;
        Editor.CaretBrush = caret;
        Editor.SelectionBrush = selection;
        Editor.SelectionTextBrush = selectionText;
        Editor.FontFamily = CreateThemeFontFamily(theme);
        Editor.FontSize = theme.FontSize;
        Editor.Document.FontFamily = Editor.FontFamily;
        Editor.Document.FontSize = theme.FontSize;
        Editor.Document.Foreground = text;
        Editor.EditorLineHeight = theme.FontSize * 1.5;
        Editor.ParagraphSpacing = theme.FontSize * 0.75;
        Editor.MaxWidth = theme.ColumnWidth;
        Editor.Width = theme.ColumnWidth;
        SaveErrorOverlay.Background = header;
        SaveErrorOverlay.BorderBrush = separator;
        SaveErrorText.Foreground = text;
        UpdateEditorGeometry();
    }

    private ContextMenu BuildContextMenu()
    {
        ContextMenu menu = AppContextMenuFactory.CreateMenu(this);
        menu.PlacementTarget = Editor;
        menu.PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                Close();
                e.Handled = true;
            }
        };

        menu.Items.Add(CreateMenuItem(
            MenuIcons.NewDocument,
            "ZenEditor_NewDocument",
            async () => await CreateNewDocumentAsync(),
            inputGesture: "Ctrl+N"));
        menu.Items.Add(CreateMenuItem(
            MenuIcons.Open,
            "ZenEditor_OpenDocument",
            async () => await OpenDocumentPickerAsync(),
            inputGesture: "Ctrl+O"));
        menu.Items.Add(CreateMenuItem(
            MenuIcons.RecentlyDeleted,
            "ZenEditor_RecentlyDeleted",
            async () => await OpenRecentlyDeletedAsync()));
        menu.Items.Add(CreateMenuSeparator());
        menu.Items.Add(CreateMenuItem(
            MenuIcons.Export,
            "ZenEditor_ExportTxt",
            async () => await ExportCopyAsync(),
            inputGesture: "Ctrl+Shift+S"));
        menu.Items.Add(CreateMenuItem(
            MenuIcons.Search,
            "ZenEditor_Search",
            ShowSearch,
            inputGesture: "Ctrl+F"));
        menu.Items.Add(CreateMenuSeparator());
        ZenEditorUndoHistory history = _document is null
            ? new ZenEditorUndoHistory()
            : GetUndoHistory(_document.Id);
        menu.Items.Add(CreateMenuItem(
            MenuIcons.Undo,
            "ZenEditor_Undo",
            Undo,
            history.CanUndo,
            "Ctrl+Z",
            iconFont: SegoeMenuIconFont));
        menu.Items.Add(CreateMenuItem(
            MenuIcons.Redo,
            "ZenEditor_Redo",
            Redo,
            history.CanRedo,
            "Ctrl+Y",
            iconFont: SegoeMenuIconFont));
        menu.Items.Add(CreateMenuSeparator());
        menu.Items.Add(CreateMenuItem(
            MenuIcons.Cut,
            "ZenEditor_Cut",
            () => Editor.Cut(),
            Editor.SelectionLength > 0,
            "Ctrl+X",
            iconFont: MaterialMenuIconFont));
        menu.Items.Add(CreateMenuItem(
            MenuIcons.Copy,
            "ZenEditor_Copy",
            () => Editor.Copy(),
            Editor.SelectionLength > 0,
            "Ctrl+C"));
        menu.Items.Add(CreateMenuItem(
            MenuIcons.Paste,
            "ZenEditor_Paste",
            PastePlainText,
            CanPastePlainText(),
            "Ctrl+V",
            iconFont: MaterialMenuIconFont));
        menu.Items.Add(CreateMenuItem(
            MenuIcons.SelectAll,
            "ZenEditor_SelectAll",
            Editor.SelectAll,
            Editor.Text.Length > 0,
            "Ctrl+A",
            iconFont: MaterialMenuIconFont));
        menu.Items.Add(CreateMenuSeparator());

        MenuItem formatting = CreateSubmenuItem(
            MenuIcons.Formatting,
            "ZenEditor_Formatting");
        formatting.Items.Add(CreateFormattingMenuItem(
            MenuIcons.Bold,
            "ZenEditor_Bold",
            EditingCommands.ToggleBold,
            "Ctrl+B",
            IsBoldActive()));
        formatting.Items.Add(CreateFormattingMenuItem(
            MenuIcons.Italic,
            "ZenEditor_Italic",
            EditingCommands.ToggleItalic,
            "Ctrl+I",
            IsItalicActive()));
        formatting.Items.Add(CreateFormattingMenuItem(
            MenuIcons.Underline,
            "ZenEditor_Underline",
            EditingCommands.ToggleUnderline,
            "Ctrl+U",
            IsUnderlineActive()));
        menu.Items.Add(formatting);

        MenuItem themes = CreateSubmenuItem(MenuIcons.Theme, "ZenEditor_Theme");
        foreach (ZenEditorTheme theme in ZenEditorThemeCatalog.All)
        {
            MenuItem themeItem = CreateMenuItem(
                MenuIcons.ThemeChoice,
                theme.DisplayNameKey,
                async () => await ChangeThemeAsync(theme),
                isActive: theme.Id == _theme.Id);
            themeItem.IsCheckable = true;
            themeItem.IsChecked = theme.Id == _theme.Id;
            themes.Items.Add(themeItem);
        }
        menu.Items.Add(themes);
        return menu;
    }

    private void RefreshContextMenu()
    {
        Editor.ContextMenu = BuildContextMenu();
    }

    private MenuItem CreateMenuItem(
        int glyph,
        string key,
        Action action,
        bool enabled = true,
        string? inputGesture = null,
        bool isActive = false,
        FontFamily? iconFont = null)
    {
        MenuItem item = CreateMenuItemShell(glyph, key, enabled, inputGesture, isActive, iconFont);
        item.Click += (_, _) => action();
        return item;
    }

    private MenuItem CreateMenuItem(
        int glyph,
        string key,
        Func<Task> action,
        bool enabled = true,
        string? inputGesture = null,
        bool isActive = false,
        FontFamily? iconFont = null)
    {
        MenuItem item = CreateMenuItemShell(glyph, key, enabled, inputGesture, isActive, iconFont);
        item.Click += async (_, _) => await ExecuteGuardedAsync(action);
        return item;
    }

    private MenuItem CreateSubmenuItem(int glyph, string key) =>
        CreateMenuItemShell(
            glyph,
            key,
            enabled: true,
            inputGesture: null,
            isActive: false,
            iconFont: null);

    private MenuItem CreateFormattingMenuItem(
        int glyph,
        string key,
        RoutedUICommand command,
        string inputGesture,
        bool isActive)
    {
        MenuItem item = CreateMenuItem(
            glyph,
            key,
            () =>
            {
                command.Execute(null, Editor);
                Editor.Focus();
            },
            inputGesture: inputGesture,
            isActive: isActive);
        item.IsCheckable = true;
        item.IsChecked = isActive;
        return item;
    }

    private bool IsBoldActive() =>
        Editor.Selection.GetPropertyValue(TextElement.FontWeightProperty)
            is FontWeight weight
        && weight >= FontWeights.Bold;

    private bool IsItalicActive() =>
        Editor.Selection.GetPropertyValue(TextElement.FontStyleProperty)
            is System.Windows.FontStyle style
        && (style == FontStyles.Italic || style == FontStyles.Oblique);

    private bool IsUnderlineActive() =>
        Editor.Selection.GetPropertyValue(Inline.TextDecorationsProperty)
            is TextDecorationCollection decorations
        && decorations.Any(decoration =>
            decoration.Location == TextDecorationLocation.Underline);

    private Separator CreateMenuSeparator() =>
        AppContextMenuFactory.CreateSeparator(this);

    private MenuItem CreateMenuItemShell(
        int glyph,
        string key,
        bool enabled,
        string? inputGesture,
        bool isActive,
        FontFamily? iconFont)
    {
        return AppContextMenuFactory.CreateItem(
            this,
            char.ConvertFromUtf32(glyph),
            LocalizationService.Get(key),
            isActive: isActive,
            isEnabled: enabled,
            inputGesture: inputGesture,
            iconFont: iconFont);
    }

    private static bool CanPastePlainText()
    {
        try
        {
            return Clipboard.ContainsText();
        }
        catch (ExternalException)
        {
            return false;
        }
    }

    private void OpenContextMenu()
    {
        RefreshContextMenu();
        Editor.ContextMenu!.IsOpen = true;
    }

    private void PastePlainText()
    {
        try
        {
            if (Clipboard.ContainsText())
            {
                string text = Clipboard.GetText(System.Windows.TextDataFormat.UnicodeText);
                Editor.SelectedText = text;
                Editor.CaretIndex = Editor.SelectionStart + Editor.SelectionLength;
            }
        }
        catch (ExternalException ex)
        {
            Logger.Log(ex);
        }
    }

    private void ShowSearch()
    {
        string selected = Editor.SelectionLength is > 0 and <= 256
            ? Editor.SelectedText
            : string.Empty;
        SearchOverlay.Visibility = Visibility.Visible;
        SearchStatusText.Text = string.Empty;
        if (!string.IsNullOrEmpty(selected))
        {
            _suppressSearchChange = true;
            SearchTextBox.Text = selected;
            _suppressSearchChange = false;
            FindSearchMatch(forward: true, restart: true);
        }

        SearchTextBox.Focus();
        SearchTextBox.SelectAll();
    }

    private void HideSearch()
    {
        SearchOverlay.Visibility = Visibility.Collapsed;
        SearchStatusText.Text = string.Empty;
        Editor.Focus();
    }

    private void FindSearchMatch(bool forward, bool restart = false)
    {
        if (SearchOverlay.Visibility != Visibility.Visible)
        {
            ShowSearch();
            if (string.IsNullOrEmpty(SearchTextBox.Text))
            {
                return;
            }
        }

        string query = SearchTextBox.Text;
        if (string.IsNullOrEmpty(query))
        {
            SearchStatusText.Text = string.Empty;
            return;
        }

        int startIndex = restart
            ? (forward ? 0 : Editor.Text.Length)
            : forward
                ? Editor.SelectionStart + Math.Max(1, Editor.SelectionLength)
                : Editor.SelectionStart - 1;
        int match = ZenEditorSearchHelper.Find(
            Editor.Text,
            query,
            startIndex,
            forward);
        if (match < 0)
        {
            SearchStatusText.Text =
                LocalizationService.Get("ZenEditor_NoSearchResults");
            SearchTextBox.Focus();
            return;
        }

        SearchStatusText.Text = string.Empty;
        _selectionTimer.Stop();
        _suppressSelectionCopy = true;
        try
        {
            Editor.Select(match, query.Length);
            Editor.Focus();
        }
        finally
        {
            _suppressSelectionCopy = false;
        }

        SearchTextBox.Focus();
    }

    private ZenEditorUndoHistory GetUndoHistory(Guid documentId)
    {
        if (!_undoHistories.TryGetValue(documentId, out ZenEditorUndoHistory? history))
        {
            history = new ZenEditorUndoHistory(500);
            _undoHistories.Add(documentId, history);
        }

        return history;
    }

    private void Undo()
    {
        if (_document is null
            || !GetUndoHistory(_document.Id).TryUndo(
                Editor.Text,
                out string text,
                out IReadOnlyList<ZenEditorTextStyle> styles,
                out int caret))
        {
            return;
        }

        ApplyHistoryState(text, styles, caret);
    }

    private void Redo()
    {
        if (_document is null
            || !GetUndoHistory(_document.Id).TryRedo(
                Editor.Text,
                out string text,
                out IReadOnlyList<ZenEditorTextStyle> styles,
                out int caret))
        {
            return;
        }

        ApplyHistoryState(text, styles, caret);
    }

    private void ApplyHistoryState(
        string text,
        IReadOnlyList<ZenEditorTextStyle> styles,
        int caret)
    {
        _suppressChanges = true;
        try
        {
            Editor.Text = text;
            Editor.ApplyTextStyles(styles);
            Editor.CaretIndex = Math.Clamp(caret, 0, text.Length);
            _previousText = text;
            _previousStyles = Editor.CaptureTextStyles();
        }
        finally
        {
            _suppressChanges = false;
        }

        _dirty = true;
        _editVersion++;
        if (_document is not null)
        {
            _document.ModifiedUtc = DateTime.UtcNow;
            _document.HasEverContainedText |= text.Length > 0;
        }
        UpdateTitle();
        _saveTimer.Stop();
        _saveTimer.Start();
    }

    private void CopySelectionToClipboard()
    {
        _selectionTimer.Stop();
        if (Editor.SelectionLength <= 0)
        {
            return;
        }

        try
        {
            Clipboard.SetText(Editor.SelectedText);
        }
        catch (ExternalException ex)
        {
            Logger.Log(ex);
        }
    }

    private void KeepCaretInWorkingZone()
    {
        if (_manualScroll || Editor.Text.Length == 0)
        {
            return;
        }

        Rect caret = Editor.GetCaretRect();
        if (caret.IsEmpty || Editor.ViewportHeight <= 0)
        {
            return;
        }

        double upper = Editor.ViewportHeight * 0.35;
        double lower = Editor.ViewportHeight * 0.70;
        if (caret.Top < upper)
        {
            Editor.ScrollToVerticalOffset(Math.Max(0, Editor.VerticalOffset - (upper - caret.Top)));
        }
        else if (caret.Bottom > lower)
        {
            Editor.ScrollToVerticalOffset(Editor.VerticalOffset + (caret.Bottom - lower));
        }
    }

    private void UpdateEditorGeometry()
    {
        if (EditorHost.ActualWidth <= 0 || EditorHost.ActualHeight <= 0)
        {
            return;
        }

        double availableWidth = Math.Max(0, EditorHost.ActualWidth - 64);
        Editor.Width = Math.Min(_theme.ColumnWidth, availableWidth);
        Editor.Margin = new Thickness(32, EditorHost.ActualHeight * 0.18, 32, 32);
    }

    private void UpdateTitle()
    {
        string title = ZenEditorTextHelper.GetDisplayTitle(
            Editor.Text,
            LocalizationService.Get("ZenEditor_Untitled"));
        Title = title;
    }

    private void ApplyFullScreenBounds()
    {
        IntPtr handle = new WindowInteropHelper(this).Handle;
        if (handle == IntPtr.Zero)
        {
            return;
        }

        FormsScreen screen = FormsScreen.AllScreens.FirstOrDefault(candidate =>
                                 string.Equals(
                                     candidate.DeviceName,
                                     _index.LastMonitorDeviceName,
                                     StringComparison.OrdinalIgnoreCase))
                             ?? FormsScreen.FromHandle(handle);
        _index.LastMonitorDeviceName = screen.DeviceName;
        System.Drawing.Rectangle bounds = screen.Bounds;
        SetWindowPos(
            handle,
            IsActive ? HwndTopmost : HwndNotTopmost,
            bounds.Left,
            bounds.Top,
            bounds.Width,
            bounds.Height,
            SwpShowWindow);
    }

    private void SetFullscreenTopmost(bool isTopmost)
    {
        IntPtr handle = new WindowInteropHelper(this).Handle;
        if (handle == IntPtr.Zero)
        {
            return;
        }

        SetWindowPos(
            handle,
            isTopmost ? HwndTopmost : HwndNotTopmost,
            0,
            0,
            0,
            0,
            SwpNoMove | SwpNoSize | SwpNoActivate);
    }

    private void ShowSaveError(Exception exception)
    {
        SaveErrorText.Text = LocalizationService.Format("ZenEditor_SaveError", exception.Message);
        SaveErrorOverlay.Visibility = Visibility.Visible;
        Dispatcher.BeginInvoke(
            () => RetrySaveButton.Focus(),
            DispatcherPriority.Input);
    }

    private async Task ExecuteGuardedAsync(Func<Task> action)
    {
        await ZenEditorAsyncCommandGuard.ExecuteAsync(
            action,
            exception =>
            {
                Logger.Log(exception);
                ShowSaveError(exception);
            });
    }

    private void Application_SessionEnding(object sender, SessionEndingCancelEventArgs e)
    {
        if (_document is null)
        {
            return;
        }

        _saveTimer.Stop();
        CaptureEditorState(_document);
        ZenEditorDocument document = _document.Clone();
        ZenEditorStoreIndex index = new()
        {
            ActiveDocumentId = document.Id,
            ThemeId = _index.ThemeId,
            LastMonitorDeviceName = _index.LastMonitorDeviceName,
            LastExportDirectory = _index.LastExportDirectory
        };

        try
        {
            Task save = Task.Run(async () =>
            {
                await _store.SaveAsync(document, createSnapshot: true).ConfigureAwait(false);
                await _store.SaveIndexAsync(index).ConfigureAwait(false);
            });
            if (!save.Wait(TimeSpan.FromSeconds(10)))
            {
                e.Cancel = true;
            }
        }
        catch (Exception ex)
        {
            Logger.Log(ex);
            e.Cancel = true;
        }
    }

    private static SolidColorBrush BrushFrom(string color) =>
        new((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(color));

    private DispatcherTimer CreateTimer(TimeSpan interval, Action action)
    {
        var timer = new DispatcherTimer { Interval = interval };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            action();
        };
        return timer;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(
        IntPtr hWnd,
        IntPtr hWndInsertAfter,
        int x,
        int y,
        int cx,
        int cy,
        uint flags);
}
