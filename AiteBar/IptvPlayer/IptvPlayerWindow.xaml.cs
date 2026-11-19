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
using ButtonBase = System.Windows.Controls.Primitives.ButtonBase;

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
    private List<IptvPlaylistSource> _playlistSources;
    private IptvChannel? _selectedChannel;
    private IptvChannel? _displayedChannel;
    private IptvChannel? _onAirChannel;
    private IptvChannel? _pendingTune;

    private readonly DispatcherTimer _searchDebounceTimer;
    private readonly DispatcherTimer _channelTuneTimer;
    private IptvChannel? _queuedChannel;
    private bool _isUpdatingVariants;
    private bool _isInitializing = true;
    private bool _isUpdatingFilters;
    private bool _catalogReloadPending;
    private int _catalogRequest;
    private bool _isClosing;
    private GridLength _prevSidebarWidth = new(280);

    public IptvPlayerWindow(AppSettingsService? settingsService, MainWindow? mainWindow)
    {
        _settingsService = settingsService;
        _mainWindow = mainWindow;
        _playlistSources = IptvCatalogService.GetEnabledSources(settingsService?.Settings.Iptv ?? new IptvSettings());
        _store = new IptvPlayerStore();
        _catalogService = new IptvCatalogService(_store);

        IptvLogoCache.Initialize(_store.CacheDirectory);

        InitializeComponent();
        _videoControlsTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2.5) };
        _videoControlsTimer.Tick += VideoControlsTimer_Tick;
        _videoClickTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(System.Windows.Forms.SystemInformation.DoubleClickTime)
        };
        _videoClickTimer.Tick += VideoClickTimer_Tick;
        _channelTuneTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(180) };
        _channelTuneTimer.Tick += ChannelTuneTimer_Tick;
        _streamInfoTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(750) };
        _streamInfoTimer.Tick += StreamInfoTimer_Tick;

        _searchDebounceTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(150)
        };
        _searchDebounceTimer.Tick += SearchDebounceTimer_Tick;
        UpdatePlaybackControls();

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
            _playbackService.VideoOutputChanged += OnVideoOutputChanged;
            ApplyVideoCrop();

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
            if (_pendingTune is { } requested)
            {
                _pendingTune = null;
                PlayChannel(requested);
            }
        }
        catch (Exception ex)
        {
            Logger.Log(ex);
            if (!_isClosing)
            {
                BtnPlayStop.IsEnabled = false;
                _playbackEngineUnavailable = true;
                _pendingTune = null;
                UpdatePipOverlay();
                TxtStatusMessage.Text = LocalizationService.Get("Iptv_EngineUnavailable");
            }
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
        try
        {
            var state = await Task.Run(() => _store.LoadPlayerState());
            if (_isClosing) return;

            SldVolume.Value = state.LastVolume;
            BtnFillVideo.IsChecked = state.FillVideo;

            if (state.IsMuted)
            {
                BtnMute.Content = "\uEB4A";
            }

            _pipPlacement = state.PictureInPicturePlacement;
            if (!state.IsSidebarOpen && _viewModes.Windowed == IptvViewMode.Standard)
            {
                ChangeViewMode(_viewModes.ToggleVideoOnly);
            }

            TxtSearch.Text = state.SearchQuery ?? string.Empty;
            await ReloadCatalogAsync(state);
        }
        catch (Exception ex)
        {
            Logger.Log(ex);
            PnlLoadingChannels.Visibility = Visibility.Collapsed;
            TxtNoChannels.Visibility = Visibility.Visible;
        }
        finally
        {
            _isInitializing = false;
            if (_catalogReloadPending && !_isClosing)
            {
                _catalogReloadPending = false;
                await ReloadCatalogAsync();
            }
        }
    }

    private async Task ReloadCatalogAsync(IptvPlayerState? initialState = null)
    {
        int request = ++_catalogRequest;
        try
        {
            var settings = _settingsService?.Settings.Iptv ?? new IptvSettings();
            var sources = IptvCatalogService.GetEnabledSources(settings);
            var (catalog, groupOptions) = await Task.Run(() =>
            {
                var cat = _catalogService.BuildCatalog(settings, includeTestStreams: false);
                var groups = new List<IptvFilterOption>
                {
                    new(LocalizationService.Get("Iptv_AllGroups"), "ALL")
                };
                groups.AddRange(IptvCatalogService.GetGroupNames(cat)
                    .Select(group => new IptvFilterOption(group, group)));
                return (cat, groups);
            });

            if (_isClosing || request != _catalogRequest) return;

            var previousIds = _playlistSources.Select(source => source.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var addedSource = sources.LastOrDefault(source => !previousIds.Contains(source.Id));
            string? targetPlaylist = addedSource?.Id ?? initialState?.SelectedPlaylistFilter ?? CmbPlaylists.SelectedValue as string;
            string? targetGroup = initialState?.SelectedGroupFilter ?? CmbGroups.SelectedValue as string;
            string? targetChannel = initialState?.LastSelectedChannelKey ?? _selectedChannel?.Id;
            _playlistSources = sources;
            _fullCatalog = catalog;

            var playlistOptions = new List<IptvFilterOption> { new(LocalizationService.Get("Iptv_AllPlaylists"), "ALL") };
            playlistOptions.AddRange(sources.Select(source => new IptvFilterOption(
                string.IsNullOrWhiteSpace(source.Name) ? "Плейлист" : source.Name, source.Id)));

            _isUpdatingFilters = true;
            try
            {
                CmbPlaylists.ItemsSource = playlistOptions;
                CmbPlaylists.SelectedValue = targetPlaylist ?? "ALL";
                if (CmbPlaylists.SelectedIndex < 0) CmbPlaylists.SelectedIndex = 0;

                CmbGroups.ItemsSource = groupOptions;
                if (initialState is not null)
                {
                    bool legacyFavorites = targetGroup == IptvCatalogService.FavoritesGroupKey;
                    BtnFavorites.IsChecked = initialState.ShowFavorites || legacyFavorites;
                    RestoreResolutionFilter(initialState.ResolutionFilter);
                    if (legacyFavorites) targetGroup = null;
                }
                CmbGroups.SelectedValue = addedSource is null ? targetGroup ?? "ALL" : "ALL";
                if (CmbGroups.SelectedIndex < 0) CmbGroups.SelectedIndex = 0;
                if (addedSource is not null)
                {
                    BtnFavorites.IsChecked = false;
                    RestoreResolutionFilter(IptvResolution.Any);
                    TxtSearch.Clear();
                    _searchDebounceTimer.Stop();
                }
            }
            finally
            {
                _isUpdatingFilters = false;
            }

            ApplyFiltersAndRefreshList(targetChannel);
            UpdateOnAirChannel();
        }
        catch (Exception ex)
        {
            Logger.Log(ex);
        }
        finally
        {
            if (request == _catalogRequest)
                PnlLoadingChannels.Visibility = Visibility.Collapsed;
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

        bool favoritesOnly = BtnFavorites.IsChecked == true;
        BtnFavorites.Content = favoritesOnly ? "★" : "☆";
        var filtered = IptvCatalogService.FilterChannels(_fullCatalog, plFilter, grpFilter, query,
            favoritesOnly, GetResolutionFilter()).ToList();
        bool changed = !_filteredCatalog.SequenceEqual(filtered);
        if (changed)
        {
            _filteredCatalog = filtered;
            LstChannels.ItemsSource = _filteredCatalog;
        }
        if (_queuedChannel is { } queued && !_filteredCatalog.Contains(queued))
            CancelQueuedTune();
        UpdateActiveFilterLabels();

        if (TxtNoChannels != null)
        {
            TxtNoChannels.Visibility = _filteredCatalog.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            if (_filteredCatalog.Count == 0)
            {
                TxtNoChannels.Text = LocalizationService.Get(favoritesOnly && !_fullCatalog.Exists(channel => channel.IsFavorite)
                    ? "Iptv_NoFavorites" : GetEmptyCatalogMessage(plFilter));
            }
        }

        if (_filteredCatalog.Count > 0)
        {
            int index = -1;
            if (!string.IsNullOrWhiteSpace(preferredChannelKey))
            {
                index = _filteredCatalog.FindIndex(c => string.Equals(c.Id, preferredChannelKey, StringComparison.OrdinalIgnoreCase));
            }

            if (index >= 0 && LstChannels.SelectedIndex != index)
            {
                LstChannels.SelectedIndex = index;
                LstChannels.ScrollIntoView(_filteredCatalog[index]);
            }
            else if (index < 0 && (changed || LstChannels.SelectedIndex < 0))
            {
                LstChannels.SelectedIndex = 0;
            }
        }
        UpdatePipOverlay();
    }

    private string GetEmptyCatalogMessage(string? playlistId)
    {
        if (_playlistSources.Count == 0) return "Iptv_AddPlaylistInSettings";

        if (playlistId is null)
        {
            if (_fullCatalog.Count > 0) return "Iptv_NoMatchingChannels";
            return _playlistSources.TrueForAll(source => source.LastStatus == "Error")
                ? "Iptv_PlaylistLoadFailed" : "Iptv_PlaylistNotLoaded";
        }

        if (_fullCatalog.Exists(channel => string.Equals(channel.PlaylistId, playlistId, StringComparison.OrdinalIgnoreCase)))
            return "Iptv_NoMatchingChannels";

        var selectedSource = _playlistSources.Find(source => string.Equals(source.Id, playlistId, StringComparison.OrdinalIgnoreCase));
        return selectedSource?.LastStatus == "Error" ? "Iptv_PlaylistLoadFailed" : "Iptv_PlaylistNotLoaded";
    }

    private void LstChannels_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _selectedChannel = LstChannels.SelectedItem as IptvChannel;
        UpdateChannelDetails();
    }

    private void LstChannels_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not DependencyObject origin ||
            ItemsControl.ContainerFromElement(LstChannels, origin) is not ListBoxItem { Content: IptvChannel channel } row)
        {
            return;
        }

        for (DependencyObject? element = origin; element != null && element != row;
            element = element is Visual ? VisualTreeHelper.GetParent(element) : LogicalTreeHelper.GetParent(element))
        {
            if (element is ButtonBase) return;
        }

        LstChannels.SelectedItem = channel;
        PlayChannel(channel);
    }

    private static bool IsPlaybackActive(IptvPlaybackState state) => state is
        IptvPlaybackState.Playing or IptvPlaybackState.Paused or IptvPlaybackState.Connecting or
        IptvPlaybackState.Buffering or IptvPlaybackState.Reconnecting;

    private void UpdateChannelDetails()
    {
        IptvChannel? channel = _playbackService is { } playback &&
            (IsPlaybackActive(playback.State) || playback.State == IptvPlaybackState.Error)
            ? playback.CurrentChannel
            : _selectedChannel;

        _isUpdatingVariants = true;
        try
        {
            if (!ReferenceEquals(_displayedChannel, channel))
            {
                _displayedChannel = channel;
                TxtActiveChannel.Text = channel?.Title ?? string.Empty;
                CmbVariants.Items.Clear();
                if (channel?.Variants.Count > 1)
                {
                    for (int i = 0; i < channel.Variants.Count; i++)
                    {
                        string title = channel.Variants[i].Title;
                        bool distinct = !string.IsNullOrWhiteSpace(title) && !string.Equals(title, channel.Title, StringComparison.OrdinalIgnoreCase) &&
                            channel.Variants.Count(variant => string.Equals(variant.Title, title, StringComparison.OrdinalIgnoreCase)) == 1;
                        CmbVariants.Items.Add(distinct ? title : LocalizationService.Format("Iptv_StreamNumber", i + 1));
                    }
                }
                UpdateVariantVisibility();
            }

            if (channel is not null && CmbVariants.Items.Count > 1)
            {
                CmbVariants.SelectedIndex = Math.Clamp(channel.ActiveVariantIndex, 0, CmbVariants.Items.Count - 1);
            }
        }
        finally
        {
            _isUpdatingVariants = false;
        }
    }

    private void CmbVariants_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingVariants || _displayedChannel is not { } channel || CmbVariants.SelectedIndex < 0) return;

        int index = CmbVariants.SelectedIndex;
        if (_playbackService is { } playback && IsPlaybackActive(playback.State) &&
            ReferenceEquals(playback.CurrentChannel, channel))
        {
            playback.SelectVariant(index);
        }
        else
        {
            channel.ActiveVariantIndex = index;
        }
    }

    private bool PlayChannel(IptvChannel? channel)
    {
        if (channel is null || _isClosing || _playbackEngineUnavailable) return false;
        _videoClickTimer.Stop();
        CancelQueuedTune();
        if (_playbackService is not { } playback)
        {
            _pendingTune = channel;
            UpdatePipOverlay();
            return true;
        }

        if (playback.CurrentChannel?.Id == channel.Id && IsPlaybackActive(playback.State))
        {
            if (playback.State == IptvPlaybackState.Paused) playback.TogglePlayPause();
        }
        else
        {
            playback.PlayChannel(channel);
            UpdateChannelDetails();
        }
        UpdatePipOverlay();
        return true;
    }

    private void BtnPlayStop_Click(object sender, RoutedEventArgs e)
    {
        _videoClickTimer.Stop();
        CancelQueuedTune();
        if (_playbackService is not { } playback)
        {
            PlayChannel(_selectedChannel);
            return;
        }

        if (playback.State == IptvPlaybackState.Paused)
            playback.TogglePlayPause();
        else if (IsPlaybackActive(playback.State))
            playback.Stop();
        else
            PlayChannel(_selectedChannel);
    }

    private void BtnPause_Click(object sender, RoutedEventArgs e)
    {
        _videoClickTimer.Stop();
        TogglePlayback();
    }

    private void BtnMute_Click(object sender, RoutedEventArgs e)
    {
        _playbackService?.ToggleMute();
    }

    private void SldVolume_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_updatingVolume) return;
        int vol = (int)Math.Round(e.NewValue);
        _playbackService?.SetVolume(vol);
    }

    private void SetSidebarVisible(bool visible)
    {
        if (visible)
        {
            ChannelsSidebar.Visibility = Visibility.Visible;
            ColSidebar.MinWidth = 230;
            ColSidebar.Width = _prevSidebarWidth;
            Splitter.Visibility = Visibility.Visible;
        }
        else
        {
            if (ChannelsSidebar.IsKeyboardFocusWithin || FocusManager.GetFocusedElement(this) is TextBox or ComboBox)
                BtnPictureInPicture.Focus();
            ChannelsSidebar.Visibility = Visibility.Collapsed;
            if (ColSidebar.Width.Value > 0) _prevSidebarWidth = ColSidebar.Width;
            ColSidebar.MinWidth = 0;
            ColSidebar.Width = new GridLength(0);
            Splitter.Visibility = Visibility.Collapsed;
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

            if (BtnFavorites.IsChecked == true)
            {
                ApplyFiltersAndRefreshList(_selectedChannel?.Id);
            }
        }
    }

    private void IptvPlayerWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && _viewModes.Current is IptvViewMode.PictureInPicture or IptvViewMode.Fullscreen)
        {
            ChangeViewMode(_viewModes.Current == IptvViewMode.Fullscreen
                ? _viewModes.ToggleFullscreen : _viewModes.ReturnToWindowed);
            e.Handled = true;
            return;
        }
        if (e.Key == Key.Escape && BtnFilters.IsChecked == true && ChannelsSidebar.IsVisible)
        {
            if (CmbGroups.IsDropDownOpen)
                CmbGroups.IsDropDownOpen = false;
            else if (CmbPlaylists.IsDropDownOpen)
                CmbPlaylists.IsDropDownOpen = false;
            else
                CloseCatalogFilters();
            e.Handled = true;
            return;
        }
        if (e.Key == Key.Tab && _viewModes.Current == IptvViewMode.PictureInPicture)
        {
            _pipKeyboardNavigation = true;
            UpdatePipOverlay();
            if (ReferenceEquals(sender, this) && PipVideoOverlay.IsVisible)
            {
                FocusPipControl(PipVideoOverlay, (Keyboard.Modifiers & ModifierKeys.Shift) != 0
                    ? "NextChannelButton" : "ReturnButton");
                e.Handled = true;
                return;
            }
        }
        if (Keyboard.FocusedElement is TextBox or ComboBox ||
            CmbGroups.IsDropDownOpen || CmbPlaylists.IsDropDownOpen || CmbVariants.IsDropDownOpen)
        {
            return;
        }

        if (Keyboard.Modifiers != ModifierKeys.None) return;
        RevealVideoControls();

        switch (e.Key)
        {
            case Key.PageDown:
                _videoClickTimer.Stop();
                NavigateChannel(1);
                e.Handled = true;
                break;

            case Key.PageUp:
                _videoClickTimer.Stop();
                NavigateChannel(-1);
                e.Handled = true;
                break;

            case Key.F:
                if (!e.IsRepeat) ChangeViewMode(_viewModes.ToggleFullscreen);
                e.Handled = true;
                break;

            case Key.P:
                if (!e.IsRepeat) ChangeViewMode(_viewModes.TogglePictureInPicture);
                e.Handled = true;
                break;

            case Key.V when _viewModes.Current is IptvViewMode.Standard or IptvViewMode.VideoOnly:
                if (!e.IsRepeat) ChangeViewMode(_viewModes.ToggleVideoOnly);
                e.Handled = true;
                break;

            case Key.M:
                if (!e.IsRepeat) _playbackService?.ToggleMute();
                e.Handled = true;
                break;

            case Key.Return:
                if (!e.IsRepeat && Keyboard.FocusedElement is not ButtonBase)
                    e.Handled = PlayChannel(_selectedChannel);
                break;

            case Key.Space:
                if (Keyboard.FocusedElement is ButtonBase) break;
                _videoClickTimer.Stop();
                if (_playbackService is { } playback && IsPlaybackActive(playback.State))
                {
                    if (!e.IsRepeat) TogglePlayback();
                    e.Handled = true;
                }
                else if (!e.IsRepeat)
                {
                    e.Handled = PlayChannel(_selectedChannel);
                }
                break;
        }
    }

    private void ChannelTuneTimer_Tick(object? sender, EventArgs e)
    {
        var channel = _queuedChannel;
        CancelQueuedTune();
        PlayChannel(channel);
    }

    private void CancelQueuedTune()
    {
        _channelTuneTimer.Stop();
        _queuedChannel = null;
    }

    private void NavigateChannel(int delta)
    {
        int count = LstChannels.Items.Count;
        if (count == 0 || _playbackEngineUnavailable) return;

        int next = LstChannels.SelectedIndex + delta;
        if (next < 0) next = count - 1;
        if (next >= count) next = 0;

        if (LstChannels.Items[next] is IptvChannel channel)
        {
            LstChannels.SelectedIndex = next;
            LstChannels.ScrollIntoView(channel);
            _queuedChannel = channel;
            _channelTuneTimer.Stop();
            _channelTuneTimer.Start();
            UpdatePipOverlay();
        }
    }

    private void OnVideoOutputChanged()
    {
        if (Dispatcher.HasShutdownStarted || _isClosing) return;
        Dispatcher.BeginInvoke(() =>
        {
            if (!_isClosing)
            {
                UpdateVideoPresentation();
            }
        });
    }

    private void UpdateVideoPresentation()
    {
        bool hasVideo = _playbackService?.HasVideoOutput == true;
        bool pip = _viewModes.Current == IptvViewMode.PictureInPicture;
        bool showVideoSurface = hasVideo && !pip;
        bool wasVideoSurfaceVisible = VideoSurface.Visibility == Visibility.Visible;
        var oldOverlay = hasVideo ? PipIdleOverlay : PipVideoOverlay;
        string? focusedControl = _pipKeyboardNavigation && oldOverlay.IsKeyboardFocusWithin &&
            Keyboard.FocusedElement is FrameworkElement element
            ? element.Name : null;
        VlcVideoView.Visibility = hasVideo ? Visibility.Visible : Visibility.Hidden;
        TxtStatusMessage.Visibility = hasVideo || pip ? Visibility.Collapsed : Visibility.Visible;
        PipIdleOverlay.Visibility = pip && !hasVideo ? Visibility.Visible : Visibility.Collapsed;
        PipVideoOverlay.Visibility = pip && hasVideo ? Visibility.Visible : Visibility.Collapsed;
        VideoSurface.Visibility = showVideoSurface ? Visibility.Visible : Visibility.Collapsed;
        TransportBar.Visibility = pip || (_viewModes.Current == IptvViewMode.Fullscreen && hasVideo)
            ? Visibility.Collapsed : Visibility.Visible;
        if (!showVideoSurface)
        {
            _videoControlsTimer.Stop();
            _videoClickTimer.Stop();
            VideoControls.Visibility = Visibility.Collapsed;
        }
        else if (!wasVideoSurfaceVisible)
        {
            RevealVideoControls();
        }
        UpdateOnAirChannel();
        UpdateStreamInfoPresentation();
        if (pip && focusedControl is not null)
        {
            var overlay = hasVideo ? PipVideoOverlay : PipIdleOverlay;
            Dispatcher.BeginInvoke(() =>
            {
                if (overlay.IsVisible) FocusPipControl(overlay, focusedControl);
            }, DispatcherPriority.Input);
        }
    }

    private void BtnFillVideo_Changed(object sender, RoutedEventArgs e)
    {
        if (VideoViewport is null) return;
        VideoViewport.Fill = _viewModes.Current == IptvViewMode.PictureInPicture || BtnFillVideo.IsChecked == true;
        ApplyVideoCrop();
    }

    private void VideoViewport_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (VideoViewport.Fill) ApplyVideoCrop();
    }

    private void ApplyVideoCrop()
    {
        _playbackService?.SetVideoCrop(IptvVideoViewport.GetCropGeometry(VideoViewport.Fill,
            VideoViewport.ActualWidth, VideoViewport.ActualHeight));
    }

    private void OnPlaybackStateChanged(IptvPlaybackState state, string? message)
    {
        if (Dispatcher.HasShutdownStarted || _isClosing) return;

        Dispatcher.BeginInvoke(() =>
        {
            if (_isClosing || _playbackService?.State != state) return;
            UpdateChannelDetails();
            UpdateOnAirChannel();
            UpdateStreamInfoPresentation();
            UpdatePipOverlay();
            UpdatePlaybackControls();
            if (state is IptvPlaybackState.Playing or IptvPlaybackState.Paused)
                RevealVideoControls();
            BtnPlayStop.Content = IsPlaybackActive(state) && state != IptvPlaybackState.Paused ? "\uF72B" : "\uF606";
            TxtStatusMessage.Text = LocalizationService.Get(state switch
            {
                IptvPlaybackState.Connecting => "Iptv_OpeningStream",
                IptvPlaybackState.Buffering => "Iptv_BufferingStream",
                IptvPlaybackState.Reconnecting => "Iptv_ReconnectingStream",
                IptvPlaybackState.Error => "Iptv_StreamUnavailable",
                IptvPlaybackState.Stopped when _selectedChannel is null => "Iptv_SelectChannel",
                IptvPlaybackState.Stopped => "Iptv_StreamStopped",
                IptvPlaybackState.Playing or IptvPlaybackState.Paused when _playbackService.IsAudioOnly => "Iptv_AudioOnly",
                _ => "Iptv_WaitingForVideo"
            });
        });
    }

    private void OnPlaybackVolumeChanged(int vol)
    {
        if (Dispatcher.HasShutdownStarted || _isClosing) return;

        Dispatcher.BeginInvoke((Action)(() =>
        {
            _updatingVolume = true;
            try
            {
                if (Math.Abs(SldVolume.Value - vol) > 0.5) SldVolume.Value = vol;
                if (Math.Abs(VideoVolume.Value - vol) > 0.5) VideoVolume.Value = vol;
            }
            finally
            {
                _updatingVolume = false;
            }
        }));
    }

    private void OnPlaybackMutedChanged(bool muted)
    {
        if (Dispatcher.HasShutdownStarted || _isClosing) return;

        Dispatcher.BeginInvoke((Action)(() =>
        {
            BtnMute.Content = VideoMute.Content = muted ? "\uEB4A" : "\uEB42";
            UpdatePlaybackControls();
        }));
    }

    private void OnPlaybackCanPauseChanged(bool canPause)
    {
        if (Dispatcher.HasShutdownStarted || _isClosing) return;

        Dispatcher.BeginInvoke((Action)(() =>
        {
            _canPauseStream = canPause;
            UpdatePlaybackControls();
        }));
    }

    private void OnSettingsChanged(object? sender, EventArgs e)
    {
        if (Dispatcher.HasShutdownStarted || _isClosing) return;

        Dispatcher.BeginInvoke((Action)(async () =>
        {
            if (_isClosing) return;
            if (_isInitializing)
            {
                _catalogReloadPending = true;
                return;
            }
            await ReloadCatalogAsync();
        }));
    }

    protected override void OnClosed(EventArgs e)
    {
        _isClosing = true;
        _searchDebounceTimer.Stop();
        _videoControlsTimer.Stop();
        _videoClickTimer.Stop();
        CancelSurfacePress();
        _streamInfoTimer.Stop();
        CancelQueuedTune();

        if (_playbackService != null)
        {
            _playbackService.StateChanged -= OnPlaybackStateChanged;
            _playbackService.VolumeChanged -= OnPlaybackVolumeChanged;
            _playbackService.MutedChanged -= OnPlaybackMutedChanged;
            _playbackService.CanPauseChanged -= OnPlaybackCanPauseChanged;
            _playbackService.VideoOutputChanged -= OnVideoOutputChanged;
        }

        if (_settingsService != null)
        {
            _settingsService.SettingsChanged -= OnSettingsChanged;

            try
            {
                WindowPlacement placement = _windowedPlacement ?? CaptureWindowPlacement();
                _settingsService.UpdateSettings(s =>
                {
                    s.IptvPlayerWidth = placement.Bounds.Width;
                    s.IptvPlayerHeight = placement.Bounds.Height;
                    s.IptvPlayerLeft = placement.Bounds.Left;
                    s.IptvPlayerTop = placement.Bounds.Top;
                    s.IptvPlayerWindowState = placement.State.ToString();
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
                IsSidebarOpen = _viewModes.Windowed == IptvViewMode.Standard,
                FillVideo = BtnFillVideo.IsChecked == true,
                PictureInPicturePlacement = _pipPlacement,
                IsTestStreamsEnabled = false,
                SelectedPlaylistFilter = CmbPlaylists?.SelectedValue as string,
                SelectedGroupFilter = CmbGroups?.SelectedValue as string,
                ShowFavorites = BtnFavorites.IsChecked == true,
                ResolutionFilter = GetResolutionFilter(),
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
