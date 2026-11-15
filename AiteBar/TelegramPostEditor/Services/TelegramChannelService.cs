using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.Versioning;
using AiteBar.TelegramPostEditor.Models;
using AiteBar.TelegramPostEditor.Persistence;
using Microsoft.Data.Sqlite;

namespace AiteBar.TelegramPostEditor.Services;

[SupportedOSPlatform("windows6.1")]
public sealed class TelegramChannelService
{
    private readonly PostDbMigrator _migrator;
    private readonly TelegramBotClient _botClient;
    private readonly TelegramSecretStore _secretStore;

    public TelegramChannelService(
        PostDbMigrator migrator,
        TelegramBotClient botClient,
        TelegramSecretStore secretStore)
    {
        _migrator = migrator ?? throw new ArgumentNullException(nameof(migrator));
        _botClient = botClient ?? throw new ArgumentNullException(nameof(botClient));
        _secretStore = secretStore ?? throw new ArgumentNullException(nameof(secretStore));
        _migrator.EnsureMigrated();
    }

    public async Task<BotAccount> ConnectBotAsync(string token, string? customDisplayName = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        TelegramBotInfo info = await _botClient.GetMeAsync(token.Trim(), cancellationToken).ConfigureAwait(false);

        BotAccount? existing = GetBots().FirstOrDefault(b => b.TelegramBotId == info.Id);
        string botId = existing?.Id ?? Guid.NewGuid().ToString("N");
        string secretRef = _secretStore.SaveBotToken(botId, token.Trim());

        var bot = new BotAccount
        {
            Id = botId,
            DisplayName = string.IsNullOrWhiteSpace(customDisplayName)
                ? (string.IsNullOrWhiteSpace(info.Username) ? info.FirstName : $"@{info.Username}")
                : customDisplayName.Trim(),
            TokenSecretRef = secretRef,
            TelegramBotId = info.Id,
            Username = info.Username,
            State = "Active",
            CreatedUtc = existing?.CreatedUtc ?? DateTime.UtcNow
        };

        SaveBot(bot);
        return bot;
    }

