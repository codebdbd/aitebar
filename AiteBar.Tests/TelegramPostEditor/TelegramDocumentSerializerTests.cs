using AiteBar.TelegramPostEditor.Models;
using AiteBar.TelegramPostEditor.Services;

namespace AiteBar.Tests.TelegramPostEditor;

public sealed class TelegramDocumentSerializerTests
{
    [Fact]
    public void SerializeForTelegram_ComputesExactUtf16OffsetsAndEntities()
    {
        var doc = new PostDocument
        {
            IncludePublishTitle = true,
            PublishTitle = "🚀 Старт",
            Blocks =
            [
                new PostBlock
                {
                    Type = PostBlockType.Paragraph,
                    Runs =
                    [
                        new PostTextRun { Text = "Жирный", Formats = PostTextFormats.Bold },
                        new PostTextRun { Text = " и " },
                        new PostTextRun { Text = "спойлер", Formats = PostTextFormats.Spoiler },
                        new PostTextRun { Text = " + " },
                        new PostTextRun { Text = "ссылка", LinkUrl = "https://example.com" }
                    ]
                },
                new PostBlock
                {
                    Type = PostBlockType.Quote,
                    Runs = [new PostTextRun { Text = "Цитата" }]
                }
            ]
        };

        TelegramSerializedContent result = TelegramDocumentSerializer.SerializeForTelegram(doc);

        Assert.StartsWith("🚀 Старт\n\nЖирный и спойлер + ссылка\nЦитата", result.Text);
        Assert.Contains(result.Entities, e => e.Type == "bold" && e.Offset == 0 && e.Length == "🚀 Старт".Length);
        Assert.Contains(result.Entities, e => e.Type == "spoiler" && result.Text.Substring(e.Offset, e.Length) == "спойлер");
        Assert.Contains(result.Entities, e => e.Type == "text_link" && e.Url == "https://example.com" && result.Text.Substring(e.Offset, e.Length) == "ссылка");
        Assert.Contains(result.Entities, e => e.Type == "blockquote" && result.Text.Substring(e.Offset, e.Length) == "Цитата");
    }

    [Fact]
    public void NormalizePastedPlainText_DoesNotDoubleLineBreaks()
    {
        string normalized = TelegramDocumentSerializer.NormalizePastedPlainText("Line 1\r\nLine 2\rLine 3\nLine 4");
        Assert.Equal("Line 1\nLine 2\nLine 3\nLine 4", normalized);
    }

    [Fact]
    public void ParseMarkdownToBlocks_ParsesInlineAndBlockFormattingWithoutRawStars()
    {
        const string markdown = """
            # Заголовок новости

            Привет, **жирный**, *курсив*, __подчерк__, ~~зачеркнутый~~, ||спойлер||, `код` и [ссылка](https://example.com).

            - Первый пункт
            > Важная цитата

            ```cs
            var x = 42;
            ```
            """;

        var blocks = TelegramDocumentSerializer.ParseMarkdownToBlocks(markdown);
        var contentBlocks = blocks.Where(b => b.Runs.Count > 0).ToList();

        Assert.Equal(5, contentBlocks.Count);
        Assert.Equal(PostBlockType.Paragraph, contentBlocks[0].Type);
        Assert.Contains(contentBlocks[0].Runs, r => r.Text == "Заголовок новости" && r.Formats.HasFlag(PostTextFormats.Bold));

        var inlineRuns = contentBlocks[1].Runs;
        Assert.Contains(inlineRuns, r => r.Text == "жирный" && r.Formats.HasFlag(PostTextFormats.Bold));
        Assert.Contains(inlineRuns, r => r.Text == "курсив" && r.Formats.HasFlag(PostTextFormats.Italic));
        Assert.Contains(inlineRuns, r => r.Text == "подчерк" && r.Formats.HasFlag(PostTextFormats.Bold));
        Assert.Contains(inlineRuns, r => r.Text == "зачеркнутый" && r.Formats.HasFlag(PostTextFormats.Strikethrough));
        Assert.Contains(inlineRuns, r => r.Text == "спойлер" && r.Formats.HasFlag(PostTextFormats.Spoiler));
        Assert.Contains(inlineRuns, r => r.Text == "код" && r.Formats.HasFlag(PostTextFormats.Code));
        Assert.Contains(inlineRuns, r => r.Text == "ссылка" && r.LinkUrl == "https://example.com");

        Assert.Equal(PostBlockType.ListItem, contentBlocks[2].Type);
        Assert.Equal("Первый пункт", string.Concat(contentBlocks[2].Runs.Select(r => r.Text)));

        Assert.Equal(PostBlockType.Quote, contentBlocks[3].Type);
        Assert.Equal("Важная цитата", string.Concat(contentBlocks[3].Runs.Select(r => r.Text)));

        Assert.Equal(PostBlockType.CodeBlock, contentBlocks[4].Type);
        Assert.Equal("cs", contentBlocks[4].Language);
        Assert.Equal("var x = 42;", string.Concat(contentBlocks[4].Runs.Select(r => r.Text)));

        var doc = new PostDocument { Blocks = blocks };
        var serialized = TelegramDocumentSerializer.SerializeForTelegram(doc);
        Assert.DoesNotContain("**", serialized.Text);
        Assert.Contains(serialized.Entities, e => e.Type == "bold" && serialized.Text.Substring(e.Offset, e.Length) == "жирный");
        Assert.Contains(serialized.Entities, e => e.Type == "italic" && serialized.Text.Substring(e.Offset, e.Length) == "курсив");
        Assert.Contains(serialized.Entities, e => e.Type == "spoiler" && serialized.Text.Substring(e.Offset, e.Length) == "спойлер");
        Assert.Contains(serialized.Entities, e => e.Type == "strikethrough" && serialized.Text.Substring(e.Offset, e.Length) == "зачеркнутый");
        Assert.Contains(serialized.Entities, e => e.Type == "code" && serialized.Text.Substring(e.Offset, e.Length) == "код");
        Assert.Contains(serialized.Entities, e => e.Type == "text_link" && e.Url == "https://example.com" && serialized.Text.Substring(e.Offset, e.Length) == "ссылка");
        Assert.Contains(serialized.Entities, e => e.Type == "pre" && e.Language == "cs");
    }

    [Fact]
    public void ParseMarkdownToBlocks_ParsesHtmlUnderlineTag()
    {
        var blocks = TelegramDocumentSerializer.ParseMarkdownToBlocks("Текст с <u>подчеркиванием</u> внутри.");

        var block = Assert.Single(blocks);
        Assert.Contains(block.Runs, r => r.Text == "подчеркиванием" && r.Formats.HasFlag(PostTextFormats.Underline));
    }
}
