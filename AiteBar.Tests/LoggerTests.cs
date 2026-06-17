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
        private readonly string _directory = Path.GetDirectoryName(PathHelper.LogFile)!;
        private readonly string _backupRoot = Path.Combine(Path.GetTempPath(), "AiteBarTests", Guid.NewGuid().ToString("N"));
        private readonly List<string> _originalFiles = [];

        public LogArtifactScope()
        {
            Directory.CreateDirectory(_directory);
            Directory.CreateDirectory(_backupRoot);

            string fileName = Path.GetFileName(PathHelper.LogFile);
            foreach (string path in Directory.GetFiles(_directory, $"{fileName}*"))
            {
                string destination = Path.Combine(_backupRoot, Path.GetFileName(path));
                File.Copy(path, destination, overwrite: true);
                _originalFiles.Add(destination);
                File.Delete(path);
            }
        }

        public void Dispose()
        {
            string fileName = Path.GetFileName(PathHelper.LogFile);
            foreach (string path in Directory.GetFiles(_directory, $"{fileName}*"))
            {
                File.Delete(path);
            }

            foreach (string backupPath in _originalFiles)
            {
                string restorePath = Path.Combine(_directory, Path.GetFileName(backupPath));
                File.Copy(backupPath, restorePath, overwrite: true);
            }

            if (Directory.Exists(_backupRoot))
            {
                Directory.Delete(_backupRoot, recursive: true);
            }
        }
    }
}
