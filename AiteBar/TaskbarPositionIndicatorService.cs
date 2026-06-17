using System;
using System.Diagnostics;
using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;

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
    private DispatcherTimer? _visibilityTimer;

    public void Initialize(AppSettingsService appSettingsService, MainWindow mainWindow)
    {
        Debug.WriteLine("TaskbarPositionIndicatorService.Initialize called");
        _appSettingsService = appSettingsService;
        _mainWindow = mainWindow;

        _appSettingsService.SettingsChanged += AppSettingsService_SettingsChanged;

        // Initialize visibility check timer
        _visibilityTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(2)
        };
        _visibilityTimer.Tick += VisibilityTimer_Tick;
        _visibilityTimer.Start();

        var showIndicator = _appSettingsService.Settings.ShowTaskbarPositionIndicator.GetValueOrDefault(true);
        Debug.WriteLine($"ShowTaskbarPositionIndicator: {showIndicator}");
        if (showIndicator)
        {
            ShowIndicator();
        }
    }

    private void AppSettingsService_SettingsChanged(object? sender, EventArgs e)
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

    public void UpdatePosition()
    {
        if (_disposed) return;

        Debug.WriteLine("TaskbarPositionIndicatorService.UpdatePosition");
        if (_window == null || _mainWindow == null || _appSettingsService == null) return;

        try
        {
            var settings = _appSettingsService.Settings;
            var taskbarInfo = TaskbarGeometryHelper.GetTaskbarInfo(settings.MonitorIndex);
            var position = TaskbarGeometryHelper.CalculateIndicatorPosition(taskbarInfo, IndicatorSize);
            Debug.WriteLine($"Taskbar info: Edge {taskbarInfo.Edge}, Bounds {taskbarInfo.Bounds}");
            Debug.WriteLine($"Position calculated: X {position.X}, Y {position.Y}");

            var helper = new WindowInteropHelper(_window);
            NativeMethods.SetWindowPos(
                helper.Handle,
                NativeMethods.HWND_TOPMOST,
                (int)position.X,
                (int)position.Y,
                (int)IndicatorSize,
                (int)IndicatorSize,
                NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_SHOWWINDOW
            );

            _window.UpdateArrow(settings.Edge);
            _window.UpdateTooltip(GetTooltipText(settings));
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"UpdatePosition exception: {ex}");
            Logger.Log(ex);
        }
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
            ShowIndicator();
        }
        else
        {
            HideIndicator();
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
        GC.SuppressFinalize(this);
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

            if (_appSettingsService != null)
            {
                _appSettingsService.SettingsChanged -= AppSettingsService_SettingsChanged;
            }

            if (_window != null)
            {
                _window.TogglePanelRequested -= Window_TogglePanelRequested;
                _window.ShowSettingsRequested -= Window_ShowSettingsRequested;
                _window.HideIndicatorRequested -= Window_HideIndicatorRequested;
                _window.Loaded -= Window_Loaded;
                _hwndSource?.RemoveHook(WndProc);
                _hwndSource = null;
                _window.Close();
                _window = null;
            }
        }
    }

    ~TaskbarPositionIndicatorService()
    {
        Dispose(false);
    }
}
