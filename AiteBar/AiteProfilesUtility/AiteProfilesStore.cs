using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace AiteBar.AiteProfilesUtility;

internal sealed class AiteProfilesStore
{
    private static readonly Regex ProfileNumberRegex = new(@"Profile\s+(\d+)", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private readonly IAiteProfilesScanner _scanner;
    private readonly string _favoritesPath;
    private readonly string _farmPath;
    private readonly string _tagsPath;
    private readonly string _cachePath;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private List<AiteProfile> _profiles = [];
    private HashSet<string> _favorites = new(StringComparer.Ordinal);
    private HashSet<string> _farm = new(StringComparer.Ordinal);
    private Dictionary<string, string> _tags = new(StringComparer.Ordinal);

    public AiteProfilesStore(IAiteProfilesScanner scanner, string rootDirectory)
    {
        _scanner = scanner ?? throw new ArgumentNullException(nameof(scanner));
        Directory.CreateDirectory(rootDirectory);
        _favoritesPath = Path.Combine(rootDirectory, "favorites.json");
        _farmPath = Path.Combine(rootDirectory, "farm.json");
        _tagsPath = Path.Combine(rootDirectory, "tags.json");
        _cachePath = Path.Combine(rootDirectory, "profiles_cache.json");
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _favorites = await LoadSetAsync(_favoritesPath, cancellationToken).ConfigureAwait(false);
            _farm = await LoadSetAsync(_farmPath, cancellationToken).ConfigureAwait(false);
            _tags = await LoadTagsAsync(cancellationToken).ConfigureAwait(false);
            RestoreProfilesFromCache(await LoadCacheAsync(cancellationToken).ConfigureAwait(false));
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyList<AiteProfile>> SnapshotProfilesAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return _profiles.Select(static profile => profile with { }).ToList();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task RefreshAsync(bool includeExpensiveStats, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            AiteProfilesCacheDocument cache = await LoadCacheAsync(cancellationToken).ConfigureAwait(false);
            IReadOnlyList<AiteProfileScanRow> rows = await _scanner.ScanAsync(cache.Profiles, includeExpensiveStats, cancellationToken).ConfigureAwait(false);
            ReplaceProfiles(rows);
            await SaveCacheAsync(rows, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task MarkFavoriteAsync(string folder, string path, bool value, CancellationToken cancellationToken = default)
    {
        string key = AiteProfileKey.Build(folder, path);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (value)
            {
                _favorites.Add(key);
            }
            else
            {
                _favorites.Remove(key);
            }

            _profiles = _profiles.Select(profile => string.Equals(AiteProfileKey.Build(profile.Folder, profile.Path), key, StringComparison.Ordinal)
                ? profile with { IsFavorite = value }
                : profile).ToList();
            await AiteProfilesJsonStore.WriteAsync(_favoritesPath, _favorites.Order(StringComparer.Ordinal).ToArray(), cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task MarkFarmAsync(string folder, string path, bool value, CancellationToken cancellationToken = default)
    {
        string key = AiteProfileKey.Build(folder, path);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (value)
            {
                _farm.Add(key);
            }
            else
            {
                _farm.Remove(key);
            }

            _profiles = _profiles.Select(profile => string.Equals(AiteProfileKey.Build(profile.Folder, profile.Path), key, StringComparison.Ordinal)
                ? profile with { IsFarm = value }
                : profile).ToList();
            await AiteProfilesJsonStore.WriteAsync(_farmPath, _farm.Order(StringComparer.Ordinal).ToArray(), cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SetTagsAsync(string folder, string path, string tagsText, CancellationToken cancellationToken = default)
    {
        string key = AiteProfileKey.Build(folder, path);
        string normalized = NormalizeTags(tagsText);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (string.IsNullOrWhiteSpace(normalized))
            {
                _tags.Remove(key);
            }
            else
            {
                _tags[key] = normalized;
            }

            _profiles = _profiles.Select(profile => string.Equals(AiteProfileKey.Build(profile.Folder, profile.Path), key, StringComparison.Ordinal)
                ? profile with { TagsText = normalized }
                : profile).ToList();
            await AiteProfilesJsonStore.WriteAsync(_tagsPath, _tags.OrderBy(static pair => pair.Key, StringComparer.Ordinal).ToDictionary(static pair => pair.Key, static pair => pair.Value, StringComparer.Ordinal), cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    internal static string NormalizeTags(string tagsText)
    {
        if (string.IsNullOrWhiteSpace(tagsText))
        {
            return string.Empty;
        }

        return string.Join(", ", tagsText
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase));
    }

    private static async Task<HashSet<string>> LoadSetAsync(string path, CancellationToken cancellationToken)
    {
        var data = await AiteProfilesJsonStore.ReadAsync<List<string>>(path, cancellationToken).ConfigureAwait(false);
        return new HashSet<string>(data?.Where(static value => !string.IsNullOrWhiteSpace(value)) ?? [], StringComparer.Ordinal);
    }

    private async Task<Dictionary<string, string>> LoadTagsAsync(CancellationToken cancellationToken)
    {
        var data = await AiteProfilesJsonStore.ReadAsync<Dictionary<string, string>>(_tagsPath, cancellationToken).ConfigureAwait(false);
        return data?
            .Where(static pair => !string.IsNullOrWhiteSpace(pair.Key))
            .ToDictionary(static pair => pair.Key, static pair => NormalizeTags(pair.Value), StringComparer.Ordinal)
            ?? new Dictionary<string, string>(StringComparer.Ordinal);
    }

    private async Task<AiteProfilesCacheDocument> LoadCacheAsync(CancellationToken cancellationToken)
    {
        AiteProfilesCacheDocument? cache = await AiteProfilesJsonStore.ReadAsync<AiteProfilesCacheDocument>(_cachePath, cancellationToken).ConfigureAwait(false);
        return cache ?? new AiteProfilesCacheDocument();
    }

    private void RestoreProfilesFromCache(AiteProfilesCacheDocument cache)
    {
        if (cache.Profiles.Count == 0)
        {
            _profiles = [];
            return;
        }

        ReplaceProfiles(cache.Profiles.Values.Select(static entry => new AiteProfileScanRow
        {
            Folder = entry.Folder,
            Name = entry.Name,
            Email = entry.Email,
            LastTs = entry.LastTs,
            Path = entry.Path,
            Sig = entry.Sig,
            Bookmarks = entry.Bookmarks,
            DiskMb = entry.DiskMb,
            DiskMbTs = entry.DiskMbTs,
            AvatarPath = entry.AvatarPath
        }).OrderBy(static row => ProfileSortKey(row.Folder).Group).ThenBy(static row => ProfileSortKey(row.Folder).Index).ToList());
    }

    private void ReplaceProfiles(IReadOnlyList<AiteProfileScanRow> rows)
    {
        _profiles = rows
            .Where(IsExistingProfilePath)
            .Select(row =>
        {
            string key = AiteProfileKey.Build(row.Folder, row.Path);
            return new AiteProfile
            {
                Folder = row.Folder,
                Name = string.IsNullOrWhiteSpace(row.Name) ? row.Folder : row.Name,
                Email = row.Email,
                LastTs = row.LastTs,
                Path = row.Path,
                IsFavorite = _favorites.Contains(key),
                IsFarm = _farm.Contains(key),
                TagsText = _tags.TryGetValue(key, out string? tags) ? tags : string.Empty,
                Bookmarks = row.Bookmarks,
                DiskMb = row.DiskMb,
                AvatarPath = row.AvatarPath,
                SearchKey = BuildSearchKey(row.Folder, row.Name, row.Email, row.Path, _tags.TryGetValue(key, out string? tagText) ? tagText : string.Empty)
            };
        }).ToList();
    }

    private async Task SaveCacheAsync(IReadOnlyList<AiteProfileScanRow> rows, CancellationToken cancellationToken)
    {
        var profiles = rows.Where(IsExistingProfilePath).ToDictionary(
            row => AiteProfileKey.Build(row.Folder, row.Path),
            row => new AiteProfileCacheEntry
            {
                Folder = row.Folder,
                Name = row.Name,
                Email = row.Email,
                LastTs = row.LastTs,
                Path = row.Path,
                Sig = row.Sig,
                Bookmarks = row.Bookmarks,
                DiskMb = row.DiskMb,
                DiskMbTs = row.DiskMbTs,
                AvatarPath = row.AvatarPath
            },
            StringComparer.Ordinal);
        await AiteProfilesJsonStore.WriteAsync(_cachePath, new AiteProfilesCacheDocument { Profiles = profiles }, cancellationToken).ConfigureAwait(false);
    }

    private static string BuildSearchKey(string folder, string name, string email, string path, string tags) =>
        $"{folder} {name} {email} {path} {tags}".ToLowerInvariant().Trim();

    private static bool IsExistingProfilePath(AiteProfileScanRow row)
    {
        return !string.IsNullOrWhiteSpace(row.Folder) &&
               !string.IsNullOrWhiteSpace(row.Path) &&
               Directory.Exists(row.Path);
    }

    private static (int Group, int Index) ProfileSortKey(string folder)
    {
        if (string.Equals(folder, "Default", StringComparison.OrdinalIgnoreCase))
        {
            return (0, 0);
        }

        Match match = ProfileNumberRegex.Match(folder);
        return match.Success && int.TryParse(match.Groups[1].Value, out int number) ? (1, number) : (2, int.MaxValue);
    }
}
