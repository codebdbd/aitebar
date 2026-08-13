using AiteBar;
using AiteBar.AiteProfilesUtility;
using System.IO;

namespace AiteBar.Tests;

public sealed class AiteProfilesUtilityTests
{
    [Fact]
    public void QuickLinkService_ParsesCommandAndNormalizesUrl()
    {
        var service = new AiteProfilesQuickLinkService(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));

        bool parsed = service.TryParseCommand("work:Docs:example.com|https://openai.com", out AiteProfileSnippet snippet);

        Assert.True(parsed);
        Assert.Equal("Docs", snippet.Name);
        Assert.Equal(["work"], snippet.Tags);
        Assert.Equal(["https://example.com/", "https://openai.com/"], snippet.Urls);
    }

    [Fact]
    public void QuickLinkService_RejectsNonHttpUrls()
    {
        var service = new AiteProfilesQuickLinkService(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));

        bool parsed = service.TryParseCommand("bad:Local:file:///C:/Temp/test.txt", out _);

        Assert.False(parsed);
    }

    [Fact]
    public void Store_NormalizeTags_TrimsDeduplicatesAndPreservesFirstCasing()
    {
        string normalized = AiteProfilesStore.NormalizeTags(" Farm, ai, farm,  Work ");

        Assert.Equal("Farm, ai, Work", normalized);
    }

    [Fact]
    public void ProfileKey_BuildsStableCompositeKey()
    {
        string key = AiteProfileKey.Build(" Profile 1 ", " C:\\Chrome\\Profile 1 ");

        Assert.Equal("Profile 1|C:\\Chrome\\Profile 1", key);
    }

    [Fact]
    public void UtilityButtonCatalog_IncludesAiteProfiles()
    {
        bool found = UtilityButtonCatalog.TryGet("AiteProfiles", out UtilityButtonDefinition? definition);

        Assert.True(found);
        Assert.NotNull(definition);
        Assert.Equal("Main_AiteProfilesTooltip", definition.TooltipKey);
    }
}
