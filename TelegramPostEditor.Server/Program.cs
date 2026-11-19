using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using TelegramPostEditor.Server.Persistence;
using TelegramPostEditor.Server.Security;
using TelegramPostEditor.Server.Services;
using TelegramPostEditor.Server.Workers;

var builder = WebApplication.CreateBuilder(args);

string dataDir = builder.Configuration["Storage:DataDir"]
    ?? Path.Combine(AppContext.BaseDirectory, "data");
string dbPath = Path.Combine(dataDir, "server.db");
string mediaDir = Path.Combine(dataDir, "media");
Directory.CreateDirectory(dataDir);
Directory.CreateDirectory(mediaDir);

var migrator = new ServerDbMigrator(dbPath);
migrator.EnsureMigrated();

SecretProtector protector = SecretProtector.FromEnvironmentOrFile(
    Environment.GetEnvironmentVariable("TPE_MASTER_KEY") ?? builder.Configuration["Security:MasterKeyBase64"],
    builder.Configuration["Security:MasterKeyFile"]);

if (args.Contains("--create-pairing-code", StringComparer.Ordinal))
{
    string code = Convert.ToHexString(RandomNumberGenerator.GetBytes(6));
    string codeHash = SecretProtector.HashToken(code);
    using SqliteConnection conn = migrator.OpenConnection();
    using SqliteCommand cmd = conn.CreateCommand();
    cmd.CommandText = "INSERT INTO pairing_codes (code_hash, expires_utc) VALUES ($hash, $exp);";
    cmd.Parameters.AddWithValue("$hash", codeHash);
    cmd.Parameters.AddWithValue("$exp", DateTime.UtcNow.AddMinutes(15).ToString("O"));
    cmd.ExecuteNonQuery();
    Console.WriteLine($"PAIRING_CODE={code}");
    Console.WriteLine($"EXPIRES_UTC={DateTime.UtcNow.AddMinutes(15):O}");
    return;
}

builder.Services.AddSingleton(migrator);
builder.Services.AddSingleton(protector);
builder.Services.AddSingleton(new HttpClient { Timeout = TimeSpan.FromSeconds(45) });
builder.Services.AddSingleton(sp => new TelegramDispatcher(
    sp.GetRequiredService<HttpClient>(),
    builder.Configuration["Telegram:BaseApiUrl"] ?? "https://api.telegram.org"));
builder.Services.AddSingleton(sp => new PublicationWorker(
    sp.GetRequiredService<ServerDbMigrator>(),
    sp.GetRequiredService<SecretProtector>(),
    sp.GetRequiredService<TelegramDispatcher>(),
    mediaDir,
    sp.GetRequiredService<ILogger<PublicationWorker>>()));
builder.Services.AddHostedService(sp => sp.GetRequiredService<PublicationWorker>());

var app = builder.Build();

app.MapGet("/api/v1/health", () => Results.Ok(new { status = "ok", utc = DateTime.UtcNow }));

app.MapPost("/api/v1/pair/start", (PairStartRequest req) =>
{
    if (string.IsNullOrWhiteSpace(req.PairingCode))
    {
        return Results.BadRequest(new { error = "Pairing code is required." });
    }

    string hash = SecretProtector.HashToken(req.PairingCode.Trim());
    using SqliteConnection conn = migrator.OpenConnection();
    using SqliteCommand cmd = conn.CreateCommand();
    cmd.CommandText = "SELECT expires_utc, used_utc FROM pairing_codes WHERE code_hash = $hash;";
    cmd.Parameters.AddWithValue("$hash", hash);
    using SqliteDataReader reader = cmd.ExecuteReader();
    if (!reader.Read())
    {
        return Results.Unauthorized();
    }

    DateTime exp = DateTime.Parse(reader.GetString(0), null, System.Globalization.DateTimeStyles.RoundtripKind);
    bool used = !reader.IsDBNull(1);
    if (used || exp < DateTime.UtcNow)
    {
        return Results.Unauthorized();
    }

    return Results.Ok(new { valid = true, expiresUtc = exp });
});

