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
        public const string CodeBackground = QuickNoteThemeCatalog.CodeBackground;
        public const string CodeHeaderBackground = QuickNoteThemeCatalog.CodeHeaderBackground;
        public const string CodeForeground = QuickNoteThemeCatalog.CodeText;
        public const string CodeBorder = QuickNoteThemeCatalog.CodeBorder;
        public const double CodeContentVerticalPadding = 8;
        public const double QuoteContentVerticalPadding = 8;
        public const double ListMarkerOffset = 6;
        public const double ListIndent = 24;
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
                Background = QuickNoteBrush.FromHex(background),
                Foreground = QuickNoteBrush.FromHex(foreground),
                BorderThickness = new Thickness(1),
                BorderBrush = QuickNoteBrush.FromHex(CodeBorder),
                Margin = new Thickness(0, 6, 0, 6),
                Padding = new Thickness(0, 0, 0, CodeContentVerticalPadding),
                FontFamily = QuickNoteFonts.Code,
                FontSize = 13
            };

            section.Blocks.Add(CreateCodeHeader(theme));

            foreach (string line in lines)
            {
                section.Blocks.Add(new Paragraph(new Run(line))
                {
                    Margin = new Thickness(8, 0, 8, 0),
                    FontFamily = QuickNoteFonts.Code,
                    FontSize = 13,
                    Foreground = QuickNoteBrush.FromHex(foreground),
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
                list.Margin = new Thickness(0);
                list.Padding = new Thickness(ListIndent, 0, 0, 0);
                foreach (ListItem item in list.ListItems)
                {
                    item.Margin = new Thickness(0);
                    item.Padding = new Thickness(0);
                    foreach (Paragraph paragraph in item.Blocks.OfType<Paragraph>())
                    {
                        paragraph.Margin = new Thickness(0);
                        paragraph.Padding = new Thickness(0);
                    }
                }
            }
        }

        private static BlockUIContainer CreateCodeHeader(QuickNoteTheme theme)
        {
            var label = new TextBlock
            {
                Text = "code",
                FontFamily = QuickNoteFonts.Code,
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 0, 0)
            };

            var copy = new Button
            {
                Content = "\uF32C",
                Tag = CodeCopyLink,
                BorderThickness = new Thickness(0),
                OverridesDefaultStyle = true,
                Template = CreateTransparentGlyphButtonTemplate(),
                FontFamily = FontHelper.Resolve(FontHelper.FluentKey),
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
                Height = 20
            };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) });
            Grid.SetColumn(label, 0);
            Grid.SetColumn(copy, 1);
            grid.Children.Add(label);
            grid.Children.Add(copy);

            var header = new BlockUIContainer(grid)
            {
                Tag = QuickNoteTags.CodeHeader,
                Margin = new Thickness(0),
                Padding = new Thickness(0)
            };
            ApplyCodeHeaderTheme(header, theme);
            return header;
        }

        public static void ApplyCodeHeaderTheme(BlockUIContainer header, QuickNoteTheme theme)
        {
            ArgumentNullException.ThrowIfNull(header);
            ArgumentNullException.ThrowIfNull(theme);

            header.Margin = new Thickness(0);
            header.Padding = new Thickness(0, 0, 0, CodeContentVerticalPadding);
            if (header.Child is not Grid grid)
            {
                return;
            }

            grid.Height = 20;
            grid.Background = QuickNoteBrush.FromHex(QuickNoteThemeCatalog.GetCodeHeaderBackground(theme));
            foreach (UIElement child in grid.Children)
            {
                if (child is TextBlock label)
                {
                    label.Foreground = QuickNoteBrush.FromHex(GetCodeText(theme));
                    label.FontFamily = QuickNoteFonts.Code;
                    label.FontSize = 12;
                }
                else if (child is Button button)
                {
                    button.Foreground = QuickNoteBrush.FromHex(GetCodeText(theme));
                    button.Background = Brushes.Transparent;
                    button.BorderThickness = new Thickness(0);
                    button.OverridesDefaultStyle = true;
                    button.FontFamily = FontHelper.Resolve(FontHelper.FluentKey);
                    button.FontSize = 12;
                    button.Width = 24;
                    button.Height = 18;
                }
            }
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

        public static bool IsQuoteBlock(Block block) =>
            block is Section section &&
            (Equals(section.Tag, QuickNoteTags.Quote) ||
             (section.Tag == null &&
              section.BorderThickness.Left > 0 &&
              section.BorderThickness.Top == 0 &&
              section.BorderThickness.Right == 0 &&
              section.BorderThickness.Bottom == 0));

        public static bool IsDividerBlock(Block block) =>
            block is Section section &&
            (Equals(section.Tag, QuickNoteTags.Divider) ||
             (section.Tag == null &&
              section.Blocks.OfType<Paragraph>().Any(static p => p.BorderThickness.Top > 0 && p.Inlines.Count == 0)));

        public static bool IsCodeBlock(Block block) =>
            block is Section section &&
            !IsQuoteBlock(section) &&
            !IsDividerBlock(section) &&
            (Equals(section.Tag, QuickNoteTags.Code) ||
             (section.Tag == null && (
                 string.Equals(section.FontFamily?.Source, QuickNoteFonts.Code.Source, StringComparison.Ordinal) ||
                 section.Blocks.OfType<BlockUIContainer>().Any(IsCodeHeader)
             )));

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

        public static string GetQuoteBlockText(Section section) =>
            string.Join(Environment.NewLine, section.Blocks
                .OfType<Paragraph>()
                .Select(GetQuoteParagraphText)
                .SkipWhile(string.IsNullOrWhiteSpace));

        private static string GetQuoteParagraphText(Paragraph paragraph) =>
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

        public static string GetCodeBackground(QuickNoteTheme theme) => theme?.CodeBackground ?? CodeBackground;

        public static string GetCodeText(QuickNoteTheme theme) => theme?.CodeText ?? CodeForeground;

        public static Section CreateDividerElement(QuickNoteTheme theme)
        {
            var section = new Section
            {
                Tag = QuickNoteTags.Divider,
                Margin = new Thickness(0, 6, 0, 6),
                Padding = new Thickness(0),
                BorderThickness = new Thickness(0)
            };

            var line = new Paragraph
            {
                Margin = new Thickness(0),
                Padding = new Thickness(0),
                BorderBrush = QuickNoteBrush.FromHex(theme?.MutedText ?? "#555559"),
                BorderThickness = new Thickness(0, 1, 0, 0)
            };
            section.Blocks.Add(line);
            return section;
        }

        public static Section CreateQuoteBlockElement(string quoteText, QuickNoteTheme theme)
        {
            string[] lines = QuickNoteDocumentHelper.NormalizeLineEndings(quoteText).Split('\n');
            string accentColor = theme?.Accent ?? "#007ACC";
            string bgColor = theme != null ? QuickNoteThemeCatalog.GetQuoteBackground(theme) : "#22263A";
            string textColor = theme?.Text ?? "#F6F0E6";

            var section = new Section
            {
                Tag = QuickNoteTags.Quote,
                BorderBrush = QuickNoteBrush.FromHex(accentColor),
                BorderThickness = new Thickness(3, 0, 0, 0),
                Background = QuickNoteBrush.FromHex(bgColor),
                Margin = new Thickness(0, 6, 0, 6),
                Padding = new Thickness(0, QuoteContentVerticalPadding, 0, QuoteContentVerticalPadding)
            };

            foreach (string line in lines)
            {
                section.Blocks.Add(new Paragraph(new Run(line))
                {
                    Margin = new Thickness(10, 0, 8, 0),
                    FontFamily = QuickNoteFonts.Default,
                    FontSize = GetHeadingFontSizeForLevel(0),
                    FontStyle = FontStyles.Italic,
                    Foreground = QuickNoteBrush.FromHex(textColor),
                    LineHeight = 20
                });
            }

            return section;
        }

        public static InlineUIContainer CreateTaskCheckbox(bool isChecked, Action<bool>? onToggled, QuickNoteTheme? theme)
        {
            var checkBox = new CheckBox
            {
                IsChecked = isChecked,
                Focusable = false,
                Cursor = System.Windows.Input.Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0),
                Tag = QuickNoteTags.Task(isChecked),
                Template = CreateTaskCheckboxTemplate(theme)
            };

            if (onToggled != null)
            {
                checkBox.Click += (_, _) =>
                {
                    bool state = checkBox.IsChecked == true;
                    checkBox.Tag = QuickNoteTags.Task(state);
                    onToggled(state);
                };
            }

            return new InlineUIContainer(checkBox)
            {
                Tag = QuickNoteTags.Task(isChecked),
                BaselineAlignment = BaselineAlignment.Center
            };
        }

        public static ControlTemplate CreateTaskCheckboxTemplate(QuickNoteTheme? theme)
        {
            var template = new ControlTemplate(typeof(CheckBox));

            var gridFactory = new FrameworkElementFactory(typeof(Grid));
            gridFactory.SetValue(FrameworkElement.WidthProperty, 16.0);
            gridFactory.SetValue(FrameworkElement.HeightProperty, 16.0);
            gridFactory.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            gridFactory.SetValue(FrameworkElement.HorizontalAlignmentProperty, System.Windows.HorizontalAlignment.Center);
            gridFactory.SetValue(UIElement.SnapsToDevicePixelsProperty, true);

            var borderFactory = new FrameworkElementFactory(typeof(Border), "BoxBorder");
            borderFactory.SetValue(FrameworkElement.WidthProperty, 15.0);
            borderFactory.SetValue(FrameworkElement.HeightProperty, 15.0);
            borderFactory.SetValue(FrameworkElement.HorizontalAlignmentProperty, System.Windows.HorizontalAlignment.Center);
            borderFactory.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(3));
            borderFactory.SetValue(Border.BorderThicknessProperty, new Thickness(1.5));
            borderFactory.SetValue(Border.BorderBrushProperty, QuickNoteBrush.FromHex(theme?.MutedText ?? "#74757A"));
            borderFactory.SetValue(Border.BackgroundProperty, Brushes.Transparent);

            var pathFactory = new FrameworkElementFactory(typeof(System.Windows.Shapes.Path), "CheckGlyph");
            pathFactory.SetValue(System.Windows.Shapes.Path.DataProperty, Geometry.Parse("M 0 4 L 3 7 L 9 0"));
            pathFactory.SetValue(FrameworkElement.WidthProperty, 11.0);
            pathFactory.SetValue(FrameworkElement.HeightProperty, 9.0);
            pathFactory.SetValue(System.Windows.Shapes.Path.StretchProperty, Stretch.Uniform);
            pathFactory.SetValue(System.Windows.Shapes.Path.StrokeProperty, Brushes.White);
            pathFactory.SetValue(System.Windows.Shapes.Path.StrokeThicknessProperty, 1.8);
            pathFactory.SetValue(System.Windows.Shapes.Path.StrokeStartLineCapProperty, PenLineCap.Round);
            pathFactory.SetValue(System.Windows.Shapes.Path.StrokeEndLineCapProperty, PenLineCap.Round);
            pathFactory.SetValue(System.Windows.Shapes.Path.StrokeLineJoinProperty, PenLineJoin.Round);
            pathFactory.SetValue(UIElement.VisibilityProperty, Visibility.Collapsed);
            pathFactory.SetValue(FrameworkElement.HorizontalAlignmentProperty, System.Windows.HorizontalAlignment.Center);
            pathFactory.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);

            borderFactory.AppendChild(pathFactory);
            gridFactory.AppendChild(borderFactory);
            template.VisualTree = gridFactory;

            var checkedTrigger = new Trigger { Property = System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty, Value = true };
            checkedTrigger.Setters.Add(new Setter(Border.BackgroundProperty, QuickNoteBrush.FromHex(theme?.Accent ?? "#007ACC"), "BoxBorder"));
            checkedTrigger.Setters.Add(new Setter(Border.BorderBrushProperty, QuickNoteBrush.FromHex(theme?.Accent ?? "#007ACC"), "BoxBorder"));
            checkedTrigger.Setters.Add(new Setter(UIElement.VisibilityProperty, Visibility.Visible, "CheckGlyph"));
            template.Triggers.Add(checkedTrigger);

            var hoverTrigger = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
            hoverTrigger.Setters.Add(new Setter(Border.BorderBrushProperty, QuickNoteBrush.FromHex(theme?.Accent ?? "#007ACC"), "BoxBorder"));
            template.Triggers.Add(hoverTrigger);

            return template;
        }

        public static bool IsTaskParagraph(Paragraph? paragraph, out bool isChecked, out InlineUIContainer? container, out CheckBox? checkBox)
        {
            isChecked = false;
            container = null;
            checkBox = null;

            if (paragraph?.Inlines.FirstInline is InlineUIContainer uiContainer &&
                uiContainer.Child is CheckBox cb)
            {
                // Tags are runtime metadata and XamlPackage may omit them. The first
                // InlineUIContainer hosting a CheckBox is the persisted task schema.
                if (!QuickNoteTags.TryGetTaskState(uiContainer.Tag, out isChecked) &&
                    !QuickNoteTags.TryGetTaskState(cb.Tag, out isChecked))
                {
                    isChecked = cb.IsChecked == true;
                }

                if (cb.IsChecked == true)
                {
                    isChecked = true;
                }
                container = uiContainer;
                checkBox = cb;
                SetTaskParagraph(cb, paragraph);
                return true;
            }

            return false;
        }

        private static readonly DependencyProperty TaskParagraphProperty =
            DependencyProperty.RegisterAttached(
                "TaskParagraph",
                typeof(Paragraph),
                typeof(QuickNoteDocumentFormatting),
                new PropertyMetadata(null));

        public static Paragraph? GetTaskParagraph(CheckBox checkBox) =>
            (Paragraph?)checkBox.GetValue(TaskParagraphProperty);

        private static void SetTaskParagraph(CheckBox checkBox, Paragraph paragraph) =>
            checkBox.SetValue(TaskParagraphProperty, paragraph);

        public static void ApplyInlineTheme(
            InlineCollection inlines,
            Brush normalText,
            Brush codeBackground,
            Brush codeText,
            Brush linkBrush)
        {
            QuickNoteDocumentContract.ApplyInlineTheme(inlines, normalText, codeBackground, codeText, linkBrush);
        }

        public static void ApplyTaskFormattingToParagraph(Paragraph paragraph, bool isChecked, QuickNoteTheme? theme)
        {
            if (paragraph.Inlines.FirstInline is InlineUIContainer container)
            {
                container.Tag = QuickNoteTags.Task(isChecked);
                if (container.Child is CheckBox checkBox)
                {
                    SetTaskParagraph(checkBox, paragraph);
                    checkBox.Tag = QuickNoteTags.Task(isChecked);
                    if (checkBox.IsChecked != isChecked)
                    {
                        checkBox.IsChecked = isChecked;
                    }
                }
            }

            Brush defaultTextBrush = QuickNoteBrush.FromHex(theme?.Text ?? "#F6F0E6");
            Brush mutedTextBrush = QuickNoteBrush.FromHex(theme?.MutedText ?? "#74757A");
            Brush linkBrush = QuickNoteBrush.FromHex(theme?.Link ?? "#4EA6FF");
            QuickNoteTheme safeTheme = theme ?? QuickNoteThemeCatalog.Find(null);
            Brush codeTextBrush = QuickNoteBrush.FromHex(GetCodeText(safeTheme));
            Brush codeBackgroundBrush = QuickNoteBrush.FromHex(GetCodeBackground(safeTheme));

            paragraph.Foreground = isChecked ? mutedTextBrush : defaultTextBrush;
            paragraph.TextDecorations = isChecked || GetIsUserStrikethrough(paragraph)
                ? TextDecorations.Strikethrough
                : null;
            ApplyInlineTheme(paragraph.Inlines, defaultTextBrush, codeBackgroundBrush, codeTextBrush, linkBrush);

            foreach (Inline inline in paragraph.Inlines)
            {
                if (inline is InlineUIContainer)
                {
                    continue;
                }

                ApplyTaskInlineFormatting(inline, isChecked, mutedTextBrush);
            }
        }

        public static bool RemoveTaskCheckbox(Paragraph paragraph, QuickNoteTheme? theme)
        {
            if (!IsTaskParagraph(paragraph, out _, out InlineUIContainer? container, out _))
            {
                return false;
            }

            if (container != null)
            {
                paragraph.Inlines.Remove(container);
            }

            ApplyTaskFormattingToParagraph(paragraph, false, theme);
            return true;
        }

        public static readonly DependencyProperty IsTaskStrikethroughProperty =
            DependencyProperty.RegisterAttached(
                "IsTaskStrikethrough",
                typeof(bool),
                typeof(QuickNoteDocumentFormatting),
                new PropertyMetadata(false));

        public static bool GetIsTaskStrikethrough(Inline inline) => (bool)inline.GetValue(IsTaskStrikethroughProperty);
        public static void SetIsTaskStrikethrough(Inline inline, bool value) => inline.SetValue(IsTaskStrikethroughProperty, value);

        public static readonly DependencyProperty IsUserStrikethroughProperty =
            DependencyProperty.RegisterAttached(
                "IsUserStrikethrough",
                typeof(bool),
                typeof(QuickNoteDocumentFormatting),
                new PropertyMetadata(false));

        public static bool GetIsUserStrikethrough(Inline inline) => (bool)inline.GetValue(IsUserStrikethroughProperty);
        public static void SetIsUserStrikethrough(Inline inline, bool value) => inline.SetValue(IsUserStrikethroughProperty, value);

        public static bool GetIsUserStrikethrough(Paragraph paragraph) => (bool)paragraph.GetValue(IsUserStrikethroughProperty);
        public static void SetIsUserStrikethrough(Paragraph paragraph, bool value) => paragraph.SetValue(IsUserStrikethroughProperty, value);

        public static void MarkUserStrikethroughRecursive(Paragraph paragraph)
        {
            if (paragraph.TextDecorations?.Any(d => d.Location == TextDecorationLocation.Strikethrough) == true)
            {
                SetIsUserStrikethrough(paragraph, true);
                if (paragraph.Tag == null)
                {
                    paragraph.Tag = QuickNoteTags.UserStrikethrough;
                }
            }

            foreach (Inline inline in paragraph.Inlines)
            {
                MarkUserStrikethroughRecursive(inline);
            }
        }

        public static void MarkUserStrikethroughRecursive(Inline inline)
        {
            if (inline.TextDecorations?.Any(d => d.Location == TextDecorationLocation.Strikethrough) == true)
            {
                SetIsUserStrikethrough(inline, true);
            }

            if (inline is Span span)
            {
                foreach (Inline child in span.Inlines)
                {
                    MarkUserStrikethroughRecursive(child);
                }
            }
        }

        public static bool HasUserStrikethrough(Paragraph paragraph) =>
            GetIsUserStrikethrough(paragraph) ||
            paragraph.Inlines.Any(HasUserStrikethrough);

        private static bool HasUserStrikethrough(Inline inline) =>
            GetIsUserStrikethrough(inline) ||
            inline is Span span && span.Inlines.Any(HasUserStrikethrough);

        public static void ToggleTaskParagraph(Paragraph paragraph, Action<bool>? onToggled, QuickNoteTheme? theme)
        {
            if (IsTaskParagraph(paragraph, out bool currentChecked, out InlineUIContainer? container, out CheckBox? checkBox))
            {
                RemoveTaskCheckbox(paragraph, theme);
            }
            else
            {
                MarkUserStrikethroughRecursive(paragraph);

                var newContainer = CreateTaskCheckbox(false, onToggled, theme);
                if (paragraph.Inlines.FirstInline != null)
                {
                    paragraph.Inlines.InsertBefore(paragraph.Inlines.FirstInline, newContainer);
                }
                else
                {
                    paragraph.Inlines.Add(newContainer);
                    paragraph.Inlines.Add(new Run(string.Empty));
                }
                ApplyTaskFormattingToParagraph(paragraph, false, theme);
            }
        }

        private static bool HasAncestorHyperlink(Inline inline)
        {
            DependencyObject? current = inline.Parent;
            while (current != null)
            {
                if (current is Hyperlink) return true;
                if (current is Paragraph or FlowDocument) break;
                current = LogicalTreeHelper.GetParent(current) ?? (current as FrameworkContentElement)?.Parent;
            }
            return false;
        }

        private static void ApplyTaskInlineFormatting(Inline inline, bool isChecked, Brush mutedText)
        {
            if (inline is Hyperlink hyperlink)
            {
                if (isChecked)
                {
                    hyperlink.Foreground = mutedText;
                }
                UpdateStrikethrough(hyperlink, isChecked, isHyperlink: true);
                foreach (Inline child in hyperlink.Inlines)
                {
                    ApplyTaskInlineFormatting(child, isChecked, mutedText);
                }
            }
            else if (inline is Span span)
            {
                if (isChecked)
                {
                    span.Foreground = mutedText;
                }
                UpdateStrikethrough(span, isChecked, isHyperlink: false);
                foreach (Inline child in span.Inlines)
                {
                    ApplyTaskInlineFormatting(child, isChecked, mutedText);
                }
            }
            else if (inline is Run run)
            {
                if (isChecked)
                {
                    run.Foreground = mutedText;
                }
                UpdateStrikethrough(run, isChecked, isHyperlink: HasAncestorHyperlink(run));
            }
        }

        private static void UpdateStrikethrough(Inline inline, bool isChecked, bool isHyperlink)
        {
            TextDecorationCollection decorations = inline.TextDecorations != null ? inline.TextDecorations.Clone() : [];
            bool hasStrikethrough = decorations.Any(d => d.Location == TextDecorationLocation.Strikethrough);

            if (isChecked)
            {
                if (!hasStrikethrough)
                {
                    foreach (var dec in TextDecorations.Strikethrough)
                    {
                        decorations.Add(dec);
                    }
                    inline.TextDecorations = decorations;
                    SetIsTaskStrikethrough(inline, true);
                }
                else if (!GetIsUserStrikethrough(inline))
                {
                    SetIsTaskStrikethrough(inline, true);
                }
            }
            else
            {
                if (GetIsUserStrikethrough(inline))
                {
                    SetIsTaskStrikethrough(inline, false);
                }
                else
                {
                    if (hasStrikethrough)
                    {
                        foreach (var dec in decorations.Where(d => d.Location == TextDecorationLocation.Strikethrough).ToList())
                        {
                            decorations.Remove(dec);
                        }
                    }
                    SetIsTaskStrikethrough(inline, false);
                }

                if (isHyperlink)
                {
                    bool hasUnderline = decorations.Any(d => d.Location == TextDecorationLocation.Underline);
                    if (!hasUnderline)
                    {
                        foreach (var dec in TextDecorations.Underline)
                        {
                            decorations.Add(dec);
                        }
                    }
                    inline.TextDecorations = decorations;
                }
                else
                {
                    inline.TextDecorations = decorations.Count > 0 ? decorations : null;
                }
            }
        }
    }
}
