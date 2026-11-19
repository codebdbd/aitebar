using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Threading;

namespace AiteBar.IptvPlayer;

public partial class IptvPlayerWindow
{
    private readonly DispatcherTimer _streamInfoTimer;
    private IptvPlaybackService.StreamInfo? _streamInfo;
    private bool _readingStreamInfo;

    private void CatalogFavorites_Click(object sender, RoutedEventArgs e) => ApplyFiltersAndRefreshList(_selectedChannel?.Id);

    private void ResolutionChoice_Click(object sender, RoutedEventArgs e) => ApplyFiltersAndRefreshList(_selectedChannel?.Id);

    private IptvResolution GetResolutionFilter()
    {
        var selected = ResolutionChoices.Children.OfType<RadioButton>().FirstOrDefault(choice => choice.IsChecked == true);
        return selected?.Tag is IptvResolution resolution ? resolution : IptvResolution.Any;
    }

    private void RestoreResolutionFilter(IptvResolution resolution)
    {
        var choices = ResolutionChoices.Children.OfType<RadioButton>();
        var selected = choices.FirstOrDefault(choice => Equals(choice.Tag, resolution)) ?? choices.First();
        selected.IsChecked = true;
    }

    private void CloseCatalogFilters()
    {
        BtnFilters.IsChecked = false;
        BtnFilters.Focus();
    }

    private void ClearPlaylistFilter_Click(object sender, RoutedEventArgs e) => CmbPlaylists.SelectedValue = "ALL";
    private void ClearGroupFilter_Click(object sender, RoutedEventArgs e) => CmbGroups.SelectedValue = "ALL";

    private void ClearCatalogFilters_Click(object sender, RoutedEventArgs e)
    {
        _isUpdatingFilters = true;
        try
        {
            CmbPlaylists.SelectedValue = "ALL";
            CmbGroups.SelectedValue = "ALL";
        }
        finally
        {
            _isUpdatingFilters = false;
        }
        ApplyFiltersAndRefreshList(_selectedChannel?.Id);
        BtnFilters.Focus();
    }

    private void UpdateActiveFilterLabels()
    {
        var playlist = CmbPlaylists.SelectedItem as IptvFilterOption;
        var group = CmbGroups.SelectedItem as IptvFilterOption;
        bool hasPlaylist = playlist is not null && playlist.Value != "ALL";
        bool hasGroup = group is not null && group.Value != "ALL";
        BtnClearPlaylist.Visibility = hasPlaylist ? Visibility.Visible : Visibility.Collapsed;
        BtnClearGroup.Visibility = hasGroup ? Visibility.Visible : Visibility.Collapsed;
        var filterVisibility = hasPlaylist || hasGroup ? Visibility.Visible : Visibility.Collapsed;
        ActiveFilters.Visibility = filterVisibility;
        BtnResetFilters.Visibility = filterVisibility;
        FilterIndicator.Visibility = filterVisibility;
        TxtPlaylistFilter.Text = playlist?.Display ?? string.Empty;
        TxtGroupFilter.Text = group?.Display ?? string.Empty;
        BtnClearPlaylist.ToolTip = playlist?.Display;
        BtnClearGroup.ToolTip = group?.Display;
    }

    private void UpdateOnAirChannel()
    {
        var playback = _playbackService;
        bool onAir = playback?.State is IptvPlaybackState.Playing or IptvPlaybackState.Paused ||
            playback is { State: IptvPlaybackState.Buffering, HasVideoOutput: true };
        string? activeId = onAir ? playback?.CurrentChannel?.Id : null;
        var active = activeId is null ? null
            : _fullCatalog.Find(channel => string.Equals(channel.Id, activeId, StringComparison.OrdinalIgnoreCase));
        if (ReferenceEquals(active, _onAirChannel)) return;

        _onAirChannel?.IsOnAir = false;
        _onAirChannel = active;
        if (active is not null) active.IsOnAir = true;
    }

    private void BtnRetryStream_Click(object sender, RoutedEventArgs e)
    {
        if (_playbackService is { State: IptvPlaybackState.Error } playback)
            PlayChannel(playback.CurrentChannel);
    }

    private void UpdateStreamInfoPresentation()
    {
        bool hasVideo = _playbackService?.HasVideoOutput == true;
        var state = _playbackService?.State;
        BtnRetryStream.Visibility = state == IptvPlaybackState.Error && _viewModes.Current != IptvViewMode.PictureInPicture
            ? Visibility.Visible : Visibility.Collapsed;
        bool buffering = hasVideo && state == IptvPlaybackState.Buffering;
        TxtPlaybackStatus.Visibility = buffering ? Visibility.Visible : Visibility.Collapsed;
        if (buffering) TxtPlaybackStatus.Text = LocalizationService.Get("Iptv_BufferingStream");
        if (!hasVideo)
        {
            _streamInfoTimer.Stop();
            _streamInfo = null;
        }
        else if (!_streamInfoTimer.IsEnabled)
        {
            _streamInfoTimer.Start();
        }

        bool showInfo = !buffering && _streamInfo is { } info && _playbackService?.IsCurrentStreamInfo(info) == true;
        BtnStreamInfo.Visibility = showInfo ? Visibility.Visible : Visibility.Collapsed;
        TxtStreamFormat.Text = showInfo ? FormatStreamInfo(_streamInfo!) : string.Empty;
    }

    private async void StreamInfoTimer_Tick(object? sender, EventArgs e)
    {
        if (_isClosing || _readingStreamInfo || !TransportBar.IsVisible ||
            _playbackService is not { HasVideoOutput: true } playback) return;

        _readingStreamInfo = true;
        try
        {
            var info = await playback.ReadStreamInfoAsync();
            if (_isClosing) return;
            if (info is not null && playback.IsCurrentStreamInfo(info))
                _streamInfo = info;
            UpdateStreamInfoPresentation();
        }
        catch (Exception ex)
        {
            _streamInfoTimer.Stop();
            Logger.Log(ex);
        }
        finally
        {
            _readingStreamInfo = false;
        }
    }

    private static string FormatStreamInfo(IptvPlaybackService.StreamInfo info)
    {
        string size = $"{info.Width}×{info.Height}";
        return info.FramesPerSecond is double fps
            ? $"{size} · {LocalizationService.Format("Iptv_FramesPerSecond", fps)}" : size;
    }

    private void BtnStreamInfo_Click(object sender, RoutedEventArgs e)
    {
        if (_streamInfo is not { } info || _playbackService?.IsCurrentStreamInfo(info) != true) return;
        var details = new List<string> { FormatStreamInfo(info) };
        if (!string.IsNullOrWhiteSpace(info.Codec))
            details.Add(LocalizationService.Format("Iptv_VideoCodec", info.Codec));
        if (info.Bitrate > 0)
            details.Add(LocalizationService.Format("Iptv_VideoBitrate", info.Bitrate / 1000.0));
        if (info.DroppedFrames is int dropped)
            details.Add(LocalizationService.Format("Iptv_DroppedFrames", dropped));
        new DarkDialog(string.Join(Environment.NewLine, details))
        {
            Owner = this,
            Title = LocalizationService.Get("Iptv_StreamInfo")
        }.ShowDialog();
    }
}
