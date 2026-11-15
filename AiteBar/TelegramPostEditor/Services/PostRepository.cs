using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using AiteBar.TelegramPostEditor.Models;
using AiteBar.TelegramPostEditor.Persistence;
using Microsoft.Data.Sqlite;

namespace AiteBar.TelegramPostEditor.Services;

public sealed class PostRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private static readonly JsonSerializerOptions PrettyJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly PostDbMigrator _migrator;

    public PostRepository(PostDbMigrator migrator)
    {
        _migrator = migrator ?? throw new ArgumentNullException(nameof(migrator));
        _migrator.EnsureMigrated();
    }

    public PostDocument SavePost(PostDocument post, bool createRevision = true)
    {
        ArgumentNullException.ThrowIfNull(post);
        if (string.IsNullOrWhiteSpace(post.Id))
        {
            post.Id = Guid.NewGuid().ToString("N");
        }

        DateTime now = DateTime.UtcNow;
        if (post.CreatedUtc == default)
        {
            post.CreatedUtc = now;
        }
        post.UpdatedUtc = now;

        using SqliteConnection connection = _migrator.OpenConnection();
        using SqliteTransaction transaction = connection.BeginTransaction();

        int existingRevision = GetCurrentRevision(connection, transaction, post.Id);
        if (existingRevision > 0 && createRevision)
        {
            post.Revision = existingRevision + 1;
        }
        else if (post.Revision < 1)
        {
            post.Revision = 1;
        }

        string documentJson = JsonSerializer.Serialize(post, JsonOptions);
        string plainText = TelegramDocumentSerializer.ExtractPlainText(post);
        string tagsJson = JsonSerializer.Serialize(post.Tags, JsonOptions);

        using (SqliteCommand cmd = connection.CreateCommand())
        {
            cmd.Transaction = transaction;
            cmd.CommandText = """
                INSERT INTO posts (id, title, document_json, plain_text, tags_json, current_revision, folder_id, is_favorite, status, created_utc, updated_utc, deleted_utc)
                VALUES ($id, $title, $docJson, $plainText, $tagsJson, $revision, $folderId, $isFav, $status, $created, $updated, $deleted)
                ON CONFLICT(id) DO UPDATE SET
                    title = excluded.title,
                    document_json = excluded.document_json,
                    plain_text = excluded.plain_text,
                    tags_json = excluded.tags_json,
                    current_revision = excluded.current_revision,
                    folder_id = excluded.folder_id,
                    is_favorite = excluded.is_favorite,
                    status = excluded.status,
                    updated_utc = excluded.updated_utc,
                    deleted_utc = excluded.deleted_utc;
                """;
            cmd.Parameters.AddWithValue("$id", post.Id);
            cmd.Parameters.AddWithValue("$title", post.Title ?? string.Empty);
            cmd.Parameters.AddWithValue("$docJson", documentJson);
            cmd.Parameters.AddWithValue("$plainText", plainText);
            cmd.Parameters.AddWithValue("$tagsJson", tagsJson);
            cmd.Parameters.AddWithValue("$revision", post.Revision);
            cmd.Parameters.AddWithValue("$folderId", (object?)post.FolderId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$isFav", post.IsFavorite ? 1 : 0);
            cmd.Parameters.AddWithValue("$status", (int)post.WorkflowStatus);
            cmd.Parameters.AddWithValue("$created", post.CreatedUtc.ToString("O"));
            cmd.Parameters.AddWithValue("$updated", post.UpdatedUtc.ToString("O"));
            cmd.Parameters.AddWithValue("$deleted", post.DeletedUtc.HasValue ? post.DeletedUtc.Value.ToString("O") : DBNull.Value);
            cmd.ExecuteNonQuery();
        }

        SyncPostMediaLinks(connection, transaction, post);

        if (createRevision || existingRevision == 0)
        {
            InsertRevision(connection, transaction, post.Id, post.Revision, documentJson, now);
        }

        transaction.Commit();
        return post;
    }

    public PostDocument? GetPost(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        using SqliteConnection connection = _migrator.OpenConnection();
        using SqliteCommand cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT document_json FROM posts WHERE id = $id;";
        cmd.Parameters.AddWithValue("$id", id);

        if (cmd.ExecuteScalar() is string json)
        {
            return JsonSerializer.Deserialize<PostDocument>(json, JsonOptions);
        }

        return null;
    }

    public IReadOnlyList<PostDocument> SearchPosts(PostSearchFilter? filter = null)
    {
        filter ??= new PostSearchFilter();
        using SqliteConnection connection = _migrator.OpenConnection();
        using SqliteCommand cmd = connection.CreateCommand();

        var where = new List<string>();
        if (!filter.IncludeDeleted)
        {
            where.Add("p.deleted_utc IS NULL");
        }
        if (!string.IsNullOrWhiteSpace(filter.FolderId))
        {
            where.Add("p.folder_id = $folderId");
            cmd.Parameters.AddWithValue("$folderId", filter.FolderId);
        }
        if (filter.Status.HasValue)
        {
            where.Add("p.status = $status");
            cmd.Parameters.AddWithValue("$status", (int)filter.Status.Value);
        }
        if (filter.FavoritesOnly)
        {
            where.Add("p.is_favorite = 1");
        }
        if (filter.FromUtc.HasValue)
        {
            where.Add("p.updated_utc >= $fromUtc");
            cmd.Parameters.AddWithValue("$fromUtc", filter.FromUtc.Value.ToString("O"));
        }
        if (filter.ToUtc.HasValue)
        {
            where.Add("p.updated_utc <= $toUtc");
            cmd.Parameters.AddWithValue("$toUtc", filter.ToUtc.Value.ToString("O"));
        }
        if (!string.IsNullOrWhiteSpace(filter.SearchText))
        {
            where.Add("(p.title LIKE $search ESCAPE '\\' OR p.plain_text LIKE $search ESCAPE '\\')");
            string escaped = EscapeLike(filter.SearchText.Trim());
            cmd.Parameters.AddWithValue("$search", $"%{escaped}%");
        }
        if (!string.IsNullOrWhiteSpace(filter.Tag))
        {
            where.Add("p.tags_json LIKE $tagSearch ESCAPE '\\'");
            string escapedTag = EscapeLike($"\"{filter.Tag.Trim()}\"");
            cmd.Parameters.AddWithValue("$tagSearch", $"%{escapedTag}%");
        }
        if (!string.IsNullOrWhiteSpace(filter.ChannelId))
        {
            where.Add("EXISTS (SELECT 1 FROM publication_targets pt WHERE pt.post_id = p.id AND pt.channel_id = $channelId)");
            cmd.Parameters.AddWithValue("$channelId", filter.ChannelId);
        }

        string whereClause = where.Count > 0 ? $"WHERE {string.Join(" AND ", where)}" : string.Empty;
        cmd.CommandText = $"""
            SELECT p.document_json
            FROM posts p
            {whereClause}
            ORDER BY p.is_favorite DESC, p.updated_utc DESC;
            """;

        var results = new List<PostDocument>();
        using SqliteDataReader reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            string json = reader.GetString(0);
            PostDocument? doc = JsonSerializer.Deserialize<PostDocument>(json, JsonOptions);
            if (doc != null)
            {
                results.Add(doc);
            }
        }

        return results;
    }

    public bool SoftDeletePost(string id)
    {
        PostDocument? doc = GetPost(id);
        if (doc == null)
        {
            return false;
        }

        doc.DeletedUtc = DateTime.UtcNow;
        SavePost(doc, createRevision: false);
        return true;
    }

    public bool RestorePost(string id)
    {
        PostDocument? doc = GetPost(id);
        if (doc == null)
        {
            return false;
        }

        doc.DeletedUtc = null;
        SavePost(doc, createRevision: false);
        return true;
    }

    public bool PermanentlyDeletePost(string id)
    {
        using SqliteConnection connection = _migrator.OpenConnection();
        using SqliteCommand cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM posts WHERE id = $id;";
        cmd.Parameters.AddWithValue("$id", id);
        return cmd.ExecuteNonQuery() > 0;
    }

    public PostDocument DuplicatePost(string id, string? newTitle = null)
    {
        PostDocument source = GetPost(id) ?? throw new KeyNotFoundException($"Post '{id}' was not found.");
        string json = JsonSerializer.Serialize(source, JsonOptions);
        PostDocument copy = JsonSerializer.Deserialize<PostDocument>(json, JsonOptions)!;

        copy.Id = Guid.NewGuid().ToString("N");
        copy.Title = string.IsNullOrWhiteSpace(newTitle) ? $"{source.Title} (Copy)" : newTitle.Trim();
        copy.CreatedUtc = DateTime.UtcNow;
        copy.UpdatedUtc = DateTime.UtcNow;
        copy.DeletedUtc = null;
        copy.Revision = 1;
        copy.WorkflowStatus = WorkflowStatus.DRAFT;

        return SavePost(copy, createRevision: true);
    }

    public IReadOnlyList<PostRevision> GetRevisions(string postId)
    {
        using SqliteConnection connection = _migrator.OpenConnection();
        using SqliteCommand cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT id, post_id, revision, snapshot_json, created_utc
            FROM post_revisions
            WHERE post_id = $postId
            ORDER BY revision DESC;
            """;
        cmd.Parameters.AddWithValue("$postId", postId);

        var list = new List<PostRevision>();
        using SqliteDataReader reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            list.Add(new PostRevision
            {
                Id = reader.GetString(0),
                PostId = reader.GetString(1),
                Revision = reader.GetInt32(2),
                SnapshotJson = reader.GetString(3),
                CreatedUtc = DateTime.Parse(reader.GetString(4), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)
            });
        }

        return list;
    }

    public PostDocument RestoreRevision(string postId, int revisionNumber)
    {
        using SqliteConnection connection = _migrator.OpenConnection();
        using SqliteCommand cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT snapshot_json
            FROM post_revisions
            WHERE post_id = $postId AND revision = $revision;
            """;
        cmd.Parameters.AddWithValue("$postId", postId);
        cmd.Parameters.AddWithValue("$revision", revisionNumber);

        string? json = cmd.ExecuteScalar() as string;
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new KeyNotFoundException($"Revision {revisionNumber} for post '{postId}' was not found.");
        }

        PostDocument restored = JsonSerializer.Deserialize<PostDocument>(json, JsonOptions)!;
        restored.Id = postId;
        return SavePost(restored, createRevision: true);
    }

    public void EnsureBuiltInTemplates()
    {
        if (GetTemplates().Any(t => t.IsBuiltIn))
        {
            return;
        }

        var builtIns = new[]
        {
            CreateTemplate("Новости", "News", "News", "Краткий заголовок новости", "Ключевой факт и контекст события.\n\nИсточник: "),
            CreateTemplate("Обзор", "Review", "Review", "Обзор: ", "1. Что это и зачем\n2. Плюсы и минусы\n3. Итог"),
            CreateTemplate("Анонс", "Announcement", "Announcement", "Анонс: ", "Дата и время: \nЧто будет: \nКак присоединиться: "),
            CreateTemplate("Инструкция", "Guide", "Guide", "Пошаговая инструкция", "Шаг 1. \nШаг 2. \nШаг 3. ")
        };

        foreach (PostTemplate template in builtIns)
        {
            SaveTemplate(template);
        }
    }

    public PostTemplate SaveTemplate(PostTemplate template)
    {
        ArgumentNullException.ThrowIfNull(template);
        if (string.IsNullOrWhiteSpace(template.Id))
        {
            template.Id = Guid.NewGuid().ToString("N");
        }
        if (template.CreatedUtc == default)
        {
            template.CreatedUtc = DateTime.UtcNow;
        }

        string json = JsonSerializer.Serialize(template, JsonOptions);
        using SqliteConnection connection = _migrator.OpenConnection();
        using SqliteCommand cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO templates (id, title, category, is_built_in, document_json, created_utc)
            VALUES ($id, $title, $category, $builtIn, $json, $created)
            ON CONFLICT(id) DO UPDATE SET
                title = excluded.title,
                category = excluded.category,
                is_built_in = excluded.is_built_in,
                document_json = excluded.document_json;
            """;
        cmd.Parameters.AddWithValue("$id", template.Id);
        cmd.Parameters.AddWithValue("$title", template.Title);
        cmd.Parameters.AddWithValue("$category", template.Category);
        cmd.Parameters.AddWithValue("$builtIn", template.IsBuiltIn ? 1 : 0);
        cmd.Parameters.AddWithValue("$json", json);
        cmd.Parameters.AddWithValue("$created", template.CreatedUtc.ToString("O"));
        cmd.ExecuteNonQuery();

        return template;
    }

    public IReadOnlyList<PostTemplate> GetTemplates()
    {
        using SqliteConnection connection = _migrator.OpenConnection();
        using SqliteCommand cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT document_json FROM templates ORDER BY is_built_in DESC, title ASC;";

        var list = new List<PostTemplate>();
        using SqliteDataReader reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            PostTemplate? item = JsonSerializer.Deserialize<PostTemplate>(reader.GetString(0), JsonOptions);
            if (item != null)
            {
                list.Add(item);
            }
        }
        return list;
    }

    public PostDocument CreateDraftFromTemplate(string templateId)
    {
        PostTemplate template = GetTemplates().FirstOrDefault(t => t.Id == templateId)
            ?? throw new KeyNotFoundException($"Template '{templateId}' was not found.");

        string json = JsonSerializer.Serialize(template.Document, JsonOptions);
        PostDocument draft = JsonSerializer.Deserialize<PostDocument>(json, JsonOptions)!;
        draft.Id = Guid.NewGuid().ToString("N");
        draft.Title = template.Title;
        draft.CreatedUtc = DateTime.UtcNow;
        draft.UpdatedUtc = DateTime.UtcNow;
        draft.DeletedUtc = null;
        draft.Revision = 1;
        draft.WorkflowStatus = WorkflowStatus.DRAFT;
        draft.InstructionId ??= template.DefaultInstructionId;

        return SavePost(draft, createRevision: true);
    }

    public string ExportTemplateToJson(PostTemplate template) =>
        JsonSerializer.Serialize(template, PrettyJsonOptions);

    public PostTemplate ImportTemplateFromJson(string json)
    {
        PostTemplate imported = JsonSerializer.Deserialize<PostTemplate>(json, JsonOptions)
            ?? throw new InvalidOperationException("Invalid template JSON.");
        imported.Id = Guid.NewGuid().ToString("N");
        imported.IsBuiltIn = false;
        imported.CreatedUtc = DateTime.UtcNow;
        return SaveTemplate(imported);
    }

    private static PostTemplate CreateTemplate(string title, string category, string taskType, string publishTitle, string bodyText)
    {
        return new PostTemplate
        {
            Id = $"builtin-{category.ToLowerInvariant()}",
            Title = title,
            Category = category,
            TaskType = taskType,
            IsBuiltIn = true,
            Document = new PostDocument
            {
                Title = title,
                PublishTitle = publishTitle,
                IncludePublishTitle = true,
                Blocks =
                [
                    new PostBlock
                    {
                        Type = PostBlockType.Paragraph,
                        Runs = [new PostTextRun { Text = bodyText }]
                    }
                ]
            }
        };
    }

    private static int GetCurrentRevision(SqliteConnection connection, SqliteTransaction transaction, string postId)
    {
        using SqliteCommand cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = "SELECT current_revision FROM posts WHERE id = $id;";
        cmd.Parameters.AddWithValue("$id", postId);
        object? result = cmd.ExecuteScalar();
        return result is null or DBNull ? 0 : Convert.ToInt32(result);
    }

    private static void InsertRevision(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string postId,
        int revision,
        string snapshotJson,
        DateTime createdUtc)
    {
        using SqliteCommand cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = """
            INSERT OR REPLACE INTO post_revisions (id, post_id, revision, snapshot_json, created_utc)
            VALUES ($id, $postId, $revision, $snapshot, $created);
            """;
        cmd.Parameters.AddWithValue("$id", Guid.NewGuid().ToString("N"));
        cmd.Parameters.AddWithValue("$postId", postId);
        cmd.Parameters.AddWithValue("$revision", revision);
        cmd.Parameters.AddWithValue("$snapshot", snapshotJson);
        cmd.Parameters.AddWithValue("$created", createdUtc.ToString("O"));
        cmd.ExecuteNonQuery();
    }

    private static void SyncPostMediaLinks(SqliteConnection connection, SqliteTransaction transaction, PostDocument post)
    {
        using (SqliteCommand deleteCmd = connection.CreateCommand())
        {
            deleteCmd.Transaction = transaction;
            deleteCmd.CommandText = "DELETE FROM post_media WHERE post_id = $postId;";
            deleteCmd.Parameters.AddWithValue("$postId", post.Id);
            deleteCmd.ExecuteNonQuery();
        }

        foreach (PostMediaAttachment attachment in post.Media)
        {
            if (string.IsNullOrWhiteSpace(attachment.MediaId))
            {
                continue;
            }

            using SqliteCommand insertCmd = connection.CreateCommand();
            insertCmd.Transaction = transaction;
            insertCmd.CommandText = """
                INSERT OR IGNORE INTO post_media (post_id, media_id, sort_order, caption)
                SELECT $postId, $mediaId, $sortOrder, $caption
                WHERE EXISTS (SELECT 1 FROM media WHERE id = $mediaId);
                """;
            insertCmd.Parameters.AddWithValue("$postId", post.Id);
            insertCmd.Parameters.AddWithValue("$mediaId", attachment.MediaId);
            insertCmd.Parameters.AddWithValue("$sortOrder", attachment.SortOrder);
            insertCmd.Parameters.AddWithValue("$caption", (object?)attachment.Caption ?? DBNull.Value);
            insertCmd.ExecuteNonQuery();
        }
    }

    private static string EscapeLike(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal)
             .Replace("%", "\\%", StringComparison.Ordinal)
             .Replace("_", "\\_", StringComparison.Ordinal);
}
