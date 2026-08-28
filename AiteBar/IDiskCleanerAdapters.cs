using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;

namespace AiteBar;

public sealed record ProcessRunResult(
    bool Success,
    int ExitCode,
    bool TimedOut,
    string StandardOutput,
    string StandardError);

public interface IFileSystemOperations
{
    long CalculateDirectorySize(string directoryPath);
    (long FreedBytes, int CleanedFiles, int LockedFiles) CleanDirectoryContents(string directoryPath);
    bool DirectoryExists(string path);
    IEnumerable<string> EnumerateFixedDriveRootPaths();
    string GetSpecialFolderPath(Environment.SpecialFolder folder);
    IEnumerable<string> EnumerateDirectories(string path);
}

public interface IProcessRunner
{
    Task<ProcessRunResult> RunSilentProcessAsync(
        string fileName,
        string arguments,
        TimeSpan timeout,
        CancellationToken cancellationToken = default);
}

public interface IWin32DiskOperations
{
    int EmptyRecycleBin(string? rootPath);
    bool FlushDnsCache();
}

[SupportedOSPlatform("windows6.1")]
public sealed class WindowsFileSystemOperations : IFileSystemOperations
{
    public long CalculateDirectorySize(string directoryPath)
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

    public (long FreedBytes, int CleanedFiles, int LockedFiles) CleanDirectoryContents(string directoryPath)
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
                var (innerFreed, innerCleaned, innerLocked) = CleanDirectoryContents(subDir.FullName);
                freed += innerFreed;
                cleaned += innerCleaned;
                locked += innerLocked;
            }
        }

        return (freed, cleaned, locked);
    }

    public bool DirectoryExists(string path) => Directory.Exists(path);

    public IEnumerable<string> EnumerateFixedDriveRootPaths()
    {
        foreach (var drive in DriveInfo.GetDrives())
        {
            if (drive.DriveType == DriveType.Fixed)
            {
                yield return drive.RootDirectory.FullName;
            }
        }
    }

    public string GetSpecialFolderPath(Environment.SpecialFolder folder) =>
        Environment.GetFolderPath(folder);

    public IEnumerable<string> EnumerateDirectories(string path)
    {
        if (!Directory.Exists(path)) return [];
        try
        {
            return Directory.EnumerateDirectories(path);
        }
        catch
        {
            return [];
        }
    }
}

public sealed class SystemProcessRunner : IProcessRunner
{
    public async Task<ProcessRunResult> RunSilentProcessAsync(
        string fileName,
        string arguments,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            if (!process.Start())
            {
                return new ProcessRunResult(false, -1, false, string.Empty, "Process could not start.");
            }

            using var timeoutCts = new CancellationTokenSource(timeout);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            var stdoutTask = process.StandardOutput.ReadToEndAsync(linkedCts.Token);
            var stderrTask = process.StandardError.ReadToEndAsync(linkedCts.Token);

            try
            {
                await Task.WhenAll(process.WaitForExitAsync(linkedCts.Token), stdoutTask, stderrTask).ConfigureAwait(false);
                string stdout = await stdoutTask.ConfigureAwait(false);
                string stderr = await stderrTask.ConfigureAwait(false);

                return new ProcessRunResult(
                    process.ExitCode == 0,
                    process.ExitCode,
                    false,
                    stdout,
                    stderr);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                try { process.Kill(true); } catch { }
                throw;
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
            {
                try { process.Kill(true); } catch { }
                return new ProcessRunResult(false, -1, true, string.Empty, "Process timed out.");
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new ProcessRunResult(false, -1, false, string.Empty, ex.Message);
        }
    }
}

[SupportedOSPlatform("windows6.1")]
public sealed class NativeWin32DiskOperations : IWin32DiskOperations
{
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHEmptyRecycleBin(IntPtr hwnd, string? pszRootPath, uint dwFlags);

    [DllImport("dnsapi.dll", EntryPoint = "DnsFlushResolverCache")]
    private static extern int DnsFlushResolverCache();

    private const uint SHERB_NOCONFIRMATION = 0x00000001;
    private const uint SHERB_NOPROGRESSUI = 0x00000002;
    private const uint SHERB_NOSOUND = 0x00000004;

    public int EmptyRecycleBin(string? rootPath)
    {
        try
        {
            return SHEmptyRecycleBin(IntPtr.Zero, rootPath, SHERB_NOCONFIRMATION | SHERB_NOPROGRESSUI | SHERB_NOSOUND);
        }
        catch (Exception ex)
        {
            return ex.HResult != 0 ? ex.HResult : -1;
        }
    }

    public bool FlushDnsCache()
    {
        try
        {
            int res = DnsFlushResolverCache();
            return res != 0;
        }
        catch
        {
            return false;
        }
    }
}
