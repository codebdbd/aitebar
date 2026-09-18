using System;
using System.Windows.Forms;
using Xunit;

namespace AiteBar.Tests;

public class GameFullscreenServiceTests
{
    private sealed class FakeGameFullscreenNativeQuery : IGameFullscreenNativeQuery
    {
        public IntPtr ForegroundWindow { get; set; } = (IntPtr)100;
        public uint ProcessId { get; set; } = 9999;
        public bool IsVisible { get; set; } = true;
        public bool IsMinimized { get; set; } = false;
        public string ClassName { get; set; } = "GameWindowClass";
        public int WindowStyle { get; set; } = NativeMethods.WS_POPUP | NativeMethods.WS_VISIBLE;
        public NativeMethods.RECT WindowRect { get; set; } = new() { Left = 0, Top = 0, Right = 1920, Bottom = 1080 };
        public NativeMethods.RECT MonitorRect { get; set; } = new() { Left = 0, Top = 0, Right = 1920, Bottom = 1080 };
        public NativeMethods.RECT WorkRect { get; set; } = new() { Left = 0, Top = 0, Right = 1920, Bottom = 1040 };
        public IntPtr WindowMonitor { get; set; } = (IntPtr)1;
        public NativeMethods.QUERY_USER_NOTIFICATION_STATE NotificationState { get; set; } = NativeMethods.QUERY_USER_NOTIFICATION_STATE.QUNS_ACCEPTS_NOTIFICATIONS;
        public int NotificationQueryResult { get; set; } = 0;
        public IntPtr ScreenMonitor { get; set; } = (IntPtr)1;

        public IntPtr GetForegroundWindow() => ForegroundWindow;
        public uint GetProcessId(IntPtr hwnd) => ProcessId;
        public bool IsWindowVisible(IntPtr hwnd) => IsVisible;
        public bool IsWindowMinimized(IntPtr hwnd) => IsMinimized;
        public string GetClassName(IntPtr hwnd) => ClassName;
        public int GetWindowStyle(IntPtr hwnd) => WindowStyle;
        public bool GetWindowRect(IntPtr hwnd, out NativeMethods.RECT rect)
        {
            rect = WindowRect;
            return true;
        }
        public bool GetMonitorRects(IntPtr hwnd, out NativeMethods.RECT rcMonitor, out NativeMethods.RECT rcWork, out IntPtr hMonitor)
        {
            rcMonitor = MonitorRect;
            rcWork = WorkRect;
            hMonitor = WindowMonitor;
            return true;
        }
        public int QueryUserNotificationState(out NativeMethods.QUERY_USER_NOTIFICATION_STATE state)
        {
            state = NotificationState;
            return NotificationQueryResult;
        }
        public IntPtr MonitorFromScreen(Screen screen) => ScreenMonitor;
    }

    [Fact]
    public void IsGameOrFullscreenForeground_ReturnsFalse_WhenForegroundIsZero()
    {
        var query = new FakeGameFullscreenNativeQuery { ForegroundWindow = IntPtr.Zero };
        using var service = new GameFullscreenService(query, currentProcessId: 1000);

        Assert.False(service.IsGameOrFullscreenForeground());
    }

    [Fact]
    public void IsGameOrFullscreenForeground_ReturnsFalse_WhenOwnProcess()
    {
        var query = new FakeGameFullscreenNativeQuery { ProcessId = 1000 };
        using var service = new GameFullscreenService(query, currentProcessId: 1000);

        Assert.False(service.IsGameOrFullscreenForeground());
    }

    [Fact]
    public void IsGameOrFullscreenForeground_ReturnsFalse_WhenInvisibleOrMinimized()
    {
        var queryVisible = new FakeGameFullscreenNativeQuery { IsVisible = false };
        using var serviceVisible = new GameFullscreenService(queryVisible, currentProcessId: 1000);
        Assert.False(serviceVisible.IsGameOrFullscreenForeground());

        var queryMin = new FakeGameFullscreenNativeQuery { IsMinimized = true };
        using var serviceMin = new GameFullscreenService(queryMin, currentProcessId: 1000);
        Assert.False(serviceMin.IsGameOrFullscreenForeground());
    }

    [Theory]
    [InlineData("Progman")]
    [InlineData("WorkerW")]
    [InlineData("Shell_TrayWnd")]
    [InlineData("Shell_SecondaryTrayWnd")]
    public void IsGameOrFullscreenForeground_ReturnsFalse_WhenDesktopOrTaskbar(string className)
    {
        var query = new FakeGameFullscreenNativeQuery { ClassName = className };
        using var service = new GameFullscreenService(query, currentProcessId: 1000);

        Assert.False(service.IsGameOrFullscreenForeground());
    }

    [Fact]
    public void IsGameOrFullscreenForeground_ReturnsTrue_WhenD3DFullscreen()
    {
        var query = new FakeGameFullscreenNativeQuery
        {
            NotificationState = NativeMethods.QUERY_USER_NOTIFICATION_STATE.QUNS_RUNNING_D3D_FULL_SCREEN,
            NotificationQueryResult = 0
        };
        using var service = new GameFullscreenService(query, currentProcessId: 1000);

        Assert.True(service.IsGameOrFullscreenForeground());
    }

    [Fact]
    public void IsGameOrFullscreenForeground_ReturnsTrue_WhenBorderlessFullscreenGame()
    {
        var query = new FakeGameFullscreenNativeQuery
        {
            NotificationState = NativeMethods.QUERY_USER_NOTIFICATION_STATE.QUNS_ACCEPTS_NOTIFICATIONS,
            WindowRect = new() { Left = 0, Top = 0, Right = 1920, Bottom = 1080 },
            MonitorRect = new() { Left = 0, Top = 0, Right = 1920, Bottom = 1080 },
            WindowStyle = NativeMethods.WS_POPUP | NativeMethods.WS_VISIBLE // No WS_CAPTION
        };
        using var service = new GameFullscreenService(query, currentProcessId: 1000);

        Assert.True(service.IsGameOrFullscreenForeground());
    }

