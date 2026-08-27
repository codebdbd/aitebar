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
    public partial class QuickNoteWindow
    {
        private void BtnBold_Click(object sender, RoutedEventArgs e) => ToggleFormatting(TextElement.FontWeightProperty, FontWeights.Bold, FontWeights.Normal);

        private void BtnItalic_Click(object sender, RoutedEventArgs e) => ToggleFormatting(TextElement.FontStyleProperty, FontStyles.Italic, FontStyles.Normal);

        private void BtnUnderline_Click(object sender, RoutedEventArgs e) => ToggleTextDecoration(TextDecorationLocation.Underline);

        private void BtnStrikethrough_Click(object sender, RoutedEventArgs e) => ToggleTextDecoration(TextDecorationLocation.Strikethrough);

        private void BtnCode_Click(object sender, RoutedEventArgs e)
        {
            string text = TxtNote.Selection.Text;
            Section codeBlock = QuickNoteDocumentFormatting.CreateCodeBlockElement(text, _theme);

            TxtNote.BeginChange();
            try
            {
                var range = TxtNote.Selection;
                if (!range.IsEmpty)
                {
                    range.Text = string.Empty;
                }

                TextPointer insertionPointer = range.Start.GetInsertionPosition(LogicalDirection.Forward);
                Paragraph? parentParagraph = insertionPointer.Paragraph;
                if (parentParagraph != null)
                {
                    BlockCollection siblingBlocks = parentParagraph.SiblingBlocks;
                    siblingBlocks.InsertAfter(parentParagraph, codeBlock);
                    siblingBlocks.InsertAfter(codeBlock, new Paragraph(new Run(string.Empty)));

                    if (new TextRange(parentParagraph.ContentStart, parentParagraph.ContentEnd).Text.Trim().Length == 0)
                    {
                        siblingBlocks.Remove(parentParagraph);
                    }
                }
                else
                {
                    TxtNote.Document.Blocks.Add(codeBlock);
                    TxtNote.Document.Blocks.Add(new Paragraph(new Run(string.Empty)));
                }
            }
            finally
            {
                TxtNote.EndChange();
            }

            MarkChangedAndScheduleSave();
            ScheduleFooterStatsUpdate();
            Dispatcher.BeginInvoke(() =>
            {
                TxtNote.Focus();
                if (codeBlock.Blocks.LastBlock is Paragraph codeParagraph)
                {
                    TxtNote.CaretPosition = codeParagraph.ContentEnd;
                }
            }, DispatcherPriority.Input);
        }

        private void BtnBullet_Click(object sender, RoutedEventArgs e) => ApplyListFormatting(numbered: false);

        private void BtnNumbered_Click(object sender, RoutedEventArgs e) => ApplyListFormatting(numbered: true);

        private void BtnTaskList_Click(object sender, RoutedEventArgs e) => ToggleTaskList();

        private void ToggleTaskList()
        {
            _saveSuppressionCount++;
            try
            {
                TxtNote.BeginChange();
                try
                {
                    List<Paragraph> paragraphs = GetSelectedParagraphs();
                    if (paragraphs.Count == 0 && TxtNote.CaretPosition?.Paragraph is Paragraph single)
                    {
                        paragraphs.Add(single);
                    }
                    if (paragraphs.Count == 0 && TxtNote.Document.Blocks.FirstBlock is Paragraph firstP)
                    {
                        paragraphs.Add(firstP);
                    }

                    bool allAreTasks = paragraphs.Count > 0 && paragraphs.All(static p => QuickNoteDocumentFormatting.IsTaskParagraph(p, out _, out _, out _));

                    foreach (Paragraph p in paragraphs)
                    {
                        if (allAreTasks)
                        {
                            QuickNoteDocumentFormatting.RemoveTaskCheckbox(p, _theme);
                        }
                        else
                        {
                            if (!QuickNoteDocumentFormatting.IsTaskParagraph(p, out _, out _, out _))
                            {
                                var container = QuickNoteDocumentFormatting.CreateTaskCheckbox(false, isChecked => OnTaskItemToggled(p, isChecked), _theme);
                                if (p.Inlines.FirstInline != null)
                                {
                                    p.Inlines.InsertBefore(p.Inlines.FirstInline, container);
                                }
                                else
                                {
                                    p.Inlines.Add(container);
                                    p.Inlines.Add(new Run(string.Empty));
                                }
                                QuickNoteDocumentFormatting.ApplyTaskFormattingToParagraph(p, false, _theme);
                            }
                        }
                    }
                }
                finally
                {
                    TxtNote.EndChange();
                }
            }
            finally
            {
                _saveSuppressionCount--;
            }

            MarkChangedAndScheduleSave();
            ScheduleFooterStatsUpdate();
            TxtNote.Focus();
        }

        internal void OnTaskItemToggled(Paragraph paragraph, bool isChecked)
        {
            _saveSuppressionCount++;
            try
            {
                TxtNote.BeginChange();
                try
                {
                    QuickNoteDocumentFormatting.ApplyTaskFormattingToParagraph(paragraph, isChecked, _theme);
                }
                finally
                {
                    TxtNote.EndChange();
                }
            }
            finally
            {
                _saveSuppressionCount--;
            }

            MarkChangedAndScheduleSave();
            ScheduleFooterStatsUpdate();
        }

        internal void ConnectTaskItemEvents(FlowDocument document)
        {
            foreach (Block block in document.Blocks)
            {
                if (block is Paragraph paragraph)
                {
                    ConnectTaskItemInParagraph(paragraph);
                }
                else if (block is Section section)
                {
                    foreach (Block child in section.Blocks)
                    {
                        if (child is Paragraph childParagraph)
                        {
                            ConnectTaskItemInParagraph(childParagraph);
                        }
                    }
                }
            }
        }

        private void ConnectTaskItemInParagraph(Paragraph paragraph)
        {
            if (QuickNoteDocumentFormatting.IsTaskParagraph(paragraph, out bool isChecked, out InlineUIContainer? container, out CheckBox? checkBox))
            {
                if (checkBox != null)
                {
                    checkBox.Click -= OnTaskCheckBoxClicked;
                    checkBox.Click += OnTaskCheckBoxClicked;
                    checkBox.Tag = paragraph;
                }
            }
        }

        private void OnTaskCheckBoxClicked(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox cb && cb.Tag is Paragraph paragraph)
            {
                bool isChecked = cb.IsChecked == true;
                OnTaskItemToggled(paragraph, isChecked);
            }
            else if (sender is CheckBox cb2)
            {
                DependencyObject? current = cb2;
                while (current != null && current is not Paragraph)
                {
                    current = LogicalTreeHelper.GetParent(current);
                }
                if (current is Paragraph p)
                {
                    OnTaskItemToggled(p, cb2.IsChecked == true);
                }
            }
        }

        private List<Paragraph> GetSelectedParagraphs()
        {
            var list = new List<Paragraph>();
            TextPointer start = TxtNote.Selection.Start;
            TextPointer end = TxtNote.Selection.End;

            Paragraph? startP = start.Paragraph;
            Paragraph? endP = end.Paragraph;

            if (startP == null || endP == null)
            {
                return list;
            }

            if (startP == endP)
            {
                list.Add(startP);
                return list;
            }

            TextPointer current = start;
            while (current != null && current.CompareTo(end) <= 0)
            {
                Paragraph? p = current.Paragraph;
                if (p != null && !list.Contains(p))
                {
                    list.Add(p);
                }

                TextPointer next = current.GetNextContextPosition(LogicalDirection.Forward);
                if (next == null || next.CompareTo(current) <= 0)
                {
                    break;
                }
                current = next;
            }

            return list;
        }

        private bool HandleTaskItemEnterKey()
        {
            Paragraph? currentParagraph = TxtNote.CaretPosition?.Paragraph;
            if (currentParagraph == null || !QuickNoteDocumentFormatting.IsTaskParagraph(currentParagraph, out _, out _, out _))
            {
                return false;
            }

            string itemText = new TextRange(currentParagraph.ContentStart, currentParagraph.ContentEnd).Text.Trim();
            if (string.IsNullOrEmpty(itemText))
            {
                _saveSuppressionCount++;
                try
                {
                    TxtNote.BeginChange();
                    try
                    {
                        QuickNoteDocumentFormatting.RemoveTaskCheckbox(currentParagraph, _theme);
                    }
                    finally
                    {
                        TxtNote.EndChange();
                    }
                }
                finally
                {
                    _saveSuppressionCount--;
                }

                MarkChangedAndScheduleSave();
                ScheduleFooterStatsUpdate();
                return true;
            }

            _saveSuppressionCount++;
            try
            {
                TxtNote.BeginChange();
                try
                {
                    if (!TxtNote.Selection.IsEmpty)
                    {
                        TxtNote.Selection.Text = string.Empty;
                    }

                    TextPointer? caret = TxtNote.CaretPosition;
                    if (caret == null)
                    {
                        return false;
                    }
                    TextRange tailRange = new TextRange(caret, currentParagraph.ContentEnd);
                    string tailText = tailRange.Text.TrimEnd('\r', '\n');
                    tailRange.Text = string.Empty;

                    var newParagraph = new Paragraph();
                    var newContainer = QuickNoteDocumentFormatting.CreateTaskCheckbox(false, isChecked => OnTaskItemToggled(newParagraph, isChecked), _theme);
                    newParagraph.Inlines.Add(newContainer);
                    if (!string.IsNullOrEmpty(tailText))
                    {
                        newParagraph.Inlines.Add(new Run(tailText)
                        {
                            Foreground = QuickNoteBrush.FromHex(_theme?.Text ?? "#F6F0E6")
                        });
                    }
                    else
                    {
                        newParagraph.Inlines.Add(new Run(string.Empty));
                    }

                    currentParagraph.SiblingBlocks.InsertAfter(currentParagraph, newParagraph);
                    TxtNote.CaretPosition = newParagraph.ContentEnd;
                }
                finally
                {
                    TxtNote.EndChange();
                }
            }
            finally
            {
                _saveSuppressionCount--;
            }

            MarkChangedAndScheduleSave();
            ScheduleFooterStatsUpdate();
            return true;
        }

        private bool HandleTaskItemBackspaceKey()
        {
            if (!TxtNote.Selection.IsEmpty)
            {
                return false;
            }

            Paragraph? currentParagraph = TxtNote.CaretPosition?.Paragraph;
            if (currentParagraph == null || !QuickNoteDocumentFormatting.IsTaskParagraph(currentParagraph, out _, out _, out _))
            {
                return false;
            }

            TextPointer? caret = TxtNote.CaretPosition;
            if (caret == null)
            {
                return false;
            }
            TextRange headRange = new TextRange(currentParagraph.ContentStart, caret);
            if (string.IsNullOrWhiteSpace(headRange.Text))
            {
                _saveSuppressionCount++;
                try
                {
                    TxtNote.BeginChange();
                    try
                    {
                        QuickNoteDocumentFormatting.RemoveTaskCheckbox(currentParagraph, _theme);
                    }
                    finally
                    {
                        TxtNote.EndChange();
                    }
                }
                finally
                {
                    _saveSuppressionCount--;
                }

                MarkChangedAndScheduleSave();
                ScheduleFooterStatsUpdate();
                return true;
            }

            return false;
        }

        private bool TryAutoConvertMarkdownOnSpace()
        {
            if (_saveSuppressionCount > 0 || !_loaded || !TxtNote.Selection.IsEmpty)
            {
                return false;
            }

            Paragraph? paragraph = TxtNote.CaretPosition?.Paragraph;
            if (paragraph == null || QuickNoteDocumentFormatting.IsTaskParagraph(paragraph, out _, out _, out _))
            {
                return false;
            }

            TextPointer? caret = TxtNote.CaretPosition;
            if (caret == null)
            {
                return false;
            }

            TextRange rangeBeforeCaret = new TextRange(paragraph.ContentStart, caret);
            string textBeforeCaret = rangeBeforeCaret.Text.Trim();

            if (textBeforeCaret is "[ ]" or "[x]" or "[X]")
            {
                bool isChecked = textBeforeCaret is "[x]" or "[X]";
                _saveSuppressionCount++;
                try
                {
                    TxtNote.BeginChange();
                    try
                    {
                        rangeBeforeCaret.Text = string.Empty;
                        var container = QuickNoteDocumentFormatting.CreateTaskCheckbox(isChecked, state => OnTaskItemToggled(paragraph, state), _theme);
                        if (paragraph.Inlines.FirstInline != null)
                        {
                            paragraph.Inlines.InsertBefore(paragraph.Inlines.FirstInline, container);
                        }
                        else
                        {
                            paragraph.Inlines.Add(container);
                            paragraph.Inlines.Add(new Run(string.Empty));
                        }
                        QuickNoteDocumentFormatting.ApplyTaskFormattingToParagraph(paragraph, isChecked, _theme);
                        TxtNote.CaretPosition = paragraph.ContentEnd;
                    }
                    finally
                    {
                        TxtNote.EndChange();
                    }
                }
                finally
                {
                    _saveSuppressionCount--;
                }

                MarkChangedAndScheduleSave();
                ScheduleFooterStatsUpdate();
                return true;
            }

            if (textBeforeCaret is "-" or "*")
            {
                _saveSuppressionCount++;
                try
                {
                    TxtNote.BeginChange();
                    try
                    {
                        rangeBeforeCaret.Text = string.Empty;
                        ApplyListFormatting(numbered: false);
                    }
                    finally
                    {
                        TxtNote.EndChange();
                    }
                }
                finally
                {
                    _saveSuppressionCount--;
                }

                MarkChangedAndScheduleSave();
                ScheduleFooterStatsUpdate();
                return true;
            }

            if (textBeforeCaret is "1.")
            {
                _saveSuppressionCount++;
                try
                {
                    TxtNote.BeginChange();
                    try
                    {
                        rangeBeforeCaret.Text = string.Empty;
                        ApplyListFormatting(numbered: true);
                    }
                    finally
                    {
                        TxtNote.EndChange();
                    }
                }
                finally
                {
                    _saveSuppressionCount--;
                }

                MarkChangedAndScheduleSave();
                ScheduleFooterStatsUpdate();
                return true;
            }

            if (textBeforeCaret is "#" or "##" or "###")
            {
                int level = textBeforeCaret.Length;
                _saveSuppressionCount++;
                try
                {
                    TxtNote.BeginChange();
                    try
                    {
                        rangeBeforeCaret.Text = string.Empty;
                        paragraph.FontSize = QuickNoteDocumentFormatting.GetHeadingFontSizeForLevel(level);
                        paragraph.FontWeight = FontWeights.SemiBold;
                    }
                    finally
                    {
                        TxtNote.EndChange();
                    }
                }
                finally
                {
                    _saveSuppressionCount--;
                }

                MarkChangedAndScheduleSave();
                ScheduleFooterStatsUpdate();
                return true;
            }

            return false;
        }

        private bool TryAutoConvertMarkdownOnEnter()
        {
            if (_saveSuppressionCount > 0 || !_loaded || !TxtNote.Selection.IsEmpty)
            {
                return false;
            }

            Paragraph? paragraph = TxtNote.CaretPosition?.Paragraph;
            if (paragraph == null)
            {
                return false;
            }

            TextRange range = new TextRange(paragraph.ContentStart, paragraph.ContentEnd);
            string lineText = range.Text.Trim();

            if (lineText is "---" or "***")
            {
                _saveSuppressionCount++;
                try
                {
                    TxtNote.BeginChange();
                    try
                    {
                        range.Text = string.Empty;
                        BtnDivider_Click(this, new RoutedEventArgs());
                    }
                    finally
                    {
                        TxtNote.EndChange();
                    }
                }
                finally
                {
                    _saveSuppressionCount--;
                }
                return true;
            }

            if (lineText is "```")
            {
                _saveSuppressionCount++;
                try
                {
                    TxtNote.BeginChange();
                    try
                    {
                        range.Text = string.Empty;
                        BtnCode_Click(this, new RoutedEventArgs());
                    }
                    finally
                    {
                        TxtNote.EndChange();
                    }
                }
                finally
                {
                    _saveSuppressionCount--;
                }
                return true;
            }

            return false;
        }

        private void TryAutoConvertTaskPrefix()
        {
            if (_saveSuppressionCount > 0 || !_loaded)
            {
                return;
            }

            Paragraph? paragraph = TxtNote.CaretPosition?.Paragraph;
            if (paragraph == null || QuickNoteDocumentFormatting.IsTaskParagraph(paragraph, out _, out _, out _))
            {
                return;
            }

            if (paragraph.Inlines.FirstInline is Run run)
            {
                string text = run.Text;
                if (text.StartsWith("[ ] ", StringComparison.Ordinal) ||
                    text.StartsWith("[x] ", StringComparison.OrdinalIgnoreCase) ||
                    text.StartsWith("[X] ", StringComparison.Ordinal))
                {
                    bool isChecked = text.StartsWith("[x] ", StringComparison.OrdinalIgnoreCase) ||
                                     text.StartsWith("[X] ", StringComparison.Ordinal);
                    string remainder = text[4..];

                    _saveSuppressionCount++;
                    try
                    {
                        TxtNote.BeginChange();
                        try
                        {
                            run.Text = remainder;
                            var container = QuickNoteDocumentFormatting.CreateTaskCheckbox(isChecked, state => OnTaskItemToggled(paragraph, state), _theme);
                            paragraph.Inlines.InsertBefore(run, container);
                            QuickNoteDocumentFormatting.ApplyTaskFormattingToParagraph(paragraph, isChecked, _theme);
                            TxtNote.CaretPosition = run.ContentStart;
                        }
                        finally
                        {
                            TxtNote.EndChange();
                        }
                    }
                    finally
                    {
                        _saveSuppressionCount--;
                    }

                    MarkChangedAndScheduleSave();
                    ScheduleFooterStatsUpdate();
                }
            }
        }

        private void BtnQuote_Click(object sender, RoutedEventArgs e)
        {
            string text = TxtNote.Selection.Text;
            Section quoteBlock = QuickNoteDocumentFormatting.CreateQuoteBlockElement(text, _theme);
            InsertBlockElement(quoteBlock);
        }

        private void BtnDivider_Click(object sender, RoutedEventArgs e)
        {
            Section divider = QuickNoteDocumentFormatting.CreateDividerElement(_theme);
            InsertBlockElement(divider);
        }

        private void InsertBlockElement(Section block)
        {
            TxtNote.BeginChange();
            try
            {
                var range = TxtNote.Selection;
                if (!range.IsEmpty)
                {
                    range.Text = string.Empty;
                }

                TextPointer insertionPointer = range.Start.GetInsertionPosition(LogicalDirection.Forward);
                Paragraph? parentParagraph = insertionPointer.Paragraph;
                if (parentParagraph != null)
                {
                    BlockCollection siblingBlocks = parentParagraph.SiblingBlocks;
                    siblingBlocks.InsertAfter(parentParagraph, block);
                    siblingBlocks.InsertAfter(block, new Paragraph(new Run(string.Empty)));

                    if (new TextRange(parentParagraph.ContentStart, parentParagraph.ContentEnd).Text.Trim().Length == 0)
                    {
                        siblingBlocks.Remove(parentParagraph);
                    }
                }
                else
                {
                    TxtNote.Document.Blocks.Add(block);
                    TxtNote.Document.Blocks.Add(new Paragraph(new Run(string.Empty)));
                }
            }
            finally
            {
                TxtNote.EndChange();
            }

            MarkChangedAndScheduleSave();
            ScheduleFooterStatsUpdate();
            Dispatcher.BeginInvoke(() =>
            {
                TxtNote.Focus();
                if (block.Blocks.LastBlock is Paragraph lastParagraph)
                {
                    TxtNote.CaretPosition = lastParagraph.ContentEnd;
                }
            }, DispatcherPriority.Input);
        }

        private void BtnDecreaseFontSize_Click(object sender, RoutedEventArgs e) => ChangeSelectionFontSize(-2);

        private void BtnIncreaseFontSize_Click(object sender, RoutedEventArgs e) => ChangeSelectionFontSize(2);

        private void BtnInsertLink_Click(object sender, RoutedEventArgs e) => InsertLinkFromDialog();

        private void BtnInsertImage_Click(object sender, RoutedEventArgs e)
        {
            var insertionSelection = new TextRange(TxtNote.Selection.Start, TxtNote.Selection.End);
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Image files|*.png;*.jpg;*.jpeg;*.bmp;*.gif|All files|*.*",
                CheckFileExists = true
            };

            _isModalDialogOpen = true;
            bool? accepted;
            try
            {
                accepted = dialog.ShowDialog(this);
            }
            finally
            {
                _isModalDialogOpen = false;
            }

            if (accepted == true)
            {
                RestoreFormatSelection(insertionSelection);
                TryInsertImageFile(dialog.FileName);
            }
        }

        private void FormatButton_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            _preservedFormatSelection = new TextRange(TxtNote.Selection.Start, TxtNote.Selection.End);
        }

        private void RestoreFormatSelection(TextRange? selection)
        {
            if (selection == null)
            {
                return;
            }

            try
            {
                TxtNote.Selection.Select(selection.Start, selection.End);
            }
            catch (InvalidOperationException)
            {
                _preservedFormatSelection = null;
            }
        }

        private void BtnClearFormatting_Click(object sender, RoutedEventArgs e)
        {
            RestoreFormatSelection(_preservedFormatSelection);
            ClearSelectedFormatting();
            _preservedFormatSelection = null;
        }

        private void BtnClearFormattingInlineOnly_Click(object sender, RoutedEventArgs e)
        {
            RestoreFormatSelection(_preservedFormatSelection);
            ClearSelectedFormatting(ClearFormattingScope.InlineOnly);
            _preservedFormatSelection = null;
        }

        private void BtnClearFormattingPreserveLinks_Click(object sender, RoutedEventArgs e)
        {
            RestoreFormatSelection(_preservedFormatSelection);
            ClearSelectedFormatting(ClearFormattingScope.PreserveLinks);
            _preservedFormatSelection = null;
        }

        private void BtnClearFormattingAll_Click(object sender, RoutedEventArgs e)
        {
            RestoreFormatSelection(_preservedFormatSelection);
            ClearSelectedFormatting(ClearFormattingScope.All);
            _preservedFormatSelection = null;
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
                if (source is System.Windows.Controls.Button or System.Windows.Controls.MenuItem)
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

        private void ChangeSelectionFontSize(double delta)
        {
            RestoreFormatSelection(_preservedFormatSelection);
            object value = TxtNote.Selection.GetPropertyValue(TextElement.FontSizeProperty);
            double current = value is double size ? size : QuickNoteDocumentFormatting.GetHeadingFontSizeForLevel(0);
            TxtNote.Selection.ApplyPropertyValue(TextElement.FontSizeProperty, Math.Clamp(current + delta, 10, 36));
            _preservedFormatSelection = null;
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

        private void ApplyListFormatting(bool numbered)
        {
            RoutedCommand command = numbered
                ? EditingCommands.ToggleNumbering
                : EditingCommands.ToggleBullets;

            _saveSuppressionCount++;
            try
            {
                TxtNote.BeginChange();
                try
                {
                    command.Execute(null, TxtNote);
                    QuickNoteDocumentFormatting.NormalizeListLayout(TxtNote.Document);
                }
                finally
                {
                    TxtNote.EndChange();
                }
            }
            finally
            {
                _saveSuppressionCount--;
            }

            MarkChangedAndScheduleSave();
            ScheduleFooterStatsUpdate();
            TxtNote.Focus();
        }

        private void ApplyHeadingToSelectedLines(int headingLevel, int selectionStart, int selectionEnd)
        {
            _saveSuppressionCount++;
            try
            {
                ApplyHeadingFormattingToLineRange(selectionStart, selectionEnd, headingLevel);
            }
            finally
            {
                _saveSuppressionCount--;
            }
            SelectEditorRange(selectionStart, selectionEnd);
            MarkChangedAndScheduleSave();
            ScheduleFooterStatsUpdate();
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
            range.ApplyPropertyValue(TextElement.FontFamilyProperty, QuickNoteFonts.Default);
            range.ApplyPropertyValue(TextElement.FontSizeProperty, QuickNoteDocumentFormatting.GetHeadingFontSizeForLevel(headingLevel));
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
            ScheduleFooterStatsUpdate();
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

                InsertHyperlinkAtPointer(insertionPointer, QuickNoteDocumentFormatting.CreateHyperlink(linkText, url));
            }
            finally
            {
                TxtNote.EndChange();
            }

            SetCaretOffset(insertionOffset + linkText.Length);
        }

        private bool TryInsertImageFile(string path)
        {
            try
            {
                var file = new System.IO.FileInfo(path);
                if (!file.Exists || file.Length <= 0 || file.Length > QuickNoteImageHelper.MaxEncodedBytes ||
                    !QuickNoteImageHelper.TryCreateInlineImage(System.IO.File.ReadAllBytes(path), out InlineUIContainer? image))
                {
                    SetStatus(QuickNoteStatusKind.ImageInsertFailed);
                    return false;
                }

                return InsertImage(image);
            }
            catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException or ArgumentException)
            {
                Logger.Log(ex);
                SetStatus(QuickNoteStatusKind.ImageInsertFailed);
                return false;
            }
        }

        private bool InsertImage(BitmapSource image) =>
            QuickNoteImageHelper.TryCreateInlineImage(image, out InlineUIContainer? container) && InsertImage(container);

        private bool InsertImage(InlineUIContainer? container)
        {
            if (container == null || !QuickNoteImageHelper.CanAddToDocument(TxtNote.Document, container))
            {
                SetStatus(QuickNoteStatusKind.ImageInsertFailed);
                return false;
            }

            try
            {
                TxtNote.BeginChange();
                try
                {
                    TxtNote.Selection.Text = string.Empty;
                    InsertInlineAtCaret(container);
                    TextPointer caret = container.ElementEnd.GetNextInsertionPosition(LogicalDirection.Forward)
                        ?? container.ElementEnd.GetNextContextPosition(LogicalDirection.Forward)
                        ?? container.ElementEnd;
                    TxtNote.Selection.Select(caret, caret);
                }
                finally
                {
                    TxtNote.EndChange();
                }
                MarkChangedAndScheduleSave();
                TxtNote.Focus();
                return true;
            }
            catch (Exception ex)
            {
                Logger.Log("InsertImage crashed: " + ex);
                SetStatus(QuickNoteStatusKind.ImageInsertFailed);
                return false;
            }
        }

        private void InsertInlineAtCaret(Inline inline)
        {
            TextPointer pointer = TxtNote.Selection.Start;
            if (pointer.Parent is Run run && GetInlineSiblings(run) is { } siblings)
            {
                int offset = new TextRange(run.ContentStart, pointer).Text.Length;
                string after = run.Text[offset..];
                run.Text = run.Text[..offset];
                siblings.InsertAfter(run, inline);
                if (!string.IsNullOrEmpty(after))
                {
                    siblings.InsertAfter(inline, CloneRunWithText(run, after));
                }
                return;
            }

            if (pointer.Paragraph is { } paragraph)
            {
                InsertInlineInCollection(paragraph.Inlines, pointer, inline);
                return;
            }

            var fallbackParagraph = new Paragraph(inline)
            {
                Margin = new Thickness(0),
                FontFamily = QuickNoteFonts.Default,
                FontSize = QuickNoteDocumentFormatting.GetHeadingFontSizeForLevel(0),
                Foreground = Brush(_theme.Text)
            };
            TxtNote.Document.Blocks.Add(fallbackParagraph);
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
            Inline anchor;
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
                FontFamily = QuickNoteFonts.Default,
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
                    FontFamily = QuickNoteFonts.Default,
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

        internal void ClearSelectedFormatting(ClearFormattingScope scope = ClearFormattingScope.All)
        {
            if (TryConvertSelectedCodeBlocksToPlainText())
            {
                MarkChangedAndScheduleSave();
                ScheduleFooterStatsUpdate();
                TxtNote.Focus();
                return;
            }

            var (selectionStart, selectionEnd) = GetSelectionOffsets();
            string selectedTextToRestore = QuickNoteDocumentHelper.NormalizeLineEndings(
                QuickNoteDocumentHelper.RemoveVisualListMarkers(TxtNote.Selection.Text)).TrimEnd('\n');

            _saveSuppressionCount++;
            try
            {
                TxtNote.BeginChange();
                try
                {
                    var markerEdit = ClearSelectedTextMarkers(selectionStart, selectionEnd);
                    selectionStart = markerEdit.Start;
                    selectionEnd = markerEdit.End;
                    if (markerEdit.Changed)
                    {
                        SelectEditorRange(selectionStart, selectionEnd);
                    }

                    if (scope != ClearFormattingScope.InlineOnly)
                    {
                        string textBeforeListUnwrap = GetEditorText();
                        RemoveSelectedListFormatting(TxtNote.Selection);
                        string textAfterListUnwrap = GetEditorText();
                        (selectionStart, selectionEnd) = QuickNoteDocumentHelper.RemapSelection(
                            textBeforeListUnwrap,
                            textAfterListUnwrap,
                            selectionStart,
                            selectionEnd,
                            selectedTextToRestore);
                        SelectEditorRangeByText(selectionStart, selectionEnd, selectedTextToRestore);
                    }

                    if (scope != ClearFormattingScope.PreserveLinks)
                    {
                        UnwrapHyperlinksInSelection();
                        SelectEditorRangeByText(selectionStart, selectionEnd, selectedTextToRestore);
                    }

                    ResetSelectionFormatting();
                }
                finally
                {
                    TxtNote.EndChange();
                }
            }
            finally
            {
                _saveSuppressionCount--;
            }

            SelectEditorRangeByText(selectionStart, selectionEnd, selectedTextToRestore);
            ApplyDocumentStyles(
                TxtNote.Document,
                Brush(_theme.Text),
                Brush(QuickNoteDocumentFormatting.GetCodeBackground(_theme)),
                Brush(QuickNoteDocumentFormatting.GetCodeText(_theme)),
                Brush(_theme.Link));
            MarkChangedAndScheduleSave();
            ScheduleFooterStatsUpdate();
            TxtNote.Focus();
        }

        private bool TryConvertSelectedCodeBlocksToPlainText()
        {
            TextPointer selectionStart = TxtNote.Selection.Start;
            TextPointer selectionEnd = TxtNote.Selection.End;
            List<Section> selectedCodeBlocks = TxtNote.Document.Blocks
                .OfType<Section>()
                .Where(QuickNoteDocumentFormatting.IsCodeBlock)
                .Where(section => TextRangeIntersectsOrContainsCaret(selectionStart, selectionEnd, section.ContentStart, section.ContentEnd))
                .ToList();

            if (selectedCodeBlocks.Count == 0)
            {
                return false;
            }

            TextPointer? newSelectionStart = null;
            TextPointer? newSelectionEnd = null;

            TxtNote.BeginChange();
            try
            {
                foreach (Section section in selectedCodeBlocks)
                {
                    BlockCollection siblings = section.SiblingBlocks;
                    string[] lines = QuickNoteDocumentHelper.NormalizeLineEndings(
                            QuickNoteDocumentFormatting.GetCodeBlockText(section))
                        .Split('\n');
                    var inserted = new List<Paragraph>();

                    foreach (string line in lines)
                    {
                        var paragraph = new Paragraph(new Run(line))
                        {
                            Margin = new Thickness(0),
                            FontFamily = QuickNoteFonts.Default,
                            FontSize = QuickNoteDocumentFormatting.GetHeadingFontSizeForLevel(0),
                            Foreground = Brush(_theme.Text)
                        };
                        siblings.InsertBefore(section, paragraph);
                        inserted.Add(paragraph);
                    }

                    siblings.Remove(section);

                    if (inserted.Count > 0)
                    {
                        newSelectionStart ??= inserted[0].ContentStart;
                        newSelectionEnd = inserted[^1].ContentEnd;
                    }
                }
            }
            finally
            {
                TxtNote.EndChange();
            }

            if (newSelectionStart != null && newSelectionEnd != null)
            {
                TxtNote.Selection.Select(newSelectionStart, newSelectionEnd);
            }

            return true;
        }

        private static bool TextRangeIntersectsOrContainsCaret(TextPointer selectionStart, TextPointer selectionEnd, TextPointer rangeStart, TextPointer rangeEnd)
        {
            if (selectionStart.CompareTo(selectionEnd) == 0)
            {
                return selectionStart.CompareTo(rangeStart) >= 0 && selectionStart.CompareTo(rangeEnd) <= 0;
            }

            return TextRangesIntersect(selectionStart, selectionEnd, rangeStart, rangeEnd);
        }

        private void UnwrapHyperlinksInSelection()
        {
            TextPointer start = TxtNote.Selection.Start;
            TextPointer end = TxtNote.Selection.End;
            var hyperlinks = GetAllHyperlinks(TxtNote.Document.Blocks)
                .Where(hyperlink => TextRangesIntersect(start, end, hyperlink.ContentStart, hyperlink.ContentEnd))
                .Where(hyperlink => !string.Equals(
                    QuickNoteDocumentFormatting.GetHyperlinkUrl(hyperlink),
                    QuickNoteDocumentFormatting.CodeCopyLink,
                    StringComparison.OrdinalIgnoreCase))
                .Select(hyperlink =>
                {
                    string text = QuickNoteDocumentHelper.NormalizeLineEndings(
                        new TextRange(hyperlink.ContentStart, hyperlink.ContentEnd).Text);
                    int selectionStart = start.CompareTo(hyperlink.ContentStart) <= 0
                        ? 0
                        : QuickNoteDocumentHelper.NormalizeLineEndings(
                            new TextRange(hyperlink.ContentStart, start).Text).Length;
                    int selectionEnd = end.CompareTo(hyperlink.ContentEnd) >= 0
                        ? text.Length
                        : QuickNoteDocumentHelper.NormalizeLineEndings(
                            new TextRange(hyperlink.ContentStart, end).Text).Length;
                    return (
                        Hyperlink: hyperlink,
                        Text: text,
                        Start: Math.Clamp(selectionStart, 0, text.Length),
                        End: Math.Clamp(selectionEnd, 0, text.Length));
                })
                .ToList();

            TxtNote.BeginChange();
            try
            {
                foreach (var item in hyperlinks.AsEnumerable().Reverse())
                {
                    Hyperlink hyperlink = item.Hyperlink;
                    InlineCollection? parentInlines = GetInlineSiblings(hyperlink);
                    if (parentInlines == null)
                    {
                        continue;
                    }

                    if (item.Start > 0 || item.End < item.Text.Length)
                    {
                        ReplaceHyperlinkWithFragments(parentInlines, hyperlink, item.Text, item.Start, item.End);
                        continue;
                    }

                    foreach (Inline child in hyperlink.Inlines.ToList())
                    {
                        hyperlink.Inlines.Remove(child);
                        parentInlines.InsertBefore(hyperlink, child);
                    }

                    parentInlines.Remove(hyperlink);
                }
            }
            finally
            {
                TxtNote.EndChange();
            }
        }

        private static void ReplaceHyperlinkWithFragments(
            InlineCollection parentInlines,
            Hyperlink source,
            string text,
            int selectionStart,
            int selectionEnd)
        {
            if (selectionStart > 0)
            {
                parentInlines.InsertBefore(source, CreateHyperlinkFragment(
                    source,
                    CloneInlineRange(source.Inlines, 0, selectionStart)));
            }

            if (selectionEnd > selectionStart)
            {
                foreach (Inline inline in CloneInlineRange(source.Inlines, selectionStart, selectionEnd))
                {
                    parentInlines.InsertBefore(source, inline);
                }
            }

            if (selectionEnd < text.Length)
            {
                parentInlines.InsertBefore(source, CreateHyperlinkFragment(
                    source,
                    CloneInlineRange(source.Inlines, selectionEnd, text.Length)));
            }

            parentInlines.Remove(source);
        }

        private static Hyperlink CreateHyperlinkFragment(Hyperlink source, IEnumerable<Inline> inlines)
        {
            var fragment = new Hyperlink
            {
                NavigateUri = source.NavigateUri,
                Tag = source.Tag,
                Foreground = source.Foreground,
                TextDecorations = source.TextDecorations?.Clone()
            };

            foreach (Inline inline in inlines)
            {
                fragment.Inlines.Add(inline);
            }

            return fragment;
        }

        private static IReadOnlyList<Inline> CloneInlineRange(InlineCollection inlines, int start, int end)
        {
            var result = new List<Inline>();
            int offset = 0;
            foreach (Inline inline in inlines)
            {
                int length = GetInlineTextLength(inline);
                int localStart = Math.Clamp(start - offset, 0, length);
                int localEnd = Math.Clamp(end - offset, 0, length);
                if (localEnd > localStart && CloneInlineRange(inline, localStart, localEnd) is { } clone)
                {
                    result.Add(clone);
                }

                offset += length;
                if (offset >= end)
                {
                    break;
                }
            }

            return result;
        }

        private static Inline? CloneInlineRange(Inline inline, int start, int end)
        {
            if (inline is Run run)
            {
                return CloneRunWithText(run, run.Text[start..end]);
            }

            if (inline is LineBreak)
            {
                return start == 0 && end > 0 ? new LineBreak() : null;
            }

            if (inline is InlineUIContainer container)
            {
                if (start == 0 && end > 0 && QuickNoteImageHelper.TryGetPngPayload(container, out byte[]? png) && png != null &&
                    QuickNoteImageHelper.TryCreateInlineImage(png, out InlineUIContainer? clone))
                {
                    return clone;
                }

                return null;
            }

            if (inline is Span span)
            {
                Span clone = CloneSpanShell(span);
                foreach (Inline child in CloneInlineRange(span.Inlines, start, end))
                {
                    clone.Inlines.Add(child);
                }

                return clone.Inlines.Count > 0 ? clone : null;
            }

            return null;
        }

        private static Span CloneSpanShell(Span source)
        {
            Span clone = source switch
            {
                Bold => new Bold(),
                Italic => new Italic(),
                _ => new Span()
            };
            clone.Tag = source.Tag;
            clone.FontFamily = source.FontFamily;
            clone.FontSize = source.FontSize;
            clone.FontStretch = source.FontStretch;
            clone.FontStyle = source.FontStyle;
            clone.FontWeight = source.FontWeight;
            clone.Foreground = source.Foreground;
            clone.Background = source.Background;
            clone.TextDecorations = source.TextDecorations?.Clone();
            return clone;
        }

        private static int GetInlineTextLength(Inline inline)
        {
            if (inline is Run run)
            {
                return QuickNoteDocumentHelper.NormalizeLineEndings(run.Text).Length;
            }

            if (inline is LineBreak or InlineUIContainer)
            {
                return 1;
            }

            return inline is Span span
                ? span.Inlines.Sum(GetInlineTextLength)
                : 0;
        }

        private static IEnumerable<Hyperlink> GetAllHyperlinks(BlockCollection blocks)
        {
            foreach (Block block in blocks)
            {
                if (block is Paragraph paragraph)
                {
                    foreach (Hyperlink hyperlink in GetAllHyperlinks(paragraph.Inlines))
                    {
                        yield return hyperlink;
                    }
                }
                else if (block is FlowList list)
                {
                    foreach (ListItem item in list.ListItems)
                    {
                        foreach (Hyperlink hyperlink in GetAllHyperlinks(item.Blocks))
                        {
                            yield return hyperlink;
                        }
                    }
                }
                else if (block is Section section)
                {
                    foreach (Hyperlink hyperlink in GetAllHyperlinks(section.Blocks))
                    {
                        yield return hyperlink;
                    }
                }
                else if (block is Table table)
                {
                    foreach (TableRowGroup rowGroup in table.RowGroups)
                    {
                        foreach (TableRow row in rowGroup.Rows)
                        {
                            foreach (TableCell cell in row.Cells)
                            {
                                foreach (Hyperlink hyperlink in GetAllHyperlinks(cell.Blocks))
                                {
                                    yield return hyperlink;
                                }
                            }
                        }
                    }
                }
            }
        }

        private static IEnumerable<Hyperlink> GetAllHyperlinks(InlineCollection inlines)
        {
            foreach (Inline inline in inlines)
            {
                if (inline is Hyperlink hyperlink)
                {
                    yield return hyperlink;
                }
                else if (inline is Span span)
                {
                    foreach (Hyperlink child in GetAllHyperlinks(span.Inlines))
                    {
                        yield return child;
                    }
                }
            }
        }

        private (int Start, int End, bool Changed) ClearSelectedTextMarkers(int selectionStart, int selectionEnd)
        {
            string text = GetEditorText();
            QuickNoteRangeEdit edit = QuickNoteDocumentFormatting.GetClearLineMarkerRangeEdit(text, selectionStart, selectionEnd);
            if (!(edit.RemoveLength == edit.InsertText.Length &&
                  string.Equals(text.Substring(edit.StartOffset, edit.RemoveLength), edit.InsertText, StringComparison.Ordinal)))
            {
                ApplyRangeEdit(edit);
                return (edit.CaretOffset, edit.CaretOffset + edit.SelectionLength, true);
            }

            return (selectionStart, selectionEnd, false);
        }

        private void ResetSelectionFormatting()
        {
            TxtNote.Selection.ApplyPropertyValue(TextElement.FontWeightProperty, FontWeights.Normal);
            TxtNote.Selection.ApplyPropertyValue(TextElement.FontStyleProperty, FontStyles.Normal);
            TxtNote.Selection.ApplyPropertyValue(TextElement.FontSizeProperty, QuickNoteDocumentFormatting.GetHeadingFontSizeForLevel(0));
            TxtNote.Selection.ApplyPropertyValue(Inline.TextDecorationsProperty, null);
            TxtNote.Selection.ApplyPropertyValue(TextElement.FontFamilyProperty, QuickNoteFonts.Default);
            TxtNote.Selection.ApplyPropertyValue(TextElement.ForegroundProperty, Brush(_theme.Text));
        }

        private static IEnumerable<FlowList> GetAllListsRecursively(BlockCollection blocks)
        {
            foreach (Block block in blocks)
            {
                if (block is FlowList list)
                {
                    yield return list;
                    
                    // Check for nested lists inside list items
                    foreach (ListItem item in list.ListItems)
                    {
                        foreach (FlowList nestedList in GetAllListsRecursively(item.Blocks))
                        {
                            yield return nestedList;
                        }
                    }
                }
                else if (block is Section section)
                {
                    // Check for lists inside sections
                    foreach (FlowList nestedList in GetAllListsRecursively(section.Blocks))
                    {
                        yield return nestedList;
                    }
                }
            }
        }

        private void RemoveSelectedListFormatting(TextRange selection)
        {
            TextPointer start = selection.Start;
            TextPointer end = selection.End;

            if (start.CompareTo(end) > 0)
            {
                (start, end) = (end, start);
            }

            var selectedLists = GetAllListsRecursively(TxtNote.Document.Blocks)
                .Select(list => (List: list, Items: GetSelectedListItems(list, start, end).ToList()))
                .Where(selection => selection.Items.Count > 0)
                .ToList();

            if (selectedLists.Count == 0)
            {
                return;
            }

            TxtNote.BeginChange();
            try
            {
                foreach (var (list, items) in selectedLists)
                {
                    UnwrapSelectedListItems(list, items);
                }
            }
            finally
            {
                TxtNote.EndChange();
            }
        }

        private static bool TextRangesIntersect(TextPointer selectionStart, TextPointer selectionEnd, TextPointer rangeStart, TextPointer rangeEnd)
        {
            bool collapsedSelection = selectionStart.CompareTo(selectionEnd) == 0;
            if (collapsedSelection)
            {
                return rangeStart.CompareTo(selectionStart) <= 0 && rangeEnd.CompareTo(selectionStart) >= 0;
            }

            return rangeStart.CompareTo(selectionEnd) < 0 && rangeEnd.CompareTo(selectionStart) > 0;
        }

        private IEnumerable<ListItem> GetSelectedListItems(
            FlowList list,
            TextPointer selectionStart,
            TextPointer selectionEnd)
        {
            foreach (ListItem item in list.ListItems)
            {
                if (TextRangesIntersect(selectionStart, selectionEnd, item.ContentStart, item.ContentEnd))
                {
                    yield return item;
                }
            }
        }

        private void UnwrapSelectedListItems(FlowList list, IReadOnlyCollection<ListItem> selectedItems)
        {
            // Determine the parent block collection
            BlockCollection? parentBlocks = null;
            if (list.Parent is ListItem parentListItem)
            {
                parentBlocks = parentListItem.Blocks;
            }
            else if (list.Parent is FlowDocument parentDocument)
            {
                parentBlocks = parentDocument.Blocks;
            }
            else if (list.Parent is Section parentSection)
            {
                parentBlocks = parentSection.Blocks;
            }
            
            if (parentBlocks == null)
            {
                return;
            }

            var allItems = list.ListItems.ToList();
            var selectedSet = selectedItems.ToHashSet();
            var beforeItems = allItems.TakeWhile(item => !selectedSet.Contains(item)).ToList();
            var afterItems = allItems.Skip(beforeItems.Count + selectedItems.Count).ToList();

            if (beforeItems.Count > 0)
            {
                FlowList beforeList = CreateListShell(list);
                foreach (ListItem item in beforeItems)
                {
                    list.ListItems.Remove(item);
                    beforeList.ListItems.Add(item);
                }

                parentBlocks.InsertBefore(list, beforeList);
            }

            foreach (ListItem item in selectedItems)
            {
                foreach (Block block in item.Blocks.ToList())
                {
                    item.Blocks.Remove(block);
                    parentBlocks.InsertBefore(list, block);
                }

                list.ListItems.Remove(item);
            }

            if (afterItems.Count > 0)
            {
                FlowList afterList = CreateListShell(list);
                foreach (ListItem item in afterItems)
                {
                    list.ListItems.Remove(item);
                    afterList.ListItems.Add(item);
                }

                parentBlocks.InsertBefore(list, afterList);
            }

            parentBlocks.Remove(list);
        }

        private static FlowList CreateListShell(FlowList source) =>
            new()
            {
                MarkerStyle = source.MarkerStyle,
                Margin = source.Margin,
                Padding = source.Padding,
                Tag = source.Tag
            };

    }
}
