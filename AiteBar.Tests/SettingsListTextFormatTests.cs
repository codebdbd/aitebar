using System.IO;

namespace AiteBar.Tests;

public sealed class SettingsListTextFormatTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PromptRoundTripPreservesMultilineContent(bool markdown)
    {
        string[] values = ["Дайджест | IT", "  Первая строка\r\nВторая\\строка | пример\tтекст  ", "Новости"];
        string exported = SettingsListTextFormat.Write(SettingsListKind.Prompts, [values], markdown);
        var row = Assert.Single(SettingsListTextFormat.Read(SettingsListKind.Prompts, exported));
        Assert.Equal(values, row.Values);
        Assert.Equal(markdown ? 3 : 2, row.Line);
    }

    [Fact]
    public void ReadsWindowsPathsWithoutReinterpretingBackslashes()
    {
        var row = Assert.Single(SettingsListTextFormat.Read(SettingsListKind.Iptv,
            "Name\tURL or path\tType\nLocal\tC:\\test\\news.m3u\tLocalFile"));
        Assert.Equal(@"C:\test\news.m3u", row.Values[1]);
    }

    [Theory]
    [InlineData("Заголовок,Ссылка\n", 2)]
    [InlineData("", 1)]
    public void ReadsIptvTitleAndUrlSeparatedByComma(string header, int expectedFirstLine)
    {
        var rows = SettingsListTextFormat.Read(SettingsListKind.Iptv,
            header + "Тестовая подборка,https://example.org/playlist.m3u?items=1,2\nДругой источник,https://example.org/watch");

        Assert.Equal(2, rows.Count);
        Assert.Equal(expectedFirstLine, rows[0].Line);
        Assert.Equal("Тестовая подборка", rows[0].Values[0]);
        Assert.Equal("https://example.org/playlist.m3u?items=1,2", rows[0].Values[1]);
        Assert.Equal("https://example.org/watch", rows[1].Values[1]);
    }

    [Fact]
    public void AcceptsChannelListsAndReorderedRussianHeader()
    {
        var rows = SettingsListTextFormat.Read(SettingsListKind.TelegramReading, "@source\n-1001234567890");
        Assert.Equal(["", "@source"], rows[0].Values);
        Assert.Equal(["", "-1001234567890"], rows[1].Values);

        var mapped = Assert.Single(SettingsListTextFormat.Read(SettingsListKind.TelegramReading,
            "| Категория | Ссылка | Название |\n| --- | --- | --- |\n| IT | @source | Новости |"));
        Assert.Equal(["Новости", "@source", "IT"], mapped.Values);
    }

    [Fact]
    public void CloningSettingsPreservesSourcesAndTheirOrder()
    {
        var settings = new AppSettings
        {
            RssFeeds = [new() { Name = "News", Value = "https://example.org/feed", Category = "IT", IsEnabled = false }],
            TelegramReadingSources = [new() { Name = "Channel", Value = "@example" }],
            SavedPrompts = [new() { Name = "Digest", Value = "Summarize" }],
            TelegramPublishingCategories = new() { ["channel-id"] = "News" },
            AiConnectionDisplayOrder = ["second", "first"]
        };
        AppSettings clone = AppSettingsService.CloneAppSettings(settings);
        Assert.Equal(settings.RssFeeds, clone.RssFeeds);
        Assert.Equal(settings.TelegramReadingSources, clone.TelegramReadingSources);
        Assert.Equal(settings.SavedPrompts, clone.SavedPrompts);
        Assert.Equal("News", clone.TelegramPublishingCategories["channel-id"]);
        Assert.Equal(["second", "first"], clone.AiConnectionDisplayOrder);
        Assert.NotSame(settings.RssFeeds, clone.RssFeeds);
    }

    [Theory]
    [InlineData("<rss version='2.0'><channel><title>News</title></channel></rss>", true)]
    [InlineData("<feed xmlns='http://www.w3.org/2005/Atom'><title>News</title></feed>", true)]
    [InlineData("<html><body>Not a feed</body></html>", false)]
    [InlineData("<rss><channel>", false)]
    [InlineData("<!DOCTYPE rss [<!ENTITY external SYSTEM 'file:///secret'>]><rss><channel>&external;</channel></rss>", false)]
    public void FeedVerificationRejectsMalformedXmlAndExternalEntities(string xml, bool valid)
    {
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(xml));
        Assert.Equal(valid, AppSettingsWindow.IsFeedXml(stream));
    }
}
