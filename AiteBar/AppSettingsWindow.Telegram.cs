using System.Globalization;
using System.IO;
using AiteBar.TelegramPostEditor.Models;
using AiteBar.TelegramPostEditor.Persistence;
using AiteBar.TelegramPostEditor.Services;

namespace AiteBar;

public partial class AppSettingsWindow
{
    private TelegramChannelService? _telegramChannelService;
    private List<BotAccount> _telegramBots = [];
    private List<ChannelAccount> _telegramChannels = [];
    private Dictionary<string, string> _telegramChannelCategories = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _telegramTokens = new(StringComparer.Ordinal);
    private readonly HashSet<string> _changedBots = new(StringComparer.Ordinal);
    private readonly HashSet<string> _changedChannels = new(StringComparer.Ordinal);
    private readonly HashSet<string> _removedBots = new(StringComparer.Ordinal);
    private readonly HashSet<string> _removedChannels = new(StringComparer.Ordinal);
    private Dictionary<string, string> _originalBotVersions = new(StringComparer.Ordinal);
    private Dictionary<string, string> _originalChannelVersions = new(StringComparer.Ordinal);

    private void InitializeTelegramSettings()
    {
        string dbPath = Path.Combine(PathHelper.AppDataFolder, "TelegramPostEditor", "posts.db");
        _telegramChannelService = new TelegramChannelService(new PostDbMigrator(dbPath), new TelegramBotClient(), new TelegramSecretStore());
        _telegramBots = OrderList(_telegramChannelService.GetBots(), _settings.TelegramBotDisplayOrder, bot => bot.Id);
        _telegramChannels = OrderList(_telegramChannelService.GetChannels(), _settings.TelegramChannelDisplayOrder, channel => channel.Id);
        _originalBotVersions = _telegramBots.ToDictionary(bot => bot.Id, bot => JsonSerializer.Serialize(bot), StringComparer.Ordinal);
        _originalChannelVersions = _telegramChannels.ToDictionary(channel => channel.Id, channel => JsonSerializer.Serialize(channel), StringComparer.Ordinal);
        _telegramChannelCategories = new(_settings.TelegramPublishingCategories, StringComparer.Ordinal);
        RefreshTelegramSettingsLists();
    }

