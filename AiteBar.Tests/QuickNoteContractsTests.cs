namespace AiteBar.Tests;

public sealed class QuickNoteContractsTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(6)]
    public void HeadingTag_RoundTripsSupportedLevels(int level)
    {
        Assert.True(QuickNoteTags.TryGetHeadingLevel(QuickNoteTags.Heading(level), out int parsed));
        Assert.Equal(level, parsed);
    }

    [Theory]
    [InlineData("heading:0")]
    [InlineData("heading:7")]
    [InlineData("heading:one")]
    [InlineData("Heading:1")]
    [InlineData(null)]
    public void HeadingTag_RejectsInvalidValues(string? tag)
    {
        Assert.False(QuickNoteTags.TryGetHeadingLevel(tag, out _));
    }

    [Fact]
    public void LinkAndIndentTags_RoundTripWithoutParsingPayloadContents()
    {
        const string url = "https://example.com/?value=link:test";
        Assert.Equal(url, QuickNoteTags.GetLink(QuickNoteTags.Link(url)));
        Assert.Equal("  ", QuickNoteTags.GetIndent(QuickNoteTags.Indent("  ")));
    }

    [Fact]
    public void FontContract_UsesExpectedFamilies()
    {
        Assert.Equal(QuickNoteFonts.DefaultFamilyName, QuickNoteFonts.Default.Source);
        Assert.EndsWith($"#{QuickNoteFonts.CodeFamilyName}", QuickNoteFonts.Code.Source);
    }
}
