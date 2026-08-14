using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace AiteBar.AiteProfilesUtility;

internal interface IAiteProfilesScanner
{
    Task<IReadOnlyList<AiteProfileScanRow>> ScanAsync(
        IReadOnlyDictionary<string, AiteProfileCacheEntry>? cache = null,
        bool includeExpensiveStats = true,
        CancellationToken cancellationToken = default);
}

internal sealed class AiteProfilesChromeScanner : IAiteProfilesScanner
{
    private static readonly Regex ProfileNumberRegex = new(@"Profile\s+(\d+)", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly string[] SignatureFiles =
    [
        "Preferences",
        "Secure Preferences",
        "Bookmarks",
        "AccountBookmarks",
        "Google Profile Picture.png"
    ];
    private static readonly string[] LastLaunchFiles = ["Preferences", "Secure Preferences", "History", "Bookmarks"];
    private static readonly string[] BookmarkFiles = ["Bookmarks", "AccountBookmarks"];
    private static readonly HashSet<string> ExcludedProfileFolders = new(StringComparer.OrdinalIgnoreCase)
    {
        "System Profile",
        "Guest Profile",
        "Crashpad",
        "CertificateRevocation"
    };

    private static readonly HashSet<string> DefaultProfileNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Your Chrome",
        "Ваш Chrome",
        "Profile",
        "Профиль",
        "Default",
        "Person"
    };

    public Task<IReadOnlyList<AiteProfileScanRow>> ScanAsync(
        IReadOnlyDictionary<string, AiteProfileCacheEntry>? cache = null,
        bool includeExpensiveStats = true,
        CancellationToken cancellationToken = default) =>
        Task.Run(() => ScanCore(cache, includeExpensiveStats, cancellationToken), cancellationToken);

