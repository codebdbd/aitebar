using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media.Animation;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using FlowList = System.Windows.Documents.List;
using Forms = System.Windows.Forms;

namespace AiteBar
{
    [SupportedOSPlatform("windows6.1")]
    public partial class QuickNoteWindow
    {
        private void BuildThemePalette()
        {
            ThemePalette.Children.Clear();
            for (int index = 0; index < QuickNoteThemeCatalog.Themes.Count; index++)
            {
                var theme = QuickNoteThemeCatalog.Themes[index];
                var button = new System.Windows.Controls.Button
                {
                    Width = 42,
                    Height = 48,
                    Margin = new Thickness(0),
                    Background = Brush(QuickNoteThemeCatalog.GetSwatchColor(theme)),
                    Foreground = theme.IsDark ? Brushes.White : Brushes.Black,
                    Content = theme.Id == _theme.Id ? "\uF295" : null,
                    FontFamily = FontHelper.Resolve(FontHelper.FluentKey),
                    FontSize = 12,
                    BorderBrush = Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    FocusVisualStyle = null,
                    Cursor = System.Windows.Input.Cursors.Hand
                };

                CornerRadius corners = index == 0 ? new CornerRadius(8, 0, 0, 8)
                    : index == QuickNoteThemeCatalog.Themes.Count - 1 ? new CornerRadius(0, 8, 8, 0)
                    : new CornerRadius(0);
                button.Template = CreateSwatchTemplate(corners);
                string name = LocalizationService.Get("QuickNote_Theme_" + theme.Id);
                button.ToolTip = name;
                System.Windows.Automation.AutomationProperties.SetName(button, name);
                button.Click += async (_, _) =>
                {
                    _theme = theme;
                    _settingsService.UpdateSettings(s =>
                    {
                        s.QuickNoteThemeId = theme.Id;
                    });
                    ClearCaches();
                    ApplyTheme(theme);
                    BuildThemePalette();
                    ThemePopup.IsOpen = false;
                    await SaveSettingsSafelyAsync();
                };
                ThemePalette.Children.Add(button);
            }
        }

