using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AiteBar.IptvPlayer;

namespace AiteBar;

public partial class AppSettingsWindow
{
    private IptvSettings _iptvDraft = new();
    private readonly Dictionary<string, string> _pendingIptvSecrets = new(StringComparer.Ordinal);
    private readonly HashSet<string> _deletedIptvSourceIds = new(StringComparer.OrdinalIgnoreCase);
    private IptvPlayerStore? _iptvStore;
    private IptvPlaylistService? _iptvPlaylistService;

    private void InitializeIptvSettings()
    {
        _iptvStore = new IptvPlayerStore();
        _iptvPlaylistService = new IptvPlaylistService(_iptvStore);

        _iptvDraft = _settings.Iptv != null
            ? _settings.Iptv.Clone()
            : new IptvSettings();

        ChkIptvAutoUpdate.IsChecked = _iptvDraft.AutoUpdateEnabled;
        TxtIptvUpdateInterval.Text = _iptvDraft.UpdateIntervalHours.ToString();

        RefreshIptvSourcesList();
    }

    private void RefreshIptvSourcesList()
    {
        if (IptvSourcesList == null)
        {
            return;
        }

        IptvSourcesList.Children.Clear();
        var sources = _iptvDraft.Sources.OrderBy(s => s.SortOrder).ToList();
        TxtIptvSourcesEmpty.Visibility = sources.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        for (int i = 0; i < sources.Count; i++)
        {
            var source = sources[i];
            source.SortOrder = i;
            var row = BuildIptvSourceRow(source, i, sources.Count);
            IptvSourcesList.Children.Add(row);

            if (i < sources.Count - 1)
            {
                IptvSourcesList.Children.Add(new Border { Style = (Style)FindResource("ModernDividerStyle") });
            }
        }
    }

