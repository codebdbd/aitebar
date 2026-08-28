using AiteBar;
using AiteBar.AiteProfilesUtility;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls.Primitives;
using System.Xml.Linq;

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

            await viewModel.RefreshAsync(includeExpensiveStats: false, cancellationToken: cancellationSource.Token);

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

    [Fact]
    public void QuickLinkPopupLayout_UsesLargestAvailableSideAndFullAvailableHeight()
    {
        AiteProfilesQuickLinkPopupLayout layout = AiteProfilesQuickLinkPopupLayoutHelper.Calculate(spaceAbove: 268, spaceBelow: 84);

        Assert.Equal(PlacementMode.Top, layout.Placement);
        Assert.Equal(268, layout.MaxHeight);
        Assert.Equal(-2, layout.VerticalOffset);
    }

    [Fact]
    public void QuickLinkPopupLayout_OpensBelowWhenThereIsMoreRoom()
    {
        AiteProfilesQuickLinkPopupLayout layout = AiteProfilesQuickLinkPopupLayoutHelper.Calculate(spaceAbove: 90, spaceBelow: 196);

        Assert.Equal(PlacementMode.Bottom, layout.Placement);
        Assert.Equal(196, layout.MaxHeight);
        Assert.Equal(2, layout.VerticalOffset);
    }

    [Fact]
    public void QuickLinkSuggestionsPopup_UsesInputWidthInsteadOfContentWidth()
    {
        XDocument xaml = XDocument.Load(Path.Combine(
            FindRepoRoot(),
            "AiteBar",
            "AiteProfilesUtility",
            "AiteProfilesWindow.xaml"));
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";

        XElement listBox = Assert.Single(xaml.Descendants(presentation + "ListBox"),
            element => element.Attribute(x + "Name")?.Value == "QuickLinkSuggestionsList");

        Assert.Equal("{Binding ActualWidth, ElementName=QuickLinkBox}", listBox.Attribute("Width")?.Value);
        Assert.Null(listBox.Attribute("MaxWidth"));
    }

    [Fact]
    public void NormalizeSnippets_SingleUrlRecords_DeduplicateAndTakeNameFromLaterRecord()
    {
        var input = new[]
        {
            new AiteProfileSnippet { Name = "First", Tags = ["work"], Urls = ["https://example.com/"] },
            new AiteProfileSnippet { Name = "Second", Tags = ["ai"], Urls = ["https://example.com/"] }
        };

        IReadOnlyList<AiteProfileSnippet> result = AiteProfilesQuickLinkService.NormalizeSnippets(input);

        AiteProfileSnippet single = Assert.Single(result);
        Assert.Equal("Second", single.Name);
        Assert.Equal(["ai", "work"], single.Tags);
        Assert.Equal(["https://example.com/"], single.Urls);
    }

    [Fact]
    public void NormalizeSnippets_MultiUrlRecords_DoNotMergeWithOtherRecords()
    {
        var input = new[]
        {
            new AiteProfileSnippet { Name = "Bundle A", Tags = ["oldTag"], Urls = ["https://a.com/", "https://b.com/"] },
            new AiteProfileSnippet { Name = "Bundle B", Tags = ["newTag"], Urls = ["https://b.com/", "https://c.com/"] },
            new AiteProfileSnippet { Name = "Single B", Tags = ["singleTag"], Urls = ["https://b.com/"] }
        };

        IReadOnlyList<AiteProfileSnippet> result = AiteProfilesQuickLinkService.NormalizeSnippets(input);

        Assert.Equal(3, result.Count);
        Assert.Equal("Bundle A", result[0].Name);
        Assert.Equal(["https://a.com/", "https://b.com/"], result[0].Urls);

        Assert.Equal("Bundle B", result[1].Name);
        Assert.Equal(["https://b.com/", "https://c.com/"], result[1].Urls);

        Assert.Equal("Single B", result[2].Name);
        Assert.Equal(["https://b.com/"], result[2].Urls);
    }

    [Fact]
    public void NormalizeSnippets_SortsResultByName()
    {
        var input = new[]
        {
            new AiteProfileSnippet { Name = "Zebra", Tags = ["tag1"], Urls = ["https://zebra.com/"] },
            new AiteProfileSnippet { Name = "Alpha", Tags = ["tag2"], Urls = ["https://alpha.com/"] }
        };

        IReadOnlyList<AiteProfileSnippet> result = AiteProfilesQuickLinkService.NormalizeSnippets(input);

        Assert.Equal(2, result.Count);
        Assert.Equal("Alpha", result[0].Name);
        Assert.Equal("Zebra", result[1].Name);
    }

    [Fact]
    public void NormalizeSnippets_ImportSingleUrlOverwritesOlderMatchingNameAndMergesTags()
    {
        var service = new AiteProfilesQuickLinkService(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
        var existing = new[]
        {
            new AiteProfileSnippet { Name = "Docs", Tags = ["work"], Urls = ["https://docs.google.com/"] }
        };
        string importedContent = "Updated Docs | work; ai | https://docs.google.com/";

        IReadOnlyList<AiteProfileSnippet> imported = service.ParseImportLines(importedContent);
        IReadOnlyList<AiteProfileSnippet> combined = AiteProfilesQuickLinkService.NormalizeSnippets(existing.Concat(imported));

        AiteProfileSnippet merged = Assert.Single(combined);
        Assert.Equal("Updated Docs", merged.Name);
        Assert.Equal(["ai", "work"], merged.Tags);
        Assert.Equal(["https://docs.google.com/"], merged.Urls);
    }

    private static string CreateTemporaryDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), "AiteBarTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static string FindRepoRoot()
    {
        string? current = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(current))
        {
            if (File.Exists(Path.Combine(current, "AiteBar.sln")))
            {
                return current;
            }

            current = Directory.GetParent(current)?.FullName;
        }

        throw new DirectoryNotFoundException("Repository root with AiteBar.sln was not found.");
    }

    private static AiteProfileScanRow CreateProfileRow(string profilePath) => new()
    {
        Folder = "Profile 1",
        Name = "Profile 1",
        Email = "profile@example.com",
        LastTs = 0,
        Path = profilePath
    };

    [Fact]
    public async Task Refresh_ForceRescan_PassesFlagToScanner()
    {
        string root = CreateTemporaryDirectory();
        string profilePath = Path.Combine(root, "Profile 1");
        Directory.CreateDirectory(profilePath);
        try
        {
            var scanner = new RescanTrackingScanner([CreateProfileRow(profilePath)]);
            var store = new AiteProfilesStore(scanner, root);
            await store.InitializeAsync();

            await store.RefreshAsync(includeExpensiveStats: true, forceRescan: true);

            Assert.True(scanner.LastForceRescan);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private sealed class TestScanner(IReadOnlyList<AiteProfileScanRow> rows) : IAiteProfilesScanner
    {
        public Task<IReadOnlyList<AiteProfileScanRow>> ScanAsync(
            IReadOnlyDictionary<string, AiteProfileCacheEntry>? cache = null,
            bool includeExpensiveStats = true,
            bool forceRescan = false,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(rows);
        }
    }

    private sealed class RescanTrackingScanner(IReadOnlyList<AiteProfileScanRow> rows) : IAiteProfilesScanner
    {
        public bool LastForceRescan { get; private set; }

        public Task<IReadOnlyList<AiteProfileScanRow>> ScanAsync(
            IReadOnlyDictionary<string, AiteProfileCacheEntry>? cache = null,
            bool includeExpensiveStats = true,
            bool forceRescan = false,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastForceRescan = forceRescan;
            return Task.FromResult(rows);
        }
    }
}
