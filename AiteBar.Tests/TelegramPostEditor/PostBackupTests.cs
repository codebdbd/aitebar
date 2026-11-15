using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using AiteBar.TelegramPostEditor.Models;
using AiteBar.TelegramPostEditor.Persistence;
using AiteBar.TelegramPostEditor.Services;

namespace AiteBar.Tests.TelegramPostEditor;

public sealed class PostBackupTests : IDisposable
{
    private readonly string _tempDir;
    private readonly PostDbMigrator _migrator;
    private readonly PostRepository _repository;
    private readonly PostMediaStore _mediaStore;
    private readonly PostBackupService _backupService;

    public PostBackupTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"aitebar_tg_backup_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _migrator = new PostDbMigrator(Path.Combine(_tempDir, "posts.db"));
        _repository = new PostRepository(_migrator);
        _mediaStore = new PostMediaStore(_migrator, Path.Combine(_tempDir, "Media"));
        _backupService = new PostBackupService(_repository, _mediaStore);
    }

    [Fact]
    public void ExportAndImportLibraryArchive_RoundTripsAndRejectsPathTraversal()
    {
        var post = new PostDocument
        {
            Title = "Backup Test",
            Blocks = [new PostBlock { Runs = [new PostTextRun { Text = "Content without secrets" }] }]
        };
        _repository.SavePost(post);

        string zipPath = Path.Combine(_tempDir, "library.zip");
        _backupService.ExportLibraryArchive(zipPath);
        Assert.True(File.Exists(zipPath));

        _repository.PermanentlyDeletePost(post.Id);
        Assert.Empty(_repository.SearchPosts(new PostSearchFilter { IncludeDeleted = true }));

        int imported = _backupService.ImportLibraryArchive(zipPath);
        Assert.Equal(1, imported);
        Assert.Single(_repository.SearchPosts());

        // Verify path traversal is rejected
        string evilZip = Path.Combine(_tempDir, "evil.zip");
        using (FileStream fs = File.Create(evilZip))
        using (var archive = new ZipArchive(fs, ZipArchiveMode.Create))
        {
            WriteText(archive, "manifest.json", "{\"schemaVersion\":1,\"exportedUtc\":\"2026-11-15T00:00:00Z\",\"postCount\":0,\"fileSha256\":{}}");
            WriteText(archive, "../../evil.txt", "bad");
        }

        Assert.Throws<InvalidDataException>(() => _backupService.ImportLibraryArchive(evilZip));
    }

    private static void WriteText(ZipArchive archive, string name, string content)
    {
        ZipArchiveEntry entry = archive.CreateEntry(name);
        using Stream s = entry.Open();
        byte[] bytes = Encoding.UTF8.GetBytes(content);
        s.Write(bytes, 0, bytes.Length);
    }

    public void Dispose()
    {
        try
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, recursive: true);
            }
        }
        catch
        {
        }
    }
}
