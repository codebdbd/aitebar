using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;
using System.Windows.Documents;
using FlowList = System.Windows.Documents.List;

namespace AiteBar
{
    [SupportedOSPlatform("windows6.1")]
    internal static class QuickNoteHyperlinkHelper
    {
        public static void InsertHyperlinkAtPointer(TextPointer pointer, Hyperlink hyperlink)
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

        public static void InsertHyperlinkInRun(Run run, TextPointer pointer, Hyperlink hyperlink)
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
                siblings.InsertAfter(anchor, QuickNoteDocumentContract.CloneRunWithText(run, after));
            }
        }

        public static void InsertInlineInCollection(InlineCollection inlines, TextPointer pointer, Inline inline)
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

        public static InlineCollection? GetInlineSiblings(Inline inline)
        {
            return inline.Parent switch
            {
                Paragraph paragraph => paragraph.Inlines,
                Span span => span.Inlines,
                _ => null
            };
        }

        public static bool ContainsInline(InlineCollection inlines, Inline inline)
        {
            return inlines.Cast<Inline>().Any(candidate => ReferenceEquals(candidate, inline));
        }

        public static void UnwrapHyperlinksInSelection(FlowDocument document, TextRange selection)
        {
            TextPointer start = selection.Start;
            TextPointer end = selection.End;
            var hyperlinks = GetAllHyperlinks(document.Blocks)
                .Where(hyperlink => QuickNoteListHelper.TextRangesIntersect(start, end, hyperlink.ContentStart, hyperlink.ContentEnd))
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

        public static void ReplaceHyperlinkWithFragments(
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

        public static Hyperlink CreateHyperlinkFragment(Hyperlink source, IEnumerable<Inline> inlines)
        {
            Hyperlink fragment = QuickNoteDocumentContract.CloneHyperlinkShell(source);

            foreach (Inline inline in inlines)
            {
                fragment.Inlines.Add(inline);
            }

            return fragment;
        }

        public static IReadOnlyList<Inline> CloneInlineRange(InlineCollection inlines, int start, int end)
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

        public static Inline? CloneInlineRange(Inline inline, int start, int end)
        {
            if (inline is Run run)
            {
                return QuickNoteDocumentContract.CloneRunWithText(run, run.Text[start..end]);
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
                Span clone = QuickNoteDocumentContract.CloneSpanShell(span);
                foreach (Inline child in CloneInlineRange(span.Inlines, start, end))
                {
                    clone.Inlines.Add(child);
                }

                return clone.Inlines.Count > 0 ? clone : null;
            }

            return null;
        }

        public static int GetInlineTextLength(Inline inline)
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

        public static IEnumerable<Hyperlink> GetAllHyperlinks(BlockCollection blocks)
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

        public static IEnumerable<Hyperlink> GetAllHyperlinks(InlineCollection inlines)
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
    }
}
