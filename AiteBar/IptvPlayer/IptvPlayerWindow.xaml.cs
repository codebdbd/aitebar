using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace AiteBar.IptvPlayer;

[SupportedOSPlatform("windows6.1")]
public partial class IptvPlayerWindow : DarkWindow
{
    private readonly AppSettingsService? _settingsService;
    private readonly MainWindow? _mainWindow;
    private readonly IptvPlayerStore _store;
    private readonly IptvCatalogService _catalogService;
    private IptvPlaybackService? _playbackService;

    private List<IptvChannel> _fullCatalog = [];
    private List<IptvChannel> _filteredCatalog = [];
    private IptvChannel? _selectedChannel;

    private readonly DispatcherTimer _searchDebounceTimer;
    private readonly DispatcherTimer _channelSwitchDebounceTimer;
    private bool _isFullscreen;
    private bool _isInitializing = true;
    private bool _isUpdatingFilters;
    private bool _isClosing;
    private WindowState _prevWindowState = WindowState.Normal;
    private GridLength _prevSidebarWidth = new(360);

    public IptvPlayerWindow(AppSettingsService? settingsService, MainWindow? mainWindow)
    {
        _settingsService = settingsService;
        _mainWindow = mainWindow;
        _store = new IptvPlayerStore();
        _catalogService = new IptvCatalogService(_store);

        IptvLogoCache.Initialize(_store.CacheDirectory);

        InitializeComponent();

        _searchDebounceTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(150)
        };
        _searchDebounceTimer.Tick += SearchDebounceTimer_Tick;

