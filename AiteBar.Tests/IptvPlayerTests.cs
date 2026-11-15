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
                new IptvCachedChannelEntry { Title = "ESPN 720p", TvgId = "espn", Group = "Sports", StreamUrl = "http://espn.com/720" },
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
