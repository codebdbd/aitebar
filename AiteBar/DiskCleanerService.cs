using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
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

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHEmptyRecycleBin(IntPtr hwnd, string? pszRootPath, uint dwFlags);

    [DllImport("dnsapi.dll", EntryPoint = "DnsFlushResolverCache")]
    private static extern int DnsFlushResolverCache();

    private const uint SHERB_NOCONFIRMATION = 0x00000001;
    private const uint SHERB_NOPROGRESSUI = 0x00000002;
    private const uint SHERB_NOSOUND = 0x00000004;

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

    public async Task<DiskCleanScanResult> ScanAsync(
        IProgress<DiskCleanProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            var categories = new List<DiskCleanCategory>();

            // 1. User Temp
            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report(new DiskCleanProgress(CategoryUserTemp, "Scanning User Temp...", 10));
            long userTempSize = CalculateDirectorySize(Path.GetTempPath());
            categories.Add(new DiskCleanCategory(
                CategoryUserTemp,
                "DiskCleaner_Category_UserTemp",
                "DiskCleaner_Category_UserTemp_Desc",
                userTempSize));

            // 2. Windows Temp
            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report(new DiskCleanProgress(CategoryWindowsTemp, "Scanning Windows Temp...", 25));
            string winTempPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp");
            long winTempSize = CalculateDirectorySize(winTempPath);
            categories.Add(new DiskCleanCategory(
                CategoryWindowsTemp,
                "DiskCleaner_Category_WindowsTemp",
                "DiskCleaner_Category_WindowsTemp_Desc",
                winTempSize));

            // 3. Recycle Bin
            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report(new DiskCleanProgress(CategoryRecycleBin, "Scanning Recycle Bin...", 40));
            long recycleBinSize = CalculateRecycleBinSize();
            categories.Add(new DiskCleanCategory(
                CategoryRecycleBin,
                "DiskCleaner_Category_RecycleBin",
                "DiskCleaner_Category_RecycleBin_Desc",
                recycleBinSize));

            // 4. Browser Cache (Chrome, Edge)
            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report(new DiskCleanProgress(CategoryBrowserCache, "Scanning Browser Caches...", 55));
            long browserCacheSize = CalculateBrowserCacheSize();
            categories.Add(new DiskCleanCategory(
                CategoryBrowserCache,
                "DiskCleaner_Category_BrowserCache",
                "DiskCleaner_Category_BrowserCache_Desc",
                browserCacheSize));

            // 5. GPU Cache
            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report(new DiskCleanProgress(CategoryGpuCache, "Scanning GPU Caches...", 70));
            long gpuCacheSize = CalculateGpuCacheSize();
            categories.Add(new DiskCleanCategory(
                CategoryGpuCache,
                "DiskCleaner_Category_GpuCache",
                "DiskCleaner_Category_GpuCache_Desc",
                gpuCacheSize));

            // 6. Dev Caches (NuGet, pip, npm)
            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report(new DiskCleanProgress(CategoryDevCache, "Scanning Developer Caches...", 80));
            long devCacheSize = CalculateDevCacheSize();
            categories.Add(new DiskCleanCategory(
                CategoryDevCache,
                "DiskCleaner_Category_DevCache",
                "DiskCleaner_Category_DevCache_Desc",
                devCacheSize));

            // 7. Crash Dumps
            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report(new DiskCleanProgress(CategoryCrashDumps, "Scanning Crash Dumps...", 90));
            long crashDumpsSize = CalculateCrashDumpsSize();
            categories.Add(new DiskCleanCategory(
                CategoryCrashDumps,
                "DiskCleaner_Category_CrashDumps",
                "DiskCleaner_Category_CrashDumps_Desc",
                crashDumpsSize));

            // 8. DNS Cache (Logical flag)
            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report(new DiskCleanProgress(CategoryDnsCache, "Scanning DNS...", 100));
            categories.Add(new DiskCleanCategory(
                CategoryDnsCache,
                "DiskCleaner_Category_DnsCache",
                "DiskCleaner_Category_DnsCache_Desc",
                0));

            long total = categories.Sum(c => c.SizeBytes);
            return new DiskCleanScanResult(categories, total);
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<DiskCleanResult> CleanAsync(
        IReadOnlySet<string> selectedCategoryIds,
        IProgress<DiskCleanProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            long totalFreed = 0;
            int cleanedCount = 0;
            int lockedCount = 0;
            var cleanedList = new List<string>();

            double stepProgress = selectedCategoryIds.Count > 0 ? 100.0 / selectedCategoryIds.Count : 100.0;
            int currentStep = 0;

            if (selectedCategoryIds.Contains(CategoryUserTemp))
            {
                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report(new DiskCleanProgress(CategoryUserTemp, "Cleaning User Temp...", currentStep * stepProgress));
                var (freed, cleaned, locked) = CleanDirectoryContents(Path.GetTempPath());
                totalFreed += freed;
                cleanedCount += cleaned;
                lockedCount += locked;
                cleanedList.Add(CategoryUserTemp);
                currentStep++;
            }

            if (selectedCategoryIds.Contains(CategoryWindowsTemp))
            {
                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report(new DiskCleanProgress(CategoryWindowsTemp, "Cleaning Windows Temp...", currentStep * stepProgress));
                string winTemp = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp");
                var (freed, cleaned, locked) = CleanDirectoryContents(winTemp);
                totalFreed += freed;
                cleanedCount += cleaned;
                lockedCount += locked;
                cleanedList.Add(CategoryWindowsTemp);
                currentStep++;
            }

            if (selectedCategoryIds.Contains(CategoryRecycleBin))
            {
                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report(new DiskCleanProgress(CategoryRecycleBin, "Emptying Recycle Bin...", currentStep * stepProgress));
                long before = CalculateRecycleBinSize();
                try
                {
                    SHEmptyRecycleBin(IntPtr.Zero, null, SHERB_NOCONFIRMATION | SHERB_NOPROGRESSUI | SHERB_NOSOUND);
                }
                catch
                {
                    // Ignore Win32 shell errors
                }
                long after = CalculateRecycleBinSize();
                long freed = Math.Max(0, before - after);
                totalFreed += freed;
                cleanedCount++;
                cleanedList.Add(CategoryRecycleBin);
                currentStep++;
            }

            if (selectedCategoryIds.Contains(CategoryBrowserCache))
            {
                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report(new DiskCleanProgress(CategoryBrowserCache, "Cleaning Browser Caches...", currentStep * stepProgress));
                var (freed, cleaned, locked) = CleanBrowserCaches();
                totalFreed += freed;
                cleanedCount += cleaned;
                lockedCount += locked;
                cleanedList.Add(CategoryBrowserCache);
                currentStep++;
            }

            if (selectedCategoryIds.Contains(CategoryGpuCache))
            {
                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report(new DiskCleanProgress(CategoryGpuCache, "Cleaning GPU Caches...", currentStep * stepProgress));
                var (freed, cleaned, locked) = CleanGpuCaches();
                totalFreed += freed;
                cleanedCount += cleaned;
                lockedCount += locked;
                cleanedList.Add(CategoryGpuCache);
                currentStep++;
            }

            if (selectedCategoryIds.Contains(CategoryDevCache))
            {
                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report(new DiskCleanProgress(CategoryDevCache, "Cleaning Developer Caches...", currentStep * stepProgress));
                long before = CalculateDevCacheSize();
                CleanDevCaches();
                long after = CalculateDevCacheSize();
                long freed = Math.Max(0, before - after);
                totalFreed += freed;
                cleanedCount++;
                cleanedList.Add(CategoryDevCache);
                currentStep++;
            }

            if (selectedCategoryIds.Contains(CategoryCrashDumps))
            {
                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report(new DiskCleanProgress(CategoryCrashDumps, "Cleaning Crash Dumps...", currentStep * stepProgress));
                var (freed, cleaned, locked) = CleanCrashDumps();
                totalFreed += freed;
                cleanedCount += cleaned;
                lockedCount += locked;
                cleanedList.Add(CategoryCrashDumps);
                currentStep++;
            }

            if (selectedCategoryIds.Contains(CategoryDnsCache))
            {
                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report(new DiskCleanProgress(CategoryDnsCache, "Flushing DNS Resolver Cache...", currentStep * stepProgress));
                try
                {
                    DnsFlushResolverCache();
                }
                catch
                {
                    // Ignore DNS API errors
                }
                cleanedCount++;
                cleanedList.Add(CategoryDnsCache);
                currentStep++;
            }

            progress?.Report(new DiskCleanProgress(string.Empty, "Done", 100));
            return new DiskCleanResult(totalFreed, cleanedCount, lockedCount, cleanedList);
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

    public static long CalculateDirectorySize(string directoryPath)
    {
        if (!Directory.Exists(directoryPath)) return 0;
        long total = 0;
        try
        {
            var dirInfo = new DirectoryInfo(directoryPath);
            foreach (var file in dirInfo.EnumerateFiles("*", new EnumerationOptions { IgnoreInaccessible = true, RecurseSubdirectories = true }))
            {
                try
                {
                    total += file.Length;
                }
                catch
                {
                    // Ignore inaccessible or deleted file lengths
                }
            }
        }
        catch
        {
            // Ignore access errors
        }
        return total;
    }

    private static long CalculateRecycleBinSize()
    {
        long total = 0;
        foreach (var drive in DriveInfo.GetDrives())
        {
            if (drive.DriveType != DriveType.Fixed) continue;
            string recycleBin = Path.Combine(drive.RootDirectory.FullName, "$Recycle.Bin");
            total += CalculateDirectorySize(recycleBin);
        }
        return total;
    }

    private static long CalculateBrowserCacheSize()
    {
        long total = 0;
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        // Google Chrome
        string chromeUserData = Path.Combine(localAppData, "Google", "Chrome", "User Data");
        total += CalculateChromiumCacheSize(chromeUserData);

        // Microsoft Edge
        string edgeUserData = Path.Combine(localAppData, "Microsoft", "Edge", "User Data");
        total += CalculateChromiumCacheSize(edgeUserData);

        return total;
    }

    private static long CalculateChromiumCacheSize(string userDataPath)
    {
        if (!Directory.Exists(userDataPath)) return 0;
        long total = 0;
        try
        {
            var dir = new DirectoryInfo(userDataPath);
            foreach (var subDir in dir.EnumerateDirectories())
            {
                if (BrowserProfileRegex.IsMatch(subDir.Name))
                {
                    foreach (string cacheName in BrowserCacheFolderNames)
                    {
                        string target = Path.Combine(subDir.FullName, cacheName);
                        total += CalculateDirectorySize(target);
                    }
                }
            }

            // Root shader caches
            total += CalculateDirectorySize(Path.Combine(userDataPath, "ShaderCache"));
            total += CalculateDirectorySize(Path.Combine(userDataPath, "GrShaderCache"));
        }
        catch
        {
            // Ignore
        }
        return total;
    }

    private static long CalculateGpuCacheSize()
    {
        long total = 0;
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        total += CalculateDirectorySize(Path.Combine(localAppData, "NVIDIA", "DXCache"));
        total += CalculateDirectorySize(Path.Combine(localAppData, "NVIDIA", "GLCache"));
        total += CalculateDirectorySize(Path.Combine(localAppData, "AMD", "DxCache"));
        total += CalculateDirectorySize(Path.Combine(localAppData, "D3DSCache"));

        return total;
    }

    private static long CalculateDevCacheSize()
    {
        long total = 0;
        string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        // NuGet http-cache & global packages
        total += CalculateDirectorySize(Path.Combine(localAppData, "NuGet", "v3-cache"));
        total += CalculateDirectorySize(Path.Combine(localAppData, "NuGet", "plugins-cache"));
        total += CalculateDirectorySize(Path.Combine(userProfile, ".nuget", "packages"));

        // pip cache
        total += CalculateDirectorySize(Path.Combine(localAppData, "pip", "cache"));

        // npm cache
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        total += CalculateDirectorySize(Path.Combine(appData, "npm-cache"));

        return total;
    }

    private static long CalculateCrashDumpsSize()
    {
        long total = 0;
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string windowsDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);

        total += CalculateDirectorySize(Path.Combine(localAppData, "CrashDumps"));
        total += CalculateDirectorySize(Path.Combine(localAppData, "Microsoft", "Windows", "WER"));
        total += CalculateDirectorySize(Path.Combine(windowsDir, "Minidump"));

        return total;
    }

    #endregion

    #region Cleaning Helpers

    public static (long FreedBytes, int CleanedFiles, int LockedFiles) CleanDirectoryContents(string directoryPath)
    {
        if (!Directory.Exists(directoryPath)) return (0, 0, 0);

        long freed = 0;
        int cleaned = 0;
        int locked = 0;

        var dirInfo = new DirectoryInfo(directoryPath);

        // Files
        foreach (var file in dirInfo.EnumerateFiles("*", new EnumerationOptions { IgnoreInaccessible = true }))
        {
            try
            {
                long len = file.Length;
                file.Attributes = FileAttributes.Normal;
                file.Delete();
                freed += len;
                cleaned++;
            }
            catch
            {
                locked++;
            }
        }

        // Subdirectories
        foreach (var subDir in dirInfo.EnumerateDirectories("*", new EnumerationOptions { IgnoreInaccessible = true }))
        {
            try
            {
                long subFreed = CalculateDirectorySize(subDir.FullName);
                subDir.Delete(true);
                freed += subFreed;
                cleaned++;
            }
            catch
            {
                // If directory couldn't be deleted as a whole, try inner contents
                var (innerFreed, innerCleaned, innerLocked) = CleanDirectoryContents(subDir.FullName);
                freed += innerFreed;
                cleaned += innerCleaned;
                locked += innerLocked;
            }
        }

        return (freed, cleaned, locked);
    }

    private static (long FreedBytes, int CleanedFiles, int LockedFiles) CleanBrowserCaches()
    {
        long freed = 0;
        int cleaned = 0;
        int locked = 0;
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        var userDatas = new[]
        {
            Path.Combine(localAppData, "Google", "Chrome", "User Data"),
            Path.Combine(localAppData, "Microsoft", "Edge", "User Data")
        };

        foreach (var userData in userDatas)
        {
            if (!Directory.Exists(userData)) continue;
            try
            {
                var dir = new DirectoryInfo(userData);
                foreach (var subDir in dir.EnumerateDirectories())
                {
                    if (BrowserProfileRegex.IsMatch(subDir.Name))
                    {
                        foreach (string cacheName in BrowserCacheFolderNames)
                        {
                            string target = Path.Combine(subDir.FullName, cacheName);
                            var res = CleanDirectoryContents(target);
                            freed += res.FreedBytes;
                            cleaned += res.CleanedFiles;
                            locked += res.LockedFiles;
                        }
                    }
                }

                var r1 = CleanDirectoryContents(Path.Combine(userData, "ShaderCache"));
                var r2 = CleanDirectoryContents(Path.Combine(userData, "GrShaderCache"));
                freed += r1.FreedBytes + r2.FreedBytes;
                cleaned += r1.CleanedFiles + r2.CleanedFiles;
                locked += r1.LockedFiles + r2.LockedFiles;
            }
            catch
            {
                // Ignore
            }
        }

        return (freed, cleaned, locked);
    }

    private static (long FreedBytes, int CleanedFiles, int LockedFiles) CleanGpuCaches()
    {
        long freed = 0;
        int cleaned = 0;
        int locked = 0;
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        var paths = new[]
        {
            Path.Combine(localAppData, "NVIDIA", "DXCache"),
            Path.Combine(localAppData, "NVIDIA", "GLCache"),
            Path.Combine(localAppData, "AMD", "DxCache"),
            Path.Combine(localAppData, "D3DSCache")
        };

        foreach (var p in paths)
        {
            var res = CleanDirectoryContents(p);
            freed += res.FreedBytes;
            cleaned += res.CleanedFiles;
            locked += res.LockedFiles;
        }

        return (freed, cleaned, locked);
    }

    private static (long FreedBytes, int CleanedFiles, int LockedFiles) CleanCrashDumps()
    {
        long freed = 0;
        int cleaned = 0;
        int locked = 0;
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string windowsDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);

        var paths = new[]
        {
            Path.Combine(localAppData, "CrashDumps"),
            Path.Combine(localAppData, "Microsoft", "Windows", "WER"),
            Path.Combine(windowsDir, "Minidump")
        };

        foreach (var p in paths)
        {
            var res = CleanDirectoryContents(p);
            freed += res.FreedBytes;
            cleaned += res.CleanedFiles;
            locked += res.LockedFiles;
        }

        return (freed, cleaned, locked);
    }

    private static void CleanDevCaches()
    {
        // dotnet nuget locals all --clear
        TryRunSilentProcess("dotnet", "nuget locals all --clear");

        // npm cache clean --force
        TryRunSilentProcess("npm", "cache clean --force");

        // pip cache purge
        TryRunSilentProcess("pip", "cache purge");
    }

    private static void TryRunSilentProcess(string fileName, string arguments)
    {
        try
        {
            using var proc = new Process();
            proc.StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            proc.Start();
            proc.WaitForExit(5000);
        }
        catch
        {
            // If tool is not installed, ignore gracefully
        }
    }

    #endregion
}
