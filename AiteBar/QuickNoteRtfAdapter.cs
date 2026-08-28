using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Documents;
using System.Windows.Markup;

namespace AiteBar
{
    [System.Runtime.Versioning.SupportedOSPlatform("windows6.1")]
    internal static class QuickNoteRtfAdapter
    {
        private const string CodeFenceStart = "\uE000AiteBar:code:v2:start\uE001";
        private const string CodeFenceEnd = "\uE000AiteBar:code:v2:end\uE001";
        private const string LegacyCodeFenceStart = "```code";
        private const string LegacyCodeFenceEnd = "```";

        public static FlowDocument CreatePackageDocument(FlowDocument source)
        {
            var export = new FlowDocument { PagePadding = source.PagePadding, FontFamily = source.FontFamily, FontSize = source.FontSize };
            foreach (Block block in source.Blocks) export.Blocks.Add(ClonePackageBlock(block));
            return export;
        }

        private static Block ClonePackageBlock(Block source)
        {
            if (source is Paragraph paragraph)
            {
                if (QuickNoteDocumentFormatting.IsTaskParagraph(paragraph, out bool isChecked, out _, out _))
                {
                    var projection = new FlowDocument();
                    AddTaskParagraph(projection, paragraph, isChecked, convertImagesToMarkers: false);
                    Block task = projection.Blocks.FirstBlock!;
                    projection.Blocks.Remove(task);
                    return task;
                }
                var clone = CreateParagraphShell(paragraph);
                foreach (Inline inline in paragraph.Inlines) clone.Inlines.Add(QuickNoteDocumentContract.CloneInline(inline, strict: true));
                return clone;
            }
            if (source is Section section)
            {
                var clone = new Section();
                CopyLocalValues(section, clone);
                foreach (Block block in section.Blocks) clone.Blocks.Add(ClonePackageBlock(block));
                return clone;
            }
            if (source is System.Windows.Documents.List list)
            {
                var clone = new System.Windows.Documents.List();
                CopyLocalValues(list, clone);
                foreach (ListItem item in list.ListItems)
                {
                    var itemClone = new ListItem();
                    CopyLocalValues(item, itemClone);
                    foreach (Block block in item.Blocks) itemClone.Blocks.Add(ClonePackageBlock(block));
                    clone.ListItems.Add(itemClone);
                }
                return clone;
            }
            if (source is Table table)
            {
                var clone = new Table();
                CopyLocalValues(table, clone);
                foreach (TableColumn column in table.Columns)
                {
                    var columnClone = new TableColumn();
                    CopyLocalValues(column, columnClone);
                    clone.Columns.Add(columnClone);
                }
                foreach (TableRowGroup group in table.RowGroups)
                {
                    var groupClone = new TableRowGroup();
                    CopyLocalValues(group, groupClone);
                    foreach (TableRow row in group.Rows)
                    {
                        var rowClone = new TableRow();
                        CopyLocalValues(row, rowClone);
                        foreach (TableCell cell in row.Cells)
                        {
                            var cellClone = new TableCell();
                            CopyLocalValues(cell, cellClone);
                            foreach (Block block in cell.Blocks) cellClone.Blocks.Add(ClonePackageBlock(block));
                            rowClone.Cells.Add(cellClone);
                        }
                        groupClone.Rows.Add(rowClone);
                    }
                    clone.RowGroups.Add(groupClone);
                }
                return clone;
            }
            // Runtime headers and other native blocks retain their structure. Never silently
            // replace a failed primary-format serialization with plain text.
            using var reader = new StringReader(XamlWriter.Save(source));
            return (Block)XamlReader.Load(System.Xml.XmlReader.Create(reader));
        }

