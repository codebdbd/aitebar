using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using TelegramPostEditor.Server.Persistence;
using TelegramPostEditor.Server.Security;
using TelegramPostEditor.Server.Services;
using TelegramPostEditor.Server.Workers;

namespace TelegramPostEditor.Server.Tests;

public sealed class ServerWorkerAndSecurityTests : IDisposable
{
    private readonly string _tempDir;
    private readonly ServerDbMigrator _migrator;
    private readonly SecretProtector _protector;

    public ServerWorkerAndSecurityTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"aitebar_server_tests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _migrator = new ServerDbMigrator(Path.Combine(_tempDir, "server.db"));
        _migrator.EnsureMigrated();
        _protector = new SecretProtector(RandomNumberGenerator.GetBytes(32));
    }

    [Fact]
    public void SecretProtector_EncryptsAndDecryptsWithAad()
    {
        string cipher = _protector.Encrypt("123456:SECRET_BOT_TOKEN", "bot-1");
        Assert.DoesNotContain("SECRET_BOT_TOKEN", cipher);

        string plain = _protector.Decrypt(cipher, "bot-1");
        Assert.Equal("123456:SECRET_BOT_TOKEN", plain);

        Assert.ThrowsAny<CryptographicException>(() => _protector.Decrypt(cipher, "wrong-bot"));
    }

    [Fact]
    public async Task PublicationWorker_Handles429RetryAfter_AndNetworkDropUnknownResult()
    {
        SeedDeviceBotChannelAndTarget("t-429", "QUEUED_SERVER", DateTime.UtcNow.AddMinutes(-1));

        var handler429 = new StubHttpHandler(_ => new HttpResponseMessage((HttpStatusCode)429)
        {
            Content = new StringContent("""{"ok":false,"error_code":429,"description":"Too Many Requests","parameters":{"retry_after":42}}""", Encoding.UTF8, "application/json")
        });

        var worker429 = new PublicationWorker(
            _migrator,
            _protector,
            new TelegramDispatcher(new HttpClient(handler429)),
            _tempDir,
            NullLogger<PublicationWorker>.Instance);

        DateTime before = DateTime.UtcNow;
        bool processed = await worker429.ProcessOneDueTargetAsync(CancellationToken.None);
        Assert.True(processed);

        (string status, string? nextAttempt) = ReadTargetStatus("t-429");
        Assert.Equal("RETRY_WAIT", status);
        DateTime nextUtc = DateTime.Parse(nextAttempt!, null, System.Globalization.DateTimeStyles.RoundtripKind);
        Assert.True(nextUtc >= before.AddSeconds(40));

        // Now test network drop -> UNKNOWN_RESULT
        SeedDeviceBotChannelAndTarget("t-drop", "QUEUED_SERVER", DateTime.UtcNow.AddMinutes(-1), seedDevice: false);
        var dropHandler = new StubHttpHandler(_ => throw new HttpRequestException("Socket reset"));
        var workerDrop = new PublicationWorker(
            _migrator,
            _protector,
            new TelegramDispatcher(new HttpClient(dropHandler)),
            _tempDir,
            NullLogger<PublicationWorker>.Instance);

        Assert.True(await workerDrop.ProcessOneDueTargetAsync(CancellationToken.None));
        (string dropStatus, _) = ReadTargetStatus("t-drop");
        Assert.Equal("UNKNOWN_RESULT", dropStatus);

        // Verify expired SENDING lease recovery marks UNKNOWN_RESULT instead of blind retry
        SeedDeviceBotChannelAndTarget("t-stuck", "SENDING", DateTime.UtcNow.AddMinutes(-10), seedDevice: false, expiredLease: true);
        workerDrop.RecoverExpiredLeases();
        (string stuckStatus, _) = ReadTargetStatus("t-stuck");
        Assert.Equal("UNKNOWN_RESULT", stuckStatus);
    }

    private void SeedDeviceBotChannelAndTarget(string targetId, string status, DateTime scheduledUtc, bool seedDevice = true, bool expiredLease = false)
    {
        using SqliteConnection conn = _migrator.OpenConnection();
        using SqliteCommand cmd = conn.CreateCommand();
        string encToken = _protector.Encrypt("999:TEST", "bot-1");
        string now = DateTime.UtcNow.ToString("O");
        string snap = JsonSerializer.Serialize(new ServerPostContentDto("telegram-document-v1", "Scheduled post"));

        if (seedDevice)
        {
            cmd.CommandText = """
                INSERT INTO devices (id, name, created_utc) VALUES ('dev-1', 'TestPC', $now);
                INSERT INTO bots (id, device_id, display_name, encrypted_token, updated_utc) VALUES ('bot-1', 'dev-1', 'Bot', $enc, $now);
                INSERT INTO channels (id, device_id, bot_id, telegram_chat_id, display_name, timezone, updated_utc) VALUES ('ch-1', 'dev-1', 'bot-1', -100123, 'Chan', 'UTC', $now);
                INSERT INTO publication_jobs (id, device_id, client_post_id, revision, idempotency_key, created_utc) VALUES ('job-1', 'dev-1', 'post-1', 1, 'idem-1', $now);
                """;
            cmd.Parameters.AddWithValue("$now", now);
            cmd.Parameters.AddWithValue("$enc", encToken);
            cmd.ExecuteNonQuery();
            cmd.Parameters.Clear();
        }

        cmd.CommandText = """
            INSERT INTO publication_targets (id, job_id, device_id, channel_id, revision, scheduled_at_utc, immutable_content_snapshot, status, lease_until_utc, telegram_chat_id, updated_utc)
            VALUES ($id, 'job-1', 'dev-1', 'ch-1', 1, $sched, $snap, $status, $lease, -100123, $now);
            """;
        cmd.Parameters.AddWithValue("$id", targetId);
        cmd.Parameters.AddWithValue("$sched", scheduledUtc.ToString("O"));
        cmd.Parameters.AddWithValue("$snap", snap);
        cmd.Parameters.AddWithValue("$status", status);
        cmd.Parameters.AddWithValue("$lease", expiredLease ? DateTime.UtcNow.AddMinutes(-5).ToString("O") : DBNull.Value);
        cmd.Parameters.AddWithValue("$now", now);
        cmd.ExecuteNonQuery();
    }

    private (string Status, string? NextAttempt) ReadTargetStatus(string targetId)
    {
        using SqliteConnection conn = _migrator.OpenConnection();
        using SqliteCommand cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT status, next_attempt_utc FROM publication_targets WHERE id = $id;";
        cmd.Parameters.AddWithValue("$id", targetId);
        using SqliteDataReader r = cmd.ExecuteReader();
        Assert.True(r.Read());
        return (r.GetString(0), r.IsDBNull(1) ? null : r.GetString(1));
    }

    public void Dispose()
    {
        try
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, recursive: true);
            }
        }
        catch
        {
        }
    }

    private sealed class StubHttpHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(responder(request));
    }
}