    private Grid BuildIptvSourceRow(IptvPlaylistSource source, int index, int totalCount)
    {
        var row = new Grid { Margin = new Thickness(0, 8, 0, 8) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Чекбокс активности
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Инфо
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Кнопки действий

        // 1. Чекбокс включен/выключен
        var chkEnabled = new CheckBox
        {
            IsChecked = source.IsEnabled,
            Style = (Style)FindResource("ModernSwitchStyle"),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 12, 0),
            ToolTip = LocalizationService.Get("Iptv_ToggleEnabled") ?? "Включить/отключить источник"
        };
        chkEnabled.Click += (_, _) =>
        {
            source.IsEnabled = chkEnabled.IsChecked == true;
        };
        Grid.SetColumn(chkEnabled, 0);
        row.Children.Add(chkEnabled);

        // 2. Блок названия и статуса
        var infoPanel = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 12, 0)
        };

        var titleBlock = new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(source.Name) ? "Безымянный плейлист" : source.Name,
            Style = (Style)FindResource("ModernSettingTitleStyle"),
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        infoPanel.Children.Add(titleBlock);

        string kindLabel = source.Kind switch
        {
            IptvSourceKind.Url => "URL",
            IptvSourceKind.LocalFile => "Файл",
            IptvSourceKind.InlineText => "Текст",
            IptvSourceKind.SingleStream => "Поток",
            _ => "Источник"
        };

        string statusText = source.ChannelCount > 0
            ? $"{kindLabel} • {source.ChannelCount} кан."
            : $"{kindLabel} • Нет кэша";

        if (!string.IsNullOrWhiteSpace(source.LastStatus) && source.LastStatus != "Ok")
        {
            statusText += $" • Ошибка: {source.LastErrorMessage}";
        }

        var statusBlock = new TextBlock
        {
            Text = statusText,
            Style = (Style)FindResource("ModernSettingDescriptionStyle"),
            Foreground = source.LastStatus == "Error"
                ? new SolidColorBrush(System.Windows.Media.Color.FromRgb(240, 90, 90))
                : (Brush)FindResource("MutedText")
        };
        infoPanel.Children.Add(statusBlock);

        Grid.SetColumn(infoPanel, 1);
        row.Children.Add(infoPanel);

        // 3. Блок кнопок управления
        var actionsPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center
        };

        // Вверх
        var btnUp = CreateIconButton("\uF19B", "Вверх", () => MoveIptvSource(index, -1));
        btnUp.IsEnabled = index > 0;
        actionsPanel.Children.Add(btnUp);

        // Вниз
        var btnDown = CreateIconButton("\uF148", "Вниз", () => MoveIptvSource(index, 1));
        btnDown.IsEnabled = index < totalCount - 1;
        actionsPanel.Children.Add(btnDown);

        // Проверить
        var btnCheck = CreateIconButton("\uF298", "Проверить", async () =>
        {
            if (_iptvPlaylistService == null) return;
            var res = await _iptvPlaylistService.CheckSourceAsync(source);
            string msg = res.IsSuccess
                ? $"OK: {res.TotalCount} кан. ({res.ElapsedMs} ms)"
                : $"Ошибка: {res.ErrorMessage}";
            System.Windows.MessageBox.Show(this, msg, "Проверка плейлиста", MessageBoxButton.OK,
                res.IsSuccess ? MessageBoxImage.Information : MessageBoxImage.Warning);
        });
        actionsPanel.Children.Add(btnCheck);

        // Обновить
        Button? btnRefresh = null;
        btnRefresh = CreateIconButton("\uF190", "Обновить", async () =>
        {
            if (_iptvPlaylistService == null || btnRefresh == null) return;
            btnRefresh.IsEnabled = false;
            try
            {
                bool ok = await _iptvPlaylistService.RefreshSourceAsync(source);
                RefreshIptvSourcesList();
            }
            finally
            {
                btnRefresh.IsEnabled = true;
            }
        });
        actionsPanel.Children.Add(btnRefresh);

        // Редактировать
        var btnEdit = CreateIconButton("\uF3DD", "Редактировать", () =>
        {
            if (_iptvStore == null || _iptvPlaylistService == null) return;
            var dlg = new IptvPlaylistDialog(source, _iptvStore, _iptvPlaylistService) { Owner = this };
            if (dlg.ShowDialog() == true)
            {
                int idx = _iptvDraft.Sources.FindIndex(s => s.Id == source.Id);
                if (idx >= 0)
                {
                    _iptvDraft.Sources[idx] = dlg.ResultSource;
                    if (!string.IsNullOrWhiteSpace(dlg.PendingSecretLocation))
                    {
                        _pendingIptvSecrets[dlg.ResultSource.Id] = dlg.PendingSecretLocation;
                    }
                    RefreshIptvSourcesList();
                }
            }
        });
        actionsPanel.Children.Add(btnEdit);

        // Удалить
        var btnDelete = CreateIconButton("\uF34C", "Удалить", () =>
        {
            var confirm = System.Windows.MessageBox.Show(this,
                $"Удалить плейлист \"{source.Name}\"?",
                "Подтверждение удаления",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm == MessageBoxResult.Yes)
            {
                _iptvDraft.Sources.RemoveAll(s => s.Id == source.Id);
                _deletedIptvSourceIds.Add(source.Id);
                _pendingIptvSecrets.Remove(source.Id);
                RefreshIptvSourcesList();
            }
        });
        actionsPanel.Children.Add(btnDelete);

        Grid.SetColumn(actionsPanel, 2);
        row.Children.Add(actionsPanel);

        return row;
    }

    private Button CreateIconButton(string glyph, string tooltip, Action onClick)
    {
        var btn = new Button
        {
            Content = glyph,
            FontFamily = (FontFamily)FindResource("FluentSystemIcons"),
            FontSize = 14,
            Width = 28,
            Height = 28,
            Margin = new Thickness(2, 0, 2, 0),
            Style = (Style)FindResource("IconButtonStyle"),
            ToolTip = tooltip
        };
        btn.Click += (_, _) => onClick();
        return btn;
    }

    private void MoveIptvSource(int index, int delta)
    {
        int newIndex = index + delta;
        if (newIndex < 0 || newIndex >= _iptvDraft.Sources.Count)
        {
            return;
        }

        var item = _iptvDraft.Sources[index];
        _iptvDraft.Sources.RemoveAt(index);
        _iptvDraft.Sources.Insert(newIndex, item);

        for (int i = 0; i < _iptvDraft.Sources.Count; i++)
        {
            _iptvDraft.Sources[i].SortOrder = i;
        }

        RefreshIptvSourcesList();
    }

    private void BtnAddIptvPlaylist_Click(object sender, RoutedEventArgs e)
    {
        if (_iptvStore == null || _iptvPlaylistService == null)
        {
            return;
        }

        var dlg = new IptvPlaylistDialog(null, _iptvStore, _iptvPlaylistService) { Owner = this };
        if (dlg.ShowDialog() == true)
        {
            var newSource = dlg.ResultSource;
            newSource.SortOrder = _iptvDraft.Sources.Count;
            _iptvDraft.Sources.Add(newSource);

            if (!string.IsNullOrWhiteSpace(dlg.PendingSecretLocation))
            {
                _pendingIptvSecrets[newSource.Id] = dlg.PendingSecretLocation;
            }

            RefreshIptvSourcesList();
        }
    }

    private async void BtnRefreshAllIptv_Click(object sender, RoutedEventArgs e)
    {
        if (_iptvPlaylistService == null)
        {
            return;
        }

        BtnRefreshAllIptv.IsEnabled = false;
        try
        {
            int updated = await _iptvPlaylistService.RefreshAllUrlSourcesAsync(_iptvDraft.Sources);
            RefreshIptvSourcesList();
            System.Windows.MessageBox.Show(this,
                $"Обновлено плейлистов: {updated}",
                "Обновление плейлистов",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(this,
                $"Ошибка при обновлении: {ex.Message}",
                "Ошибка",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            BtnRefreshAllIptv.IsEnabled = true;
        }
    }

    private void CommitIptvDraftChanges()
    {
        _iptvDraft.AutoUpdateEnabled = ChkIptvAutoUpdate.IsChecked == true;
        if (int.TryParse(TxtIptvUpdateInterval.Text.Trim(), out int interval) && interval >= 1 && interval <= 720)
        {
            _iptvDraft.UpdateIntervalHours = interval;
        }
        else
        {
            _iptvDraft.UpdateIntervalHours = 24;
        }

        if (_iptvStore != null)
        {
            // Сохраняем ожидающие секреты
            foreach (var (sourceId, secret) in _pendingIptvSecrets)
            {
                _iptvStore.SaveSecretLocation(sourceId, secret);
            }

            // Удаляем данные удаленных источников
            foreach (string deletedId in _deletedIptvSourceIds)
            {
                _iptvStore.DeleteSecret(deletedId);
                _iptvStore.DeleteCache(deletedId);
                _iptvStore.DeleteSourceFile(deletedId);
            }
        }

        _pendingIptvSecrets.Clear();
        _deletedIptvSourceIds.Clear();

        _settings.Iptv = _iptvDraft.Clone();
    }

    private void CleanupPendingIptvState()
    {
        _pendingIptvSecrets.Clear();
        _deletedIptvSourceIds.Clear();
        _iptvPlaylistService?.Dispose();
        _iptvPlaylistService = null;
    }
}
