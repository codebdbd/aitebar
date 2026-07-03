using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media.Animation;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
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
        private readonly QuickNoteService _noteService;
        private readonly AppSettingsService _settingsService;
        private readonly DispatcherTimer _saveTimer;
        private readonly DispatcherTimer _geometrySaveTimer;
        private QuickNoteTheme _theme;
        private bool _loaded;
        private bool _hasPendingChanges;
        private readonly System.Threading.SemaphoreSlim _saveSemaphore = new(1, 1);
        private bool _saveAgainAfterCurrent;
        private long _changeVersion;
        private bool _isModalDialogOpen;
        private bool _updatingFormatComboSelection;
        private (int Start, int End)? _preservedFormatSelection;
        private QuickNoteStatusKind _statusKind;
        private string? _statusArgument;
        private System.Windows.Controls.MenuItem? _cachedConflictCopyMenuItem;
        private List<TextBlock>? _cachedTextBlocks;
        private List<System.Windows.Controls.Button>? _cachedButtons;
        private List<ToggleButton>? _cachedToggleButtons;

        public QuickNoteWindow(QuickNoteService noteService, AppSettingsService settingsService)
        {
            InitializeComponent();
            _noteService = noteService;
            _settingsService = settingsService;
            _theme = QuickNoteThemeCatalog.Find(_settingsService.Settings.QuickNoteThemeId);
            _saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(700) };
            _saveTimer.Tick += async (_, _) => await SaveNowAsync();
            _geometrySaveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
            _geometrySaveTimer.Tick += async (_, _) => await SaveGeometryNowAsync();
            BuildThemePalette();
            ApplyTheme(_theme);
        }

        private void ClearCaches()
        {
            _cachedTextBlocks = null;
            _cachedButtons = null;
            _cachedToggleButtons = null;
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            bool restoreUndoEnabled = TxtNote.IsUndoEnabled;
            TxtNote.IsUndoEnabled = false;
            try
            {
                await _noteService.LoadAsync(TxtNote.Document);
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
            }

            _loaded = true;
            if (FindName("BtnPin") is System.Windows.Controls.Primitives.ToggleButton pinButton)
            {
                pinButton.IsChecked = _settingsService.Settings.QuickNotePinned;
            }
            UpdateConflictMenuState();
            ClearCaches();
            ApplyTheme(_theme);
            UpdatePlaceholderAndStats();
            if (_statusKind != QuickNoteStatusKind.LoadFailed)
            {
                UpdateStatusSaved();
            }

            TxtNote.Focus();
            TxtNote.CaretPosition = TxtNote.Document.ContentEnd;
            ResetCaretFormatting();
        }

        private async void Window_Deactivated(object? sender, EventArgs e)
        {
            await Dispatcher.Yield(DispatcherPriority.Background);
            if (!_settingsService.Settings.QuickNotePinned && !IsTransientUiOpen())
            {
                Close();
            }
        }

        private async void Window_Closing(object? sender, CancelEventArgs e)
        {
            // Сохраняем перед закрытием
            await SaveNowAsync(force: true);
            await SaveGeometryNowAsync();
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            Dispose();
        }

        public void Dispose()
        {
            _saveSemaphore?.Dispose();
            _saveTimer?.Stop();
            _geometrySaveTimer?.Stop();
        }

        private void TxtNote_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            UpdatePlaceholderAndStats();
            if (!_loaded)
            {
                return;
            }

            _changeVersion++;
            _hasPendingChanges = true;
            ScheduleSave();
        }

        private void TxtNote_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Close();
                e.Handled = true;
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
            else if (Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift) && e.Key == Key.X)
            {
                ToggleTextDecoration(TextDecorationLocation.Strikethrough);
                e.Handled = true;
            }
            else if (Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift) && e.Key == Key.C)
            {
                System.Windows.Clipboard.SetText(GetEditorText());
                e.Handled = true;
            }
        }

        private void TxtNote_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if ((Keyboard.Modifiers == ModifierKeys.Control || Keyboard.Modifiers == ModifierKeys.None) &&
                e.ClickCount == 1 &&
                TryOpenUrlAtMouse(e))
            {
                e.Handled = true;
            }
        }

        private void TxtNote_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            TxtNote.Cursor = (Keyboard.Modifiers == ModifierKeys.Control || Keyboard.Modifiers == ModifierKeys.None) &&
                             FindLinkAtMouse(e.GetPosition(TxtNote)) != null
                ? System.Windows.Input.Cursors.Hand
                : System.Windows.Input.Cursors.IBeam;
        }

        private async Task<bool> SaveNowAsync(bool force = false)
        {
            _saveTimer.Stop();
            if (!_loaded || (!_hasPendingChanges && !force))
            {
                return true;
            }

            // If we can't acquire the semaphore immediately, mark that we need to save again after the current one finishes
            if (!await _saveSemaphore.WaitAsync(0))
            {
                _saveAgainAfterCurrent = true;
                return true;
            }

            SetStatus(QuickNoteStatusKind.Saving);
            try
            {
                do
                {
                    if (!_loaded || (!_hasPendingChanges && !force))
                    {
                        return true;
                    }

                    if (_noteService.HasExternalChanges())
                    {
                        string conflictPath = await _noteService.SaveConflictCopyAsync(TxtNote.Document);
                        _hasPendingChanges = false;
                        _saveAgainAfterCurrent = false;
                        SetStatus(QuickNoteStatusKind.ConflictCopySaved, System.IO.Path.GetFileName(conflictPath));
                        UpdateConflictMenuState();
                        return true;
                    }

                    _saveAgainAfterCurrent = false;
                    long savedVersion = _changeVersion;
                    await _noteService.SaveAsync(TxtNote.Document);
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

        private void UpdatePlaceholderAndStats()
        {
            string text = GetEditorText();
            TxtPlaceholder.Visibility = string.IsNullOrWhiteSpace(text) ? Visibility.Visible : Visibility.Collapsed;
            int lines = string.IsNullOrEmpty(text) ? 0 : text.Count(c => c == '\n') + 1;
            TxtStats.Text = LocalizationService.Format("QuickNote_Stats", text.Length, lines);
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
            await _settingsService.SaveAsync();
            TxtNote.Focus();
        }

        private void BtnMenu_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not System.Windows.Controls.Button button || button.ContextMenu == null)
            {
                return;
            }

            button.ContextMenu.PlacementTarget = button;
            button.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            button.ContextMenu.IsOpen = true;
        }

        private void BtnClear_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new DarkDialog(LocalizationService.Get("QuickNote_ClearConfirm"), isConfirm: true) { Owner = this };
            if (dialog.ShowDialog() == true)
            {
                TxtNote.Document.Blocks.Clear();
                TxtNote.Document.Blocks.Add(new Paragraph(new Run(string.Empty)));
                MarkChangedAndScheduleSave();
                UpdatePlaceholderAndStats();
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

        private void BtnOpenConflictCopy_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _noteService.OpenConflictCopy();
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
                SetStatus(QuickNoteStatusKind.OpenFailed);
            }
        }

        private void BtnBold_Click(object sender, RoutedEventArgs e) => ToggleFormatting(TextElement.FontWeightProperty, FontWeights.Bold, FontWeights.Normal);

        private void BtnItalic_Click(object sender, RoutedEventArgs e) => ToggleFormatting(TextElement.FontStyleProperty, FontStyles.Italic, FontStyles.Normal);

        private void BtnUnderline_Click(object sender, RoutedEventArgs e) => ToggleTextDecoration(TextDecorationLocation.Underline);

        private void BtnStrikethrough_Click(object sender, RoutedEventArgs e) => ToggleTextDecoration(TextDecorationLocation.Strikethrough);

        private void BtnCode_Click(object sender, RoutedEventArgs e) => ToggleFormatting(TextElement.FontFamilyProperty, new System.Windows.Media.FontFamily("Consolas"), new System.Windows.Media.FontFamily("Segoe UI"));

        private void BtnBullet_Click(object sender, RoutedEventArgs e) => PrefixSelectedLines("- ");

        private void BtnNumbered_Click(object sender, RoutedEventArgs e) => PrefixSelectedLines(numbered: true);

        private void BtnInsertLink_Click(object sender, RoutedEventArgs e) => InsertLinkFromDialog();

        private void FormatCombo_DropDownOpened(object sender, EventArgs e)
        {
            _preservedFormatSelection = GetSelectionOffsets();
            Dispatcher.BeginInvoke(() =>
            {
                if (_preservedFormatSelection is { } selection)
                {
                    SelectEditorRange(selection.Start, selection.End);
                }
            }, DispatcherPriority.Input);
        }

        private void CmbHeading_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_loaded || _updatingFormatComboSelection || sender is not ComboBox comboBox || comboBox.SelectedItem is not ComboBoxItem item)
            {
                return;
            }

            var selection = _preservedFormatSelection ?? GetSelectionOffsets();
            SelectEditorRange(selection.Start, selection.End);
            if (int.TryParse(item.Tag?.ToString(), out int headingLevel))
            {
                ApplyHeadingToSelectedLines(headingLevel, selection.Start, selection.End);
            }

            ResetFormatCombo(comboBox);
            _preservedFormatSelection = null;
        }

        private void CmbList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_loaded || _updatingFormatComboSelection || sender is not ComboBox comboBox || comboBox.SelectedItem is not ComboBoxItem item)
            {
                return;
            }

            var selection = _preservedFormatSelection ?? GetSelectionOffsets();
            SelectEditorRange(selection.Start, selection.End);
            string? listKind = item.Tag?.ToString();
            if (string.Equals(listKind, "bullet", StringComparison.Ordinal))
            {
                PrefixSelectedLines("-", numbered: false, selection.Start, selection.End);
            }
            else if (string.Equals(listKind, "numbered", StringComparison.Ordinal))
            {
                PrefixSelectedLines(string.Empty, numbered: true, selection.Start, selection.End);
            }

            ResetFormatCombo(comboBox);
            _preservedFormatSelection = null;
        }

        private void ResetFormatCombo(ComboBox comboBox)
        {
            _updatingFormatComboSelection = true;
            try
            {
                comboBox.SelectedIndex = 0;
            }
            finally
            {
                _updatingFormatComboSelection = false;
            }
        }

        private void BtnClearFormatting_Click(object sender, RoutedEventArgs e)
        {
            ClearSelectedFormatting();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed && !IsHeaderInteractiveSource(e.OriginalSource as DependencyObject))
            {
                DragMove();
            }
        }

        private bool IsHeaderInteractiveSource(DependencyObject? source)
        {
            while (source != null && source != HeaderBar)
            {
                if (source is System.Windows.Controls.Button or System.Windows.Controls.MenuItem or ComboBox)
                {
                    return true;
                }

                source = VisualTreeHelper.GetParent(source);
            }

            return false;
        }

        private void ToggleFormatting(DependencyProperty property, object enabledValue, object? disabledValue)
        {
            object current = TxtNote.Selection.GetPropertyValue(property);
            TxtNote.Selection.ApplyPropertyValue(property, IsFormattingEnabled(current, enabledValue) ? disabledValue : enabledValue);
            MarkChangedAndScheduleSave();
            TxtNote.Focus();
        }

        private void ToggleTextDecoration(TextDecorationLocation location)
        {
            object currentValue = TxtNote.Selection.GetPropertyValue(Inline.TextDecorationsProperty);
            TextDecorationCollection decorations = currentValue is TextDecorationCollection currentDecorations
                ? currentDecorations.Clone()
                : [];

            bool hasDecoration = decorations.Any(decoration => decoration.Location == location);
            if (hasDecoration)
            {
                foreach (var decoration in decorations.Where(decoration => decoration.Location == location).ToList())
                {
                    decorations.Remove(decoration);
                }
            }
            else
            {
                TextDecorationCollection source = location == TextDecorationLocation.Strikethrough
                    ? TextDecorations.Strikethrough
                    : TextDecorations.Underline;
                foreach (var decoration in source)
                {
                    decorations.Add(decoration);
                }
            }

            TxtNote.Selection.ApplyPropertyValue(Inline.TextDecorationsProperty, decorations.Count == 0 ? null : decorations);
            MarkChangedAndScheduleSave();
            TxtNote.Focus();
        }

        private static bool IsFormattingEnabled(object current, object enabledValue)
        {
            if (current == DependencyProperty.UnsetValue)
            {
                return false;
            }

            if (current is System.Windows.Media.FontFamily currentFont && enabledValue is System.Windows.Media.FontFamily enabledFont)
            {
                return currentFont.Source.Equals(enabledFont.Source, StringComparison.OrdinalIgnoreCase);
            }

            if (current is TextDecorationCollection currentDecorations && enabledValue is TextDecorationCollection enabledDecorations)
            {
                return currentDecorations.Count == enabledDecorations.Count && currentDecorations.Count > 0;
            }

            return Equals(current, enabledValue);
        }

        private void PrefixSelectedLines(string prefix = "", bool numbered = false)
        {
            var (selectionStart, selectionEnd) = GetSelectionOffsets();
            PrefixSelectedLines(prefix, numbered, selectionStart, selectionEnd);
        }

        private void PrefixSelectedLines(string prefix, bool numbered, int selectionStart, int selectionEnd)
        {
            string text = GetEditorText();
            QuickNoteRangeEdit edit = QuickNoteMarkdown.GetToggleListMarkerRangeEdit(text, selectionStart, selectionEnd, numbered);
            ApplyRangeEdit(edit);
            SelectEditorRange(edit.CaretOffset, edit.CaretOffset + edit.SelectionLength);
            MarkChangedAndScheduleSave();
            UpdatePlaceholderAndStats();
            TxtNote.Focus();
        }

        private void ApplyHeadingToSelectedLines(int headingLevel, int selectionStart, int selectionEnd)
        {
            ApplyHeadingFormattingToLineRange(selectionStart, selectionEnd, headingLevel);
            SelectEditorRange(selectionStart, selectionEnd);
            MarkChangedAndScheduleSave();
            UpdatePlaceholderAndStats();
            TxtNote.Focus();
        }

        private void ApplyHeadingFormattingToLineRange(int selectionStart, int selectionEnd, int headingLevel)
        {
            string text = GetEditorText();
            int start = Math.Clamp(Math.Min(selectionStart, selectionEnd), 0, text.Length);
            int end = Math.Clamp(Math.Max(selectionStart, selectionEnd), 0, text.Length);
            int lineStart = text.LastIndexOf('\n', Math.Max(0, start - 1));
            lineStart = lineStart < 0 ? 0 : lineStart + 1;
            int lineEnd = text.IndexOf('\n', end);
            lineEnd = lineEnd < 0 ? text.Length : lineEnd;

            TxtNote.BeginChange();
            try
            {
                int offset = lineStart;
                while (offset <= lineEnd)
                {
                    int nextBreak = text.IndexOf('\n', offset);
                    int currentEnd = nextBreak < 0 || nextBreak > lineEnd ? lineEnd : nextBreak;
                    if (currentEnd > offset && !string.IsNullOrWhiteSpace(text[offset..currentEnd]))
                    {
                        ApplyHeadingFormattingToRange(offset, currentEnd, headingLevel);
                    }

                    if (nextBreak < 0 || nextBreak >= lineEnd)
                    {
                        break;
                    }

                    offset = nextBreak + 1;
                }
            }
            finally
            {
                TxtNote.EndChange();
            }
        }

        private void ApplyHeadingFormattingToRange(int startOffset, int endOffset, int headingLevel)
        {
            TextPointer? start = GetTextPointerAtOffset(startOffset);
            TextPointer? end = GetTextPointerAtOffset(endOffset);
            if (start == null || end == null)
            {
                return;
            }

            var range = new TextRange(start, end);
            range.ApplyPropertyValue(TextElement.FontFamilyProperty, new System.Windows.Media.FontFamily("Segoe UI"));
            range.ApplyPropertyValue(TextElement.FontSizeProperty, QuickNoteMarkdown.GetHeadingFontSizeForLevel(headingLevel));
            range.ApplyPropertyValue(TextElement.FontWeightProperty, headingLevel == 0 ? FontWeights.Normal : FontWeights.SemiBold);
            range.ApplyPropertyValue(TextElement.FontStyleProperty, FontStyles.Normal);
        }

        private void InsertLinkFromDialog()
        {
            string selectedText = TxtNote.Selection.Text.Trim();
            var dialog = new QuickNoteLinkDialog(selectedText, string.Empty) { Owner = this };
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

            if (result != true)
            {
                TxtNote.Focus();
                return;
            }

            InsertHyperlinkAtSelection(dialog.LinkText, dialog.Url);
            MarkChangedAndScheduleSave();
            UpdatePlaceholderAndStats();
            TxtNote.Focus();
        }

        private void InsertHyperlinkAtSelection(string linkText, string url)
        {
            int insertionOffset = GetSelectionOffsets().Start;
            TxtNote.BeginChange();
            try
            {
                TxtNote.Selection.Text = string.Empty;
                TextPointer? insertionPointer = GetTextPointerAtOffset(insertionOffset);
                if (insertionPointer == null)
                {
                    return;
                }

                InsertHyperlinkAtPointer(insertionPointer, QuickNoteMarkdown.CreateHyperlink(linkText, url));
            }
            finally
            {
                TxtNote.EndChange();
            }

            SetCaretOffset(insertionOffset + linkText.Length);
        }

        private static void InsertHyperlinkAtPointer(TextPointer pointer, Hyperlink hyperlink)
        {
            if (pointer.Parent is Run run)
            {
                InsertHyperlinkInRun(run, pointer, hyperlink);
                return;
            }

            if (pointer.Parent is Span span)
            {
                InsertInlineInCollection(span.Inlines, pointer, hyperlink);
                return;
            }

            if (pointer.Paragraph is { } paragraph)
            {
                InsertInlineInCollection(paragraph.Inlines, pointer, hyperlink);
            }
        }

        private static void InsertHyperlinkInRun(Run run, TextPointer pointer, Hyperlink hyperlink)
        {
            InlineCollection? siblings = GetInlineSiblings(run);
            if (siblings == null)
            {
                return;
            }

            int splitOffset = new TextRange(run.ContentStart, pointer).Text.Length;
            splitOffset = Math.Clamp(splitOffset, 0, run.Text.Length);
            string before = run.Text[..splitOffset];
            string after = run.Text[splitOffset..];

            run.Text = before;
            Inline anchor = run;
            if (string.IsNullOrEmpty(before))
            {
                siblings.InsertBefore(run, hyperlink);
                anchor = hyperlink;
                siblings.Remove(run);
            }
            else
            {
                siblings.InsertAfter(run, hyperlink);
                anchor = hyperlink;
            }

            if (!string.IsNullOrEmpty(after))
            {
                siblings.InsertAfter(anchor, CloneRunWithText(run, after));
            }
        }

        private static void InsertInlineInCollection(InlineCollection inlines, TextPointer pointer, Inline inline)
        {
            Inline? nextInline = pointer.GetAdjacentElement(LogicalDirection.Forward) as Inline;
            if (nextInline != null && ContainsInline(inlines, nextInline))
            {
                inlines.InsertBefore(nextInline, inline);
                return;
            }

            Inline? previousInline = pointer.GetAdjacentElement(LogicalDirection.Backward) as Inline;
            if (previousInline != null && ContainsInline(inlines, previousInline))
            {
                inlines.InsertAfter(previousInline, inline);
                return;
            }

            inlines.Add(inline);
        }

        private static InlineCollection? GetInlineSiblings(Inline inline)
        {
            return inline.Parent switch
            {
                Paragraph paragraph => paragraph.Inlines,
                Span span => span.Inlines,
                _ => null
            };
        }

        private static bool ContainsInline(InlineCollection inlines, Inline inline)
        {
            return inlines.Cast<Inline>().Any(candidate => ReferenceEquals(candidate, inline));
        }

        private static Run CloneRunWithText(Run source, string text)
        {
            return new Run(text)
            {
                FontFamily = source.FontFamily,
                FontSize = source.FontSize,
                FontStretch = source.FontStretch,
                FontStyle = source.FontStyle,
                FontWeight = source.FontWeight,
                Foreground = source.Foreground,
                Background = source.Background,
                TextDecorations = source.TextDecorations?.Clone()
            };
        }

        private void SetEditorPlainText(string text)
        {
            TxtNote.Document.Blocks.Clear();
            var paragraph = new Paragraph
            {
                Margin = new Thickness(0),
                FontFamily = new System.Windows.Media.FontFamily("Segoe UI"),
                FontSize = 14,
                FontWeight = FontWeights.Normal,
                FontStyle = FontStyles.Normal
            };

            string[] lines = QuickNoteDocumentHelper.NormalizeLineEndings(text).Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                if (i > 0)
                {
                    paragraph.Inlines.Add(new LineBreak());
                }

                paragraph.Inlines.Add(new Run(lines[i])
                {
                    FontFamily = new System.Windows.Media.FontFamily("Segoe UI"),
                    FontWeight = FontWeights.Normal,
                    FontStyle = FontStyles.Normal
                });
            }

            TxtNote.Document.Blocks.Add(paragraph);
        }

        private void ApplyRangeEdit(QuickNoteRangeEdit edit)
        {
            TextPointer? start = GetTextPointerAtOffset(edit.StartOffset);
            TextPointer? end = GetTextPointerAtOffset(edit.StartOffset + edit.RemoveLength);
            if (start == null || end == null)
            {
                return;
            }

            TxtNote.BeginChange();
            try
            {
                new TextRange(start, end).Text = edit.InsertText;
            }
            finally
            {
                TxtNote.EndChange();
            }
        }

        private void ClearSelectedFormatting()
        {
            string text = GetEditorText();
            var (selectionStart, selectionEnd) = GetSelectionOffsets();
            QuickNoteRangeEdit edit = QuickNoteMarkdown.GetClearLineMarkerRangeEdit(text, selectionStart, selectionEnd);
            if (!(edit.RemoveLength == edit.InsertText.Length &&
                  string.Equals(text.Substring(edit.StartOffset, edit.RemoveLength), edit.InsertText, StringComparison.Ordinal)))
            {
                ApplyRangeEdit(edit);
                SetCaretOffset(edit.CaretOffset);
            }

            TxtNote.Selection.ApplyPropertyValue(TextElement.FontWeightProperty, FontWeights.Normal);
            TxtNote.Selection.ApplyPropertyValue(TextElement.FontStyleProperty, FontStyles.Normal);
            TxtNote.Selection.ApplyPropertyValue(TextElement.FontSizeProperty, QuickNoteMarkdown.GetHeadingFontSizeForLevel(0));
            TxtNote.Selection.ApplyPropertyValue(Inline.TextDecorationsProperty, null);
            TxtNote.Selection.ApplyPropertyValue(TextElement.FontFamilyProperty, new System.Windows.Media.FontFamily("Segoe UI"));
            TxtNote.Selection.ApplyPropertyValue(TextElement.ForegroundProperty, Brush(_theme.Text));
            MarkChangedAndScheduleSave();
            TxtNote.Focus();
        }

        private void ResetCaretFormatting()
        {
            TxtNote.Selection.ApplyPropertyValue(TextElement.FontWeightProperty, FontWeights.Normal);
            TxtNote.Selection.ApplyPropertyValue(TextElement.FontStyleProperty, FontStyles.Normal);
            TxtNote.Selection.ApplyPropertyValue(TextElement.FontSizeProperty, QuickNoteMarkdown.GetHeadingFontSizeForLevel(0));
            TxtNote.Selection.ApplyPropertyValue(Inline.TextDecorationsProperty, null);
            TxtNote.Selection.ApplyPropertyValue(TextElement.FontFamilyProperty, new System.Windows.Media.FontFamily("Segoe UI"));
            TxtNote.Selection.ApplyPropertyValue(TextElement.ForegroundProperty, Brush(_theme.Text));
        }

        private void MarkChangedAndScheduleSave()
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
            var work = GetWorkArea();
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

        private static bool HasSavedBounds(AppSettings settings) =>
            settings.QuickNoteLeft.HasValue ||
            settings.QuickNoteTop.HasValue ||
            settings.QuickNoteWidth.HasValue ||
            settings.QuickNoteHeight.HasValue;

        private bool IsTransientUiOpen()
        {
            if (_isModalDialogOpen || ThemePopup.IsOpen || TxtNote.ContextMenu?.IsOpen == true || CmbHeading.IsDropDownOpen || CmbList.IsDropDownOpen)
            {
                return true;
            }

            return FindVisualChildren<System.Windows.Controls.Button>(this)
                .Any(button => button.ContextMenu?.IsOpen == true);
        }

        private void ResizeGrip_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not FrameworkElement { Tag: string edge } || e.ButtonState != MouseButtonState.Pressed)
            {
                return;
            }

            int direction = edge switch
            {
                "Left" => WMSZ_LEFT,
                "Right" => WMSZ_RIGHT,
                "Top" => WMSZ_TOP,
                "TopLeft" => WMSZ_TOPLEFT,
                "TopRight" => WMSZ_TOPRIGHT,
                "Bottom" => WMSZ_BOTTOM,
                "BottomLeft" => WMSZ_BOTTOMLEFT,
                _ => WMSZ_BOTTOMRIGHT
            };

            var handle = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            ReleaseCapture();
            SendMessage(handle, WM_SYSCOMMAND, new IntPtr(SC_SIZE + direction), IntPtr.Zero);
        }

        private void BuildThemePalette()
        {
            ThemePalette.Children.Clear();
            foreach (var theme in QuickNoteThemeCatalog.Themes)
            {
                var button = new System.Windows.Controls.Button
                {
                    Width = 36,
                    Height = 36,
                    Margin = new Thickness(6),
                    Background = Brush(theme.Background),
                    BorderBrush = Brush(theme.Id == _theme.Id ? theme.Accent : "#00000000"),
                    BorderThickness = new Thickness(theme.Id == _theme.Id ? 2 : 0),
                    Cursor = System.Windows.Input.Cursors.Hand
                };

                button.Template = CreateSwatchTemplate();
                button.Click += async (_, _) =>
                {
                    _theme = theme;
                    _settingsService.UpdateSettings(s =>
                    {
                        s.QuickNoteThemeId = theme.Id;
                    });
                    ClearCaches();
                    ApplyTheme(theme);
                    BuildThemePalette();
                    ThemePopup.IsOpen = false;
                    await _settingsService.SaveAsync();
                };
                ThemePalette.Children.Add(button);
            }
        }

        private static ControlTemplate CreateSwatchTemplate()
        {
            var border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(18));
            border.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding(nameof(Background)) { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
            border.SetBinding(Border.BorderBrushProperty, new System.Windows.Data.Binding(nameof(BorderBrush)) { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
            border.SetBinding(Border.BorderThicknessProperty, new System.Windows.Data.Binding(nameof(BorderThickness)) { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
            return new ControlTemplate(typeof(System.Windows.Controls.Button)) { VisualTree = border };
        }

        private void ApplyTheme(QuickNoteTheme theme)
        {
            var background = Brush(theme.Background);
            var border = Brush(theme.Border);
            var text = Brush(theme.Text);
            var muted = Brush(theme.MutedText);
            var accent = Brush(theme.Accent);
            var codeBackground = Brush(theme.CodeBackground);
            var codeText = Brush(theme.CodeText);
            var link = Brush(theme.Link);
            var iconColor = theme.IsDark ? Brush("#AFAFB7") : Brush("#000000");

            if (Content is Grid root && root.Children.OfType<Border>().FirstOrDefault() is { } shell)
            {
                shell.Background = background;
                shell.BorderBrush = border;
            }

            TxtNote.Foreground = text;
            TxtNote.CaretBrush = accent;
            TxtPlaceholder.Foreground = muted;
            TxtSaveStatus.Foreground = muted;
            TxtStats.Foreground = muted;
            HeaderBar.BorderBrush = System.Windows.Media.Brushes.Transparent;
            FooterBar.BorderBrush = System.Windows.Media.Brushes.Transparent;
            HeaderBar.Background = System.Windows.Media.Brushes.Transparent;
            FooterBar.Background = System.Windows.Media.Brushes.Transparent;
            ThemePopupBorder.Background = Brush(theme.Background);
            ThemePopupBorder.BorderBrush = border;
            Resources["QuickNoteHoverBrush"] = Brush(theme.IsDark ? "#303238" : "#14000000");
            Resources["QuickNoteHoverForegroundBrush"] = text;
            FormatSeparator1.Fill = muted;
            FormatSeparator2.Fill = muted;
            FormatSeparator1.Opacity = theme.IsDark ? 0.35 : 0.45;
            FormatSeparator2.Opacity = theme.IsDark ? 0.35 : 0.45;

            _cachedTextBlocks ??= FindVisualChildren<TextBlock>(this).ToList();
            foreach (var textBlock in _cachedTextBlocks)
            {
                textBlock.Foreground = textBlock == TxtPlaceholder || textBlock == TxtSaveStatus || textBlock == TxtStats
                    ? muted
                    : text;
            }

            _cachedButtons ??= FindVisualChildren<System.Windows.Controls.Button>(this).ToList();
            foreach (var button in _cachedButtons)
            {
                button.Foreground = iconColor;
            }

            _cachedToggleButtons ??= FindVisualChildren<ToggleButton>(this).ToList();
            foreach (var toggleButton in _cachedToggleButtons)
            {
                toggleButton.Foreground = iconColor;
            }

            ApplyDocumentStyles(TxtNote.Document, codeBackground, codeText, link);
        }

        private void ApplyDocumentStyles(FlowDocument document, System.Windows.Media.Brush codeBackground, System.Windows.Media.Brush codeText, System.Windows.Media.Brush linkBrush)
        {
            foreach (Block block in document.Blocks)
            {
                if (block is Paragraph paragraph)
                {
                    ApplyInlineStyles(paragraph.Inlines, codeBackground, codeText, linkBrush);
                }
            }
        }

        private void ApplyInlineStyles(InlineCollection inlines, System.Windows.Media.Brush codeBackground, System.Windows.Media.Brush codeText, System.Windows.Media.Brush linkBrush)
        {
            foreach (Inline inline in inlines.ToList())
            {
                if (inline is Hyperlink hyperlink)
                {
                    hyperlink.Foreground = linkBrush;
                    hyperlink.TextDecorations = TextDecorations.Underline;
                    ApplyInlineStyles(hyperlink.Inlines, codeBackground, codeText, linkBrush);
                }
                else if (inline is Span span)
                {
                    if (span.Tag?.ToString() == "code")
                    {
                        span.Background = codeBackground;
                        span.Foreground = codeText;
                        span.FontFamily = new System.Windows.Media.FontFamily("Consolas");
                    }
                    ApplyInlineStyles(span.Inlines, codeBackground, codeText, linkBrush);
                }
                else if (inline is Bold bold)
                {
                    ApplyInlineStyles(bold.Inlines, codeBackground, codeText, linkBrush);
                }
                else if (inline is Italic italic)
                {
                    ApplyInlineStyles(italic.Inlines, codeBackground, codeText, linkBrush);
                }
                else if (inline is Run run && run.FontFamily?.Source == "Consolas")
                {
                    run.Background = codeBackground;
                    run.Foreground = codeText;
                }
            }
        }

        private static SolidColorBrush Brush(string color)
        {
            var brush = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(color));
            brush.Freeze();
            return brush;
        }

        private static IEnumerable<T> FindVisualChildren<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T typed)
                {
                    yield return typed;
                }

                foreach (var descendant in FindVisualChildren<T>(child))
                {
                    yield return descendant;
                }
            }
        }



        private System.Drawing.Rectangle GetWorkArea()
        {
            var screens = Forms.Screen.AllScreens;
            // Находим экран, где сейчас находится окно или используем основной
            var currentScreen = Forms.Screen.FromHandle(new System.Windows.Interop.WindowInteropHelper(this).Handle);
            return currentScreen?.WorkingArea ?? Forms.Screen.PrimaryScreen?.WorkingArea ?? GetVirtualScreenFallback();
        }

        private static System.Drawing.Rectangle GetVirtualScreenFallback()
        {
            int left = (int)SystemParameters.VirtualScreenLeft;
            int top = (int)SystemParameters.VirtualScreenTop;
            int width = (int)SystemParameters.VirtualScreenWidth;
            int height = (int)SystemParameters.VirtualScreenHeight;
            return new System.Drawing.Rectangle(left, top, width, height);
        }

        private void Window_LocationChanged(object? sender, EventArgs e)
        {
            ScheduleGeometrySave();
        }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            ScheduleGeometrySave();
        }

        private void ScheduleGeometrySave()
        {
            if (!_loaded || !IsVisible)
            {
                return;
            }

            _geometrySaveTimer.Stop();
            _geometrySaveTimer.Start();
        }

        private async Task SaveGeometryNowAsync()
        {
            _geometrySaveTimer.Stop();
            if (!_loaded || double.IsNaN(Left) || double.IsNaN(Top))
            {
                return;
            }

            double width = ActualWidth > 0 ? ActualWidth : Width;
            double height = ActualHeight > 0 ? ActualHeight : Height;
            var bounds = QuickNoteLayoutHelper.ClampBoundsToWorkArea(GetWorkArea(), Left, Top, width, height);
            _settingsService.UpdateSettings(s =>
            {
                s.QuickNoteLeft = bounds.Left;
                s.QuickNoteTop = bounds.Top;
                s.QuickNoteWidth = bounds.Width;
                s.QuickNoteHeight = bounds.Height;
            });
            await _settingsService.SaveAsync();
        }

        private bool TryOpenUrlAtMouse(MouseButtonEventArgs e)
        {
            (string Link, QuickNoteMarkdown.LinkType Type)? link = FindLinkAtMouse(e.GetPosition(TxtNote));
            if (link == null)
            {
                return false;
            }

            try
            {
                string normalized = QuickNoteMarkdown.NormalizeLinkForOpen(link.Value.Link, link.Value.Type);
                if (!IsValidLink(normalized, link.Value.Type))
                {
                    SetStatus(QuickNoteStatusKind.OpenFailed);
                    return false;
                }
                Process.Start(new ProcessStartInfo(normalized) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
                SetStatus(QuickNoteStatusKind.OpenFailed);
                return false;
            }

            return true;
        }

        private static bool IsValidLink(string link, QuickNoteMarkdown.LinkType type)
        {
            if (string.IsNullOrWhiteSpace(link))
            {
                return false;
            }

            return type switch
            {
                QuickNoteMarkdown.LinkType.Url => Uri.TryCreate(link, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps),
                QuickNoteMarkdown.LinkType.Email => link.Contains('@') && link.Length > 3,
                QuickNoteMarkdown.LinkType.Phone => link.Length > 3,
                _ => false
            };
        }

        private (string Link, QuickNoteMarkdown.LinkType Type)? FindLinkAtMouse(System.Windows.Point position)
        {
            TextPointer? pointer = TxtNote.GetPositionFromPoint(position, true);
            if (pointer == null)
            {
                return null;
            }

            if (FindHyperlink(pointer) is { } hyperlink)
            {
                string url = GetHyperlinkUrl(hyperlink);
                if (!string.IsNullOrWhiteSpace(url))
                {
                    return (url, QuickNoteMarkdown.LinkType.Url);
                }
            }

            int index = GetTextOffset(pointer);
            if (index < 0)
            {
                return null;
            }

            string text = GetEditorText();
            foreach (var (match, type) in QuickNoteMarkdown.MatchLinks(text))
            {
                if (index < match.Index || index >= match.Index + match.Length)
                {
                    continue;
                }

                return (match.Value, type);
            }

            return null;
        }

        private static Hyperlink? FindHyperlink(TextPointer pointer)
        {
            DependencyObject? current = pointer.Parent as DependencyObject;
            while (current != null)
            {
                if (current is Hyperlink hyperlink)
                {
                    return hyperlink;
                }

                current = current is FrameworkContentElement contentElement
                    ? contentElement.Parent
                    : LogicalTreeHelper.GetParent(current);
            }

            return null;
        }

        private static string GetHyperlinkUrl(Hyperlink hyperlink)
        {
            if (hyperlink.Tag is string tag && tag.StartsWith("link:", StringComparison.Ordinal))
            {
                return tag["link:".Length..];
            }

            return hyperlink.NavigateUri?.ToString() ?? string.Empty;
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

        private void UpdateConflictMenuState()
        {
            bool hasConflict = !string.IsNullOrWhiteSpace(_noteService.LastConflictCopyPath);
            if (_cachedConflictCopyMenuItem is { } menuItem)
            {
                menuItem.IsEnabled = hasConflict;
                menuItem.ToolTip = hasConflict ? _noteService.LastConflictCopyPath : null;
            }
            else
            {
                _cachedConflictCopyMenuItem = FindConflictCopyMenuItem();
                if (_cachedConflictCopyMenuItem is { } cachedItem)
                {
                    cachedItem.IsEnabled = hasConflict;
                    cachedItem.ToolTip = hasConflict ? _noteService.LastConflictCopyPath : null;
                }
            }
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
                QuickNoteStatusKind.ConflictCopySaved => LocalizationService.Format("QuickNote_ConflictCopySavedAt", argument ?? string.Empty),
                _ => string.Empty
            };
        }

        private System.Windows.Controls.MenuItem? FindConflictCopyMenuItem()
        {
            return FindVisualChildren<System.Windows.Controls.Button>(this)
                .Select(button => button.ContextMenu)
                .Where(contextMenu => contextMenu != null)
                .SelectMany(contextMenu => contextMenu!.Items.OfType<System.Windows.Controls.MenuItem>())
                .FirstOrDefault(item => string.Equals(item.Name, "MenuOpenConflictCopy", StringComparison.Ordinal));
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
            UpdateConflictMenuState();
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
        ConflictCopySaved
    }
}
