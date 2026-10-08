using System.Runtime.Versioning;
using System.Windows.Controls;
using System.Windows.Documents;
using WpfRichTextBox = System.Windows.Controls.RichTextBox;

namespace AiteBar
{
    [SupportedOSPlatform("windows6.1")]
    internal sealed class QuickNoteTaskListController
    {
        public static IEnumerable<Paragraph> EnumerateAllParagraphs(FlowDocument document)
        {
            if (document == null)
            {
                yield break;
            }

            foreach (var p in EnumerateBlocks(document.Blocks))
            {
                yield return p;
            }
        }

        private static IEnumerable<Paragraph> EnumerateBlocks(BlockCollection blocks)
        {
            foreach (Block block in blocks)
            {
                if (block is Paragraph p)
                {
                    yield return p;
                }
                else if (block is Section section)
                {
                    foreach (var inner in EnumerateBlocks(section.Blocks))
                    {
                        yield return inner;
                    }
                }
                else if (block is System.Windows.Documents.List list)
                {
                    foreach (var item in list.ListItems)
                    {
                        foreach (var inner in EnumerateBlocks(item.Blocks))
                        {
                            yield return inner;
                        }
                    }
                }
                else if (block is Table table)
                {
                    foreach (var rg in table.RowGroups)
                    {
                        foreach (var row in rg.Rows)
                        {
                            foreach (var cell in row.Cells)
                            {
                                foreach (var inner in EnumerateBlocks(cell.Blocks))
                                {
                                    yield return inner;
                                }
                            }
                        }
                    }
                }
            }
        }

        public static int ResetAllTasks(WpfRichTextBox editor, QuickNoteTheme theme)
        {
            if (editor?.Document == null)
            {
                return 0;
            }

            int count = 0;
            editor.BeginChange();
            try
            {
                foreach (Paragraph p in EnumerateAllParagraphs(editor.Document))
                {
                    if (QuickNoteDocumentFormatting.IsTaskParagraph(p, out bool isChecked, out _, out CheckBox? cb))
                    {
                        if (isChecked || (cb != null && cb.IsChecked == true))
                        {
                            if (cb != null)
                            {
                                cb.IsChecked = false;
                            }
                            QuickNoteDocumentFormatting.ApplyTaskFormattingToParagraph(p, false, theme);
                            count++;
                        }
                    }
                }
            }
            finally
            {
                editor.EndChange();
            }

            return count;
        }

        public static int MarkAllTasksCompleted(WpfRichTextBox editor, QuickNoteTheme theme)
        {
            if (editor?.Document == null)
            {
                return 0;
            }

            int count = 0;
            editor.BeginChange();
            try
            {
                foreach (Paragraph p in EnumerateAllParagraphs(editor.Document))
                {
                    if (QuickNoteDocumentFormatting.IsTaskParagraph(p, out bool isChecked, out _, out CheckBox? cb))
                    {
                        if (!isChecked || (cb != null && cb.IsChecked != true))
                        {
                            if (cb != null)
                            {
                                cb.IsChecked = true;
                            }
                            QuickNoteDocumentFormatting.ApplyTaskFormattingToParagraph(p, true, theme);
                            count++;
                        }
                    }
                }
            }
            finally
            {
                editor.EndChange();
            }

            return count;
        }

        public static int ToggleAllTasks(WpfRichTextBox editor, QuickNoteTheme theme)
        {
            if (editor?.Document == null)
            {
                return 0;
            }

            int count = 0;
            editor.BeginChange();
            try
            {
                foreach (Paragraph p in EnumerateAllParagraphs(editor.Document))
                {
                    if (QuickNoteDocumentFormatting.IsTaskParagraph(p, out bool isChecked, out _, out CheckBox? cb))
                    {
                        bool newState = !isChecked;
                        if (cb != null)
                        {
                            cb.IsChecked = newState;
                        }
                        QuickNoteDocumentFormatting.ApplyTaskFormattingToParagraph(p, newState, theme);
                        count++;
                    }
                }
            }
            finally
            {
                editor.EndChange();
            }

            return count;
        }

