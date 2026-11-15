using System.IO;
using System.Windows;
using System.Windows.Controls;
using AiteBar.TelegramPostEditor.Models;
using AiteBar.TelegramPostEditor.Persistence;
using AiteBar.TelegramPostEditor.Services;

namespace AiteBar;

public partial class AppSettingsWindow
{
    private TelegramChannelService? _telegramChannelService;

    private void InitializeTelegramSettings()
    {
        string dbPath = Path.Combine(PathHelper.AppDataFolder, "TelegramPostEditor", "posts.db");
        var migrator = new PostDbMigrator(dbPath);
        var secretStore = new TelegramSecretStore();
        var botClient = new TelegramBotClient();
        _telegramChannelService = new TelegramChannelService(migrator, botClient, secretStore);
        RefreshTelegramSettingsLists();
    }

    private void RefreshTelegramSettingsLists()
    {
        if (_telegramChannelService == null)
        {
            return;
        }

        IReadOnlyList<BotAccount> bots = _telegramChannelService.GetBots();
        TelegramBotsList.Children.Clear();
        TxtTelegramBotsEmpty.Visibility = bots.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        for (int index = 0; index < bots.Count; index++)
        {
            TelegramBotsList.Children.Add(BuildTelegramBotRow(bots[index]));
            if (index < bots.Count - 1)
            {
                TelegramBotsList.Children.Add(new Border { Style = (Style)FindResource("ModernDividerStyle") });
            }
        }

        IReadOnlyList<ChannelAccount> channels = _telegramChannelService.GetChannels();
        TelegramChannelsList.Children.Clear();
        TxtTelegramChannelsEmpty.Visibility = channels.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        for (int index = 0; index < channels.Count; index++)
        {
            TelegramChannelsList.Children.Add(BuildTelegramChannelRow(channels[index]));
            if (index < channels.Count - 1)
            {
                TelegramChannelsList.Children.Add(new Border { Style = (Style)FindResource("ModernDividerStyle") });
            }
        }
    }

    private Grid BuildTelegramBotRow(BotAccount bot)
    {
        var row = new Grid { Margin = new Thickness(0, 10, 0, 10) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var info = new StackPanel { Margin = new Thickness(0, 0, 16, 0), VerticalAlignment = VerticalAlignment.Center };
        info.Children.Add(new TextBlock
        {
            Text = bot.DisplayName,
            Style = (Style)FindResource("ModernSettingTitleStyle"),
            TextTrimming = TextTrimming.CharacterEllipsis
        });
        string subtitle = string.IsNullOrWhiteSpace(bot.Username)
            ? $"ID {bot.TelegramBotId}"
            : $"@{bot.Username.TrimStart('@')} · ID {bot.TelegramBotId}";
        info.Children.Add(new TextBlock
        {
            Text = subtitle,
            Style = (Style)FindResource("ModernSettingDescriptionStyle"),
            TextWrapping = TextWrapping.Wrap
        });

        var actions = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        actions.Children.Add(CreateAiRowButton(
            LocalizationService.Get("Common_Delete"),
            "Common_Delete",
            bot.Id,
            BtnTelegramRemoveBot_Click,
            150));

        Grid.SetColumn(info, 0);
        Grid.SetColumn(actions, 1);
        row.Children.Add(info);
        row.Children.Add(actions);
        return row;
    }

    private Grid BuildTelegramChannelRow(ChannelAccount channel)
    {
        var row = new Grid { Margin = new Thickness(0, 10, 0, 10) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var info = new StackPanel { Margin = new Thickness(0, 0, 16, 0), VerticalAlignment = VerticalAlignment.Center };
        string title = channel.IsTestChannel ? $"{channel.DisplayName} [TEST]" : channel.DisplayName;
        info.Children.Add(new TextBlock
        {
            Text = title,
            Style = (Style)FindResource("ModernSettingTitleStyle"),
            TextTrimming = TextTrimming.CharacterEllipsis
        });
        info.Children.Add(new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(channel.Username)
                ? $"Chat ID: {channel.TelegramChatId}"
                : $"@{channel.Username.TrimStart('@')} · {channel.TelegramChatId}",
            Style = (Style)FindResource("ModernSettingDescriptionStyle"),
            TextWrapping = TextWrapping.Wrap
        });

        var actions = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        actions.Children.Add(CreateAiRowButton(
            LocalizationService.Get("Common_Delete"),
            "Common_Delete",
            channel.Id,
            BtnTelegramRemoveChannel_Click,
            150));

        Grid.SetColumn(info, 0);
        Grid.SetColumn(actions, 1);
        row.Children.Add(info);
        row.Children.Add(actions);
        return row;
    }

    private void BtnTelegramRemoveBot_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string botId })
        {
            _telegramChannelService?.DeleteBot(botId);
            RefreshTelegramSettingsLists();
        }
    }

    private void BtnTelegramRemoveChannel_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string channelId })
        {
            _telegramChannelService?.DeleteChannel(channelId);
            RefreshTelegramSettingsLists();
        }
    }

    private async void BtnTelegramAddBot_Click(object sender, RoutedEventArgs e)
    {
        if (_telegramChannelService == null)
        {
            return;
        }

        string token = PwdTelegramBotToken.Password;
        if (string.IsNullOrWhiteSpace(token))
        {
            return;
        }

        BtnTelegramAddBot.IsEnabled = false;
        try
        {
            await _telegramChannelService.ConnectBotAsync(token);
            PwdTelegramBotToken.Clear();
            RefreshTelegramSettingsLists();
        }
        catch (Exception ex)
        {
            new DarkDialog(ex.Message) { Owner = this }.ShowDialog();
        }
        finally
        {
            BtnTelegramAddBot.IsEnabled = true;
        }
    }

    private async void BtnTelegramAddChannel_Click(object sender, RoutedEventArgs e)
    {
        if (_telegramChannelService == null ||
            string.IsNullOrWhiteSpace(TxtTelegramChannelInput.Text))
        {
            return;
        }

        BtnTelegramAddChannel.IsEnabled = false;
        try
        {
            if (!string.IsNullOrWhiteSpace(PwdTelegramBotToken.Password))
            {
                await _telegramChannelService.ConnectBotAsync(PwdTelegramBotToken.Password);
                PwdTelegramBotToken.Clear();
                RefreshTelegramSettingsLists();
            }

            await _telegramChannelService.ConnectChannelAsync(TxtTelegramChannelInput.Text);
            TxtTelegramChannelInput.Clear();
            RefreshTelegramSettingsLists();
        }
        catch (Exception ex)
        {
            new DarkDialog(ex.Message) { Owner = this }.ShowDialog();
        }
        finally
        {
            BtnTelegramAddChannel.IsEnabled = true;
        }
    }
}