    private void RefreshTelegramSettingsLists()
    {
        TelegramBotsList.Children.Clear();
        TelegramChannelsList.Children.Clear();
        TxtTelegramBotsEmpty.Visibility = VisibleWhen(_telegramBots.Count == 0);
        TxtTelegramChannelsEmpty.Visibility = VisibleWhen(_telegramChannels.Count == 0);
        for (int index = 0; index < _telegramBots.Count; index++)
        {
            BotAccount bot = _telegramBots[index];
            int position = index;
            string details = string.Join(" · ", new[] { bot.Username, bot.TelegramBotId?.ToString(CultureInfo.InvariantCulture), _sourceChecks.GetValueOrDefault(bot.Id) }
                .Where(value => !string.IsNullOrWhiteSpace(value)));
            TelegramBotsList.Children.Add(BuildSettingsListRow(bot.DisplayName, details, null, null, index, _telegramBots.Count,
                offset => { MoveListItem(_telegramBots, position, offset); RefreshTelegramSettingsLists(); },
                async () =>
                {
                    await _telegramChannelService!.VerifyBotAsync(GetDraftBotToken(bot.Id), _listOperations.Token);
                    _sourceChecks[bot.Id] = Locale("SettingsLists_Checked");
                    RefreshTelegramSettingsLists();
                }, async () => await EditTelegramBotAsync(bot), () =>
                {
                    if (_telegramChannels.Any(channel => channel.BotId == bot.Id)) { ShowListMessage("SettingsLists_TelegramInUse"); return; }
                    _telegramBots.RemoveAt(position);
                    if (_originalBotVersions.ContainsKey(bot.Id)) _removedBots.Add(bot.Id);
                    RegisterListUndo(SettingsListKind.TelegramBots, () =>
                    {
                        _removedBots.Remove(bot.Id);
                        _telegramBots.Insert(Math.Min(position, _telegramBots.Count), bot);
                        RefreshTelegramSettingsLists();
                    });
                    RefreshTelegramSettingsLists();
                }));
        }
        for (int index = 0; index < _telegramChannels.Count; index++)
        {
            ChannelAccount channel = _telegramChannels[index];
            int position = index;
            string details = string.Join(" · ", new[] { ChannelAddress(channel), _telegramBots.FirstOrDefault(bot => bot.Id == channel.BotId)?.DisplayName,
                _telegramChannelCategories.GetValueOrDefault(channel.Id), _sourceChecks.GetValueOrDefault(channel.Id) }.Where(value => !string.IsNullOrWhiteSpace(value)));
            TelegramChannelsList.Children.Add(BuildSettingsListRow(channel.DisplayName, details, null, null, position, _telegramChannels.Count,
                offset => { MoveListItem(_telegramChannels, position, offset); RefreshTelegramSettingsLists(); },
                async () =>
                {
                    BotAccount bot = _telegramBots.First(item => item.Id == channel.BotId);
                    await _telegramChannelService!.VerifyChannelAsync(bot, GetDraftBotToken(bot.Id), ChannelAddress(channel), _listOperations.Token);
                    _sourceChecks[channel.Id] = Locale("SettingsLists_Checked");
                    RefreshTelegramSettingsLists();
                }, async () => await EditTelegramChannelAsync(channel), () =>
                {
                    _telegramChannels.RemoveAt(position);
                    if (_originalChannelVersions.ContainsKey(channel.Id)) _removedChannels.Add(channel.Id);
                    RegisterListUndo(SettingsListKind.TelegramPublishing, () =>
                    {
                        if (!_telegramBots.Any(bot => bot.Id == channel.BotId)) { ShowListMessage("TelegramSettings_NoBots"); return; }
                        _removedChannels.Remove(channel.Id);
                        _telegramChannels.Insert(Math.Min(position, _telegramChannels.Count), channel);
                        RefreshTelegramSettingsLists();
                    });
                    RefreshTelegramSettingsLists();
                }));
        }
    }

    private string GetDraftBotToken(string botId) => _telegramTokens.GetValueOrDefault(botId) ??
        _telegramChannelService?.ReadBotToken(botId) ?? throw new InvalidOperationException(Locale("SettingsLists_InvalidEntry"));

    private static string ChannelAddress(ChannelAccount channel) => channel.TelegramChatId.ToString(CultureInfo.InvariantCulture);

    private async Task EditTelegramBotAsync(BotAccount? existing)
    {
        string[]? values = ShowListEditor(ListLabel(SettingsListKind.TelegramBots),
            new("SettingsLists_Name", existing?.DisplayName ?? ""), new("SettingsLists_Token", Secret: true));
        if (values is null) return;
        try { await StageBotAsync(Cell(values, 0), "", Cell(values, 1), existing); }
        catch (Exception) { if (!_listOperations.IsCancellationRequested) ShowListMessage("SettingsLists_InvalidEntry"); }
    }

    private async Task StageBotAsync(string name, string expectedId, string token, BotAccount? existing)
    {
        if (token.Length == 0)
        {
            if (existing is null || name.Length == 0) throw new InvalidOperationException();
            existing.DisplayName = name;
            _changedBots.Add(existing.Id);
            RefreshTelegramSettingsLists();
            return;
        }
        TelegramBotInfo info = await _telegramChannelService!.VerifyBotAsync(token, _listOperations.Token);
        _listOperations.Token.ThrowIfCancellationRequested();
        if (existing?.TelegramBotId is long currentId && info.Id != currentId ||
            expectedId.Length > 0 && expectedId != info.Id.ToString(CultureInfo.InvariantCulture) ||
            _telegramBots.Any(bot => bot.Id != existing?.Id && bot.TelegramBotId == info.Id) ||
            _telegramChannelService.GetBots().Any(bot => bot.Id != existing?.Id && bot.TelegramBotId == info.Id))
        {
            throw new InvalidOperationException();
        }
        var bot = existing ?? new BotAccount();
        bot.DisplayName = name.Length == 0 ? (string.IsNullOrWhiteSpace(info.Username) ? info.FirstName : "@" + info.Username) : name;
        bot.Username = info.Username;
        bot.TelegramBotId = info.Id;
        bot.TokenSecretRef = TelegramSecretStore.GetBotTargetName(bot.Id);
        if (existing is null) _telegramBots.Add(bot);
        _telegramTokens[bot.Id] = token;
        _changedBots.Add(bot.Id);
        _sourceChecks[bot.Id] = Locale("SettingsLists_Checked");
        RefreshTelegramSettingsLists();
    }