    private static IReadOnlyList<AiteProfileScanRow> ScanCore(
        IReadOnlyDictionary<string, AiteProfileCacheEntry>? cache,
        bool includeExpensiveStats,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string chromeUserData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Google", "Chrome", "User Data");
        if (!Directory.Exists(chromeUserData))
        {
            return [];
        }

        Dictionary<string, string> names = GetProfileNames(chromeUserData);
        var knownProfileFolders = new HashSet<string>(names.Keys, StringComparer.OrdinalIgnoreCase);
        var profileDirs = new List<string>();
        foreach (string dir in Directory.EnumerateDirectories(chromeUserData))
        {
            cancellationToken.ThrowIfCancellationRequested();
            string folder = Path.GetFileName(dir) ?? string.Empty;
            if (IsProfileDirectory(folder, dir, knownProfileFolders))
            {
                profileDirs.Add(dir);
            }
        }

        var bag = new ConcurrentBag<AiteProfileScanRow>();
        Parallel.ForEach(profileDirs, new ParallelOptions
        {
            CancellationToken = cancellationToken,
            MaxDegreeOfParallelism = Math.Min(Environment.ProcessorCount, 4)
        }, dir =>
        {
            string folder = Path.GetFileName(dir) ?? string.Empty;
            bag.Add(BuildRow(dir, folder, names, cache, includeExpensiveStats));
        });

        return bag
            .OrderBy(static row => ProfileSortKey(row.Folder).Group)
            .ThenBy(static row => ProfileSortKey(row.Folder).Index)
            .ThenBy(static row => row.Folder, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool IsProfileDirectory(string folder, string fullPath, IReadOnlySet<string> knownProfileFolders)
    {
        if (string.IsNullOrWhiteSpace(folder) || ExcludedProfileFolders.Contains(folder))
        {
            return false;
        }

        if (string.Equals(folder, "Default", StringComparison.OrdinalIgnoreCase) || knownProfileFolders.Contains(folder) || ProfileNumberRegex.IsMatch(folder))
        {
            return HasProfileMetadata(fullPath);
        }

        return false;
    }

    private static bool HasProfileMetadata(string fullPath) =>
        File.Exists(Path.Combine(fullPath, "Preferences")) ||
        File.Exists(Path.Combine(fullPath, "Secure Preferences"));

    private static AiteProfileScanRow BuildRow(
        string fullPath,
        string folder,
        IReadOnlyDictionary<string, string> names,
        IReadOnlyDictionary<string, AiteProfileCacheEntry>? cache,
        bool includeExpensiveStats)
    {
        string key = AiteProfileKey.Build(folder, fullPath);
        AiteProfileCacheEntry? cached = null;
        cache?.TryGetValue(key, out cached);
        string sig = BuildSignature(fullPath);
        bool cacheHit = cached is not null && string.Equals(cached.Sig, sig, StringComparison.Ordinal);

        try
        {
            string email = cacheHit ? cached!.Email : ReadProfileEmail(fullPath);
            string gaiaFallback = names.TryGetValue(folder, out string? profileName) && !IsDefaultProfileName(profileName) ? profileName : string.Empty;
            string prefsName = cacheHit ? string.Empty : ReadProfileDisplayName(fullPath);
            string cachedName = cacheHit && !IsDefaultProfileName(cached!.Name) ? cached.Name : string.Empty;

            string name = gaiaFallback;
            if (string.IsNullOrWhiteSpace(name))
            {
                name = cachedName;
            }
            if (string.IsNullOrWhiteSpace(name))
            {
                name = prefsName;
            }
            if (string.IsNullOrWhiteSpace(name) && email.Contains('@', StringComparison.Ordinal))
            {
                name = email.Split('@', 2)[0];
            }

            int bookmarks = cacheHit ? cached!.Bookmarks : GetBookmarksCount(fullPath);
            string avatarPath = ResolveAvatarPath(fullPath);
            double diskMb = cached?.DiskMb ?? -1.0;
            double diskMbTs = cached?.DiskMbTs ?? 0;
            if (includeExpensiveStats && (!cacheHit || diskMb < 0))
            {
                diskMb = GetDiskSizeMb(fullPath);
                diskMbTs = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            }

            return new AiteProfileScanRow
            {
                Folder = folder,
                Name = string.IsNullOrWhiteSpace(name) ? folder : name,
                Email = email,
                LastTs = GetLastLaunchTs(fullPath),
                Path = fullPath,
                Sig = sig,
                Bookmarks = bookmarks,
                DiskMb = diskMb,
                DiskMbTs = diskMbTs,
                AvatarPath = avatarPath
            };
        }
        catch (Exception ex)
        {
            Logger.Log(ex);
            string email = cached?.Email ?? string.Empty;
            string gaiaFallback = names.TryGetValue(folder, out string? profileName) && !IsDefaultProfileName(profileName) ? profileName : string.Empty;
            string name = gaiaFallback;
            if (string.IsNullOrWhiteSpace(name) && !IsDefaultProfileName(cached?.Name ?? string.Empty))
            {
                name = cached!.Name;
            }
            if (string.IsNullOrWhiteSpace(name) && email.Contains('@', StringComparison.Ordinal))
            {
                name = email.Split('@', 2)[0];
            }
            return new AiteProfileScanRow
            {
                Folder = folder,
                Name = string.IsNullOrWhiteSpace(name) ? folder : name,
                Email = email,
                LastTs = cached?.LastTs ?? 0,
                Path = fullPath,
                Sig = sig,
                Bookmarks = cached?.Bookmarks ?? -1,
                DiskMb = cached?.DiskMb ?? -1,
                DiskMbTs = cached?.DiskMbTs ?? 0,
                AvatarPath = ResolveAvatarPath(fullPath)
            };
        }
    }

    private static Dictionary<string, string> GetProfileNames(string chromeUserData)
    {
        try
        {
            JsonObject? root = ReadJsonObject(Path.Combine(chromeUserData, "Local State"));
            JsonObject? infoCache = root?["profile"]?["info_cache"] as JsonObject;
            if (infoCache is null)
            {
                return new Dictionary<string, string>(StringComparer.Ordinal);
            }

            var output = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach ((string folder, JsonNode? value) in infoCache)
            {
                if (value is not JsonObject meta)
                {
                    continue;
                }

                string name = ToText(meta["gaia_name"]);
                if (string.IsNullOrWhiteSpace(name) || IsDefaultProfileName(name))
                {
                    name = ToText(meta["gaia_given_name"]);
                }

                if (string.IsNullOrWhiteSpace(name) || IsDefaultProfileName(name))
                {
                    string rawName = ToText(meta["name"]);
                    if (!IsDefaultProfileName(rawName))
                    {
                        name = rawName;
                    }
                }

                if (!string.IsNullOrWhiteSpace(name))
                {
                    output[folder] = name;
                }
            }

            return output;
        }
        catch (Exception ex)
        {
            Logger.Log(ex);
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }
    }

    private static string ReadProfileEmail(string profilePath)
    {
        var candidates = new List<string>();
        foreach (string filename in new[] { "Preferences", "Secure Preferences" })
        {
            JsonObject? root = ReadJsonObject(Path.Combine(profilePath, filename));
            AddCandidate(candidates, root?["account_info"]?[0]?["email"]);
            AddCandidate(candidates, root?["profile"]?["user_name"]);
            AddCandidate(candidates, root?["signin"]?["allowed_username"]);
        }

        return candidates.FirstOrDefault(static value => value.Contains('@', StringComparison.Ordinal)) ?? string.Empty;
    }

    private static string ReadProfileDisplayName(string profilePath)
    {
        var candidates = new List<string>();
        foreach (string filename in new[] { "Preferences", "Secure Preferences" })
        {
            JsonObject? root = ReadJsonObject(Path.Combine(profilePath, filename));
            AddCandidate(candidates, root?["account_info"]?[0]?["full_name"]);
            AddCandidate(candidates, root?["account_info"]?[0]?["given_name"]);
            AddCandidate(candidates, root?["profile"]?["name"]);
        }

        return candidates.FirstOrDefault(static value => !string.IsNullOrWhiteSpace(value) && !IsDefaultProfileName(value)) ?? string.Empty;
    }

    private static bool IsDefaultProfileName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return true;
        }

        string trimmed = name.Trim();
        if (DefaultProfileNames.Contains(trimmed))
        {
            return true;
        }

        if (Regex.IsMatch(trimmed, @"^(Profile|Профиль)\s*\d*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            return true;
        }

        return false;
    }

    private static int GetBookmarksCount(string profilePath)
    {
        foreach (string filename in BookmarkFiles)
        {
            int? count = ReadBookmarks(Path.Combine(profilePath, filename));
            if (count.HasValue)
            {
                return count.Value;
            }
        }

        return -1;
    }

    private static int? ReadBookmarks(string path)
    {
        try
        {
            JsonObject? root = ReadJsonObject(path);
            if (root is null)
            {
                return null;
            }

            int count = 0;
            CountBookmarkNodes(root["roots"], ref count);
            return count;
        }
        catch (Exception ex)
        {
            Logger.Log(ex);
            return null;
        }
    }

    private static void CountBookmarkNodes(JsonNode? node, ref int count)
    {
        if (node is JsonObject obj)
        {
            string type = ToText(obj["type"]);
            if (string.Equals(type, "url", StringComparison.OrdinalIgnoreCase))
            {
                count++;
            }

            if (obj["children"] is JsonArray children)
            {
                foreach (JsonNode? child in children)
                {
                    CountBookmarkNodes(child, ref count);
                }
            }

            foreach (KeyValuePair<string, JsonNode?> child in obj)
            {
                if (!string.Equals(child.Key, "children", StringComparison.Ordinal))
                {
                    CountBookmarkNodes(child.Value, ref count);
                }
            }
        }
        else if (node is JsonArray array)
        {
            foreach (JsonNode? child in array)
            {
                CountBookmarkNodes(child, ref count);
            }
        }
    }

    private static double GetDiskSizeMb(string profilePath)
    {
        try
        {
            long totalBytes = 0;
            var stack = new Stack<string>();
            stack.Push(profilePath);
            while (stack.Count > 0)
            {
                string current = stack.Pop();
                foreach (string file in Directory.EnumerateFiles(current))
                {
                    try
                    {
                        totalBytes += new FileInfo(file).Length;
                    }
                    catch
                    {
                    }
                }

                foreach (string dir in Directory.EnumerateDirectories(current))
                {
                    stack.Push(dir);
                }
            }

            return totalBytes / 1024.0 / 1024.0;
        }
        catch (Exception ex)
        {
            Logger.Log(ex);
            return -1;
        }
    }

    private static long GetLastLaunchTs(string profilePath)
    {
        long max = 0;
        foreach (string filename in LastLaunchFiles)
        {
            string path = Path.Combine(profilePath, filename);
            try
            {
                if (File.Exists(path))
                {
                    max = Math.Max(max, new DateTimeOffset(File.GetLastWriteTimeUtc(path)).ToUnixTimeSeconds());
                }
            }
            catch
            {
            }
        }

        return max;
    }

    private static string BuildSignature(string profilePath)
    {
        var parts = new List<string>(SignatureFiles.Length);
        foreach (string file in SignatureFiles)
        {
            string path = Path.Combine(profilePath, file);
            try
            {
                if (File.Exists(path))
                {
                    var info = new FileInfo(path);
                    parts.Add($"{file}:{info.Length}:{info.LastWriteTimeUtc.Ticks}");
                }
            }
            catch
            {
            }
        }

        return string.Join("|", parts);
    }

    private static string ResolveAvatarPath(string profilePath)
    {
        foreach (string file in new[] { "Google Profile Picture.png", "Avatar.png" })
        {
            string path = Path.Combine(profilePath, file);
            if (File.Exists(path))
            {
                return path;
            }
        }

        return string.Empty;
    }

    private static JsonObject? ReadJsonObject(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        byte[] data = ReadSharedFileBytes(path);
        return JsonNode.Parse(data) as JsonObject;
    }

    private static byte[] ReadSharedFileBytes(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 81920, FileOptions.SequentialScan);
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }

    private static void AddCandidate(List<string> candidates, JsonNode? node)
    {
        string value = ToText(node).Trim();
        if (!string.IsNullOrWhiteSpace(value))
        {
            candidates.Add(value);
        }
    }

    private static string ToText(JsonNode? node) => node?.ToString() ?? string.Empty;

    private static (int Group, int Index) ProfileSortKey(string folder)
    {
        if (string.Equals(folder, "Default", StringComparison.OrdinalIgnoreCase))
        {
            return (0, 0);
        }

        Match match = ProfileNumberRegex.Match(folder);
        if (match.Success && int.TryParse(match.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int number))
        {
            return (1, number);
        }

        return (2, int.MaxValue);
    }
}

internal sealed class AiteProfilesChromeLauncher
{
    public AiteProfilesChromeLauncher(string? chromeExeOverride = null)
    {
        ChromeExe = chromeExeOverride ?? FindChromeExe();
    }

    public string? ChromeExe { get; }
    public bool IsChromeAvailable => !string.IsNullOrWhiteSpace(ChromeExe) && File.Exists(ChromeExe);

    public void OpenProfile(string folder) => StartChrome($"--profile-directory={folder}", "--start-maximized");

    public void OpenProfileIncognito(string folder) => StartChrome("--incognito", $"--profile-directory={folder}", "--start-maximized");

