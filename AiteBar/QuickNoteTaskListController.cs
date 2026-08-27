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
    }
}
