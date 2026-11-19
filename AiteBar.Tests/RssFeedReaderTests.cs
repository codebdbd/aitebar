using System.IO;
using System.Text;

namespace AiteBar.Tests;

public sealed class RssFeedReaderTests
{
    [Fact]
    public void ReadsAtomArticleRelativeToFeedAndIgnoresNonWebLinks()
    {
        using var stream = Xml("""
            <feed xmlns="http://www.w3.org/2005/Atom" xml:base="/news/">
              <entry><title>Updates &amp; fixes</title><link rel="self" href="private.xml"/><link href="release"/></entry>
              <entry><title>Invalid link</title><link href="file:///C:/settings.json"/></entry>
              <entry><summary>No title or link</summary></entry>
            </feed>
            """);
        var entries = Assert.IsAssignableFrom<IReadOnlyList<RssFeedReader.Entry>>(
            RssFeedReader.Read(stream, new Uri("https://example.org/feeds/main.xml")));
        Assert.Equal(2, entries.Count);
        Assert.Equal($"Updates & fixes{Environment.NewLine}https://example.org/news/release", entries[0].PromptText);
        Assert.Equal("Invalid link", entries[1].PromptText);
    }

    [Theory]
    [InlineData("<rss version='2.0'><channel><item><title>Release</title></item></channel></rss>")]
    [InlineData("<rdf:RDF xmlns:rdf='http://www.w3.org/1999/02/22-rdf-syntax-ns#' xmlns='http://purl.org/rss/1.0/'><channel/><item><title>Release</title></item></rdf:RDF>")]
    public void ReadsEntriesFromBothRssFormats(string xml)
    {
        using var stream = Xml(xml);
        Assert.Equal("Release", Assert.Single(RssFeedReader.Read(stream)!).Title);
    }

    [Fact]
    public void EmptyValidFeedIsDistinctFromAnInvalidDocument()
    {
        using var emptyFeed = Xml("<rss><channel/></rss>");
        using var invalid = Xml("<html><body>Not a feed</body></html>");
        Assert.Empty(RssFeedReader.Read(emptyFeed)!);
        Assert.Null(RssFeedReader.Read(invalid));
    }

    private static MemoryStream Xml(string text) => new(Encoding.UTF8.GetBytes(text));
}
