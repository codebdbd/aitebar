using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using MediaFontFamily = System.Windows.Media.FontFamily;

namespace AiteBar
{
    internal readonly record struct QuickNoteTextEdit(string Text, int CaretOffset, int SelectionLength);
    internal readonly record struct QuickNoteTextOperation(int Offset, int RemoveLength, string InsertText);

    internal static class QuickNoteMarkdown
    {
        private static readonly MediaFontFamily DefaultFont = new("Segoe UI");
        private static readonly MediaFontFamily CodeFont = new("Consolas");
        private static readonly Regex UrlRegex = new(@"(?i)\b(?:https?://|www\.)[^\s<>()""']+", RegexOptions.Compiled);
        private static readonly Regex EmailRegex = new(@"(?i)\b[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}\b", RegexOptions.Compiled);
        private static readonly Regex PhoneRegex = new(@"(?i)\b(?:\+?\d{1,3}[-.\s]?)?\(?\d{3}\)?[-.\s]?\d{3}[-.\s]?\d{4}\b", RegexOptions.Compiled);
        private static readonly Regex BulletMarkerRegex = new(@"^\s*[-*]\s+", RegexOptions.Compiled);
        private static readonly Regex NumberMarkerRegex = new(@"^\s*\d+\.\s+", RegexOptions.Compiled);
        private static readonly Regex AnyListMarkerRegex = new(@"^(?<indent>\s*)(?:[-*]\s+|\d+\.\s+)", RegexOptions.Compiled);

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
            var paragraph = CreateParagraph();
            string[] lines = NormalizeLineEndings(markdown).Split('\n');

            for (int i = 0; i < lines.Length; i++)
            {
                if (i > 0)
                {
                    paragraph.Inlines.Add(new LineBreak());
                }

                AddMarkdownInlines(paragraph, lines[i]);
            }

            if (!paragraph.Inlines.Any())
            {
                paragraph.Inlines.Add(CreateRun(string.Empty));
            }

            document.Blocks.Add(paragraph);
        }

        public static string ToMarkdown(FlowDocument document)
        {
            var builder = new StringBuilder();
            bool firstParagraph = true;
            foreach (Block block in document.Blocks)
            {
                if (block is not Paragraph paragraph)
                {
                    continue;
                }

                if (!firstParagraph)
                {
                    builder.AppendLine();
                }

                foreach (Inline inline in paragraph.Inlines)
                {
                    AppendInlineMarkdown(builder, inline, false, false, false, false);
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

        public static QuickNoteTextEdit ClearLineMarkers(string text, int selectionStart, int selectionEnd)
        {
            text = NormalizeLineEndings(text);
            QuickNoteTextOperation[] operations = GetClearMarkerOperations(text, selectionStart, selectionEnd);
            string updatedText = ApplyOperations(text, operations);
            int caret = MapOffsetThroughOperations(Math.Max(selectionStart, selectionEnd), operations);
            return new QuickNoteTextEdit(updatedText, caret, 0);
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

        private static void AddMarkdownInlines(Paragraph paragraph, string line)
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
                    FlushPlain(paragraph, plain);
                    paragraph.Inlines.Add(new Bold(CreateRun(UnescapeMarkdownText(boldText))));
                    index = boldEnd;
                    continue;
                }

                if (TryReadHtmlUnderline(line, index, out string underlineText, out int underlineEnd))
                {
                    FlushPlain(paragraph, plain);
                    paragraph.Inlines.Add(new Span(CreateRun(UnescapeMarkdownText(underlineText)))
                    {
                        TextDecorations = TextDecorations.Underline
                    });
                    index = underlineEnd;
                    continue;
                }

                if (TryReadDelimited(line, index, "`", out string codeText, out int codeEnd))
                {
                    FlushPlain(paragraph, plain);
                    var codeSpan = new Span(CreateRun(UnescapeMarkdownText(codeText), CodeFont))
                    {
                        Tag = "code"
                    };
                    paragraph.Inlines.Add(codeSpan);
                    index = codeEnd;
                    continue;
                }

                if (line[index] == '*' && (index + 1 >= line.Length || line[index + 1] != '*') &&
                    TryReadDelimited(line, index, "*", out string italicText, out int italicEnd))
                {
                    FlushPlain(paragraph, plain);
                    paragraph.Inlines.Add(new Italic(CreateRun(UnescapeMarkdownText(italicText))));
                    index = italicEnd;
                    continue;
                }

                plain.Append(line[index]);
                index++;
            }

            FlushPlain(paragraph, plain);
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

        private static void FlushPlain(Paragraph paragraph, StringBuilder plain)
        {
            if (plain.Length == 0)
            {
                return;
            }

            paragraph.Inlines.Add(CreateRun(plain.ToString()));
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

        private static Run CreateRun(string text, MediaFontFamily? fontFamily = null) =>
            new(text)
            {
                FontFamily = fontFamily ?? DefaultFont,
                FontWeight = FontWeights.Normal,
                FontStyle = FontStyles.Normal,
                TextDecorations = null
            };

        private static void AppendInlineMarkdown(StringBuilder builder, Inline inline, bool bold, bool italic, bool code, bool underline)
        {
            bool isBold = bold || inline is Bold || IsLocalValue(inline, TextElement.FontWeightProperty, FontWeights.Bold);
            bool isItalic = italic || inline is Italic || IsLocalValue(inline, TextElement.FontStyleProperty, FontStyles.Italic);
            bool isCode = code || IsCodeInline(inline);
            bool isUnderline = underline || IsUnderlineInline(inline);

            if (inline is Run run)
            {
                AppendStyledText(builder, run.Text, isBold, isItalic, isCode, isUnderline);
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
                    AppendInlineMarkdown(builder, child, isBold, isItalic, isCode, isUnderline);
                }
            }
        }

        private static void AppendStyledText(StringBuilder builder, string text, bool bold, bool italic, bool code, bool underline)
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

            builder.Append(escaped);

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
                if (ch is '\\' or '*' or '`' or '<' or '>')
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
                    if (next is '\\' or '*' or '`' or '<' or '>')
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

        private static bool IsUnderlineInline(Inline inline) =>
            inline.TextDecorations?.Count > 0 ||
            inline.ReadLocalValue(Inline.TextDecorationsProperty) is TextDecorationCollection { Count: > 0 };

        private static bool IsLocalValue(DependencyObject element, DependencyProperty property, object expectedValue)
        {
            object value = element.ReadLocalValue(property);
            return value != DependencyProperty.UnsetValue && Equals(value, expectedValue);
        }

        private static (int Start, int End) GetSelectedLineBounds(string text, int selectionStart, int selectionEnd)
        {
            int lineStart = text.LastIndexOf('\n', Math.Max(0, selectionStart - 1));
            lineStart = lineStart < 0 ? 0 : lineStart + 1;
            int lineEnd = text.IndexOf('\n', selectionEnd);
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