        private static void CopyLocalValues(System.Windows.DependencyObject source, System.Windows.DependencyObject target)
        {
            var values = source.GetLocalValueEnumerator();
            while (values.MoveNext())
            {
                var property = values.Current.Property;
                if (property.ReadOnly) continue;
                object value = source.GetValue(property);
                target.SetValue(property, value is System.Windows.Freezable freezable ? freezable.CloneCurrentValue() : value);
            }
        }
        public static FlowDocument CreateExportDocument(
            FlowDocument source,
            bool convertImagesToMarkers = true,
            bool convertCodeBlocksToFences = true)
        {
            var exportDocument = new FlowDocument
            {
                PagePadding = source.PagePadding,
                FontFamily = source.FontFamily,
                FontSize = source.FontSize
            };

            foreach (Block block in source.Blocks)
            {
                if (convertCodeBlocksToFences && QuickNoteDocumentFormatting.IsCodeBlock(block))
                {
                    AddCodeFence(exportDocument, (Section)block);
                    continue;
                }

                if (convertCodeBlocksToFences && QuickNoteDocumentFormatting.IsCodeHeader(block))
                {
                    continue;
                }

                if (block is Paragraph taskParagraph && QuickNoteDocumentFormatting.IsTaskParagraph(taskParagraph, out bool isChecked, out _, out _))
                {
                    AddTaskParagraph(exportDocument, taskParagraph, isChecked, convertImagesToMarkers);
                    continue;
                }

                if (convertImagesToMarkers && block is Paragraph paragraph && AddParagraphWithEmbeddedImages(exportDocument, paragraph))
                {
                    continue;
                }

                exportDocument.Blocks.Add(CloneBlockOrPlainText(block));
            }

            if (exportDocument.Blocks.Count == 0)
            {
                exportDocument.Blocks.Add(new Paragraph(new Run(string.Empty)));
            }

            return exportDocument;
        }

        private static void AddTaskParagraph(FlowDocument document, Paragraph source, bool isChecked, bool convertImagesToMarkers)
        {
            var exportParagraph = CreateParagraphShell(source);
            // The task prefix restores generated strike; retain an explicitly user-authored paragraph strike.
            exportParagraph.TextDecorations = QuickNoteDocumentFormatting.GetIsUserStrikethrough(source)
                ? source.TextDecorations?.Clone()
                : null;
            exportParagraph.Inlines.Add(new Run(isChecked ? "[x] " : "[ ] "));

            var imageMarkers = new List<string>();
            foreach (Inline inline in source.Inlines.Skip(1))
            {
                if (convertImagesToMarkers && inline is InlineUIContainer container &&
                    QuickNoteImageHelper.TryGetMarker(container, out string marker, out _))
                {
                    imageMarkers.Add(marker);
                    continue;
                }

                exportParagraph.Inlines.Add(CloneTaskInlineForExport(inline, strict: !convertImagesToMarkers));
            }

            if (exportParagraph.Inlines.Count == 1)
            {
                exportParagraph.Inlines.Add(new Run(string.Empty));
            }

            document.Blocks.Add(exportParagraph);
            foreach (string marker in imageMarkers)
            {
                document.Blocks.Add(CreateImageMarkerParagraph(marker));
            }
        }

        public static void RestoreCodeBlocksFromFences(FlowDocument document)
        {
            List<Block> blocks = document.Blocks.ToList();
            document.Blocks.Clear();

            for (int index = 0; index < blocks.Count; index++)
            {
                if (blocks[index] is Paragraph paragraph && TryGetCodeFenceEnd(paragraph, out string? endMarker))
                {
                    var codeLines = new List<string>();
                    int cursor = index + 1;

                    while (cursor < blocks.Count)
                    {
                        if (blocks[cursor] is Paragraph endParagraph && IsCodeFenceEnd(endParagraph, endMarker))
                        {
                            break;
                        }

                        codeLines.Add(GetParagraphText(blocks[cursor]).TrimEnd('\r', '\n'));
                        cursor++;
                    }

                    if (cursor < blocks.Count)
                    {
                        document.Blocks.Add(QuickNoteDocumentFormatting.CreateCodeBlockElement(
                            string.Join(Environment.NewLine, codeLines),
                            QuickNoteThemeCatalog.Find(null)));
                        index = cursor;
                        continue;
                    }
                }

                document.Blocks.Add(blocks[index]);
            }

            if (document.Blocks.Count == 0)
            {
                document.Blocks.Add(new Paragraph(new Run(string.Empty)));
            }
        }

