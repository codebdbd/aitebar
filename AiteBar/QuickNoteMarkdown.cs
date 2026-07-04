using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using FlowList = System.Windows.Documents.List;
using MediaFontFamily = System.Windows.Media.FontFamily;

namespace AiteBar
{
    internal readonly record struct QuickNoteTextEdit(string Text, int CaretOffset, int SelectionLength);
    internal readonly record struct QuickNoteTextOperation(int Offset, int RemoveLength, string InsertText);
    internal readonly record struct QuickNoteRangeEdit(int StartOffset, int RemoveLength, string InsertText, int CaretOffset, int SelectionLength);

    internal static class QuickNoteMarkdown
    {
        private static readonly MediaFontFamily DefaultFont = new("Segoe UI");
        private static readonly MediaFontFamily CodeFont = new("Consolas");
        private static readonly Regex UrlRegex = new(@"(?i)\b(?:https?://|www\.)[^\s<>()""']+", RegexOptions.Compiled);
        private static readonly Regex EmailRegex = new(@"(?i)\b[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}\b", RegexOptions.Compiled);
        private static readonly Regex PhoneRegex = new(@"(?i)\b(?:\+?\d{1,3}[-.\s]?)?\(?\d{3}\)?[-.\s]?\d{3}[-.\s]?\d{4}\b", RegexOptions.Compiled);
        private static readonly Regex BulletMarkerRegex = new(@"^(?<indent>\s*)[-*]\s+", RegexOptions.Compiled);
        private static readonly Regex NumberMarkerRegex = new(@"^(?<indent>\s*)\d+\.\s+", RegexOptions.Compiled);
        private static readonly Regex AnyListMarkerRegex = new(@"^(?<indent>\s*)(?:[-*]\s+|\d+\.\s+)", RegexOptions.Compiled);
        private static readonly Regex HeadingMarkerRegex = new(@"^(?<indent>\s*)(?<marker>#{1,6})\s+", RegexOptions.Compiled);
        private const string HeadingTagPrefix = "heading:";

        public enum LinkType
        {
            Url,
            Email,
            Phone
        }

        public static IEnumerable<(Match Match, LinkType Type)> MatchLinks(string text)
        {
            var matches = new List<(Match Match, LinkType Type)>();
            foreach (Match match in UrlRegex.Matches(text))
            {
                matches.Add((match, LinkType.Url));
            }
            foreach (Match match in EmailRegex.Matches(text))
            {
                matches.Add((match, LinkType.Email));
            }
            foreach (Match match in PhoneRegex.Matches(text))
            {
                matches.Add((match, LinkType.Phone));
            }
            return matches.OrderBy(m => m.Match.Index);
        }

        public static string NormalizeLinkForOpen(string matchedText, LinkType type)
        {
            string text = matchedText.TrimEnd('.', ',', ';', ':', '!', '?', ')', ']');
            return type switch
            {
                LinkType.Url => text.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? "https://" + text : text,
                LinkType.Email => "mailto:" + text,
                LinkType.Phone => "tel:" + text,
                _ => text
            };
        }

        public static IEnumerable<Match> MatchUrls(string text)
        {
            return UrlRegex.Matches(text);
        }

        public static string NormalizeUrlForOpen(string matchedUrl)
        {
            return NormalizeLinkForOpen(matchedUrl, LinkType.Url);
        }

