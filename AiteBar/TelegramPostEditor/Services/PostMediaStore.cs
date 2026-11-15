using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using AiteBar.TelegramPostEditor.Models;
using AiteBar.TelegramPostEditor.Persistence;
using Microsoft.Data.Sqlite;

namespace AiteBar.TelegramPostEditor.Services;

public sealed class PostMediaStore
{
    public const long MaxPhotoBytes = 10 * 1024 * 1024;
    public const long MaxMediaOrDocumentBytes = 50 * 1024 * 1024;

    private static readonly HashSet<string> PhotoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp"
    };

    private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".mov", ".m4v"
    };

    private static readonly HashSet<string> AnimationExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".gif"
    };

    private readonly PostDbMigrator _migrator;
    private readonly string _mediaRoot;

    public PostMediaStore(PostDbMigrator migrator, string? mediaRoot = null)
    {
        _migrator = migrator ?? throw new ArgumentNullException(nameof(migrator));
        _migrator.EnsureMigrated();
        _mediaRoot = string.IsNullOrWhiteSpace(mediaRoot)
            ? Path.Combine(PostDbMigrator.GetDefaultRootFolder(), "Media")
            : mediaRoot;
    }

    public string MediaRoot => _mediaRoot;

    public static MediaKind ClassifyKind(string filePath)
    {
        string ext = Path.GetExtension(filePath);
        if (PhotoExtensions.Contains(ext)) return MediaKind.Photo;
        if (VideoExtensions.Contains(ext)) return MediaKind.Video;
        if (AnimationExtensions.Contains(ext)) return MediaKind.Animation;
        return MediaKind.Document;
    }

    public static string GetMimeType(string filePath) =>
        Path.GetExtension(filePath).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            ".gif" => "image/gif",
            ".mp4" or ".m4v" => "video/mp4",
            ".mov" => "video/quicktime",
            ".pdf" => "application/pdf",
            ".txt" => "text/plain",
            _ => "application/octet-stream"
        };

    public PostMediaItem ImportFile(string sourceFilePath)
    {
        if (string.IsNullOrWhiteSpace(sourceFilePath) || !File.Exists(sourceFilePath))
        {
            throw new FileNotFoundException("Media file not found.", sourceFilePath);
        }

        var fileInfo = new FileInfo(sourceFilePath);
        MediaKind kind = ClassifyKind(sourceFilePath);
        long maxAllowed = kind == MediaKind.Photo ? MaxPhotoBytes : MaxMediaOrDocumentBytes;
        if (fileInfo.Length == 0 || fileInfo.Length > maxAllowed)
        {
            throw new InvalidOperationException($"File size {fileInfo.Length} bytes exceeds allowed limit ({maxAllowed} bytes) for {kind}.");
        }

        string sha256 = ComputeSha256(sourceFilePath);
        PostMediaItem? existing = FindBySha256(sha256);
        if (existing != null && File.Exists(ResolveAbsolutePath(existing.RelativePath)))
        {
            return existing;
        }

        Directory.CreateDirectory(_mediaRoot);
        string id = Guid.NewGuid().ToString("N");
        string ext = Path.GetExtension(sourceFilePath).ToLowerInvariant();
        string relativeName = $"{id}{ext}";
        string destination = Path.Combine(_mediaRoot, relativeName);
        File.Copy(sourceFilePath, destination, overwrite: true);

        var item = new PostMediaItem
        {
            Id = id,
            Sha256 = sha256,
            RelativePath = relativeName,
            FileName = Path.GetFileName(sourceFilePath),
            MimeType = GetMimeType(sourceFilePath),
            Kind = kind,
            SizeBytes = fileInfo.Length,
            CreatedUtc = DateTime.UtcNow
        };

        using SqliteConnection connection = _migrator.OpenConnection();
        using SqliteCommand cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO media (id, sha256, relative_path, file_name, mime, kind, size, width, height, created_utc)
            VALUES ($id, $sha, $rel, $name, $mime, $kind, $size, $width, $height, $created);
            """;
        cmd.Parameters.AddWithValue("$id", item.Id);
        cmd.Parameters.AddWithValue("$sha", item.Sha256);
        cmd.Parameters.AddWithValue("$rel", item.RelativePath);
        cmd.Parameters.AddWithValue("$name", item.FileName);
        cmd.Parameters.AddWithValue("$mime", item.MimeType);
        cmd.Parameters.AddWithValue("$kind", (int)item.Kind);
        cmd.Parameters.AddWithValue("$size", item.SizeBytes);
        cmd.Parameters.AddWithValue("$width", (object?)item.Width ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$height", (object?)item.Height ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$created", item.CreatedUtc.ToString("O"));
        cmd.ExecuteNonQuery();

        return item;
    }

    public PostMediaItem? GetById(string mediaId)
    {
        using SqliteConnection connection = _migrator.OpenConnection();
        using SqliteCommand cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT id, sha256, relative_path, file_name, mime, kind, size, width, height, created_utc FROM media WHERE id = $id;";
        cmd.Parameters.AddWithValue("$id", mediaId);
        using SqliteDataReader reader = cmd.ExecuteReader();
        return reader.Read() ? ReadMedia(reader) : null;
    }

    public PostMediaItem? FindBySha256(string sha256)
    {
        using SqliteConnection connection = _migrator.OpenConnection();
        using SqliteCommand cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT id, sha256, relative_path, file_name, mime, kind, size, width, height, created_utc FROM media WHERE sha256 = $sha LIMIT 1;";
        cmd.Parameters.AddWithValue("$sha", sha256);
        using SqliteDataReader reader = cmd.ExecuteReader();
        return reader.Read() ? ReadMedia(reader) : null;
    }

    public string ResolveAbsolutePath(string relativePath)
    {
        string fullRoot = Path.GetFullPath(_mediaRoot);
        string fullTarget = Path.GetFullPath(Path.Combine(fullRoot, relativePath));
        if (!fullTarget.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Invalid media relative path.");
        }
        return fullTarget;
    }

    public int CleanupUnusedMedia()
    {
        using SqliteConnection connection = _migrator.OpenConnection();
        using SqliteCommand selectCmd = connection.CreateCommand();
        selectCmd.CommandText = """
            SELECT m.id, m.relative_path
            FROM media m
            WHERE NOT EXISTS (SELECT 1 FROM post_media pm WHERE pm.media_id = m.id)
              AND NOT EXISTS (
                  SELECT 1 FROM posts p
                  WHERE p.document_json LIKE '%' || m.id || '%'
              );
            """;

        var toDelete = new List<(string Id, string RelativePath)>();
        using (SqliteDataReader reader = selectCmd.ExecuteReader())
        {
            while (reader.Read())
            {
                toDelete.Add((reader.GetString(0), reader.GetString(1)));
            }
        }

        int deleted = 0;
        foreach ((string id, string relPath) in toDelete)
        {
            using SqliteCommand delCmd = connection.CreateCommand();
            delCmd.CommandText = "DELETE FROM media WHERE id = $id;";
            delCmd.Parameters.AddWithValue("$id", id);
            if (delCmd.ExecuteNonQuery() > 0)
            {
                deleted++;
                string abs = ResolveAbsolutePath(relPath);
                if (File.Exists(abs))
                {
                    File.Delete(abs);
                }
            }
        }

        return deleted;
    }

    public static string ComputeSha256(string filePath)
    {
        using FileStream stream = File.OpenRead(filePath);
        byte[] hash = SHA256.HashData(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static PostMediaItem ReadMedia(SqliteDataReader reader) => new()
    {
        Id = reader.GetString(0),
        Sha256 = reader.GetString(1),
        RelativePath = reader.GetString(2),
        FileName = reader.GetString(3),
        MimeType = reader.GetString(4),
        Kind = (MediaKind)reader.GetInt32(5),
        SizeBytes = reader.GetInt64(6),
        Width = reader.IsDBNull(7) ? null : reader.GetInt32(7),
        Height = reader.IsDBNull(8) ? null : reader.GetInt32(8),
        CreatedUtc = DateTime.Parse(reader.GetString(9), null, System.Globalization.DateTimeStyles.RoundtripKind)
    };
}