app.MapPost("/api/v1/pair/confirm", (PairConfirmRequest req) =>
{
    string hash = SecretProtector.HashToken(req.PairingCode.Trim());
    using SqliteConnection conn = migrator.OpenConnection();
    using SqliteTransaction tx = conn.BeginTransaction();

    using (SqliteCommand check = conn.CreateCommand())
    {
        check.Transaction = tx;
        check.CommandText = "SELECT expires_utc, used_utc FROM pairing_codes WHERE code_hash = $hash;";
        check.Parameters.AddWithValue("$hash", hash);
        using SqliteDataReader reader = check.ExecuteReader();
        if (!reader.Read())
        {
            return Results.Unauthorized();
        }
        DateTime exp = DateTime.Parse(reader.GetString(0), null, System.Globalization.DateTimeStyles.RoundtripKind);
        if (!reader.IsDBNull(1) || exp < DateTime.UtcNow)
        {
            return Results.Unauthorized();
        }
    }

    using (SqliteCommand markUsed = conn.CreateCommand())
    {
        markUsed.Transaction = tx;
        markUsed.CommandText = "UPDATE pairing_codes SET used_utc = $now WHERE code_hash = $hash;";
        markUsed.Parameters.AddWithValue("$now", DateTime.UtcNow.ToString("O"));
        markUsed.Parameters.AddWithValue("$hash", hash);
        markUsed.ExecuteNonQuery();
    }

    string deviceId = Guid.NewGuid().ToString("N");
    string rawToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
    string tokenHash = SecretProtector.HashToken(rawToken);
    string now = DateTime.UtcNow.ToString("O");

    using (SqliteCommand insDev = conn.CreateCommand())
    {
        insDev.Transaction = tx;
        insDev.CommandText = "INSERT INTO devices (id, name, created_utc) VALUES ($id, $name, $now);";
        insDev.Parameters.AddWithValue("$id", deviceId);
        insDev.Parameters.AddWithValue("$name", string.IsNullOrWhiteSpace(req.DeviceName) ? "AiteBar" : req.DeviceName.Trim());
        insDev.Parameters.AddWithValue("$now", now);
        insDev.ExecuteNonQuery();
    }

    using (SqliteCommand insTok = conn.CreateCommand())
    {
        insTok.Transaction = tx;
        insTok.CommandText = "INSERT INTO auth_tokens (token_hash, device_id, created_utc) VALUES ($hash, $dev, $now);";
        insTok.Parameters.AddWithValue("$hash", tokenHash);
        insTok.Parameters.AddWithValue("$dev", deviceId);
        insTok.Parameters.AddWithValue("$now", now);
        insTok.ExecuteNonQuery();
    }

    tx.Commit();
    return Results.Ok(new PairResponse(deviceId, rawToken));
});

app.MapGet("/api/v1/channels", (HttpContext ctx) =>
{
    if (!TryAuthenticate(ctx, migrator, out string deviceId)) return Results.Unauthorized();

    using SqliteConnection conn = migrator.OpenConnection();
    using SqliteCommand cmd = conn.CreateCommand();
    cmd.CommandText = "SELECT id, bot_id, telegram_chat_id, display_name, username, timezone FROM channels WHERE device_id = $dev;";
    cmd.Parameters.AddWithValue("$dev", deviceId);
    var list = new List<ChannelResponse>();
    using SqliteDataReader reader = cmd.ExecuteReader();
    while (reader.Read())
    {
        list.Add(new ChannelResponse(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetInt64(2),
            reader.GetString(3),
            reader.IsDBNull(4) ? null : reader.GetString(4),
            reader.GetString(5)));
    }
    return Results.Ok(list);
});