        public static void LoadMarkdown(FlowDocument document, string markdown)
        {
            document.Blocks.Clear();
            string[] lines = NormalizeLineEndings(markdown).Split('\n');
            int index = 0;
            var listStack = new Stack<(FlowList List, int IndentLevel)>();

            while (index < lines.Length)
            {
                string line = lines[index];
                if (TryReadListLine(line, out bool numbered, out string listText, out string indent))
                {
                    int indentLevel = indent.Length;
                    
                    // Pop stack to find the correct parent level
                    while (listStack.Count > 0 && listStack.Peek().IndentLevel >= indentLevel)
                    {
                        listStack.Pop();
                    }

                    FlowList currentList;
                    bool isNewList = true;
                    
                    if (listStack.Count == 0)
                    {
                        // Top-level list: check if previous block was a list of same type AND same indent
                        if (document.Blocks.LastBlock is FlowList lastTopLevel &&
                            lastTopLevel.MarkerStyle == (numbered ? TextMarkerStyle.Decimal : TextMarkerStyle.Disc) &&
                            GetListIndent(lastTopLevel) == indent)
                        {
                            currentList = lastTopLevel;
                            isNewList = false;
                        }
                        else
                        {
                            currentList = CreateList(numbered, indent);
                            document.Blocks.Add(currentList);
                        }
                    }
                    else
                    {
                        // Nested list: check if last item of parent has a list of same type
                        var (parentList, _) = listStack.Peek();
                        if (parentList.ListItems.Count == 0)
                        {
                            parentList.ListItems.Add(CreateListItem(string.Empty));
                        }
                        var lastItemParent = parentList.ListItems.Last();
                        if (lastItemParent.Blocks.LastBlock is FlowList lastNestedList &&
                            lastNestedList.MarkerStyle == (numbered ? TextMarkerStyle.Decimal : TextMarkerStyle.Disc))
                        {
                            currentList = lastNestedList;
                            isNewList = false;
                        }
                        else
                        {
                            currentList = CreateList(numbered, indent);
                            lastItemParent.Blocks.Add(currentList);
                        }
                    }
                    
                    // Add current list item
                    currentList.ListItems.Add(CreateListItem(listText));
                    
                    // Push to stack only if it was a new list
                    if (isNewList)
                    {
                        listStack.Push((currentList, indentLevel));
                    }
                    else
                    {
                        // If we reused an existing list at this indent, we need to restore it to the top of stack
                        // Because we popped it earlier when checking indent level
                        listStack.Push((currentList, indentLevel));
                    }
                    
                    index++;
                    continue;
                }

                // Not a list line - clear stack and add paragraph
                listStack.Clear();
                document.Blocks.Add(CreateParagraphFromMarkdownLine(line));
                index++;
            }

            if (!document.Blocks.Any())
            {
                document.Blocks.Add(CreateParagraph(CreateRun(string.Empty)));
            }
        }

        public static string ToMarkdown(FlowDocument document)
        {
            var builder = new StringBuilder();
            bool firstParagraph = true;
            foreach (Block block in document.Blocks)
            {
                if (!firstParagraph)
                {
                    builder.AppendLine();
                }

                if (block is Paragraph paragraph)
                {
                    AppendParagraphMarkdown(builder, paragraph);
                }
                else if (block is FlowList list)
                {
                    AppendListMarkdown(builder, list);
                }
                else
                {
                    continue;
                }

                firstParagraph = false;
            }

            return builder.ToString().TrimEnd('\r', '\n');
        }

        public static QuickNoteTextEdit ToggleListMarkers(string text, int selectionStart, int selectionEnd, bool numbered)
        {
            text = NormalizeLineEndings(text);
            QuickNoteTextOperation[] operations = GetListMarkerOperations(text, selectionStart, selectionEnd, numbered);
            string updatedText = ApplyOperations(text, operations);
            int caret = MapOffsetThroughOperations(Math.Max(selectionStart, selectionEnd), operations);
            return new QuickNoteTextEdit(updatedText, caret, 0);
        }

        public static QuickNoteRangeEdit GetToggleListMarkerRangeEdit(string text, int selectionStart, int selectionEnd, bool numbered)
        {
            text = NormalizeLineEndings(text);
            var (lineStart, lineEnd) = GetSelectedLineBounds(text, selectionStart, selectionEnd);
            string selectedText = text[lineStart..lineEnd];
            int relativeSelectionStart = Math.Clamp(selectionStart - lineStart, 0, selectedText.Length);
            int relativeSelectionEnd = Math.Clamp(selectionEnd - lineStart, 0, selectedText.Length);
            QuickNoteTextOperation[] operations = GetListMarkerOperations(selectedText, relativeSelectionStart, relativeSelectionEnd, numbered);
            string updatedText = ApplyOperations(selectedText, operations);
            int mappedStart = lineStart + MapOffsetThroughOperations(relativeSelectionStart, operations);
            int mappedEnd = lineStart + MapOffsetThroughOperations(relativeSelectionEnd, operations);
            return new QuickNoteRangeEdit(lineStart, lineEnd - lineStart, updatedText, mappedStart, Math.Max(0, mappedEnd - mappedStart));
        }

        public static QuickNoteTextEdit ClearLineMarkers(string text, int selectionStart, int selectionEnd)
        {
            text = NormalizeLineEndings(text);
            QuickNoteTextOperation[] operations = GetClearMarkerOperations(text, selectionStart, selectionEnd);
            string updatedText = ApplyOperations(text, operations);
            int caret = MapOffsetThroughOperations(Math.Max(selectionStart, selectionEnd), operations);
            return new QuickNoteTextEdit(updatedText, caret, 0);
        }

