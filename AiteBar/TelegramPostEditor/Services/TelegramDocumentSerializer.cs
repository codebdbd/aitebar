using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using AiteBar.TelegramPostEditor.Models;

namespace AiteBar.TelegramPostEditor.Services;

public sealed record TelegramSerializedContent(
    string Text,
    IReadOnlyList<TelegramMessageEntity> Entities);

public static class TelegramDocumentSerializer
{
    public static readonly DependencyProperty IsSpoilerProperty =
        DependencyProperty.RegisterAttached(
            "IsSpoiler",
            typeof(bool),
            typeof(TelegramDocumentSerializer),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.Inherits));

    public static readonly DependencyProperty IsInlineCodeProperty =
        DependencyProperty.RegisterAttached(
            "IsInlineCode",
            typeof(bool),
            typeof(TelegramDocumentSerializer),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.Inherits));

    public static readonly DependencyProperty BlockTypeProperty =
        DependencyProperty.RegisterAttached(
            "BlockType",
            typeof(PostBlockType),
            typeof(TelegramDocumentSerializer),
            new FrameworkPropertyMetadata(PostBlockType.Paragraph));

    public static readonly DependencyProperty CodeLanguageProperty =
        DependencyProperty.RegisterAttached(
            "CodeLanguage",
            typeof(string),
            typeof(TelegramDocumentSerializer),
            new FrameworkPropertyMetadata(null));

    public static void SetIsSpoiler(DependencyObject element, bool value) => element.SetValue(IsSpoilerProperty, value);
    public static bool GetIsSpoiler(DependencyObject element) => (bool)element.GetValue(IsSpoilerProperty);

    public static void SetIsInlineCode(DependencyObject element, bool value) => element.SetValue(IsInlineCodeProperty, value);
    public static bool GetIsInlineCode(DependencyObject element) => (bool)element.GetValue(IsInlineCodeProperty);

    public static void SetBlockType(DependencyObject element, PostBlockType value) => element.SetValue(BlockTypeProperty, value);
    public static PostBlockType GetBlockType(DependencyObject element) => (PostBlockType)element.GetValue(BlockTypeProperty);

    public static void SetCodeLanguage(DependencyObject element, string? value) => element.SetValue(CodeLanguageProperty, value);
    public static string? GetCodeLanguage(DependencyObject element) => (string?)element.GetValue(CodeLanguageProperty);

    public static string ExtractPlainText(PostDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var sb = new StringBuilder();

        if (document.IncludePublishTitle && !string.IsNullOrWhiteSpace(document.PublishTitle))
        {
            sb.Append(document.PublishTitle.Trim());
            if (document.Blocks.Count > 0)
            {
                sb.Append("\n\n");
            }
        }

        for (int i = 0; i < document.Blocks.Count; i++)
        {
            PostBlock block = document.Blocks[i];
            if (i > 0)
            {
                sb.Append('\n');
            }

            if (block.Type == PostBlockType.ListItem)
            {
                sb.Append("• ");
            }

            foreach (PostTextRun run in block.Runs)
            {
                if (!string.IsNullOrEmpty(run.Text))
                {
                    sb.Append(run.Text);
                }
            }
        }

        return sb.ToString();
    }

    public static List<PostBlock> ParseMarkdownToBlocks(string? markdownText)
    {
        if (string.IsNullOrWhiteSpace(markdownText))
        {
            return [];
        }

        string normalized = NormalizePastedPlainText(markdownText).Trim();
        string[] lines = normalized.Split('\n');
        var blocks = new List<PostBlock>();

        bool inCodeFence = false;
        string? codeLang = null;
        var codeLines = new List<string>();

        foreach (string rawLine in lines)
        {
            string line = rawLine;
            string trimmedStart = line.TrimStart();

            if (!inCodeFence && trimmedStart.StartsWith("```", StringComparison.Ordinal))
            {
                inCodeFence = true;
                codeLang = trimmedStart.Length > 3 ? trimmedStart[3..].Trim() : null;
                if (string.IsNullOrWhiteSpace(codeLang))
                {
                    codeLang = null;
                }
                codeLines.Clear();
                continue;
            }

            if (inCodeFence)
            {
                if (trimmedStart.StartsWith("```", StringComparison.Ordinal))
                {
                    blocks.Add(new PostBlock
                    {
                        Type = PostBlockType.CodeBlock,
                        Language = codeLang,
                        Runs = [new PostTextRun { Text = string.Join('\n', codeLines) }]
                    });
                    inCodeFence = false;
                    codeLang = null;
                    codeLines.Clear();
                }
                else
                {
                    codeLines.Add(line);
                }
                continue;
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                if (blocks.Count > 0 && blocks[^1].Runs.Count > 0)
                {
                    blocks.Add(new PostBlock { Type = PostBlockType.Paragraph, Runs = [] });
                }
                continue;
            }

            PostBlockType blockType = PostBlockType.Paragraph;
            PostTextFormats lineBaseFormat = PostTextFormats.None;

            if (trimmedStart.StartsWith("> ", StringComparison.Ordinal))
            {
                blockType = PostBlockType.Quote;
                line = trimmedStart[2..];
            }
            else if (string.Equals(trimmedStart, ">", StringComparison.Ordinal))
            {
                blockType = PostBlockType.Quote;
                line = string.Empty;
            }
            else if (trimmedStart.StartsWith("- ", StringComparison.Ordinal) ||
                     trimmedStart.StartsWith("* ", StringComparison.Ordinal) ||
                     trimmedStart.StartsWith("• ", StringComparison.Ordinal))
            {
                blockType = PostBlockType.ListItem;
                line = trimmedStart[2..];
            }
            else if (trimmedStart.StartsWith("### ", StringComparison.Ordinal) ||
                     trimmedStart.StartsWith("## ", StringComparison.Ordinal) ||
                     trimmedStart.StartsWith("# ", StringComparison.Ordinal))
            {
                int spaceIdx = trimmedStart.IndexOf(' ');
                line = spaceIdx > 0 ? trimmedStart[(spaceIdx + 1)..].TrimStart() : trimmedStart;
                lineBaseFormat = PostTextFormats.Bold;
            }

            var runs = new List<PostTextRun>();
            ParseInlineMarkdown(line, lineBaseFormat, linkUrl: null, runs);
            MergeAdjacentRuns(runs);

            if (runs.Count == 0 && blockType == PostBlockType.Paragraph)
            {
                continue;
            }

            blocks.Add(new PostBlock
            {
                Type = blockType,
                Runs = runs
            });
        }

        if (inCodeFence && codeLines.Count > 0)
        {
            blocks.Add(new PostBlock
            {
                Type = PostBlockType.CodeBlock,
                Language = codeLang,
                Runs = [new PostTextRun { Text = string.Join('\n', codeLines) }]
            });
        }

        while (blocks.Count > 0 && blocks[^1].Runs.Count == 0 && blocks[^1].Type == PostBlockType.Paragraph)
        {
            blocks.RemoveAt(blocks.Count - 1);
        }

        return blocks;
    }

    private static void ParseInlineMarkdown(
        string text,
        PostTextFormats inheritedFormats,
        string? linkUrl,
        List<PostTextRun> target)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        var plainBuffer = new StringBuilder();
        int i = 0;

        void FlushPlain()
        {
            if (plainBuffer.Length > 0)
            {
                target.Add(new PostTextRun
                {
                    Text = plainBuffer.ToString(),
                    Formats = inheritedFormats,
                    LinkUrl = linkUrl
                });
                plainBuffer.Clear();
            }
        }

        while (i < text.Length)
        {
            // Inline code: `...`
            if (text[i] == '`')
            {
                int close = text.IndexOf('`', i + 1);
                if (close > i + 1)
                {
                    FlushPlain();
                    target.Add(new PostTextRun
                    {
                        Text = text[(i + 1)..close],
                        Formats = inheritedFormats | PostTextFormats.Code,
                        LinkUrl = linkUrl
                    });
                    i = close + 1;
                    continue;
                }
            }

            // Hyperlink: [label](https://...)
            if (text[i] == '[')
            {
                int closeBracket = text.IndexOf(']', i + 1);
                if (closeBracket > i + 1 && closeBracket + 1 < text.Length && text[closeBracket + 1] == '(')
                {
                    int closeParen = text.IndexOf(')', closeBracket + 2);
                    if (closeParen > closeBracket + 2)
                    {
                        string innerLabel = text[(i + 1)..closeBracket];
                        string candidateUrl = text[(closeBracket + 2)..closeParen].Trim();
                        if (Uri.TryCreate(candidateUrl, UriKind.Absolute, out _))
                        {
                            FlushPlain();
                            ParseInlineMarkdown(innerLabel, inheritedFormats, candidateUrl, target);
                            i = closeParen + 1;
                            continue;
                        }
                    }
                }
            }

            // Bold: **...**
            if (i + 1 < text.Length && text[i] == '*' && text[i + 1] == '*')
            {
                int close = text.IndexOf("**", i + 2, StringComparison.Ordinal);
                if (close > i + 2)
                {
                    FlushPlain();
                    ParseInlineMarkdown(text[(i + 2)..close], inheritedFormats | PostTextFormats.Bold, linkUrl, target);
                    i = close + 2;
                    continue;
                }
            }

            // Bold / Underline alt: __...__ -> Bold in Markdown
            if (i + 1 < text.Length && text[i] == '_' && text[i + 1] == '_')
            {
                int close = text.IndexOf("__", i + 2, StringComparison.Ordinal);
                if (close > i + 2)
                {
                    FlushPlain();
                    ParseInlineMarkdown(text[(i + 2)..close], inheritedFormats | PostTextFormats.Bold, linkUrl, target);
                    i = close + 2;
                    continue;
                }
            }

            // Spoiler: ||...||
            if (i + 1 < text.Length && text[i] == '|' && text[i + 1] == '|')
            {
                int close = text.IndexOf("||", i + 2, StringComparison.Ordinal);
                if (close > i + 2)
                {
                    FlushPlain();
                    ParseInlineMarkdown(text[(i + 2)..close], inheritedFormats | PostTextFormats.Spoiler, linkUrl, target);
                    i = close + 2;
                    continue;
                }
            }

            // Strikethrough: ~~...~~
            if (i + 1 < text.Length && text[i] == '~' && text[i + 1] == '~')
            {
                int close = text.IndexOf("~~", i + 2, StringComparison.Ordinal);
                if (close > i + 2)
                {
                    FlushPlain();
                    ParseInlineMarkdown(text[(i + 2)..close], inheritedFormats | PostTextFormats.Strikethrough, linkUrl, target);
                    i = close + 2;
                    continue;
                }
            }

            // Underline HTML: <u>...</u>
            if (text.AsSpan(i).StartsWith("<u>", StringComparison.OrdinalIgnoreCase))
            {
                int close = text.IndexOf("</u>", i + 3, StringComparison.OrdinalIgnoreCase);
                if (close > i + 3)
                {
                    FlushPlain();
                    ParseInlineMarkdown(text[(i + 3)..close], inheritedFormats | PostTextFormats.Underline, linkUrl, target);
                    i = close + 4;
                    continue;
                }
            }

            // Italic: *...*
            if (text[i] == '*' && (i + 1 >= text.Length || text[i + 1] != '*'))
            {
                int close = FindSingleClosingDelimiter(text, i + 1, '*');
                if (close > i + 1)
                {
                    FlushPlain();
                    ParseInlineMarkdown(text[(i + 1)..close], inheritedFormats | PostTextFormats.Italic, linkUrl, target);
                    i = close + 1;
                    continue;
                }
            }

            plainBuffer.Append(text[i]);
            i++;
        }

        FlushPlain();
    }

    private static int FindSingleClosingDelimiter(string text, int startIndex, char delimiter)
    {
        for (int idx = startIndex; idx < text.Length; idx++)
        {
            if (text[idx] != delimiter)
            {
                continue;
            }
            if (idx + 1 < text.Length && text[idx + 1] == delimiter)
            {
                idx++;
                continue;
            }
            return idx;
        }
        return -1;
    }

    public static TelegramSerializedContent SerializeForTelegram(
        PostDocument document,
        ChannelPostOverride? channelOverride = null)
    {
        ArgumentNullException.ThrowIfNull(document);

        List<PostBlock> blocks = channelOverride?.Blocks ?? document.Blocks;
        var sb = new StringBuilder();
        var entities = new List<TelegramMessageEntity>();

        if (document.IncludePublishTitle && !string.IsNullOrWhiteSpace(document.PublishTitle))
        {
            string title = document.PublishTitle.Trim();
            int titleOffset = sb.Length;
            sb.Append(title);
            entities.Add(new TelegramMessageEntity
            {
                Type = "bold",
                Offset = titleOffset,
                Length = title.Length
            });

            if (blocks.Count > 0)
            {
                sb.Append("\n\n");
            }
        }

        for (int i = 0; i < blocks.Count; i++)
        {
            if (i > 0)
            {
                sb.Append('\n');
            }

            PostBlock block = blocks[i];
            int blockStart = sb.Length;

            if (block.Type == PostBlockType.ListItem)
            {
                sb.Append("• ");
            }

            if (block.Type == PostBlockType.CodeBlock)
            {
                foreach (PostTextRun run in block.Runs)
                {
                    if (!string.IsNullOrEmpty(run.Text))
                    {
                        sb.Append(run.Text);
                    }
                }

                int blockLength = sb.Length - blockStart;
                if (blockLength > 0)
                {
                    entities.Add(new TelegramMessageEntity
                    {
                        Type = "pre",
                        Offset = blockStart,
                        Length = blockLength,
                        Language = string.IsNullOrWhiteSpace(block.Language) ? null : block.Language.Trim()
                    });
                }

                continue;
            }

            foreach (PostTextRun run in block.Runs)
            {
                if (string.IsNullOrEmpty(run.Text))
                {
                    continue;
                }

                int runStart = sb.Length;
                sb.Append(run.Text);
                int runLength = run.Text.Length; // C# string.Length is UTF-16 code units, matching Telegram Bot API.

                if (run.Formats.HasFlag(PostTextFormats.Code))
                {
                    entities.Add(new TelegramMessageEntity { Type = "code", Offset = runStart, Length = runLength });
                    continue;
                }

                if (run.Formats.HasFlag(PostTextFormats.Bold))
                {
                    entities.Add(new TelegramMessageEntity { Type = "bold", Offset = runStart, Length = runLength });
                }

                if (run.Formats.HasFlag(PostTextFormats.Italic))
                {
                    entities.Add(new TelegramMessageEntity { Type = "italic", Offset = runStart, Length = runLength });
                }

                if (run.Formats.HasFlag(PostTextFormats.Underline))
                {
                    entities.Add(new TelegramMessageEntity { Type = "underline", Offset = runStart, Length = runLength });
                }

                if (run.Formats.HasFlag(PostTextFormats.Strikethrough))
                {
                    entities.Add(new TelegramMessageEntity { Type = "strikethrough", Offset = runStart, Length = runLength });
                }

                if (run.Formats.HasFlag(PostTextFormats.Spoiler))
                {
                    entities.Add(new TelegramMessageEntity { Type = "spoiler", Offset = runStart, Length = runLength });
                }

                if (!string.IsNullOrWhiteSpace(run.LinkUrl))
                {
                    entities.Add(new TelegramMessageEntity
                    {
                        Type = "text_link",
                        Offset = runStart,
                        Length = runLength,
                        Url = run.LinkUrl.Trim()
                    });
                }
            }

            int totalBlockLength = sb.Length - blockStart;
            if (totalBlockLength > 0 && block.Type is PostBlockType.Quote or PostBlockType.ExpandableQuote)
            {
                entities.Add(new TelegramMessageEntity
                {
                    Type = block.Type == PostBlockType.ExpandableQuote ? "expandable_blockquote" : "blockquote",
                    Offset = blockStart,
                    Length = totalBlockLength
                });
            }
        }

        return new TelegramSerializedContent(sb.ToString(), entities);
    }

    public static FlowDocument ToFlowDocument(PostDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var flow = new FlowDocument
        {
            PagePadding = new Thickness(0)
        };

        if (document.Blocks.Count == 0)
        {
            flow.Blocks.Add(new Paragraph());
            return flow;
        }

        foreach (PostBlock block in document.Blocks)
        {
            var paragraph = new Paragraph
            {
                Margin = new Thickness(0, 0, 0, 4)
            };
            SetBlockType(paragraph, block.Type);
            if (!string.IsNullOrWhiteSpace(block.Language))
            {
                SetCodeLanguage(paragraph, block.Language);
            }

            ApplyBlockVisualStyle(paragraph, block.Type);

            foreach (PostTextRun run in block.Runs)
            {
                if (string.IsNullOrEmpty(run.Text))
                {
                    continue;
                }

                var wpfRun = new Run(run.Text);
                ApplyRunVisualStyle(wpfRun, run.Formats);

                if (!string.IsNullOrWhiteSpace(run.LinkUrl) &&
                    Uri.TryCreate(run.LinkUrl, UriKind.Absolute, out Uri? uri))
                {
                    var link = new Hyperlink(wpfRun)
                    {
                        NavigateUri = uri,
                        ToolTip = uri.ToString()
                    };
                    paragraph.Inlines.Add(link);
                }
                else
                {
                    paragraph.Inlines.Add(wpfRun);
                }
            }

            flow.Blocks.Add(paragraph);
        }

        return flow;
    }

    public static List<PostBlock> FromFlowDocument(FlowDocument flowDocument)
    {
        ArgumentNullException.ThrowIfNull(flowDocument);
        var blocks = new List<PostBlock>();

        foreach (Block block in flowDocument.Blocks)
        {
            if (block is Paragraph paragraph)
            {
                var postBlock = new PostBlock
                {
                    Type = GetBlockType(paragraph),
                    Language = GetCodeLanguage(paragraph)
                };

                CollectInlines(paragraph.Inlines, linkUrl: null, postBlock.Runs);
                MergeAdjacentRuns(postBlock.Runs);
                blocks.Add(postBlock);
            }
        }

        return blocks;
    }

    public static string NormalizePastedPlainText(string rawText)
    {
        if (string.IsNullOrEmpty(rawText))
        {
            return string.Empty;
        }

        // Normalize CRLF/CR to LF without turning single line breaks into double empty lines.
        return rawText.Replace("\r\n", "\n", StringComparison.Ordinal)
                      .Replace('\r', '\n');
    }

    public static string ToMarkdownExport(PostDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var sb = new StringBuilder();

        if (!string.IsNullOrWhiteSpace(document.PublishTitle))
        {
            sb.Append("# ").AppendLine(document.PublishTitle.Trim()).AppendLine();
        }

        for (int i = 0; i < document.Blocks.Count; i++)
        {
            PostBlock block = document.Blocks[i];
            if (block.Type == PostBlockType.CodeBlock)
            {
                sb.Append("```").AppendLine(block.Language ?? string.Empty);
                foreach (PostTextRun run in block.Runs)
                {
                    sb.Append(run.Text);
                }
                sb.AppendLine().AppendLine("```");
                continue;
            }

            if (block.Type is PostBlockType.Quote or PostBlockType.ExpandableQuote)
            {
                sb.Append("> ");
            }
            else if (block.Type == PostBlockType.ListItem)
            {
                sb.Append("- ");
            }

            foreach (PostTextRun run in block.Runs)
            {
                string text = run.Text;
                if (run.Formats.HasFlag(PostTextFormats.Code))
                {
                    text = $"`{text}`";
                }
                else
                {
                    if (run.Formats.HasFlag(PostTextFormats.Bold)) text = $"**{text}**";
                    if (run.Formats.HasFlag(PostTextFormats.Italic)) text = $"*{text}*";
                    if (run.Formats.HasFlag(PostTextFormats.Underline)) text = $"<u>{text}</u>";
                    if (run.Formats.HasFlag(PostTextFormats.Strikethrough)) text = $"~~{text}~~";
                    if (run.Formats.HasFlag(PostTextFormats.Spoiler)) text = $"||{text}||";
                    if (!string.IsNullOrWhiteSpace(run.LinkUrl)) text = $"[{text}]({run.LinkUrl})";
                }
                sb.Append(text);
            }

            if (i < document.Blocks.Count - 1)
            {
                sb.AppendLine();
            }
        }

        return sb.ToString();
    }

    private static void CollectInlines(InlineCollection inlines, string? linkUrl, List<PostTextRun> target)
    {
        foreach (Inline inline in inlines)
        {
            switch (inline)
            {
                case Run run:
                    if (!string.IsNullOrEmpty(run.Text))
                    {
                        target.Add(new PostTextRun
                        {
                            Text = run.Text,
                            Formats = ReadRunFormats(run),
                            LinkUrl = linkUrl
                        });
                    }
                    break;
                case Hyperlink hyperlink:
                    string? url = hyperlink.NavigateUri?.ToString() ?? linkUrl;
                    CollectInlines(hyperlink.Inlines, url, target);
                    break;
                case Span span:
                    CollectInlines(span.Inlines, linkUrl, target);
                    break;
                case LineBreak:
                    target.Add(new PostTextRun { Text = "\n" });
                    break;
            }
        }
    }

    private static PostTextFormats ReadRunFormats(Run run)
    {
        PostTextFormats flags = PostTextFormats.None;
        if (run.FontWeight >= FontWeights.Bold)
        {
            flags |= PostTextFormats.Bold;
        }
        if (run.FontStyle == FontStyles.Italic)
        {
            flags |= PostTextFormats.Italic;
        }
        if (run.TextDecorations != null)
        {
            foreach (TextDecoration dec in run.TextDecorations)
            {
                if (dec.Location == TextDecorationLocation.Underline)
                {
                    flags |= PostTextFormats.Underline;
                }
                else if (dec.Location == TextDecorationLocation.Strikethrough)
                {
                    flags |= PostTextFormats.Strikethrough;
                }
            }
        }
        if (GetIsSpoiler(run))
        {
            flags |= PostTextFormats.Spoiler;
        }
        if (GetIsInlineCode(run))
        {
            flags |= PostTextFormats.Code;
        }
        return flags;
    }

    private static void MergeAdjacentRuns(List<PostTextRun> runs)
    {
        for (int i = runs.Count - 1; i > 0; i--)
        {
            PostTextRun prev = runs[i - 1];
            PostTextRun curr = runs[i];
            if (prev.Formats == curr.Formats &&
                string.Equals(prev.LinkUrl, curr.LinkUrl, StringComparison.Ordinal))
            {
                prev.Text += curr.Text;
                runs.RemoveAt(i);
            }
        }
    }

    private static void ApplyBlockVisualStyle(Paragraph paragraph, PostBlockType blockType)
    {
        switch (blockType)
        {
            case PostBlockType.Quote:
            case PostBlockType.ExpandableQuote:
                paragraph.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x00, 0x7A, 0xCC));
                paragraph.BorderThickness = new Thickness(3, 0, 0, 0);
                paragraph.Padding = new Thickness(8, 2, 0, 2);
                break;
            case PostBlockType.CodeBlock:
                paragraph.FontFamily = new FontFamily("JetBrains Mono, Consolas");
                paragraph.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x1E, 0x1E, 0x20));
                paragraph.Padding = new Thickness(8, 6, 8, 6);
                break;
            case PostBlockType.ListItem:
                paragraph.Margin = new Thickness(12, 0, 0, 4);
                break;
        }
    }

    private static void ApplyRunVisualStyle(Run run, PostTextFormats formats)
    {
        if (formats.HasFlag(PostTextFormats.Bold))
        {
            run.FontWeight = FontWeights.Bold;
        }
        if (formats.HasFlag(PostTextFormats.Italic))
        {
            run.FontStyle = FontStyles.Italic;
        }

        var decorations = new TextDecorationCollection();
        if (formats.HasFlag(PostTextFormats.Underline))
        {
            decorations.Add(TextDecorations.Underline);
        }
        if (formats.HasFlag(PostTextFormats.Strikethrough))
        {
            decorations.Add(TextDecorations.Strikethrough);
        }
        if (decorations.Count > 0)
        {
            run.TextDecorations = decorations;
        }

        if (formats.HasFlag(PostTextFormats.Spoiler))
        {
            SetIsSpoiler(run, true);
            run.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x3A, 0x3A, 0x42));
        }

        if (formats.HasFlag(PostTextFormats.Code))
        {
            SetIsInlineCode(run, true);
            run.FontFamily = new FontFamily("JetBrains Mono, Consolas");
            run.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x2D, 0x2D, 0x30));
        }
    }
}