app.MapPut("/api/v1/channels/{id}", (string id, ChannelUpsertRequest req, HttpContext ctx) =>
{
    if (!TryAuthenticate(ctx, migrator, out string deviceId)) return Results.Unauthorized();

    string now = DateTime.UtcNow.ToString("O");
    using SqliteConnection conn = migrator.OpenConnection();
    using SqliteTransaction tx = conn.BeginTransaction();

    if (!string.IsNullOrWhiteSpace(req.BotToken))
    {
        string encrypted = protector.Encrypt(req.BotToken.Trim(), req.BotId);
        using SqliteCommand botCmd = conn.CreateCommand();
        botCmd.Transaction = tx;
        botCmd.CommandText = """
            INSERT INTO bots (id, device_id, display_name, encrypted_token, updated_utc)
            VALUES ($id, $dev, $name, $enc, $now)
            ON CONFLICT(id) DO UPDATE SET
                display_name = excluded.display_name,
                encrypted_token = excluded.encrypted_token,
                updated_utc = excluded.updated_utc
            WHERE bots.device_id = excluded.device_id;
            """;
        botCmd.Parameters.AddWithValue("$id", req.BotId);
        botCmd.Parameters.AddWithValue("$dev", deviceId);
        botCmd.Parameters.AddWithValue("$name", req.BotDisplayName);
        botCmd.Parameters.AddWithValue("$enc", encrypted);
        botCmd.Parameters.AddWithValue("$now", now);
        if (botCmd.ExecuteNonQuery() == 0) return Results.Conflict(new { error = "Bot ID is already in use." });
    }

    using (SqliteCommand ownerCmd = conn.CreateCommand())
    {
        ownerCmd.Transaction = tx;
        ownerCmd.CommandText = "SELECT 1 FROM bots WHERE id = $id AND device_id = $dev;";
        ownerCmd.Parameters.AddWithValue("$id", req.BotId);
        ownerCmd.Parameters.AddWithValue("$dev", deviceId);
        if (ownerCmd.ExecuteScalar() is null) return Results.BadRequest(new { error = "Bot is not registered for this device." });
    }

    using (SqliteCommand chCmd = conn.CreateCommand())
    {
        chCmd.Transaction = tx;
        chCmd.CommandText = """
            INSERT INTO channels (id, device_id, bot_id, telegram_chat_id, display_name, username, timezone, updated_utc)
            VALUES ($id, $dev, $botId, $chatId, $name, $user, $tz, $now)
            ON CONFLICT(id) DO UPDATE SET
                bot_id = excluded.bot_id,
                telegram_chat_id = excluded.telegram_chat_id,
                display_name = excluded.display_name,
                username = excluded.username,
                timezone = excluded.timezone,
                updated_utc = excluded.updated_utc
            WHERE channels.device_id = excluded.device_id;
            """;
        chCmd.Parameters.AddWithValue("$id", id);
        chCmd.Parameters.AddWithValue("$dev", deviceId);
        chCmd.Parameters.AddWithValue("$botId", req.BotId);
        chCmd.Parameters.AddWithValue("$chatId", req.TelegramChatId);
        chCmd.Parameters.AddWithValue("$name", req.DisplayName);
        chCmd.Parameters.AddWithValue("$user", (object?)req.Username ?? DBNull.Value);
        chCmd.Parameters.AddWithValue("$tz", req.Timezone);
        chCmd.Parameters.AddWithValue("$now", now);
        if (chCmd.ExecuteNonQuery() == 0) return Results.Conflict(new { error = "Channel ID is already in use." });
    }

    tx.Commit();
    return Results.Ok(new ChannelResponse(id, req.BotId, req.TelegramChatId, req.DisplayName, req.Username, req.Timezone));
});