        public static QuickNoteRangeEdit GetClearLineMarkerRangeEdit(string text, int selectionStart, int selectionEnd)
        {
            text = NormalizeLineEndings(text);
            var (lineStart, lineEnd) = GetSelectedLineBounds(text, selectionStart, selectionEnd);
            string selectedText = text[lineStart..lineEnd];
            QuickNoteTextOperation[] operations = GetClearMarkerOperations(selectedText, selectionStart - lineStart, selectionEnd - lineStart);
            string updatedText = ApplyOperations(selectedText, operations);
            int caret = lineStart + MapOffsetThroughOperations(Math.Max(selectionStart, selectionEnd) - lineStart, operations);
            return new QuickNoteRangeEdit(lineStart, lineEnd - lineStart, updatedText, caret, 0);
        }

        public static QuickNoteRangeEdit GetHeadingRangeEdit(string text, int selectionStart, int selectionEnd, int headingLevel)
        {
            if (headingLevel is < 0 or > 6)
            {
                throw new ArgumentOutOfRangeException(nameof(headingLevel), "Heading level must be 0 for body text or 1 through 6.");
            }

            text = NormalizeLineEndings(text);
            var (lineStart, lineEnd) = GetSelectedLineBounds(text, selectionStart, selectionEnd);
            string selectedText = text[lineStart..lineEnd];
            QuickNoteTextOperation[] operations = GetHeadingOperations(selectedText, selectionStart - lineStart, selectionEnd - lineStart, headingLevel);
            string updatedText = ApplyOperations(selectedText, operations);
            int caret = lineStart + MapOffsetThroughOperations(Math.Max(selectionStart, selectionEnd) - lineStart, operations);
            return new QuickNoteRangeEdit(lineStart, lineEnd - lineStart, updatedText, caret, 0);
        }

        public static QuickNoteTextOperation[] GetListMarkerOperations(string text, int selectionStart, int selectionEnd, bool numbered)
        {
            var lines = GetSelectedLines(text, selectionStart, selectionEnd);
            bool removeList = LinesHaveListMarker(lines.Select(line => line.Text), numbered);
            var operations = new List<QuickNoteTextOperation>();
            int number = 1;

            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line.Text))
                {
                    continue;
                }

                var marker = GetListMarker(line.Text);
                int insertOffset = line.Offset + marker.IndentLength;
                if (removeList)
                {
                    if (marker.MarkerLength > 0)
                    {
                        operations.Add(new QuickNoteTextOperation(insertOffset, marker.MarkerLength, string.Empty));
                    }

                    continue;
                }

                if (marker.MarkerLength > 0)
                {
                    operations.Add(new QuickNoteTextOperation(insertOffset, marker.MarkerLength, string.Empty));
                }