    [Fact]
    public void IsGameOrFullscreenForeground_ReturnsFalse_WhenNormalMaximizedWindow()
    {
        var query = new FakeGameFullscreenNativeQuery
        {
            NotificationState = NativeMethods.QUERY_USER_NOTIFICATION_STATE.QUNS_ACCEPTS_NOTIFICATIONS,
            WindowRect = new() { Left = 0, Top = 0, Right = 1920, Bottom = 1040 }, // Doesn't cover taskbar
            MonitorRect = new() { Left = 0, Top = 0, Right = 1920, Bottom = 1080 },
            WindowStyle = NativeMethods.WS_CAPTION | NativeMethods.WS_VISIBLE
        };
        using var service = new GameFullscreenService(query, currentProcessId: 1000);

        Assert.False(service.IsGameOrFullscreenForeground());
    }

    [Fact]
    public void IsGameOrFullscreenForeground_ReturnsFalse_WhenBusyStateOnNormalWindow()
    {
        var query = new FakeGameFullscreenNativeQuery
        {
            NotificationState = NativeMethods.QUERY_USER_NOTIFICATION_STATE.QUNS_BUSY,
            WindowRect = new() { Left = 0, Top = 0, Right = 1920, Bottom = 1040 },
            MonitorRect = new() { Left = 0, Top = 0, Right = 1920, Bottom = 1080 },
            WindowStyle = NativeMethods.WS_CAPTION | NativeMethods.WS_VISIBLE
        };
        using var service = new GameFullscreenService(query, currentProcessId: 1000);

        Assert.False(service.IsGameOrFullscreenForeground());
    }

    [Fact]
    public void IsGameOrFullscreenForeground_ReturnsTrue_WhenBusyStateOnBorderlessFullscreen()
    {
        var query = new FakeGameFullscreenNativeQuery
        {
            NotificationState = NativeMethods.QUERY_USER_NOTIFICATION_STATE.QUNS_BUSY,
            WindowRect = new() { Left = 0, Top = 0, Right = 1920, Bottom = 1080 },
            MonitorRect = new() { Left = 0, Top = 0, Right = 1920, Bottom = 1080 },
            WindowStyle = NativeMethods.WS_POPUP | NativeMethods.WS_VISIBLE
        };
        using var service = new GameFullscreenService(query, currentProcessId: 1000);

        Assert.True(service.IsGameOrFullscreenForeground());
    }

    [Fact]
    public void IsFullscreenActive_ReturnsTrue_WhenUtilityFullscreenActive()
    {
        var query = new FakeGameFullscreenNativeQuery { ForegroundWindow = IntPtr.Zero };
        using var service = new GameFullscreenService(query, currentProcessId: 1000)
        {
            IsUtilityFullscreenActive = true
        };

        Assert.True(service.IsFullscreenActive());
    }

    [Fact]
    public void IsUtilityFullscreenActive_DoesNotTriggerFullscreenStateChanged_PreservingHotkeys()
    {
        var query = new FakeGameFullscreenNativeQuery
        {
            ForegroundWindow = (IntPtr)500,
            ProcessId = 1000 // Own process, e.g. ZenEditor
        };
        using var service = new GameFullscreenService(query, currentProcessId: 1000);

        bool stateChangedFired = false;
        service.FullscreenStateChanged += (_, _) => stateChangedFired = true;

        service.IsUtilityFullscreenActive = true;
        service.EvaluateState();

        Assert.False(stateChangedFired);
        Assert.False(service.IsGameOrFullscreenForeground());
        Assert.True(service.IsFullscreenActive());
    }

    [Fact]
    public void FullscreenStateChanged_Fires_WhenStateTransitions()
    {
        var query = new FakeGameFullscreenNativeQuery
        {
            NotificationState = NativeMethods.QUERY_USER_NOTIFICATION_STATE.QUNS_ACCEPTS_NOTIFICATIONS,
            WindowRect = new() { Left = 100, Top = 100, Right = 800, Bottom = 600 } // Normal window
        };
        using var service = new GameFullscreenService(query, currentProcessId: 1000);

        bool? lastFiredState = null;
        int fireCount = 0;
        service.FullscreenStateChanged += (s, isFullscreen) =>
        {
            lastFiredState = isFullscreen;
            fireCount++;
        };

        // Transition to D3D fullscreen
        query.NotificationState = NativeMethods.QUERY_USER_NOTIFICATION_STATE.QUNS_RUNNING_D3D_FULL_SCREEN;
        service.EvaluateState();

        Assert.Equal(1, fireCount);
        Assert.True(lastFiredState);

        // Evaluate again without change -> should not fire
        service.EvaluateState();
        Assert.Equal(1, fireCount);

        // Transition back to desktop
        query.NotificationState = NativeMethods.QUERY_USER_NOTIFICATION_STATE.QUNS_ACCEPTS_NOTIFICATIONS;
        service.EvaluateState();

        Assert.Equal(2, fireCount);
        Assert.False(lastFiredState);
    }

    [Fact]
    public void AppSettings_Defaults_HaveFullscreenSuppressionEnabled()
    {
        var settings = new AppSettings();
        Assert.True(settings.SuppressPanelInFullscreen);
        Assert.True(settings.SuppressHotkeysInFullscreen);
    }
}
