using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;
using TelegramPostEditor.Server.Persistence;
using TelegramPostEditor.Server.Security;
using TelegramPostEditor.Server.Services;

namespace TelegramPostEditor.Server.Tests;

public sealed class ServerEndpointsTests
{
    [Fact]
    public async Task PairedDevicesCanQueueAndRetryWithoutAccessToEachOthersRecords()
    {
        string root = Path.Combine(Path.GetTempPath(), $"aitebar_server_http_{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(root, "media-other"));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(40));
        CancellationToken token = timeout.Token;
        Process? server = null;
        try
        {
            SeedPairingCodes(root);
            server = StartServer(root);
            string address = await ReadServerAddressAsync(server, token);
            using var first = new HttpClient { BaseAddress = new Uri(address) };
            using var second = new HttpClient { BaseAddress = new Uri(address) };
            PairResponse firstPair = await PairAsync(first, "first-code", token);
            PairResponse secondPair = await PairAsync(second, "second-code", token);

            string firstBot = $"{firstPair.DeviceId}:bot";
            string firstChannel = $"{firstPair.DeviceId}:channel";
            string secondBot = $"{secondPair.DeviceId}:bot";
            string secondChannel = $"{secondPair.DeviceId}:channel";
            Assert.Equal(HttpStatusCode.OK, await RegisterAsync(first, firstChannel, firstBot, "111:first", token));
            Assert.Equal(HttpStatusCode.OK, await RegisterAsync(second, secondChannel, secondBot, "222:second", token));
            Assert.Equal(HttpStatusCode.Conflict, await RegisterAsync(second, secondChannel, firstBot, "222:second", token));
            Assert.Equal(HttpStatusCode.Conflict, await RegisterAsync(second, firstChannel, secondBot, "222:replacement", token));
            Assert.Equal(HttpStatusCode.BadRequest, await RegisterAsync(second, secondChannel, firstBot, null, token));

            string clientMediaId = Guid.NewGuid().ToString("N");
            using HttpResponseMessage rejectedPath = await UploadAsync(second, "../media-other/escape", "unwanted", token);
            Assert.Equal(HttpStatusCode.BadRequest, rejectedPath.StatusCode);
            Assert.Empty(Directory.EnumerateFiles(Path.Combine(root, "media-other")));

            using HttpResponseMessage firstUpload = await UploadAsync(first, clientMediaId, "first attachment", token);
            using HttpResponseMessage secondUpload = await UploadAsync(second, clientMediaId, "second attachment", token);
            firstUpload.EnsureSuccessStatusCode();
            secondUpload.EnsureSuccessStatusCode();
            var firstMedia = (await firstUpload.Content.ReadFromJsonAsync<MediaUploadResponse>(token))!;
            var secondMedia = (await secondUpload.Content.ReadFromJsonAsync<MediaUploadResponse>(token))!;
            Assert.NotEqual(firstMedia.MediaId, secondMedia.MediaId);
            using HttpResponseMessage overwrite = await UploadAsync(first, clientMediaId, "replacement", token);
            Assert.Equal(HttpStatusCode.Conflict, overwrite.StatusCode);
            using HttpResponseMessage retryUpload = await UploadAsync(first, clientMediaId, "first attachment", token);
            Assert.Equal(firstMedia, await retryUpload.Content.ReadFromJsonAsync<MediaUploadResponse>(token));

            var target = new CreatePublicationTargetDto(Guid.NewGuid().ToString("N"), firstChannel, DateTime.UtcNow.AddHours(2),
                new ServerPostContentDto("telegram-document-v1", "Queued post"), [firstMedia.MediaId], new ServerTargetOptionsDto());
            using HttpResponseMessage queued = await QueueAsync(first, target, "delivery-key", token);
            queued.EnsureSuccessStatusCode();
            var accepted = (await queued.Content.ReadFromJsonAsync<CreatePublicationResponse>(token))!;
            using HttpResponseMessage repeated = await QueueAsync(first, target with { TargetId = Guid.NewGuid().ToString("N") }, "delivery-key", token);
            repeated.EnsureSuccessStatusCode();
            var existing = (await repeated.Content.ReadFromJsonAsync<CreatePublicationResponse>(token))!;
            Assert.Equal(accepted.ServerPublicationId, existing.ServerPublicationId);
            string targetPath = $"/api/v1/publications/{target.TargetId}";
            DateTime newTime = DateTime.UtcNow.AddHours(3);
            var timeChange = new PatchPublicationRequest(newTime, null, null, null, null);
            using HttpResponseMessage forbiddenEdit = await second.PatchAsJsonAsync(targetPath, timeChange, token);
            Assert.Equal(HttpStatusCode.Conflict, forbiddenEdit.StatusCode);
            using (var connection = new ServerDbMigrator(Path.Combine(root, "server.db")).OpenConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = """
                    UPDATE publication_targets SET status = 'RETRY_WAIT', next_attempt_utc = scheduled_at_utc WHERE id = $id;
                    """;
                command.Parameters.AddWithValue("$id", target.TargetId);
                Assert.Equal(1, command.ExecuteNonQuery());
            }
            using HttpResponseMessage rescheduled = await first.PatchAsJsonAsync(targetPath, timeChange, token);
            rescheduled.EnsureSuccessStatusCode();
            var moved = await first.GetFromJsonAsync<TargetStatusDto>(targetPath, token);
            Assert.Equal(target.TargetId, moved!.TargetId);
            Assert.Equal(newTime, moved.ScheduledAtUtc);
            using (var connection = new ServerDbMigrator(Path.Combine(root, "server.db")).OpenConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT next_attempt_utc FROM publication_targets WHERE id = $id;";
                command.Parameters.AddWithValue("$id", target.TargetId);
                Assert.Equal(newTime.ToString("O"), command.ExecuteScalar());
            }
            using HttpResponseMessage foreignMedia = await QueueAsync(second,
                target with { TargetId = Guid.NewGuid().ToString("N"), ChannelId = secondChannel }, "another-delivery", token);
            Assert.Equal(HttpStatusCode.BadRequest, foreignMedia.StatusCode);
        }
        finally
        {
            if (server is not null)
            {
                if (!server.HasExited) server.Kill(entireProcessTree: true);
                await server.WaitForExitAsync();
                server.Dispose();
            }
            SqliteConnection.ClearAllPools();
            Directory.Delete(root, recursive: true);
        }
    }

    private static void SeedPairingCodes(string root)
    {
        var migrator = new ServerDbMigrator(Path.Combine(root, "server.db"));
        migrator.EnsureMigrated();
        using var conn = migrator.OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO pairing_codes (code_hash, expires_utc)
            VALUES ($first, $expiration), ($second, $expiration);
            """;
        cmd.Parameters.AddWithValue("$first", SecretProtector.HashToken("first-code"));
        cmd.Parameters.AddWithValue("$second", SecretProtector.HashToken("second-code"));
        cmd.Parameters.AddWithValue("$expiration", DateTime.UtcNow.AddMinutes(5).ToString("O"));
        cmd.ExecuteNonQuery();
    }

    private static Process StartServer(string root)
    {
        var start = new ProcessStartInfo("dotnet")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            CreateNoWindow = true,
            WorkingDirectory = root
        };
        start.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "TelegramPostEditor.Server.dll"));
        start.ArgumentList.Add("--urls");
        start.ArgumentList.Add("http://127.0.0.1:0");
        start.ArgumentList.Add("--Storage:DataDir");
        start.ArgumentList.Add(root);
        start.ArgumentList.Add("--Logging:LogLevel:Microsoft.Hosting.Lifetime");
        start.ArgumentList.Add("Information");
        start.Environment["TPE_MASTER_KEY"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        return Process.Start(start)!;
    }

    private static async Task<string> ReadServerAddressAsync(Process server, CancellationToken token)
    {
        const string prefix = "Now listening on: ";
        while (await server.StandardOutput.ReadLineAsync(token) is { } line)
        {
            int index = line.IndexOf(prefix, StringComparison.Ordinal);
            if (index >= 0)
            {
                _ = server.StandardOutput.ReadToEndAsync(token);
                return line[(index + prefix.Length)..].Trim();
            }
        }
        throw new InvalidOperationException("Telegram scheduling server stopped before listening.");
    }

    private static async Task<PairResponse> PairAsync(HttpClient client, string code, CancellationToken token)
    {
        using var response = await client.PostAsJsonAsync("/api/v1/pair/confirm", new PairConfirmRequest(code, code), token);
        response.EnsureSuccessStatusCode();
        var pairing = (await response.Content.ReadFromJsonAsync<PairResponse>(token))!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", pairing.AccessToken);
        return pairing;
    }

    private static async Task<HttpStatusCode> RegisterAsync(
        HttpClient client, string channelId, string botId, string? botToken, CancellationToken token)
    {
        var channel = new ChannelUpsertRequest(botId, "Bot", botToken, -100123, "Channel", null, "UTC");
        using var response = await client.PutAsJsonAsync($"/api/v1/channels/{Uri.EscapeDataString(channelId)}", channel, token);
        return response.StatusCode;
    }

    private static async Task<HttpResponseMessage> UploadAsync(HttpClient client, string id, string data, CancellationToken token)
    {
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(id), "mediaId");
        form.Add(new ByteArrayContent(Encoding.UTF8.GetBytes(data)), "file", "photo.jpg");
        return await client.PostAsync("/api/v1/media", form, token);
    }

    private static async Task<HttpResponseMessage> QueueAsync(
        HttpClient client, CreatePublicationTargetDto target, string key, CancellationToken token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/publications")
        {
            Content = JsonContent.Create(new CreatePublicationRequest("post", 1, [target]))
        };
        request.Headers.Add("Idempotency-Key", key);
        return await client.SendAsync(request, token);
    }
}
