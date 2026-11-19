using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AiteBar.IptvPlayer;
using Xunit;

namespace AiteBar.Tests;

[Collection("WpfTestCollection")]
public sealed class IptvPlayerTests : IDisposable
{
    private readonly string _tempDirectory;

    public IptvPlayerTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"aitebar_iptv_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDirectory);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, true);
            }
        }
        catch
        {
            // ignore
        }
    }

    #region Parser & URL Security Tests

    [Fact]
    public void ParseM3u_StandardExtInf_ParsesAttributesAndTitleCorrectly()
    {
        string m3u = """
            #EXTM3U
            #EXTINF:-1 tvg-id="cnn.us" tvg-name="CNN USA" tvg-logo="http://logos.com/cnn.png" group-title="News",CNN News Live
            https://stream.example.com/cnn/live.m3u8
            #EXTINF:-1 tvg-id="bbc.uk" group-title="News",BBC World News
            #EXTGRP:International
            https://stream.example.com/bbc/live.m3u8
            """;

        var (channels, skipped) = IptvPlaylistParser.ParseM3u(m3u, IptvSourceKind.Url);

        Assert.Equal(0, skipped);
        Assert.Equal(2, channels.Count);

        var ch1 = channels[0];
        Assert.Equal("CNN News Live", ch1.Title);
        Assert.Equal("News", ch1.Group);
        Assert.Equal("cnn.us", ch1.TvgId);
        Assert.Equal("CNN USA", ch1.TvgName);
        Assert.Equal("http://logos.com/cnn.png", ch1.LogoUrl);
        Assert.Equal("https://stream.example.com/cnn/live.m3u8", ch1.StreamUrl);

        var ch2 = channels[1];
        Assert.Equal("BBC World News", ch2.Title);
        Assert.Equal("International", ch2.Group); // #EXTGRP переопределяет group-title
        Assert.Equal("bbc.uk", ch2.TvgId);
    }

    [Fact]
    public void ParseM3u_UnquotedAttributesAndExtraCommas_ParsesCleanly()
    {
        string m3u = """
            #EXTM3U
            #EXTINF:-1 tvg-id=discovery.hd group-title=Documentary,Discovery, The World (HD) [FHD]
            https://stream.example.com/disc.m3u8
            """;

        var (channels, skipped) = IptvPlaylistParser.ParseM3u(m3u, IptvSourceKind.Url);

        Assert.Equal(0, skipped);
        Assert.Single(channels);
        var ch = channels[0];
        Assert.Equal("discovery.hd", ch.TvgId);
        Assert.Equal("Documentary", ch.Group);
        Assert.Equal("Discovery, The World (HD) [FHD]", ch.Title);
        Assert.Equal("Discovery, The World (HD)", IptvPlaylistParser.CleanChannelTitle(ch.Title));
    }

    [Fact]
    public void ParseM3u_HlsMasterManifest_DetectsAndReturnsAsSingleStream()
    {
        string hlsManifest = """
            #EXTM3U
            #EXT-X-VERSION:3
            #EXT-X-STREAM-INF:BANDWIDTH=1280000,RESOLUTION=720x480
            low.m3u8
            #EXT-X-STREAM-INF:BANDWIDTH=2560000,RESOLUTION=1280x720
            mid.m3u8
            #EXT-X-STREAM-INF:BANDWIDTH=7680000,RESOLUTION=1920x1080
            high.m3u8
            """;

        bool isHls = IptvPlaylistParser.IsHlsManifestContent(hlsManifest);
        Assert.True(isHls);

        var (channels, skipped) = IptvPlaylistParser.ParseM3u(
            hlsManifest,
            IptvSourceKind.Url,
            baseLocation: "https://stream.example.com/live/master.m3u8");

        Assert.Equal(0, skipped);
        Assert.Single(channels);
        Assert.Equal("master", channels[0].Title);
        Assert.Equal("https://stream.example.com/live/master.m3u8", channels[0].StreamUrl);
    }

    [Fact]
    public void ParseM3u_BlocksFileUriInRemotePlaylist()
    {
        string maliciousM3u = """
            #EXTM3U
            #EXTINF:-1,Legit Stream
            https://example.com/video.m3u8
            #EXTINF:-1,Malicious Local File
            file:///C:/Windows/System32/drivers/etc/hosts
            #EXTINF:-1,CLI Injection
            --no-sandbox -v http://bad.com
            """;

        var (channels, skipped) = IptvPlaylistParser.ParseM3u(maliciousM3u, IptvSourceKind.Url);

        Assert.Equal(2, skipped);
        Assert.Single(channels);
        Assert.Equal("Legit Stream", channels[0].Title);
    }

    [Fact]
    public void IsSensitiveUrl_AndMaskSensitiveUrl_ProtectsSecrets()
    {
        string tokenUrl = "https://provider.tv/live/user123/stream.m3u8?token=SECRET_ABC_123&quality=best";
        Assert.True(IptvPlaylistParser.IsSensitiveUrl(tokenUrl));

        string masked = IptvPlaylistParser.MaskSensitiveUrl(tokenUrl);
        Assert.Contains("token=***", masked);
        Assert.DoesNotContain("SECRET_ABC_123", masked);
        Assert.Contains("quality=best", masked);

        string basicAuthUrl = "https://myuser:mypassword@iptv.com/playlist.m3u";
        Assert.True(IptvPlaylistParser.IsSensitiveUrl(basicAuthUrl));
        string maskedAuth = IptvPlaylistParser.MaskSensitiveUrl(basicAuthUrl);
        Assert.DoesNotContain("myuser", maskedAuth);
        Assert.DoesNotContain("mypassword", maskedAuth);
    }

    #endregion

    #region Store Tests (Atomic save & .bak recovery)

    [Fact]
    public void LoadCache_MigratesLegacyPrimaryAndBackupWithoutExposingUrls()
    {
        const string stream = "https://203.0.113.10/live?token=private-stream";
        var store = new IptvPlayerStore(_tempDirectory);
        string path = store.GetCacheFilePath("legacy");
        string legacy = System.Text.Json.JsonSerializer.Serialize(new IptvPlaylistCache
        {
            PlaylistId = "legacy",
            Channels = [new() { Title = "Legacy channel", StreamUrl = stream }]
        });
        File.WriteAllText(path, legacy);
        File.WriteAllText(path + ".bak", legacy);

        Assert.Equal(stream, Assert.Single(store.LoadCache("legacy")!.Channels).StreamUrl);
        Assert.DoesNotContain("private-stream", File.ReadAllText(path));
        Assert.DoesNotContain("private-stream", File.ReadAllText(path + ".bak"));
        File.WriteAllText(path, "broken");
        Assert.Equal(stream, Assert.Single(store.LoadCache("legacy")!.Channels).StreamUrl);
    }

    [Fact]
    public void Store_SavesAndLoadsFavoritesAtomically()
    {
        var store = new IptvPlayerStore(_tempDirectory);
        var favorites = new List<IptvFavorite>
        {
            new() { ChannelKey = "key_1", Title = "Channel 1", StreamUrl = "http://ch1.com" },
            new() { ChannelKey = "key_2", Title = "Channel 2", StreamUrl = "http://ch2.com" }
        };

        store.SaveFavorites(favorites);
        var loaded = store.LoadFavorites();

        Assert.Equal(2, loaded.Count);
        Assert.Equal("key_1", loaded[0].ChannelKey);
        Assert.Equal("key_2", loaded[1].ChannelKey);
    }

    [Fact]
    public void Store_RecoversFromBak_WhenMainFileCorrupted()
    {
        var store = new IptvPlayerStore(_tempDirectory);
        var state = new IptvPlayerState
        {
            LastSelectedChannelKey = "saved_channel",
            LastVolume = 95,
            IsMuted = true
        };

        // 1-е сохранение создает основной файл
        store.SavePlayerState(state);
        // 2-е сохранение копирует предыдущий в .bak
        state.LastVolume = 60;
        store.SavePlayerState(state);

        // Повреждаем основной файл
        File.WriteAllText(store.PlayerStatePath, "{ invalid json corrupt");

        var recovered = store.LoadPlayerState();
        Assert.NotNull(recovered);
        Assert.Equal("saved_channel", recovered.LastSelectedChannelKey);
    }

    [Fact]
    public void Store_SavesAndLoadsCache()
    {
        var store = new IptvPlayerStore(_tempDirectory);
        var cache = new IptvPlaylistCache
        {
            PlaylistId = "pl_123",
            RefreshedAtUtc = DateTime.UtcNow,
            Channels =
            [
                new IptvCachedChannelEntry { Title = "Discovery", Group = "Doc", StreamUrl = "http://disc.com" }
            ]
        };

        store.SaveCache(cache);
        var loaded = store.LoadCache("pl_123");

        Assert.NotNull(loaded);
        Assert.Equal("pl_123", loaded.PlaylistId);
        Assert.Single(loaded.Channels);
        Assert.Equal("Discovery", loaded.Channels[0].Title);

        store.DeleteCache("pl_123");
        Assert.Null(store.LoadCache("pl_123"));
    }

    #endregion

    #region Catalog & Variant Grouping Tests

    [Theory]
    [InlineData("Пятница 720x576", IptvResolution.Sd)]
    [InlineData("Eurosport (720p)", IptvResolution.Hd)]
    [InlineData("Sports HD (1080i) [Geo-blocked]", IptvResolution.Fhd)]
    [InlineData("News [Full HD]", IptvResolution.Fhd)]
    [InlineData("News (2560×1440)", IptvResolution.Qhd)]
    [InlineData("Cinema (2160p)", IptvResolution.Uhd)]
    [InlineData("Cinema 4K", IptvResolution.Uhd)]
    [InlineData("Channel 1080", IptvResolution.Any)]
    [InlineData("Channel HD Plus", IptvResolution.Any)]
    public void ResolutionLabel_RecognizesExplicitPlaylistNames(string title, IptvResolution expected)
    {
        Assert.Equal(expected, IptvResolutionLabels.Read(title));
    }

    [Fact]
    public void Catalog_ResolutionCombinesWithGroupsAndKeepsAllVariants()
    {
        var store = new IptvPlayerStore(_tempDirectory);
        var service = new IptvCatalogService(store);
        var source = new IptvPlaylistSource { Id = "quality" };
        store.SaveCache(new IptvPlaylistCache
        {
            PlaylistId = source.Id,
            Channels =
            [
                new() { TvgId = "sports", Title = "Sport (720p)", Group = "Sports", StreamUrl = "https://example.com/720" },
                new() { TvgId = "sports", Title = "Sport (1080p)", Group = "Sports", StreamUrl = "https://example.com/1080" },
                new() { Title = "News (1080p)", Group = "News", StreamUrl = "https://example.com/news" },
                new() { Title = "Untitled quality", Group = "Sports", StreamUrl = "https://example.com/unknown" }
            ]
        });

        var channels = service.BuildCatalog(new IptvSettings { Sources = [source] });

        Assert.Equal(2, service.FilterCatalog(channels, source.Id, "Sports", null).Count);
        var hd = Assert.Single(service.FilterCatalog(channels, source.Id, "Sports", null, resolution: IptvResolution.Hd));
        Assert.Same(hd, Assert.Single(service.FilterCatalog(channels, source.Id, "Sports", null, resolution: IptvResolution.Fhd)));
        Assert.Equal(2, service.FilterCatalog(channels, source.Id, null, null, resolution: IptvResolution.Fhd).Count);
        Assert.Equal(2, hd.Variants.Count);
    }

    [Fact]
    public void Catalog_ExpandsCompositeGroupsFromExistingCache()
    {
        var store = new IptvPlayerStore(_tempDirectory);
        var service = new IptvCatalogService(store);
        var source = new IptvPlaylistSource { Id = "cached-groups", AutoFallbackVariants = false };
        store.SaveCache(new IptvPlaylistCache
        {
            PlaylistId = source.Id,
            Channels =
            [
                new() { Title = "Outdoor TV", Group = "Auto; Outdoor;;Sports", StreamUrl = "https://example.com/outdoor" },
                new() { Title = "Sports TV", Group = "sports; Series", StreamUrl = "https://example.com/sports" }
            ]
        });

        var catalog = service.BuildCatalog(new IptvSettings { Sources = [source] });

        Assert.Equal(new[] { "Auto", "Outdoor", "Series", "Sports" }, IptvCatalogService.GetGroupNames(catalog));
        Assert.Equal("Outdoor TV", Assert.Single(service.FilterCatalog(catalog, source.Id, "outdoor", null)).Title);
        Assert.Equal(2, service.FilterCatalog(catalog, source.Id, "Sports", null).Count);
    }

    [Fact]
    public void Catalog_FavoritesCanBeFilteredByPlaylistGroupAndSearch()
    {
        var catalog = new IptvCatalogService(new IptvPlayerStore(_tempDirectory));
        var chosen = new IptvChannel { PlaylistId = "one", Title = "Sports North", Group = "Sports", IsFavorite = true };
        var channels = new List<IptvChannel>
        {
            chosen,
            new() { PlaylistId = "one", Title = "Sports South", Group = "Sports" },
            new() { PlaylistId = "two", Title = "Sports North", Group = "Sports", IsFavorite = true },
            new() { PlaylistId = "one", Title = "Local North", Group = "News", IsFavorite = true }
        };

        Assert.Same(chosen, Assert.Single(catalog.FilterCatalog(channels, "one", "Sports", "North", favoritesOnly: true)));
        Assert.Equal(2, catalog.FilterCatalog(channels, "one", "Sports", null).Count);
    }

    [Fact]
    public void Catalog_OnlyDuplicateTitlesGetDistinguishingText()
    {
        var first = new IptvChannel { Title = "Sports", PlaylistId = "one", PlaylistName = "First list" };
        var second = new IptvChannel { Title = "sports", PlaylistId = "two", PlaylistName = "Second list" };
        var independent = new IptvChannel { Title = "News", PlaylistId = "two", PlaylistName = "Second list" };

        IptvCatalogService.SetDuplicateChannelLabels([first, second, independent]);

        Assert.Equal("First list", first.SecondaryText);
        Assert.Equal("Second list", second.SecondaryText);
        Assert.Null(independent.SecondaryText);
    }

    [Fact]
    public async Task Catalog_KeepsNewPlaylistSelectableBeforeItsSavedRevisionLoads()
    {
        var store = new IptvPlayerStore(_tempDirectory);
        var catalog = new IptvCatalogService(store);
        using var service = new IptvPlaylistService(store);
        var source = new IptvPlaylistSource
        {
            Name = "New playlist",
            Kind = IptvSourceKind.InlineText,
            ContentRevision = Guid.NewGuid().ToString("N"),
            InlineContent = "#EXTM3U\n#EXTINF:-1,New channel\nhttps://example.com/new.m3u8"
        };
        var disabled = new IptvPlaylistSource { IsEnabled = false };
        var settings = new IptvSettings { Sources = [disabled, source] };
        store.SaveCache(new IptvPlaylistCache
        {
            PlaylistId = source.Id,
            Channels = [new IptvCachedChannelEntry { Title = "Old channel", StreamUrl = "https://example.com/old.m3u8" }]
        });

        Assert.Equal(source.Id, Assert.Single(IptvCatalogService.GetEnabledSources(settings)).Id);
        Assert.Empty(catalog.BuildCatalog(settings));

        Assert.True(await service.RefreshSourceAsync(source));

        var channel = Assert.Single(catalog.BuildCatalog(settings));
        Assert.Equal(source.Id, channel.PlaylistId);
        Assert.Equal("New channel", channel.Title);
    }

    [Fact]
    public void Catalog_AutoFallbackVariants_ConservativeGrouping()
    {
        var store = new IptvPlayerStore(_tempDirectory);
        var catalogService = new IptvCatalogService(store);

        var source = new IptvPlaylistSource
        {
            Id = "src_1",
            Name = "Main Source",
            IsEnabled = true,
            AutoFallbackVariants = true
        };

        // Создаем каналы:
        // 1 и 2 имеют одинаковый tvg-id "espn" -> должны объединиться в 1 канал с 2 вариантами
        // 3 ("Discovery") и 4 ("Discovery HD") имеют разные имена и пустой tvg-id -> НЕ должны объединяться
        var cache = new IptvPlaylistCache
        {
            PlaylistId = source.Id,
            Channels =
            [
                new IptvCachedChannelEntry { Title = "ESPN 1080p", TvgId = "espn", Group = "Sports", StreamUrl = "http://espn.com/1080" },
                new IptvCachedChannelEntry { Title = "ESPN 720p", TvgId = "espn", Group = "Sports;Live", StreamUrl = "http://espn.com/720" },
                new IptvCachedChannelEntry { Title = "Discovery", TvgId = null, Group = "Doc", StreamUrl = "http://disc.com/sd" },
                new IptvCachedChannelEntry { Title = "Discovery HD", TvgId = null, Group = "Doc", StreamUrl = "http://disc.com/hd" }
            ]
        };
        store.SaveCache(cache);

        var settings = new IptvSettings { Sources = [source] };
        var catalog = catalogService.BuildCatalog(settings);

        // Должно получиться 3 канала (ESPN с 2 вариантами, Discovery, Discovery HD)
        Assert.Equal(3, catalog.Count);

        var espn = catalog.Find(c => c.TvgId == "espn");
        Assert.NotNull(espn);
        Assert.Equal(2, espn.Variants.Count);
        Assert.Equal("http://espn.com/1080", espn.Variants[0].StreamUrl);
        Assert.Equal("http://espn.com/720", espn.Variants[1].StreamUrl);
        Assert.Same(espn, Assert.Single(catalogService.FilterCatalog(catalog, null, "Live", null)));

        var disc1 = catalog.Find(c => c.Title == "Discovery");
        var disc2 = catalog.Find(c => c.Title == "Discovery HD");
        Assert.NotNull(disc1);
        Assert.NotNull(disc2);
        Assert.NotEqual(disc1.Id, disc2.Id);
    }

    [Fact]
    public void Catalog_FilterCatalog_AppliesFiltersCorrectly()
    {
        var store = new IptvPlayerStore(_tempDirectory);
        var catalogService = new IptvCatalogService(store);

        var channels = new List<IptvChannel>
        {
            new() { Id = "1", PlaylistId = "pl1", Title = "Eurosport 1", Group = "Sports", IsFavorite = true, SortOrder = 0 },
            new() { Id = "2", PlaylistId = "pl1", Title = "Eurosport 2", Group = "Sports", IsFavorite = false, SortOrder = 1 },
            new() { Id = "3", PlaylistId = "pl2", Title = "BBC News", Group = "News", IsFavorite = true, SortOrder = 2 },
            new() { Id = "4", PlaylistId = "pl2", Title = "CNN", Group = "News", IsFavorite = false, SortOrder = 3 }
        };

        // 1. Фильтр по плейлисту
        var pl1Only = catalogService.FilterCatalog(channels, "pl1", "ALL", null);
        Assert.Equal(2, pl1Only.Count);

        // 2. Фильтр по группе
        var sportsOnly = catalogService.FilterCatalog(channels, "ALL", "Sports", null);
        Assert.Equal(2, sportsOnly.Count);

        // 3. Фильтр по избранному
        var favOnly = catalogService.FilterCatalog(channels, "ALL", IptvCatalogService.FavoritesGroupKey, null);
        Assert.Equal(2, favOnly.Count);
        Assert.Contains(favOnly, c => c.Id == "1");
        Assert.Contains(favOnly, c => c.Id == "3");

        // 4. Поиск
        var searchResult = catalogService.FilterCatalog(channels, "ALL", "ALL", "euro");
        Assert.Equal(2, searchResult.Count);
    }

    [Fact]
    public void Catalog_DiagnosticDemoChannels_ReturnsValidTestStreams()
    {
        var demo = IptvCatalogService.GetDiagnosticDemoChannels();
        Assert.NotEmpty(demo);
        Assert.All(demo, ch =>
        {
            Assert.False(string.IsNullOrWhiteSpace(ch.Id));
            Assert.False(string.IsNullOrWhiteSpace(ch.Title));
            Assert.True(IptvPlaylistParser.IsValidStreamUrl(ch.StreamUrl, IptvSourceKind.Url));
        });
    }

    #endregion

    #region Service & Settings Normalizer Tests

    [Fact]
    public void SettingsNormalizer_EnsuresValidIntervalAndIds()
    {
        var raw = new IptvSettings
        {
            UpdateIntervalHours = 0, // Недопустимое значение (< 1)
            Sources =
            [
                new IptvPlaylistSource { Id = "", Name = "First" },
                new IptvPlaylistSource { Id = "dup", Name = "Second", SortOrder = 10 },
                new IptvPlaylistSource { Id = "dup", Name = "Third (Duplicate Id)" }
            ]
        };

        var normalized = IptvSettingsNormalizer.Normalize(raw, out bool changed);

        Assert.True(changed);
        Assert.Equal(24, normalized.UpdateIntervalHours);
        Assert.Equal(3, normalized.Sources.Count);
        Assert.Equal(0, normalized.Sources[0].SortOrder);
        Assert.Equal(1, normalized.Sources[1].SortOrder);
        Assert.Equal(2, normalized.Sources[2].SortOrder);
        Assert.NotEqual(normalized.Sources[1].Id, normalized.Sources[2].Id);
    }

    [Fact]
    public async Task PlaylistService_CheckSourceAsync_InlineTextSuccess()
    {
        var store = new IptvPlayerStore(_tempDirectory);
        using var service = new IptvPlaylistService(store);

        var source = new IptvPlaylistSource
        {
            Id = "inline_1",
            Name = "Inline Test",
            Kind = IptvSourceKind.InlineText,
            InlineContent = """
                #EXTM3U
                #EXTINF:-1,Test Channel 1
                http://example.com/stream1.m3u8
                #EXTINF:-1,Test Channel 2
                http://example.com/stream2.m3u8
                """
        };

        var result = await service.CheckSourceAsync(source);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.TotalCount);
        Assert.Equal("Test Channel 1", result.FirstChannels[0]);
    }

    [Fact]
    public async Task PlaylistService_CheckAndRefreshUseEditedUrlAndRefreshDownloadsOnce()
    {
        const string current = "https://203.0.113.10/list.m3u?token=edited";
        var store = new IptvPlayerStore(_tempDirectory);
        store.SaveSecretLocation("test", "https://203.0.113.10/list.m3u?token=old");
        var handler = new PlaylistHttpHandler();
        using var service = new IptvPlaylistService(store, handler);
        var source = new IptvPlaylistSource
        {
            Id = "test",
            Location = IptvPlaylistParser.MaskSensitiveUrl(current)
        };

        Assert.True((await service.CheckSourceAsync(source, current)).IsSuccess);
        Assert.Equal(current, Assert.Single(handler.Requests));
        handler.Requests.Clear();

        Assert.True(await service.RefreshSourceAsync(source, current));
        Assert.Equal(current, Assert.Single(handler.Requests));
        Assert.Equal("https://203.0.113.10/stream?token=stream-key",
            Assert.Single(store.LoadCache(source.Id)!.Channels).StreamUrl);
        Assert.DoesNotContain("stream-key", File.ReadAllText(store.GetCacheFilePath(source.Id)));
    }

    [Fact]
    public async Task PlaylistDownload_RejectsOversizedPayloadBeforeCaching()
    {
        using var client = new HttpClient(new PlaylistHttpHandler());
        await Assert.ThrowsAsync<InvalidOperationException>(() => IptvPlaylistService.DownloadRemoteBytesAsync(
            client, "https://203.0.113.10/logo.png", 4, CancellationToken.None));
    }

    private sealed class PlaylistHttpHandler : HttpMessageHandler
    {
        internal List<string> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Requests.Add(request.RequestUri!.AbsoluteUri);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("#EXTM3U\n#EXTINF:-1,Channel\nstream?token=stream-key")
            });
        }
    }

    [Fact]
    public async Task PlaylistService_RefreshSourceAsync_SavesCacheAndUpdatesMetadata()
    {
        var store = new IptvPlayerStore(_tempDirectory);
        using var service = new IptvPlaylistService(store);

        var source = new IptvPlaylistSource
        {
            Id = "single_stream_1",
            Name = "My Stream",
            Kind = IptvSourceKind.SingleStream,
            Location = "http://example.com/live.m3u8"
        };

        bool ok = await service.RefreshSourceAsync(source);

        Assert.True(ok);
        Assert.Equal("Ok", source.LastStatus);
        Assert.Equal(1, source.ChannelCount);
        Assert.NotNull(source.LastRefreshedUtc);

        var cache = store.LoadCache(source.Id);
        Assert.NotNull(cache);
        Assert.Single(cache.Channels);
        Assert.Equal("http://example.com/live.m3u8", cache.Channels[0].StreamUrl);
    }

    [Fact]
    public void PlaylistService_IsAutoUpdateDue_CalculatesInterval()
    {
        var store = new IptvPlayerStore(_tempDirectory);
        using var service = new IptvPlaylistService(store);

        var settings = new IptvSettings
        {
            AutoUpdateEnabled = true,
            UpdateIntervalHours = 24,
            LastAutoUpdateUtc = DateTime.UtcNow.AddHours(-25)
        };

        Assert.True(service.IsAutoUpdateDue(settings));

        settings.LastAutoUpdateUtc = DateTime.UtcNow.AddHours(-10);
        Assert.False(service.IsAutoUpdateDue(settings));

        settings.AutoUpdateEnabled = false;
        Assert.False(service.IsAutoUpdateDue(settings));
    }

    #endregion

    #region Window XAML Instantiation Tests

    [Theory]
    [InlineData(672, 554, 672, 378)]
    [InlineData(1600, 450, 800, 450)]
    [InlineData(800, 450, 800, 450)]
    [InlineData(640, 0, 0, 0)]
    public void VideoViewportFitsSixteenByNine(double width, double height, double expectedWidth, double expectedHeight)
    {
        var size = IptvVideoViewport.Fit(new System.Windows.Size(width, height));
        Assert.Equal(expectedWidth, size.Width, 5);
        Assert.Equal(expectedHeight, size.Height, 5);
    }

    [Theory]
    [InlineData(true, 820, 440, "820:440")]
    [InlineData(false, 820, 440, "")]
    [InlineData(true, 820, 0, "")]
    public void FillModeUsesTheViewportRatioWithoutAffectingFitMode(bool fill, double width, double height, string expected)
    {
        Assert.Equal(expected, IptvVideoViewport.GetCropGeometry(fill, width, height));
    }

    [Fact]
    public void ViewingModesRestoreThePreviousModeAndKeepTheWindowedPreference()
    {
        var modes = new IptvViewModes();
        modes.ToggleVideoOnly();
        modes.TogglePictureInPicture();
        Assert.Equal(IptvViewMode.PictureInPicture, modes.Current);

        modes.ToggleFullscreen();
        Assert.Equal(IptvViewMode.Fullscreen, modes.Current);
        modes.ToggleFullscreen();
        Assert.Equal(IptvViewMode.PictureInPicture, modes.Current);
        modes.ReturnToWindowed();
        Assert.Equal(IptvViewMode.VideoOnly, modes.Current);
        modes.ToggleVideoOnly();
        Assert.Equal(IptvViewMode.Standard, modes.Current);
    }

    [Fact]
    public void PictureInPicturePlacementIsClampedAndPersistedSeparately()
    {
        var area = new System.Windows.Rect(-1920, 24, 1920, 1000);
        var saved = new IptvPipPlacement("secondary", 3000, -200, 390, 280);
        var result = IptvPipLayout.Fit(area, 1.25, saved);
        Assert.Equal(area.Right, result.Right);
        Assert.Equal(area.Top, result.Top);
        Assert.Equal(487.5, result.Width);
        Assert.Equal(274.21875, result.Height, 6);
        Assert.Equal(new System.Windows.Rect(-650, 8, 640, 360),
            IptvPipLayout.Fit(new System.Windows.Rect(-650, 0, 640, 400), 2, null));

        var store = new IptvPlayerStore(_tempDirectory);
        store.SavePlayerState(new IptvPlayerState { IsSidebarOpen = true, PictureInPicturePlacement = saved });
        Assert.Equal(saved, store.LoadPlayerState().PictureInPicturePlacement);
        Assert.True(store.LoadPlayerState().IsSidebarOpen);
    }

    [Fact]
    public async Task QueuedTuningSkipsIntermediateChannelsAndPreservesCommandOrder()
    {
        var queue = new IptvPlaybackCommandQueue();
        using var hold = new ManualResetEventSlim();
        var applied = new List<string>();
        int requested = 0;
        Task first = queue.Enqueue(() =>
        {
            Assert.True(hold.Wait(TimeSpan.FromSeconds(10)));
            applied.Add("Previous stream stopped");
        });
        Task stale = queue.Enqueue(() => applied.Add("Intermediate channel opened"), () => Volatile.Read(ref requested) == 1);
        Task latest = queue.Enqueue(() => applied.Add("Latest channel opened"), () => Volatile.Read(ref requested) == 2);
        Volatile.Write(ref requested, 2);
        hold.Set();
        await Task.WhenAll(first, stale, latest).WaitAsync(TimeSpan.FromSeconds(15));
        Assert.Equal(["Previous stream stopped", "Latest channel opened"], applied);
    }

    [Fact]
    public async Task BrowsingChannelsLetsTheUserChooseQualityBeforePlayback()
    {
        WpfTestApplication.EnsureCreated();
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                PathHelper.SetAppDataFolderOverride(_tempDirectory);
                var window = new IptvPlayerWindow(null, null);
                try
                {
                    var channels = (System.Windows.Controls.ListBox)window.FindName("LstChannels");
                    var quality = (System.Windows.Controls.ComboBox)window.FindName("CmbVariants");
                    var title = (System.Windows.Controls.TextBlock)window.FindName("TxtActiveChannel");
                    var second = new IptvChannel
                    {
                        Id = "second",
                        Title = "Second",
                        Variants =
                        [
                            new() { Title = "SD", StreamUrl = Path.Combine(_tempDirectory, "missing-sd.ts") },
                            new() { Title = "HD", StreamUrl = Path.Combine(_tempDirectory, "missing-hd.ts") }
                        ]
                    };
                    channels.ItemsSource = new[] { new IptvChannel { Id = "first", Title = "First" }, second };
                    var video = (System.Windows.FrameworkElement)window.FindName("VlcVideoView");
                    Assert.Equal(System.Windows.Visibility.Hidden, video.Visibility);
                    var sidebar = (System.Windows.FrameworkElement)window.FindName("ChannelsSidebar");
                    var toggle = (System.Windows.Controls.Button)window.FindName("BtnToggleSidebar");
                    toggle.RaiseEvent(new System.Windows.RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                    Assert.Equal(System.Windows.Visibility.Collapsed, sidebar.Visibility);
                    Assert.Same(video, window.FindName("VlcVideoView"));
                    toggle.RaiseEvent(new System.Windows.RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                    Assert.Equal(System.Windows.Visibility.Visible, sidebar.Visibility);

                    channels.SelectedIndex = 0;
                    channels.SelectedIndex = 1;
                    quality.SelectedIndex = 1;

                    Assert.Equal("Second", title.Text);
                    Assert.Equal(1, second.ActiveVariantIndex);

                    channels.SelectedIndex = 0;
                    Assert.Equal("First", title.Text);
                    Assert.Equal(System.Windows.Visibility.Collapsed, quality.Visibility);
                    channels.SelectedIndex = 1;
                    Assert.Equal(1, quality.SelectedIndex);

                    var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                    var pendingTune = typeof(IptvPlayerWindow).GetField("_pendingTune", flags)!;
                    var navigate = typeof(IptvPlayerWindow).GetMethod("NavigateChannel", flags)!;
                    Assert.Null(pendingTune.GetValue(window));
                    navigate.Invoke(window, [1]);
                    Assert.Equal(0, channels.SelectedIndex);
                    Assert.Null(pendingTune.GetValue(window));
                    navigate.Invoke(window, [1]);
                    Assert.Equal(1, channels.SelectedIndex);
                    Assert.Null(pendingTune.GetValue(window));
                    navigate.Invoke(window, [-1]);
                    typeof(IptvPlayerWindow).GetMethod("ChannelTuneTimer_Tick", flags)!.Invoke(window, [null, EventArgs.Empty]);
                    Assert.Same(channels.Items[0], pendingTune.GetValue(window));
                }
                finally
                {
                    window.Close();
                }
                completion.SetResult();
            }
            catch (Exception ex)
            {
                completion.SetException(ex);
            }
            finally
            {
                PathHelper.ClearAppDataFolderOverride();
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        await completion.Task.WaitAsync(TimeSpan.FromSeconds(15));
    }

    [Fact]
    public void VideoDoubleClickConsumesPendingPauseAndTogglesFullscreen()
    {
        WpfTestApplication.EnsureCreated();

        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var window = new IptvPlayerWindow(null, null);
                try
                {
                    var surface = (System.Windows.FrameworkElement)window.FindName("VideoSurface");
                    var controls = (System.Windows.FrameworkElement)window.FindName("VideoControls");
                    Assert.True(surface.Focusable);
                    Assert.Equal(System.Windows.Visibility.Collapsed, controls.Visibility);

                    var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                    var playerType = typeof(IptvPlayerWindow);
                    var timer = Assert.IsType<System.Windows.Threading.DispatcherTimer>(
                        playerType.GetField("_videoClickTimer", flags)!.GetValue(window));
                    var modes = Assert.IsType<IptvViewModes>(playerType.GetField("_viewModes", flags)!.GetValue(window));
                    var completeClick = playerType.GetMethod("CompleteVideoClick", flags)!;
                    var resolveClick = playerType.GetMethod("ResolvePendingVideoClick", flags)!;

                    Assert.False(Assert.IsType<bool>(resolveClick.Invoke(window, [2])));
                    completeClick.Invoke(window, null);
                    Assert.True(timer.IsEnabled);
                    Assert.Equal(IptvViewMode.Standard, modes.Current);
                    Assert.True(Assert.IsType<bool>(resolveClick.Invoke(window, [2])));
                    Assert.False(timer.IsEnabled);
                    Assert.Equal(IptvViewMode.Fullscreen, modes.Current);

                    completeClick.Invoke(window, null);
                    Assert.True(Assert.IsType<bool>(resolveClick.Invoke(window, [2])));
                    Assert.False(timer.IsEnabled);
                    Assert.Equal(IptvViewMode.Standard, modes.Current);
                }
                finally
                {
                    window.Close();
                }
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(15)));
        Assert.Null(failure);
    }

    [Fact]
    public void IptvPlayerWindow_InstantiatesWithoutResourceException()
    {
        WpfTestApplication.EnsureCreated();

        string? title = null;
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var window = new IptvPlayerWindow(null, null);
                title = window.Title;
                window.Close();
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (failure != null)
        {
            throw new Xunit.Sdk.XunitException($"IptvPlayerWindow construction failed: {failure}");
        }

        Assert.Equal("IPTV Player", title);
    }

    [Fact]
    public void IptvPlaylistDialog_InstantiatesWithoutResourceException()
    {
        WpfTestApplication.EnsureCreated();

        string? title = null;
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var store = new IptvPlayerStore(_tempDirectory);
                using var service = new IptvPlaylistService(store);
                var dialog = new IptvPlaylistDialog(null, store, service);
                title = dialog.Title;
                dialog.Close();
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (failure != null)
        {
            throw new Xunit.Sdk.XunitException($"IptvPlaylistDialog construction failed: {failure}");
        }

        Assert.NotNull(title);
    }

    #endregion
}
