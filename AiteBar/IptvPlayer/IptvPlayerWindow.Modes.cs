using System;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Rect = System.Windows.Rect;
using Screen = System.Windows.Forms.Screen;

namespace AiteBar.IptvPlayer;

public partial class IptvPlayerWindow
{
    private sealed record WindowPlacement(Rect Bounds, WindowState State, WindowStyle Style,
        ResizeMode ResizeMode, bool Topmost, double MinWidth, double MinHeight);

    private sealed record PipOverlayState(string ChannelName, string Status, bool HasError, bool CanNavigate, bool ShowForKeyboard);

    private bool _playbackEngineUnavailable;
    private bool _pipKeyboardNavigation;
    private readonly IptvViewModes _viewModes = new();
    private WindowPlacement? _windowedPlacement;
    private IptvPipPlacement? _pipPlacement;

    private void BtnFullscreen_Click(object sender, RoutedEventArgs e) => ChangeViewMode(_viewModes.ToggleFullscreen);
    private void BtnPictureInPicture_Click(object sender, RoutedEventArgs e) => ChangeViewMode(_viewModes.TogglePictureInPicture);
    private void BtnToggleSidebar_Click(object sender, RoutedEventArgs e) => ChangeViewMode(_viewModes.ToggleVideoOnly);

    private void PipReturn_Click(object sender, RoutedEventArgs e) => ChangeViewMode(_viewModes.ReturnToWindowed);

    private void PipPreviousChannel_Click(object sender, RoutedEventArgs e) => NavigateChannel(-1);
    private void PipNextChannel_Click(object sender, RoutedEventArgs e) => NavigateChannel(1);

    private void UpdatePipOverlay()
    {
        if (_viewModes.Current != IptvViewMode.PictureInPicture) return;

        var playback = _playbackService;
        var requested = _queuedChannel ?? _pendingTune;
        IptvPlaybackState playbackState = requested is not null ? IptvPlaybackState.Connecting
            : playback?.State ?? IptvPlaybackState.Stopped;
        var channel = requested ?? (playbackState != IptvPlaybackState.Stopped ? playback?.CurrentChannel : null);
        string statusKey = _playbackEngineUnavailable ? "Iptv_EngineUnavailable" : playbackState switch
        {
            IptvPlaybackState.Connecting => "Iptv_OpeningStream",
            IptvPlaybackState.Buffering => "Iptv_BufferingStream",
            IptvPlaybackState.Reconnecting => "Iptv_ReconnectingStream",
            IptvPlaybackState.Error => "Iptv_StreamUnavailable",
            _ => string.Empty
        };
        var state = new PipOverlayState(channel?.Title ?? LocalizationService.Get("Iptv_NoActiveChannel"),
            statusKey.Length == 0 ? string.Empty : LocalizationService.Get(statusKey),
            _playbackEngineUnavailable || playbackState == IptvPlaybackState.Error,
            !_playbackEngineUnavailable && (LstChannels.Items.Count > 1 ||
                (channel is null && LstChannels.Items.Count == 1)),
            _pipKeyboardNavigation);

        if (Equals(PipIdleOverlay.Content, state)) return;
        PipIdleOverlay.Content = state;
        PipVideoOverlay.Content = state;
    }

    private void FocusPipControl(ContentControl overlay, string name)
    {
        overlay.ApplyTemplate();
        if (overlay.Template.FindName(name, overlay) is System.Windows.Controls.Button button && button.Focus()) return;
        overlay.MoveFocus(new TraversalRequest(FocusNavigationDirection.First));
    }

    protected override void OnLocalizationChanged()
    {
        base.OnLocalizationChanged();
        UpdatePipOverlay();
        UpdatePlaybackControls();
    }

