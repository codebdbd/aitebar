using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
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
    private readonly HashSet<string> _changedIptvSourceIds = new(StringComparer.OrdinalIgnoreCase);
    private readonly CancellationTokenSource _iptvOperations = new();
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

        SelectIptvRefreshSource();
        IptvSourcesList.Children.Clear();
        var sources = _iptvDraft.Sources.OrderBy(s => s.SortOrder).ToList();
        TxtIptvSourcesEmpty.Visibility = sources.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        for (int i = 0; i < sources.Count; i++)
        {
            var source = sources[i];
            source.SortOrder = i;
            var row = BuildIptvSourceRow(source, i, sources.Count);
            IptvSourcesList.Children.Add(row);
        }
    }

    private readonly Dictionary<string, string> _iptvChecks = new(StringComparer.Ordinal);
    private IptvPlaylistSource? _selectedIptvSource;

    private Grid BuildIptvSourceRow(IptvPlaylistSource source, int index, int totalCount)
    {
        string details = string.Join(" · ", new[] { source.Kind.ToString(),
            _iptvChecks.GetValueOrDefault(source.Id) ?? (_changedIptvSourceIds.Contains(source.Id)
                ? Locale("IptvSettings_PendingSave")
                : source.LastStatus == "Error" ? Locale("SettingsLists_CheckFailed")
                : source.ChannelCount.ToString(System.Globalization.CultureInfo.CurrentCulture)) });
        Grid row = BuildSettingsListRow(source.Name, details, source.IsEnabled, enabled => source.IsEnabled = enabled,
            index, totalCount, offset => MoveIptvSource(index, offset), async () =>
            {
                if (_iptvPlaylistService == null) return;
                var result = await _iptvPlaylistService.CheckSourceAsync(source, _pendingIptvSecrets.GetValueOrDefault(source.Id), _iptvOperations.Token);
                if (_iptvOperations.IsCancellationRequested) return;
                _iptvChecks[source.Id] = result.IsSuccess ? $"{Locale("SettingsLists_Checked")} · {result.TotalCount}" : Locale("SettingsLists_CheckFailed");
                RefreshIptvSourcesList();
            }, () => EditIptvSource(source), () =>
            {
                _iptvDraft.Sources.Remove(source);
                bool changed = _changedIptvSourceIds.Remove(source.Id);
                _deletedIptvSourceIds.Add(source.Id);
                RegisterListUndo(SettingsListKind.Iptv, () =>
                {
                    _deletedIptvSourceIds.Remove(source.Id);
                    if (changed) _changedIptvSourceIds.Add(source.Id);
                    _iptvDraft.Sources.Insert(Math.Min(index, _iptvDraft.Sources.Count), source);
                    SetIptvSortOrder();
                    RefreshIptvSourcesList();
                });
                if (_selectedIptvSource == source) _selectedIptvSource = null;
                SetIptvSortOrder();
                RefreshIptvSourcesList();
            });
        row.Background = System.Windows.Media.Brushes.Transparent;
        row.Focusable = true;
        row.PreviewMouseLeftButtonDown += (_, _) => SelectSource();
        row.GotKeyboardFocus += (_, _) => SelectSource();
        if (_selectedIptvSource == source) ((Border)row.Children[0]).SetResourceReference(Border.BorderBrushProperty, "AccentColor");
        return row;

        void SelectSource()
        {
            _selectedIptvSource = source;
            foreach (Grid item in IptvSourcesList.Children.OfType<Grid>())
            {
                var surface = (Border)item.Children[0];
                if (item == row) surface.SetResourceReference(Border.BorderBrushProperty, "AccentColor");
                else surface.ClearValue(Border.BorderBrushProperty);
            }
            SelectIptvRefreshSource();
        }
    }

    private void EditIptvSource(IptvPlaylistSource source)
    {
        if (_iptvStore == null || _iptvPlaylistService == null) return;
        var dialog = new IptvPlaylistDialog(source, _iptvStore, _iptvPlaylistService,
            _pendingIptvSecrets.GetValueOrDefault(source.Id)) { Owner = this };
        if (dialog.ShowDialog() != true) return;
        _iptvDraft.Sources[_iptvDraft.Sources.IndexOf(source)] = dialog.ResultSource;
        _changedIptvSourceIds.Add(source.Id);
        _iptvChecks.Remove(source.Id);
        _pendingIptvSecrets.Remove(source.Id);
        if (!string.IsNullOrWhiteSpace(dialog.PendingSecretLocation))
        {
            _pendingIptvSecrets[dialog.ResultSource.Id] = dialog.PendingSecretLocation;
        }
        if (_selectedIptvSource == source) _selectedIptvSource = dialog.ResultSource;
        RefreshIptvSourcesList();
    }

    private void SelectIptvRefreshSource() =>
        BtnRefreshSelectedIptv.IsEnabled = _selectedIptvSource is { } source && !_changedIptvSourceIds.Contains(source.Id);

    private async void BtnRefreshSelectedIptv_Click(object sender, RoutedEventArgs e)
    {
        if (_iptvPlaylistService == null || _selectedIptvSource is not { } source) return;
        BtnRefreshSelectedIptv.IsEnabled = false;
        try
        {
            bool success = await _iptvPlaylistService.RefreshSourceAsync(source, _iptvOperations.Token);
            if (_iptvOperations.IsCancellationRequested) return;
            _iptvChecks[source.Id] = Locale(success ? "SettingsLists_Checked" : "SettingsLists_CheckFailed");
            RefreshIptvSourcesList();
        }
        catch (OperationCanceledException) when (_iptvOperations.IsCancellationRequested) { }
        catch (Exception) { ShowListMessage("SettingsLists_CheckFailed"); }
        finally { SelectIptvRefreshSource(); }
    }

    private void SetIptvSortOrder()
    {
        for (int index = 0; index < _iptvDraft.Sources.Count; index++) _iptvDraft.Sources[index].SortOrder = index;
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
            _changedIptvSourceIds.Add(newSource.Id);

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
            int updated = 0;
            var sources = _iptvDraft.Sources.Where(source => source.IsEnabled && !_changedIptvSourceIds.Contains(source.Id)).ToList();
            foreach (var source in sources)
            {
                if (_iptvOperations.IsCancellationRequested) return;
                if (await _iptvPlaylistService.RefreshSourceAsync(source, _iptvOperations.Token)) updated++;
            }
            if (_iptvOperations.IsCancellationRequested) return;
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

    private (IptvSettings Settings, HashSet<string> ToRefresh, List<IptvPlaylistSource> Retired) CommitIptvDraftChanges(
        IptvSettings currentIptv)
    {
        IptvSettings draft = _iptvDraft.Clone();
        draft.AutoUpdateEnabled = ChkIptvAutoUpdate.IsChecked == true;
        if (int.TryParse(TxtIptvUpdateInterval.Text.Trim(), out int interval) && interval >= 1 && interval <= 720)
        {
            draft.UpdateIntervalHours = interval;
        }
        else
        {
            draft.UpdateIntervalHours = 24;
        }

        var retired = new List<IptvPlaylistSource>();
        if (_iptvStore != null)
        {
            var original = currentIptv.Sources.ToDictionary(source => source.Id, StringComparer.OrdinalIgnoreCase);
            foreach (var source in draft.Sources.Where(source => _changedIptvSourceIds.Contains(source.Id)))
            {
                original.TryGetValue(source.Id, out var previous);
                source.ContentRevision = Guid.NewGuid().ToString("N");
                if (_pendingIptvSecrets.TryGetValue(source.Id, out string? secret))
                {
                    _iptvStore.SaveSecretLocation(source.SourceStorageId, secret);
                }

                string? sourceContent = source.InlineContent;
                if (source.Kind == IptvSourceKind.LocalFile && previous?.Kind == IptvSourceKind.LocalFile &&
                    previous.Location == source.Location && source.CopyLocalFileToStore)
                {
                    sourceContent = _iptvStore.ReadSourceFileContent(previous.SourceStorageId);
                }

                if (sourceContent is not null)
                {
                    _iptvStore.SaveSourceFileContent(source.SourceStorageId, sourceContent);
                }
                source.InlineContent = null;
                source.LastRefreshedUtc = null;
                source.LastStatus = null;
                source.LastErrorMessage = null;
                source.ChannelCount = 0;
                if (previous is not null) retired.Add(previous);
            }

            retired.AddRange(currentIptv.Sources.Where(source => _deletedIptvSourceIds.Contains(source.Id)));
        }

        draft.LastAutoUpdateUtc = currentIptv.LastAutoUpdateUtc;
        var toRefresh = draft.Sources.Where(source => source.IsEnabled &&
            (_changedIptvSourceIds.Contains(source.Id) || source.LastRefreshedUtc is null))
            .Select(source => source.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return (draft, toRefresh, retired);
    }

    private void RemoveRetiredIptvSources(List<IptvPlaylistSource> retired)
    {
        if (_iptvStore is null) return;
        foreach (var source in retired)
        {
            _iptvStore.DeleteSecret(source.SourceStorageId);
            _iptvStore.DeleteSourceFile(source.SourceStorageId);
            if (_deletedIptvSourceIds.Contains(source.Id)) _iptvStore.DeleteCache(source.Id);
        }
    }

    private void CleanupPendingIptvState()
    {
        _pendingIptvSecrets.Clear();
        _deletedIptvSourceIds.Clear();
        _iptvOperations.Cancel();
        _iptvPlaylistService?.Dispose();
        _iptvPlaylistService = null;
    }
}
