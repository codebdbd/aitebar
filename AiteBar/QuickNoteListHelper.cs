using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;
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
    }
}