    private void PipOverlay_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        _pipKeyboardNavigation = false;
        UpdatePipOverlay();
    }

    private void ChangeViewMode(Action change)
    {
        IptvViewMode previous = _viewModes.Current;
        change();
        IptvViewMode next = _viewModes.Current;
        if (previous == next) return;

        if (previous == IptvViewMode.PictureInPicture)
        {
            CapturePipPlacement();
            _pipKeyboardNavigation = false;
        }
        _videoClickTimer.Stop();
        CancelSurfacePress();

        bool wasWindowed = previous is IptvViewMode.Standard or IptvViewMode.VideoOnly;
        bool isWindowed = next is IptvViewMode.Standard or IptvViewMode.VideoOnly;
        bool pip = next == IptvViewMode.PictureInPicture;
        if (wasWindowed && !isWindowed) _windowedPlacement = CaptureWindowPlacement();

        SetSidebarVisible(next == IptvViewMode.Standard);
        ColVideo.MinWidth = pip ? 0 : 480;
        VideoViewport.Fill = pip || BtnFillVideo.IsChecked == true;
        ApplyVideoCrop();
        UpdatePipOverlay();
        UpdateVideoPresentation();
        if (isWindowed && _windowedPlacement is { } placement)
        {
            RestorePlacement(placement);
            _windowedPlacement = null;
            if (previous == IptvViewMode.PictureInPicture)
            {
                Activate();
                BtnPictureInPicture.Focus();
            }
        }
        else if (next == IptvViewMode.PictureInPicture)
        {
            EnterPictureInPicture();
        }
        else if (next == IptvViewMode.Fullscreen)
        {
            WindowState = WindowState.Normal;
            Topmost = false;
            WindowStyle = WindowStyle.None;
            WindowState = WindowState.Maximized;
            RevealVideoControls();
        }

        BtnToggleSidebar.Visibility = isWindowed ? Visibility.Visible : Visibility.Collapsed;
        UpdatePlaybackControls();
        UpdateVariantVisibility();
    }

    private void UpdateVariantVisibility() => CmbVariants.Visibility =
        CmbVariants.Items.Count > 1 && _viewModes.Current != IptvViewMode.PictureInPicture
            ? Visibility.Visible : Visibility.Collapsed;

    private WindowPlacement CaptureWindowPlacement()
    {
        Rect bounds = RestoreBounds;
        if (bounds.IsEmpty)
        {
            bounds = new Rect(double.IsFinite(Left) ? Left : 0, double.IsFinite(Top) ? Top : 0, Width, Height);
        }

        return new WindowPlacement(bounds, WindowState == WindowState.Minimized ? WindowState.Normal : WindowState,
            WindowStyle, ResizeMode, Topmost, MinWidth, MinHeight);
    }

    private void RestorePlacement(WindowPlacement placement)
    {
        WindowState = WindowState.Normal;
        WindowStyle = placement.Style;
        ResizeMode = placement.ResizeMode;
        Topmost = placement.Topmost;
        MinWidth = placement.MinWidth;
        MinHeight = placement.MinHeight;
        Left = placement.Bounds.Left;
        Top = placement.Bounds.Top;
        Width = placement.Bounds.Width;
        Height = placement.Bounds.Height;
        WindowState = placement.State;
    }

    private void EnterPictureInPicture()
    {
        IntPtr hwnd = new WindowInteropHelper(this).Handle;
        Screen? savedMonitor = _pipPlacement is { } saved
            ? Screen.AllScreens.FirstOrDefault(screen => screen.DeviceName == saved.Monitor) : null;
        Screen screen = savedMonitor ?? Screen.FromHandle(hwnd);
        double scale = TaskbarGeometryHelper.GetMonitorDpiScale(screen, VisualTreeHelper.GetDpi(this).DpiScaleX);
        var area = screen.WorkingArea;
        Rect placement = IptvPipLayout.Fit(new Rect(area.X, area.Y, area.Width, area.Height), scale,
            savedMonitor is null ? null : _pipPlacement);

        WindowState = WindowState.Normal;
        MinWidth = 0;
        MinHeight = 0;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        Topmost = true;
        Width = placement.Width / scale;
        Height = placement.Height / scale;
        if (hwnd != IntPtr.Zero)
        {
            NativeMethods.SetWindowPos(hwnd, IntPtr.Zero, (int)Math.Round(placement.X), (int)Math.Round(placement.Y),
                (int)Math.Round(placement.Width), (int)Math.Round(placement.Height),
                NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_NOZORDER);
        }
    }

    private void CapturePipPlacement()
    {
        IntPtr hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero || WindowState == WindowState.Minimized ||
            !NativeMethods.GetWindowRect(hwnd, out var bounds)) return;

        Screen screen = Screen.FromHandle(hwnd);
        var area = screen.WorkingArea;
        double scale = TaskbarGeometryHelper.GetMonitorDpiScale(screen, VisualTreeHelper.GetDpi(this).DpiScaleX);
        _pipPlacement = new IptvPipPlacement(screen.DeviceName, (bounds.Left - area.Left) / scale,
            (bounds.Top - area.Top) / scale, (bounds.Right - bounds.Left) / scale, (bounds.Bottom - bounds.Top) / scale);
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (_viewModes.Current == IptvViewMode.PictureInPicture) CapturePipPlacement();
        base.OnClosing(e);
    }
}
