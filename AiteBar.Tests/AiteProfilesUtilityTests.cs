using AiteBar;
using AiteBar.AiteProfilesUtility;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

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
    public void QuickLinkService_AcceptsSingleLabelIntranetHost()
    {
        var service = new AiteProfilesQuickLinkService(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));

        bool parsed = service.TryParseDirectUrls("http://myserver", out List<string> urls);

        Assert.True(parsed);
        Assert.Equal(["http://myserver/"], urls);
    }

    [Fact]
    public void QuickLinkService_NormalizesBareSingleLabelIntranetHost()
    {
        var service = new AiteProfilesQuickLinkService(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));

        bool parsed = service.TryParseDirectUrls("intranet", out List<string> urls);

        Assert.True(parsed);
        Assert.Equal(["https://intranet/"], urls);
    }

    [Fact]
    public void QuickLinkService_ParseTags_NormalizesDialogAndImportInputConsistently()
    {
        List<string> tags = AiteProfilesQuickLinkService.ParseTags(" Work; ai | work  docs ");

        Assert.Equal(["work", "ai", "docs"], tags);
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

    [Fact]
    public async Task Store_DoesNotApplyFavoriteWhenPersistenceFails()
    {
        string root = CreateTemporaryDirectory();
        string profilePath = Path.Combine(root, "Profile 1");
        Directory.CreateDirectory(profilePath);
        try
        {
            var store = new AiteProfilesStore(new TestScanner([CreateProfileRow(profilePath)]), root);
            await store.InitializeAsync();
            await store.RefreshAsync(includeExpensiveStats: false);
            Directory.CreateDirectory(Path.Combine(root, "favorites.json"));

            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => store.MarkFavoriteAsync("Profile 1", profilePath, true));

            AiteProfile profile = Assert.Single(await store.SnapshotProfilesAsync());
            Assert.False(profile.IsFavorite);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task JsonStore_PropagatesCancellation()
    {
        string root = CreateTemporaryDirectory();
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();
        try
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                AiteProfilesJsonStore.WriteAsync(Path.Combine(root, "cancelled.json"), new { Value = "cancelled" }, cancellationSource.Token));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Refresh_CancellationDoesNotReportAScanFailure()
    {
        string root = CreateTemporaryDirectory();
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();
        try
        {
            var viewModel = new AiteProfilesViewModel(
                new AiteProfilesStore(new TestScanner([]), root),
                new AiteProfilesChromeLauncher(),
                new AiteProfilesQuickLinkService(root),
                new AiteProfilesRotationStateService(root));
            int messages = 0;
            viewModel.MessageRequested += (_, _) => messages++;

            await viewModel.RefreshAsync(includeExpensiveStats: false, cancellationSource.Token);

            Assert.Equal(0, messages);
            Assert.NotEqual(LocalizationService.Get("AiteProfiles_StatusScanFailed"), viewModel.StatusText);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task RotationState_PersistsWithoutBlockingCallers()
    {
        string root = CreateTemporaryDirectory();
        try
        {
            var state = new AiteProfilesRotationStateService(root);
            state.SetEnabled(true);
            state.SetLastProfileKey("Profile 2|C:\\Chrome\\Profile 2");
            state.SetRotationOrder(["first", "second", "first"]);
            Assert.True(await state.FlushAsync(TimeSpan.FromSeconds(2)));

            var restored = new AiteProfilesRotationStateService(root);
            await restored.InitializeAsync();

            Assert.True(restored.GetEnabled());
            Assert.Equal("Profile 2|C:\\Chrome\\Profile 2", restored.GetLastProfileKey());
            Assert.Equal(["first", "second"], restored.GetRotationOrder());
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task RotationState_ReportsPersistenceFailureAndFlushFails()
    {
        string root = CreateTemporaryDirectory();
        Directory.CreateDirectory(Path.Combine(root, "rotation_state.json"));
        try
        {
            var state = new AiteProfilesRotationStateService(root);
            var failure = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
            state.PersistenceFailed += exception => failure.TrySetResult(exception);

            state.SetEnabled(true);

            Exception reported = await failure.Task.WaitAsync(TimeSpan.FromSeconds(2));
            Assert.IsType<UnauthorizedAccessException>(reported);
            Assert.False(await state.FlushAsync(TimeSpan.FromSeconds(2)));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string CreateTemporaryDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), "AiteBarTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static AiteProfileScanRow CreateProfileRow(string profilePath) => new()
    {
        Folder = "Profile 1",
        Name = "Profile 1",
        Email = "profile@example.com",
        LastTs = 0,
        Path = profilePath
    };

    private sealed class TestScanner(IReadOnlyList<AiteProfileScanRow> rows) : IAiteProfilesScanner
    {
        public Task<IReadOnlyList<AiteProfileScanRow>> ScanAsync(
            IReadOnlyDictionary<string, AiteProfileCacheEntry>? cache = null,
            bool includeExpensiveStats = true,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(rows);
        }
    }
}
