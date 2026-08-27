using System;
using System.Collections.Generic;
using System.Runtime.Versioning;
using System.Text.RegularExpressions;
using System.Windows.Controls;
using System.Windows.Documents;
using WpfRichTextBox = System.Windows.Controls.RichTextBox;

namespace AiteBar
{
    internal sealed class QuickNoteFindResult
    {
        public int TotalMatches { get; set; }
        public int CurrentIndex { get; set; }
        public bool Found { get; set; }
    }

    [SupportedOSPlatform("windows6.1")]
    internal sealed class QuickNoteFindController
    {
        public static QuickNoteFindResult Find(WpfRichTextBox editor, string query, bool forward, FindReplaceOptions options)
        {
            return Find(editor, query, forward, matchCase: options.CaseSensitive, wholeWord: options.WholeWord);
        }

        public static QuickNoteFindResult Find(WpfRichTextBox editor, string query, bool forward = true, bool matchCase = false, bool wholeWord = false)
        {
            var result = new QuickNoteFindResult();
            if (editor?.Document == null || string.IsNullOrEmpty(query))
            {
                return result;
            }

            List<(TextPointer Start, TextPointer End)> allMatches = FindAllMatches(editor.Document, query, matchCase, wholeWord);
            result.TotalMatches = allMatches.Count;

            if (allMatches.Count == 0)
            {
                return result;
            }

            TextPointer currentCaret = editor.Selection.Start ?? editor.Document.ContentStart;

            int targetIndex = -1;
            if (forward)
            {
                for (int i = 0; i < allMatches.Count; i++)
                {
                    if (allMatches[i].Start.CompareTo(currentCaret) > 0)
                    {
                        targetIndex = i;
                        break;
                    }
                }
                if (targetIndex == -1)
                {
                    targetIndex = 0; // wrap around to first
                }
            }
            else
            {
                for (int i = allMatches.Count - 1; i >= 0; i--)
                {
                    if (allMatches[i].End.CompareTo(currentCaret) < 0)
                    {
                        targetIndex = i;
                        break;
                    }
                }
                if (targetIndex == -1)
                {
                    targetIndex = allMatches.Count - 1; // wrap around to last
                }
            }

            var targetMatch = allMatches[targetIndex];
            editor.Selection.Select(targetMatch.Start, targetMatch.End);
            
            // Scroll match into view
            if (targetMatch.Start.Paragraph != null)
            {
                targetMatch.Start.Paragraph.BringIntoView();
            }

            result.CurrentIndex = targetIndex + 1;
            result.Found = true;
            return result;
        }

        public static int CountMatches(FlowDocument document, string query, FindReplaceOptions options)
        {
            return CountMatches(document, query, matchCase: options.CaseSensitive, wholeWord: options.WholeWord);
        }

        public static int CountMatches(FlowDocument document, string query, bool matchCase = false, bool wholeWord = false)
        {
            if (document == null || string.IsNullOrEmpty(query))
            {
                return 0;
            }

            return FindAllMatches(document, query, matchCase, wholeWord).Count;
        }

        public static List<(TextPointer Start, TextPointer End)> FindAllMatches(FlowDocument document, string query, FindReplaceOptions options)
        {
            return FindAllMatches(document, query, matchCase: options.CaseSensitive, wholeWord: options.WholeWord);
        }

        public static List<(TextPointer Start, TextPointer End)> FindAllMatches(FlowDocument document, string query, bool matchCase = false, bool wholeWord = false)
        {
            var matches = new List<(TextPointer Start, TextPointer End)>();
            if (document == null || string.IsNullOrEmpty(query))
            {
                return matches;
            }

            StringComparison comparison = matchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

            TextPointer position = document.ContentStart;
            while (position != null && position.CompareTo(document.ContentEnd) < 0)
            {
                if (position.GetPointerContext(LogicalDirection.Forward) == TextPointerContext.Text)
                {
                    string textRun = position.GetTextInRun(LogicalDirection.Forward);
                    int searchIndex = 0;
                    while (searchIndex < textRun.Length)
                    {
                        int matchIndex = textRun.IndexOf(query, searchIndex, comparison);
                        if (matchIndex == -1)
                        {
                            break;
                        }

                        if (!wholeWord || IsWholeWord(textRun, matchIndex, query.Length))
                        {
                            TextPointer start = position.GetPositionAtOffset(matchIndex, LogicalDirection.Forward);
                            TextPointer end = position.GetPositionAtOffset(matchIndex + query.Length, LogicalDirection.Forward);
                            if (start != null && end != null)
                            {
                                matches.Add((start, end));
                            }
                        }

                        searchIndex = matchIndex + query.Length;
                    }
                }

                position = position.GetNextContextPosition(LogicalDirection.Forward);
            }

            return matches;
        }

        private static bool IsWholeWord(string text, int index, int length)
        {
            if (index > 0 && char.IsLetterOrDigit(text[index - 1]))
            {
                return false;
            }

            int afterIndex = index + length;
            if (afterIndex < text.Length && char.IsLetterOrDigit(text[afterIndex]))
            {
                return false;
            }

            return true;
        }
    }
}