                operations.Add(new QuickNoteTextOperation(insertOffset, 0, numbered ? $"{number}. " : "- "));
                number++;
            }

            return operations.ToArray();
        }

        public static QuickNoteTextOperation[] GetClearMarkerOperations(string text, int selectionStart, int selectionEnd)
        {
            return GetSelectedLines(text, selectionStart, selectionEnd)
                .Select(line => (Line: line, Marker: GetListMarker(line.Text)))
                .Where(item => item.Marker.MarkerLength > 0)
                .Select(item => new QuickNoteTextOperation(item.Line.Offset + item.Marker.IndentLength, item.Marker.MarkerLength, string.Empty))
                .ToArray();
        }

        public static QuickNoteTextOperation[] GetHeadingOperations(string text, int selectionStart, int selectionEnd, int headingLevel)
        {
            if (headingLevel is < 0 or > 6)
            {
                throw new ArgumentOutOfRangeException(nameof(headingLevel), "Heading level must be 0 for body text or 1 through 6.");
            }

            var operations = new List<QuickNoteTextOperation>();
            string prefix = headingLevel == 0 ? string.Empty : new string('#', headingLevel) + " ";

            foreach (var line in GetSelectedLines(text, selectionStart, selectionEnd))
            {
                if (string.IsNullOrWhiteSpace(line.Text))
                {
                    continue;
                }

                var marker = GetHeadingMarker(line.Text);
                int markerOffset = line.Offset + marker.IndentLength;
                if (marker.MarkerLength > 0)
                {
                    operations.Add(new QuickNoteTextOperation(markerOffset, marker.MarkerLength, prefix));
                }
                else if (headingLevel > 0)
                {
                    operations.Add(new QuickNoteTextOperation(markerOffset, 0, prefix));
                }
            }

            return operations.ToArray();
        }

        private static void AddMarkdownInlines(InlineCollection inlines, string line)
        {
            var plain = new StringBuilder();
            int index = 0;
            while (index < line.Length)
            {
                if (line[index] == '\\' && index + 1 < line.Length)
                {
                    plain.Append(line[index + 1]);
                    index += 2;
                    continue;
                }

                if (TryReadDelimited(line, index, "**", out string boldText, out int boldEnd))
                {
                    FlushPlain(inlines, plain);
                    inlines.Add(new Bold(CreateRun(UnescapeMarkdownText(boldText))));
                    index = boldEnd;
                    continue;
                }

                if (TryReadHtmlUnderline(line, index, out string underlineText, out int underlineEnd))
                {
                    FlushPlain(inlines, plain);
                    inlines.Add(new Span(CreateRun(UnescapeMarkdownText(underlineText)))
                    {
                        TextDecorations = TextDecorations.Underline
                    });
                    index = underlineEnd;
                    continue;
                }

                if (TryReadDelimited(line, index, "~~", out string strikeText, out int strikeEnd))
                {
                    FlushPlain(inlines, plain);
                    inlines.Add(new Span(CreateRun(UnescapeMarkdownText(strikeText)))
                    {
                        TextDecorations = TextDecorations.Strikethrough
                    });
                    index = strikeEnd;
                    continue;
                }

                if (TryReadMarkdownLink(line, index, out string linkText, out string url, out int linkEnd))
                {
                    FlushPlain(inlines, plain);
                    inlines.Add(CreateHyperlink(UnescapeMarkdownText(linkText), UnescapeMarkdownText(url)));
                    index = linkEnd;
                    continue;
                }

                if (TryReadDelimited(line, index, "`", out string codeText, out int codeEnd))
                {
                    FlushPlain(inlines, plain);
                    var codeSpan = new Span(CreateRun(UnescapeMarkdownText(codeText), CodeFont))
                    {
                        Tag = "code"
                    };
                    inlines.Add(codeSpan);
                    index = codeEnd;
                    continue;
                }

                if (line[index] == '*' && (index + 1 >= line.Length || line[index + 1] != '*') &&
                    TryReadDelimited(line, index, "*", out string italicText, out int italicEnd))
                {
                    FlushPlain(inlines, plain);
                    inlines.Add(new Italic(CreateRun(UnescapeMarkdownText(italicText))));
                    index = italicEnd;
                    continue;
                }

                plain.Append(line[index]);
                index++;
            }

            FlushPlain(inlines, plain);
        }

        private static bool TryReadDelimited(string text, int start, string marker, out string value, out int end)
        {
            value = string.Empty;
            end = start;
            if (!text.AsSpan(start).StartsWith(marker, StringComparison.Ordinal))
            {
                return false;
            }

            int contentStart = start + marker.Length;
            int close = FindClosingMarker(text, marker, contentStart);
            if (close <= contentStart)
            {
                return false;
            }

            value = text[contentStart..close];
            end = close + marker.Length;
            return true;
        }

        private static bool TryReadMarkdownLink(string text, int start, out string linkText, out string url, out int end)
        {
            linkText = string.Empty;
            url = string.Empty;
            end = start;
            if (text[start] != '[')
            {
                return false;
            }

            int textEnd = FindClosingMarker(text, "]", start + 1);
            if (textEnd <= start + 1 || textEnd + 1 >= text.Length || text[textEnd + 1] != '(')
            {
                return false;
            }

            int urlStart = textEnd + 2;
            int urlEnd = FindClosingMarker(text, ")", urlStart);
            if (urlEnd <= urlStart)
            {
                return false;
            }

            linkText = text[(start + 1)..textEnd];
            url = text[urlStart..urlEnd];
            end = urlEnd + 1;
            return true;
        }

        private static int FindClosingMarker(string text, string marker, int start)
        {
            for (int i = start; i <= text.Length - marker.Length; i++)
            {
                if (text[i] == '\\')
                {
                    i++;
                    continue;
                }

                if (text.AsSpan(i).StartsWith(marker, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
        }

        private static bool TryReadHtmlUnderline(string text, int start, out string value, out int end)
        {
            const string open = "<u>";
            const string close = "</u>";
            value = string.Empty;
            end = start;
            if (!text.AsSpan(start).StartsWith(open, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            int contentStart = start + open.Length;
            int closeIndex = text.IndexOf(close, contentStart, StringComparison.OrdinalIgnoreCase);
            if (closeIndex <= contentStart)
            {
                return false;
            }

            value = text[contentStart..closeIndex];
            end = closeIndex + close.Length;
            return true;
        }

        private static bool TryReadHeadingLine(string line, out int headingLevel, out string headingText)
        {
            headingLevel = 0;
            headingText = line;
            Match match = HeadingMarkerRegex.Match(line);
            if (!match.Success)
            {
                return false;
            }

            headingLevel = match.Groups["marker"].Value.Length;
            headingText = line[match.Length..];
            return !string.IsNullOrWhiteSpace(headingText);
        }

        private static bool TryReadListLine(string line, out bool numbered, out string itemText) =>
            TryReadListLine(line, out numbered, out itemText, out _);

        private static bool TryReadListLine(string line, out bool numbered, out string itemText, out string indent)
        {
            numbered = false;
            itemText = line;
            indent = string.Empty;

            Match numberMatch = NumberMarkerRegex.Match(line);
            if (numberMatch.Success)
            {
                numbered = true;
                indent = numberMatch.Groups["indent"].Value;
                itemText = line[numberMatch.Length..];
                return true;
            }

            Match bulletMatch = BulletMarkerRegex.Match(line);
            if (bulletMatch.Success)
            {
                indent = bulletMatch.Groups["indent"].Value;
                itemText = line[bulletMatch.Length..];
                return true;
            }

            return false;
        }

        private static void FlushPlain(InlineCollection inlines, StringBuilder plain)
        {
            if (plain.Length == 0)
            {
                return;
            }

            inlines.Add(CreateRun(plain.ToString()));
            plain.Clear();
        }

        private static Paragraph CreateParagraph(params Inline[] inlines)
        {
            var paragraph = new Paragraph
            {
                FontFamily = DefaultFont,
                FontSize = 14,
                FontWeight = FontWeights.Normal,
                FontStyle = FontStyles.Normal,
                Margin = new Thickness(0)
            };

            foreach (var inline in inlines)
            {
                paragraph.Inlines.Add(inline);
            }

            return paragraph;
        }

        private static Paragraph CreateParagraphFromMarkdownLine(string line)
        {
            if (TryReadHeadingLine(line, out int headingLevel, out string headingText))
            {
                var heading = CreateHeadingSpan(headingLevel);
                AddMarkdownInlines(heading.Inlines, headingText);
                return CreateParagraph(heading);
            }

            var paragraph = CreateParagraph();
            AddMarkdownInlines(paragraph.Inlines, line);
            if (!paragraph.Inlines.Any())
            {
                paragraph.Inlines.Add(CreateRun(string.Empty));
            }

            return paragraph;
        }

        private static FlowList CreateList(bool numbered, string indent = "") =>
            new()
            {
                MarkerStyle = numbered ? TextMarkerStyle.Decimal : TextMarkerStyle.Disc,
                Margin = new Thickness(0),
                Tag = string.IsNullOrEmpty(indent) ? null : "indent:" + indent
            };

        private static ListItem CreateListItem(string markdownText)
        {
            var item = new ListItem
            {
                Margin = new Thickness(0)
            };
            item.Blocks.Add(CreateParagraphFromMarkdownLine(markdownText));
            return item;
        }

        private static Run CreateRun(string text, MediaFontFamily? fontFamily = null) =>
            new(text)
            {
                FontFamily = fontFamily ?? DefaultFont,
                FontWeight = FontWeights.Normal,
                FontStyle = FontStyles.Normal,
                TextDecorations = null
            };

        public static Hyperlink CreateHyperlink(string text, string url)
        {
            var hyperlink = new Hyperlink(CreateRun(text))
            {
                NavigateUri = Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri : null,
                Tag = "link:" + url,
                Foreground = Brushes.DodgerBlue,
                TextDecorations = TextDecorations.Underline
            };
            return hyperlink;
        }

        private static Span CreateHeadingSpan(int headingLevel)
        {
            return new Span
            {
                Tag = HeadingTagPrefix + headingLevel.ToString(System.Globalization.CultureInfo.InvariantCulture),
                FontSize = GetHeadingFontSize(headingLevel),
                FontWeight = FontWeights.SemiBold
            };
        }

        public static double GetHeadingFontSizeForLevel(int headingLevel) => headingLevel switch
        {
            1 => 32,
            2 => 26,
            3 => 22,
            4 => 18,
            5 => 16,
            6 => 15,
            _ => 14
        };

        private static double GetHeadingFontSize(int headingLevel) => GetHeadingFontSizeForLevel(headingLevel);

        private static void AppendParagraphMarkdown(StringBuilder builder, Paragraph paragraph)
        {
            var line = new List<Inline>();
            foreach (Inline inline in paragraph.Inlines)
            {
                if (inline is LineBreak)
                {
                    AppendLineMarkdown(builder, line);
                    builder.AppendLine();
                    line.Clear();
                    continue;
                }

                line.Add(inline);
            }

            AppendLineMarkdown(builder, line);
        }

        private static void AppendListMarkdown(StringBuilder builder, FlowList list) =>
            AppendListMarkdown(builder, list, 0);

        private static void AppendListMarkdown(StringBuilder builder, FlowList list, int nestingLevel)
        {
            bool numbered = list.MarkerStyle is TextMarkerStyle.Decimal or TextMarkerStyle.LowerLatin or TextMarkerStyle.UpperLatin or TextMarkerStyle.LowerRoman or TextMarkerStyle.UpperRoman;
            string indent = GetListIndent(list);
            // If no stored indent (nested list), compute from nesting level
            if (string.IsNullOrEmpty(indent) && nestingLevel > 0)
            {
                indent = new string(' ', nestingLevel * 2);
            }
            string continuationIndent = indent + "  ";
            int number = 1;
            bool firstItem = true;

            foreach (ListItem item in list.ListItems)
            {
                if (!firstItem)
                {
                    builder.AppendLine();
                }

                builder.Append(indent);
                builder.Append(numbered ? $"{number}. " : "- ");
                AppendListItemMarkdown(builder, item, continuationIndent, nestingLevel);
                number++;
                firstItem = false;
            }
        }

        private static void AppendListItemMarkdown(StringBuilder builder, ListItem item, string continuationIndent, int currentNestingLevel)
        {
            bool firstBlock = true;
            foreach (Block block in item.Blocks)
            {
                if (block is FlowList nestedList)
                {
                    if (!firstBlock)
                    {
                        builder.AppendLine();
                    }

                    AppendListMarkdown(builder, nestedList, currentNestingLevel + 1);
                    firstBlock = false;
                    continue;
                }

                if (!firstBlock)
                {
                    builder.AppendLine();
                    builder.Append(continuationIndent);
                }

                if (block is Paragraph paragraph)
                {
                    AppendParagraphMarkdown(builder, paragraph);
                }

                firstBlock = false;
            }
        }

        private static void AppendLineMarkdown(StringBuilder builder, IReadOnlyList<Inline> line)
        {
            if (TryGetHeadingLevelForLine(line, out int headingLevel))
            {
                builder.Append(new string('#', headingLevel));
                builder.Append(' ');
            }

            foreach (Inline inline in line)
            {
                AppendInlineMarkdown(builder, inline, false, false, false, false, false);
            }
        }

        private static bool TryGetHeadingLevelForLine(IReadOnlyList<Inline> line, out int headingLevel)
        {
            headingLevel = 0;
            foreach (Inline inline in line)
            {
                if (inline is Run { Text.Length: > 0 } run && !string.IsNullOrWhiteSpace(run.Text))
                {
                    return TryGetHeadingLevelFromLocalFontSize(run, out headingLevel);
                }

                if (inline is Span span)
                {
                    Inline? firstText = span.Inlines.FirstInline;
                    while (firstText != null)
                    {
                        if (firstText is Run { Text.Length: > 0 } childRun && !string.IsNullOrWhiteSpace(childRun.Text))
                        {
                            if (HasLocalFontSize(childRun))
                            {
                                return TryGetHeadingLevelFromLocalFontSize(childRun, out headingLevel);
                            }

                            return TryGetHeadingLevelFromLocalFontSize(span, out headingLevel);
                        }

                        firstText = firstText.NextInline;
                    }

                    return TryGetHeadingLevelFromLocalFontSize(span, out headingLevel);
                }

                if (inline is Hyperlink hyperlink)
                {
                    Inline? firstText = hyperlink.Inlines.FirstInline;
                    while (firstText != null)
                    {
                        if (firstText is Run { Text.Length: > 0 } linkRun && !string.IsNullOrWhiteSpace(linkRun.Text))
                        {
                            if (HasLocalFontSize(linkRun))
                            {
                                return TryGetHeadingLevelFromLocalFontSize(linkRun, out headingLevel);
                            }

                            return TryGetHeadingLevelFromLocalFontSize(hyperlink, out headingLevel);
                        }

                        firstText = firstText.NextInline;
                    }
                }
            }

            return false;
        }

        private static bool TryGetHeadingLevelFromLocalFontSize(TextElement element, out int headingLevel)
        {
            object value = element.ReadLocalValue(TextElement.FontSizeProperty);
            if (value is double fontSize)
            {
                return TryGetHeadingLevelFromFontSize(fontSize, out headingLevel);
            }

            headingLevel = 0;
            return false;
        }

        private static bool HasLocalFontSize(TextElement element) =>
            element.ReadLocalValue(TextElement.FontSizeProperty) is double;

        private static bool TryGetHeadingLevelFromFontSize(double fontSize, out int headingLevel)
        {
            for (int level = 1; level <= 6; level++)
            {
                if (Math.Abs(fontSize - GetHeadingFontSizeForLevel(level)) < 0.1)
                {
                    headingLevel = level;
                    return true;
                }
            }

            headingLevel = 0;
            return false;
        }

        private static void AppendInlineMarkdown(StringBuilder builder, Inline inline, bool bold, bool italic, bool code, bool underline, bool strikethrough)
        {
            bool isBold = bold || inline is Bold || IsLocalValue(inline, TextElement.FontWeightProperty, FontWeights.Bold);
            bool isItalic = italic || inline is Italic || IsLocalValue(inline, TextElement.FontStyleProperty, FontStyles.Italic);
            bool isCode = code || IsCodeInline(inline);
            bool isUnderline = underline || HasTextDecoration(inline, TextDecorationLocation.Underline);
            bool isStrikethrough = strikethrough || HasTextDecoration(inline, TextDecorationLocation.Strikethrough);

            if (inline is Hyperlink hyperlink)
            {
                var linkBuilder = new StringBuilder();
                foreach (Inline child in hyperlink.Inlines)
                {
                    AppendInlineMarkdown(linkBuilder, child, isBold, isItalic, isCode, false, isStrikethrough);
                }

                string linkUrl = GetHyperlinkUrl(hyperlink);
                if (string.IsNullOrWhiteSpace(linkUrl))
                {
                    builder.Append(linkBuilder);
                }
                else
                {
                    builder.Append('[');
                    builder.Append(linkBuilder);
                    builder.Append("](");
                    builder.Append(EscapeMarkdownText(linkUrl));
                    builder.Append(')');
                }

                return;
            }

            if (inline is Run run)
            {
                AppendStyledText(builder, run.Text, isBold, isItalic, isCode, isUnderline, isStrikethrough);
                return;
            }

            if (inline is LineBreak)
            {
                builder.AppendLine();
                return;
            }

            if (inline is Span span)
            {
                foreach (Inline child in span.Inlines)
                {
                    AppendInlineMarkdown(builder, child, isBold, isItalic, isCode, isUnderline, isStrikethrough);
                }
            }
        }

        private static string GetHyperlinkUrl(Hyperlink hyperlink)
        {
            if (hyperlink.Tag is string tag && tag.StartsWith("link:", StringComparison.Ordinal))
            {
                return tag["link:".Length..];
            }

            return hyperlink.NavigateUri?.ToString() ?? string.Empty;
        }

        private static bool TryGetHeadingLevel(Span span, out int headingLevel)
        {
            headingLevel = 0;
            if (span.Tag is not string tag || !tag.StartsWith(HeadingTagPrefix, StringComparison.Ordinal))
            {
                return false;
            }

            return int.TryParse(tag[HeadingTagPrefix.Length..], out headingLevel) &&
                   headingLevel is >= 1 and <= 6;
        }

        private static string GetListIndent(FlowList list, string? fallbackIndent = null)
        {
            if (list.Tag is string tag && tag.StartsWith("indent:", StringComparison.Ordinal))
            {
                return tag["indent:".Length..];
            }

            return fallbackIndent ?? string.Empty;
        }

        private static void AppendStyledText(StringBuilder builder, string text, bool bold, bool italic, bool code, bool underline, bool strikethrough)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            string escaped = EscapeMarkdownText(text);
            if (underline)
            {
                builder.Append("<u>");
            }

            if (bold)
            {
                builder.Append("**");
            }

            if (italic)
            {
                builder.Append('*');
            }

            if (code)
            {
                builder.Append('`');
            }

            if (strikethrough)
            {
                builder.Append("~~");
            }

            builder.Append(escaped);

            if (strikethrough)
            {
                builder.Append("~~");
            }

            if (code)
            {
                builder.Append('`');
            }

            if (italic)
            {
                builder.Append('*');
            }

            if (bold)
            {
                builder.Append("**");
            }

            if (underline)
            {
                builder.Append("</u>");
            }
        }

        private static string EscapeMarkdownText(string text)
        {
            var builder = new StringBuilder(text.Length);
            foreach (char ch in text)
            {
                if (ch is '\\' or '*' or '`' or '<' or '>' or '~' or '[' or ']' or '(' or ')')
                {
                    builder.Append('\\');
                }

                builder.Append(ch);
            }

            return builder.ToString();
        }

        private static string UnescapeMarkdownText(string text)
        {
            var builder = new StringBuilder(text.Length);
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == '\\' && i + 1 < text.Length)
                {
                    char next = text[i + 1];
                    if (next is '\\' or '*' or '`' or '<' or '>' or '~' or '[' or ']' or '(' or ')' or '/')
                    {
                        builder.Append(next);
                        i++;
                        continue;
                    }
                }

                builder.Append(text[i]);
            }

            return builder.ToString();
        }

        private static bool IsCodeInline(Inline inline) =>
            inline.FontFamily?.Source.Equals("Consolas", StringComparison.OrdinalIgnoreCase) == true ||
            (inline is Span span && span.Tag?.ToString() == "code");

        private static bool HasTextDecoration(Inline inline, TextDecorationLocation location)
        {
            if (inline.TextDecorations?.Any(decoration => decoration.Location == location) == true)
            {
                return true;
            }

            return inline.ReadLocalValue(Inline.TextDecorationsProperty) is TextDecorationCollection localDecorations &&
                   localDecorations.Any(decoration => decoration.Location == location);
        }

        private static bool IsLocalValue(DependencyObject element, DependencyProperty property, object expectedValue)
        {
            object value = element.ReadLocalValue(property);
            return value != DependencyProperty.UnsetValue && Equals(value, expectedValue);
        }

        private static (int Start, int End) GetSelectedLineBounds(string text, int selectionStart, int selectionEnd)
        {
            int start = Math.Clamp(Math.Min(selectionStart, selectionEnd), 0, text.Length);
            int end = Math.Clamp(Math.Max(selectionStart, selectionEnd), 0, text.Length);
            int effectiveEnd = end;
            if (end > start && end > 0 && text[end - 1] == '\n')
            {
                effectiveEnd = end - 1;
            }

            int lineStart = text.LastIndexOf('\n', Math.Max(0, start - 1));
            lineStart = lineStart < 0 ? 0 : lineStart + 1;
            int lineEnd = text.IndexOf('\n', effectiveEnd);
            lineEnd = lineEnd < 0 ? text.Length : lineEnd;
            return (lineStart, lineEnd);
        }

        private static bool LinesHaveListMarker(IEnumerable<string> lines, bool numbered)
        {
            Regex regex = numbered ? NumberMarkerRegex : BulletMarkerRegex;
            bool hasContent = false;
            foreach (string line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                hasContent = true;
                if (!regex.IsMatch(line))
                {
                    return false;
                }
            }

            return hasContent;
        }

        private static (int IndentLength, int MarkerLength) GetListMarker(string line)
        {
            Match match = AnyListMarkerRegex.Match(line);
            if (!match.Success)
            {
                return (line.Length - line.TrimStart().Length, 0);
            }

            int indentLength = match.Groups["indent"].Value.Length;
            return (indentLength, match.Length - indentLength);
        }

        private static (int IndentLength, int MarkerLength) GetHeadingMarker(string line)
        {
            Match match = HeadingMarkerRegex.Match(line);
            if (!match.Success)
            {
                return (line.Length - line.TrimStart().Length, 0);
            }

            int indentLength = match.Groups["indent"].Value.Length;
            return (indentLength, match.Length - indentLength);
        }

        private static IReadOnlyList<(int Offset, string Text)> GetSelectedLines(string text, int selectionStart, int selectionEnd)
        {
            int start = Math.Clamp(Math.Min(selectionStart, selectionEnd), 0, text.Length);
            int end = Math.Clamp(Math.Max(selectionStart, selectionEnd), 0, text.Length);
            var (lineStart, lineEnd) = GetSelectedLineBounds(text, start, end);
            var lines = new List<(int Offset, string Text)>();
            int offset = lineStart;
            while (offset <= lineEnd)
            {
                int nextBreak = text.IndexOf('\n', offset);
                int currentEnd = nextBreak < 0 || nextBreak > lineEnd ? lineEnd : nextBreak;
                lines.Add((offset, text[offset..currentEnd]));
                if (nextBreak < 0 || nextBreak >= lineEnd)
                {
                    break;
                }

                offset = nextBreak + 1;
            }

            return lines;
        }

        private static string NormalizeLineEndings(string text) => text.Replace("\r\n", "\n").Replace('\r', '\n');

        private static string ApplyOperations(string text, IReadOnlyCollection<QuickNoteTextOperation> operations)
        {
            var sortedOperations = operations.OrderByDescending(op => op.Offset).ToList();
            var builder = new StringBuilder(text);

            foreach (var operation in sortedOperations)
            {
                builder.Remove(operation.Offset, operation.RemoveLength);
                builder.Insert(operation.Offset, operation.InsertText);
            }

            return builder.ToString();
        }

        private static int MapOffsetThroughOperations(int offset, IReadOnlyCollection<QuickNoteTextOperation> operations)
        {
            int mapped = offset;
            foreach (var operation in operations.OrderBy(operation => operation.Offset))
            {
                int delta = operation.InsertText.Length - operation.RemoveLength;
                if (operation.Offset < mapped)
                {
                    mapped += delta;
                }
                else if (operation.Offset == mapped && operation.RemoveLength == 0)
                {
                    mapped += operation.InsertText.Length;
                }
            }

            return Math.Max(0, mapped);
        }
    }
}
