using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;

namespace AiteBar.IptvPlayer;

[SupportedOSPlatform("windows6.1")]
public sealed class IptvCatalogService
{
    public const string FavoritesGroupKey = "__FAVORITES__";

    private readonly IptvPlayerStore _store;

    public IptvCatalogService(IptvPlayerStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public List<IptvChannel> BuildCatalog(
        IptvSettings settings,
        bool includeTestStreams = false)
    {
        var result = new List<IptvChannel>();
        var favorites = _store.LoadFavorites();
        var favLookup = new HashSet<string>(favorites.Select(f => f.ChannelKey), StringComparer.OrdinalIgnoreCase);

        if (includeTestStreams)
        {
            var testChannels = GetDiagnosticDemoChannels();
            foreach (var ch in testChannels)
            {
                ch.IsFavorite = favLookup.Contains(ch.Id);
                result.Add(ch);
            }
        }

        if (settings?.Sources == null || settings.Sources.Count == 0)
        {
            return result;
        }

        int globalOrder = result.Count;

        foreach (var source in GetEnabledSources(settings))
        {
            var cache = _store.LoadCache(source.Id);
            if (cache?.Channels == null || cache.Channels.Count == 0 || cache.SourceHash != source.ContentRevision)
            {
                continue;
            }

            if (source.AutoFallbackVariants)
            {
                // Консервативное объединение вариантов: только при совпадении непустого tvg-id
                // или строгого совпадения (Group + Title) внутри одного источника
                var grouped = GroupIntoVariants(cache.Channels, source);
                foreach (var ch in grouped)
                {
                    ch.SortOrder = globalOrder++;
                    ch.IsFavorite = favLookup.Contains(ch.Id);
                    result.Add(ch);
                }
            }
            else
            {
                foreach (var entry in cache.Channels)
                {
                    string id = ComputeChannelKey(source.Id, entry.TvgId, entry.Title, entry.StreamUrl);
                    var ch = new IptvChannel
                    {
                        Id = id,
                        PlaylistId = source.Id,
                        PlaylistName = source.Name,
                        Title = entry.Title,
                        CleanTitle = IptvPlaylistParser.CleanChannelTitle(entry.Title),
                        OriginalTitle = entry.Title,
                        Group = string.IsNullOrWhiteSpace(entry.Group) ? "Общие" : entry.Group,
                        LogoUrl = entry.LogoUrl,
                        TvgId = entry.TvgId,
                        TvgName = entry.TvgName,
                        StreamUrl = entry.StreamUrl,
                        Variants =
                        [
                            new IptvChannelVariant
                            {
                                StreamUrl = entry.StreamUrl,
                                Title = "Default",
                                Order = 0
                            }
                        ],
                        ActiveVariantIndex = 0,
                        IsFavorite = favLookup.Contains(id),
                        SortOrder = globalOrder++
                    };
                    result.Add(ch);
                }
            }
        }

        foreach (var channel in result)
        {
            channel.AvailableResolutions = IptvResolutionLabels.Read(channel.Title);
            foreach (var variant in channel.Variants)
                channel.AvailableResolutions |= IptvResolutionLabels.Read(variant.Title);
        }
        SetDuplicateChannelLabels(result);
        return result;
    }

    internal static void SetDuplicateChannelLabels(IEnumerable<IptvChannel> channels)
    {
        foreach (var matches in channels.GroupBy(channel => channel.Title.Trim(), StringComparer.OrdinalIgnoreCase))
        {
            if (matches.Count() < 2) continue;

            bool differentSources = matches.Select(channel => channel.PlaylistId).Distinct(StringComparer.OrdinalIgnoreCase).Skip(1).Any();
            foreach (var channel in matches)
            {
                channel.SecondaryText = differentSources && !string.IsNullOrWhiteSpace(channel.PlaylistName)
                    ? channel.PlaylistName : string.Join(" · ", channel.GroupNames);
            }
        }
    }

    internal static List<IptvPlaylistSource> GetEnabledSources(IptvSettings settings) =>
        settings.Sources.Where(source => source.IsEnabled).OrderBy(source => source.SortOrder).ToList();

    internal static IEnumerable<string> GetGroupNames(IEnumerable<IptvChannel> catalog) =>
        catalog.SelectMany(channel => channel.GroupNames)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group, StringComparer.CurrentCultureIgnoreCase);