        public static void RestoreEmbeddedImages(FlowDocument document)
        {
            int totalPayloadBytes = 0;
            List<Block> blocks = document.Blocks.ToList();
            document.Blocks.Clear();

            foreach (Block block in blocks)
            {
                if (block is Paragraph paragraph &&
                    QuickNoteImageHelper.TryCreateInlineImageFromMarker(GetParagraphText(paragraph).Trim(), ref totalPayloadBytes, out InlineUIContainer? image))
                {
                    document.Blocks.Add(new Paragraph(image));
                }
                else
                {
                    document.Blocks.Add(block);
                }
            }

            if (document.Blocks.Count == 0)
            {
                document.Blocks.Add(new Paragraph(new Run(string.Empty)));
            }
        }

        public static void NormalizeCodeBlocks(FlowDocument document)
        {
            List<Block> blocks = document.Blocks.ToList();
            document.Blocks.Clear();

            foreach (Block block in blocks)
            {
                if (block is Section section && IsPersistedCodeBlock(section))
                {
                    document.Blocks.Add(QuickNoteDocumentFormatting.CreateCodeBlockElement(
                        QuickNoteDocumentFormatting.GetCodeBlockText(section),
                        QuickNoteThemeCatalog.Find(null)));
                }
                else
                {
                    document.Blocks.Add(block);
                }
            }
        }

        private static bool IsPersistedCodeBlock(Section section)
        {
            if (QuickNoteDocumentFormatting.IsCodeBlock(section))
            {
                return true;
            }

            // XamlPackage can omit runtime-only Tag values. The serialized header is a
            // stable marker that lets us restore the interactive code block on reload.
            return section.Blocks.OfType<BlockUIContainer>().Any(static container =>
                container.Child is System.Windows.Controls.Grid grid &&
                grid.Children.OfType<System.Windows.Controls.TextBlock>().Any(static text =>
                    string.Equals(text.Text, "code", StringComparison.Ordinal)));
        }

        private static bool AddParagraphWithEmbeddedImages(FlowDocument document, Paragraph paragraph)
        {
            Inline[] inlines = paragraph.Inlines.ToArray();
            if (!QuickNoteImageHelper.EnumerateImageContainers(paragraph.Inlines).Any())
            {
                return false;
            }

            Paragraph? segment = null;
            foreach (Inline inline in inlines)
            {
                foreach (Inline part in SplitInlineAtImages(inline))
                {
                    if (part is InlineUIContainer image && QuickNoteImageHelper.TryGetMarker(image, out string marker, out _))
                    {
                        AddParagraphSegment(document, segment);
                        document.Blocks.Add(CreateImageMarkerParagraph(marker));
                        segment = null;
                        continue;
                    }

                    segment ??= CreateParagraphShell(paragraph);
                    segment.Inlines.Add(part);
                }
            }

            AddParagraphSegment(document, segment);
            return true;
        }

        private static IEnumerable<Inline> SplitInlineAtImages(Inline inline)
        {
            if (inline is InlineUIContainer)
            {
                yield return inline;
                yield break;
            }

            if (inline is not Span span)
            {
                yield return CloneInlineOrPlainText(inline);
                yield break;
            }

            Span? segment = null;
            foreach (Inline child in span.Inlines)
            {
                foreach (Inline part in SplitInlineAtImages(child))
                {
                    if (part is InlineUIContainer)
                    {
                        if (segment?.Inlines.Count > 0)
                        {
                            yield return segment;
                        }

                        yield return part;
                        segment = null;
                    }
                    else
                    {
                        segment ??= CreateInlineShell(span);
                        segment.Inlines.Add(part);
                    }
                }
            }

            if (segment?.Inlines.Count > 0)
            {
                yield return segment;
            }
        }

        private static Paragraph CreateImageMarkerParagraph(string marker) =>
            new(new Run(marker))
            {
                FontSize = 1,
                Foreground = System.Windows.Media.Brushes.Transparent
            };

        private static Span CreateInlineShell(Span source)
        {
            if (CloneInlineOrPlainText(source) is Span clone)
            {
                clone.Inlines.Clear();
                return clone;
            }

            return new Span();
        }