        private static ControlTemplate CreateSwatchTemplate(CornerRadius corners)
        {
            var border = new FrameworkElementFactory(typeof(Border));
            border.Name = "SwatchBorder";
            border.SetValue(Border.CornerRadiusProperty, corners);
            border.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding(nameof(Background)) { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
            border.SetBinding(Border.BorderBrushProperty, new System.Windows.Data.Binding(nameof(BorderBrush)) { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
            border.SetBinding(Border.BorderThicknessProperty, new System.Windows.Data.Binding(nameof(BorderThickness)) { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
            var content = new FrameworkElementFactory(typeof(ContentPresenter));
            content.SetValue(FrameworkElement.HorizontalAlignmentProperty, System.Windows.HorizontalAlignment.Center);
            content.SetValue(FrameworkElement.VerticalAlignmentProperty, System.Windows.VerticalAlignment.Center);
            border.AppendChild(content);
            var template = new ControlTemplate(typeof(System.Windows.Controls.Button)) { VisualTree = border };
            var focus = new MultiTrigger();
            focus.Conditions.Add(new Condition(IsKeyboardFocusedProperty, true));
            focus.Conditions.Add(new Condition(KeyboardFocusVisualService.ShowKeyboardFocusCueProperty, true));
            focus.Setters.Add(new Setter(Border.BorderBrushProperty, new DynamicResourceExtension("QuickNoteFocusBrush"), "SwatchBorder"));
            focus.Setters.Add(new Setter(Border.BorderThicknessProperty, new Thickness(2), "SwatchBorder"));
            template.Triggers.Add(focus);
            return template;
        }

        private void ApplyTheme(QuickNoteTheme theme)
        {
            var background = Brush(theme.Background);
            var border = Brush(theme.Border);
            var text = Brush(theme.Text);
            var muted = Brush(theme.MutedText);
            var accent = Brush(theme.Accent);
            var codeBackground = Brush(QuickNoteDocumentFormatting.GetCodeBackground(theme));
            var codeText = Brush(QuickNoteDocumentFormatting.GetCodeText(theme));
            var link = Brush(theme.Link);

            if (Content is Grid root && root.Children.OfType<Border>().FirstOrDefault() is { } shell)
            {
                shell.Background = background;
                shell.BorderBrush = border;
            }

            TxtNote.Foreground = text;
            TxtNote.CaretBrush = accent;
            TxtSaveStatus.Foreground = muted;
            TxtStats.Foreground = muted;
            HeaderBar.BorderBrush = System.Windows.Media.Brushes.Transparent;
            FooterBar.BorderBrush = System.Windows.Media.Brushes.Transparent;
            HeaderBar.Background = Brush(QuickNoteThemeCatalog.GetHeaderBackground(theme));
            FooterBar.Background = System.Windows.Media.Brushes.Transparent;
            ThemePopupBorder.Background = Brush(theme.Background);
            ThemePopupBorder.BorderBrush = border;
            Resources["QuickNoteHoverBrush"] = Brush(theme.IsDark ? "#303238" : "#14000000");
            Resources["QuickNoteHoverForegroundBrush"] = text;
            Resources["QuickNoteFocusBrush"] = accent;
            Foreground = text;
            Resources["QuickNoteChromeTextBrush"] = text;

            ApplyDocumentStyles(TxtNote.Document, text, codeBackground, codeText, link);
            UpdateStatusAppearance();
        }

        private void ScheduleDocumentStylesUpdate()
        {
            _linkHighlightController?.ScheduleUpdate();
        }

        private void ScheduleDocumentStylesUpdateImmediate()
        {
            var codeBackground = Brush(QuickNoteDocumentFormatting.GetCodeBackground(_theme));
            var codeText = Brush(QuickNoteDocumentFormatting.GetCodeText(_theme));
            var link = Brush(_theme.Link);
            ApplyDocumentStyles(TxtNote.Document, Brush(_theme.Text), codeBackground, codeText, link);
        }

        private void ApplyDocumentStyles(
            FlowDocument document,
            System.Windows.Media.Brush normalText,
            System.Windows.Media.Brush codeBackground,
            System.Windows.Media.Brush codeText,
            System.Windows.Media.Brush linkBrush)
        {
            RunDocumentChangeWithoutAutoSave(() =>
            {
                document.Foreground = normalText;
                foreach (Block block in document.Blocks)
                {
                    ApplyBlockStyles(block, normalText, codeBackground, codeText, linkBrush);
                }
            });
        }

        private void ApplyBlockStyles(
            Block block,
            System.Windows.Media.Brush normalText,
            System.Windows.Media.Brush codeBackground,
            System.Windows.Media.Brush codeText,
            System.Windows.Media.Brush linkBrush)
        {
            if (block is Paragraph paragraph)
            {
                if (QuickNoteDocumentFormatting.IsTaskParagraph(paragraph, out bool isChecked, out InlineUIContainer? container, out CheckBox? checkBox))
                {
                    if (checkBox != null)
                    {
                        checkBox.Template = QuickNoteDocumentFormatting.CreateTaskCheckboxTemplate(_theme);
                    }
                    QuickNoteDocumentFormatting.ApplyTaskFormattingToParagraph(paragraph, isChecked, _theme);
                    return;
                }

                paragraph.Foreground = normalText;
                QuickNoteDocumentFormatting.ApplyInlineTheme(paragraph.Inlines, normalText, codeBackground, codeText, linkBrush);
                return;
            }

            if (block is FlowList list)
            {
                list.Foreground = normalText;
                foreach (ListItem item in list.ListItems)
                {
                    foreach (Block childBlock in item.Blocks)
                    {
                        ApplyBlockStyles(childBlock, normalText, codeBackground, codeText, linkBrush);
                    }
                }
                return;
            }

            if (block is BlockUIContainer uiContainer)
            {
                if (QuickNoteDocumentFormatting.IsCodeHeader(uiContainer))
                {
                    QuickNoteDocumentFormatting.ApplyCodeHeaderTheme(uiContainer, _theme);
                    return;
                }
            }

            if (block is Section section)
            {
                if (QuickNoteDocumentFormatting.IsQuoteBlock(section))
                {
                    ApplyQuoteBlockStyles(section);
                    return;
                }

                if (QuickNoteDocumentFormatting.IsDividerBlock(section))
                {
                    ApplyDividerBlockStyles(section);
                    return;
                }

                if (QuickNoteDocumentFormatting.IsCodeBlock(section))
                {
                    section.Background = codeBackground;
                    section.Foreground = codeText;
                    section.BorderBrush = Brush(QuickNoteDocumentFormatting.CodeBorder);
                    section.FontFamily = QuickNoteFonts.Code;
                    section.FontSize = 13;
                    section.Padding = new Thickness(0, 0, 0, QuickNoteDocumentFormatting.CodeContentVerticalPadding);

                    foreach (Block childBlock in section.Blocks)
                    {
                        ApplyCodeBlockChildStyles(childBlock, codeBackground, codeText, linkBrush);
                    }
                    return;
                }

                foreach (Block childBlock in section.Blocks)
                {
                    ApplyBlockStyles(childBlock, normalText, codeBackground, codeText, linkBrush);
                }
                return;
            }
        }

        private void ApplyQuoteBlockStyles(Section quoteSection)
        {
            string accentColor = _theme.Accent ?? "#007ACC";
            string bgColor = QuickNoteThemeCatalog.GetQuoteBackground(_theme);
            string textColor = _theme.Text ?? "#F6F0E6";

            quoteSection.Tag = QuickNoteTags.Quote;
            quoteSection.BorderBrush = Brush(accentColor);
            quoteSection.BorderThickness = new Thickness(3, 0, 0, 0);
            quoteSection.Background = Brush(bgColor);
            quoteSection.Margin = new Thickness(0, 6, 0, 6);
            quoteSection.Padding = new Thickness(0, QuickNoteDocumentFormatting.QuoteContentVerticalPadding, 0, QuickNoteDocumentFormatting.QuoteContentVerticalPadding);

            foreach (Block childBlock in quoteSection.Blocks)
            {
                if (childBlock is Paragraph paragraph)
                {
                    paragraph.Margin = new Thickness(10, 0, 8, 0);
                    paragraph.FontFamily = QuickNoteFonts.Default;
                    paragraph.FontSize = QuickNoteDocumentFormatting.GetHeadingFontSizeForLevel(0);
                    paragraph.FontStyle = FontStyles.Italic;
                    paragraph.Foreground = Brush(textColor);
                    paragraph.LineHeight = 20;
                    QuickNoteDocumentFormatting.ApplyInlineTheme(
                        paragraph.Inlines,
                        Brush(textColor),
                        Brush(QuickNoteDocumentFormatting.GetCodeBackground(_theme)),
                        Brush(QuickNoteDocumentFormatting.GetCodeText(_theme)),
                        Brush(_theme.Link));
                }
            }
        }

        private void ApplyDividerBlockStyles(Section dividerSection)
        {
            dividerSection.Tag = QuickNoteTags.Divider;
            dividerSection.Background = System.Windows.Media.Brushes.Transparent;
            dividerSection.BorderThickness = new Thickness(0);
            dividerSection.Margin = new Thickness(0, 6, 0, 6);
            dividerSection.Padding = new Thickness(0);

            foreach (Block childBlock in dividerSection.Blocks)
            {
                if (childBlock is Paragraph paragraph)
                {
                    paragraph.Margin = new Thickness(0);
                    paragraph.Padding = new Thickness(0);
                    paragraph.BorderBrush = Brush(_theme.MutedText ?? "#555559");
                    paragraph.BorderThickness = new Thickness(0, 1, 0, 0);
                }
            }
        }

        private void ApplyCodeBlockChildStyles(
            Block block,
            System.Windows.Media.Brush codeBackground,
            System.Windows.Media.Brush codeText,
            System.Windows.Media.Brush linkBrush)
        {
            if (block is BlockUIContainer uiContainer && QuickNoteDocumentFormatting.IsCodeHeader(uiContainer))
            {
                QuickNoteDocumentFormatting.ApplyCodeHeaderTheme(uiContainer, _theme);
                return;
            }

            if (block is Paragraph paragraph)
            {
                paragraph.Foreground = codeText;
                paragraph.FontFamily = QuickNoteFonts.Code;
                paragraph.FontSize = 13;
                QuickNoteDocumentFormatting.ApplyInlineTheme(paragraph.Inlines, codeText, codeBackground, codeText, linkBrush);
            }
        }

        private static Brush Brush(string color)
        {
            return QuickNoteBrush.FromHex(color);
        }

        private static IEnumerable<T> FindVisualChildren<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T typed)
                {
                    yield return typed;
                }

                foreach (var descendant in FindVisualChildren<T>(child))
                {
                    yield return descendant;
                }
            }
        }



        private System.Drawing.Rectangle GetWorkArea(AppSettings? settings = null)
        {
            if (settings != null && HasSavedBounds(settings))
            {
                Forms.Screen? primary = Forms.Screen.PrimaryScreen;
                var workAreas = Forms.Screen.AllScreens
                    .OrderByDescending(screen => ReferenceEquals(screen, primary))
                    .Select(screen => screen.WorkingArea)
                    .ToList();
                System.Drawing.Rectangle selected = QuickNoteLayoutHelper.SelectWorkArea(
                    workAreas,
                    settings.QuickNoteLeft,
                    settings.QuickNoteTop,
                    settings.QuickNoteWidth,
                    settings.QuickNoteHeight);
                if (!selected.IsEmpty)
                {
                    return selected;
                }
            }

            var currentScreen = Forms.Screen.FromHandle(new System.Windows.Interop.WindowInteropHelper(this).Handle);
            return currentScreen?.WorkingArea ?? Forms.Screen.PrimaryScreen?.WorkingArea ?? GetVirtualScreenFallback();
        }

        private static System.Drawing.Rectangle GetVirtualScreenFallback()
        {
            int left = (int)SystemParameters.VirtualScreenLeft;
            int top = (int)SystemParameters.VirtualScreenTop;
            int width = (int)SystemParameters.VirtualScreenWidth;
            int height = (int)SystemParameters.VirtualScreenHeight;
            return new System.Drawing.Rectangle(left, top, width, height);
        }

        private void Window_LocationChanged(object? sender, EventArgs e)
        {
            ScheduleGeometrySave();
        }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            ScheduleGeometrySave();
        }

        private void ScheduleGeometrySave()
        {
            if (!_loaded || !IsVisible)
            {
                return;
            }

            _geometrySaveTimer.Stop();
            _geometrySaveTimer.Start();
        }

        private async Task SaveGeometryNowAsync()
        {
            _geometrySaveTimer.Stop();
            if (!_loaded || double.IsNaN(Left) || double.IsNaN(Top))
            {
                return;
            }

            double width = ActualWidth > 0 ? ActualWidth : Width;
            double height = ActualHeight > 0 ? ActualHeight : Height;
            var bounds = QuickNoteLayoutHelper.ClampBoundsToWorkArea(GetWorkArea(), Left, Top, width, height);
            _settingsService.UpdateSettings(s =>
            {
                s.QuickNoteLeft = bounds.Left;
                s.QuickNoteTop = bounds.Top;
                s.QuickNoteWidth = bounds.Width;
                s.QuickNoteHeight = bounds.Height;
            });
            await SaveSettingsSafelyAsync();
        }

        private async Task SaveSettingsSafelyAsync()
        {
            try
            {
                await _settingsService.SaveAsync();
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
        }

        private bool TryOpenUrlAtMouse(MouseButtonEventArgs e)
        {
            if (TryCopyCodeBlockAtMouse(e.GetPosition(TxtNote)))
            {
                return true;
            }

            (string Link, QuickNoteDocumentFormatting.LinkType Type)? link = FindLinkAtMouse(e.GetPosition(TxtNote));
            if (link == null)
            {
                return false;
            }

            try
            {
                string normalized = QuickNoteDocumentFormatting.NormalizeLinkForOpen(link.Value.Link, link.Value.Type);
                if (!QuickNoteDocumentFormatting.IsSafeLinkForOpen(normalized, link.Value.Type))
                {
                    SetStatus(QuickNoteStatusKind.OpenFailed);
                    return false;
                }
                SuppressAutoDismiss(TimeSpan.FromSeconds(3));
                Process.Start(new ProcessStartInfo(normalized) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
                SetStatus(QuickNoteStatusKind.OpenFailed);
                return false;
            }

            return true;
        }

        private bool TryCopyCodeBlockAtMouse(System.Windows.Point position)
        {
            TextPointer? pointer = TxtNote.GetPositionFromPoint(position, true);
            if (pointer == null || FindHyperlink(pointer) is not { } hyperlink ||
                !string.Equals(QuickNoteDocumentFormatting.GetHyperlinkUrl(hyperlink), QuickNoteDocumentFormatting.CodeCopyLink, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            Section? section = FindAncestorSection(hyperlink);
            if (section == null)
            {
                return false;
            }

            return TryCopyText(QuickNoteDocumentFormatting.GetCodeBlockText(section));
        }

        private void CodeBlockCopyButton_Click(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource is not DependencyObject source ||
                FindAncestorButton(source) is not Button { Tag: string tag } button ||
                !string.Equals(tag, QuickNoteDocumentFormatting.CodeCopyLink, StringComparison.OrdinalIgnoreCase) ||
                FindAncestorSection(button) is not { } section)
            {
                return;
            }

            if (!TryCopyText(QuickNoteDocumentFormatting.GetCodeBlockText(section)))
            {
                return;
            }
            e.Handled = true;

            object originalContent = button.Content;
            button.Content = "\uE73E";
            Dispatcher.BeginInvoke(async () =>
            {
                await Task.Delay(700);
                button.Content = originalContent;
            }, DispatcherPriority.Background);
        }

        internal (string Link, QuickNoteDocumentFormatting.LinkType Type)? FindLinkAtMouse(System.Windows.Point position)
        {
            TextPointer? pointer = TxtNote.GetPositionFromPoint(position, true);
            if (pointer == null)
            {
                return null;
            }

            if (FindHyperlink(pointer) is { } hyperlink)
            {
                string url = QuickNoteDocumentFormatting.GetHyperlinkUrl(hyperlink);
                if (!string.IsNullOrWhiteSpace(url))
                {
                    return (url, QuickNoteDocumentFormatting.LinkType.Url);
                }
            }

            if (!TryGetParagraphTextPosition(pointer, out Paragraph? paragraph, out string paragraphText, out int indexInParagraph))
            {
                return null;
            }

            if (paragraphText.Length > QuickNoteLinkHighlightController.MaxLinkScanParagraphLength)
            {
                SetStatus(QuickNoteStatusKind.LinkHighlightPaused);
                return null;
            }

            if (!_linkHighlightController.Cache.TryGetValue(paragraph!, out var cache) || !string.Equals(cache.Text, paragraphText, StringComparison.Ordinal))
            {
                cache = new LinkMatchCacheEntry(paragraphText, QuickNoteDocumentFormatting.MatchLinks(paragraphText).ToList());
                _linkHighlightController.Cache.AddOrUpdate(paragraph!, cache);
            }

            foreach (var (match, type) in cache.Matches)
            {
                if (indexInParagraph < match.Index || indexInParagraph >= match.Index + match.Length)
                {
                    continue;
                }

                return (match.Value, type);
            }

            return null;
        }

        private static bool TryGetParagraphTextPosition(TextPointer pointer, out Paragraph? paragraph, out string text, out int indexInParagraph)
        {
            paragraph = null;
            text = string.Empty;
            indexInParagraph = 0;

            paragraph = FindAncestorParagraph(pointer.Parent as DependencyObject)
                ?? FindAncestorParagraph(pointer.GetAdjacentElement(LogicalDirection.Forward) as DependencyObject)
                ?? FindAncestorParagraph(pointer.GetAdjacentElement(LogicalDirection.Backward) as DependencyObject);
            if (paragraph == null)
            {
                return false;
            }

            try
            {
                TextPointer boundedPointer = pointer;
                if (boundedPointer.CompareTo(paragraph.ContentStart) < 0)
                {
                    boundedPointer = paragraph.ContentStart;
                }
                else if (boundedPointer.CompareTo(paragraph.ContentEnd) > 0)
                {
                    boundedPointer = paragraph.ContentEnd;
                }

                text = QuickNoteDocumentHelper.NormalizeLineEndings(new TextRange(paragraph.ContentStart, paragraph.ContentEnd).Text);
                indexInParagraph = Math.Clamp(
                    QuickNoteDocumentHelper.NormalizeLineEndings(new TextRange(paragraph.ContentStart, boundedPointer).Text).Length,
                    0,
                    text.Length);
                return true;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        private bool TryCopyText(string text)
        {
            if (_clipboard.TrySetText(text))
            {
                SetStatus(QuickNoteStatusKind.Copied);
                return true;
            }

            SetStatus(QuickNoteStatusKind.CopyFailed);
            return false;
        }

        private static Paragraph? FindAncestorParagraph(DependencyObject? current)
        {
            while (current != null)
            {
                if (current is Paragraph paragraph)
                {
                    return paragraph;
                }

                current = current is FrameworkContentElement contentElement
                    ? contentElement.Parent
                    : LogicalTreeHelper.GetParent(current);
            }

            return null;
        }

        private static Section? FindAncestorSection(DependencyObject? current)
        {
            while (current != null)
            {
                if (current is Section section)
                {
                    return section;
                }

                current = GetParentDependencyObject(current);
            }

            return null;
        }

        private static Button? FindAncestorButton(DependencyObject? current)
        {
            while (current != null)
            {
                if (current is Button button)
                {
                    return button;
                }

                current = GetParentDependencyObject(current);
            }

            return null;
        }

        private static DependencyObject? GetParentDependencyObject(DependencyObject current)
        {
            if (current is FrameworkContentElement contentElement)
            {
                return contentElement.Parent;
            }

            if (current is FrameworkElement { Parent: DependencyObject parent })
            {
                return parent;
            }

            DependencyObject? visualParent = current is Visual
                ? VisualTreeHelper.GetParent(current)
                : null;

            return visualParent ?? LogicalTreeHelper.GetParent(current);
        }

        private static Hyperlink? FindHyperlink(TextPointer pointer)
        {
            DependencyObject? current = pointer.Parent as DependencyObject;
            while (current != null)
            {
                if (current is Hyperlink hyperlink)
                {
                    return hyperlink;
                }

                current = current is FrameworkContentElement contentElement
                    ? contentElement.Parent
                    : LogicalTreeHelper.GetParent(current);
            }

            return null;
        }



        private static bool IsDescendantOf(DependencyObject child, DependencyObject parent)
        {
            DependencyObject? current = child;
            while (current != null)
            {
                if (ReferenceEquals(current, parent))
                {
                    return true;
                }
                DependencyObject? next = current is Visual ? VisualTreeHelper.GetParent(current) : null;
                if (next == null)
                {
                    next = LogicalTreeHelper.GetParent(current);
                }
                current = next;
            }
            return false;
        }
    }
}
