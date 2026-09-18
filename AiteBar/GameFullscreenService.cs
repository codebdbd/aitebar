using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using System.Windows.Forms;

namespace AiteBar;

public interface IGameFullscreenService : IDisposable
{
    bool IsFullscreenActive(Screen? targetScreen = null);
    bool IsGameOrFullscreenForeground();
    bool IsUtilityFullscreenActive { get; set; }
    event EventHandler<bool>? FullscreenStateChanged;
    void EvaluateState();
}

internal interface IGameFullscreenNativeQuery
{
    IntPtr GetForegroundWindow();
    uint GetProcessId(IntPtr hwnd);
    bool IsWindowVisible(IntPtr hwnd);
    bool IsWindowMinimized(IntPtr hwnd);
    string GetClassName(IntPtr hwnd);
    int GetWindowStyle(IntPtr hwnd);
    bool GetWindowRect(IntPtr hwnd, out NativeMethods.RECT rect);
    bool GetMonitorRects(IntPtr hwnd, out NativeMethods.RECT rcMonitor, out NativeMethods.RECT rcWork, out IntPtr hMonitor);
    int QueryUserNotificationState(out NativeMethods.QUERY_USER_NOTIFICATION_STATE state);
    IntPtr MonitorFromScreen(Screen screen);
}

[SupportedOSPlatform("windows6.1")]
internal sealed class DefaultGameFullscreenNativeQuery : IGameFullscreenNativeQuery
{
    public IntPtr GetForegroundWindow() =>
        NativeMethods.GetForegroundWindow();

    public uint GetProcessId(IntPtr hwnd)
    {
        NativeMethods.GetWindowThreadProcessId(hwnd, out uint pid);
        return pid;
    }

    public bool IsWindowVisible(IntPtr hwnd) =>
        NativeMethods.IsWindowVisible(hwnd);

    public bool IsWindowMinimized(IntPtr hwnd)
    {
        var placement = default(NativeMethods.WINDOWPLACEMENT);
        placement.length = Marshal.SizeOf<NativeMethods.WINDOWPLACEMENT>();
        return NativeMethods.GetWindowPlacement(hwnd, out placement) &&
               placement.showCmd == NativeMethods.SW_SHOWMINIMIZED;
    }

    public string GetClassName(IntPtr hwnd)
    {
        var sb = new StringBuilder(256);
        return NativeMethods.GetClassName(hwnd, sb, sb.Capacity) > 0 ? sb.ToString() : string.Empty;
    }

    public int GetWindowStyle(IntPtr hwnd) =>
        NativeMethods.GetWindowLong(hwnd, NativeMethods.GWL_STYLE);

    public bool GetWindowRect(IntPtr hwnd, out NativeMethods.RECT rect) =>
        NativeMethods.GetWindowRect(hwnd, out rect);

    public bool GetMonitorRects(IntPtr hwnd, out NativeMethods.RECT rcMonitor, out NativeMethods.RECT rcWork, out IntPtr hMonitor)
    {
        hMonitor = NativeMethods.MonitorFromWindow(hwnd, NativeMethods.MONITOR_DEFAULTTONEAREST);
        if (hMonitor == IntPtr.Zero)
        {
            rcMonitor = default;
            rcWork = default;
            return false;
        }

        var monitorInfo = new NativeMethods.MONITORINFO();
        if (NativeMethods.GetMonitorInfo(hMonitor, monitorInfo))
        {
            rcMonitor = monitorInfo.rcMonitor;
            rcWork = monitorInfo.rcWork;
            return true;
        }

        rcMonitor = default;
        rcWork = default;
        return false;
    }

    public int QueryUserNotificationState(out NativeMethods.QUERY_USER_NOTIFICATION_STATE state) =>
        NativeMethods.SHQueryUserNotificationState(out state);

    public IntPtr MonitorFromScreen(Screen screen)
    {
        var pt = new NativeMethods.Win32Point
        {
            X = screen.Bounds.Left + (screen.Bounds.Width / 2),
            Y = screen.Bounds.Top + (screen.Bounds.Height / 2)
        };
        return NativeMethods.MonitorFromPoint(pt, NativeMethods.MONITOR_DEFAULTTONEAREST);
    }
}

[SupportedOSPlatform("windows6.1")]
public sealed class GameFullscreenService : IGameFullscreenService
{
    private readonly IGameFullscreenNativeQuery _query;
    private readonly uint _currentProcessId;
    private readonly NativeMethods.WinEventProc _foregroundProc;
    private IntPtr _hook = IntPtr.Zero;
    private bool _disposed;
    private bool _isUtilityFullscreenActive;
    private bool _lastForegroundFullscreen;

    public event EventHandler<bool>? FullscreenStateChanged;

    public bool IsUtilityFullscreenActive
    {
        get => _isUtilityFullscreenActive;
        set => _isUtilityFullscreenActive = value;
    }