        _channelSwitchDebounceTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(350)
        };
        _channelSwitchDebounceTimer.Tick += ChannelSwitchDebounceTimer_Tick;

        Loaded += IptvPlayerWindow_Loaded;
        PreviewKeyDown += IptvPlayerWindow_PreviewKeyDown;

        if (_settingsService != null)
        {
            _settingsService.SettingsChanged += OnSettingsChanged;
        }
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        RestoreWindowPlacement();
    }

    private async void IptvPlayerWindow_Loaded(object sender, RoutedEventArgs e)
    {
        EnsureVideoViewBlackBackground();

        if (PnlLoadingChannels != null)
        {
            PnlLoadingChannels.Visibility = Visibility.Visible;
        }
        if (TxtNoChannels != null)
        {
            TxtNoChannels.Visibility = Visibility.Collapsed;
        }

        var initPlaybackTask = InitializePlaybackAsync();
        var loadCatalogTask = LoadPlayerStateAndCatalogAsync();

        await Task.WhenAll(initPlaybackTask, loadCatalogTask);
    }

    private async Task InitializePlaybackAsync()
    {
        try
        {
            var playback = await Task.Run(() => new IptvPlaybackService());

            if (_isClosing)
            {
                _ = Task.Run(() => playback.Dispose());
                return;
            }

            _playbackService = playback;
            _playbackService.StateChanged += OnPlaybackStateChanged;
            _playbackService.VolumeChanged += OnPlaybackVolumeChanged;
            _playbackService.MutedChanged += OnPlaybackMutedChanged;
            _playbackService.CanPauseChanged += OnPlaybackCanPauseChanged;

            try
            {
                if (VlcVideoView != null)
                {
                    VlcVideoView.MediaPlayer = _playbackService.MediaPlayer;
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }

            var state = _store.LoadPlayerState();
            _playbackService.SetVolume(state.LastVolume);
            if (state.IsMuted)
            {
                _playbackService.ToggleMute();
            }
        }
        catch (Exception ex)
        {
            Logger.Log(ex);
        }
    }

    private void EnsureVideoViewBlackBackground()
    {
        try
        {
            VlcVideoView.Background = Brushes.Black;
            if (VideoContainer != null)
            {
                VideoContainer.Background = Brushes.Black;
            }
        }
        catch (Exception ex)
        {
            Logger.Log(ex);
        }
    }

    private void RestoreWindowPlacement()
    {
        if (_settingsService == null) return;
        var s = _settingsService.Settings;

        if (s.IptvPlayerWidth.HasValue && s.IptvPlayerWidth.Value >= MinWidth)
        {
            Width = Math.Min(s.IptvPlayerWidth.Value, SystemParameters.VirtualScreenWidth);
        }

        if (s.IptvPlayerHeight.HasValue && s.IptvPlayerHeight.Value >= MinHeight)
        {
            Height = Math.Min(s.IptvPlayerHeight.Value, SystemParameters.VirtualScreenHeight);
        }

        if (s.IptvPlayerLeft.HasValue && s.IptvPlayerTop.HasValue)
        {
            double left = s.IptvPlayerLeft.Value;
            double top = s.IptvPlayerTop.Value;

            double maxLeft = SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth - 100;
            double maxTop = SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight - 100;

            if (left >= SystemParameters.VirtualScreenLeft && left <= maxLeft &&
                top >= SystemParameters.VirtualScreenTop && top <= maxTop)
            {
                WindowStartupLocation = WindowStartupLocation.Manual;
                Left = left;
                Top = top;
            }
        }

        if (Enum.TryParse(s.IptvPlayerWindowState, out WindowState ws) && ws != WindowState.Minimized)
        {
            WindowState = ws;
        }
    }

    private async Task LoadPlayerStateAndCatalogAsync()
    {
        _isInitializing = true;
        try
        {
            var state = await Task.Run(() => _store.LoadPlayerState());

            SldVolume.Value = state.LastVolume;
            TxtVolume.Text = $"{state.LastVolume}%";

            if (state.IsMuted)
            {
                BtnMute.Content = "\uEB4A";
            }

            if (!state.IsSidebarOpen)
            {
                ColSidebar.Width = new GridLength(0);
                Splitter.Visibility = Visibility.Collapsed;
            }

            TxtSearch.Text = state.SearchQuery ?? string.Empty;

            var settings = _settingsService?.Settings.Iptv ?? new IptvSettings();

            var (catalog, playlistOptions, groupOptions, filtered) = await Task.Run(() =>
            {
                var cat = _catalogService.BuildCatalog(settings, includeTestStreams: false);

                var plOptions = new List<IptvFilterOption>
                {
                    new("Все плейлисты", "ALL")
                };
                var distinctPlaylists = cat
                    .Select(c => new { c.PlaylistId, c.PlaylistName })
                    .DistinctBy(p => p.PlaylistId)
                    .ToList();
                foreach (var p in distinctPlaylists)
                {
                    plOptions.Add(new IptvFilterOption(
                        string.IsNullOrWhiteSpace(p.PlaylistName) ? "Плейлист" : p.PlaylistName,
                        p.PlaylistId));
                }

                var grpOptions = new List<IptvFilterOption>
                {
                    new("Все группы", "ALL"),
                    new("★ Избранное", IptvCatalogService.FavoritesGroupKey)
                };
                var distinctGroups = cat
                    .Select(c => c.Group)
                    .Where(g => !string.IsNullOrWhiteSpace(g))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(g => g)
                    .ToList();
                foreach (var grp in distinctGroups)
                {
                    grpOptions.Add(new IptvFilterOption(grp, grp));
                }

                string? plFilter = state.SelectedPlaylistFilter;
                if (string.Equals(plFilter, "ALL", StringComparison.OrdinalIgnoreCase)) plFilter = null;
                string? grpFilter = state.SelectedGroupFilter;
                if (string.Equals(grpFilter, "ALL", StringComparison.OrdinalIgnoreCase)) grpFilter = null;

                var filteredList = _catalogService.FilterCatalog(
                    cat,
                    plFilter,
                    grpFilter,
                    state.SearchQuery?.Trim());

                return (cat, plOptions, grpOptions, filteredList);
            });

            if (_isClosing) return;

            _fullCatalog = catalog;
            _filteredCatalog = filtered;

            _isUpdatingFilters = true;
            try
            {
                CmbPlaylists.ItemsSource = playlistOptions;
                CmbPlaylists.SelectedValue = state.SelectedPlaylistFilter ?? "ALL";
                if (CmbPlaylists.SelectedIndex < 0) CmbPlaylists.SelectedIndex = 0;

                CmbGroups.ItemsSource = groupOptions;
                CmbGroups.SelectedValue = state.SelectedGroupFilter ?? "ALL";
                if (CmbGroups.SelectedIndex < 0) CmbGroups.SelectedIndex = 0;
            }
            finally
            {
                _isUpdatingFilters = false;
            }

            LstChannels.ItemsSource = _filteredCatalog;
            if (PnlLoadingChannels != null) PnlLoadingChannels.Visibility = Visibility.Collapsed;
            if (TxtNoChannels != null) TxtNoChannels.Visibility = _filteredCatalog.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

            if (_filteredCatalog.Count > 0)
            {
                int index = -1;
                if (!string.IsNullOrWhiteSpace(state.LastSelectedChannelKey))
                {
                    index = _filteredCatalog.FindIndex(c => string.Equals(c.Id, state.LastSelectedChannelKey, StringComparison.OrdinalIgnoreCase));
                }

                if (index >= 0)
                {
                    LstChannels.SelectedIndex = index;
                    LstChannels.ScrollIntoView(_filteredCatalog[index]);
                }
                else
                {
                    LstChannels.SelectedIndex = 0;
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Log(ex);
            if (PnlLoadingChannels != null) PnlLoadingChannels.Visibility = Visibility.Collapsed;
            if (TxtNoChannels != null) TxtNoChannels.Visibility = Visibility.Visible;
        }
        finally
        {
            _isInitializing = false;
        }
    }

    private async void ReloadCatalog(
        string? targetChannelKey = null,
        string? targetPlaylist = null,
        string? targetGroup = null)
    {
        if (PnlLoadingChannels != null) PnlLoadingChannels.Visibility = Visibility.Visible;
        try
        {
            var settings = _settingsService?.Settings.Iptv ?? new IptvSettings();
            var (catalog, playlistOptions, groupOptions) = await Task.Run(() =>
            {
                var cat = _catalogService.BuildCatalog(settings, includeTestStreams: false);

                var plOptions = new List<IptvFilterOption>
                {
                    new("Все плейлисты", "ALL")
                };
                var distinctPlaylists = cat
                    .Select(c => new { c.PlaylistId, c.PlaylistName })
                    .DistinctBy(p => p.PlaylistId)
                    .ToList();
                foreach (var p in distinctPlaylists)
                {
                    plOptions.Add(new IptvFilterOption(
                        string.IsNullOrWhiteSpace(p.PlaylistName) ? "Плейлист" : p.PlaylistName,
                        p.PlaylistId));
                }

                var grpOptions = new List<IptvFilterOption>
                {
                    new("Все группы", "ALL"),
                    new("★ Избранное", IptvCatalogService.FavoritesGroupKey)
                };
                var distinctGroups = cat
                    .Select(c => c.Group)
                    .Where(g => !string.IsNullOrWhiteSpace(g))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(g => g)
                    .ToList();
                foreach (var grp in distinctGroups)
                {
                    grpOptions.Add(new IptvFilterOption(grp, grp));
                }

                return (cat, plOptions, grpOptions);
            });

            if (_isClosing) return;

            _fullCatalog = catalog;

            _isUpdatingFilters = true;
            try
            {
                CmbPlaylists.ItemsSource = playlistOptions;
                CmbPlaylists.SelectedValue = targetPlaylist ?? "ALL";
                if (CmbPlaylists.SelectedIndex < 0) CmbPlaylists.SelectedIndex = 0;

                CmbGroups.ItemsSource = groupOptions;
                CmbGroups.SelectedValue = targetGroup ?? "ALL";
                if (CmbGroups.SelectedIndex < 0) CmbGroups.SelectedIndex = 0;
            }
            finally
            {
                _isUpdatingFilters = false;
            }

            ApplyFiltersAndRefreshList(targetChannelKey);
        }
        catch (Exception ex)
        {
            Logger.Log(ex);
        }
        finally
        {
            if (PnlLoadingChannels != null) PnlLoadingChannels.Visibility = Visibility.Collapsed;
        }
    }

    private void ApplyFiltersAndRefreshList(string? preferredChannelKey = null)
    {
        string? plFilter = CmbPlaylists.SelectedValue as string;
        if (string.Equals(plFilter, "ALL", StringComparison.OrdinalIgnoreCase))
        {
            plFilter = null;
        }

        string? grpFilter = CmbGroups.SelectedValue as string;
        if (string.Equals(grpFilter, "ALL", StringComparison.OrdinalIgnoreCase))
        {
            grpFilter = null;
        }

        string query = TxtSearch.Text.Trim();

        _filteredCatalog = _catalogService.FilterCatalog(_fullCatalog, plFilter, grpFilter, query);
        LstChannels.ItemsSource = _filteredCatalog;

        if (TxtNoChannels != null)
        {
            TxtNoChannels.Visibility = _filteredCatalog.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        if (_filteredCatalog.Count > 0)
        {
            int index = -1;
            if (!string.IsNullOrWhiteSpace(preferredChannelKey))
            {
                index = _filteredCatalog.FindIndex(c => string.Equals(c.Id, preferredChannelKey, StringComparison.OrdinalIgnoreCase));
            }

            if (index >= 0)
            {
                LstChannels.SelectedIndex = index;
                LstChannels.ScrollIntoView(_filteredCatalog[index]);
            }
            else
            {
                LstChannels.SelectedIndex = 0;
            }
        }
    }

    private void LstChannels_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (LstChannels.SelectedItem is IptvChannel channel)
        {
            _selectedChannel = channel;
            TxtActiveChannel.Text = channel.Title;
            TxtActiveGroup.Text = $"• {channel.Group} ({channel.PlaylistName})";

            PopulateVariants(channel);

            if (!_isInitializing && _playbackService != null)
            {
                // Задержка 350мс при быстром перелистывании каналов, чтобы не спамить LibVLC
                _channelSwitchDebounceTimer.Stop();
                _channelSwitchDebounceTimer.Start();
            }
        }
    }

    private void ChannelSwitchDebounceTimer_Tick(object? sender, EventArgs e)
    {
        _channelSwitchDebounceTimer.Stop();
        if (_selectedChannel != null && _playbackService != null && !_isClosing)
        {
            _playbackService.PlayChannel(_selectedChannel);
        }
    }

    private void LstChannels_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        _channelSwitchDebounceTimer.Stop();
        if (_selectedChannel != null && _playbackService != null)
        {
            _playbackService.PlayChannel(_selectedChannel);
        }
    }

    private void PopulateVariants(IptvChannel channel)
    {
        if (channel.Variants != null && channel.Variants.Count > 1)
        {
            CmbVariants.Items.Clear();
            for (int i = 0; i < channel.Variants.Count; i++)
            {
                var v = channel.Variants[i];
                CmbVariants.Items.Add(new ComboBoxItem
                {
                    Content = string.IsNullOrWhiteSpace(v.Title) ? $"Variant {i + 1}" : v.Title,
                    Tag = i
                });
            }
            CmbVariants.SelectedIndex = Math.Clamp(channel.ActiveVariantIndex, 0, channel.Variants.Count - 1);
            CmbVariants.Visibility = Visibility.Visible;
        }
        else
        {
            CmbVariants.Visibility = Visibility.Collapsed;
        }
    }

    private void CmbVariants_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_playbackService != null && CmbVariants.SelectedItem is ComboBoxItem item && item.Tag is int idx)
        {
            _playbackService.SelectVariant(idx);
        }
    }

    private void BtnPlayStop_Click(object sender, RoutedEventArgs e)
    {
        if (_playbackService == null) return;

        if (_playbackService.State == IptvPlaybackState.Playing ||
            _playbackService.State == IptvPlaybackState.Buffering ||
            _playbackService.State == IptvPlaybackState.Connecting)
        {
            _playbackService.Stop();
        }
        else
        {
            if (_selectedChannel != null)
            {
                _playbackService.PlayChannel(_selectedChannel);
            }
        }
    }

    private void BtnPause_Click(object sender, RoutedEventArgs e)
    {
        _playbackService?.TogglePlayPause();
    }

    private void BtnMute_Click(object sender, RoutedEventArgs e)
    {
        _playbackService?.ToggleMute();
    }

    private void SldVolume_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (TxtVolume == null)
        {
            return;
        }

        int vol = (int)Math.Round(e.NewValue);
        _playbackService?.SetVolume(vol);
        TxtVolume.Text = $"{vol}%";
    }

    private void BtnFullscreen_Click(object sender, RoutedEventArgs e)
    {
        ToggleFullscreen();
    }

    private void ToggleFullscreen()
    {
        if (!_isFullscreen)
        {
            _prevWindowState = WindowState;
            _prevSidebarWidth = ColSidebar.Width;

            ColSidebar.Width = new GridLength(0);
            Splitter.Visibility = Visibility.Collapsed;

            WindowState = WindowState.Normal;
            WindowStyle = WindowStyle.None;
            WindowState = WindowState.Maximized;

            BtnFullscreen.Content = "\uE687"; // Exit fullscreen glyph
            _isFullscreen = true;
        }
        else
        {
            WindowState = WindowState.Normal;
            WindowStyle = WindowStyle.SingleBorderWindow;
            WindowState = _prevWindowState;

            ColSidebar.Width = _prevSidebarWidth.Value > 0 ? _prevSidebarWidth : new GridLength(360);
            Splitter.Visibility = Visibility.Visible;

            BtnFullscreen.Content = "\uE684"; // Enter fullscreen glyph
            _isFullscreen = false;
        }
    }

    private void BtnToggleSidebar_Click(object sender, RoutedEventArgs e)
    {
        if (ColSidebar.Width.Value > 0)
        {
            _prevSidebarWidth = ColSidebar.Width;
            ColSidebar.Width = new GridLength(0);
            Splitter.Visibility = Visibility.Collapsed;
        }
        else
        {
            ColSidebar.Width = _prevSidebarWidth.Value > 0 ? _prevSidebarWidth : new GridLength(360);
            Splitter.Visibility = Visibility.Visible;
        }
    }

    private void BtnSources_Click(object sender, RoutedEventArgs e)
    {
        _mainWindow?.ShowAppSettingsWindow(AppSettingsSection.Iptv);
    }

    private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (TxtSearch == null || TxtSearchWatermark == null || BtnClearSearch == null) return;
        bool hasText = !string.IsNullOrEmpty(TxtSearch.Text);
        TxtSearchWatermark.Visibility = hasText ? Visibility.Collapsed : Visibility.Visible;
        BtnClearSearch.Visibility = hasText ? Visibility.Visible : Visibility.Collapsed;

        _searchDebounceTimer.Stop();
        _searchDebounceTimer.Start();
    }

    private void SearchDebounceTimer_Tick(object? sender, EventArgs e)
    {
        _searchDebounceTimer.Stop();
        ApplyFiltersAndRefreshList(_selectedChannel?.Id);
    }

    private void BtnClearSearch_Click(object sender, RoutedEventArgs e)
    {
        if (TxtSearch != null)
        {
            TxtSearch.Text = string.Empty;
        }
    }

    private void CmbPlaylists_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (LstChannels == null || _isInitializing || _isUpdatingFilters) return;
        ApplyFiltersAndRefreshList(_selectedChannel?.Id);
    }

    private void CmbGroups_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (LstChannels == null || _isInitializing || _isUpdatingFilters) return;
        ApplyFiltersAndRefreshList(_selectedChannel?.Id);
    }

    private void BtnStar_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement el && el.Tag is IptvChannel ch)
        {
            _catalogService.ToggleFavorite(ch);

            string? grp = CmbGroups.SelectedValue as string;
            if (string.Equals(grp, IptvCatalogService.FavoritesGroupKey, StringComparison.OrdinalIgnoreCase))
            {
                ApplyFiltersAndRefreshList(_selectedChannel?.Id);
            }
        }
    }

    private void IptvPlayerWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (FocusManager.GetFocusedElement(this) is TextBox ||
            FocusManager.GetFocusedElement(this) is ComboBox)
        {
            return;
        }

        switch (e.Key)
        {
            case Key.PageDown:
                NavigateChannel(1);
                e.Handled = true;
                break;

            case Key.PageUp:
                NavigateChannel(-1);
                e.Handled = true;
                break;

            case Key.F:
                ToggleFullscreen();
                e.Handled = true;
                break;

            case Key.Escape:
                if (_isFullscreen)
                {
                    ToggleFullscreen();
                    e.Handled = true;
                }
                break;

            case Key.M:
                _playbackService?.ToggleMute();
                e.Handled = true;
                break;

            case Key.Return:
                _channelSwitchDebounceTimer.Stop();
                if (_selectedChannel != null && _playbackService != null)
                {
                    _playbackService.PlayChannel(_selectedChannel);
                    e.Handled = true;
                }
                break;

            case Key.Space:
                _playbackService?.TogglePlayPause();
                e.Handled = true;
                break;
        }
    }

    private void NavigateChannel(int delta)
    {
        if (_filteredCatalog.Count == 0) return;

        int cur = LstChannels.SelectedIndex;
        int next = cur + delta;

        if (next < 0) next = _filteredCatalog.Count - 1;
        if (next >= _filteredCatalog.Count) next = 0;

        LstChannels.SelectedIndex = next;
        LstChannels.ScrollIntoView(_filteredCatalog[next]);
    }

    private void OnPlaybackStateChanged(IptvPlaybackState state, string? message)
    {
        if (Dispatcher.HasShutdownStarted || _isClosing) return;

        Dispatcher.BeginInvoke((Action)(() =>
        {
            switch (state)
            {
                case IptvPlaybackState.Connecting:
                    BtnPlayStop.Content = "\uF72B"; // Stop icon
                    StatusPanel.Visibility = Visibility.Visible;
                    TxtStatusIcon.Text = "\uF190"; // Sync
                    TxtStatusMessage.Text = message ?? "Подключение...";
                    break;

                case IptvPlaybackState.Buffering:
                    BtnPlayStop.Content = "\uF72B";
                    StatusPanel.Visibility = Visibility.Visible;
                    TxtStatusIcon.Text = "\uF190";
                    TxtStatusMessage.Text = message ?? "Буферизация...";
                    break;

                case IptvPlaybackState.Playing:
                    BtnPlayStop.Content = "\uF72B";
                    StatusPanel.Visibility = Visibility.Collapsed;
                    break;

                case IptvPlaybackState.Paused:
                    BtnPlayStop.Content = "\uF606"; // Play icon
                    StatusPanel.Visibility = Visibility.Collapsed;
                    break;

                case IptvPlaybackState.Stopped:
                    BtnPlayStop.Content = "\uF606";
                    StatusPanel.Visibility = Visibility.Collapsed;
                    break;

                case IptvPlaybackState.Reconnecting:
                    BtnPlayStop.Content = "\uF72B";
                    StatusPanel.Visibility = Visibility.Visible;
                    TxtStatusIcon.Text = "\uF190";
                    TxtStatusMessage.Text = message ?? "Переподключение...";
                    break;

                case IptvPlaybackState.Error:
                    BtnPlayStop.Content = "\uF606";
                    StatusPanel.Visibility = Visibility.Visible;
                    TxtStatusIcon.Text = "\uF3F1"; // Error circle
                    TxtStatusMessage.Text = message ?? "Ошибка воспроизведения";
                    break;
            }
        }));
    }

    private void OnPlaybackVolumeChanged(int vol)
    {
        if (Dispatcher.HasShutdownStarted || _isClosing) return;

        Dispatcher.BeginInvoke((Action)(() =>
        {
            if (SldVolume != null && Math.Abs(SldVolume.Value - vol) > 0.5)
            {
                SldVolume.Value = vol;
            }
            if (TxtVolume != null)
            {
                TxtVolume.Text = $"{vol}%";
            }
        }));
    }

    private void OnPlaybackMutedChanged(bool muted)
    {
        if (Dispatcher.HasShutdownStarted || _isClosing) return;

        Dispatcher.BeginInvoke((Action)(() =>
        {
            if (BtnMute != null)
            {
                BtnMute.Content = muted ? "\uEB4A" : "\uEB42";
            }
        }));
    }

    private void OnPlaybackCanPauseChanged(bool canPause)
    {
        if (Dispatcher.HasShutdownStarted || _isClosing) return;

        Dispatcher.BeginInvoke((Action)(() =>
        {
            if (BtnPause != null)
            {
                BtnPause.IsEnabled = canPause;
            }
        }));
    }

    private void OnSettingsChanged(object? sender, EventArgs e)
    {
        if (Dispatcher.HasShutdownStarted || _isClosing) return;

        Dispatcher.BeginInvoke((Action)(() =>
        {
            ReloadCatalog(_selectedChannel?.Id);
        }));
    }

    protected override void OnClosed(EventArgs e)
    {
        _isClosing = true;
        _searchDebounceTimer.Stop();
        _channelSwitchDebounceTimer.Stop();

        if (_playbackService != null)
        {
            _playbackService.StateChanged -= OnPlaybackStateChanged;
            _playbackService.VolumeChanged -= OnPlaybackVolumeChanged;
            _playbackService.MutedChanged -= OnPlaybackMutedChanged;
            _playbackService.CanPauseChanged -= OnPlaybackCanPauseChanged;
        }

        if (_settingsService != null)
        {
            _settingsService.SettingsChanged -= OnSettingsChanged;

            try
            {
                _settingsService.UpdateSettings(s =>
                {
                    s.IptvPlayerWidth = Width;
                    s.IptvPlayerHeight = Height;
                    s.IptvPlayerLeft = Left;
                    s.IptvPlayerTop = Top;
                    s.IptvPlayerWindowState = WindowState.ToString();
                });
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
        }

        try
        {
            var state = new IptvPlayerState
            {
                LastSelectedChannelKey = _selectedChannel?.Id,
                LastVolume = (int)Math.Round(SldVolume?.Value ?? 80),
                IsMuted = _playbackService?.IsMuted ?? false,
                IsSidebarOpen = ColSidebar?.Width.Value > 0,
                IsTestStreamsEnabled = false,
                SelectedPlaylistFilter = CmbPlaylists?.SelectedValue as string,
                SelectedGroupFilter = CmbGroups?.SelectedValue as string,
                SearchQuery = TxtSearch?.Text ?? string.Empty
            };
            _store.SavePlayerState(state);
        }
        catch (Exception ex)
        {
            Logger.Log(ex);
        }

        try
        {
            if (VlcVideoView != null)
            {
                VlcVideoView.MediaPlayer = null;
            }
        }
        catch (Exception ex)
        {
            Logger.Log(ex);
        }

        var playback = _playbackService;
        if (playback != null)
        {
            Task.Run(() =>
            {
                try
                {
                    playback.Dispose();
                }
                catch (Exception ex)
                {
                    Logger.Log(ex);
                }
            });
        }

        base.OnClosed(e);
    }
}
