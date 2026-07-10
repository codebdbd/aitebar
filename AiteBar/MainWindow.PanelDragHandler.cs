

namespace AiteBar;

public partial class MainWindow
{
    private static int FindScreenIndex(Screen targetScreen)
    {
        var screens = Screen.AllScreens;
        return PanelPositionHelper.FindScreenIndex(
            screens.Select(screen => screen.DeviceName).ToArray(),
            targetScreen.DeviceName);
    }

    private void SetDragHandleActive(bool isActive)
    {
        DragHandleGrip.Background = isActive
            ? (Brush)_brushConverter.ConvertFromString("#64C7FF")!
            : (Brush)_brushConverter.ConvertFromString("#2A9CFF")!;
    }

    private void SetPanelDragRenderingActive(bool isActive)
    {
        if (isActive)
        {
            BeginAnimation(LeftProperty, null);
            BeginAnimation(TopProperty, null);
            _isAnimating = false;
            RootBorder.CacheMode ??= new BitmapCache();
            return;
        }

        RootBorder.CacheMode = null;
    }

    private void DragHandle_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _isPanelDragging = true;
        _panelDragChanged = false;
        _dragStartEdge = AppSettings.Edge;
        _dragStartMonitorIndex = AppSettings.MonitorIndex;
        SetPanelDragRenderingActive(true);
        DragHandle.CaptureMouse();
        SetDragHandleActive(true);
        e.Handled = true;
    }

    private async Task EndPanelDragAsync()
    {
        if (!_isPanelDragging)
        {
            return;
        }

        _isPanelDragging = false;
        SetDragHandleActive(false);
        SetPanelDragRenderingActive(false);

        if (_panelDragChanged)
        {
            await SaveSettingsWithNotificationAsync();
        }
        else
        {
            // Reset settings via service
            _settingsService.UpdateSettings(s =>
            {
                s.Edge = _dragStartEdge;
                s.MonitorIndex = _dragStartMonitorIndex;
            });

            UpdateOrientation();
        }
    }

    private void DragHandle_LostMouseCapture(object sender, MouseEventArgs e)
    {
        _ = EndPanelDragAsync();
    }

    private void DragHandle_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_isPanelDragging || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        NativeMethods.Win32Point pt = new();
        if (!NativeMethods.GetCursorPos(ref pt))
        {
            return;
        }

        var targetScreen = Screen.FromPoint(new System.Drawing.Point(pt.X, pt.Y));
        int nextMonitorIndex = FindScreenIndex(targetScreen);
        DockEdge nextEdge = PanelPositionHelper.GetClosestDockEdge(targetScreen.WorkingArea, pt.X, pt.Y, AppSettings.Edge);

        if (AppSettings.MonitorIndex == nextMonitorIndex && AppSettings.Edge == nextEdge)
        {
            return;
        }

        // Update settings via service
            _settingsService.UpdateSettings(s =>
            {
                s.MonitorIndex = nextMonitorIndex;
                s.Edge = nextEdge;
            });

            _panelDragChanged = true;
            UpdateOrientation();
            PositionWindowImmediately(shown: true);
            _positionIndicatorService.Refresh();
    }

    private void DragHandle_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        DragHandle.ReleaseMouseCapture();
        e.Handled = true;
    }
}