    public List<IptvChannel> FilterCatalog(
        List<IptvChannel> catalog,
        string? playlistFilterId,
        string? groupFilter,
        string? searchQuery,
        bool favoritesOnly = false,
        IptvResolution resolution = IptvResolution.Any)
        => FilterChannels(catalog, playlistFilterId, groupFilter, searchQuery, favoritesOnly, resolution)
            .OrderBy(channel => channel.SortOrder).ToList();

    // BuildCatalog appends channels in SortOrder, so the browser can preserve that order.
    internal static IEnumerable<IptvChannel> FilterChannels(
        List<IptvChannel> catalog,
        string? playlistFilterId,
        string? groupFilter,
        string? searchQuery,
        bool favoritesOnly,
        IptvResolution resolution)
    {
        if (catalog == null || catalog.Count == 0)
        {
            return [];
        }

        IEnumerable<IptvChannel> query = catalog;
        if (favoritesOnly) query = query.Where(channel => channel.IsFavorite);
        if (resolution != IptvResolution.Any)
            query = query.Where(channel => (channel.AvailableResolutions & resolution) != 0);

        // Фильтр по плейлисту
        if (!string.IsNullOrWhiteSpace(playlistFilterId) && playlistFilterId != "ALL")
        {
            query = query.Where(c => string.Equals(c.PlaylistId, playlistFilterId, StringComparison.OrdinalIgnoreCase));
        }

        // Фильтр по группе или Избранному
        if (!string.IsNullOrWhiteSpace(groupFilter) && groupFilter != "ALL")
        {
            if (string.Equals(groupFilter, FavoritesGroupKey, StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(c => c.IsFavorite);
            }
            else
            {
                query = query.Where(channel => channel.GroupNames.Contains(groupFilter, StringComparer.OrdinalIgnoreCase));
            }
        }

        // Фильтр по поисковому запросу
        if (!string.IsNullOrWhiteSpace(searchQuery))
        {
            string needle = searchQuery.Trim();
            query = query.Where(c =>
                c.Title.Contains(needle, StringComparison.OrdinalIgnoreCase) ||
                c.Group.Contains(needle, StringComparison.OrdinalIgnoreCase) ||
                c.GroupNames.Any(group => group.Contains(needle, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrWhiteSpace(c.TvgName) && c.TvgName.Contains(needle, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrWhiteSpace(c.CleanTitle) && c.CleanTitle.Contains(needle, StringComparison.OrdinalIgnoreCase)));
        }

        return query;
    }

    public void ToggleFavorite(IptvChannel channel)
    {
        if (channel == null || string.IsNullOrWhiteSpace(channel.Id))
        {
            return;
        }

        var favorites = _store.LoadFavorites();
        int existingIndex = favorites.FindIndex(f => string.Equals(f.ChannelKey, channel.Id, StringComparison.OrdinalIgnoreCase));

        if (existingIndex >= 0)
        {
            favorites.RemoveAt(existingIndex);
            channel.IsFavorite = false;
        }
        else
        {
            favorites.Add(new IptvFavorite
            {
                ChannelKey = channel.Id,
                PlaylistId = channel.PlaylistId,
                TvgId = channel.TvgId,
                Title = channel.Title,
                AddedAtUtc = DateTime.UtcNow
            });
            channel.IsFavorite = true;
        }

        _store.SaveFavorites(favorites);
    }

    private static List<IptvChannel> GroupIntoVariants(List<IptvCachedChannelEntry> entries, IptvPlaylistSource source)
    {
        var result = new List<IptvChannel>();
        // Ключ группировки: если есть непустой tvg-id, то "tvg:" + tvgId, иначе "name:" + Group + "|" + CleanTitle
        var dict = new Dictionary<string, List<IptvCachedChannelEntry>>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in entries)
        {
            string key;
            if (!string.IsNullOrWhiteSpace(entry.TvgId))
            {
                key = $"tvg:{entry.TvgId.Trim()}";
            }
            else
            {
                // Консервативное правило: без tvg-id объединяются только записи с одинаковым названием
                key = $"name:{entry.Group.Trim()}|{entry.Title.Trim()}";
            }

            if (!dict.TryGetValue(key, out var list))
            {
                list = [];
                dict[key] = list;
            }
            list.Add(entry);
        }

        foreach (var pair in dict)
        {
            var list = pair.Value;
            var primary = list[0];
            string id = ComputeChannelKey(source.Id, primary.TvgId, primary.Title, primary.StreamUrl);

            var channel = new IptvChannel
            {
                Id = id,
                PlaylistId = source.Id,
                PlaylistName = source.Name,
                Title = primary.Title,
                CleanTitle = IptvPlaylistParser.CleanChannelTitle(primary.Title),
                OriginalTitle = primary.Title,
                Group = string.IsNullOrWhiteSpace(primary.Group) ? "Общие" : primary.Group,
                GroupNames = list.SelectMany(entry => IptvChannel.SplitGroupNames(
                        string.IsNullOrWhiteSpace(entry.Group) ? "Общие" : entry.Group))
                    .Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
                LogoUrl = primary.LogoUrl,
                TvgId = primary.TvgId,
                TvgName = primary.TvgName,
                StreamUrl = primary.StreamUrl,
                Variants = [],
                ActiveVariantIndex = 0
            };

            for (int i = 0; i < list.Count; i++)
            {
                var item = list[i];
                string variantTitle = list.Count > 1
                    ? (string.IsNullOrWhiteSpace(item.Title) ? $"Variant {i + 1}" : item.Title)
                    : "Default";

                channel.Variants.Add(new IptvChannelVariant
                {
                    StreamUrl = item.StreamUrl,
                    Title = variantTitle,
                    Order = i
                });
            }

            result.Add(channel);
        }

        return result;
    }

    public static string ComputeChannelKey(string playlistId, string? tvgId, string title, string streamUrl)
    {
        string raw = $"{playlistId}|{tvgId?.Trim().ToLowerInvariant()}|{title.Trim().ToLowerInvariant()}|{streamUrl.Trim()}";
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(hash)[..16].ToLowerInvariant();
    }

    public static List<IptvChannel> GetDiagnosticDemoChannels()
    {
        return
        [
            new IptvChannel
            {
                Id = "demo_bigbuckbunny_hls",
                PlaylistId = "DEMO",
                PlaylistName = "Test Streams",
                Title = "Big Buck Bunny (HLS 1080p)",
                CleanTitle = "Big Buck Bunny",
                OriginalTitle = "Big Buck Bunny (HLS 1080p)",
                Group = "Demo / Test",
                StreamUrl = "https://test-streams.mux.dev/x36xhzz/x36xhzz.m3u8",
                Variants =
                [
                    new IptvChannelVariant
                    {
                        StreamUrl = "https://test-streams.mux.dev/x36xhzz/x36xhzz.m3u8",
                        Title = "Multi-bitrate HLS",
                        Order = 0
                    }
                ]
            },
            new IptvChannel
            {
                Id = "demo_tears_of_steel_hls",
                PlaylistId = "DEMO",
                PlaylistName = "Test Streams",
                Title = "Tears of Steel (HLS)",
                CleanTitle = "Tears of Steel",
                OriginalTitle = "Tears of Steel (HLS)",
                Group = "Demo / Test",
                StreamUrl = "https://demo.unified-streaming.com/k8s/features/stable/video/tears-of-steel/tears-of-steel.ism/.m3u8",
                Variants =
                [
                    new IptvChannelVariant
                    {
                        StreamUrl = "https://demo.unified-streaming.com/k8s/features/stable/video/tears-of-steel/tears-of-steel.ism/.m3u8",
                        Title = "Unified Streaming HLS",
                        Order = 0
                    }
                ]
            },
            new IptvChannel
            {
                Id = "demo_sintel_trailer",
                PlaylistId = "DEMO",
                PlaylistName = "Test Streams",
                Title = "Sintel Trailer (MP4)",
                CleanTitle = "Sintel Trailer",
                OriginalTitle = "Sintel Trailer (MP4)",
                Group = "Demo / Test",
                StreamUrl = "https://commondatastorage.googleapis.com/gtv-videos-bucket/sample/Sintel.mp4",
                Variants =
                [
                    new IptvChannelVariant
                    {
                        StreamUrl = "https://commondatastorage.googleapis.com/gtv-videos-bucket/sample/Sintel.mp4",
                        Title = "Direct MP4",
                        Order = 0
                    }
                ]
            }
        ];
    }
}
