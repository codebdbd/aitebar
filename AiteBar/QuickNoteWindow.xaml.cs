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
        private const int WM_SYSCOMMAND = 0x0112;
        private const int SC_SIZE = 0xF000;
        private const int WMSZ_LEFT = 1;
        private const int WMSZ_RIGHT = 2;
        private const int WMSZ_TOP = 3;
        private const int WMSZ_TOPLEFT = 4;
        private const int WMSZ_TOPRIGHT = 5;
        private const int WMSZ_BOTTOM = 6;
        private const int WMSZ_BOTTOMLEFT = 7;
        private const int WMSZ_BOTTOMRIGHT = 8;
        internal static readonly TimeSpan ForcedSaveWaitTimeout = TimeSpan.FromSeconds(10);
        private readonly IQuickNotePersistence _noteService;
        private readonly IQuickNoteClipboard _clipboard;
        private readonly AppSettingsService _settingsService;
        private readonly DispatcherTimer _saveTimer;
        private readonly DispatcherTimer _geometrySaveTimer;
        private readonly DispatcherTimer _footerStatsTimer;
        private QuickNoteTheme _theme;
        private bool _loaded;
        private bool _hasPendingChanges;
        private readonly System.Threading.SemaphoreSlim _saveSemaphore = new(1, 1);
        private bool _saveAgainAfterCurrent;
        private long _changeVersion;
        private bool _isModalDialogOpen;
        private bool _documentLoaded;
        private TextRange? _preservedFormatSelection;
        private bool _footerStatsDirty = true;
        private bool _cachedEditorIsEmpty = true;
        private int _cachedEditorCharacterCount;
        private int _cachedEditorLineCount;
        private QuickNoteStatusKind _statusKind;
        private string? _statusArgument;
        private List<TextBlock>? _cachedTextBlocks;
        private List<System.Windows.Controls.Button>? _cachedButtons;
        private List<ToggleButton>? _cachedToggleButtons;
        private bool _closeSaveInProgress;
        private bool _closeAfterSave;
        private bool _disposed;
        private int _saveSuppressionCount;
        private readonly QuickNoteImageInteractionController _imageInteraction;
        internal QuickNoteImageInteractionController ImageInteractionController => _imageInteraction;
        private const int MaxLinkScanParagraphLength = 10_000;
        private readonly Dictionary<Paragraph, (string Text, List<(Match Match, QuickNoteDocumentFormatting.LinkType Type)> Matches)> _linkMatchCache = [];

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
            _noteService = noteService ?? throw new ArgumentNullException(nameof(noteService));
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _clipboard = clipboard ?? throw new ArgumentNullException(nameof(clipboard));
            System.Windows.DataObject.AddPastingHandler(TxtNote, TxtNote_Pasting);
            CommandManager.AddPreviewExecutedHandler(TxtNote, OnPreviewExecutedCommand);
            CommandManager.AddPreviewCanExecuteHandler(TxtNote, OnPreviewCanExecuteCommand);
            _theme = QuickNoteThemeCatalog.Find(_settingsService.Settings.QuickNoteThemeId);
            _saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(700) };
            _saveTimer.Tick += async (_, _) => await SaveNowAsync();
            _geometrySaveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
            _geometrySaveTimer.Tick += async (_, _) => await SaveGeometryNowAsync();
            _footerStatsTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(120) };
            _footerStatsTimer.Tick += (_, _) =>
            {
                _footerStatsTimer.Stop();
                UpdatePlaceholderAndStats();
            };
            BuildThemePalette();
            ApplyTheme(_theme);
        }

        private void ClearCaches()
        {
            _cachedTextBlocks = null;
            _cachedButtons = null;
            _cachedToggleButtons = null;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            EnsureDocumentLoadedForFirstPaint();

            _loaded = true;
            if (FindName("BtnPin") is System.Windows.Controls.Primitives.ToggleButton pinButton)
            {
                pinButton.IsChecked = _settingsService.Settings.QuickNotePinned;
            }
            UpdatePlaceholderAndStats();
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
            await Dispatcher.Yield(DispatcherPriority.Background);
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
            StopTimers();
            _imageInteraction.Dispose();
            _saveSemaphore.Dispose();
        }

        private void StopTimers()
        {
            _saveTimer.Stop();
            _geometrySaveTimer.Stop();
            _footerStatsTimer.Stop();
        }

        private void TxtNote_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            _linkMatchCache.Clear();
            _footerStatsDirty = true;
            ScheduleFooterStatsUpdate();
            if (!_loaded)
            {
                return;
            }

            _changeVersion++;
            _hasPendingChanges = true;
            if (_saveSuppressionCount == 0)
            {
                ScheduleSave();
            }
        }

        private void TxtNote_SelectionChanged(object sender, RoutedEventArgs e)
        {
            if (_imageInteraction.HasSelectedImage && _imageInteraction.SelectedImage is { } image)
            {
                try
                {
                    Logger.Log($"SelectionChanged: image is selected, selection starts at {TxtNote.Selection.Start.GetOffsetToPosition(TxtNote.Document.ContentStart)}");
                    if (!image.ElementStart.IsInSameDocument(TxtNote.Selection.Start))
                    {
                        Logger.Log("SelectionChanged: selected image is no longer in the same document. Clearing selection.");
                        _imageInteraction.ClearSelection();
                    }
                    else
                    {
                        bool isImageSelected = TxtNote.Selection.Start.CompareTo(image.ElementStart) == 0 &&
                                               TxtNote.Selection.End.CompareTo(image.ElementEnd) == 0;
                        Logger.Log($"SelectionChanged: isImageSelected = {isImageSelected}");
                        if (!isImageSelected)
                        {
                            Logger.Log("SelectionChanged: selection moved away from the image. Clearing selection.");
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
            ScheduleFooterStatsUpdate();
        }

        private void TxtNote_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if ((e.Key == Key.Delete || e.Key == Key.Back) && _imageInteraction.TryDeleteSelected())
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
            else if (Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift) && e.Key == Key.C)
            {
                TryCopyText(GetEditorText());
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
            Logger.Log($"PreviewMouseDown: ChangedButton = {e.ChangedButton}, OriginalSource = {e.OriginalSource?.GetType().Name}");
            if (e.ChangedButton == MouseButton.Left)
            {
                if (_imageInteraction.TrySelectFromMouseInput(e.OriginalSource as DependencyObject))
                {
                    Logger.Log("PreviewMouseDown Left: Image detected, handling selection.");
                    e.Handled = true;
                    TxtNote.Focus();
                    if (_imageInteraction.SelectedImage is { } selectedContainer)
                    {
                        TxtNote.Selection.Select(selectedContainer.ElementStart, selectedContainer.ElementEnd);
                        Logger.Log("PreviewMouseDown Left: Range selected for image container.");
                    }
                    return;
                }

                if ((Keyboard.Modifiers == ModifierKeys.Control || Keyboard.Modifiers == ModifierKeys.None) &&
                    e.ClickCount == 1 &&
                    TryOpenUrlAtMouse(e))
                {
                    Logger.Log("PreviewMouseDown Left: URL click handled.");
                    e.Handled = true;
                }
            }
            else if (e.ChangedButton == MouseButton.Right)
            {
                if (_imageInteraction.TrySelectFromMouseInput(e.OriginalSource as DependencyObject))
                {
                    Logger.Log("PreviewMouseDown Right: Image detected, handling selection (non-handled to open ContextMenu).");
                    TxtNote.Focus();
                    if (_imageInteraction.SelectedImage is { } selectedContainer)
                    {
                        TxtNote.Selection.Select(selectedContainer.ElementStart, selectedContainer.ElementEnd);
                        Logger.Log("PreviewMouseDown Right: Range selected for image container.");
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

            TxtNote.Cursor = (Keyboard.Modifiers == ModifierKeys.Control || Keyboard.Modifiers == ModifierKeys.None) &&
                             FindLinkAtMouse(e.GetPosition(TxtNote)) != null
                ? System.Windows.Input.Cursors.Hand
                : System.Windows.Input.Cursors.IBeam;
        }


        private void TxtNote_Pasting(object sender, DataObjectPastingEventArgs e)
        {
            Logger.Log("TxtNote_Pasting triggered.");
            if (_clipboard.TryGetImage(out BitmapSource? image) && image != null)
            {
                Logger.Log("TxtNote_Pasting: Image retrieved from clipboard, inserting.");
                e.CancelCommand();
                InsertImage(image);
            }
            else
            {
                Logger.Log("TxtNote_Pasting: Clipboard does not contain an image.");
            }
        }

        private void OnPreviewCanExecuteCommand(object sender, CanExecuteRoutedEventArgs e)
        {
            if ((e.Command == ApplicationCommands.Copy || e.Command == ApplicationCommands.Cut || e.Command == ApplicationCommands.Delete) && _imageInteraction.HasSelectedImage)
            {
                Logger.Log($"OnPreviewCanExecuteCommand: Command = {e.Command}, image is selected. Forcing CanExecute = true.");
                e.CanExecute = true;
                e.Handled = true;
            }
        }

        private void OnPreviewExecutedCommand(object sender, ExecutedRoutedEventArgs e)
        {
            Logger.Log($"OnPreviewExecutedCommand: Command = {e.Command}, image is selected = {_imageInteraction.HasSelectedImage}");
            if (e.Command == ApplicationCommands.Copy && _imageInteraction.HasSelectedImage)
            {
                if (_imageInteraction.SelectedImage != null &&
                    QuickNoteImageHelper.TryGetImageControl(_imageInteraction.SelectedImage, out Image? imageControl) &&
                    imageControl != null && imageControl.Source is BitmapSource source)
                {
                    Logger.Log("OnPreviewExecutedCommand: Copying image source to clipboard.");
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
                    Logger.Log("OnPreviewExecutedCommand: Cutting image, copying to clipboard and deleting.");
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
                Logger.Log("OnPreviewExecutedCommand: Deleting image.");
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

        internal async Task<bool> SaveNowAsync(bool force = false)
        {
            _saveTimer.Stop();
            if (!_loaded || (!_hasPendingChanges && !force))
            {
                return true;
            }

            if (force)
            {
                if (!await _saveSemaphore.WaitAsync(ForcedSaveWaitTimeout))
                {
                    SetStatus(QuickNoteStatusKind.SaveFailed);
                    return false;
                }
            }
            // If a timer save can't acquire the semaphore immediately, coalesce it with the current save.
            else if (!await _saveSemaphore.WaitAsync(0))
            {
                _saveAgainAfterCurrent = true;
                return true;
            }

            SetStatus(QuickNoteStatusKind.Saving);
            try
            {
                if (!_hasPendingChanges)
                {
                    UpdateStatusSaved();
                    return true;
                }

                do
                {
                    if (!_hasPendingChanges && !force)
                    {
                        return true;
                    }

                    if (await _noteService.HasExternalChangesAsync())
                    {
                        string conflictPath = await _noteService.SaveConflictCopyAsync(TxtNote.Document);
                        _hasPendingChanges = false;
                        _saveAgainAfterCurrent = false;
                        SetStatus(QuickNoteStatusKind.ConflictCopySaved, System.IO.Path.GetFileName(conflictPath));
                        return true;
                    }

                    _saveAgainAfterCurrent = false;
                    long savedVersion = _changeVersion;
                    try
                    {
                        await _noteService.SaveAsync(TxtNote.Document);
                    }
                    catch (QuickNoteExternalChangeException)
                    {
                        string conflictPath = await _noteService.SaveConflictCopyAsync(TxtNote.Document);
                        _hasPendingChanges = false;
                        _saveAgainAfterCurrent = false;
                        SetStatus(QuickNoteStatusKind.ConflictCopySaved, System.IO.Path.GetFileName(conflictPath));
                        return true;
                    }
                    if (_changeVersion == savedVersion)
                    {
                        _hasPendingChanges = false;
                    }
                    else
                    {
                        _hasPendingChanges = true;
                        _saveAgainAfterCurrent = true;
                    }
                }
                while (_saveAgainAfterCurrent || (force && _hasPendingChanges));

                UpdateStatusSaved();
                return true;
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
                SetStatus(QuickNoteStatusKind.SaveFailed);
                return false;
            }
            finally
            {
                _saveSemaphore.Release();
            }
        }

        private void UpdateStatusSaved()
        {
            SetStatus(QuickNoteStatusKind.SavedAt);
        }

        private void ScheduleSave()
        {
            SetStatus(QuickNoteStatusKind.Saving);
            _saveTimer.Stop();
            _saveTimer.Start();
        }

        private void ScheduleFooterStatsUpdate()
        {
            _footerStatsTimer.Stop();
            _footerStatsTimer.Start();
        }

        private void UpdatePlaceholderAndStats()
        {
            string selectedText = QuickNoteDocumentHelper.NormalizeLineEndings(TxtNote.Selection.Text);
            if (!string.IsNullOrEmpty(selectedText))
            {
                TxtPlaceholder.Visibility = Visibility.Collapsed;
                int selectedWithoutWhitespace = selectedText.Count(c => !char.IsWhiteSpace(c));
                TxtStats.Text = LocalizationService.Format("QuickNote_SelectedStats", selectedText.Length, selectedWithoutWhitespace);
                return;
            }

            if (_footerStatsDirty)
            {
                string text = GetEditorText();
                _cachedEditorIsEmpty = string.IsNullOrWhiteSpace(text);
                _cachedEditorCharacterCount = text.Length;
                _cachedEditorLineCount = string.IsNullOrEmpty(text) ? 0 : text.Count(c => c == '\n') + 1;
                _footerStatsDirty = false;
            }

            TxtPlaceholder.Visibility = _cachedEditorIsEmpty ? Visibility.Visible : Visibility.Collapsed;
            TxtStats.Text = LocalizationService.Format("QuickNote_Stats", _cachedEditorCharacterCount, _cachedEditorLineCount);
        }

        private async void BtnOpenFile_Click(object sender, RoutedEventArgs e)
        {
            if (!await SaveNowAsync(force: true))
            {
                return;
            }

            try
            {
                _noteService.OpenInEditor();
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
                SetStatus(QuickNoteStatusKind.OpenFailed);
            }
        }

        private void BtnTheme_Click(object sender, RoutedEventArgs e)
        {
            ThemePopup.IsOpen = true;
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
                TxtNote.Document.Blocks.Clear();
                TxtNote.Document.Blocks.Add(new Paragraph(new Run(string.Empty)));
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
            TxtNote.Selection.ApplyPropertyValue(TextElement.FontWeightProperty, FontWeights.Normal);
            TxtNote.Selection.ApplyPropertyValue(TextElement.FontStyleProperty, FontStyles.Normal);
            TxtNote.Selection.ApplyPropertyValue(TextElement.FontSizeProperty, QuickNoteDocumentFormatting.GetHeadingFontSizeForLevel(0));
            TxtNote.Selection.ApplyPropertyValue(Inline.TextDecorationsProperty, null);
            TxtNote.Selection.ApplyPropertyValue(TextElement.FontFamilyProperty, QuickNoteFonts.Default);
            TxtNote.Selection.ApplyPropertyValue(TextElement.ForegroundProperty, Brush(_theme.Text));
        }

        internal void MarkChangedAndScheduleSave()
        {
            if (!_loaded)
            {
                return;
            }

            _changeVersion++;
            _hasPendingChanges = true;
            ScheduleSave();
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
                _noteService.Load(TxtNote.Document);
                QuickNoteDocumentFormatting.NormalizeListLayout(TxtNote.Document);
                _imageInteraction.ClearSelection();
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
                TxtNote.Document.Blocks.Clear();
                TxtNote.Document.Blocks.Add(new Paragraph(new Run(string.Empty)));
                SetStatus(QuickNoteStatusKind.LoadFailed);
            }
            finally
            {
                TxtNote.IsUndoEnabled = restoreUndoEnabled;
                _documentLoaded = true;
                _footerStatsDirty = true;
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

        private void ResizeGrip_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not FrameworkElement { Tag: string edge } ||
                e.ButtonState != MouseButtonState.Pressed ||
                !QuickNoteResizeEdges.TryParse(edge, out QuickNoteResizeEdge resizeEdge))
            {
                return;
            }

            int direction = resizeEdge switch
            {
                QuickNoteResizeEdge.Left => WMSZ_LEFT,
                QuickNoteResizeEdge.Right => WMSZ_RIGHT,
                QuickNoteResizeEdge.Top => WMSZ_TOP,
                QuickNoteResizeEdge.TopLeft => WMSZ_TOPLEFT,
                QuickNoteResizeEdge.TopRight => WMSZ_TOPRIGHT,
                QuickNoteResizeEdge.Bottom => WMSZ_BOTTOM,
                QuickNoteResizeEdge.BottomLeft => WMSZ_BOTTOMLEFT,
                QuickNoteResizeEdge.BottomRight => WMSZ_BOTTOMRIGHT,
                _ => throw new ArgumentOutOfRangeException(nameof(resizeEdge))
            };

            var handle = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            ReleaseCapture();
            SendMessage(handle, WM_SYSCOMMAND, new IntPtr(SC_SIZE + direction), IntPtr.Zero);
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
            _statusKind = kind;
            _statusArgument = argument;
            TxtSaveStatus.Text = kind switch
            {
                QuickNoteStatusKind.Saving => LocalizationService.Get("QuickNote_Saving"),
                QuickNoteStatusKind.SavedAt => LocalizationService.Format("QuickNote_SavedAt", DateTime.Now.ToString("HH:mm")),
                QuickNoteStatusKind.LoadFailed => LocalizationService.Get("QuickNote_LoadFailed"),
                QuickNoteStatusKind.SaveFailed => LocalizationService.Get("QuickNote_SaveFailed"),
                QuickNoteStatusKind.OpenFailed => LocalizationService.Get("QuickNote_OpenFailed"),
                QuickNoteStatusKind.Copied => LocalizationService.Get("QuickNote_Copied"),
                QuickNoteStatusKind.CopyFailed => LocalizationService.Get("QuickNote_CopyFailed"),
                QuickNoteStatusKind.ImageInsertFailed => LocalizationService.Get("QuickNote_ImageInsertFailed"),
                QuickNoteStatusKind.LinkHighlightPaused => LocalizationService.Get("QuickNote_LinkHighlightPaused"),
                QuickNoteStatusKind.ConflictCopySaved => LocalizationService.Format("QuickNote_ConflictCopySavedAt", argument ?? string.Empty),
                _ => string.Empty
            };
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
            UpdatePlaceholderAndStats();
            SetStatus(_statusKind, _statusArgument);
        }

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
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
