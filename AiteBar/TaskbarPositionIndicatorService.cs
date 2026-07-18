using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using WpfSize = System.Windows.Size;

namespace AiteBar;

[SupportedOSPlatform("windows6.1")]
internal class TaskbarPositionIndicatorService : IDisposable
{
    private TaskbarPositionIndicatorWindow? _window;
    private AppSettingsService? _appSettingsService;
    private MainWindow? _mainWindow;
    private HwndSource? _hwndSource;
    private bool _disposed;
    private const double IndicatorSize = 28;
    private static readonly TimeSpan VisibilityCheckInterval = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan ZOrderRefreshInterval = TimeSpan.FromMilliseconds(250);
    private DispatcherTimer? _visibilityTimer;
    private DispatcherTimer? _zOrderTimer;
    private bool _isSuppressedByFullscreen;
    private bool _isDragging;

    public void Initialize(AppSettingsService appSettingsService, MainWindow mainWindow)
    {
        Debug.WriteLine("TaskbarPositionIndicatorService.Initialize called");
        _appSettingsService = appSettingsService;
        _mainWindow = mainWindow;

        _appSettingsService.SettingsChanged += AppSettingsService_SettingsChanged;

        // Initialize visibility check timer
        _visibilityTimer = new DispatcherTimer
        {
            Interval = VisibilityCheckInterval
        };
        _visibilityTimer.Tick += VisibilityTimer_Tick;
        _visibilityTimer.Start();

        _zOrderTimer = new DispatcherTimer
        {
            Interval = ZOrderRefreshInterval
        };
        _zOrderTimer.Tick += ZOrderTimer_Tick;
        _zOrderTimer.Start();

        var showIndicator = _appSettingsService.Settings.ShowTaskbarPositionIndicator.GetValueOrDefault(true);
        Debug.WriteLine($"ShowTaskbarPositionIndicator: {showIndicator}");
        if (showIndicator)
        {
            ShowIndicator();
        }
    }

    private void AppSettingsService_SettingsChanged(object? sender, EventArgs e)
    {
        MainWindow? mainWindow = _mainWindow;
        if (_disposed || mainWindow == null) return;

        UiDispatcher.Run(mainWindow.Dispatcher, ApplySettingsChanged);
    }

    private void ApplySettingsChanged()
    {
        if (_disposed) return;

        Debug.WriteLine("TaskbarPositionIndicatorService.SettingsChanged");
        if (_appSettingsService?.Settings.ShowTaskbarPositionIndicator.GetValueOrDefault(true) == true)
        {
            ShowIndicator();
            UpdatePosition();
        }
        else
        {
            HideIndicator();
        }
    }

    private void ShowIndicator()
    {
        if (_disposed) return;

        Debug.WriteLine("TaskbarPositionIndicatorService.ShowIndicator");

        try
        {
            if (_window == null)
            {
                Debug.WriteLine("Creating indicator window");
                _window = new TaskbarPositionIndicatorWindow();
                Debug.WriteLine("Calling _window.Initialize");
                _window.Initialize(_appSettingsService!);
                Debug.WriteLine("Subscribing to TogglePanelRequested");
                _window.TogglePanelRequested += Window_TogglePanelRequested;
                Debug.WriteLine("Subscribing to ShowSettingsRequested");
                _window.ShowSettingsRequested += Window_ShowSettingsRequested;
                Debug.WriteLine("Subscribing to HideIndicatorRequested");
                _window.HideIndicatorRequested += Window_HideIndicatorRequested;
                _window.DragRequested += Window_DragRequested;
                _window.DragCompleted += Window_DragCompleted;
                Debug.WriteLine("Subscribing to _window.Loaded");
                _window.Loaded += Window_Loaded;
                // Don't subscribe to Closed, since we won't close the window
            }

            if (!_window.IsVisible)
            {
                Debug.WriteLine("Calling _window.Show()");
                _window.Show();
                _window.Topmost = true;
                Debug.WriteLine("Indicator window shown successfully");
            }
            UpdatePosition();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"ShowIndicator exception: {ex}");
            Logger.Log(ex);
        }
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        Debug.WriteLine("TaskbarPositionIndicatorWindow.Loaded");
        if (_window == null) return;

