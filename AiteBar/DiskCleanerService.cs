using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace AiteBar;

[SupportedOSPlatform("windows6.1")]
public sealed class DiskCleanerService
{
    public const string CategoryUserTemp = "UserTemp";
    public const string CategoryWindowsTemp = "WindowsTemp";
    public const string CategoryRecycleBin = "RecycleBin";
    public const string CategoryBrowserCache = "BrowserCache";
    public const string CategoryGpuCache = "GpuCache";
    public const string CategoryDevCache = "DevCache";
    public const string CategoryCrashDumps = "CrashDumps";
    public const string CategoryDnsCache = "DnsCache";

    private readonly IFileSystemOperations _fs;
    private readonly IProcessRunner _processRunner;
    private readonly IWin32DiskOperations _win32;

    private static readonly string[] BrowserCacheFolderNames =
    [
        "Cache",
        "Code Cache",
        "GPUCache",
        "DawnWebGPUCache",
        "DawnGraphiteCache",
        "ShaderCache",
        "GrShaderCache"
    ];

    private static readonly Regex BrowserProfileRegex =
        new(@"^(Default|Profile \d+|System Profile|Guest Profile)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public DiskCleanerService(
        IFileSystemOperations? fs = null,
        IProcessRunner? processRunner = null,
        IWin32DiskOperations? win32 = null)
    {
        _fs = fs ?? new WindowsFileSystemOperations();
        _processRunner = processRunner ?? new SystemProcessRunner();
        _win32 = win32 ?? new NativeWin32DiskOperations();
    }

