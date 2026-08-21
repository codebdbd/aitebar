using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace AiteBar
{
    internal readonly record struct QuickNoteRangeEdit(int StartOffset, int RemoveLength, string InsertText, int CaretOffset, int SelectionLength);

    [System.Runtime.Versioning.SupportedOSPlatform("windows6.1")]
    internal static class QuickNoteDocumentFormatting
    {
        private const string LightThemeCodeBackground = "#2A2C30";
        private const string LightThemeCodeText = "#F6F0E6";
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
            var section = new Section
            {
                Background = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(background)),
                Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(foreground)),
                BorderThickness = new Thickness(1),
                BorderBrush = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(theme.Border)),
                Margin = new Thickness(0, 6, 0, 6),
                Padding = new Thickness(10),
                FontFamily = QuickNoteFonts.Code,
                FontSize = 13
            };

            var copy = new Hyperlink(new Run(LocalizationService.Get("QuickNote_Copy")))
            {
                NavigateUri = new Uri("aitebar://copy-code"),
                Tag = "aitebar://copy-code",
                Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FFFFFF")),
                Background = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#4A5568")),
                TextDecorations = null
            };
            section.Blocks.Add(new Paragraph(copy)
            {
                TextAlignment = TextAlignment.Right,
                Margin = new Thickness(0, 0, 0, 6),
                FontFamily = QuickNoteFonts.Default,
                FontSize = 11
            });

            foreach (string line in QuickNoteDocumentHelper.NormalizeLineEndings(codeText).Split('\n'))
            {
                section.Blocks.Add(new Paragraph(new Run(line)) { Margin = new Thickness(0) });
            }

            return section;
        }

        public static string GetCodeBackground(QuickNoteTheme theme) => theme.IsDark ? theme.CodeBackground : LightThemeCodeBackground;

        public static string GetCodeText(QuickNoteTheme theme) => theme.IsDark ? theme.CodeText : LightThemeCodeText;
    }
}