    public void SaveBot(BotAccount bot)
    {
        ArgumentNullException.ThrowIfNull(bot);
        using SqliteConnection connection = _migrator.OpenConnection();
        using SqliteCommand cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO bots (id, display_name, token_secret_ref, telegram_bot_id, username, state, created_utc)
            VALUES ($id, $name, $ref, $tgId, $username, $state, $created)
            ON CONFLICT(id) DO UPDATE SET
                display_name = excluded.display_name,
                token_secret_ref = excluded.token_secret_ref,
                telegram_bot_id = excluded.telegram_bot_id,
                username = excluded.username,
                state = excluded.state;
            """;
        cmd.Parameters.AddWithValue("$id", bot.Id);
        cmd.Parameters.AddWithValue("$name", bot.DisplayName);
        cmd.Parameters.AddWithValue("$ref", bot.TokenSecretRef);
        cmd.Parameters.AddWithValue("$tgId", (object?)bot.TelegramBotId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$username", (object?)bot.Username ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$state", bot.State);
        cmd.Parameters.AddWithValue("$created", bot.CreatedUtc.ToString("O"));
        cmd.ExecuteNonQuery();
    }

    public IReadOnlyList<BotAccount> GetBots()
    {
        using SqliteConnection connection = _migrator.OpenConnection();
        using SqliteCommand cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT id, display_name, token_secret_ref, telegram_bot_id, username, state, created_utc FROM bots ORDER BY display_name ASC;";
        var list = new List<BotAccount>();
        using SqliteDataReader reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            list.Add(new BotAccount
            {
                Id = reader.GetString(0),
                DisplayName = reader.GetString(1),
                TokenSecretRef = reader.GetString(2),
                TelegramBotId = reader.IsDBNull(3) ? null : reader.GetInt64(3),
                Username = reader.IsDBNull(4) ? null : reader.GetString(4),
                State = reader.GetString(5),
                CreatedUtc = DateTime.Parse(reader.GetString(6), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)
            });
        }
        return list;
    }

    public static string NormalizeChannelIdentifier(string chatIdOrUsername)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(chatIdOrUsername);
        string value = chatIdOrUsername.Trim().TrimEnd('/');

        string[] prefixes =
        [
            "https://t.me/",
            "http://t.me/",
            "https://telegram.me/",
            "http://telegram.me/",
            "t.me/"
        ];

        foreach (string prefix in prefixes)
        {
            if (value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                string rest = value[prefix.Length..];
                if (rest.StartsWith("c/", StringComparison.OrdinalIgnoreCase))
                {
                    string afterC = rest[2..];
                    int slashIndex = afterC.IndexOf('/');
                    string candidate = slashIndex > 0 ? afterC[..slashIndex] : afterC;
                    if (long.TryParse(candidate, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
                    {
                        value = candidate;
                    }
                }
                else if (!rest.StartsWith("+", StringComparison.Ordinal))
                {
                    int slashIndex = rest.IndexOf('/');
                    value = slashIndex > 0 ? rest[..slashIndex] : rest;
                }
                break;
            }
        }

        if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long numericId))
        {
            if (numericId > 0)
            {
                return $"-100{numericId.ToString(CultureInfo.InvariantCulture)}";
            }

            return numericId.ToString(CultureInfo.InvariantCulture);
        }

        return value.StartsWith('@') ? value : $"@{value}";
    }

    public async Task<ChannelAccount> ConnectChannelAsync(
        string chatIdOrUsername,
        bool isTestChannel = false,
        string? timezone = null,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<BotAccount> bots = GetBots();
        if (bots.Count == 0)
        {
            throw new InvalidOperationException(LocalizationService.Get("TelegramSettings_NoBots"));
        }

        if (bots.Count == 1)
        {
            return await ConnectChannelAsync(bots[0].Id, chatIdOrUsername, isTestChannel, timezone, cancellationToken).ConfigureAwait(false);
        }

        Exception? lastError = null;
        foreach (BotAccount bot in bots)
        {
            try
            {
                return await ConnectChannelAsync(bot.Id, chatIdOrUsername, isTestChannel, timezone, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                lastError = ex;
            }
        }

        throw lastError ?? new InvalidOperationException("Failed to connect channel with any configured bot.");
    }

    public async Task<ChannelAccount> ConnectChannelAsync(
        string botId,
        string chatIdOrUsername,
        bool isTestChannel = false,
        string? timezone = null,
        CancellationToken cancellationToken = default)
    {
        BotAccount bot = GetBots().FirstOrDefault(b => b.Id == botId)
            ?? throw new KeyNotFoundException($"Bot '{botId}' was not found.");
        string token = _secretStore.ReadBotToken(bot.Id)
            ?? throw new InvalidOperationException("Bot token is missing from Windows Credential Manager.");

        string normalizedIdentifier = NormalizeChannelIdentifier(chatIdOrUsername);
        TelegramChatInfo chat = await _botClient.GetChatAsync(token, normalizedIdentifier, cancellationToken).ConfigureAwait(false);
        if (bot.TelegramBotId.HasValue)
        {
            TelegramChatMemberInfo member = await _botClient.GetChatMemberAsync(token, chat.Id, bot.TelegramBotId.Value, cancellationToken).ConfigureAwait(false);
            if (!member.CanPostMessages)
            {
                throw new InvalidOperationException($"Bot '{bot.DisplayName}' does not have permission to post messages in '{chat.Title ?? normalizedIdentifier}'.");
            }
        }

        ChannelAccount? existingChannel = GetChannels().FirstOrDefault(c => c.TelegramChatId == chat.Id);
        var channel = new ChannelAccount
        {
            Id = existingChannel?.Id ?? Guid.NewGuid().ToString("N"),
            TelegramChatId = chat.Id,
            Username = chat.Username,
            DisplayName = string.IsNullOrWhiteSpace(chat.Title) ? (chat.Username ?? chat.Id.ToString(CultureInfo.InvariantCulture)) : chat.Title,
            BotId = bot.Id,
            DefaultInstructionId = existingChannel?.DefaultInstructionId,
            Timezone = string.IsNullOrWhiteSpace(timezone) ? (existingChannel?.Timezone ?? TimeZoneInfo.Local.Id) : timezone.Trim(),
            IsTestChannel = isTestChannel,
            DefaultDisableNotification = existingChannel?.DefaultDisableNotification ?? false,
            DefaultDisableWebPagePreview = existingChannel?.DefaultDisableWebPagePreview ?? false
        };

        SaveChannel(channel);
        return channel;
    }

    public void SaveChannel(ChannelAccount channel)
    {
        ArgumentNullException.ThrowIfNull(channel);
        using SqliteConnection connection = _migrator.OpenConnection();
        using SqliteCommand cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO channels (id, telegram_chat_id, username, display_name, bot_id, default_instruction_id, timezone, is_test_channel, default_disable_notification, default_disable_web_preview)
            VALUES ($id, $chatId, $username, $name, $botId, $instId, $tz, $isTest, $noNotify, $noPreview)
            ON CONFLICT(id) DO UPDATE SET
                telegram_chat_id = excluded.telegram_chat_id,
                username = excluded.username,
                display_name = excluded.display_name,
                bot_id = excluded.bot_id,
                default_instruction_id = excluded.default_instruction_id,
                timezone = excluded.timezone,
                is_test_channel = excluded.is_test_channel,
                default_disable_notification = excluded.default_disable_notification,
                default_disable_web_preview = excluded.default_disable_web_preview;
            """;
        cmd.Parameters.AddWithValue("$id", channel.Id);
        cmd.Parameters.AddWithValue("$chatId", channel.TelegramChatId);
        cmd.Parameters.AddWithValue("$username", (object?)channel.Username ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$name", channel.DisplayName);
        cmd.Parameters.AddWithValue("$botId", channel.BotId);
        cmd.Parameters.AddWithValue("$instId", (object?)channel.DefaultInstructionId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$tz", channel.Timezone);
        cmd.Parameters.AddWithValue("$isTest", channel.IsTestChannel ? 1 : 0);
        cmd.Parameters.AddWithValue("$noNotify", channel.DefaultDisableNotification ? 1 : 0);
        cmd.Parameters.AddWithValue("$noPreview", channel.DefaultDisableWebPagePreview ? 1 : 0);
        cmd.ExecuteNonQuery();
    }

    public IReadOnlyList<ChannelAccount> GetChannels()
    {
        using SqliteConnection connection = _migrator.OpenConnection();
        using SqliteCommand cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT id, telegram_chat_id, username, display_name, bot_id, default_instruction_id, timezone, is_test_channel, default_disable_notification, default_disable_web_preview
            FROM channels
            ORDER BY is_test_channel DESC, display_name ASC;
            """;
        var list = new List<ChannelAccount>();
        using SqliteDataReader reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            list.Add(new ChannelAccount
            {
                Id = reader.GetString(0),
                TelegramChatId = reader.GetInt64(1),
                Username = reader.IsDBNull(2) ? null : reader.GetString(2),
                DisplayName = reader.GetString(3),
                BotId = reader.GetString(4),
                DefaultInstructionId = reader.IsDBNull(5) ? null : reader.GetString(5),
                Timezone = reader.GetString(6),
                IsTestChannel = reader.GetInt32(7) == 1,
                DefaultDisableNotification = reader.GetInt32(8) == 1,
                DefaultDisableWebPagePreview = reader.GetInt32(9) == 1
            });
        }
        return list;
    }

    public bool DeleteBot(string botId)
    {
        if (string.IsNullOrWhiteSpace(botId))
        {
            return false;
        }

        using SqliteConnection connection = _migrator.OpenConnection();
        using SqliteCommand cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM bots WHERE id = $id;";
        cmd.Parameters.AddWithValue("$id", botId);
        int affected = cmd.ExecuteNonQuery();
        _secretStore.DeleteBotToken(botId);
        return affected > 0;
    }

    public bool DeleteChannel(string channelId)
    {
        if (string.IsNullOrWhiteSpace(channelId))
        {
            return false;
        }

        using SqliteConnection connection = _migrator.OpenConnection();
        using SqliteCommand cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM channels WHERE id = $id;";
        cmd.Parameters.AddWithValue("$id", channelId);
        return cmd.ExecuteNonQuery() > 0;
    }

    public static string? BuildMessageLink(ChannelAccount channel, long messageId)
    {
        if (messageId <= 0)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(channel.Username))
        {
            string cleanUsername = channel.Username.TrimStart('@');
            return $"https://t.me/{cleanUsername}/{messageId}";
        }

        string chatIdText = channel.TelegramChatId.ToString(CultureInfo.InvariantCulture);
        if (chatIdText.StartsWith("-100", StringComparison.Ordinal) && chatIdText.Length > 4)
        {
            return $"https://t.me/c/{chatIdText[4..]}/{messageId}";
        }

        return null;
    }
}
