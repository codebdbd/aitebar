using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace AiteBar.Tests;

public sealed class DiskCleanerServiceTests
{
    private sealed class MockFileSystemOperations : IFileSystemOperations
    {
        public Dictionary<string, long> DirectorySizes { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, (long Freed, int Cleaned, int Locked)> CleanResults { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> ExistingDirectories { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public List<string> FixedDrives { get; set; } = ["C:\\"];
        public Func<string, IEnumerable<string>>? EnumerateDirectoriesHandler { get; set; }

        public long CalculateDirectorySize(string directoryPath) =>
            DirectorySizes.TryGetValue(directoryPath, out long size) ? size : 0;

        public (long FreedBytes, int CleanedFiles, int LockedFiles) CleanDirectoryContents(string directoryPath) =>
            CleanResults.TryGetValue(directoryPath, out var res) ? res : (0, 0, 0);

        public bool DirectoryExists(string path) =>
            ExistingDirectories.Contains(path);

        public IEnumerable<string> EnumerateFixedDriveRootPaths() => FixedDrives;

        public string GetSpecialFolderPath(Environment.SpecialFolder folder) =>
            folder switch
            {
                Environment.SpecialFolder.Windows => "C:\\Windows",
                Environment.SpecialFolder.LocalApplicationData => "C:\\Users\\MockUser\\AppData\\Local",
                Environment.SpecialFolder.ApplicationData => "C:\\Users\\MockUser\\AppData\\Roaming",
                Environment.SpecialFolder.UserProfile => "C:\\Users\\MockUser",
                _ => "C:\\Mock"
            };

        public IEnumerable<string> EnumerateDirectories(string path) =>
            EnumerateDirectoriesHandler != null ? EnumerateDirectoriesHandler(path) : [];
    }

    private sealed class MockProcessRunner : IProcessRunner
    {
        public Func<string, string, ProcessRunResult>? Handler { get; set; }

        public Task<ProcessRunResult> RunSilentProcessAsync(
            string fileName,
            string arguments,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromCanceled<ProcessRunResult>(cancellationToken);
            }
            if (Handler != null)
            {
                return Task.FromResult(Handler(fileName, arguments));
            }
            return Task.FromResult(new ProcessRunResult(true, 0, false, "ok", string.Empty));
        }
    }

    private sealed class MockWin32Operations : IWin32DiskOperations
    {
        public int EmptyRecycleBinResult { get; set; } = 0; // S_OK
        public bool FlushDnsResult { get; set; } = true;
        public Action? OnEmptyRecycleBin { get; set; }

        public int EmptyRecycleBin(string? rootPath)
        {
            OnEmptyRecycleBin?.Invoke();
            return EmptyRecycleBinResult;
        }

        public bool FlushDnsCache() => FlushDnsResult;
    }

    [Theory]
    [InlineData(-10, "0 B")]
    [InlineData(0, "0 B")]
    [InlineData(512, "512 B")]
    [InlineData(1024, "1,00 KB")]
    [InlineData(1024 * 1024, "1,00 MB")]
    [InlineData(1024L * 1024 * 1024 * 5, "5,00 GB")]
    [InlineData(1024L * 1024 * 1024 * 1024 * 2, "2,00 TB")]
    public void FormatByteSize_FormatsCorrectly(long bytes, string expected)
    {
        string actual = DiskCleanerService.FormatByteSize(bytes);
        string normalizedActual = actual.Replace('.', ',');
        string normalizedExpected = expected.Replace('.', ',');
        Assert.Equal(normalizedExpected, normalizedActual);
    }

    [Fact]
    public async Task ScanAsync_SafetyContract_DefaultsOnlySafeCategoriesSelected()
    {
        var service = new DiskCleanerService();
        var result = await service.ScanAsync();

        Assert.NotNull(result);
        Assert.Equal(8, result.Categories.Count);

        var safeCategoryIds = new HashSet<string>
        {
            DiskCleanerService.CategoryUserTemp,
            DiskCleanerService.CategoryBrowserCache,
            DiskCleanerService.CategoryGpuCache,
            DiskCleanerService.CategoryDnsCache
        };

        var cautionCategoryIds = new HashSet<string>
        {
            DiskCleanerService.CategoryRecycleBin,
            DiskCleanerService.CategoryDevCache,
            DiskCleanerService.CategoryCrashDumps,
            DiskCleanerService.CategoryWindowsTemp
        };

        foreach (var category in result.Categories)
        {
            if (safeCategoryIds.Contains(category.Id))
            {
                Assert.True(category.IsSafe, $"Category {category.Id} should be marked IsSafe = true");
                Assert.True(category.IsSelected, $"Category {category.Id} should be selected by default");
                Assert.Equal(CategorySafetyLevel.Safe, category.SafetyLevel);
                Assert.Null(category.WarningKey);
            }
            else if (cautionCategoryIds.Contains(category.Id))
            {
                Assert.False(category.IsSafe, $"Category {category.Id} should be marked IsSafe = false (Caution)");
                Assert.False(category.IsSelected, $"Category {category.Id} should NOT be selected by default");
                Assert.Equal(CategorySafetyLevel.Caution, category.SafetyLevel);
                Assert.False(string.IsNullOrWhiteSpace(category.WarningKey), $"Category {category.Id} must have a WarningKey");
            }
        }
    }

    [Fact]
    public async Task CleanAsync_LockedFiles_ReportsPartiallyCleaned()
    {
        var mockFs = new MockFileSystemOperations();
        string tempPath = Path.GetTempPath();
        mockFs.ExistingDirectories.Add(tempPath);
        mockFs.CleanResults[tempPath] = (Freed: 500, Cleaned: 2, Locked: 3);

        var service = new DiskCleanerService(fs: mockFs);
        var result = await service.CleanAsync(new HashSet<string> { DiskCleanerService.CategoryUserTemp });

        Assert.NotNull(result);
        Assert.Equal(500, result.TotalFreedBytes);
        Assert.Equal(2, result.TotalCleanedCount);
        Assert.Equal(3, result.TotalLockedCount);
        Assert.True(result.HasPartial);

        var report = Assert.Single(result.Reports);
        Assert.Equal(DiskCleanerService.CategoryUserTemp, report.CategoryId);
        Assert.Equal(DiskCleanCategoryStatus.PartiallyCleaned, report.Status);
        Assert.Equal(3, report.LockedCount);
        Assert.Contains("locked", report.FailureReason ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CleanAsync_Win32RecycleBinFailure_ReportsFailedWithoutFakeSuccess()
    {
        var mockFs = new MockFileSystemOperations();
        var mockWin32 = new MockWin32Operations { EmptyRecycleBinResult = unchecked((int)0x80070005) }; // E_ACCESSDENIED

        var service = new DiskCleanerService(fs: mockFs, win32: mockWin32);
        var result = await service.CleanAsync(new HashSet<string> { DiskCleanerService.CategoryRecycleBin });

        Assert.NotNull(result);
        Assert.True(result.HasErrors);
        var report = Assert.Single(result.Reports);
        Assert.Equal(DiskCleanCategoryStatus.Failed, report.Status);
        Assert.Contains("0x80070005", report.FailureReason ?? string.Empty);
    }

    [Fact]
    public async Task CleanAsync_RecycleBinPartialSuccess_ReportsPartiallyCleaned()
    {
        string recycleBinPath = "C:\\$Recycle.Bin";
        var mockFs = new MockFileSystemOperations();
        mockFs.FixedDrives = ["C:\\"];
        mockFs.ExistingDirectories.Add(recycleBinPath);
        mockFs.DirectorySizes[recycleBinPath] = 1000;

        var mockWin32 = new MockWin32Operations
        {
            EmptyRecycleBinResult = unchecked((int)0x80004005), // E_FAIL but freed 800 bytes
            OnEmptyRecycleBin = () =>
            {
                // After empty call, only 200 bytes remain
                mockFs.DirectorySizes[recycleBinPath] = 200;
            }
        };

        var service = new DiskCleanerService(fs: mockFs, win32: mockWin32);
        var result = await service.CleanAsync(new HashSet<string> { DiskCleanerService.CategoryRecycleBin });

        Assert.NotNull(result);
        Assert.True(result.HasPartial);
        Assert.Equal(800, result.TotalFreedBytes);

        var report = Assert.Single(result.Reports);
        Assert.Equal(DiskCleanerService.CategoryRecycleBin, report.CategoryId);
        Assert.Equal(DiskCleanCategoryStatus.PartiallyCleaned, report.Status);
        Assert.Equal(800, report.FreedBytes);
        Assert.Contains("0x80004005", report.FailureReason ?? string.Empty);
    }

    [Fact]
    public async Task CleanAsync_DnsFlushFailure_ReportsFailed()
    {
        var mockWin32 = new MockWin32Operations { FlushDnsResult = false };
        var service = new DiskCleanerService(win32: mockWin32);

        var result = await service.CleanAsync(new HashSet<string> { DiskCleanerService.CategoryDnsCache });

        Assert.NotNull(result);
        Assert.True(result.HasErrors);
        var report = Assert.Single(result.Reports);
        Assert.Equal(DiskCleanCategoryStatus.Failed, report.Status);
        Assert.Equal(0, report.CleanedCount);
    }

    [Fact]
    public async Task CleanAsync_DnsFlushSuccess_ReportsSucceeded()
    {
        var mockWin32 = new MockWin32Operations { FlushDnsResult = true };
        var service = new DiskCleanerService(win32: mockWin32);

        var result = await service.CleanAsync(new HashSet<string> { DiskCleanerService.CategoryDnsCache });

        Assert.NotNull(result);
        Assert.False(result.HasErrors);
        var report = Assert.Single(result.Reports);
        Assert.Equal(DiskCleanCategoryStatus.Succeeded, report.Status);
        Assert.Equal(1, report.CleanedCount);
    }

    [Fact]
    public async Task CleanAsync_DevCacheProcessFailure_ReportsFailedNotFakeSuccess()
    {
        var mockRunner = new MockProcessRunner
        {
            Handler = (cmd, args) => new ProcessRunResult(false, 1, false, string.Empty, "Command failed.")
        };

        var service = new DiskCleanerService(processRunner: mockRunner);
        var result = await service.CleanAsync(new HashSet<string> { DiskCleanerService.CategoryDevCache });

        Assert.NotNull(result);
        Assert.True(result.HasErrors);
        var report = Assert.Single(result.Reports);
        Assert.Equal(DiskCleanCategoryStatus.Failed, report.Status);
    }

    [Fact]
    public async Task CleanAsync_DevCacheTimeout_ReportsFailedWithTimeoutReason()
    {
        var mockRunner = new MockProcessRunner
        {
            Handler = (cmd, args) => new ProcessRunResult(false, -1, true, string.Empty, "Process timed out.")
        };

        var service = new DiskCleanerService(processRunner: mockRunner);
        var result = await service.CleanAsync(new HashSet<string> { DiskCleanerService.CategoryDevCache });

        Assert.NotNull(result);
        Assert.True(result.HasErrors);
        var report = Assert.Single(result.Reports);
        Assert.Equal(DiskCleanCategoryStatus.Failed, report.Status);
        Assert.Contains("timed out", report.FailureReason ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CleanAsync_DevCacheProcessSuccess_ReportsSucceeded()
    {
        var mockRunner = new MockProcessRunner
        {
            Handler = (cmd, args) => new ProcessRunResult(true, 0, false, "success", string.Empty)
        };

        var service = new DiskCleanerService(processRunner: mockRunner);
        var result = await service.CleanAsync(new HashSet<string> { DiskCleanerService.CategoryDevCache });

        Assert.NotNull(result);
        Assert.False(result.HasErrors);
        var report = Assert.Single(result.Reports);
        Assert.Equal(DiskCleanCategoryStatus.Succeeded, report.Status);
    }

    [Fact]
    public async Task CleanAsync_BrowserCache_AccessErrors_ReportsPartiallyCleaned()
    {
        var mockFs = new MockFileSystemOperations();
        string localAppData = mockFs.GetSpecialFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string chromeUserData = Path.Combine(localAppData, "Google", "Chrome", "User Data");
        string edgeUserData = Path.Combine(localAppData, "Microsoft", "Edge", "User Data");

        mockFs.ExistingDirectories.Add(chromeUserData);
        mockFs.ExistingDirectories.Add(edgeUserData);

        string chromeProfile = Path.Combine(chromeUserData, "Default");
        string chromeCache = Path.Combine(chromeProfile, "Cache");
        mockFs.ExistingDirectories.Add(chromeCache);
        mockFs.CleanResults[chromeCache] = (Freed: 500, Cleaned: 5, Locked: 0);

        mockFs.EnumerateDirectoriesHandler = path =>
        {
            if (path.Equals(chromeUserData, StringComparison.OrdinalIgnoreCase))
                return [chromeProfile];
            if (path.Equals(edgeUserData, StringComparison.OrdinalIgnoreCase))
                throw new UnauthorizedAccessException("Access denied to Edge User Data");
            return [];
        };

        var service = new DiskCleanerService(fs: mockFs);
        var result = await service.CleanAsync(new HashSet<string> { DiskCleanerService.CategoryBrowserCache });

        Assert.NotNull(result);
        Assert.True(result.HasPartial);
        var report = Assert.Single(result.Reports);
        Assert.Equal(DiskCleanCategoryStatus.PartiallyCleaned, report.Status);
        Assert.Contains("access errors", report.FailureReason ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CleanAsync_GpuCache_LockedFiles_ReportsPartiallyCleaned()
    {
        var mockFs = new MockFileSystemOperations();
        string localAppData = mockFs.GetSpecialFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string d3dCache = Path.Combine(localAppData, "D3DSCache");
        mockFs.ExistingDirectories.Add(d3dCache);
        mockFs.CleanResults[d3dCache] = (Freed: 300, Cleaned: 2, Locked: 3);

        var service = new DiskCleanerService(fs: mockFs);
        var result = await service.CleanAsync(new HashSet<string> { DiskCleanerService.CategoryGpuCache });

        Assert.NotNull(result);
        Assert.True(result.HasPartial);
        var report = Assert.Single(result.Reports);
        Assert.Equal(DiskCleanCategoryStatus.PartiallyCleaned, report.Status);
        Assert.Equal(3, report.LockedCount);
    }

    [Fact]
    public async Task CleanAsync_CrashDumps_LockedFiles_ReportsPartiallyCleaned()
    {
        var mockFs = new MockFileSystemOperations();
        string localAppData = mockFs.GetSpecialFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string crashDumps = Path.Combine(localAppData, "CrashDumps");
        mockFs.ExistingDirectories.Add(crashDumps);
        mockFs.CleanResults[crashDumps] = (Freed: 150, Cleaned: 1, Locked: 2);

        var service = new DiskCleanerService(fs: mockFs);
        var result = await service.CleanAsync(new HashSet<string> { DiskCleanerService.CategoryCrashDumps });

        Assert.NotNull(result);
        Assert.True(result.HasPartial);
        var report = Assert.Single(result.Reports);
        Assert.Equal(DiskCleanCategoryStatus.PartiallyCleaned, report.Status);
        Assert.Equal(2, report.LockedCount);
    }

    [Fact]
    public async Task CleanAsync_DevCache_TimeoutOnOneTool_ReportsPartiallyCleaned()
    {
        var mockRunner = new MockProcessRunner
        {
            Handler = (cmd, args) =>
            {
                if (cmd == "dotnet")
                    return new ProcessRunResult(true, 0, false, "success", string.Empty);
                if (cmd == "pip")
                    return new ProcessRunResult(false, -1, true, string.Empty, "Timed out");
                return new ProcessRunResult(false, -1, false, string.Empty, "npm not found");
            }
        };

        var service = new DiskCleanerService(processRunner: mockRunner);
        var result = await service.CleanAsync(new HashSet<string> { DiskCleanerService.CategoryDevCache });

        Assert.NotNull(result);
        Assert.True(result.HasPartial);
        var report = Assert.Single(result.Reports);
        Assert.Equal(DiskCleanCategoryStatus.PartiallyCleaned, report.Status);
        Assert.Equal(1, report.CleanedCount);
        Assert.Equal(1, report.LockedCount);
        Assert.Contains("timed out", report.FailureReason ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CleanAsync_DevCache_CancellationMidSequence_AbortsImmediately()
    {
        using var cts = new CancellationTokenSource();
        int dotnetCalls = 0;
        int npmCalls = 0;

        var mockRunner = new MockProcessRunner
        {
            Handler = (cmd, args) =>
            {
                if (cmd == "dotnet")
                {
                    dotnetCalls++;
                    cts.Cancel(); // cancel mid-sequence right after dotnet
                    return new ProcessRunResult(true, 0, false, "ok", string.Empty);
                }
                if (cmd == "npm")
                {
                    npmCalls++;
                    return new ProcessRunResult(true, 0, false, "ok", string.Empty);
                }
                return new ProcessRunResult(true, 0, false, "ok", string.Empty);
            }
        };

        var service = new DiskCleanerService(processRunner: mockRunner);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.CleanAsync(new HashSet<string> { DiskCleanerService.CategoryDevCache }, cancellationToken: cts.Token));

        Assert.Equal(1, dotnetCalls);
        Assert.Equal(0, npmCalls);
    }

    [Fact]
    public async Task SystemProcessRunner_Cancellation_ThrowsOperationCanceledException()
    {
        var runner = new SystemProcessRunner();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            runner.RunSilentProcessAsync("cmd.exe", "/c ping 127.0.0.1", TimeSpan.FromSeconds(5), cts.Token));
    }

    [Fact]
    public async Task SystemProcessRunner_LargeOutput_ReadsConcurrentlyWithoutDeadlock()
    {
        var runner = new SystemProcessRunner();
        // Generate a large output in cmd to ensure stream buffering and concurrent drain work properly
        var result = await runner.RunSilentProcessAsync(
            "cmd.exe",
            "/c for /L %i in (1,1,200) do @echo Line %i of large process output for testing stream draining",
            TimeSpan.FromSeconds(10));

        Assert.True(result.Success);
        Assert.Equal(0, result.ExitCode);
        Assert.False(result.TimedOut);
        Assert.Contains("Line 200 of large process output", result.StandardOutput);
    }

    [Fact]
    public async Task SystemProcessRunner_Timeout_ReturnsTimedOutResult()
    {
        var runner = new SystemProcessRunner();
        var result = await runner.RunSilentProcessAsync(
            "powershell.exe",
            "-NoProfile -Command \"Start-Sleep -Seconds 5\"",
            TimeSpan.FromMilliseconds(200));

        Assert.False(result.Success);
        Assert.True(result.TimedOut);
    }

    [Fact]
    public async Task ScanAsync_Cancellation_ThrowsOperationCanceledException()
    {
        var service = new DiskCleanerService();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.ScanAsync(cancellationToken: cts.Token));
    }

    [Fact]
    public async Task CleanAsync_Cancellation_ThrowsOperationCanceledException()
    {
        var service = new DiskCleanerService();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.CleanAsync(new HashSet<string> { DiskCleanerService.CategoryUserTemp }, cancellationToken: cts.Token));
    }

    [Fact]
    public void WindowsFileSystemOperations_CalculateAndClean()
    {
        var fs = new WindowsFileSystemOperations();
        string tempDir = Path.Combine(Path.GetTempPath(), "AiteBarDiskCleanerTest_" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(tempDir);
            File.WriteAllBytes(Path.Combine(tempDir, "file1.bin"), new byte[150]);
            string subDir = Path.Combine(tempDir, "sub");
            Directory.CreateDirectory(subDir);
            File.WriteAllBytes(Path.Combine(subDir, "file2.bin"), new byte[250]);

            long size = fs.CalculateDirectorySize(tempDir);
            Assert.Equal(400, size);

            var (freed, cleaned, locked) = fs.CleanDirectoryContents(tempDir);
            Assert.Equal(400, freed);
            Assert.Equal(0, locked);
            Assert.True(fs.DirectoryExists(tempDir));
            Assert.Empty(Directory.GetFiles(tempDir));
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }
}
