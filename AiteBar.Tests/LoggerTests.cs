using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AiteBar;
using Xunit;

namespace AiteBar.Tests;

public sealed class LoggerTests
{
    [Fact]
    public async Task Log_WritesExceptionTextToLogFile()
    {
        using var scope = new LogArtifactScope();

        Logger.Log(new InvalidOperationException("logger smoke"));
        await Logger.WaitForFlushAsync();

        Assert.True(File.Exists(PathHelper.LogFile));
        string content = File.ReadAllText(PathHelper.LogFile);
        Assert.Contains("logger smoke", content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Log_NormalizesEmbeddedNewlinesInExceptionText()
    {
        using var scope = new LogArtifactScope();

        Logger.Log(new InvalidOperationException("first line\nsecond line"));
        await Logger.WaitForFlushAsync();

        string content = File.ReadAllText(PathHelper.LogFile);

        Assert.Contains("first line | second line", content, StringComparison.Ordinal);
        Assert.DoesNotContain("first line\nsecond line", content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LogAsync_WritesExceptionTextToLogFile()
    {
        using var scope = new LogArtifactScope();

        await Logger.LogAsync(new InvalidOperationException("async logger smoke"));
        await Logger.WaitForFlushAsync();

        Assert.True(File.Exists(PathHelper.LogFile));
        string content = File.ReadAllText(PathHelper.LogFile);
        Assert.Contains("async logger smoke", content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Log_ConcurrentBurstPersistsEveryEntryBeforeFlushCompletes()
    {
        using var scope = new LogArtifactScope();
        const int entryCount = 200;

        await Task.WhenAll(Enumerable.Range(0, entryCount)
            .Select(index => Task.Run(() =>
                Logger.Log(new InvalidOperationException($"concurrent-entry-{index:D3}")))));
        await Logger.WaitForFlushAsync();

        string content = File.ReadAllText(PathHelper.LogFile);
        for (int index = 0; index < entryCount; index++)
        {
            Assert.Contains($"concurrent-entry-{index:D3}", content, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task Log_RotatesOversizedLogAndKeepsAtMostThreeBackups()
    {
        using var scope = new LogArtifactScope();
        Directory.CreateDirectory(PathHelper.AppDataFolder);

        File.WriteAllText(PathHelper.LogFile, new string('X', 1024 * 1024 + 128));
        string directory = Path.GetDirectoryName(PathHelper.LogFile)!;
        string fileName = Path.GetFileName(PathHelper.LogFile);

        for (int i = 0; i < 2; i++)
        {
            string backupPath = Path.Combine(directory, $"{fileName}.2000010101010{i}.bak");
            File.WriteAllText(backupPath, $"backup-{i}");
            File.SetCreationTimeUtc(backupPath, DateTime.UtcNow.AddMinutes(-(10 + i)));
        }

        Logger.Log(new InvalidOperationException("rotation"));
        await Logger.WaitForFlushAsync();

        string[] backups = Directory.GetFiles(directory, $"{fileName}.*.bak");

        Assert.True(File.Exists(PathHelper.LogFile));
        Assert.Contains("rotation", File.ReadAllText(PathHelper.LogFile), StringComparison.Ordinal);
        Assert.True(backups.Length <= 3, $"Expected at most 3 backups, found {backups.Length}.");
        Assert.Contains(backups, path => new FileInfo(path).Length > 1024 * 1024);
    }

    private sealed class LogArtifactScope : IDisposable
    {
        private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), "AiteBarTests", Guid.NewGuid().ToString("N"));

        public LogArtifactScope()
        {
            Directory.CreateDirectory(_tempRoot);
            PathHelper.SetAppDataFolderOverride(_tempRoot);
            Directory.CreateDirectory(Path.GetDirectoryName(PathHelper.LogFile)!);
        }

        public void Dispose()
        {
            PathHelper.ClearAppDataFolderOverride();
            try
            {
                Directory.Delete(_tempRoot, recursive: true);
            }
            catch
            {
            }
        }
    }
}
