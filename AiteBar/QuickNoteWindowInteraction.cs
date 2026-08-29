using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Windows.Interop;

namespace AiteBar;

/// <summary>Distinguishes native window arrangement from leaving the note to use another application.</summary>
[SupportedOSPlatform("windows6.1")]
internal sealed class QuickNoteWindowInteraction : IDisposable
{
    private readonly HwndSource _source;
    private bool _movingOrSizing;

    internal QuickNoteWindowInteraction(HwndSource source)
    {
        _source = source;
        _source.AddHook(WindowMessage);
    }

    internal bool IsArrangingWindow
    {
        get
        {
            if (_movingOrSizing) return true;
            if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 18362) || !IsWindowArranged(_source.Handle))
                return false;

            IntPtr foreground = GetForegroundWindow();
            GetWindowThreadProcessId(foreground, out uint foregroundProcess);
            GetWindowThreadProcessId(GetShellWindow(), out uint shellProcess);
            return IsShellArrangementSurface(foregroundProcess, shellProcess,
                GetWindowLong(foreground, -16), GetWindowLong(foreground, -20));
        }
    }

    internal static bool IsShellArrangementSurface(uint foregroundProcess, uint shellProcess, int style, int extendedStyle)
    {
        const uint popup = 0x80000000;
        const int caption = 0x00C00000, toolWindow = 0x80;
        // Snap Assist temporarily activates a shell tool popup. Ordinary Explorer windows,
        // the desktop and other applications must still dismiss an unpinned note.
        return shellProcess != 0 && foregroundProcess == shellProcess &&
            ((uint)style & popup) != 0 && (style & caption) == 0 && (extendedStyle & toolWindow) != 0;
    }

    private IntPtr WindowMessage(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == 0x0231) _movingOrSizing = true; // WM_ENTERSIZEMOVE
        else if (message == 0x0232) _movingOrSizing = false; // WM_EXITSIZEMOVE
        return IntPtr.Zero; // WindowChrome and Windows retain complete control of the operation.
    }

    public void Dispose()
    {
        if (!_source.IsDisposed) _source.RemoveHook(WindowMessage);
    }

    [DllImport("user32.dll")]
    private static extern bool IsWindowArranged(IntPtr handle);
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")]
    private static extern IntPtr GetShellWindow();
    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr handle, out uint processId);
    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr handle, int index);
}
