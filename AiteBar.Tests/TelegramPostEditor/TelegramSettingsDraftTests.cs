using System.IO;
using System.Text.Json;
using AiteBar.TelegramPostEditor.Models;
using AiteBar.TelegramPostEditor.Persistence;
using AiteBar.TelegramPostEditor.Services;

namespace AiteBar.Tests.TelegramPostEditor;

public sealed class TelegramSettingsDraftTests
{
    [Fact]
    public void FailedDeletionRollsBackTheDraftAndRestoresTheBotToken()
    {
        string directory = Path.Combine(Path.GetTempPath(), "AiteBar-TelegramDraft-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var secrets = new TelegramSecretStore(new TestCredentials());
            var service = new TelegramChannelService(new PostDbMigrator(Path.Combine(directory, "posts.db")), new TelegramBotClient(), secrets);
            var original = new BotAccount { DisplayName = "Original", TelegramBotId = 123 };
            original.TokenSecretRef = secrets.SaveBotToken(original.Id, "original-token");
            service.SaveBot(original);
            var channel = new ChannelAccount { DisplayName = "Channel", TelegramChatId = -1001234567890, BotId = original.Id };
            service.SaveChannel(channel);
            var newBot = new BotAccount { DisplayName = "New", TelegramBotId = 456 };
            newBot.TokenSecretRef = TelegramSecretStore.GetBotTargetName(newBot.Id);

            Assert.Throws<InvalidOperationException>(() => service.ApplySettingsDraft(
                [newBot], [], new Dictionary<string, string> { [newBot.Id] = "new-token" },
                new HashSet<string> { original.Id }, new HashSet<string>(),
                new Dictionary<string, string> { [original.Id] = JsonSerializer.Serialize(Assert.Single(service.GetBots())) },
                new Dictionary<string, string>()));

            Assert.Equal(original.Id, Assert.Single(service.GetBots()).Id);
            Assert.Equal(channel.Id, Assert.Single(service.GetChannels()).Id);
            Assert.Equal("original-token", secrets.ReadBotToken(original.Id));
            Assert.Null(secrets.ReadBotToken(newBot.Id));
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            Directory.Delete(directory, recursive: true);
        }
    }

    private sealed class TestCredentials : IAiCredentialStore
    {
        private readonly Dictionary<string, string> _secrets = new(StringComparer.Ordinal);
        public void Write(string target, string secret) => _secrets[target] = secret;
        public string? Read(string target) => _secrets.GetValueOrDefault(target);
        public bool Delete(string target) => _secrets.Remove(target);
    }
}
