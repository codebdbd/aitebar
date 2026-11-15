using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AiteBar;
using AiteBar.TelegramPostEditor.Models;
using AiteBar.TelegramPostEditor.Persistence;
using AiteBar.TelegramPostEditor.Services;

namespace AiteBar.Tests.TelegramPostEditor;

public sealed class TelegramPublishingTests : IDisposable
{
    private readonly string _tempDir;
    private readonly PostDbMigrator _migrator;
    private readonly PostMediaStore _mediaStore;
    private readonly InMemoryCredentialStore _creds;
    private readonly TelegramSecretStore _secretStore;

    public TelegramPublishingTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"aitebar_tg_pub_tests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _migrator = new PostDbMigrator(Path.Combine(_tempDir, "posts.db"));
        _mediaStore = new PostMediaStore(_migrator, Path.Combine(_tempDir, "Media"));
        _creds = new InMemoryCredentialStore();
        _secretStore = new TelegramSecretStore(_creds);
    }

    [Fact]
    public void Validation_EnforcesTelegramLimitsAndMediaAlbumRules()
    {
        var tooLongText = new PostDocument
        {
            Blocks = [new PostBlock { Runs = [new PostTextRun { Text = new string('a', 4097) }] }]
        };
        PostValidationSummary textCheck = TelegramValidationService.Validate(tooLongText, []);
        Assert.False(textCheck.IsValid);
        Assert.Contains(textCheck.Errors, e => e.Contains("4096"));

        IReadOnlyList<string> split = TelegramValidationService.SplitIntoSequentialMessages(new string('x', 5000), 4096);
        Assert.Equal(2, split.Count);
        Assert.Equal(4096, split[0].Length);

        var mixedAlbum = new PostDocument
        {
            Media =
            [
                new PostMediaAttachment { MediaId = "m1", SortOrder = 0 },
                new PostMediaAttachment { MediaId = "m2", SortOrder = 1 }
            ]
        };
        PostValidationSummary albumCheck = TelegramValidationService.Validate(
            mixedAlbum,
            [
                new PostMediaItem { Id = "m1", Kind = MediaKind.Photo },
                new PostMediaItem { Id = "m2", Kind = MediaKind.Document }
            ]);
        Assert.False(albumCheck.IsValid);
        Assert.Contains(albumCheck.Errors, e => e.Contains("Documents cannot be mixed"));
    }

    [Fact]
    public async Task MultiChannelPublish_PartialFailure_RetriesOnlyFailedWithoutDuplicates()
    {
        int sendCount = 0;
        var handler = new StubHttpHandler(req =>
        {
            sendCount++;
            string body = req.Content!.ReadAsStringAsync().Result;
            if (body.Contains("-100222") && sendCount <= 2)
            {
                return new HttpResponseMessage(HttpStatusCode.Forbidden)
                {
                    Content = new StringContent("""{"ok":false,"error_code":403,"description":"Forbidden: bot was kicked"}""", Encoding.UTF8, "application/json")
                };
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent($"{{\"ok\":true,\"result\":{{\"message_id\":{100 + sendCount}}}}}", Encoding.UTF8, "application/json")
            };
        });

        var botClient = new TelegramBotClient(new HttpClient(handler));
        var channelService = new TelegramChannelService(_migrator, botClient, _secretStore);
        var pubService = new PublicationService(_migrator, _mediaStore, botClient, _secretStore, channelService);

        string botId = "bot-1";
        string secretRef = _secretStore.SaveBotToken(botId, "123456:TEST_TOKEN");
        channelService.SaveBot(new BotAccount { Id = botId, DisplayName = "TestBot", TokenSecretRef = secretRef, TelegramBotId = 123456 });

        var ch1 = new ChannelAccount { Id = "ch-1", TelegramChatId = -100111, Username = "pub1", DisplayName = "Channel 1", BotId = botId };
        var ch2 = new ChannelAccount { Id = "ch-2", TelegramChatId = -100222, DisplayName = "Channel 2", BotId = botId };
        channelService.SaveChannel(ch1);
        channelService.SaveChannel(ch2);

        var repo = new PostRepository(_migrator);
        var doc = new PostDocument
        {
            Title = "Multi",
            Blocks = [new PostBlock { Runs = [new PostTextRun { Text = "Hello channels!" }] }]
        };
        repo.SavePost(doc);

        IReadOnlyList<PublicationTarget> firstRun = await pubService.PublishNowAsync(doc, [ch1, ch2]);
        Assert.Equal(2, firstRun.Count);
        Assert.Equal(WorkflowStatus.SENT, firstRun[0].Status);
        Assert.Equal(WorkflowStatus.FAILED, firstRun[1].Status);
        Assert.Equal(2, sendCount);

        IReadOnlyList<PublicationTarget> retryRun = await pubService.RetryFailedTargetsAsync(doc, firstRun);
        Assert.Equal(WorkflowStatus.SENT, retryRun[0].Status);
        Assert.Equal(WorkflowStatus.SENT, retryRun[1].Status);
        // Only ch-2 was retried (total 3 sends, never re-sending ch-1).
        Assert.Equal(3, sendCount);
        Assert.Equal("https://t.me/pub1/101", TelegramChannelService.BuildMessageLink(ch1, retryRun[0].TelegramMessageIds[0]));
        Assert.Equal("https://t.me/c/222/103", TelegramChannelService.BuildMessageLink(ch2, retryRun[1].TelegramMessageIds[0]));
    }

    [Fact]
    public async Task NetworkDropAfterDispatch_MarksUnknownResult_AndBlocksAutoRetry()
    {
        var handler = new StubHttpHandler(_ => throw new HttpRequestException("Simulated socket reset"));
        var botClient = new TelegramBotClient(new HttpClient(handler));
        var channelService = new TelegramChannelService(_migrator, botClient, _secretStore);
        var pubService = new PublicationService(_migrator, _mediaStore, botClient, _secretStore, channelService);

        string secretRef = _secretStore.SaveBotToken("bot-u", "999:SECRET");
        channelService.SaveBot(new BotAccount { Id = "bot-u", DisplayName = "BotU", TokenSecretRef = secretRef });
        var ch = new ChannelAccount { Id = "ch-u", TelegramChatId = -100333, DisplayName = "ChU", BotId = "bot-u" };
        channelService.SaveChannel(ch);

        var repo = new PostRepository(_migrator);
        var doc = new PostDocument { Blocks = [new PostBlock { Runs = [new PostTextRun { Text = "Ping" }] }] };
        repo.SavePost(doc);
        IReadOnlyList<PublicationTarget> run = await pubService.PublishNowAsync(doc, [ch]);
        Assert.Single(run);
        Assert.Equal(WorkflowStatus.UNKNOWN_RESULT, run[0].Status);

        // RetryFailedTargetsAsync must NOT auto-retry UNKNOWN_RESULT.
        IReadOnlyList<PublicationTarget> retry = await pubService.RetryFailedTargetsAsync(doc, run);
        Assert.Equal(WorkflowStatus.UNKNOWN_RESULT, retry[0].Status);
    }

    [Fact]
    public void DeleteBot_RemovesSecretAndCascadesChannels()
    {
        var botClient = new TelegramBotClient(new HttpClient(new StubHttpHandler(_ => new HttpResponseMessage(System.Net.HttpStatusCode.OK))));
        var channelService = new TelegramChannelService(_migrator, botClient, _secretStore);

        string secretRef = _secretStore.SaveBotToken("bot-del", "111:SECRET");
        channelService.SaveBot(new BotAccount { Id = "bot-del", DisplayName = "DelBot", TokenSecretRef = secretRef });
        channelService.SaveChannel(new ChannelAccount { Id = "ch-1", TelegramChatId = -1001, DisplayName = "Ch1", BotId = "bot-del" });
        channelService.SaveChannel(new ChannelAccount { Id = "ch-2", TelegramChatId = -1002, DisplayName = "Ch2", BotId = "bot-del" });

        Assert.True(channelService.DeleteChannel("ch-1"));
        Assert.Single(channelService.GetChannels());

        Assert.True(channelService.DeleteBot("bot-del"));
        Assert.Empty(channelService.GetBots());
        Assert.Empty(channelService.GetChannels());
        Assert.Null(_secretStore.ReadBotToken("bot-del"));
    }

    [Fact]
    public void CredentialStore_SupportsAiAndTelegramTargets_AndRejectsUnknown()
    {
        Assert.True(WindowsAiCredentialStore.IsSupportedTarget("AiteBar/AI/conn-1"));
        Assert.True(WindowsAiCredentialStore.IsSupportedTarget(TelegramSecretStore.GetBotTargetName("bot-1")));
        Assert.True(WindowsAiCredentialStore.IsSupportedTarget(TelegramSecretStore.GetServerTargetName("dev-1")));

        Assert.False(WindowsAiCredentialStore.IsSupportedTarget(string.Empty));
        Assert.False(WindowsAiCredentialStore.IsSupportedTarget("OtherApp/secret"));
        Assert.False(WindowsAiCredentialStore.IsSupportedTarget("AiteBar:Other:123"));
    }

    [Theory]
    [InlineData("@my_channel", "@my_channel")]
    [InlineData("my_channel", "@my_channel")]
    [InlineData("  https://t.me/my_channel/  ", "@my_channel")]
    [InlineData("http://t.me/my_channel", "@my_channel")]
    [InlineData("t.me/my_channel/42", "@my_channel")]
    [InlineData("https://telegram.me/my_channel", "@my_channel")]
    [InlineData("1234567890", "-1001234567890")]
    [InlineData("  1234567890  ", "-1001234567890")]
    [InlineData("https://t.me/c/1234567890/42", "-1001234567890")]
    [InlineData("-1001234567890", "-1001234567890")]
    public void NormalizeChannelIdentifier_NormalizesUsernamesLinksAndNumericIds(string input, string expected)
    {
        Assert.Equal(expected, TelegramChannelService.NormalizeChannelIdentifier(input));
    }

    [Fact]
    public async Task ConnectChannelAsync_WithoutBotId_ResolvesBotAutomatically()
    {
        var handler = new StubHttpHandler(req =>
        {
            string uri = req.RequestUri!.AbsoluteUri;
            if (uri.Contains("/bot111:TOKEN_A/", StringComparison.Ordinal))
            {
                if (uri.EndsWith("/getMe", StringComparison.Ordinal))
                {
                    return JsonOk("""{"ok":true,"result":{"id":111,"username":"bot_a","first_name":"Bot A"}}""");
                }
                if (uri.EndsWith("/getChat", StringComparison.Ordinal))
                {
                    return new HttpResponseMessage(HttpStatusCode.Forbidden)
                    {
                        Content = new StringContent("""{"ok":false,"error_code":403,"description":"Forbidden"}""", Encoding.UTF8, "application/json")
                    };
                }
            }
            else if (uri.Contains("/bot222:TOKEN_B/", StringComparison.Ordinal))
            {
                if (uri.EndsWith("/getMe", StringComparison.Ordinal))
                {
                    return JsonOk("""{"ok":true,"result":{"id":222,"username":"bot_b","first_name":"Bot B"}}""");
                }
                if (uri.EndsWith("/getChat", StringComparison.Ordinal))
                {
                    string body = req.Content!.ReadAsStringAsync().Result;
                    Assert.Contains("@target_chan", body, StringComparison.Ordinal);
                    return JsonOk("""{"ok":true,"result":{"id":-100999,"title":"Target Channel","username":"target_chan","type":"channel"}}""");
                }
                if (uri.EndsWith("/getChatMember", StringComparison.Ordinal))
                {
                    return JsonOk("""{"ok":true,"result":{"status":"administrator","can_post_messages":true}}""");
                }
            }

            throw new InvalidOperationException($"Unexpected request: {uri}");
        });

        var botClient = new TelegramBotClient(new HttpClient(handler));
        var channelService = new TelegramChannelService(_migrator, botClient, _secretStore);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            channelService.ConnectChannelAsync("https://t.me/target_chan"));

        BotAccount botA = await channelService.ConnectBotAsync("111:TOKEN_A");
        BotAccount botAUpdated = await channelService.ConnectBotAsync("111:TOKEN_A");
        Assert.Equal(botA.Id, botAUpdated.Id);
        Assert.Single(channelService.GetBots());

        BotAccount botB = await channelService.ConnectBotAsync("222:TOKEN_B");
        ChannelAccount connected = await channelService.ConnectChannelAsync("https://t.me/target_chan", isTestChannel: true);

        Assert.Equal(botB.Id, connected.BotId);
        Assert.Equal(-100999, connected.TelegramChatId);
        Assert.Equal("Target Channel", connected.DisplayName);
        Assert.True(connected.IsTestChannel);
        Assert.Single(channelService.GetChannels());
    }

    [Fact]
    public async Task GetChatMemberAsync_WhenCanPostMessagesOmittedForAdmin_AllowsPosting()
    {
        var handler = new StubHttpHandler(_ =>
            JsonOk("""{"ok":true,"result":{"status":"administrator"}}"""));
        var botClient = new TelegramBotClient(new HttpClient(handler));

        TelegramChatMemberInfo info = await botClient.GetChatMemberAsync("123:TOK", -100123, 123);

        Assert.Equal("administrator", info.Status);
        Assert.True(info.CanPostMessages);
    }

    private static HttpResponseMessage JsonOk(string json) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json")
    };

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

    private sealed class InMemoryCredentialStore : IAiCredentialStore
    {
        private readonly Dictionary<string, string> _store = new(StringComparer.Ordinal);
        public void Write(string target, string secret) => _store[target] = secret;
        public string? Read(string target) => _store.TryGetValue(target, out string? v) ? v : null;
        public bool Delete(string target) => _store.Remove(target);
    }

    private sealed class StubHttpHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(responder(request));
    }
}
