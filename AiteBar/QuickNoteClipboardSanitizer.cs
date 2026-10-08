using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Documents;

namespace AiteBar
{
    [System.Runtime.Versioning.SupportedOSPlatform("windows6.1")]
    internal static class QuickNoteClipboardSanitizer
    {
        public static void SanitizeFlowDocument(FlowDocument document)
        {
            if (document == null)
            {
                return;
            }

            document.ClearValue(TextElement.FontFamilyProperty);
            document.ClearValue(TextElement.FontSizeProperty);
            document.ClearValue(TextElement.ForegroundProperty);
            document.ClearValue(TextElement.BackgroundProperty);

            foreach (Block block in document.Blocks)
            {
                SanitizeBlock(block);
            }
        }

        private static void SanitizeBlock(Block block)
        {
            block.ClearValue(TextElement.FontFamilyProperty);
            block.ClearValue(TextElement.ForegroundProperty);
            block.ClearValue(TextElement.BackgroundProperty);

            if (block is Paragraph paragraph)
            {
                // Reset block margins to 0 so pasted lines don't have enormous gaps
                paragraph.Margin = new Thickness(0);
                paragraph.ClearValue(Block.LineHeightProperty);
                paragraph.ClearValue(TextElement.FontSizeProperty);

                foreach (Inline inline in paragraph.Inlines)
                {
                    SanitizeInline(inline);
                }
            }
            else if (block is Section section)
            {
                section.ClearValue(TextElement.FontSizeProperty);
                foreach (Block child in section.Blocks)
                {
                    SanitizeBlock(child);
                }
            }
            else if (block is List list)
            {
                list.ClearValue(TextElement.FontSizeProperty);
                foreach (ListItem item in list.ListItems)
                {
                    item.ClearValue(TextElement.FontSizeProperty);
                    foreach (Block child in item.Blocks)
                    {
                        SanitizeBlock(child);
                    }
                }
            }
        }

        private static void SanitizeInline(Inline inline)
        {
            inline.ClearValue(TextElement.FontFamilyProperty);
            inline.ClearValue(TextElement.FontSizeProperty);
            inline.ClearValue(TextElement.ForegroundProperty);
            inline.ClearValue(TextElement.BackgroundProperty);

            if (inline is Span span)
            {
                foreach (Inline child in span.Inlines)
                {
                    SanitizeInline(child);
                }
            }
        }

        public static bool TrySanitizeRtf(string rtf, out byte[]? sanitizedXamlPackage)
        {
            sanitizedXamlPackage = null;
            if (string.IsNullOrWhiteSpace(rtf))
            {
                return false;
            }

            try
            {
                var doc = new FlowDocument();
                using (var readStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(rtf)))
                {
                    var range = new TextRange(doc.ContentStart, doc.ContentEnd);
                    range.Load(readStream, DataFormats.Rtf);
                }

                SanitizeFlowDocument(doc);

                using var writeStream = new MemoryStream();
                var saveRange = new TextRange(doc.ContentStart, doc.ContentEnd);
                saveRange.Save(writeStream, DataFormats.XamlPackage);
                sanitizedXamlPackage = writeStream.ToArray();
                return true;
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
                return false;
            }
        }
    }
}