        private static void AddParagraphSegment(FlowDocument document, Paragraph? paragraph)
        {
            if (paragraph == null || paragraph.Inlines.Count == 0)
            {
                return;
            }

            document.Blocks.Add(paragraph);
        }

        private static Paragraph CreateParagraphShell(Paragraph source)
        {
            var paragraph = new Paragraph();
            CopyLocalValues(source, paragraph);
            paragraph.FontFamily = source.FontFamily;
            paragraph.FontSize = source.FontSize;
            paragraph.FontWeight = source.FontWeight;
            paragraph.FontStyle = source.FontStyle;
            paragraph.FontStretch = source.FontStretch;
            paragraph.Foreground = source.Foreground;
            paragraph.Background = source.Background;
            paragraph.Margin = source.Margin;
            paragraph.Padding = source.Padding;
            paragraph.TextAlignment = source.TextAlignment;
            paragraph.TextDecorations = source.TextDecorations?.Clone();
            return paragraph;
        }

        private static Inline CloneInlineOrPlainText(Inline inline)
        {
            return QuickNoteDocumentContract.CloneInline(inline);
        }

        private static Inline CloneTaskInlineForExport(Inline inline, bool strict)
        {
            Inline cloned = QuickNoteDocumentContract.CloneInline(inline, strict);
            StripTaskStrikethroughRecursive(inline, cloned);
            return cloned;
        }

        private static void StripTaskStrikethroughRecursive(Inline source, Inline target)
        {
            if (QuickNoteDocumentFormatting.GetIsTaskStrikethrough(source) && !QuickNoteDocumentFormatting.GetIsUserStrikethrough(source))
            {
                if (target.TextDecorations != null && target.TextDecorations.Any(d => d.Location == TextDecorationLocation.Strikethrough))
                {
                    var decorations = target.TextDecorations.Clone();
                    foreach (var d in decorations.Where(d => d.Location == TextDecorationLocation.Strikethrough).ToList())
                    {
                        decorations.Remove(d);
                    }
                    target.TextDecorations = decorations.Count > 0 ? decorations : null;
                }
            }

            if (source is Span sourceSpan && target is Span targetSpan)
            {
                var sourceChildren = sourceSpan.Inlines.ToList();
                var targetChildren = targetSpan.Inlines.ToList();
                for (int i = 0; i < Math.Min(sourceChildren.Count, targetChildren.Count); i++)
                {
                    StripTaskStrikethroughRecursive(sourceChildren[i], targetChildren[i]);
                }
            }
        }

