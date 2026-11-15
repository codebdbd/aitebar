using System;
using System.IO;
using Microsoft.Data.Sqlite;

namespace AiteBar.TelegramPostEditor.Persistence;

public sealed class PostDbMigrator
{
    public const int CurrentSchemaVersion = 1;

    private readonly string _databasePath;

    public PostDbMigrator(string? databasePath = null)
    {
        _databasePath = string.IsNullOrWhiteSpace(databasePath)
            ? GetDefaultDatabasePath()
            : databasePath;
    }

    public string DatabasePath => _databasePath;

    public static string GetDefaultRootFolder() =>
        Path.Combine(PathHelper.AppDataFolder, "TelegramPostEditor");

    public static string GetDefaultDatabasePath() =>
        Path.Combine(GetDefaultRootFolder(), "posts.db");

    public SqliteConnection OpenConnection()
    {
        string? directory = Path.GetDirectoryName(_databasePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = _databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Default
        };

        var connection = new SqliteConnection(builder.ConnectionString);
        connection.Open();

        using SqliteCommand pragmaCmd = connection.CreateCommand();
        pragmaCmd.CommandText = """
            PRAGMA foreign_keys = ON;
            PRAGMA journal_mode = WAL;
            PRAGMA synchronous = NORMAL;
            PRAGMA busy_timeout = 5000;
            """;
        pragmaCmd.ExecuteNonQuery();

        return connection;
    }

    public void EnsureMigrated()
    {
        using SqliteConnection connection = OpenConnection();
        using SqliteTransaction transaction = connection.BeginTransaction();

        using (SqliteCommand cmd = connection.CreateCommand())
        {
            cmd.Transaction = transaction;
            cmd.CommandText = """
                CREATE TABLE IF NOT EXISTS schema_migrations (
                    version INTEGER PRIMARY KEY,
                    applied_utc TEXT NOT NULL
                );
                """;
            cmd.ExecuteNonQuery();
        }

        int currentVersion = GetCurrentVersion(connection, transaction);
        if (currentVersion < 1)
        {
            ApplyV1(connection, transaction);
            RecordMigration(connection, transaction, 1);
        }

        transaction.Commit();
    }

    private static int GetCurrentVersion(SqliteConnection connection, SqliteTransaction transaction)
    {
        using SqliteCommand cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = "SELECT COALESCE(MAX(version), 0) FROM schema_migrations;";
        object? result = cmd.ExecuteScalar();
        return result is null or DBNull ? 0 : Convert.ToInt32(result);
    }

    private static void RecordMigration(SqliteConnection connection, SqliteTransaction transaction, int version)
    {
        using SqliteCommand cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = "INSERT OR REPLACE INTO schema_migrations (version, applied_utc) VALUES ($version, $appliedUtc);";
        cmd.Parameters.AddWithValue("$version", version);
        cmd.Parameters.AddWithValue("$appliedUtc", DateTime.UtcNow.ToString("O"));
        cmd.ExecuteNonQuery();
    }

