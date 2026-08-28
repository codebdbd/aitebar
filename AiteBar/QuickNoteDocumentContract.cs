using System;
using System.IO;
using System.Linq;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Media;

namespace AiteBar
{
    [System.Runtime.Versioning.SupportedOSPlatform("windows6.1")]
    internal static class QuickNoteDocumentContract
    {
        public static Run CloneRunWithText(Run source, string text)
        {
            ArgumentNullException.ThrowIfNull(source);

            var clone = new Run(text);
            CopyTextElementProperties(source, clone);
            return clone;
        }

        public static Hyperlink CloneHyperlinkShell(Hyperlink source)
        {
            ArgumentNullException.ThrowIfNull(source);

            var clone = new Hyperlink
            {
                NavigateUri = source.NavigateUri
            };
            CopyTextElementProperties(source, clone);
            return clone;
        }

        public static Span CloneSpanShell(Span source)
        {
            ArgumentNullException.ThrowIfNull(source);

            Span clone = source switch
            {
                Bold => new Bold(),
                Italic => new Italic(),
                Underline => new Underline(),
                _ => new Span()
            };
            CopyTextElementProperties(source, clone);
            return clone;
        }

        public static Inline CloneInline(Inline source, bool strict = false)
        {
            ArgumentNullException.ThrowIfNull(source);

            if (source is Run run)
            {
                return CloneRunWithText(run, run.Text);
            }

            if (source is Hyperlink hyperlink)
            {
                Hyperlink clone = CloneHyperlinkShell(hyperlink);
                foreach (Inline child in hyperlink.Inlines)
                {
                    clone.Inlines.Add(CloneInline(child, strict));
                }

                return clone;
            }

            if (source is Span span)
            {
                Span clone = CloneSpanShell(span);
                foreach (Inline child in span.Inlines)
                {
                    clone.Inlines.Add(CloneInline(child, strict));
                }

                return clone;
            }

            try
            {
                using var reader = new StringReader(XamlWriter.Save(source));
                return (Inline)XamlReader.Load(System.Xml.XmlReader.Create(reader));
            }
            catch (Exception ex) when (!strict && (ex is InvalidOperationException or XamlParseException or IOException))
            {
                return new Run(new TextRange(source.ContentStart, source.ContentEnd).Text);
            }
        }

        public static void ApplyInlineTheme(
            InlineCollection inlines,
            Brush normalText,
            Brush codeBackground,
            Brush codeText,
            Brush linkBrush)
        {
            foreach (Inline inline in inlines.ToList())
            {
                if (inline is Hyperlink hyperlink)
                {
                    if (string.Equals(QuickNoteDocumentFormatting.GetHyperlinkUrl(hyperlink), QuickNoteDocumentFormatting.CodeCopyLink, StringComparison.OrdinalIgnoreCase))
                    {
                        hyperlink.Foreground = codeText;
                        hyperlink.TextDecorations = null;
                        hyperlink.FontFamily = new FontFamily("Segoe MDL2 Assets");
                    }
                    else
                    {
                        hyperlink.Foreground = linkBrush;
                        TextDecorationCollection decorations = hyperlink.TextDecorations?.Clone() ?? [];
                        if (!decorations.Any(decoration => decoration.Location == TextDecorationLocation.Underline))
                        {
                            foreach (TextDecoration decoration in TextDecorations.Underline)
                            {
                                decorations.Add(decoration);
                            }
                        }

                        hyperlink.TextDecorations = decorations;
                    }

                    ApplyInlineTheme(hyperlink.Inlines, hyperlink.Foreground, codeBackground, codeText, linkBrush);
                }
                else if (inline is Span span)
                {
                    bool isCode = Equals(span.Tag, QuickNoteTags.Code);
                    if (isCode)
                    {
                        span.Background = codeBackground;
                        span.Foreground = codeText;
                        span.FontFamily = QuickNoteFonts.Code;
                    }

                    ApplyInlineTheme(span.Inlines, isCode ? codeText : normalText, codeBackground, codeText, linkBrush);
                }
                else if (inline is Run run)
                {
                    if (normalText == codeText || run.FontFamily?.Source == QuickNoteFonts.CodeFamilyName)
                    {
                        run.ClearValue(TextElement.FontFamilyProperty);
                        run.ClearValue(TextElement.FontSizeProperty);
                        run.Background = codeBackground;
                        run.Foreground = codeText;
                    }
                    else
                    {
                        run.Foreground = normalText;
                    }
                }
            }
        }

        private static void CopyTextElementProperties(Inline source, Inline target)
        {
            target.FontFamily = source.FontFamily;
            target.FontSize = source.FontSize;
            target.FontStyle = source.FontStyle;
            target.FontWeight = source.FontWeight;
            target.FontStretch = source.FontStretch;
            target.Foreground = source.Foreground;
            target.Background = source.Background;
            target.TextDecorations = source.TextDecorations?.Clone();
            target.Tag = source.Tag;
        }
    }
}