        private static void AddCodeFence(FlowDocument document, Section section)
        {
            document.Blocks.Add(CreateCodeParagraph(CodeFenceStart));

            string code = QuickNoteDocumentFormatting.GetCodeBlockText(section);
            foreach (string line in code.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
            {
                document.Blocks.Add(CreateCodeParagraph(line));
            }

            document.Blocks.Add(CreateCodeParagraph(CodeFenceEnd));
        }

        private static Paragraph CreateCodeParagraph(string text) =>
            new(new Run(text))
            {
                FontFamily = QuickNoteFonts.Code,
                FontSize = 13,
                LineHeight = 18,
                Margin = new System.Windows.Thickness(0)
            };

        private static Block CloneBlockOrPlainText(Block block)
        {
            try
            {
                using var reader = new StringReader(XamlWriter.Save(block));
                return (Block)XamlReader.Load(System.Xml.XmlReader.Create(reader));
            }
            catch (Exception ex) when (ex is InvalidOperationException or XamlParseException or IOException)
            {
                return new Paragraph(new Run(GetParagraphText(block)));
            }
        }

        private static bool TryGetCodeFenceEnd(Paragraph paragraph, out string? endMarker)
        {
            string text = GetParagraphText(paragraph).Trim();
            if (string.Equals(text, CodeFenceStart, StringComparison.Ordinal))
            {
                endMarker = CodeFenceEnd;
                return true;
            }

            // The first RTF implementation used Markdown fences. Restrict legacy recognition to
            // its generated code font so ordinary Markdown notes remain plain text.
            if (string.Equals(text, LegacyCodeFenceStart, StringComparison.Ordinal) &&
                HasCodeFont(paragraph))
            {
                endMarker = LegacyCodeFenceEnd;
                return true;
            }

            endMarker = null;
            return false;
        }

        private static bool HasCodeFont(Paragraph paragraph)
        {
            string? source = paragraph.FontFamily?.Source;
            return string.Equals(source, QuickNoteFonts.Code.Source, StringComparison.Ordinal) ||
                   string.Equals(source, QuickNoteFonts.CodeFamilyName, StringComparison.OrdinalIgnoreCase) ||
                   source?.EndsWith($"#{QuickNoteFonts.CodeFamilyName}", StringComparison.OrdinalIgnoreCase) == true;
        }

        private static bool IsCodeFenceEnd(Paragraph paragraph, string? endMarker) =>
            endMarker != null &&
            string.Equals(GetParagraphText(paragraph).Trim(), endMarker, StringComparison.Ordinal);

        private static string GetParagraphText(Block block) =>
            new TextRange(block.ContentStart, block.ContentEnd).Text.TrimEnd('\r', '\n');

        public static void RestoreTaskItems(FlowDocument document, QuickNoteTheme? theme = null)
        {
            theme ??= QuickNoteThemeCatalog.Find(null);
            foreach (Paragraph paragraph in QuickNoteTaskListController.EnumerateAllParagraphs(document).ToArray())
            {
                RestoreTaskItemInParagraph(paragraph, theme);
            }
        }

        private static void RestoreTaskItemInParagraph(Paragraph paragraph, QuickNoteTheme theme)
        {
            if (QuickNoteDocumentFormatting.IsTaskParagraph(paragraph, out bool isChecked, out InlineUIContainer? container, out CheckBox? checkBox))
            {
                // XamlPackage retains text decorations but not their runtime provenance.
                // Preserve an existing decoration rather than silently removing user-authored text styling.
                QuickNoteDocumentFormatting.MarkUserStrikethroughRecursive(paragraph);

                if (checkBox != null)
                {
                    checkBox.Template = QuickNoteDocumentFormatting.CreateTaskCheckboxTemplate(theme);
                    checkBox.Tag = QuickNoteTags.Task(isChecked);
                }
                if (container != null)
                {
                    container.Tag = QuickNoteTags.Task(isChecked);
                }
                QuickNoteDocumentFormatting.ApplyTaskFormattingToParagraph(paragraph, isChecked, theme);
                return;
            }

            if (paragraph.Inlines.FirstInline is Run firstRun)
            {
                string text = firstRun.Text;
                if (TryExtractTaskPrefix(text, out bool checkedState, out string remainingText))
                {
                    firstRun.Text = remainingText;
                    QuickNoteDocumentFormatting.MarkUserStrikethroughRecursive(paragraph);
                    var newContainer = QuickNoteDocumentFormatting.CreateTaskCheckbox(checkedState, null, theme);
                    paragraph.Inlines.InsertBefore(firstRun, newContainer);
                    if (string.IsNullOrEmpty(remainingText) && firstRun.NextInline != null)
                    {
                        paragraph.Inlines.Remove(firstRun);
                    }
                    QuickNoteDocumentFormatting.ApplyTaskFormattingToParagraph(paragraph, checkedState, theme);
                }
            }
        }

        private static bool TryExtractTaskPrefix(string text, out bool isChecked, out string remaining)
        {
            isChecked = false;
            remaining = text;

            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            if (text.StartsWith("[ ] ", StringComparison.Ordinal))
            {
                isChecked = false;
                remaining = text[4..];
                return true;
            }

            if (text.StartsWith("[x] ", StringComparison.OrdinalIgnoreCase) ||
                text.StartsWith("[X] ", StringComparison.Ordinal))
            {
                isChecked = true;
                remaining = text[4..];
                return true;
            }

            if (text.Equals("[ ]", StringComparison.Ordinal))
            {
                isChecked = false;
                remaining = string.Empty;
                return true;
            }

            if (text.Equals("[x]", StringComparison.OrdinalIgnoreCase) ||
                text.Equals("[X]", StringComparison.Ordinal))
            {
                isChecked = true;
                remaining = string.Empty;
                return true;
            }

            return false;
        }
    }
}
