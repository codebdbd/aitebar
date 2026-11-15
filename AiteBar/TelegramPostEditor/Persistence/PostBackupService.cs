using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AiteBar.TelegramPostEditor.Models;
using AiteBar.TelegramPostEditor.Services;

namespace AiteBar.TelegramPostEditor.Persistence;

public sealed record LibraryArchiveManifest(
    int SchemaVersion,
    DateTime ExportedUtc,
    int PostCount,
    Dictionary<string, string> FileSha256);

public sealed class PostBackupService
{
    private const string PostsEntry = "posts.json";
    private const string ManifestEntry = "manifest.json";
    private const string MediaPrefix = "media/";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly PostRepository _repository;
    private readonly PostMediaStore _mediaStore;

    public PostBackupService(PostRepository repository, PostMediaStore mediaStore)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _mediaStore = mediaStore ?? throw new ArgumentNullException(nameof(mediaStore));
    }

    public string ExportPostToJson(PostDocument post) =>
        JsonSerializer.Serialize(new { schemaVersion = 1, post }, JsonOptions);

    public PostDocument ImportPostFromJson(string json)
    {
        using JsonDocument doc = JsonDocument.Parse(json);
        JsonElement postElement = doc.RootElement.TryGetProperty("post", out JsonElement p) ? p : doc.RootElement;
        PostDocument imported = JsonSerializer.Deserialize<PostDocument>(postElement.GetRawText(), JsonOptions)
            ?? throw new InvalidOperationException("Invalid post JSON.");

        imported.Id = Guid.NewGuid().ToString("N");
        imported.Revision = 1;
        imported.WorkflowStatus = WorkflowStatus.DRAFT;
        imported.DeletedUtc = null;
        return _repository.SavePost(imported, createRevision: true);
    }

    public void ExportLibraryArchive(string destinationZipPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationZipPath);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(destinationZipPath))!);

        IReadOnlyList<PostDocument> posts = _repository.SearchPosts(new PostSearchFilter { IncludeDeleted = true });
        var hashes = new Dictionary<string, string>(StringComparer.Ordinal);

        using FileStream fs = File.Create(destinationZipPath);
        using var archive = new ZipArchive(fs, ZipArchiveMode.Create, leaveOpen: false);

        byte[] postsBytes = JsonSerializer.SerializeToUtf8Bytes(posts, JsonOptions);
        hashes[PostsEntry] = Convert.ToHexString(SHA256.HashData(postsBytes)).ToLowerInvariant();
        WriteBytesEntry(archive, PostsEntry, postsBytes);

        var exportedMediaIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (PostMediaAttachment att in posts.SelectMany(p => p.Media))
        {
            if (!exportedMediaIds.Add(att.MediaId))
            {
                continue;
            }

            PostMediaItem? item = _mediaStore.GetById(att.MediaId);
            if (item == null)
            {
                continue;
            }

            string absPath = _mediaStore.ResolveAbsolutePath(item.RelativePath);
            if (!File.Exists(absPath))
            {
                continue;
            }

            string entryName = $"{MediaPrefix}{Path.GetFileName(item.RelativePath)}";
            byte[] mediaBytes = File.ReadAllBytes(absPath);
            hashes[entryName] = Convert.ToHexString(SHA256.HashData(mediaBytes)).ToLowerInvariant();
            WriteBytesEntry(archive, entryName, mediaBytes);
        }

        var manifest = new LibraryArchiveManifest(1, DateTime.UtcNow, posts.Count, hashes);
        WriteBytesEntry(archive, ManifestEntry, JsonSerializer.SerializeToUtf8Bytes(manifest, JsonOptions));
    }

    public int ImportLibraryArchive(string sourceZipPath)
    {
        if (!File.Exists(sourceZipPath))
        {
            throw new FileNotFoundException("Archive not found.", sourceZipPath);
        }

        using FileStream fs = File.OpenRead(sourceZipPath);
        using var archive = new ZipArchive(fs, ZipArchiveMode.Read, leaveOpen: false);

        ZipArchiveEntry manifestEntry = archive.GetEntry(ManifestEntry)
            ?? throw new InvalidDataException("Archive manifest is missing.");
        LibraryArchiveManifest manifest = JsonSerializer.Deserialize<LibraryArchiveManifest>(ReadEntryBytes(manifestEntry), JsonOptions)
            ?? throw new InvalidDataException("Archive manifest is invalid.");

        foreach (ZipArchiveEntry entry in archive.Entries)
        {
            if (!IsSafeArchivePath(entry.FullName))
            {
                throw new InvalidDataException($"Unsafe archive entry path detected: {entry.FullName}");
            }

            if (entry.FullName != ManifestEntry && manifest.FileSha256.TryGetValue(entry.FullName, out string? expectedHash))
            {
                byte[] bytes = ReadEntryBytes(entry);
                string actualHash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
                if (!string.Equals(expectedHash, actualHash, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException($"SHA-256 mismatch for archive entry '{entry.FullName}'.");
                }
            }
        }

        ZipArchiveEntry postsEntry = archive.GetEntry(PostsEntry)
            ?? throw new InvalidDataException("Posts entry is missing from archive.");
        List<PostDocument> posts = JsonSerializer.Deserialize<List<PostDocument>>(ReadEntryBytes(postsEntry), JsonOptions)
            ?? [];

        foreach (PostDocument post in posts)
        {
            _repository.SavePost(post, createRevision: false);
        }

        return posts.Count;
    }

    private static bool IsSafeArchivePath(string entryName)
    {
        if (string.IsNullOrWhiteSpace(entryName) || entryName.Contains("..", StringComparison.Ordinal))
        {
            return false;
        }
        return !Path.IsPathRooted(entryName) && !entryName.StartsWith('/') && !entryName.StartsWith('\\');
    }

    private static void WriteBytesEntry(ZipArchive archive, string entryName, byte[] data)
    {
        ZipArchiveEntry entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
        using Stream stream = entry.Open();
        stream.Write(data, 0, data.Length);
    }

    private static byte[] ReadEntryBytes(ZipArchiveEntry entry)
    {
        using Stream stream = entry.Open();
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return ms.ToArray();
    }
}
