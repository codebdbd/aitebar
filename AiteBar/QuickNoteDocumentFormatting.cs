using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace AiteBar
{
    internal readonly record struct QuickNoteRangeEdit(int StartOffset, int RemoveLength, string InsertText, int CaretOffset, int SelectionLength);

    [System.Runtime.Versioning.SupportedOSPlatform("windows6.1")]
    internal static class QuickNoteDocumentFormatting
    {
        public const string CodeCopyLink = "aitebar://copy-code/";
        public const string CodeBackground = "#101316";
        public const string CodeHeaderBackground = "#5E666D";
        public const string CodeForeground = "#F4F6F8";
        public const string CodeBorder = "#101316";
        public const double ListMarkerOffset = 28;
        private const RegexOptions LinkRegexOptions = RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.NonBacktracking;
        private static readonly Regex UrlRegex = new(@"\b(?:https?://|www\.)[^\s<>()""']+", LinkRegexOptions);
        private static readonly Regex EmailRegex = new(@"\b[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}\b", LinkRegexOptions);
        private static readonly Regex PhoneRegex = new(@"\b(?:\+?\d{1,3}[-.\s]?)?\(?\d{3}\)?[-.\s]?\d{3}[-.\s]?\d{4}\b", LinkRegexOptions);
        private static readonly Regex VisualListMarkerRegex = new(@"^(?<indent>\s*)(?:[•◦▪]|\d+[.)])\t", RegexOptions.Compiled | RegexOptions.Multiline);

        public enum LinkType
        {
            Url,
            Email,
            Phone
        }

        public static IEnumerable<(Match Match, LinkType Type)> MatchLinks(string text)
        {
            var matches = new List<(Match Match, LinkType Type)>();
            matches.AddRange(UrlRegex.Matches(text).Select(static match => (match, LinkType.Url)));
            matches.AddRange(EmailRegex.Matches(text).Select(static match => (match, LinkType.Email)));
            matches.AddRange(PhoneRegex.Matches(text).Select(static match => (match, LinkType.Phone)));
            return matches.OrderBy(static match => match.Match.Index);
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

        public static bool IsSafeLinkForOpen(string link, LinkType type)
        {
            if (string.IsNullOrWhiteSpace(link))
            {
                return false;
            }

            if (type == LinkType.Url)
            {
                return Uri.TryCreate(link, UriKind.Absolute, out Uri? uri) &&
                       (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
            }

            if (type == LinkType.Email)
            {
                const string prefix = "mailto:";
                string address = link.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ? link[prefix.Length..] : string.Empty;
                Match match = EmailRegex.Match(address);
                return match.Success && match.Index == 0 && match.Length == address.Length;
            }

            const string phonePrefix = "tel:";
            string number = link.StartsWith(phonePrefix, StringComparison.OrdinalIgnoreCase) ? link[phonePrefix.Length..] : string.Empty;
            return number.Count(char.IsDigit) >= 4 && number.All(static character => char.IsDigit(character) || character is '+' or '-' or '.' or '(' or ')' or ' ');
        }

        public static Hyperlink CreateHyperlink(string text, string url) =>
            new(new Run(text)) { NavigateUri = Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) ? uri : null, Tag = url };

        internal static string GetHyperlinkUrl(Hyperlink hyperlink) => hyperlink.Tag as string ?? hyperlink.NavigateUri?.OriginalString ?? string.Empty;

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

        public static QuickNoteRangeEdit GetClearLineMarkerRangeEdit(string text, int selectionStart, int selectionEnd)
        {
            text = QuickNoteDocumentHelper.NormalizeLineEndings(text);
            int start = Math.Clamp(Math.Min(selectionStart, selectionEnd), 0, text.Length);
            int end = Math.Clamp(Math.Max(selectionStart, selectionEnd), start, text.Length);
            int lineStart = text.LastIndexOf('\n', Math.Max(0, start - 1)) + 1;
            int lineEnd = text.IndexOf('\n', end);
            if (lineEnd < 0)
            {
                lineEnd = text.Length;
            }

            string selectedLines = text[lineStart..lineEnd];
            MatchCollection markers = VisualListMarkerRegex.Matches(selectedLines);
            string replacement = VisualListMarkerRegex.Replace(selectedLines, "${indent}");

            int MapOffset(int offset)
            {
                int mapped = offset;
                foreach (Match marker in markers)
                {
                    int markerStart = lineStart + marker.Index + marker.Groups["indent"].Length;
                    int markerLength = marker.Length - marker.Groups["indent"].Length;
                    if (offset > markerStart)
                    {
                        mapped -= Math.Min(offset - markerStart, markerLength);
                    }
                }

                return mapped;
            }

            int mappedStart = MapOffset(start);
            int mappedEnd = MapOffset(end);
            return new QuickNoteRangeEdit(lineStart, selectedLines.Length, replacement, mappedStart, Math.Max(0, mappedEnd - mappedStart));
        }

        public static Section CreateCodeBlockElement(string codeText, QuickNoteTheme theme)
        {
            string background = GetCodeBackground(theme);
            string foreground = GetCodeText(theme);
            string[] lines = QuickNoteDocumentHelper.NormalizeLineEndings(codeText).Split('\n');
            var section = new Section
            {
                Tag = QuickNoteTags.Code,
                Background = Brush(background),
                Foreground = Brush(foreground),
                BorderThickness = new Thickness(1),
                BorderBrush = Brush(CodeBorder),
                Margin = new Thickness(0, 6, 0, 6),
                Padding = new Thickness(0),
                FontFamily = QuickNoteFonts.Code,
                FontSize = 13
            };

            section.Blocks.Add(CreateCodeHeader());

            foreach (string line in lines)
            {
                section.Blocks.Add(new Paragraph(new Run(line))
                {
                    Margin = new Thickness(8, 0, 8, 0),
                    FontFamily = QuickNoteFonts.Code,
                    FontSize = 13,
                    Foreground = Brush(CodeForeground),
                    LineHeight = 18
                });
            }

            return section;
        }

        public static void NormalizeListLayout(FlowDocument document)
        {
            foreach (System.Windows.Documents.List list in EnumerateLists(document.Blocks))
            {
                list.MarkerOffset = ListMarkerOffset;
            }
        }

        private static BlockUIContainer CreateCodeHeader()
        {
            var label = new TextBlock
            {
                Text = "code",
                Foreground = Brush(CodeForeground),
                FontFamily = QuickNoteFonts.Code,
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 0, 0)
            };

            var copy = new Button
            {
                Content = "\uE8C8",
                Tag = CodeCopyLink,
                Foreground = Brushes.White,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                OverridesDefaultStyle = true,
                Template = CreateTransparentGlyphButtonTemplate(),
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 12,
                Width = 24,
                Height = 18,
                Padding = new Thickness(0),
                HorizontalContentAlignment = System.Windows.HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                Cursor = System.Windows.Input.Cursors.Hand,
                ToolTip = LocalizationService.Get("QuickNote_Copy")
            };

            var grid = new Grid
            {
                Background = Brush(CodeHeaderBackground),
                Height = 20
            };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) });
            Grid.SetColumn(label, 0);
            Grid.SetColumn(copy, 1);
            grid.Children.Add(label);
            grid.Children.Add(copy);

            return new BlockUIContainer(grid)
            {
                Tag = QuickNoteTags.CodeHeader,
                Margin = new Thickness(0),
                Padding = new Thickness(0)
            };
        }

        private static ControlTemplate CreateTransparentGlyphButtonTemplate()
        {
            var border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.BackgroundProperty, Brushes.Transparent);

            var content = new FrameworkElementFactory(typeof(ContentPresenter));
            content.SetValue(FrameworkElement.HorizontalAlignmentProperty, System.Windows.HorizontalAlignment.Center);
            content.SetValue(FrameworkElement.VerticalAlignmentProperty, System.Windows.VerticalAlignment.Center);
            border.AppendChild(content);

            return new ControlTemplate(typeof(Button)) { VisualTree = border };
        }

        public static bool IsCodeBlock(Block block) =>
            block is Section section &&
            (Equals(section.Tag, QuickNoteTags.Code) ||
             string.Equals(section.FontFamily?.Source, QuickNoteFonts.Code.Source, StringComparison.Ordinal));

        public static bool IsCodeHeader(Block block) =>
            Equals(block.Tag, QuickNoteTags.CodeHeader);

        public static string GetCodeBlockText(Section section) =>
            string.Join(Environment.NewLine, section.Blocks
                .OfType<Paragraph>()
                .Select(GetCodeParagraphText)
                .SkipWhile(string.IsNullOrWhiteSpace));

        private static string GetCodeParagraphText(Paragraph paragraph) =>
            string.Concat(paragraph.Inlines
                    .Select(static inline => new TextRange(inline.ContentStart, inline.ContentEnd).Text))
                .TrimEnd('\r', '\n');

        private static IEnumerable<System.Windows.Documents.List> EnumerateLists(BlockCollection blocks)
        {
            foreach (Block block in blocks)
            {
                if (block is System.Windows.Documents.List list)
                {
                    yield return list;
                    foreach (ListItem item in list.ListItems)
                    {
                        foreach (System.Windows.Documents.List nestedList in EnumerateLists(item.Blocks))
                        {
                            yield return nestedList;
                        }
                    }
                }
                else if (block is Section section)
                {
                    foreach (System.Windows.Documents.List nestedList in EnumerateLists(section.Blocks))
                    {
                        yield return nestedList;
                    }
                }
            }
        }

        private static SolidColorBrush Brush(string color)
        {
            var brush = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(color));
            brush.Freeze();
            return brush;
        }

        public static string GetCodeBackground(QuickNoteTheme theme) => theme?.CodeBackground ?? CodeBackground;

        public static string GetCodeText(QuickNoteTheme theme) => theme?.CodeText ?? CodeForeground;
    }
}
