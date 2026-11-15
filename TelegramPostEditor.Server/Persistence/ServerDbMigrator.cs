using Microsoft.Data.Sqlite;

namespace TelegramPostEditor.Server.Persistence;

public sealed class ServerDbMigrator
{
    public string DatabasePath { get; }

    public ServerDbMigrator(string databasePath)
    {
        DatabasePath = databasePath;
    }

    public SqliteConnection OpenConnection()
    {
        string? dir = Path.GetDirectoryName(DatabasePath);
        if (!string.IsNullOrWhiteSpace(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = DatabasePath,
            Mode = SqliteOpenMode.ReadWriteCreate
        };

        var conn = new SqliteConnection(builder.ConnectionString);
        conn.Open();
        using SqliteCommand cmd = conn.CreateCommand();
        cmd.CommandText = """
            PRAGMA foreign_keys = ON;
            PRAGMA journal_mode = WAL;
            PRAGMA synchronous = NORMAL;
            PRAGMA busy_timeout = 5000;
            """;
        cmd.ExecuteNonQuery();
        return conn;
    }

    public void EnsureMigrated()
    {
        using SqliteConnection conn = OpenConnection();
        using SqliteTransaction tx = conn.BeginTransaction();

        using (SqliteCommand cmd = conn.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = """
                CREATE TABLE IF NOT EXISTS migration_history (
                    version INTEGER PRIMARY KEY,
                    applied_utc TEXT NOT NULL
                );
                """;
            cmd.ExecuteNonQuery();
        }

        using (SqliteCommand verCmd = conn.CreateCommand())
        {
            verCmd.Transaction = tx;
            verCmd.CommandText = "SELECT COALESCE(MAX(version), 0) FROM migration_history;";
            int current = Convert.ToInt32(verCmd.ExecuteScalar());
            if (current < 1)
            {
                ApplyV1(conn, tx);
                using SqliteCommand rec = conn.CreateCommand();
                rec.Transaction = tx;
                rec.CommandText = "INSERT INTO migration_history (version, applied_utc) VALUES (1, $utc);";
                rec.Parameters.AddWithValue("$utc", DateTime.UtcNow.ToString("O"));
                rec.ExecuteNonQuery();
            }
        }

        tx.Commit();
    }

    private static void ApplyV1(SqliteConnection conn, SqliteTransaction tx)
    {
        using SqliteCommand cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS pairing_codes (
                code_hash TEXT PRIMARY KEY,
                expires_utc TEXT NOT NULL,
                used_utc TEXT NULL
            );

            CREATE TABLE IF NOT EXISTS devices (
                id TEXT PRIMARY KEY,
                name TEXT NOT NULL,
                created_utc TEXT NOT NULL,
                revoked_utc TEXT NULL
            );

            CREATE TABLE IF NOT EXISTS auth_tokens (
                token_hash TEXT PRIMARY KEY,
                device_id TEXT NOT NULL,
                created_utc TEXT NOT NULL,
                expires_utc TEXT NULL,
                revoked_utc TEXT NULL,
                FOREIGN KEY (device_id) REFERENCES devices(id) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS bots (
                id TEXT PRIMARY KEY,
                device_id TEXT NOT NULL,
                display_name TEXT NOT NULL,
                encrypted_token TEXT NOT NULL,
                updated_utc TEXT NOT NULL,
                FOREIGN KEY (device_id) REFERENCES devices(id) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS channels (
                id TEXT PRIMARY KEY,
                device_id TEXT NOT NULL,
                bot_id TEXT NOT NULL,
                telegram_chat_id INTEGER NOT NULL,
                display_name TEXT NOT NULL,
                username TEXT NULL,
                timezone TEXT NOT NULL,
                updated_utc TEXT NOT NULL,
                FOREIGN KEY (device_id) REFERENCES devices(id) ON DELETE CASCADE,
                FOREIGN KEY (bot_id) REFERENCES bots(id) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS uploaded_media (
                id TEXT PRIMARY KEY,
                device_id TEXT NOT NULL,
                sha256 TEXT NOT NULL,
                file_name TEXT NOT NULL,
                mime_type TEXT NOT NULL,
                kind INTEGER NOT NULL,
                size_bytes INTEGER NOT NULL,
                relative_path TEXT NOT NULL,
                created_utc TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS publication_jobs (
                id TEXT PRIMARY KEY,
                device_id TEXT NOT NULL,
                client_post_id TEXT NOT NULL,
                revision INTEGER NOT NULL,
                idempotency_key TEXT NOT NULL,
                created_utc TEXT NOT NULL,
                UNIQUE (device_id, idempotency_key)
            );

            CREATE TABLE IF NOT EXISTS publication_targets (
                id TEXT PRIMARY KEY,
                job_id TEXT NOT NULL,
                device_id TEXT NOT NULL,
                channel_id TEXT NOT NULL,
                revision INTEGER NOT NULL,
                scheduled_at_utc TEXT NOT NULL,
                immutable_content_snapshot TEXT NOT NULL,
                media_hashes_json TEXT NOT NULL DEFAULT '[]',
                options_json TEXT NOT NULL DEFAULT '{}',
                status TEXT NOT NULL,
                lease_owner TEXT NULL,
                lease_until_utc TEXT NULL,
                attempt_count INTEGER NOT NULL DEFAULT 0,
                next_attempt_utc TEXT NULL,
                telegram_chat_id INTEGER NOT NULL,
                telegram_message_ids_json TEXT NOT NULL DEFAULT '[]',
                last_error_code TEXT NULL,
                last_error_details_sanitized TEXT NULL,
                updated_utc TEXT NOT NULL,
                FOREIGN KEY (job_id) REFERENCES publication_jobs(id) ON DELETE CASCADE,
                FOREIGN KEY (channel_id) REFERENCES channels(id) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS send_attempts (
                id TEXT PRIMARY KEY,
                target_id TEXT NOT NULL,
                started_utc TEXT NOT NULL,
                finished_utc TEXT NOT NULL,
                success INTEGER NOT NULL,
                error_code TEXT NULL,
                details_sanitized TEXT NULL,
                FOREIGN KEY (target_id) REFERENCES publication_targets(id) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS publication_events (
                seq INTEGER PRIMARY KEY AUTOINCREMENT,
                target_id TEXT NOT NULL,
                device_id TEXT NOT NULL,
                event_utc TEXT NOT NULL,
                event_type TEXT NOT NULL,
                details_sanitized TEXT NOT NULL
            );

            CREATE INDEX IF NOT EXISTS ix_server_targets_due ON publication_targets(status, scheduled_at_utc);
            CREATE INDEX IF NOT EXISTS ix_server_events_seq ON publication_events(device_id, seq);
            """;
        cmd.ExecuteNonQuery();
    }
}