    private async Task EditTelegramChannelAsync(ChannelAccount? existing)
    {
        if (_telegramBots.Count == 0) { ShowListMessage("TelegramSettings_NoBots"); return; }
        var labels = _telegramBots.ToDictionary(bot => $"{bot.DisplayName} · {bot.TelegramBotId?.ToString(CultureInfo.InvariantCulture) ?? bot.Id}", bot => bot.Id);
        string currentBot = labels.FirstOrDefault(pair => pair.Value == existing?.BotId).Key ?? "";
        string[]? values = ShowListEditor(ListLabel(SettingsListKind.TelegramPublishing),
            new("SettingsLists_Name", existing?.DisplayName ?? ""),
            new("SettingsLists_ChannelAddress", existing is null ? "" : ChannelAddress(existing)),
            new("TelegramSettings_BotsTitle", currentBot, Options: [.. labels.Keys]),
            new("SettingsLists_Category", existing is null ? "" : _telegramChannelCategories.GetValueOrDefault(existing.Id, "")));
        if (values is null) return;
        try { await StageChannelAsync(Cell(values, 0), Cell(values, 1), labels.GetValueOrDefault(Cell(values, 2), ""), Cell(values, 3), existing); }
        catch (Exception) { if (!_listOperations.IsCancellationRequested) ShowListMessage("SettingsLists_InvalidEntry"); }
    }

    private async Task StageChannelAsync(string name, string address, string botId, string category, ChannelAccount? existing)
    {
        if (!IsTelegramAddress(address)) throw new InvalidOperationException();
        BotAccount bot = _telegramBots.First(item => item.Id == botId);
        TelegramChatInfo chat = await _telegramChannelService!.VerifyChannelAsync(bot, GetDraftBotToken(bot.Id), address, _listOperations.Token);
        _listOperations.Token.ThrowIfCancellationRequested();
        if (existing is not null && chat.Id != existing.TelegramChatId ||
            _telegramChannels.Any(channel => channel.Id != existing?.Id && channel.TelegramChatId == chat.Id) ||
            _telegramChannelService.GetChannels().Any(channel => channel.Id != existing?.Id && channel.TelegramChatId == chat.Id))
        {
            throw new InvalidOperationException();
        }
        var channel = existing ?? new ChannelAccount();
        channel.DisplayName = name.Length > 0 ? name : chat.Title ?? chat.Username ?? address;
        channel.Username = chat.Username;
        channel.TelegramChatId = chat.Id;
        channel.BotId = bot.Id;
        if (existing is null) _telegramChannels.Add(channel);
        _telegramChannelCategories[channel.Id] = category;
        _changedChannels.Add(channel.Id);
        _sourceChecks[channel.Id] = Locale("SettingsLists_Checked");
        RefreshTelegramSettingsLists();
    }

    private void SaveTelegramListDraft()
    {
        var bots = _telegramBots.Where(bot => _changedBots.Contains(bot.Id)).ToList();
        var channels = _telegramChannels.Where(channel => _changedChannels.Contains(channel.Id)).ToList();
        _telegramChannelService?.ApplySettingsDraft(bots, channels,
            _telegramTokens.Where(pair => bots.Any(bot => bot.Id == pair.Key)).ToDictionary(), _removedBots, _removedChannels,
            _originalBotVersions, _originalChannelVersions);
        _changedBots.Clear();
        _changedChannels.Clear();
        _removedBots.Clear();
        _removedChannels.Clear();
        _telegramTokens.Clear();
    }
}
