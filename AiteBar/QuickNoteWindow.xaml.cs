using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media.Animation;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using FlowList = System.Windows.Documents.List;
using Forms = System.Windows.Forms;

namespace AiteBar
{
    [SupportedOSPlatform("windows6.1")]
    public partial class QuickNoteWindow : DarkWindow, IDisposable
    {
        internal static readonly TimeSpan ForcedSaveWaitTimeout = TimeSpan.FromSeconds(10);
        private readonly IQuickNotePersistence _noteService;
        private readonly IQuickNoteClipboard _clipboard;
        private readonly AppSettingsService _settingsService;
        private readonly QuickNoteSaveController _saveController;
        private readonly QuickNoteFooterStatsController _footerStatsController;
        private readonly DispatcherTimer _geometrySaveTimer;
        private QuickNoteTheme _theme;
        private bool _loaded;
        private bool _isModalDialogOpen;
        private bool _documentLoaded;
        private TextRange? _preservedFormatSelection;
        private QuickNoteStatusKind _statusKind;
        private string? _statusArgument;
        private bool _closeSaveInProgress;
        private bool _closeAfterSave;
        private bool _disposed;
        private int _saveSuppressionCount;
        private readonly QuickNoteImageInteractionController _imageInteraction;
        internal QuickNoteImageInteractionController ImageInteractionController => _imageInteraction;
        private readonly QuickNoteLinkHighlightController _linkHighlightController;
        private long _suppressAutoDismissUntilTick;
        private QuickNoteWindowInteraction? _windowInteraction;

        public QuickNoteWindow(QuickNoteService noteService, AppSettingsService settingsService)
            : this(new QuickNotePersistence(noteService), settingsService, new QuickNoteClipboard())
        {
        }

        internal QuickNoteWindow(IQuickNotePersistence noteService, AppSettingsService settingsService)
            : this(noteService, settingsService, new QuickNoteClipboard())
        {
        }

        internal QuickNoteWindow(IQuickNotePersistence noteService, AppSettingsService settingsService, IQuickNoteClipboard clipboard)
        {
            InitializeComponent();
            AddHandler(System.Windows.Controls.Primitives.ButtonBase.ClickEvent, new RoutedEventHandler(CodeBlockCopyButton_Click));
            _imageInteraction = new QuickNoteImageInteractionController(TxtNote);
            _linkHighlightController = new QuickNoteLinkHighlightController(ScheduleDocumentStylesUpdateImmediate);
            _noteService = noteService ?? throw new ArgumentNullException(nameof(noteService));
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _clipboard = clipboard ?? throw new ArgumentNullException(nameof(clipboard));
            _footerStatsController = new QuickNoteFooterStatsController(TxtNote, TxtStats);
            _saveController = new QuickNoteSaveController(
                _noteService,
                getDocument: () => TxtNote.Document,
                setStatus: SetStatus,
                updateStatusSaved: UpdateStatusSaved,
                isLoaded: () => _loaded);
            System.Windows.DataObject.AddPastingHandler(TxtNote, TxtNote_Pasting);
            CommandManager.AddPreviewExecutedHandler(TxtNote, OnPreviewExecutedCommand);
            CommandManager.AddPreviewCanExecuteHandler(TxtNote, OnPreviewCanExecuteCommand);
            _theme = QuickNoteThemeCatalog.Find(_settingsService.Settings.QuickNoteThemeId);
            _geometrySaveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
            _geometrySaveTimer.Tick += async (_, _) => await SaveGeometryNowAsync();
            BuildThemePalette();
            ApplyTheme(_theme);
        }

        private void ClearCaches()
        {
            _linkHighlightController.ClearCache();
        }

        internal void SuppressAutoDismiss(TimeSpan duration)
        {
            _suppressAutoDismissUntilTick = Environment.TickCount64 + (long)duration.TotalMilliseconds;
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            _windowInteraction = new QuickNoteWindowInteraction(
                System.Windows.Interop.HwndSource.FromHwnd(new System.Windows.Interop.WindowInteropHelper(this).Handle));
            if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000)) return;

