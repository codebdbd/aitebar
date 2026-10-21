using AiteBar;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace AiteBar.Tests;

public sealed class BackupServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "AiteBarTests", Guid.NewGuid().ToString("N"));

    public BackupServiceTests()
    {
        Directory.CreateDirectory(_root);
        PathHelper.SetAppDataFolderOverride(_root);
    }

    [Fact]
    public async Task CreateAndReadEncryptedBackup_RoundTripsSettingsIconsAndCredentials()
    {
        string iconPath = Path.Combine(PathHelper.IconsFolder, "custom", "icon.bin");
        Directory.CreateDirectory(Path.GetDirectoryName(iconPath)!);
        await File.WriteAllBytesAsync(iconPath, [1, 2, 3]);

        var credentials = new FakeCredentialStore();
        const string target = "AiteBar/AI/test";
        credentials.Write(target, "secret-value");
        var settings = new AppSettings
        {
            ActiveContextId = "context-7",
            Ai = new AiSettings { Connections = [new AiConnectionSettings { CredentialTarget = target }] }
        };
        string archive = Path.Combine(_root, "backup.aitebarbackup");

        var service = new BackupService(credentials);
        await service.CreateAsync(archive, settings, new BackupCreateOptions(true, false, "correct password"));
        BackupReadResult result = await service.ReadAsync(archive, new BackupRestoreOptions("correct password"));

        Assert.Equal("context-7", result.Settings.ActiveContextId);
        Assert.Equal("secret-value", result.Secrets!.Credentials[target]);
        Assert.Equal(new byte[] { 1, 2, 3 }, result.Icons["custom/icon.bin"]);
    }

    [Fact]
    public async Task ReadEncryptedBackup_WrongPasswordIsRejected()
    {
        var credentials = new FakeCredentialStore();
        string archive = Path.Combine(_root, "backup.aitebarbackup");
        var service = new BackupService(credentials);
        await service.CreateAsync(archive, new AppSettings(), new BackupCreateOptions(true, false, "correct password"));

        await Assert.ThrowsAsync<InvalidDataException>(() => service.ReadAsync(archive, new BackupRestoreOptions("wrong password")));
    }

    [Fact]
    public async Task ReadAsync_NonExistentFile_ThrowsFileNotFoundException()
    {
        var service = new BackupService(new FakeCredentialStore());
        string path = Path.Combine(_root, "does-not-exist.aitebarbackup");

        await Assert.ThrowsAsync<FileNotFoundException>(() => service.ReadAsync(path, new BackupRestoreOptions(null)));
    }

    [Fact]
    public async Task ReadAsync_CorruptZipBytes_ThrowsInvalidDataException()
    {
        string archive = Path.Combine(_root, "corrupt.aitebarbackup");
        await File.WriteAllBytesAsync(archive, [0x00, 0x11, 0x22, 0x33, 0x44, 0x55]);
        var service = new BackupService(new FakeCredentialStore());

        await Assert.ThrowsAsync<InvalidDataException>(() => service.ReadAsync(archive, new BackupRestoreOptions(null)));
    }

    [Fact]
    public async Task ReadAsync_MissingSettingsEntry_ThrowsInvalidDataException()
    {
        string archive = Path.Combine(_root, "no-settings.aitebarbackup");
        CreateZipWithEntries(archive, zip =>
        {
            ZipArchiveEntry entry = zip.CreateEntry("icons/dummy.png");
            using var stream = entry.Open();
            stream.Write([1, 2, 3]);
        });

        var service = new BackupService(new FakeCredentialStore());
        var ex = await Assert.ThrowsAsync<InvalidDataException>(() => service.ReadAsync(archive, new BackupRestoreOptions(null)));
        Assert.Contains("settings", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ReadAsync_ExceedingMaxArchiveEntries_ThrowsInvalidDataException()
    {
        string archive = Path.Combine(_root, "too-many-entries.aitebarbackup");
        CreateZipWithEntries(archive, zip =>
        {
            ZipArchiveEntry settings = zip.CreateEntry("settings.json");
            using (var sw = new StreamWriter(settings.Open()))
            {
                sw.Write(JsonSerializer.Serialize(new AppSettings()));
            }

            for (int i = 0; i < 1005; i++)
            {
                zip.CreateEntry($"icons/icon_{i}.png");
            }
        });

        var service = new BackupService(new FakeCredentialStore());
        var ex = await Assert.ThrowsAsync<InvalidDataException>(() => service.ReadAsync(archive, new BackupRestoreOptions(null)));
        Assert.Contains("too many entries", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("icons/../outside.png")]
    [InlineData("icons/../../outside.png")]
    [InlineData("icons/subdir/../../outside.png")]
    [InlineData("icons/c:/windows/system32/cmd.exe")]
    public async Task ReadAsync_PathTraversalInIconEntry_ThrowsInvalidDataException(string maliciousPath)
    {
        string archive = Path.Combine(_root, "path-traversal.aitebarbackup");
        CreateZipWithEntries(archive, zip =>
        {
            ZipArchiveEntry settings = zip.CreateEntry("settings.json");
            using (var sw = new StreamWriter(settings.Open()))
            {
                sw.Write(JsonSerializer.Serialize(new AppSettings()));
            }

            ZipArchiveEntry malicious = zip.CreateEntry(maliciousPath);
            using (var s = malicious.Open())
            {
                s.Write([1, 2, 3]);
            }
        });

        var service = new BackupService(new FakeCredentialStore());
        var ex = await Assert.ThrowsAsync<InvalidDataException>(() => service.ReadAsync(archive, new BackupRestoreOptions(null)));
        Assert.Contains("invalid icon path", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void IsSafeRelativePath_RejectsTraversalAndRootedPaths()
    {
        Assert.False(BackupService.IsSafeRelativePath(""));
        Assert.False(BackupService.IsSafeRelativePath("   "));
        Assert.False(BackupService.IsSafeRelativePath(".."));
        Assert.False(BackupService.IsSafeRelativePath("../test.png"));
        Assert.False(BackupService.IsSafeRelativePath("sub/../../test.png"));
        Assert.False(BackupService.IsSafeRelativePath(@"sub\..\..\test.png"));
        Assert.False(BackupService.IsSafeRelativePath("C:/test.png"));
        Assert.False(BackupService.IsSafeRelativePath("/test.png"));

        Assert.True(BackupService.IsSafeRelativePath("test.png"));
        Assert.True(BackupService.IsSafeRelativePath("sub/test.png"));
        Assert.True(BackupService.IsSafeRelativePath(@"sub\test.png"));
    }

    [Fact]
    public async Task RestoreAsync_ValidBackup_RestoresSettingsAndWritesIcons()
    {
        string originalIcon = Path.Combine(PathHelper.IconsFolder, "test", "myicon.bin");
        Directory.CreateDirectory(Path.GetDirectoryName(originalIcon)!);
        await File.WriteAllBytesAsync(originalIcon, [42, 43, 44]);

        var settings = new AppSettings
        {
            ActiveContextId = "context-3",
            Contexts =
            [
                new PanelContext { Id = "context-0", Name = "Main" },
                new PanelContext { Id = "context-3", Name = "Work" }
            ],
            PanelSizePercent = 75
        };

        string archive = Path.Combine(_root, "valid.aitebarbackup");
        var service = new BackupService(new FakeCredentialStore());
        await service.CreateAsync(archive, settings, new BackupCreateOptions(false, false, null));

        // Delete the icon and modify settings service
        File.Delete(originalIcon);
        Assert.False(File.Exists(originalIcon));

        var settingsService = new AppSettingsService();
        settingsService.Settings.ActiveContextId = "old-context";
        settingsService.Settings.PanelSizePercent = 50;

        await service.RestoreAsync(archive, new BackupRestoreOptions(null), settingsService);

        Assert.Equal("context-3", settingsService.Settings.ActiveContextId);
        Assert.Equal(75, settingsService.Settings.PanelSizePercent);
        Assert.True(File.Exists(originalIcon));
        byte[] restoredBytes = await File.ReadAllBytesAsync(originalIcon);
        Assert.Equal(new byte[] { 42, 43, 44 }, restoredBytes);
    }

    private static void CreateZipWithEntries(string zipPath, Action<ZipArchive> populate)
    {
        using var stream = File.Create(zipPath);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Create, false);
        populate(archive);
    }

    public void Dispose()
    {
        PathHelper.ClearAppDataFolderOverride();
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private sealed class FakeCredentialStore : IAiCredentialStore
    {
        private readonly Dictionary<string, string> _values = new(StringComparer.Ordinal);
        public void Write(string target, string secret) => _values[target] = secret;
        public string? Read(string target) => _values.GetValueOrDefault(target);
        public bool Delete(string target) => _values.Remove(target);
    }
}
