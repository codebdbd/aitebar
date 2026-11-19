using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace AiteBar.IptvPlayer;

public partial class IptvPlayerWindow
{
    private readonly DispatcherTimer _videoControlsTimer;
    private readonly DispatcherTimer _videoClickTimer;
    private FrameworkElement? _pressedSurface;
    private Point _videoPressOrigin;
    private bool _canPauseStream;
    private bool _updatingVolume;

    private void Surface_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var surface = (FrameworkElement)sender;
        e.Handled = true;
        surface.Focus();
        RevealVideoControls();

        if (ResolvePendingVideoClick(e.ClickCount)) return;

        if (surface.CaptureMouse())
        {
            _pressedSurface = surface;
            _videoPressOrigin = e.GetPosition(surface);
        }
    }

    private bool ResolvePendingVideoClick(int clickCount)
    {
        if (!_videoClickTimer.IsEnabled) return false;

        _videoClickTimer.Stop();
        if (clickCount == 2)
        {
            ChangeViewMode(_viewModes.ToggleFullscreen);
            return true;
        }

        // Windows classifies a second click elsewhere as a separate click.
        TogglePlayback();
        return false;
    }

    private void Surface_MouseMove(object sender, MouseEventArgs e)
    {
        RevealVideoControls();
        if (!ReferenceEquals(sender, _pressedSurface) || e.LeftButton != MouseButtonState.Pressed)
            return;

        var surface = (FrameworkElement)sender;
        Vector offset = e.GetPosition(surface) - _videoPressOrigin;
        if (Math.Abs(offset.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(offset.Y) < SystemParameters.MinimumVerticalDragDistance)
            return;

        CancelSurfacePress();
        if (_viewModes.Current == IptvViewMode.PictureInPicture)
        {
            Activate();
            Keyboard.Focus(this);
            DragMove();
        }
    }

    private void Surface_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!ReferenceEquals(sender, _pressedSurface)) return;

        var surface = (FrameworkElement)sender;
        e.Handled = true;
        CancelSurfacePress();
        if (!new Rect(surface.RenderSize).Contains(e.GetPosition(surface))) return;

        CompleteVideoClick();
    }

    private void CompleteVideoClick()
    {
        if (_viewModes.Current == IptvViewMode.PictureInPicture)
            TogglePlayback();
        else
            _videoClickTimer.Start();
    }

    private void Surface_LostMouseCapture(object sender, MouseEventArgs e)
    {
        if (ReferenceEquals(sender, _pressedSurface))
            _pressedSurface = null;
    }

    private void CancelSurfacePress()
    {
        var surface = _pressedSurface;
        _pressedSurface = null;
        surface?.ReleaseMouseCapture();
    }

    private void VideoClickTimer_Tick(object? sender, EventArgs e)
    {
        _videoClickTimer.Stop();
        TogglePlayback();
    }

    private void RevealVideoControls()
    {
        if (!VideoSurface.IsVisible) return;

        VideoControls.Visibility = Visibility.Visible;
        _videoControlsTimer.Stop();
        _videoControlsTimer.Start();
    }

    private void VideoControls_FocusChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (VideoControls.IsVisible) RevealVideoControls();
    }

    private void VideoControls_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        Dispatcher.BeginInvoke(() =>
        {
            if (VideoSurface.IsVisible && VideoControls.IsKeyboardFocusWithin)
                VideoSurface.Focus();
        }, DispatcherPriority.Input);
    }

    private void VideoControlsTimer_Tick(object? sender, EventArgs e)
    {
        if (_playbackService?.State == IptvPlaybackState.Paused ||
            VideoControls.IsMouseOver || VideoControls.IsKeyboardFocusWithin)
            return;

        _videoControlsTimer.Stop();
        VideoControls.Visibility = Visibility.Collapsed;
    }

    private void TogglePlayback()
    {
        if (_playbackService is not { } playback) return;
        if (playback.State == IptvPlaybackState.Paused ||
            (playback.State == IptvPlaybackState.Playing && _canPauseStream))
            playback.TogglePlayPause();
    }

    private void UpdatePlaybackControls()
    {
        bool paused = _playbackService?.State == IptvPlaybackState.Paused;
        bool enabled = paused || (_playbackService?.State == IptvPlaybackState.Playing && _canPauseStream);
        BtnPause.IsEnabled = VideoPause.IsEnabled = enabled;
        BtnPause.Content = VideoPause.Content = paused ? "\uF606" : "\uF5A2";
        string pauseLabel = LocalizationService.Get(paused ? "Iptv_Resume" :
            enabled ? "Iptv_Pause" : "Iptv_PauseUnavailable");
        SetButtonDescription(BtnPause, pauseLabel);
        SetButtonDescription(VideoPause, pauseLabel);

        bool fullscreen = _viewModes.Current == IptvViewMode.Fullscreen;
        BtnFullscreen.Content = VideoFullscreen.Content = fullscreen ? "\uE687" : "\uE684";
        string fullscreenLabel = LocalizationService.Get(fullscreen ? "Iptv_ExitFullscreen" : "Iptv_EnterFullscreen");
        SetButtonDescription(BtnFullscreen, fullscreenLabel);
        SetButtonDescription(VideoFullscreen, fullscreenLabel);

        bool muted = Equals(VideoMute.Content, "\uEB4A");
        string soundLabel = LocalizationService.Get(muted ? "Iptv_Unmute" : "Iptv_Mute");
        SetButtonDescription(BtnMute, soundLabel);
        SetButtonDescription(VideoMute, soundLabel);
    }

    private static void SetButtonDescription(FrameworkElement button, string text)
    {
        button.ToolTip = text;
        System.Windows.Automation.AutomationProperties.SetName(button, text);
    }
}