            // Let DWM round the HWND, including its contents, border and shadow. The shell
            // also removes rounding when snapped/maximized; do not draw a separate rounded mask.
            const int dwmWindowCornerPreference = 33;
            int roundCorners = 2; // DWMWCP_ROUND
            int result = SetWindowCornerPreference(new System.Windows.Interop.WindowInteropHelper(this).Handle,
                dwmWindowCornerPreference, ref roundCorners, sizeof(int));
            if (result < 0) Logger.Log(Marshal.GetExceptionForHR(result)!);
        }

        [DllImport("dwmapi.dll", EntryPoint = "DwmSetWindowAttribute", ExactSpelling = true)]
        private static extern int SetWindowCornerPreference(IntPtr handle, int attribute, ref int preference, int size);

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            EnsureDocumentLoadedForFirstPaint();

            _loaded = true;
            if (FindName("BtnPin") is System.Windows.Controls.Primitives.ToggleButton pinButton)
            {
                pinButton.IsChecked = _settingsService.Settings.QuickNotePinned;
            }
            UpdateFooterStats();
            if (_statusKind != QuickNoteStatusKind.LoadFailed)
            {
                UpdateStatusSaved();
            }

            TxtNote.Focus();
            TxtNote.CaretPosition = TxtNote.Document.ContentEnd;
            ResetCaretFormatting();
            ScheduleDocumentStylesUpdate();

            ClearCaches();
            ApplyTheme(_theme);
        }

        protected override async void OnDeactivatedAutoDismiss()
        {
            if (_disposed) return;
            // Capture the cause before yielding: Snap Assist can transfer focus again while
            // the dispatcher is pending. It is part of arranging this note, not leaving it.
            if (_windowInteraction?.IsArrangingWindow == true) return;
            await Dispatcher.Yield(DispatcherPriority.Background);
            if (_disposed || IsPinned || IsActive || Environment.TickCount64 < _suppressAutoDismissUntilTick ||
                _windowInteraction?.IsArrangingWindow == true)
            {
                return;
            }

            if (!IsTransientUiOpen())
            {
                Close();
            }
        }

        private async void Window_Closing(object? sender, CancelEventArgs e)
        {
            if (_closeAfterSave)
            {
                return;
            }

            e.Cancel = true;
            if (_closeSaveInProgress)
            {
                return;
            }

            _closeSaveInProgress = true;
            StopTimers();
            try
            {
                if (!await SaveNowAsync(force: true))
                {
                    return;
                }

                try
                {
                    await SaveGeometryNowAsync();
                }
                catch (Exception ex)
                {
                    Logger.Log(ex);
                }

                // Editing remains possible while settings are written; drain those edits too.
                if (!await SaveNowAsync(force: true)) return;
                _closeAfterSave = true;
                Close();
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
            finally
            {
                _closeSaveInProgress = false;
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            Dispose();
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _windowInteraction?.Dispose();
            StopTimers();
            _imageInteraction.Dispose();
            _linkHighlightController.Dispose();
            _footerStatsController.Dispose();
            _saveController.Dispose();
        }

        private void StopTimers()
        {
            _saveController.Stop();
            _geometrySaveTimer.Stop();
            _footerStatsController.Stop();
        }

        private void TxtNote_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            _footerStatsController.ScheduleUpdate();
            if (!_loaded)
            {
                return;
            }

            if (e.UndoAction is UndoAction.Undo or UndoAction.Redo)
            {
                // WPF recreates embedded controls from XAML during history replay.
                // Restore runtime templates/events without editing text or clearing Redo.
                ConnectTaskItemEvents(TxtNote.Document);
            }
            else
            {
                TryAutoConvertTaskPrefix();
            }

            if (_saveSuppressionCount == 0)
            {
                _saveController.MarkChangedAndSchedule();
            }
        }

        private void TxtNote_SelectionChanged(object sender, RoutedEventArgs e)
        {
            if (_imageInteraction.HasSelectedImage && _imageInteraction.SelectedImage is { } image)
            {
                try
                {
                    if (!image.ElementStart.IsInSameDocument(TxtNote.Selection.Start))
                    {
                        _imageInteraction.ClearSelection();
                    }
                    else
                    {
                        bool isImageSelected = TxtNote.Selection.Start.CompareTo(image.ElementStart) == 0 &&
                                               TxtNote.Selection.End.CompareTo(image.ElementEnd) == 0;
                        if (!isImageSelected)
                        {
                            _imageInteraction.ClearSelection();
                        }
                    }
                }
                catch (ArgumentException argEx)
                {
                    Logger.Log($"SelectionChanged crashed with ArgumentException: {argEx.Message}. Clearing selection.");
                    _imageInteraction.ClearSelection();
                }
            }
            _footerStatsController.ScheduleUpdate(documentChanged: false);
        }

        private async void TxtNote_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.S)
            {
                e.Handled = true;
                await SaveNowAsync(force: true);
            }
            else if ((e.Key == Key.Delete || e.Key == Key.Back) && _imageInteraction.TryDeleteSelected())
            {
                e.Handled = true;
                MarkChangedAndScheduleSave();
                ScheduleFooterStatsUpdate();
            }
            else if (e.Key == Key.Escape)
            {
                if (_imageInteraction.HasSelectedImage)
                {
                    _imageInteraction.ClearSelection();
                    e.Handled = true;
                }
                else
                {
                    Close();
                    e.Handled = true;
                }
            }
            else if (Keyboard.Modifiers == ModifierKeys.None && e.Key == Key.Space)
            {
                if (TryAutoConvertMarkdownOnSpace())
                {
                    e.Handled = true;
                }
            }
            else if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.Z)
            {
                UndoEditor();
                e.Handled = true;
            }
            else if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.Y)
            {
                RedoEditor();
                e.Handled = true;
            }
            else if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.K)
            {
                InsertLinkFromDialog();
                e.Handled = true;
            }
            else if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.B)
            {
                ToggleFormatting(TextElement.FontWeightProperty, FontWeights.Bold, FontWeights.Normal);
                e.Handled = true;
            }
            else if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.I)
            {
                ToggleFormatting(TextElement.FontStyleProperty, FontStyles.Italic, FontStyles.Normal);
                e.Handled = true;
            }
            else if (Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Alt) && e.Key == Key.C)
            {
                BtnCode_Click(this, new RoutedEventArgs());
                e.Handled = true;
            }
            else if (Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift) && e.Key == Key.D8)
            {
                ApplyListFormatting(numbered: false);
                e.Handled = true;
            }
            else if (Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift) && e.Key == Key.D7)
            {
                ApplyListFormatting(numbered: true);
                e.Handled = true;
            }
            else if (Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift) && e.Key == Key.X)
            {
                ToggleTextDecoration(TextDecorationLocation.Strikethrough);
                e.Handled = true;
            }
            else if (Keyboard.Modifiers == ModifierKeys.None && e.Key == Key.Enter)
            {
                if (TryAutoConvertMarkdownOnEnter() || HandleTaskItemEnterKey())
                {
                    e.Handled = true;
                }
            }
            else if (Keyboard.Modifiers == ModifierKeys.None && e.Key == Key.Back)
            {
                if (HandleTaskItemBackspaceKey())
                {
                    e.Handled = true;
                }
            }
            else if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.U)
            {
                ToggleTextDecoration(TextDecorationLocation.Underline);
                e.Handled = true;
            }
            else if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.D1)
            {
                var (s, end) = GetSelectionOffsets();
                ApplyHeadingToSelectedLines(1, s, end);
                e.Handled = true;
            }
            else if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.D2)
            {
                var (s, end) = GetSelectionOffsets();
                ApplyHeadingToSelectedLines(2, s, end);
                e.Handled = true;
            }
            else if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.D3)
            {
                var (s, end) = GetSelectionOffsets();
                ApplyHeadingToSelectedLines(3, s, end);
                e.Handled = true;
            }
            else if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.D0)
            {
                var (s, end) = GetSelectionOffsets();
                ApplyHeadingToSelectedLines(0, s, end);
                e.Handled = true;
            }
            else if (_imageInteraction.HasSelectedImage &&
                     (e.Key is Key.Left or Key.Right or Key.Up or Key.Down or Key.Home or Key.End or Key.PageUp or Key.PageDown))
            {
                _imageInteraction.ClearSelection();
            }
        }

        private void TxtNote_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                if (_imageInteraction.TrySelectFromMouseInput(e.OriginalSource as DependencyObject))
                {
                    e.Handled = true;
                    TxtNote.Focus();
                    if (_imageInteraction.SelectedImage is { } selectedContainer)
                    {
                        TxtNote.Selection.Select(selectedContainer.ElementStart, selectedContainer.ElementEnd);
                    }
                    return;
                }

                if (e.ClickCount == 1 && TryCopyCodeBlockAtMouse(e.GetPosition(TxtNote)))
                {
                    e.Handled = true;
                    return;
                }

                if (e.ClickCount == 1 &&
                    ShouldActivateLink(Keyboard.Modifiers) &&
                    TryOpenUrlAtMouse(e))
                {
                    e.Handled = true;
                }
            }
            else if (e.ChangedButton == MouseButton.Right)
            {
                if (_imageInteraction.TrySelectFromMouseInput(e.OriginalSource as DependencyObject))
                {
                    TxtNote.Focus();
                    if (_imageInteraction.SelectedImage is { } selectedContainer)
                    {
                        TxtNote.Selection.Select(selectedContainer.ElementStart, selectedContainer.ElementEnd);
                    }
                }
            }
        }

        private void TxtNote_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (_imageInteraction.UpdateCursorFromMouseInput(e.OriginalSource as DependencyObject))
            {
                return;
            }

            TxtNote.Cursor = ShouldActivateLink(Keyboard.Modifiers) &&
                             FindLinkAtMouse(e.GetPosition(TxtNote)) != null
                ? System.Windows.Input.Cursors.Hand
                : System.Windows.Input.Cursors.IBeam;
        }

        internal static bool ShouldActivateLink(ModifierKeys modifiers) =>
            modifiers == ModifierKeys.Control;


        private void TxtNote_Pasting(object sender, DataObjectPastingEventArgs e)
        {
            if (_clipboard.TryGetImage(out BitmapSource? image) && image != null)
            {
                e.CancelCommand();
                InsertImage(image);
            }
        }

        private void OnPreviewCanExecuteCommand(object sender, CanExecuteRoutedEventArgs e)
        {
            if ((e.Command == ApplicationCommands.Copy || e.Command == ApplicationCommands.Cut || e.Command == ApplicationCommands.Delete) && _imageInteraction.HasSelectedImage)
            {
                e.CanExecute = true;
                e.Handled = true;
            }
        }

        private void OnPreviewExecutedCommand(object sender, ExecutedRoutedEventArgs e)
        {
            if (e.Command == ApplicationCommands.Copy && _imageInteraction.HasSelectedImage)
            {
                if (_imageInteraction.SelectedImage != null &&
                    QuickNoteImageHelper.TryGetImageControl(_imageInteraction.SelectedImage, out Image? imageControl) &&
                    imageControl != null && imageControl.Source is BitmapSource source)
                {
                    _clipboard.TrySetImage(source);
                    e.Handled = true;
                }
            }
            else if (e.Command == ApplicationCommands.Cut && _imageInteraction.HasSelectedImage)
            {
                if (_imageInteraction.SelectedImage != null &&
                    QuickNoteImageHelper.TryGetImageControl(_imageInteraction.SelectedImage, out Image? imageControl) &&
                    imageControl != null && imageControl.Source is BitmapSource source)
                {
                    if (_clipboard.TrySetImage(source))
                    {
                        _imageInteraction.TryDeleteSelected();
                        MarkChangedAndScheduleSave();
                        ScheduleFooterStatsUpdate();
                    }
                    e.Handled = true;
                }
            }
            else if (e.Command == ApplicationCommands.Delete && _imageInteraction.HasSelectedImage)
            {
                _imageInteraction.TryDeleteSelected();
                MarkChangedAndScheduleSave();
                ScheduleFooterStatsUpdate();
                e.Handled = true;
            }
        }

        private void TxtNote_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        }

        private void TxtNote_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetData(DataFormats.FileDrop) is string[] paths)
            {
                foreach (string path in paths)
                {
                    if (TryInsertImageFile(path))
                    {
                        break;
                    }
                }
            }

            e.Handled = true;
        }

        internal Task<bool> SaveNowAsync(bool force = false) => _saveController.SaveNowAsync(force);

        private void UpdateStatusSaved()
        {
            SetStatus(_noteService.HasLoadFailed ? QuickNoteStatusKind.LoadFailed : QuickNoteStatusKind.SavedAt);
        }

        private void ScheduleSave() => _saveController.Schedule();

        private void ScheduleFooterStatsUpdate() => _footerStatsController.ScheduleUpdate();

        private void UpdateFooterStats() => _footerStatsController.UpdateUi();

        private void BtnTheme_Click(object sender, RoutedEventArgs e)
        {
            ThemePopup.IsOpen = true;
        }

        private void BtnRecoveryCopy_Click(object sender, RoutedEventArgs e)
        {
            try { _noteService.RevealConflictCopy(); }
            catch (Exception ex)
            {
                Logger.Log(ex);
                SetStatus(QuickNoteStatusKind.OpenFailed);
            }
        }

        private async void BtnPin_Checked(object sender, RoutedEventArgs e)
        {
            _settingsService.UpdateSettings(s =>
            {
                s.QuickNotePinned = sender is System.Windows.Controls.Primitives.ToggleButton { IsChecked: true };
            });
            await SaveSettingsSafelyAsync();
            TxtNote.Focus();
        }

        private void BtnClear_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new DarkDialog(LocalizationService.Get("QuickNote_ClearConfirm"), isConfirm: true) { Owner = this };
            bool? result;
            _isModalDialogOpen = true;
            try
            {
                result = dialog.ShowDialog();
            }
            finally
            {
                _isModalDialogOpen = false;
            }

            if (result == true)
            {
                RunDocumentChangeWithoutAutoSave(() =>
                {
                    TxtNote.Document.Blocks.Clear();
                    TxtNote.Document.Blocks.Add(new Paragraph(new Run(string.Empty)));
                });
                MarkChangedAndScheduleSave();
                ScheduleFooterStatsUpdate();
            }
        }

        private void BtnUndo_Click(object sender, RoutedEventArgs e)
        {
            UndoEditor();
            TxtNote.Focus();
        }

        private void BtnRedo_Click(object sender, RoutedEventArgs e)
        {
            RedoEditor();
            TxtNote.Focus();
        }

        private void ResetCaretFormatting()
        {
            RunDocumentChangeWithoutAutoSave(() =>
            {
                TxtNote.Selection.ApplyPropertyValue(TextElement.FontWeightProperty, FontWeights.Normal);
                TxtNote.Selection.ApplyPropertyValue(TextElement.FontStyleProperty, FontStyles.Normal);
                TxtNote.Selection.ApplyPropertyValue(TextElement.FontSizeProperty, QuickNoteDocumentFormatting.GetHeadingFontSizeForLevel(0));
                TxtNote.Selection.ApplyPropertyValue(Inline.TextDecorationsProperty, null);
                TxtNote.Selection.ApplyPropertyValue(TextElement.FontFamilyProperty, QuickNoteFonts.Default);
                TxtNote.Selection.ApplyPropertyValue(TextElement.ForegroundProperty, Brush(_theme.Text));
            });
        }

        private void RunDocumentChangeWithoutAutoSave(Action change)
        {
            ArgumentNullException.ThrowIfNull(change);

            _saveSuppressionCount++;
            TxtNote.BeginChange();
            try
            {
                change();
            }
            finally
            {
                TxtNote.EndChange();
                _saveSuppressionCount--;
            }
        }

        internal void MarkChangedAndScheduleSave()
        {
            _saveController.MarkChangedAndSchedule();
        }

        public void ShowSimple(AppSettings settings)
        {
            EnsureDocumentLoadedForFirstPaint();

            var work = GetWorkArea(settings);
            var bounds = QuickNoteLayoutHelper.ClampBoundsToWorkArea(
                work,
                settings.QuickNoteLeft,
                settings.QuickNoteTop,
                settings.QuickNoteWidth,
                settings.QuickNoteHeight);

            if (HasSavedBounds(settings))
            {
                WindowStartupLocation = WindowStartupLocation.Manual;
                Left = bounds.Left;
                Top = bounds.Top;
                Width = bounds.Width;
                Height = bounds.Height;
            }
            else
            {
                WindowStartupLocation = WindowStartupLocation.CenterOwner;
            }

            Show();
            Activate();
        }

        internal void EnsureDocumentLoadedForFirstPaint()
        {
            if (_documentLoaded)
            {
                return;
            }

            bool restoreUndoEnabled = TxtNote.IsUndoEnabled;
            TxtNote.IsUndoEnabled = false;
            try
            {
                RunDocumentChangeWithoutAutoSave(() =>
                {
                    try
                    {
                        _noteService.Load(TxtNote.Document);
                        if (_noteService.HasLoadFailed) SetStatus(QuickNoteStatusKind.LoadFailed);
                        QuickNoteDocumentFormatting.NormalizeListLayout(TxtNote.Document);
                        ConnectTaskItemEvents(TxtNote.Document);
                        _imageInteraction.ClearSelection();
                    }
                    catch (Exception ex)
                    {
                        Logger.Log(ex);
                        TxtNote.Document.Blocks.Clear();
                        TxtNote.Document.Blocks.Add(new Paragraph(new Run(string.Empty)));
                        SetStatus(QuickNoteStatusKind.LoadFailed);
                    }
                });
            }
            finally
            {
                TxtNote.IsUndoEnabled = restoreUndoEnabled;
                _documentLoaded = true;
                _footerStatsController.ScheduleUpdate();
            }
        }

        private static bool HasSavedBounds(AppSettings settings) =>
            settings.QuickNoteLeft.HasValue ||
            settings.QuickNoteTop.HasValue ||
            settings.QuickNoteWidth.HasValue ||
            settings.QuickNoteHeight.HasValue;

        private bool IsTransientUiOpen()
        {
            if (_isModalDialogOpen || ThemePopup.IsOpen || TxtNote.ContextMenu?.IsOpen == true)
            {
                return true;
            }

            return FindVisualChildren<System.Windows.Controls.Button>(this)
                .Any(button => button.ContextMenu?.IsOpen == true);
        }

        private string GetEditorText()
        {
            string text = new TextRange(TxtNote.Document.ContentStart, TxtNote.Document.ContentEnd).Text;
            text = QuickNoteDocumentHelper.NormalizeLineEndings(text);
            return text.TrimEnd('\n');
        }

        private void UndoEditor()
        {
            if (!TxtNote.CanUndo)
            {
                return;
            }

            TxtNote.Undo();
        }

        private void RedoEditor()
        {
            if (!TxtNote.CanRedo)
            {
                return;
            }

            TxtNote.Redo();
        }

        private void SetStatus(QuickNoteStatusKind kind, string? argument = null)
        {
            bool incidental = kind is QuickNoteStatusKind.Copied or QuickNoteStatusKind.LinkHighlightPaused;
            if (incidental && _statusKind is QuickNoteStatusKind.LoadFailed or QuickNoteStatusKind.SaveFailed or QuickNoteStatusKind.ConflictCopySaved)
                return;
            _statusKind = kind;
            _statusArgument = argument;
            TxtSaveStatus.Text = QuickNoteFooterStatsController.FormatStatusText(kind, argument);
            UpdateStatusAppearance();
        }

        private void UpdateStatusAppearance()
        {
            bool attention = _statusKind is QuickNoteStatusKind.LoadFailed or QuickNoteStatusKind.SaveFailed
                or QuickNoteStatusKind.ConflictCopySaved or QuickNoteStatusKind.OpenFailed;
            TxtSaveStatus.Foreground = Brush(attention ? (_theme.IsDark ? "#F1BD74" : "#754000") : _theme.MutedText);
            BtnRecoveryCopy.Visibility = string.IsNullOrWhiteSpace(_noteService.LastConflictCopyPath)
                ? Visibility.Collapsed : Visibility.Visible;
        }
        private int GetTextOffset(TextPointer pointer)
        {
            return QuickNoteDocumentHelper.GetTextOffset(TxtNote.Document, pointer);
        }

        private void SetCaretOffset(int offset)
        {
            TextPointer? target = GetTextPointerAtOffset(offset);
            TxtNote.CaretPosition = target ?? TxtNote.Document.ContentEnd;
        }

        private void SelectEditorRange(int startOffset, int endOffset)
        {
            TextPointer? start = GetTextPointerAtOffset(startOffset);
            TextPointer? end = GetTextPointerAtOffset(endOffset);
            if (start == null || end == null)
            {
                return;
            }

            TxtNote.Selection.Select(start, end);
        }

        private void SelectEditorRangeByText(int startOffset, int endOffset, string expectedText)
        {
            SelectEditorRange(startOffset, endOffset);
            if (string.IsNullOrEmpty(expectedText))
            {
                return;
            }

            TextPointer start = TxtNote.Selection.Start;
            TextPointer end = TxtNote.Selection.End;
            while (end.CompareTo(TxtNote.Document.ContentEnd) < 0)
            {
                string selected = QuickNoteDocumentHelper.NormalizeLineEndings(new TextRange(start, end).Text).TrimEnd('\n');
                if (selected.Length >= expectedText.Length)
                {
                    break;
                }

                end = end.GetNextInsertionPosition(LogicalDirection.Forward)
                    ?? end.GetNextContextPosition(LogicalDirection.Forward)
                    ?? TxtNote.Document.ContentEnd;
            }

            TxtNote.Selection.Select(start, end);
        }

        private TextPointer? GetTextPointerAtOffset(int offset)
        {
            return QuickNoteDocumentHelper.GetTextPointerAtOffset(TxtNote.Document, offset);
        }

        private (int Start, int End) GetSelectionOffsets()
        {
            int start = GetTextOffset(TxtNote.Selection.Start);
            int end = GetTextOffset(TxtNote.Selection.End);
            return (Math.Min(start, end), Math.Max(start, end));
        }

        protected override void OnLocalizationChanged()
        {
            UpdateFooterStats();
            SetStatus(_statusKind, _statusArgument);
        }

    }

    internal enum QuickNoteStatusKind
    {
        None,
        Saving,
        SavedAt,
        LoadFailed,
        SaveFailed,
        OpenFailed,
        Copied,
        CopyFailed,
        ImageInsertFailed,
        LinkHighlightPaused,
        ConflictCopySaved
    }
}