    public GameFullscreenService()
        : this(new DefaultGameFullscreenNativeQuery(), (uint)Environment.ProcessId)
    {
    }

    internal GameFullscreenService(IGameFullscreenNativeQuery query, uint currentProcessId)
    {
        _query = query;
        _currentProcessId = currentProcessId;
        _foregroundProc = OnForegroundChanged;

        try
        {
            _hook = NativeMethods.SetWinEventHook(
                NativeMethods.EVENT_SYSTEM_FOREGROUND,
                NativeMethods.EVENT_SYSTEM_FOREGROUND,
                IntPtr.Zero,
                _foregroundProc,
                0,
                0,
                NativeMethods.WINEVENT_OUTOFCONTEXT);
        }
        catch (Exception ex)
        {
            Logger.Log(ex);
        }

        _lastForegroundFullscreen = IsGameOrFullscreenForeground();
    }

    private void OnForegroundChanged(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
    {
        if (eventType == NativeMethods.EVENT_SYSTEM_FOREGROUND)
        {
            EvaluateState();
        }
    }

    public void EvaluateState()
    {
        if (_disposed) return;

        bool current = IsGameOrFullscreenForeground();
        if (current != _lastForegroundFullscreen)
        {
            _lastForegroundFullscreen = current;
            FullscreenStateChanged?.Invoke(this, current);
        }
    }

    public bool IsGameOrFullscreenForeground()
    {
        if (_disposed) return false;

        try
        {
            IntPtr foregroundHwnd = _query.GetForegroundWindow();
            if (foregroundHwnd == IntPtr.Zero)
            {
                return false;
            }

            uint processId = _query.GetProcessId(foregroundHwnd);
            if (processId == _currentProcessId)
            {
                return false;
            }

            if (!_query.IsWindowVisible(foregroundHwnd))
            {
                return false;
            }

            if (_query.IsWindowMinimized(foregroundHwnd))
            {
                return false;
            }

            string className = _query.GetClassName(foregroundHwnd);
            if (string.Equals(className, "Progman", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(className, "WorkerW", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(className, "Shell_TrayWnd", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(className, "Shell_SecondaryTrayWnd", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            // 1. Direct3D / exclusive fullscreen / presentation query
            if (_query.QueryUserNotificationState(out var state) == 0)
            {
                if (state is NativeMethods.QUERY_USER_NOTIFICATION_STATE.QUNS_RUNNING_D3D_FULL_SCREEN or
                             NativeMethods.QUERY_USER_NOTIFICATION_STATE.QUNS_PRESENTATION_MODE)
                {
                    return true;
                }
            }

            // 2. Borderless fullscreen check: does foreground window cover entire monitor?
            if (_query.GetMonitorRects(foregroundHwnd, out var rcMonitor, out _, out _))
            {
                if (_query.GetWindowRect(foregroundHwnd, out var windowRect))
                {
                    bool coversMonitor =
                        windowRect.Left <= rcMonitor.Left &&
                        windowRect.Top <= rcMonitor.Top &&
                        windowRect.Right >= rcMonitor.Right &&
                        windowRect.Bottom >= rcMonitor.Bottom;

                    if (coversMonitor)
                    {
                        int style = _query.GetWindowStyle(foregroundHwnd);
                        bool hasCaption = (style & NativeMethods.WS_CAPTION) == NativeMethods.WS_CAPTION;
                        // Borderless fullscreen games lack standard caption/thickframe or are popup
                        if (!hasCaption)
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }
        catch (Exception ex)
        {
            Logger.Log(ex);
            return false;
        }
    }

    public bool IsFullscreenActive(Screen? targetScreen = null)
    {
        if (_disposed) return false;

        if (_isUtilityFullscreenActive)
        {
            return true;
        }

        // If the active foreground window is an external game or fullscreen app,
        // it must suppress edge dock activation
        if (IsGameOrFullscreenForeground())
        {
            if (targetScreen == null)
            {
                return true;
            }

            // If a specific screen is queried, check if the foreground window is on that screen
            IntPtr foregroundHwnd = _query.GetForegroundWindow();
            if (foregroundHwnd != IntPtr.Zero)
            {
                if (_query.GetMonitorRects(foregroundHwnd, out _, out _, out IntPtr hWindowMonitor))
                {
                    IntPtr targetMonitor = _query.MonitorFromScreen(targetScreen);
                    if (hWindowMonitor == targetMonitor || targetMonitor == IntPtr.Zero)
                    {
                        return true;
                    }
                }
            }

            // Even on a secondary monitor, if a game is foreground, suppress to prevent accidental edge-slips
            return true;
        }

        return false;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_hook != IntPtr.Zero)
        {
            try
            {
                NativeMethods.UnhookWinEvent(_hook);
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
            _hook = IntPtr.Zero;
        }

        FullscreenStateChanged = null;
    }
}