app.MapPost("/api/v1/media", async (HttpContext ctx) =>
{
    if (!TryAuthenticate(ctx, migrator, out string deviceId)) return Results.Unauthorized();
    if (!ctx.Request.HasFormContentType) return Results.BadRequest(new { error = "Multipart form data expected." });

    IFormCollection form = await ctx.Request.ReadFormAsync(ctx.RequestAborted);
    IFormFile? file = form.Files["file"];
    if (file == null || file.Length == 0 || file.Length > 50 * 1024 * 1024)
    {
        return Results.BadRequest(new { error = "Invalid or oversized media file." });
    }

    string? clientMediaId = form["mediaId"].FirstOrDefault();
    if (clientMediaId is not null && !Guid.TryParseExact(clientMediaId, "N", out _))
    {
        return Results.BadRequest(new { error = "Invalid media ID." });
    }

    string mediaId = $"{deviceId}:{clientMediaId?.ToLowerInvariant() ?? Guid.NewGuid().ToString("N")}";
    string expectedSha = form["sha256"].FirstOrDefault() ?? string.Empty;
    int kind = int.TryParse(form["kind"].FirstOrDefault(), out int k) ? k : 3;

    string fileName = Path.GetFileName(file.FileName);
    string extension = Path.GetExtension(fileName);
    if (extension.Length > 10 || extension.Skip(1).Any(c => !char.IsAsciiLetterOrDigit(c)))
    {
        extension = string.Empty;
    }

    string relPath = $"{Guid.NewGuid():N}{extension}";
    string absPath = Path.Combine(mediaDir, relPath);
    string mimeType = file.ContentType ?? "application/octet-stream";
    bool created = false;
    bool committed = false;
    try
    {
        string actualSha;
        await using (var stream = new FileStream(absPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
        {
            created = true;
            await file.CopyToAsync(stream, ctx.RequestAborted);
            stream.Position = 0;
            actualSha = Convert.ToHexString(await SHA256.HashDataAsync(stream, ctx.RequestAborted)).ToLowerInvariant();
        }

        if (!string.IsNullOrWhiteSpace(expectedSha) && !string.Equals(expectedSha, actualSha, StringComparison.OrdinalIgnoreCase))
        {
            return Results.BadRequest(new { error = "SHA-256 mismatch." });
        }

        using SqliteConnection conn = migrator.OpenConnection();
        using SqliteTransaction tx = conn.BeginTransaction();
        using (SqliteCommand existing = conn.CreateCommand())
        {
            existing.Transaction = tx;
            existing.CommandText = "SELECT device_id, sha256, kind, mime_type, size_bytes FROM uploaded_media WHERE id = $id;";
            existing.Parameters.AddWithValue("$id", mediaId);
            using SqliteDataReader reader = existing.ExecuteReader();
            if (reader.Read())
            {
                if (reader.GetString(0) != deviceId || reader.GetString(1) != actualSha || reader.GetInt32(2) != kind)
                {
                    return Results.Conflict(new { error = "Media ID is already in use." });
                }

                return Results.Ok(new MediaUploadResponse(mediaId, actualSha, reader.GetInt64(4), reader.GetString(3)));
            }
        }

        using SqliteCommand cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
            INSERT INTO uploaded_media (id, device_id, sha256, file_name, mime_type, kind, size_bytes, relative_path, created_utc)
            VALUES ($id, $dev, $sha, $name, $mime, $kind, $size, $rel, $now);
            """;
        cmd.Parameters.AddWithValue("$id", mediaId);
        cmd.Parameters.AddWithValue("$dev", deviceId);
        cmd.Parameters.AddWithValue("$sha", actualSha);
        cmd.Parameters.AddWithValue("$name", fileName);
        cmd.Parameters.AddWithValue("$mime", mimeType);
        cmd.Parameters.AddWithValue("$kind", kind);
        cmd.Parameters.AddWithValue("$size", file.Length);
        cmd.Parameters.AddWithValue("$rel", relPath);
        cmd.Parameters.AddWithValue("$now", DateTime.UtcNow.ToString("O"));
        cmd.ExecuteNonQuery();
        tx.Commit();
        committed = true;
        return Results.Ok(new MediaUploadResponse(mediaId, actualSha, file.Length, mimeType));
    }
    finally
    {
        if (created && !committed) File.Delete(absPath);
    }
});

app.MapPost("/api/v1/publications", (
    [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
    CreatePublicationRequest req,
    HttpContext ctx) =>
{
    if (!TryAuthenticate(ctx, migrator, out string deviceId)) return Results.Unauthorized();
    if (string.IsNullOrWhiteSpace(idempotencyKey))
    {
        return Results.BadRequest(new { error = "Idempotency-Key header is required." });
    }

    using SqliteConnection conn = migrator.OpenConnection();
    using SqliteTransaction tx = conn.BeginTransaction();

    // Check idempotency
    using (SqliteCommand dupCmd = conn.CreateCommand())
    {
        dupCmd.Transaction = tx;
        dupCmd.CommandText = "SELECT id, revision FROM publication_jobs WHERE device_id = $dev AND idempotency_key = $key;";
        dupCmd.Parameters.AddWithValue("$dev", deviceId);
        dupCmd.Parameters.AddWithValue("$key", idempotencyKey.Trim());
        using SqliteDataReader r = dupCmd.ExecuteReader();
        if (r.Read())
        {
            string existingJobId = r.GetString(0);
            int existingRev = r.GetInt32(1);
            r.Close();
            List<TargetStatusDto> existingTargets = LoadTargetsByJob(conn, tx, existingJobId);
            return Results.Ok(new CreatePublicationResponse(existingJobId, existingRev, existingTargets));
        }
    }

    string jobId = Guid.NewGuid().ToString("N");
    string now = DateTime.UtcNow.ToString("O");

    using (SqliteCommand jobCmd = conn.CreateCommand())
    {
        jobCmd.Transaction = tx;
        jobCmd.CommandText = """
            INSERT INTO publication_jobs (id, device_id, client_post_id, revision, idempotency_key, created_utc)
            VALUES ($id, $dev, $postId, $rev, $key, $now);
            """;
        jobCmd.Parameters.AddWithValue("$id", jobId);
        jobCmd.Parameters.AddWithValue("$dev", deviceId);
        jobCmd.Parameters.AddWithValue("$postId", req.ClientPostId);
        jobCmd.Parameters.AddWithValue("$rev", req.Revision);
        jobCmd.Parameters.AddWithValue("$key", idempotencyKey.Trim());
        jobCmd.Parameters.AddWithValue("$now", now);
        jobCmd.ExecuteNonQuery();
    }

    var createdTargets = new List<TargetStatusDto>();
    foreach (CreatePublicationTargetDto target in req.Targets)
    {
        // Verify channel belongs to device
        long chatId;
        using (SqliteCommand chCmd = conn.CreateCommand())
        {
            chCmd.Transaction = tx;
            chCmd.CommandText = "SELECT telegram_chat_id FROM channels WHERE id = $chId AND device_id = $dev;";
            chCmd.Parameters.AddWithValue("$chId", target.ChannelId);
            chCmd.Parameters.AddWithValue("$dev", deviceId);
            object? res = chCmd.ExecuteScalar();
            if (res == null || res == DBNull.Value)
            {
                tx.Rollback();
                return Results.BadRequest(new { error = $"Channel '{target.ChannelId}' is not registered on server." });
            }
            chatId = Convert.ToInt64(res);
        }

        // Verify all media exist on server before committing QUEUED_SERVER
        var mediaIds = new List<string>();
        foreach (string mid in target.MediaIds ?? [])
        {
            using SqliteCommand mCmd = conn.CreateCommand();
            mCmd.Transaction = tx;
            mCmd.CommandText = """
                SELECT id FROM uploaded_media
                WHERE device_id = $dev AND (id = $mid OR id = $scoped)
                ORDER BY (id = $mid) DESC LIMIT 1;
                """;
            mCmd.Parameters.AddWithValue("$mid", mid);
            mCmd.Parameters.AddWithValue("$dev", deviceId);
            mCmd.Parameters.AddWithValue("$scoped", $"{deviceId}:{mid.ToLowerInvariant()}");
            if (mCmd.ExecuteScalar() is not string storedId)
            {
                tx.Rollback();
                return Results.BadRequest(new { error = $"Media '{mid}' must be uploaded before queueing." });
            }
            mediaIds.Add(storedId);
        }

        using (SqliteCommand tCmd = conn.CreateCommand())
        {
            tCmd.Transaction = tx;
            tCmd.CommandText = """
                INSERT INTO publication_targets (id, job_id, device_id, channel_id, revision, scheduled_at_utc, immutable_content_snapshot, media_hashes_json, options_json, status, telegram_chat_id, updated_utc)
                VALUES ($id, $jobId, $dev, $chId, $rev, $sched, $snap, $media, $opts, 'QUEUED_SERVER', $chatId, $now);
                """;
            tCmd.Parameters.AddWithValue("$id", target.TargetId);
            tCmd.Parameters.AddWithValue("$jobId", jobId);
            tCmd.Parameters.AddWithValue("$dev", deviceId);
            tCmd.Parameters.AddWithValue("$chId", target.ChannelId);
            tCmd.Parameters.AddWithValue("$rev", req.Revision);
            tCmd.Parameters.AddWithValue("$sched", target.ScheduledAtUtc.ToUniversalTime().ToString("O"));
            tCmd.Parameters.AddWithValue("$snap", JsonSerializer.Serialize(target.Content));
            tCmd.Parameters.AddWithValue("$media", JsonSerializer.Serialize(mediaIds));
            tCmd.Parameters.AddWithValue("$opts", JsonSerializer.Serialize(target.Options ?? new ServerTargetOptionsDto()));
            tCmd.Parameters.AddWithValue("$chatId", chatId);
            tCmd.Parameters.AddWithValue("$now", now);
            tCmd.ExecuteNonQuery();
        }

        using (SqliteCommand evCmd = conn.CreateCommand())
        {
            evCmd.Transaction = tx;
            evCmd.CommandText = """
                INSERT INTO publication_events (target_id, device_id, event_utc, event_type, details_sanitized)
                VALUES ($tid, $dev, $now, 'QUEUED_SERVER', $details);
                """;
            evCmd.Parameters.AddWithValue("$tid", target.TargetId);
            evCmd.Parameters.AddWithValue("$dev", deviceId);
            evCmd.Parameters.AddWithValue("$now", now);
            evCmd.Parameters.AddWithValue("$details", $"Queued revision {req.Revision} for {target.ScheduledAtUtc:O}");
            evCmd.ExecuteNonQuery();
        }

        createdTargets.Add(new TargetStatusDto(target.TargetId, target.ChannelId, req.Revision, target.ScheduledAtUtc.ToUniversalTime(), "QUEUED_SERVER", [], null, null));
    }

    tx.Commit();
    return Results.Ok(new CreatePublicationResponse(jobId, req.Revision, createdTargets));
});

app.MapGet("/api/v1/publications/{id}", (string id, HttpContext ctx) =>
{
    if (!TryAuthenticate(ctx, migrator, out string deviceId)) return Results.Unauthorized();
    using SqliteConnection conn = migrator.OpenConnection();
    using SqliteCommand cmd = conn.CreateCommand();
    cmd.CommandText = """
        SELECT id, channel_id, revision, scheduled_at_utc, status, telegram_message_ids_json, last_error_code, last_error_details_sanitized
        FROM publication_targets
        WHERE id = $id AND device_id = $dev;
        """;
    cmd.Parameters.AddWithValue("$id", id);
    cmd.Parameters.AddWithValue("$dev", deviceId);
    using SqliteDataReader r = cmd.ExecuteReader();
    return r.Read() ? Results.Ok(ReadTargetDto(r)) : Results.NotFound();
});

app.MapPatch("/api/v1/publications/{id}", (string id, PatchPublicationRequest req, HttpContext ctx) =>
{
    if (!TryAuthenticate(ctx, migrator, out string deviceId)) return Results.Unauthorized();
    using SqliteConnection conn = migrator.OpenConnection();
    using SqliteCommand cmd = conn.CreateCommand();
    cmd.CommandText = """
        UPDATE publication_targets
        SET scheduled_at_utc = COALESCE($sched, scheduled_at_utc),
            next_attempt_utc = CASE WHEN status = 'RETRY_WAIT' THEN COALESCE($sched, next_attempt_utc) ELSE next_attempt_utc END,
            revision = COALESCE($rev, revision),
            immutable_content_snapshot = COALESCE($snap, immutable_content_snapshot),
            updated_utc = $now
        WHERE id = $id AND device_id = $dev AND status IN ('QUEUED_SERVER', 'PAUSED', 'RETRY_WAIT');
        """;
    cmd.Parameters.AddWithValue("$sched", req.ScheduledAtUtc.HasValue ? req.ScheduledAtUtc.Value.ToUniversalTime().ToString("O") : DBNull.Value);
    cmd.Parameters.AddWithValue("$rev", req.NewRevision.HasValue ? req.NewRevision.Value : DBNull.Value);
    cmd.Parameters.AddWithValue("$snap", req.Content != null ? JsonSerializer.Serialize(req.Content) : DBNull.Value);
    cmd.Parameters.AddWithValue("$now", DateTime.UtcNow.ToString("O"));
    cmd.Parameters.AddWithValue("$id", id);
    cmd.Parameters.AddWithValue("$dev", deviceId);

    int updated = cmd.ExecuteNonQuery();
    return updated > 0 ? Results.Ok(new { updated = true }) : Results.Conflict(new { error = "Target cannot be modified in its current state (e.g. SENDING or SENT)." });
});

app.MapPost("/api/v1/publications/{id}/pause", (string id, HttpContext ctx) =>
    TransitionStatus(id, "PAUSED", ["QUEUED_SERVER", "RETRY_WAIT"], migrator, ctx));

app.MapPost("/api/v1/publications/{id}/resume", (string id, HttpContext ctx) =>
    TransitionStatus(id, "QUEUED_SERVER", ["PAUSED"], migrator, ctx));

app.MapPost("/api/v1/publications/{id}/cancel", (string id, HttpContext ctx) =>
    TransitionStatus(id, "CANCELED", ["QUEUED_SERVER", "PAUSED", "RETRY_WAIT"], migrator, ctx));

app.MapPost("/api/v1/publications/{id}/resolve", (string id, ResolveUnknownRequest req, HttpContext ctx) =>
{
    if (!TryAuthenticate(ctx, migrator, out string deviceId)) return Results.Unauthorized();
    string targetStatus = req.Resolution switch
    {
        "MARK_SENT" => "SENT",
        "RETRY" => "QUEUED_SERVER",
        "CANCEL" => "CANCELED",
        _ => string.Empty
    };
    if (string.IsNullOrEmpty(targetStatus)) return Results.BadRequest(new { error = "Invalid resolution." });

    using SqliteConnection conn = migrator.OpenConnection();
    using SqliteCommand cmd = conn.CreateCommand();
    cmd.CommandText = """
        UPDATE publication_targets
        SET status = $status,
            telegram_message_ids_json = COALESCE($ids, telegram_message_ids_json),
            updated_utc = $now
        WHERE id = $id AND device_id = $dev AND status = 'UNKNOWN_RESULT';
        """;
    cmd.Parameters.AddWithValue("$status", targetStatus);
    cmd.Parameters.AddWithValue("$ids", req.TelegramMessageIds != null ? JsonSerializer.Serialize(req.TelegramMessageIds) : DBNull.Value);
    cmd.Parameters.AddWithValue("$now", DateTime.UtcNow.ToString("O"));
    cmd.Parameters.AddWithValue("$id", id);
    cmd.Parameters.AddWithValue("$dev", deviceId);

    return cmd.ExecuteNonQuery() > 0 ? Results.Ok(new { status = targetStatus }) : Results.Conflict(new { error = "Target is not in UNKNOWN_RESULT state." });
});

app.MapGet("/api/v1/events", ([FromQuery] long? cursor, HttpContext ctx) =>
{
    if (!TryAuthenticate(ctx, migrator, out string deviceId)) return Results.Unauthorized();
    long minSeq = cursor ?? 0;

    using SqliteConnection conn = migrator.OpenConnection();
    using SqliteCommand cmd = conn.CreateCommand();
    cmd.CommandText = """
        SELECT seq, target_id, event_utc, event_type, details_sanitized
        FROM publication_events
        WHERE device_id = $dev AND seq > $cursor
        ORDER BY seq ASC
        LIMIT 200;
        """;
    cmd.Parameters.AddWithValue("$dev", deviceId);
    cmd.Parameters.AddWithValue("$cursor", minSeq);

    var events = new List<PublicationEventDto>();
    using SqliteDataReader r = cmd.ExecuteReader();
    while (r.Read())
    {
        events.Add(new PublicationEventDto(
            r.GetInt64(0),
            r.GetString(1),
            DateTime.Parse(r.GetString(2), null, System.Globalization.DateTimeStyles.RoundtripKind),
            r.GetString(3),
            r.GetString(4)));
    }
    return Results.Ok(events);
});

app.Run();

static bool TryAuthenticate(HttpContext ctx, ServerDbMigrator migrator, out string deviceId)
{
    deviceId = string.Empty;
    string? auth = ctx.Request.Headers.Authorization.FirstOrDefault();
    if (string.IsNullOrWhiteSpace(auth) || !auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
    {
        return false;
    }

    string token = auth["Bearer ".Length..].Trim();
    if (string.IsNullOrWhiteSpace(token))
    {
        return false;
    }

    string hash = SecretProtector.HashToken(token);
    using SqliteConnection conn = migrator.OpenConnection();
    using SqliteCommand cmd = conn.CreateCommand();
    cmd.CommandText = "SELECT device_id FROM auth_tokens WHERE token_hash = $hash AND revoked_utc IS NULL;";
    cmd.Parameters.AddWithValue("$hash", hash);
    if (cmd.ExecuteScalar() is string dev)
    {
        deviceId = dev;
        return true;
    }
    return false;
}

static IResult TransitionStatus(string id, string newStatus, string[] allowedFrom, ServerDbMigrator migrator, HttpContext ctx)
{
    if (!TryAuthenticate(ctx, migrator, out string deviceId)) return Results.Unauthorized();
    using SqliteConnection conn = migrator.OpenConnection();
    using SqliteCommand cmd = conn.CreateCommand();
    string allowed = string.Join(", ", allowedFrom.Select(s => $"'{s}'"));
    cmd.CommandText = $"""
        UPDATE publication_targets
        SET status = $newStatus,
            updated_utc = $now
        WHERE id = $id AND device_id = $dev AND status IN ({allowed});
        """;
    cmd.Parameters.AddWithValue("$newStatus", newStatus);
    cmd.Parameters.AddWithValue("$now", DateTime.UtcNow.ToString("O"));
    cmd.Parameters.AddWithValue("$id", id);
    cmd.Parameters.AddWithValue("$dev", deviceId);

    return cmd.ExecuteNonQuery() > 0
        ? Results.Ok(new { status = newStatus })
        : Results.Conflict(new { error = $"Cannot transition to {newStatus} from current state." });
}

static List<TargetStatusDto> LoadTargetsByJob(SqliteConnection conn, SqliteTransaction tx, string jobId)
{
    using SqliteCommand cmd = conn.CreateCommand();
    cmd.Transaction = tx;
    cmd.CommandText = """
        SELECT id, channel_id, revision, scheduled_at_utc, status, telegram_message_ids_json, last_error_code, last_error_details_sanitized
        FROM publication_targets
        WHERE job_id = $jobId;
        """;
    cmd.Parameters.AddWithValue("$jobId", jobId);
    var list = new List<TargetStatusDto>();
    using SqliteDataReader r = cmd.ExecuteReader();
    while (r.Read())
    {
        list.Add(ReadTargetDto(r));
    }
    return list;
}

static TargetStatusDto ReadTargetDto(SqliteDataReader r) => new(
    r.GetString(0),
    r.GetString(1),
    r.GetInt32(2),
    DateTime.Parse(r.GetString(3), null, System.Globalization.DateTimeStyles.RoundtripKind),
    r.GetString(4),
    JsonSerializer.Deserialize<List<long>>(r.GetString(5)) ?? [],
    r.IsDBNull(6) ? null : r.GetString(6),
    r.IsDBNull(7) ? null : r.GetString(7));

public partial class Program;
