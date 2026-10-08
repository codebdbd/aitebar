using System.Collections.Concurrent;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AiteBar;

[SupportedOSPlatform("windows")]
public static class InstalledAppsService
{
    private static readonly string[] StartMenuDirs =
    [
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu), "Programs"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs")
    ];

    private static readonly HashSet<string> ValidProgramExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".bat", ".cmd", ".ps1", ".msc", ".cpl"
    };

    private static readonly HashSet<string> DocumentExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".url", ".htm", ".html", ".pdf", ".chm", ".hlp", ".txt", ".rtf",
        ".doc", ".docx", ".cer", ".crt", ".pfx", ".xml", ".json", ".ico", ".png", ".jpg"
    };

    private static readonly Dictionary<string, string> KnownFolders = new(StringComparer.OrdinalIgnoreCase)
    {
        ["{1AC14E77-02E7-4E5D-B744-2EB1AE5198B7}"] = Environment.GetFolderPath(Environment.SpecialFolder.System),
        ["{6D809377-6AF0-444B-8957-A3773F02200E}"] = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
        ["{7C5A40EF-A0FB-4BFC-874A-C0F2E0B9FA8E}"] = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
        ["{D65231B0-B2F1-4857-A4CE-A8E7C6EA7D27}"] = Environment.GetFolderPath(Environment.SpecialFolder.SystemX86),
        ["{F38BF404-1D43-42F2-9305-67DE0B28FC23}"] = Environment.GetFolderPath(Environment.SpecialFolder.Windows)
    };

    private static readonly object LockObj = new();
    private static List<InstalledAppInfo>? _inMemoryCache;
    private static FileSystemWatcher? _watcher1;
    private static FileSystemWatcher? _watcher2;
    private static bool _watchersInitialized;

    public static string CacheFilePath => Path.Combine(PathHelper.CacheFolder, "installed_apps.json");

    private sealed class DiskCachePayload
    {
        public Dictionary<string, long> Mtimes { get; set; } = new();
        public List<InstalledAppInfo> Apps { get; set; } = new();
    }

    public static bool IsUninstaller(string name, string path = "")
    {
        string combined = $"{name} {path}".ToLowerInvariant();
        return combined.Contains("unins000") || combined.Contains("uninstall") || combined.Contains("unin000");
    }

    public static bool IsDocumentOrWebTarget(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return true;
        string low = path.Trim().ToLowerInvariant();
        if (low.StartsWith("http://") || low.StartsWith("https://") || low.StartsWith("www.") || low.StartsWith("mailto:"))
            return true;

        string ext = Path.GetExtension(low);
        return DocumentExtensions.Contains(ext);
    }

    public static string ResolveKnownFolderGuidPath(string rawPath)
    {
        if (string.IsNullOrWhiteSpace(rawPath)) return rawPath;
        var match = Regex.Match(rawPath, @"^\{([0-9a-fA-F\-]+)\}(.*)");
        if (!match.Success) return rawPath;

        string guidStr = "{" + match.Groups[1].Value.ToUpperInvariant() + "}";
        string subPath = match.Groups[2].Value.TrimStart('\\', '/');

        if (KnownFolders.TryGetValue(guidStr, out string? resolvedFolder) && !string.IsNullOrWhiteSpace(resolvedFolder))
        {
            return Path.Combine(resolvedFolder, subPath);
        }

        return rawPath;
    }

    private static void EnsureWatchers()
    {
        if (_watchersInitialized) return;
        lock (LockObj)
        {
            if (_watchersInitialized) return;
            _watchersInitialized = true;

            try
            {
                if (Directory.Exists(StartMenuDirs[0]))
                {
                    _watcher1 = new FileSystemWatcher(StartMenuDirs[0])
                    {
                        IncludeSubdirectories = true,
                        EnableRaisingEvents = true
                    };
                    _watcher1.Changed += OnStartMenuChanged;
                    _watcher1.Created += OnStartMenuChanged;
                    _watcher1.Deleted += OnStartMenuChanged;
                    _watcher1.Renamed += OnStartMenuChanged;
                }
            }
            catch (Exception ex) { Logger.Log(ex); }

            try
            {
                if (Directory.Exists(StartMenuDirs[1]))
                {
                    _watcher2 = new FileSystemWatcher(StartMenuDirs[1])
                    {
                        IncludeSubdirectories = true,
                        EnableRaisingEvents = true
                    };
                    _watcher2.Changed += OnStartMenuChanged;
                    _watcher2.Created += OnStartMenuChanged;
                    _watcher2.Deleted += OnStartMenuChanged;
                    _watcher2.Renamed += OnStartMenuChanged;
                }
            }
            catch (Exception ex) { Logger.Log(ex); }
        }
    }

    private static void OnStartMenuChanged(object sender, FileSystemEventArgs e)
    {
        InvalidateCache();
    }

    public static void InvalidateCache()
    {
        lock (LockObj)
        {
            _inMemoryCache = null;
        }

        try
        {
            if (File.Exists(CacheFilePath))
            {
                File.Delete(CacheFilePath);
            }
        }
        catch { }
    }

    private static Dictionary<string, long> GetDirsMtimes()
    {
        var dict = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        foreach (var dir in StartMenuDirs)
        {
            if (Directory.Exists(dir))
            {
                try
                {
                    dict[dir] = Directory.GetLastWriteTimeUtc(dir).Ticks;
                }
                catch { }
            }
        }
        return dict;
    }

    private static List<InstalledAppInfo>? TryLoadDiskCache()
    {
        try
        {
            if (!File.Exists(CacheFilePath)) return null;
            string json = File.ReadAllText(CacheFilePath);
            var payload = JsonSerializer.Deserialize<DiskCachePayload>(json);
            if (payload == null || payload.Apps.Count == 0) return null;

            var currentMtimes = GetDirsMtimes();
            foreach (var kvp in currentMtimes)
            {
                if (!payload.Mtimes.TryGetValue(kvp.Key, out long cached) || cached != kvp.Value)
                {
                    return null; // Mtime mismatch: cache is stale
                }
            }

            return payload.Apps;
        }
        catch (Exception ex)
        {
            Logger.Log(ex);
            return null;
        }
    }

    private static void SaveDiskCache(List<InstalledAppInfo> apps)
    {
        try
        {
            PathHelper.EnsureDirectories();
            var payload = new DiskCachePayload
            {
                Mtimes = GetDirsMtimes(),
                Apps = apps
            };
            string json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = false });
            string tempFile = CacheFilePath + ".tmp";
            File.WriteAllText(tempFile, json);
            File.Move(tempFile, CacheFilePath, true);
        }
        catch (Exception ex)
        {
            Logger.Log(ex);
        }
    }

    public static async Task<IReadOnlyList<InstalledAppInfo>> GetInstalledAppsAsync(bool forceRefresh = false, CancellationToken cancellationToken = default)
    {
        EnsureWatchers();

        if (!forceRefresh)
        {
            lock (LockObj)
            {
                if (_inMemoryCache != null)
                {
                    return _inMemoryCache;
                }
            }

            var fromDisk = TryLoadDiskCache();
            if (fromDisk != null)
            {
                lock (LockObj)
                {
                    _inMemoryCache = fromDisk;
                }
                return fromDisk;
            }
        }

        return await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            var apps = ScanInstalledApps(cancellationToken);

            lock (LockObj)
            {
                _inMemoryCache = apps;
            }

            SaveDiskCache(apps);

            return (IReadOnlyList<InstalledAppInfo>)apps;
        }, cancellationToken).ConfigureAwait(false);
    }

    private static List<InstalledAppInfo> ScanInstalledApps(CancellationToken cancellationToken)
    {
        var shortcutsLookup = new ConcurrentDictionary<string, (string target, string args, string? iconPath, int iconIndex)>(StringComparer.OrdinalIgnoreCase);
        var uniqueShortcuts = new Dictionary<string, InstalledAppInfo>(StringComparer.OrdinalIgnoreCase);

        // 1. Сканирование папок меню "Пуск"
        foreach (var sDir in StartMenuDirs)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!Directory.Exists(sDir)) continue;

            try
            {
                var lnkFiles = Directory.EnumerateFiles(sDir, "*.lnk", SearchOption.AllDirectories);
                foreach (var lnk in lnkFiles)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    try
                    {
                        var info = LnkResolver.Resolve(lnk);
                        if (info == null) continue;

                        string target = info.TargetPath.Trim();
                        if (string.IsNullOrEmpty(target) || Directory.Exists(target)) continue;

                        string ext = Path.GetExtension(target);
                        if (DocumentExtensions.Contains(ext) || !ValidProgramExtensions.Contains(ext)) continue;

                        string appName = Path.GetFileNameWithoutExtension(lnk);
                        if (IsUninstaller(appName, target)) continue;
                        if (!File.Exists(target)) continue;

                        var record = (target, info.Arguments, info.IconPath, info.IconIndex);
                        shortcutsLookup[appName] = record;
                        shortcutsLookup[Path.GetFileName(target)] = record;
                        shortcutsLookup[Path.GetFileNameWithoutExtension(target)] = record;

                        if (!uniqueShortcuts.ContainsKey(target))
                        {
                            uniqueShortcuts[target] = new InstalledAppInfo(
                                Name: appName,
                                Path: target,
                                AppType: InstalledAppInfo.TypeDesktop,
                                Description: target,
                                IconSourcePath: info.IconPath ?? target,
                                IconIndex: info.IconIndex,
                                Arguments: info.Arguments);
                        }
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
        }

        var results = new Dictionary<string, InstalledAppInfo>(StringComparer.OrdinalIgnoreCase);

        // 2. Сканирование Shell AppsFolder через COM
        try
        {
            Type? shellType = Type.GetTypeFromProgID("Shell.Application");
            if (shellType != null)
            {
                dynamic? shell = Activator.CreateInstance(shellType);
                if (shell != null)
                {
                    dynamic? appsFolder = shell.NameSpace("shell:AppsFolder");
                    if (appsFolder != null)
                    {
                        foreach (dynamic item in appsFolder.Items())
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            try
                            {
                                string name = ((string)item.Name)?.Trim() ?? "";
                                string rawPath = ((string)item.Path)?.Trim() ?? "";
                                if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(rawPath)) continue;

                                if (IsUninstaller(name, rawPath)) continue;
                                if (IsDocumentOrWebTarget(rawPath)) continue;

                                string resolvedRaw = ResolveKnownFolderGuidPath(rawPath);
                                string finalPath;
                                string appType;
                                string args = "";
                                string? iconSource = null;
                                int iconIndex = 0;
                                string desc;

                                if (Path.IsPathRooted(resolvedRaw) && File.Exists(resolvedRaw))
                                {
                                    string ext = Path.GetExtension(resolvedRaw);
                                    if (!ValidProgramExtensions.Contains(ext)) continue;
                                    if (IsUninstaller(name, resolvedRaw)) continue;

                                    finalPath = resolvedRaw;
                                    appType = InstalledAppInfo.TypeDesktop;
                                    desc = resolvedRaw;
                                }
                                else if (shortcutsLookup.TryGetValue(name, out var matched))
                                {
                                    finalPath = matched.target;
                                    appType = InstalledAppInfo.TypeDesktop;
                                    args = matched.args;
                                    iconSource = matched.iconPath;
                                    iconIndex = matched.iconIndex;
                                    desc = matched.target;
                                }
                                else if (shortcutsLookup.TryGetValue(rawPath, out var matchedRaw))
                                {
                                    finalPath = matchedRaw.target;
                                    appType = InstalledAppInfo.TypeDesktop;
                                    args = matchedRaw.args;
                                    iconSource = matchedRaw.iconPath;
                                    iconIndex = matchedRaw.iconIndex;
                                    desc = matchedRaw.target;
                                }
                                else
                                {
                                    // UWP / Windows Store App
                                    if (DocumentExtensions.Contains(Path.GetExtension(rawPath))) continue;

                                    finalPath = $"shell:AppsFolder\\{rawPath}";
                                    appType = InstalledAppInfo.TypeUwp;
                                    desc = "Windows App";
                                }

                                if (!results.ContainsKey(finalPath))
                                {
                                    results[finalPath] = new InstalledAppInfo(
                                        Name: name,
                                        Path: finalPath,
                                        AppType: appType,
                                        Description: desc,
                                        IconSourcePath: iconSource,
                                        IconIndex: iconIndex,
                                        Arguments: args);
                                }
                            }
                            catch { }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Log(ex);
        }

        // 3. Добавление оставшихся уникальных ярлыков из меню Пуск
        foreach (var kvp in uniqueShortcuts)
        {
            if (!results.ContainsKey(kvp.Key))
            {
                results[kvp.Key] = kvp.Value;
            }
        }

        var list = results.Values.ToList();
        list.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase));
        return list;
    }
}