        public static bool HandleEnterKey(
            WpfRichTextBox editor,
            QuickNoteTheme theme,
            Action<Paragraph, bool> onTaskToggled,
            Action<Paragraph> connectTask)
        {
            if (editor?.Document == null)
            {
                return false;
            }

            Paragraph? currentParagraph = editor.CaretPosition?.Paragraph;
            if (currentParagraph == null || !QuickNoteDocumentFormatting.IsTaskParagraph(currentParagraph, out _, out _, out _))
            {
                return false;
            }

            string itemText = new TextRange(currentParagraph.ContentStart, currentParagraph.ContentEnd).Text.Trim();
            if (string.IsNullOrEmpty(itemText))
            {
                editor.BeginChange();
                try
                {
                    QuickNoteDocumentFormatting.RemoveTaskCheckbox(currentParagraph, theme);
                }
                finally
                {
                    editor.EndChange();
                }

                return true;
            }

            editor.BeginChange();
            try
            {
                if (!editor.Selection.IsEmpty)
                {
                    editor.Selection.Text = string.Empty;
                }

                TextPointer? caret = editor.CaretPosition;
                if (caret == null)
                {
                    return false;
                }

                var newParagraph = new Paragraph();
                var newContainer = QuickNoteDocumentFormatting.CreateTaskCheckbox(false, isChecked => onTaskToggled(newParagraph, isChecked), theme);
                newParagraph.Inlines.Add(newContainer);

                if (caret.CompareTo(currentParagraph.ContentEnd) >= 0)
                {
                    newParagraph.Inlines.Add(new Run(string.Empty));
                }
                else
                {
                    TextRange tailRange = new TextRange(caret, currentParagraph.ContentEnd);
                    using var stream = new System.IO.MemoryStream();
                    tailRange.Save(stream, System.Windows.DataFormats.XamlPackage);
                    tailRange.Text = string.Empty;

                    stream.Position = 0;
                    var tempDoc = new FlowDocument();
                    var tempRange = new TextRange(tempDoc.ContentStart, tempDoc.ContentEnd);
                    tempRange.Load(stream, System.Windows.DataFormats.XamlPackage);

                    List<Inline> extractedInlines = new();
                    foreach (Block b in tempDoc.Blocks.ToList())
                    {
                        if (b is Paragraph p)
                        {
                            foreach (Inline inline in p.Inlines.ToList())
                            {
                                p.Inlines.Remove(inline);
                                extractedInlines.Add(inline);
                            }
                        }
                    }

                    if (extractedInlines.Count > 0)
                    {
                        foreach (var inline in extractedInlines)
                        {
                            newParagraph.Inlines.Add(inline);
                        }
                    }
                    else
                    {
                        newParagraph.Inlines.Add(new Run(string.Empty));
                    }
                }

                currentParagraph.SiblingBlocks.InsertAfter(currentParagraph, newParagraph);
                QuickNoteDocumentFormatting.ApplyTaskFormattingToParagraph(newParagraph, false, theme);
                connectTask(newParagraph);
                editor.CaretPosition = newParagraph.Inlines.FirstInline?.NextInline?.ContentStart ?? newParagraph.ContentEnd;
            }
            finally
            {
                editor.EndChange();
            }

            return true;
        }

        public static bool HandleBackspaceKey(WpfRichTextBox editor, QuickNoteTheme theme)
        {
            if (editor?.Document == null || !editor.Selection.IsEmpty)
            {
                return false;
            }

            Paragraph? currentParagraph = editor.CaretPosition?.Paragraph;
            if (currentParagraph == null || !QuickNoteDocumentFormatting.IsTaskParagraph(currentParagraph, out _, out _, out _))
            {
                return false;
            }

            TextPointer? caret = editor.CaretPosition;
            if (caret == null)
            {
                return false;
            }

            TextRange headRange = new TextRange(currentParagraph.ContentStart, caret);
            if (string.IsNullOrWhiteSpace(headRange.Text))
            {
                editor.BeginChange();
                try
                {
                    QuickNoteDocumentFormatting.RemoveTaskCheckbox(currentParagraph, theme);
                }
                finally
                {
                    editor.EndChange();
                }

                return true;
            }

            return false;
        }
    }
}