    public void OpenProfilePicker() => StartChrome("--profile-directory=Default", "--profile-picker", "--start-maximized");

    public void OpenGmailCompose(string folder) => OpenUrlInProfile(folder, "https://mail.google.com/mail/u/0/#inbox?compose=new");

    public void OpenGmail(string folder) => OpenUrlInProfile(folder, "https://mail.google.com/");

    public void OpenGoogleDrive(string folder) => OpenUrlInProfile(folder, "https://drive.google.com/");

    public void OpenGemini(string folder) => OpenUrlInProfile(folder, "https://gemini.google.com/app");

    public void OpenGoogleAccountSettings(string folder) => OpenUrlInProfile(folder, "https://myaccount.google.com/");

    public void OpenUrlsInProfile(string folder, IReadOnlyList<string> urls)
    {
        if (urls.Count == 0)
        {
            OpenProfile(folder);
            return;
        }

        StartChrome([ $"--profile-directory={folder}", "--start-maximized", .. urls ]);
    }

    public void OpenFolder(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
        {
            throw new DirectoryNotFoundException(path);
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = path,
            UseShellExecute = true
        });
    }

    private void OpenUrlInProfile(string folder, string url) => StartChrome($"--profile-directory={folder}", "--start-maximized", url);

    private void StartChrome(params string[] arguments)
    {
        if (!IsChromeAvailable)
        {
            throw new InvalidOperationException(LocalizationService.Get("AiteProfiles_ChromeMissing"));
        }

        var processStartInfo = new ProcessStartInfo
        {
            FileName = ChromeExe!,
            UseShellExecute = false
        };

        foreach (string argument in arguments.Where(static value => !string.IsNullOrWhiteSpace(value)))
        {
            processStartInfo.ArgumentList.Add(argument);
        }

        _ = Process.Start(processStartInfo);
    }

    private static string? FindChromeExe()
    {
        string[] candidates =
        [
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Google", "Chrome", "Application", "chrome.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Google", "Chrome", "Application", "chrome.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Google", "Chrome", "Application", "chrome.exe")
        ];

        return candidates.FirstOrDefault(static candidate => !string.IsNullOrWhiteSpace(candidate) && File.Exists(candidate));
    }
}