        var helper = new WindowInteropHelper(_window);
        _hwndSource = HwndSource.FromHwnd(helper.Handle);
        if (_hwndSource != null)
        {
            _hwndSource.AddHook(WndProc);
        }

        UpdatePosition();
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_SETTINGCHANGE || msg == NativeMethods.WM_DPICHANGED)
        {
            UpdatePosition();
        }
        return IntPtr.Zero;
    }

    private void HideIndicator()
    {
        Debug.WriteLine("TaskbarPositionIndicatorService.HideIndicator");
        if (_window == null) return;

        _window.Hide();
    }

    private void BringIndicatorToTop()
    {
        if (_disposed || _window == null || !_window.IsVisible)
        {
            return;
        }

        UpdatePosition();
    }

    public void UpdatePosition()
    {
        if (_disposed) return;

        Debug.WriteLine("TaskbarPositionIndicatorService.UpdatePosition");
        if (_window == null || _mainWindow == null || _appSettingsService == null) return;

        if (_isDragging)
        {
            var draggingHandle = new WindowInteropHelper(_window).Handle;
            NativeMethods.SetWindowPos(
                draggingHandle,
                NativeMethods.HWND_TOPMOST,
                0,
                0,
                0,
                0,
                NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_SHOWWINDOW);
            return;
        }

        try
        {
            var settings = _appSettingsService.Settings;
            var helper = new WindowInteropHelper(_window);
            double dpiScale = GetWindowDpiScale(helper.Handle);
            double physicalIndicatorSize = Math.Max(1, Math.Round(IndicatorSize * dpiScale));
            var indicatorSize = new WpfSize(physicalIndicatorSize, physicalIndicatorSize);
            var monitorBounds = TaskbarGeometryHelper.GetMonitorBounds(settings.MonitorIndex);
            Point position;

            if (settings.TaskbarIndicatorPositionX.HasValue && settings.TaskbarIndicatorPositionY.HasValue)
            {
                position = IndicatorPositionHelper.FromNormalized(
                    monitorBounds,
                    indicatorSize,
                    settings.TaskbarIndicatorPositionX.Value,
                    settings.TaskbarIndicatorPositionY.Value);
            }
            else
            {
                var taskbarInfo = TaskbarGeometryHelper.GetTaskbarInfo(settings.MonitorIndex);
                position = IndicatorPositionHelper.Clamp(
                    monitorBounds,
                    indicatorSize,
                    TaskbarGeometryHelper.CalculateIndicatorPosition(taskbarInfo, physicalIndicatorSize));
                Debug.WriteLine($"Taskbar info: Edge {taskbarInfo.Edge}, Bounds {taskbarInfo.Bounds}");
            }
            Debug.WriteLine($"Position calculated: X {position.X}, Y {position.Y}");

            SetIndicatorPosition(helper.Handle, position, indicatorSize);

            // A move to a monitor with another DPI changes the WPF window DPI. Recalculate once
            // with the destination DPI so its physical bounds still match its 28-DIP content.
            double destinationDpiScale = GetWindowDpiScale(helper.Handle);
            if (Math.Abs(destinationDpiScale - dpiScale) > 0.01)
            {
                physicalIndicatorSize = Math.Max(1, Math.Round(IndicatorSize * destinationDpiScale));
                indicatorSize = new WpfSize(physicalIndicatorSize, physicalIndicatorSize);
                position = settings.TaskbarIndicatorPositionX.HasValue && settings.TaskbarIndicatorPositionY.HasValue
                    ? IndicatorPositionHelper.FromNormalized(
                        monitorBounds,
                        indicatorSize,
                        settings.TaskbarIndicatorPositionX.Value,
                        settings.TaskbarIndicatorPositionY.Value)
                    : IndicatorPositionHelper.Clamp(monitorBounds, indicatorSize, position);
                SetIndicatorPosition(helper.Handle, position, indicatorSize);
            }

            _window.UpdateArrow(settings.Edge);
            _window.UpdateTooltip(GetTooltipText(settings));
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"UpdatePosition exception: {ex}");
            Logger.Log(ex);
        }
    }

    private static double GetWindowDpiScale(IntPtr hwnd)
    {
        try
        {
            uint dpi = NativeMethods.GetDpiForWindow(hwnd);
            return dpi == 0 ? 1.0 : dpi / 96.0;
        }
        catch (EntryPointNotFoundException)
        {
            return 1.0;
        }
    }

    private static void SetIndicatorPosition(IntPtr hwnd, Point position, WpfSize size)
    {
        NativeMethods.SetWindowPos(
            hwnd,
            NativeMethods.HWND_TOPMOST,
            (int)Math.Round(position.X),
            (int)Math.Round(position.Y),
            (int)Math.Round(size.Width),
            (int)Math.Round(size.Height),
            NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_SHOWWINDOW);
    }

    private void Window_DragRequested(object? sender, IndicatorDragEventArgs e)
    {
        _isDragging = true;
        MoveDraggedIndicator(e.Left, e.Top);
    }

    private async void Window_DragCompleted(object? sender, IndicatorDragEventArgs e)
    {
        if (_disposed || _window == null || _appSettingsService == null)
        {
            return;
        }

        try
        {
            Point position = MoveDraggedIndicator(e.Left, e.Top);
            IntPtr hwnd = new WindowInteropHelper(_window).Handle;
            double physicalIndicatorSize = Math.Max(1, Math.Round(IndicatorSize * GetWindowDpiScale(hwnd)));
            var size = new WpfSize(physicalIndicatorSize, physicalIndicatorSize);
            Rect monitorBounds = TaskbarGeometryHelper.GetMonitorBounds(_appSettingsService.Settings.MonitorIndex);
            Point normalized = IndicatorPositionHelper.ToNormalized(monitorBounds, size, position);

            _isDragging = false;
            _appSettingsService.UpdateSettings(settings =>
            {
                settings.TaskbarIndicatorPositionX = normalized.X;
                settings.TaskbarIndicatorPositionY = normalized.Y;
            });
            await _appSettingsService.SaveAsync();
        }
        catch (Exception ex)
        {
            _isDragging = false;
            Logger.Log(ex);
        }
    }

    private Point MoveDraggedIndicator(int requestedLeft, int requestedTop)
    {
        if (_window == null || _appSettingsService == null)
        {
            return new Point(requestedLeft, requestedTop);
        }

        IntPtr hwnd = new WindowInteropHelper(_window).Handle;
        double physicalIndicatorSize = Math.Max(1, Math.Round(IndicatorSize * GetWindowDpiScale(hwnd)));
        var size = new WpfSize(physicalIndicatorSize, physicalIndicatorSize);
        Rect monitorBounds = TaskbarGeometryHelper.GetMonitorBounds(_appSettingsService.Settings.MonitorIndex);
        Point position = IndicatorPositionHelper.Clamp(
            monitorBounds,
            size,
            new Point(requestedLeft, requestedTop));
        SetIndicatorPosition(hwnd, position, size);
        return position;
    }

    public void Refresh()
    {
        if (_disposed) return;

        Debug.WriteLine("TaskbarPositionIndicatorService.Refresh");
        if (_appSettingsService?.Settings.ShowTaskbarPositionIndicator.GetValueOrDefault(true) == true)
        {
            ShowIndicator();
            UpdatePosition();
        }
        else
        {
            HideIndicator();
        }
    }

    public void HandleGlobalMouseDown(int screenX, int screenY)
    {
        if (_disposed || _window == null)
        {
            return;
        }

        _window.Dispatcher.InvokeAsync(() =>
        {
            if (_disposed || _isSuppressedByFullscreen)
            {
                return;
            }

            if (_appSettingsService?.Settings.ShowTaskbarPositionIndicator.GetValueOrDefault(true) != true)
            {
                return;
            }

            if (IsPointOnTargetTaskbar(screenX, screenY))
            {
                RestoreAfterTaskbarClick();
            }
        });
    }

    private bool IsPointOnTargetTaskbar(int screenX, int screenY)
    {
        if (_appSettingsService == null)
        {
            return false;
        }

        var taskbarInfo = TaskbarGeometryHelper.GetTaskbarInfo(_appSettingsService.Settings.MonitorIndex);
        return taskbarInfo.Bounds.Contains(new Point(screenX, screenY));
    }

    private void RestoreAfterTaskbarClick()
    {
        BringIndicatorToTop();
        _ = RestoreAfterTaskbarClickAsync();
    }

    private async Task RestoreAfterTaskbarClickAsync()
    {
        int[] delays = [50, 150, 350];
        foreach (int delay in delays)
        {
            await Task.Delay(delay).ConfigureAwait(false);
            if (_disposed || _window == null)
            {
                return;
            }

            await _window.Dispatcher.InvokeAsync(() =>
            {
                if (!_disposed && !_isSuppressedByFullscreen)
                {
                    BringIndicatorToTop();
                }
            });
        }
    }

    private string GetTooltipText(AppSettings settings)
    {
        var edgeText = settings.Edge switch
        {
            DockEdge.Top => LocalizationService.Get("TaskbarIndicator_EdgeTop"),
            DockEdge.Bottom => LocalizationService.Get("TaskbarIndicator_EdgeBottom"),
            DockEdge.Left => LocalizationService.Get("TaskbarIndicator_EdgeLeft"),
            DockEdge.Right => LocalizationService.Get("TaskbarIndicator_EdgeRight"),
            _ => LocalizationService.Get("TaskbarIndicator_EdgeRight")
        };

        var monitorText = settings.MonitorIndex == 0 
            ? LocalizationService.Get("TaskbarIndicator_MonitorPrimary") 
            : LocalizationService.Format("TaskbarIndicator_MonitorFormat", settings.MonitorIndex + 1);

        return LocalizationService.Format("TaskbarIndicator_TooltipFormat", edgeText, monitorText);
    }

    private void VisibilityTimer_Tick(object? sender, EventArgs e)
    {
        if (_disposed) return;

        Debug.WriteLine("TaskbarPositionIndicatorService.VisibilityTimer_Tick");
        if (_appSettingsService?.Settings.ShowTaskbarPositionIndicator.GetValueOrDefault(true) == true)
        {
            if (IsFullscreenAppRunning())
            {
                _isSuppressedByFullscreen = true;
                HideIndicator();
            }
            else
            {
                _isSuppressedByFullscreen = false;
                ShowIndicator();
            }
        }
        else
        {
            _isSuppressedByFullscreen = false;
            HideIndicator();
        }
    }

    private void ZOrderTimer_Tick(object? sender, EventArgs e)
    {
        if (_disposed || _isSuppressedByFullscreen)
        {
            return;
        }

        if (_appSettingsService?.Settings.ShowTaskbarPositionIndicator.GetValueOrDefault(true) == true)
        {
            BringIndicatorToTop();
        }
    }

    private bool IsFullscreenAppRunning()
    {
        try
        {
            if (_appSettingsService == null || _mainWindow == null)
                return false;

            var settings = _appSettingsService.Settings;
            var targetMonitor = TaskbarGeometryHelper.GetMonitorFromIndex(settings.MonitorIndex);
            if (targetMonitor == IntPtr.Zero)
                return false;

            var monitorInfo = new NativeMethods.MONITORINFO();
            if (!NativeMethods.GetMonitorInfo(targetMonitor, monitorInfo))
                return false;

            bool[] foundFullscreen = new bool[] { false };

            NativeMethods.EnumWindows((hWnd, lParam) =>
            {
                // Skip our own windows
                NativeMethods.GetWindowThreadProcessId(hWnd, out var processId);
                var currentProcessId = Process.GetCurrentProcess().Id;
                if (processId == currentProcessId)
                    return true;

                // Skip invisible windows
                if (!NativeMethods.IsWindowVisible(hWnd))
                    return true;

                // Skip minimized windows
                var placement = default(NativeMethods.WINDOWPLACEMENT);
                placement.length = Marshal.SizeOf<NativeMethods.WINDOWPLACEMENT>();
                if (!NativeMethods.GetWindowPlacement(hWnd, out placement))
                    return true;
                if (placement.showCmd == NativeMethods.SW_SHOWMINIMIZED)
                    return true;

                // Check if window is on target monitor
                var windowMonitor = NativeMethods.MonitorFromWindow(hWnd, NativeMethods.MONITOR_DEFAULTTONEAREST);
                if (windowMonitor != targetMonitor)
                    return true;

                // Skip tool windows and other non-main windows
                var exStyle = NativeMethods.GetWindowLong(hWnd, NativeMethods.GWL_EXSTYLE);
                if ((exStyle & NativeMethods.WS_EX_TOOLWINDOW) != 0)
                    return true;
                if ((exStyle & NativeMethods.WS_EX_NOACTIVATE) != 0)
                    return true;

                // Check if window is a popup window without a caption
                var style = NativeMethods.GetWindowLong(hWnd, NativeMethods.GWL_STYLE);
                if ((style & NativeMethods.WS_POPUP) != 0 && (style & NativeMethods.WS_CAPTION) == 0)
                    return true;

                // Get window rect
                if (!NativeMethods.GetWindowRect(hWnd, out var windowRect))
                    return true;

                // Check if window covers entire monitor (including taskbar area)
                bool coversFullMonitor =
                    windowRect.Left <= monitorInfo.rcMonitor.Left &&
                    windowRect.Top <= monitorInfo.rcMonitor.Top &&
                    windowRect.Right >= monitorInfo.rcMonitor.Right &&
                    windowRect.Bottom >= monitorInfo.rcMonitor.Bottom;

                if (coversFullMonitor)
                {
                    foundFullscreen[0] = true;
                    return false; // Stop enumeration
                }

                return true;
            }, IntPtr.Zero);

            return foundFullscreen[0];
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"IsFullscreenAppRunning exception: {ex}");
            Logger.Log(ex);
            return false;
        }
    }

    private void Window_TogglePanelRequested(object? sender, EventArgs e)
    {
        if (_disposed) return;
        _mainWindow?.ToggleDock();
    }

    private async void Window_ShowSettingsRequested(object? sender, EventArgs e)
    {
        if (_disposed) return;
        if (_mainWindow != null)
        {
            await _mainWindow.ShowAppSettingsWindow();
        }
    }

    private async void Window_HideIndicatorRequested(object? sender, EventArgs e)
    {
        if (_disposed) return;
        if (_appSettingsService == null) return;

        var settings = _appSettingsService.Settings;
        settings.ShowTaskbarPositionIndicator = false;
        _appSettingsService.Settings = settings;
        try
        {
            await _appSettingsService.SaveAsync();
        }
        catch (Exception ex)
        {
            Logger.Log(ex);
        }
    }

    public void Dispose()
    {
        Dispose(true);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;

        // Mark as disposed first to prevent re-entry
        _disposed = true;

        if (disposing)
        {
            if (_visibilityTimer != null)
            {
                _visibilityTimer.Stop();
                _visibilityTimer.Tick -= VisibilityTimer_Tick;
                _visibilityTimer = null;
            }

            if (_zOrderTimer != null)
            {
                _zOrderTimer.Stop();
                _zOrderTimer.Tick -= ZOrderTimer_Tick;
                _zOrderTimer = null;
            }

            if (_appSettingsService != null)
            {
                _appSettingsService.SettingsChanged -= AppSettingsService_SettingsChanged;
            }

            if (_window != null)
            {
                _window.TogglePanelRequested -= Window_TogglePanelRequested;
                _window.ShowSettingsRequested -= Window_ShowSettingsRequested;
                _window.HideIndicatorRequested -= Window_HideIndicatorRequested;
                _window.DragRequested -= Window_DragRequested;
                _window.DragCompleted -= Window_DragCompleted;
                _window.Loaded -= Window_Loaded;
                _hwndSource?.RemoveHook(WndProc);
                _hwndSource = null;
                _window.Close();
                _window = null;
            }
        }
    }

}