    public async Task<DiskCleanScanResult> ScanAsync(
        IProgress<DiskCleanProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            var categories = new List<DiskCleanCategory>();

            // 1. User Temp (Safe, Selected)
            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report(new DiskCleanProgress(CategoryUserTemp, "Scanning User Temp...", 10));
            long userTempSize = _fs.CalculateDirectorySize(Path.GetTempPath());
            categories.Add(new DiskCleanCategory(
                CategoryUserTemp,
                "DiskCleaner_Category_UserTemp",
                "DiskCleaner_Category_UserTemp_Desc",
                userTempSize,
                IsSelected: true,
                IsSafe: true));

            // 2. Browser Cache (Chrome, Edge) (Safe, Selected)
            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report(new DiskCleanProgress(CategoryBrowserCache, "Scanning Browser Caches...", 25));
            long browserCacheSize = CalculateBrowserCacheSize();
            categories.Add(new DiskCleanCategory(
                CategoryBrowserCache,
                "DiskCleaner_Category_BrowserCache",
                "DiskCleaner_Category_BrowserCache_Desc",
                browserCacheSize,
                IsSelected: true,
                IsSafe: true));

            // 3. GPU Cache (Safe, Selected)
            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report(new DiskCleanProgress(CategoryGpuCache, "Scanning GPU Caches...", 40));
            long gpuCacheSize = CalculateGpuCacheSize();
            categories.Add(new DiskCleanCategory(
                CategoryGpuCache,
                "DiskCleaner_Category_GpuCache",
                "DiskCleaner_Category_GpuCache_Desc",
                gpuCacheSize,
                IsSelected: true,
                IsSafe: true));

            // 4. DNS Cache (Safe, Selected)
            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report(new DiskCleanProgress(CategoryDnsCache, "Scanning DNS...", 55));
            categories.Add(new DiskCleanCategory(
                CategoryDnsCache,
                "DiskCleaner_Category_DnsCache",
                "DiskCleaner_Category_DnsCache_Desc",
                0,
                IsSelected: true,
                IsSafe: true));

            // 5. Recycle Bin (Caution, Not Selected)
            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report(new DiskCleanProgress(CategoryRecycleBin, "Scanning Recycle Bin...", 70));
            long recycleBinSize = CalculateRecycleBinSize();
            categories.Add(new DiskCleanCategory(
                CategoryRecycleBin,
                "DiskCleaner_Category_RecycleBin",
                "DiskCleaner_Category_RecycleBin_Desc",
                recycleBinSize,
                IsSelected: false,
                IsSafe: false,
                WarningKey: "DiskCleaner_Warning_RecycleBin"));

            // 6. Dev Caches (NuGet, pip, npm) (Caution, Not Selected)
            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report(new DiskCleanProgress(CategoryDevCache, "Scanning Developer Caches...", 80));
            long devCacheSize = CalculateDevCacheSize();
            categories.Add(new DiskCleanCategory(
                CategoryDevCache,
                "DiskCleaner_Category_DevCache",
                "DiskCleaner_Category_DevCache_Desc",
                devCacheSize,
                IsSelected: false,
                IsSafe: false,
                WarningKey: "DiskCleaner_Warning_DevCache"));

            // 7. Crash Dumps (Caution, Not Selected)
            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report(new DiskCleanProgress(CategoryCrashDumps, "Scanning Crash Dumps...", 90));
            long crashDumpsSize = CalculateCrashDumpsSize();
            categories.Add(new DiskCleanCategory(
                CategoryCrashDumps,
                "DiskCleaner_Category_CrashDumps",
                "DiskCleaner_Category_CrashDumps_Desc",
                crashDumpsSize,
                IsSelected: false,
                IsSafe: false,
                WarningKey: "DiskCleaner_Warning_CrashDumps"));

            // 8. Windows Temp (Caution, Not Selected)
            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report(new DiskCleanProgress(CategoryWindowsTemp, "Scanning Windows Temp...", 100));
            string winTempPath = Path.Combine(_fs.GetSpecialFolderPath(Environment.SpecialFolder.Windows), "Temp");
            long winTempSize = _fs.CalculateDirectorySize(winTempPath);
            categories.Add(new DiskCleanCategory(
                CategoryWindowsTemp,
                "DiskCleaner_Category_WindowsTemp",
                "DiskCleaner_Category_WindowsTemp_Desc",
                winTempSize,
                IsSelected: false,
                IsSafe: false,
                WarningKey: "DiskCleaner_Warning_WindowsTemp"));

            long total = categories.Sum(c => c.SizeBytes);
            return new DiskCleanScanResult(categories, total);
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<DiskCleanResult> CleanAsync(
        IReadOnlySet<string> selectedCategoryIds,
        IProgress<DiskCleanProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        return await Task.Run(async () =>
        {
            long totalFreed = 0;
            int totalCleaned = 0;
            int totalLocked = 0;
            var reports = new List<DiskCleanCategoryReport>();

            double stepProgress = selectedCategoryIds.Count > 0 ? 100.0 / selectedCategoryIds.Count : 100.0;
            int currentStep = 0;

            if (selectedCategoryIds.Contains(CategoryUserTemp))
            {
                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report(new DiskCleanProgress(CategoryUserTemp, "Cleaning User Temp...", currentStep * stepProgress));
                var report = CleanDirectoryCategory(CategoryUserTemp, Path.GetTempPath());
                reports.Add(report);
                totalFreed += report.FreedBytes;
                totalCleaned += report.CleanedCount;
                totalLocked += report.LockedCount;
                currentStep++;
            }

            if (selectedCategoryIds.Contains(CategoryBrowserCache))
            {
                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report(new DiskCleanProgress(CategoryBrowserCache, "Cleaning Browser Caches...", currentStep * stepProgress));
                var report = CleanBrowserCaches();
                reports.Add(report);
                totalFreed += report.FreedBytes;
                totalCleaned += report.CleanedCount;
                totalLocked += report.LockedCount;
                currentStep++;
            }

            if (selectedCategoryIds.Contains(CategoryGpuCache))
            {
                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report(new DiskCleanProgress(CategoryGpuCache, "Cleaning GPU Caches...", currentStep * stepProgress));
                var report = CleanGpuCaches();
                reports.Add(report);
                totalFreed += report.FreedBytes;
                totalCleaned += report.CleanedCount;
                totalLocked += report.LockedCount;
                currentStep++;
            }

            if (selectedCategoryIds.Contains(CategoryDnsCache))
            {
                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report(new DiskCleanProgress(CategoryDnsCache, "Flushing DNS Resolver Cache...", currentStep * stepProgress));
                bool dnsSuccess = _win32.FlushDnsCache();
                reports.Add(new DiskCleanCategoryReport(
                    CategoryDnsCache,
                    dnsSuccess ? DiskCleanCategoryStatus.Succeeded : DiskCleanCategoryStatus.Failed,
                    FreedBytes: 0,
                    CleanedCount: dnsSuccess ? 1 : 0,
                    LockedCount: 0,
                    FailureReason: dnsSuccess ? null : "DNS resolver flush returned false."));
                if (dnsSuccess) totalCleaned++;
                currentStep++;
            }

            if (selectedCategoryIds.Contains(CategoryRecycleBin))
            {
                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report(new DiskCleanProgress(CategoryRecycleBin, "Emptying Recycle Bin...", currentStep * stepProgress));
                long before = CalculateRecycleBinSize();
                int hresult = _win32.EmptyRecycleBin(null);
                long after = CalculateRecycleBinSize();
                long freed = Math.Max(0, before - after);

                if (hresult == 0) // S_OK
                {
                    reports.Add(new DiskCleanCategoryReport(
                        CategoryRecycleBin,
                        DiskCleanCategoryStatus.Succeeded,
                        freed,
                        CleanedCount: 1,
                        LockedCount: 0));
                    totalFreed += freed;
                    totalCleaned++;
                }
                else if (freed > 0)
                {
                    reports.Add(new DiskCleanCategoryReport(
                        CategoryRecycleBin,
                        DiskCleanCategoryStatus.PartiallyCleaned,
                        freed,
                        CleanedCount: 1,
                        LockedCount: 0,
                        FailureReason: $"Recycle Bin emptied partially with HRESULT 0x{hresult:X8}"));
                    totalFreed += freed;
                    totalCleaned++;
                }
                else
                {
                    reports.Add(new DiskCleanCategoryReport(
                        CategoryRecycleBin,
                        DiskCleanCategoryStatus.Failed,
                        FreedBytes: 0,
                        CleanedCount: 0,
                        LockedCount: 0,
                        FailureReason: $"Win32 EmptyRecycleBin returned HRESULT 0x{hresult:X8}"));
                }
                currentStep++;
            }

            if (selectedCategoryIds.Contains(CategoryDevCache))
            {
                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report(new DiskCleanProgress(CategoryDevCache, "Cleaning Developer Caches...", currentStep * stepProgress));
                long before = CalculateDevCacheSize();
                var report = await CleanDevCachesAsync(before, cancellationToken).ConfigureAwait(false);
                reports.Add(report);
                totalFreed += report.FreedBytes;
                totalCleaned += report.CleanedCount;
                totalLocked += report.LockedCount;
                currentStep++;
            }

            if (selectedCategoryIds.Contains(CategoryCrashDumps))
            {
                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report(new DiskCleanProgress(CategoryCrashDumps, "Cleaning Crash Dumps...", currentStep * stepProgress));
                var report = CleanCrashDumps();
                reports.Add(report);
                totalFreed += report.FreedBytes;
                totalCleaned += report.CleanedCount;
                totalLocked += report.LockedCount;
                currentStep++;
            }

            if (selectedCategoryIds.Contains(CategoryWindowsTemp))
            {
                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report(new DiskCleanProgress(CategoryWindowsTemp, "Cleaning Windows Temp...", currentStep * stepProgress));
                string winTemp = Path.Combine(_fs.GetSpecialFolderPath(Environment.SpecialFolder.Windows), "Temp");
                var report = CleanDirectoryCategory(CategoryWindowsTemp, winTemp);
                reports.Add(report);
                totalFreed += report.FreedBytes;
                totalCleaned += report.CleanedCount;
                totalLocked += report.LockedCount;
                currentStep++;
            }

            progress?.Report(new DiskCleanProgress(string.Empty, "Done", 100));
            return new DiskCleanResult(totalFreed, totalCleaned, totalLocked, reports);
        }, cancellationToken).ConfigureAwait(false);
    }

    public static string FormatByteSize(long bytes)
    {
        if (bytes <= 0) return "0 B";
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double size = bytes;
        int unitIndex = 0;
        while (size >= 1024.0 && unitIndex < units.Length - 1)
        {
            size /= 1024.0;
            unitIndex++;
        }
        return unitIndex == 0
            ? $"{bytes} B"
            : $"{size:F2} {units[unitIndex]}";
    }

    #region Calculation Helpers

    public long CalculateDirectorySize(string directoryPath) =>
        _fs.CalculateDirectorySize(directoryPath);

    public (long FreedBytes, int CleanedFiles, int LockedFiles) CleanDirectoryContents(string directoryPath) =>
        _fs.CleanDirectoryContents(directoryPath);

    private long CalculateRecycleBinSize()
    {
        long total = 0;
        foreach (var rootPath in _fs.EnumerateFixedDriveRootPaths())
        {
            string recycleBin = Path.Combine(rootPath, "$Recycle.Bin");
            total += _fs.CalculateDirectorySize(recycleBin);
        }
        return total;
    }

    private long CalculateBrowserCacheSize()
    {
        long total = 0;
        string localAppData = _fs.GetSpecialFolderPath(Environment.SpecialFolder.LocalApplicationData);

        // Google Chrome
        string chromeUserData = Path.Combine(localAppData, "Google", "Chrome", "User Data");
        total += CalculateChromiumCacheSize(chromeUserData);

        // Microsoft Edge
        string edgeUserData = Path.Combine(localAppData, "Microsoft", "Edge", "User Data");
        total += CalculateChromiumCacheSize(edgeUserData);

        // Mozilla Firefox
        string firefoxProfiles = Path.Combine(localAppData, "Mozilla", "Firefox", "Profiles");
        total += CalculateFirefoxCacheSize(firefoxProfiles);

        return total;
    }

    private long CalculateFirefoxCacheSize(string profilesPath)
    {
        if (!_fs.DirectoryExists(profilesPath)) return 0;
        long total = 0;
        try
        {
            foreach (var subDir in _fs.EnumerateDirectories(profilesPath))
            {
                total += _fs.CalculateDirectorySize(Path.Combine(subDir, "cache2"));
                total += _fs.CalculateDirectorySize(Path.Combine(subDir, "startupCache"));
                total += _fs.CalculateDirectorySize(Path.Combine(subDir, "jumpListCache"));
            }
        }
        catch
        {
            // Ignore
        }
        return total;
    }

    private long CalculateChromiumCacheSize(string userDataPath)
    {
        if (!_fs.DirectoryExists(userDataPath)) return 0;
        long total = 0;
        try
        {
            foreach (var subDir in _fs.EnumerateDirectories(userDataPath))
            {
                string dirName = Path.GetFileName(subDir);
                if (BrowserProfileRegex.IsMatch(dirName))
                {
                    foreach (string cacheName in BrowserCacheFolderNames)
                    {
                        string target = Path.Combine(subDir, cacheName);
                        total += _fs.CalculateDirectorySize(target);
                    }
                }
            }

            // Root shader caches
            total += _fs.CalculateDirectorySize(Path.Combine(userDataPath, "ShaderCache"));
            total += _fs.CalculateDirectorySize(Path.Combine(userDataPath, "GrShaderCache"));
        }
        catch
        {
            // Ignore
        }
        return total;
    }

    private long CalculateGpuCacheSize()
    {
        long total = 0;
        string localAppData = _fs.GetSpecialFolderPath(Environment.SpecialFolder.LocalApplicationData);

        total += _fs.CalculateDirectorySize(Path.Combine(localAppData, "NVIDIA", "DXCache"));
        total += _fs.CalculateDirectorySize(Path.Combine(localAppData, "NVIDIA", "GLCache"));
        total += _fs.CalculateDirectorySize(Path.Combine(localAppData, "AMD", "DxCache"));
        total += _fs.CalculateDirectorySize(Path.Combine(localAppData, "D3DSCache"));

        return total;
    }

    private long CalculateDevCacheSize()
    {
        long total = 0;
        string userProfile = _fs.GetSpecialFolderPath(Environment.SpecialFolder.UserProfile);
        string localAppData = _fs.GetSpecialFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string appData = _fs.GetSpecialFolderPath(Environment.SpecialFolder.ApplicationData);

        // NuGet http-cache & global packages
        total += _fs.CalculateDirectorySize(Path.Combine(localAppData, "NuGet", "v3-cache"));
        total += _fs.CalculateDirectorySize(Path.Combine(localAppData, "NuGet", "plugins-cache"));
        total += _fs.CalculateDirectorySize(Path.Combine(userProfile, ".nuget", "packages"));

        // pip cache
        total += _fs.CalculateDirectorySize(Path.Combine(localAppData, "pip", "cache"));

        // npm cache
        total += _fs.CalculateDirectorySize(Path.Combine(appData, "npm-cache"));

        return total;
    }

    private long CalculateCrashDumpsSize()
    {
        long total = 0;
        string localAppData = _fs.GetSpecialFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string windowsDir = _fs.GetSpecialFolderPath(Environment.SpecialFolder.Windows);

        total += _fs.CalculateDirectorySize(Path.Combine(localAppData, "CrashDumps"));
        total += _fs.CalculateDirectorySize(Path.Combine(localAppData, "Microsoft", "Windows", "WER"));
        total += _fs.CalculateDirectorySize(Path.Combine(windowsDir, "Minidump"));

        return total;
    }

    #endregion

    #region Cleaning Helpers

    private DiskCleanCategoryReport CleanDirectoryCategory(string categoryId, string directoryPath)
    {
        if (!_fs.DirectoryExists(directoryPath))
        {
            return new DiskCleanCategoryReport(
                categoryId,
                DiskCleanCategoryStatus.Skipped,
                FreedBytes: 0,
                CleanedCount: 0,
                LockedCount: 0,
                FailureReason: "Directory does not exist.");
        }

        var (freed, cleaned, locked) = _fs.CleanDirectoryContents(directoryPath);

        DiskCleanCategoryStatus status = locked > 0
            ? (cleaned > 0 ? DiskCleanCategoryStatus.PartiallyCleaned : DiskCleanCategoryStatus.Failed)
            : DiskCleanCategoryStatus.Succeeded;

        return new DiskCleanCategoryReport(
            categoryId,
            status,
            freed,
            cleaned,
            locked,
            locked > 0 ? $"{locked} files were locked by running processes." : null);
    }

    private DiskCleanCategoryReport CleanBrowserCaches()
    {
        long freed = 0;
        int cleaned = 0;
        int locked = 0;
        int accessErrors = 0;
        bool anyCacheFound = false;
        string localAppData = _fs.GetSpecialFolderPath(Environment.SpecialFolder.LocalApplicationData);

        var userDatas = new[]
        {
            Path.Combine(localAppData, "Google", "Chrome", "User Data"),
            Path.Combine(localAppData, "Microsoft", "Edge", "User Data")
        };

        foreach (var userData in userDatas)
        {
            if (!_fs.DirectoryExists(userData)) continue;
            try
            {
                foreach (var subDir in _fs.EnumerateDirectories(userData))
                {
                    string dirName = Path.GetFileName(subDir);
                    if (BrowserProfileRegex.IsMatch(dirName))
                    {
                        foreach (string cacheName in BrowserCacheFolderNames)
                        {
                            string target = Path.Combine(subDir, cacheName);
                            if (_fs.DirectoryExists(target))
                            {
                                anyCacheFound = true;
                                var res = _fs.CleanDirectoryContents(target);
                                freed += res.FreedBytes;
                                cleaned += res.CleanedFiles;
                                locked += res.LockedFiles;
                            }
                        }
                    }
                }

                string sc = Path.Combine(userData, "ShaderCache");
                if (_fs.DirectoryExists(sc))
                {
                    anyCacheFound = true;
                    var r1 = _fs.CleanDirectoryContents(sc);
                    freed += r1.FreedBytes;
                    cleaned += r1.CleanedFiles;
                    locked += r1.LockedFiles;
                }

                string gsc = Path.Combine(userData, "GrShaderCache");
                if (_fs.DirectoryExists(gsc))
                {
                    anyCacheFound = true;
                    var r2 = _fs.CleanDirectoryContents(gsc);
                    freed += r2.FreedBytes;
                    cleaned += r2.CleanedFiles;
                    locked += r2.LockedFiles;
                }
            }
            catch
            {
                accessErrors++;
            }
        }

        // Mozilla Firefox
        string firefoxProfiles = Path.Combine(localAppData, "Mozilla", "Firefox", "Profiles");
        if (_fs.DirectoryExists(firefoxProfiles))
        {
            try
            {
                foreach (var profileDir in _fs.EnumerateDirectories(firefoxProfiles))
                {
                    foreach (string ffCache in new[] { "cache2", "startupCache", "jumpListCache" })
                    {
                        string target = Path.Combine(profileDir, ffCache);
                        if (_fs.DirectoryExists(target))
                        {
                            anyCacheFound = true;
                            var res = _fs.CleanDirectoryContents(target);
                            freed += res.FreedBytes;
                            cleaned += res.CleanedFiles;
                            locked += res.LockedFiles;
                        }
                    }
                }
            }
            catch
            {
                accessErrors++;
            }
        }

        if (!anyCacheFound && accessErrors == 0)
        {
            return new DiskCleanCategoryReport(
                CategoryBrowserCache,
                DiskCleanCategoryStatus.Skipped,
                0, 0, 0, "No browser cache folders were found on disk.");
        }

        if (accessErrors > 0 && cleaned == 0 && locked == 0)
        {
            return new DiskCleanCategoryReport(
                CategoryBrowserCache,
                DiskCleanCategoryStatus.Failed,
                0, 0, 0, "Could not access browser profile directories.");
        }

        DiskCleanCategoryStatus status;
        string? reason = null;

        if (locked > 0 || accessErrors > 0)
        {
            status = (cleaned > 0 || freed > 0)
                ? DiskCleanCategoryStatus.PartiallyCleaned
                : DiskCleanCategoryStatus.Failed;

            var reasons = new List<string>();
            if (locked > 0) reasons.Add($"{locked} browser cache files were locked (close Chrome, Edge, or Firefox before cleanup).");
            if (accessErrors > 0) reasons.Add($"{accessErrors} profile directories had access errors.");
            reason = string.Join(" ", reasons);
        }
        else
        {
            status = DiskCleanCategoryStatus.Succeeded;
        }

        return new DiskCleanCategoryReport(
            CategoryBrowserCache,
            status,
            freed,
            cleaned,
            locked,
            reason);
    }

    private DiskCleanCategoryReport CleanGpuCaches()
    {
        long freed = 0;
        int cleaned = 0;
        int locked = 0;
        string localAppData = _fs.GetSpecialFolderPath(Environment.SpecialFolder.LocalApplicationData);

        var paths = new[]
        {
            Path.Combine(localAppData, "NVIDIA", "DXCache"),
            Path.Combine(localAppData, "NVIDIA", "GLCache"),
            Path.Combine(localAppData, "AMD", "DxCache"),
            Path.Combine(localAppData, "D3DSCache")
        };

        bool anyFound = false;
        foreach (var p in paths)
        {
            if (_fs.DirectoryExists(p))
            {
                anyFound = true;
                var res = _fs.CleanDirectoryContents(p);
                freed += res.FreedBytes;
                cleaned += res.CleanedFiles;
                locked += res.LockedFiles;
            }
        }

        if (!anyFound)
        {
            return new DiskCleanCategoryReport(
                CategoryGpuCache,
                DiskCleanCategoryStatus.Skipped,
                0, 0, 0, "No GPU shader cache folders found.");
        }

        DiskCleanCategoryStatus status = locked > 0
            ? (cleaned > 0 ? DiskCleanCategoryStatus.PartiallyCleaned : DiskCleanCategoryStatus.Failed)
            : DiskCleanCategoryStatus.Succeeded;

        return new DiskCleanCategoryReport(CategoryGpuCache, status, freed, cleaned, locked);
    }

    private DiskCleanCategoryReport CleanCrashDumps()
    {
        long freed = 0;
        int cleaned = 0;
        int locked = 0;
        string localAppData = _fs.GetSpecialFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string windowsDir = _fs.GetSpecialFolderPath(Environment.SpecialFolder.Windows);

        var paths = new[]
        {
            Path.Combine(localAppData, "CrashDumps"),
            Path.Combine(localAppData, "Microsoft", "Windows", "WER"),
            Path.Combine(windowsDir, "Minidump")
        };

        bool anyFound = false;
        foreach (var p in paths)
        {
            if (_fs.DirectoryExists(p))
            {
                anyFound = true;
                var res = _fs.CleanDirectoryContents(p);
                freed += res.FreedBytes;
                cleaned += res.CleanedFiles;
                locked += res.LockedFiles;
            }
        }

        if (!anyFound)
        {
            return new DiskCleanCategoryReport(
                CategoryCrashDumps,
                DiskCleanCategoryStatus.Skipped,
                0, 0, 0, "No crash dump folders found.");
        }

        DiskCleanCategoryStatus status = locked > 0
            ? (cleaned > 0 ? DiskCleanCategoryStatus.PartiallyCleaned : DiskCleanCategoryStatus.Failed)
            : DiskCleanCategoryStatus.Succeeded;

        return new DiskCleanCategoryReport(CategoryCrashDumps, status, freed, cleaned, locked);
    }

    private async Task<DiskCleanCategoryReport> CleanDevCachesAsync(long beforeSizeBytes, CancellationToken cancellationToken)
    {
        int successfulTools = 0;
        int failedTools = 0;
        int timedOutTools = 0;
        var timeout = TimeSpan.FromSeconds(15);

        // 1. dotnet nuget locals all --clear
        var nugetResult = await _processRunner.RunSilentProcessAsync("dotnet", "nuget locals all --clear", timeout, cancellationToken).ConfigureAwait(false);
        if (nugetResult.Success) successfulTools++;
        else if (nugetResult.TimedOut) timedOutTools++;
        else if (nugetResult.ExitCode != -1) failedTools++;

        // 2. npm cache clean --force
        var npmResult = await _processRunner.RunSilentProcessAsync("npm", "cache clean --force", timeout, cancellationToken).ConfigureAwait(false);
        if (npmResult.Success) successfulTools++;
        else if (npmResult.TimedOut) timedOutTools++;
        else if (npmResult.ExitCode != -1) failedTools++;

        // 3. pip cache purge
        var pipResult = await _processRunner.RunSilentProcessAsync("pip", "cache purge", timeout, cancellationToken).ConfigureAwait(false);
        if (pipResult.Success) successfulTools++;
        else if (pipResult.TimedOut) timedOutTools++;
        else if (pipResult.ExitCode != -1) failedTools++;

        long after = CalculateDevCacheSize();
        long freed = Math.Max(0, beforeSizeBytes - after);

        int totalErrors = failedTools + timedOutTools;

        if (successfulTools > 0)
        {
            DiskCleanCategoryStatus status = totalErrors > 0
                ? DiskCleanCategoryStatus.PartiallyCleaned
                : DiskCleanCategoryStatus.Succeeded;

            string? reason = totalErrors > 0
                ? $"{totalErrors} CLI package tools failed or timed out ({timedOutTools} timed out)."
                : null;

            return new DiskCleanCategoryReport(
                CategoryDevCache,
                status,
                freed,
                CleanedCount: successfulTools,
                LockedCount: totalErrors,
                reason);
        }

        if (totalErrors > 0)
        {
            return new DiskCleanCategoryReport(
                CategoryDevCache,
                DiskCleanCategoryStatus.Failed,
                FreedBytes: freed,
                CleanedCount: 0,
                LockedCount: totalErrors,
                FailureReason: timedOutTools > 0
                    ? $"{totalErrors} package tools failed or timed out ({timedOutTools} timed out)."
                    : "Package managers returned non-zero exit codes.");
        }

        // None of the tools are installed on this system
        return new DiskCleanCategoryReport(
            CategoryDevCache,
            DiskCleanCategoryStatus.Skipped,
            0, 0, 0, "No supported developer package CLI tools (dotnet, npm, pip) were found.");
    }

    #endregion
}
