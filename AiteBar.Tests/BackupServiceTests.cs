using AiteBar;
using System;
using System.Collections.Generic;
using System.IO;

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
