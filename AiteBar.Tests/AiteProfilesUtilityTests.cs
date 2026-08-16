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
    public void QuickLinkService_ResolvesCommandDirectUrlAndRankedFallbackLikeOriginal()
    {
        var service = new AiteProfilesQuickLinkService(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
        var snippets = new[]
        {
            new AiteProfileSnippet { Name = "Drive Docs", Tags = ["work"], Urls = ["https://drive.google.com/"] },
            new AiteProfileSnippet { Name = "Gmail", Tags = ["mail"], Urls = ["https://mail.google.com/"] }
        };

        Assert.True(service.TryResolveSnippet("ai:Gemini:gemini.google.com", null, snippets, out AiteProfileSnippet command, out bool saveCommand));
        Assert.True(saveCommand);
        Assert.Equal("Gemini", command.Name);
        Assert.Equal(["https://gemini.google.com/"], command.Urls);

        Assert.True(service.TryResolveSnippet("example.com", null, snippets, out AiteProfileSnippet direct, out bool saveDirect));
        Assert.False(saveDirect);
        Assert.Equal("direct", direct.Name);
        Assert.Equal(["https://example.com/"], direct.Urls);

        Assert.True(service.TryResolveSnippet("work", null, snippets, out AiteProfileSnippet fallback, out bool saveFallback));
        Assert.False(saveFallback);
        Assert.Equal("Drive Docs", fallback.Name);
    }

    [Fact]
    public void QuickLinkService_RanksTagsBeforeNameBeforeUrl()
    {
        var service = new AiteProfilesQuickLinkService(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
        var ranked = service.RankSnippets(
            [
                new AiteProfileSnippet { Name = "Work Name", Tags = ["misc"], Urls = ["https://name.example.com/"] },
                new AiteProfileSnippet { Name = "Alpha", Tags = ["work"], Urls = ["https://alpha.example.com/"] },
                new AiteProfileSnippet { Name = "Beta", Tags = ["misc"], Urls = ["https://work.example.com/"] }
            ],
            "work");

        Assert.Equal(["Alpha", "Work Name", "Beta"], ranked.Select(static snippet => snippet.Name).ToArray());
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

    [Fact]
    public void BuildTextExport_ExportsAllTags_RoundTripMatches()
    {
        var service = new AiteProfilesQuickLinkService(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
        var snippets = new[]
        {
            new AiteProfileSnippet { Name = "Multi", Tags = ["work", "ai", "docs"], Urls = ["https://example.com/"] },
            new AiteProfileSnippet { Name = "Single", Tags = ["mail"], Urls = ["https://mail.google.com/"] }
        };

        string exported = service.BuildTextExport(snippets);
        IReadOnlyList<AiteProfileSnippet> reimported = service.ParseImportLines(exported);

        AiteProfileSnippet multi = Assert.Single(reimported, s => s.Name == "Multi");
        Assert.Equal(["ai", "docs", "work"], multi.Tags.OrderBy(x => x, StringComparer.Ordinal));

        AiteProfileSnippet single = Assert.Single(reimported, s => s.Name == "Single");
        Assert.Equal(["mail"], single.Tags);
    }

    [Fact]
    public void BuildTextExport_SingleTag_FormatPreservedLegacyCompatible()
    {
        var service = new AiteProfilesQuickLinkService(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
        var snippets = new[]
        {
            new AiteProfileSnippet { Name = "Legacy", Tags = ["misc"], Urls = ["https://example.com/"] }
        };

        string exported = service.BuildTextExport(snippets).TrimEnd();

        Assert.Equal("Legacy | misc | https://example.com/", exported);
    }
}