    private static void ApplyV1(SqliteConnection connection, SqliteTransaction transaction)
    {
        using SqliteCommand cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS posts (
                id TEXT PRIMARY KEY,
                title TEXT NOT NULL,
                document_json TEXT NOT NULL,
                plain_text TEXT NOT NULL DEFAULT '',
                tags_json TEXT NOT NULL DEFAULT '[]',
                current_revision INTEGER NOT NULL DEFAULT 1,
                folder_id TEXT NULL,
                is_favorite INTEGER NOT NULL DEFAULT 0,
                status INTEGER NOT NULL DEFAULT 0,
                created_utc TEXT NOT NULL,
                updated_utc TEXT NOT NULL,
                deleted_utc TEXT NULL
            );

            CREATE TABLE IF NOT EXISTS post_revisions (
                id TEXT PRIMARY KEY,
                post_id TEXT NOT NULL,
                revision INTEGER NOT NULL,
                snapshot_json TEXT NOT NULL,
                created_utc TEXT NOT NULL,
                FOREIGN KEY (post_id) REFERENCES posts(id) ON DELETE CASCADE,
                UNIQUE (post_id, revision)
            );

            CREATE TABLE IF NOT EXISTS media (
                id TEXT PRIMARY KEY,
                sha256 TEXT NOT NULL,
                relative_path TEXT NOT NULL,
                file_name TEXT NOT NULL,
                mime TEXT NOT NULL,
                kind INTEGER NOT NULL DEFAULT 3,
                size INTEGER NOT NULL,
                width INTEGER NULL,
                height INTEGER NULL,
                created_utc TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS post_media (
                post_id TEXT NOT NULL,
                media_id TEXT NOT NULL,
                sort_order INTEGER NOT NULL DEFAULT 0,
                caption TEXT NULL,
                PRIMARY KEY (post_id, media_id),
                FOREIGN KEY (post_id) REFERENCES posts(id) ON DELETE CASCADE,
                FOREIGN KEY (media_id) REFERENCES media(id) ON DELETE RESTRICT
            );

            CREATE TABLE IF NOT EXISTS bots (
                id TEXT PRIMARY KEY,
                display_name TEXT NOT NULL,
                token_secret_ref TEXT NOT NULL,
                telegram_bot_id INTEGER NULL,
                username TEXT NULL,
                state TEXT NOT NULL DEFAULT 'Active',
                created_utc TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS channels (
                id TEXT PRIMARY KEY,
                telegram_chat_id INTEGER NOT NULL,
                username TEXT NULL,
                display_name TEXT NOT NULL,
                bot_id TEXT NOT NULL,
                default_instruction_id TEXT NULL,
                timezone TEXT NOT NULL,
                is_test_channel INTEGER NOT NULL DEFAULT 0,
                default_disable_notification INTEGER NOT NULL DEFAULT 0,
                default_disable_web_preview INTEGER NOT NULL DEFAULT 0,
                FOREIGN KEY (bot_id) REFERENCES bots(id) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS instructions (
                id TEXT PRIMARY KEY,
                title TEXT NOT NULL,
                category TEXT NOT NULL DEFAULT 'General',
                current_version INTEGER NOT NULL DEFAULT 1,
                preferred_connection_id TEXT NULL,
                preferred_model_id TEXT NULL,
                body TEXT NOT NULL,
                created_utc TEXT NOT NULL,
                updated_utc TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS instruction_versions (
                id TEXT PRIMARY KEY,
                instruction_id TEXT NOT NULL,
                version INTEGER NOT NULL,
                body TEXT NOT NULL,
                created_utc TEXT NOT NULL,
                FOREIGN KEY (instruction_id) REFERENCES instructions(id) ON DELETE CASCADE,
                UNIQUE (instruction_id, version)
            );

            CREATE TABLE IF NOT EXISTS channel_variants (
                id TEXT PRIMARY KEY,
                post_id TEXT NOT NULL,
                channel_id TEXT NOT NULL,
                revision INTEGER NOT NULL,
                variant_json TEXT NOT NULL,
                FOREIGN KEY (post_id) REFERENCES posts(id) ON DELETE CASCADE,
                FOREIGN KEY (channel_id) REFERENCES channels(id) ON DELETE CASCADE,
                UNIQUE (post_id, channel_id, revision)
            );

            CREATE TABLE IF NOT EXISTS publication_targets (
                id TEXT PRIMARY KEY,
                post_id TEXT NOT NULL,
                post_revision INTEGER NOT NULL,
                channel_id TEXT NOT NULL,
                scheduled_utc TEXT NOT NULL,
                status INTEGER NOT NULL DEFAULT 0,
                server_id TEXT NULL,
                last_sync_utc TEXT NULL,
                telegram_ids_json TEXT NOT NULL DEFAULT '[]',
                last_error_code TEXT NULL,
                last_error_details TEXT NULL,
                FOREIGN KEY (post_id) REFERENCES posts(id) ON DELETE CASCADE,
                FOREIGN KEY (channel_id) REFERENCES channels(id) ON DELETE CASCADE
            );

            CREATE UNIQUE INDEX IF NOT EXISTS ux_publication_targets_server_id
                ON publication_targets(server_id) WHERE server_id IS NOT NULL;

            CREATE TABLE IF NOT EXISTS publication_events (
                id TEXT PRIMARY KEY,
                target_id TEXT NOT NULL,
                event_utc TEXT NOT NULL,
                event_type TEXT NOT NULL,
                details_sanitized TEXT NOT NULL,
                FOREIGN KEY (target_id) REFERENCES publication_targets(id) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS templates (
                id TEXT PRIMARY KEY,
                title TEXT NOT NULL,
                category TEXT NOT NULL DEFAULT 'General',
                is_built_in INTEGER NOT NULL DEFAULT 0,
                document_json TEXT NOT NULL,
                created_utc TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS app_module_settings (
                key TEXT PRIMARY KEY,
                value_json TEXT NOT NULL
            );

            CREATE INDEX IF NOT EXISTS ix_posts_updated_utc ON posts(updated_utc DESC);
            CREATE INDEX IF NOT EXISTS ix_posts_status ON posts(status);
            CREATE INDEX IF NOT EXISTS ix_posts_folder_id ON posts(folder_id);
            CREATE INDEX IF NOT EXISTS ix_publication_targets_scheduled_utc ON publication_targets(scheduled_utc);
            CREATE INDEX IF NOT EXISTS ix_publication_targets_post_id ON publication_targets(post_id);
            CREATE INDEX IF NOT EXISTS ix_publication_targets_channel_id ON publication_targets(channel_id);
            CREATE INDEX IF NOT EXISTS ix_media_sha256 ON media(sha256);
            """;
        cmd.ExecuteNonQuery();
    }
}
