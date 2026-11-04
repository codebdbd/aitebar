using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using FlowList = System.Windows.Documents.List;

namespace AiteBar
{
    [SupportedOSPlatform("windows6.1")]
    internal static class QuickNoteListHelper
    {
        public static bool TextRangesIntersect(TextPointer selectionStart, TextPointer selectionEnd, TextPointer rangeStart, TextPointer rangeEnd)
        {
            bool collapsedSelection = selectionStart.CompareTo(selectionEnd) == 0;
            if (collapsedSelection)
            {
                return rangeStart.CompareTo(selectionStart) <= 0 && rangeEnd.CompareTo(selectionStart) >= 0;
            }

            return rangeStart.CompareTo(selectionEnd) < 0 && rangeEnd.CompareTo(selectionStart) > 0;
        }

        public static IEnumerable<FlowList> GetAllListsRecursively(BlockCollection blocks)
        {
            foreach (Block block in blocks)
            {
                if (block is FlowList list)
                {
                    yield return list;

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
                    foreach (FlowList nestedList in GetAllListsRecursively(section.Blocks))
                    {
                        yield return nestedList;
                    }
                }
            }
        }

        public static void RemoveSelectedListFormatting(FlowDocument document, TextRange selection)
        {
            TextPointer start = selection.Start;
            TextPointer end = selection.End;

            if (start.CompareTo(end) > 0)
            {
                (start, end) = (end, start);
            }

            var selectedLists = GetAllListsRecursively(document.Blocks)
                .Select(list => (List: list, Items: GetSelectedListItems(list, start, end).ToList()))
                .Where(s => s.Items.Count > 0)
                .ToList();

            if (selectedLists.Count == 0)
            {
                return;
            }

            foreach (var (list, items) in selectedLists)
            {
                UnwrapSelectedListItems(list, items);
            }
        }

        public static IEnumerable<ListItem> GetSelectedListItems(
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

        public static void UnwrapSelectedListItems(FlowList list, IReadOnlyCollection<ListItem> selectedItems)
        {
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

        public static FlowList CreateListShell(FlowList source) =>
            new()
            {
                MarkerStyle = source.MarkerStyle,
                Margin = source.Margin,
                Padding = source.Padding,
                Tag = source.Tag
            };

        public static void RemoveSelectedTaskFormatting(FlowDocument document, TextRange selection, QuickNoteTheme theme, bool preserveLinks)
        {
            TextPointer start = selection.Start;
            TextPointer end = selection.End;

            if (start.CompareTo(end) > 0)
            {
                (start, end) = (end, start);
            }

            List<Paragraph> taskParagraphs = QuickNoteTaskListController.EnumerateAllParagraphs(document)
                .Where(p => QuickNoteDocumentFormatting.IsTaskParagraph(p, out _, out _, out _))
                .Where(p => TextRangesIntersect(start, end, p.ContentStart, p.ContentEnd))
                .ToList();

            if (taskParagraphs.Count == 0)
            {
                return;
            }

            foreach (Paragraph p in taskParagraphs)
            {
                UnwrapTaskParagraph(p, theme, preserveLinks);
            }
        }

        public static void UnwrapTaskParagraph(Paragraph paragraph, QuickNoteTheme theme, bool preserveLinks)
        {
            if (QuickNoteDocumentFormatting.IsTaskParagraph(paragraph, out _, out InlineUIContainer? container, out _))
            {
                if (container != null)
                {
                    if (container.Parent is Span parentSpan)
                    {
                        parentSpan.Inlines.Remove(container);
                    }
                    else
                    {
                        paragraph.Inlines.Remove(container);
                    }
                }
            }

            paragraph.ClearValue(Paragraph.TextDecorationsProperty);
            QuickNoteDocumentFormatting.SetIsUserStrikethrough(paragraph, false);
            paragraph.Tag = null;
            paragraph.Foreground = QuickNoteBrush.FromHex(theme.Text);
            paragraph.ClearValue(TextElement.FontSizeProperty);
            paragraph.ClearValue(TextElement.FontWeightProperty);
            paragraph.ClearValue(TextElement.FontStyleProperty);
            paragraph.ClearValue(TextElement.FontFamilyProperty);

            ClearInlinesFormatting(paragraph.Inlines, preserveLinks);
        }

        public static void ClearSelectedTaskInlineFormatting(FlowDocument document, TextRange selection, QuickNoteTheme theme)
        {
            TextPointer start = selection.Start;
            TextPointer end = selection.End;

            if (start.CompareTo(end) > 0)
            {
                (start, end) = (end, start);
            }

            List<Paragraph> taskParagraphs = QuickNoteTaskListController.EnumerateAllParagraphs(document)
                .Where(p => QuickNoteDocumentFormatting.IsTaskParagraph(p, out _, out _, out _))
                .Where(p => TextRangesIntersect(start, end, p.ContentStart, p.ContentEnd))
                .ToList();

            foreach (Paragraph p in taskParagraphs)
            {
                if (!QuickNoteDocumentFormatting.IsTaskParagraph(p, out bool isChecked, out InlineUIContainer? container, out _))
                {
                    continue;
                }

                QuickNoteDocumentFormatting.SetIsUserStrikethrough(p, false);
                p.ClearValue(TextElement.FontSizeProperty);
                p.ClearValue(TextElement.FontWeightProperty);
                p.ClearValue(TextElement.FontStyleProperty);
                p.ClearValue(TextElement.FontFamilyProperty);

                ClearInlinesFormatting(p.Inlines, preserveLinks: true, skipContainer: container);

                QuickNoteDocumentFormatting.ApplyTaskFormattingToParagraph(p, isChecked, theme);
            }
        }

        private static void ClearInlinesFormatting(InlineCollection inlines, bool preserveLinks, InlineUIContainer? skipContainer = null)
        {
            foreach (Inline inline in inlines.ToList())
            {
                if (inline is InlineUIContainer uic)
                {
                    if (uic == skipContainer) continue;
                    continue;
                }

                QuickNoteDocumentFormatting.SetIsUserStrikethrough(inline, false);
                QuickNoteDocumentFormatting.SetIsTaskStrikethrough(inline, false);

                if (inline is Hyperlink hyperlink)
                {
                    if (!preserveLinks)
                    {
                        hyperlink.ClearValue(TextElement.ForegroundProperty);
                        hyperlink.ClearValue(Inline.TextDecorationsProperty);
                    }
                    ClearInlinesFormatting(hyperlink.Inlines, preserveLinks, skipContainer);
                    continue;
                }

                inline.ClearValue(TextElement.FontSizeProperty);
                inline.ClearValue(TextElement.FontWeightProperty);
                inline.ClearValue(TextElement.FontStyleProperty);
                inline.ClearValue(TextElement.FontFamilyProperty);
                inline.ClearValue(TextElement.ForegroundProperty);
                inline.ClearValue(TextElement.BackgroundProperty);
                inline.ClearValue(Inline.TextDecorationsProperty);

                if (inline is Span span)
                {
                    ClearInlinesFormatting(span.Inlines, preserveLinks, skipContainer);
                }
            }
        }
    }
}
