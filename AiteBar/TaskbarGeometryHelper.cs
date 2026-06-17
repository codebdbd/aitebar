using System;
using System.Diagnostics;
using System.Runtime.Versioning;
using System.Windows;

namespace AiteBar;

[SupportedOSPlatform("windows6.1")]
internal static class TaskbarGeometryHelper
{
    public struct TaskbarInfo
    {
        public DockEdge Edge;
        public Rect Bounds;
        public int MonitorIndex;
    }

    public static TaskbarInfo GetTaskbarInfo(int monitorIndex = 0)
    {
        var info = new TaskbarInfo
        {
            Edge = DockEdge.Bottom,
            Bounds = new Rect(),
            MonitorIndex = monitorIndex
        };

        try
        {
            var abd = new NativeMethods.APPBARDATA
            {
                cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf(typeof(NativeMethods.APPBARDATA))
            };

            Debug.WriteLine($"Calling SHAppBarMessage(ABM_GETTASKBARPOS, ...), cbSize={abd.cbSize}");
            var result = NativeMethods.SHAppBarMessage(NativeMethods.ABM_GETTASKBARPOS, ref abd);
            Debug.WriteLine($"SHAppBarMessage returned {result}");
            if (result != IntPtr.Zero)
            {
                Debug.WriteLine($"abd.uEdge={abd.uEdge}, abd.rc: Left={abd.rc.Left}, Top={abd.rc.Top}, Right={abd.rc.Right}, Bottom={abd.rc.Bottom}");
                info.Edge = abd.uEdge switch
                {
                    NativeMethods.ABE_LEFT => DockEdge.Left,
                    NativeMethods.ABE_TOP => DockEdge.Top,
                    NativeMethods.ABE_RIGHT => DockEdge.Right,
                    NativeMethods.ABE_BOTTOM => DockEdge.Bottom,
                    _ => DockEdge.Bottom
                };

                info.Bounds = new Rect(
                    abd.rc.Left,
                    abd.rc.Top,
                    abd.rc.Right - abd.rc.Left,
                    abd.rc.Bottom - abd.rc.Top
                );
            }
            else
            {
                Debug.WriteLine("Fallback to Screen.PrimaryScreen");
                // Fallback: use primary monitor work area
                var primaryScreen = System.Windows.Forms.Screen.PrimaryScreen;
                if (primaryScreen != null)
                {
                    Debug.WriteLine($"primaryScreen.Bounds: {primaryScreen.Bounds}");
                    Debug.WriteLine($"primaryScreen.WorkingArea: {primaryScreen.WorkingArea}");
                    var workArea = primaryScreen.WorkingArea;
                    var bounds = primaryScreen.Bounds;

                    // Try to guess taskbar position by comparing work area and bounds
                    if (workArea.Top > bounds.Top)
                    {
                        info.Edge = DockEdge.Top;
                        info.Bounds = new Rect(bounds.Left, bounds.Top, bounds.Width, workArea.Top - bounds.Top);
                    }
                    else if (workArea.Bottom < bounds.Bottom)
                    {
                        info.Edge = DockEdge.Bottom;
                        info.Bounds = new Rect(bounds.Left, workArea.Bottom, bounds.Width, bounds.Bottom - workArea.Bottom);
                    }
                    else if (workArea.Left > bounds.Left)
                    {
                        info.Edge = DockEdge.Left;
                        info.Bounds = new Rect(bounds.Left, bounds.Top, workArea.Left - bounds.Left, bounds.Height);
                    }
                    else if (workArea.Right < bounds.Right)
                    {
                        info.Edge = DockEdge.Right;
                        info.Bounds = new Rect(workArea.Right, bounds.Top, bounds.Right - workArea.Right, bounds.Height);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"GetTaskbarInfo exception: {ex}");
            Logger.Log(ex);
            // Fallback: default to bottom on primary
            var primaryScreen = System.Windows.Forms.Screen.PrimaryScreen;
            if (primaryScreen != null)
            {
                info.Edge = DockEdge.Bottom;
                info.Bounds = new Rect(
                    primaryScreen.Bounds.Left,
                    primaryScreen.WorkingArea.Bottom,
                    primaryScreen.Bounds.Width,
                    primaryScreen.Bounds.Bottom - primaryScreen.WorkingArea.Bottom
                );
            }
        }

        Debug.WriteLine($"Returning info.Edge={info.Edge}, info.Bounds={info.Bounds}");
        return info;
    }

    public static Point CalculateIndicatorPosition(TaskbarInfo taskbarInfo, double indicatorSize)
    {
        var taskbarRect = taskbarInfo.Bounds;
        double x = 0;
        double y = 0;
        const double startPadding = 250; // Distance from left/top edge near Start button

        Debug.WriteLine($"Calculating position for taskbarInfo.Edge={taskbarInfo.Edge}, indicatorSize={indicatorSize}");
        // Position near the Start button area
        switch (taskbarInfo.Edge)
        {
            case DockEdge.Top:
            case DockEdge.Bottom:
                // Horizontal taskbar: position near left (Start button), vertically centered
                x = taskbarRect.Left + startPadding;
                y = taskbarRect.Top + (taskbarRect.Height - indicatorSize) / 2;
                break;
            case DockEdge.Left:
            case DockEdge.Right:
                // Vertical taskbar: position near top, horizontally centered
                x = taskbarRect.Left + (taskbarRect.Width - indicatorSize) / 2;
                y = taskbarRect.Top + startPadding;
                break;
        }

        // Ensure position is within taskbar bounds
        x = Math.Max(taskbarRect.Left + 4, Math.Min(x, taskbarRect.Right - indicatorSize - 4));
        y = Math.Max(taskbarRect.Top + 4, Math.Min(y, taskbarRect.Bottom - indicatorSize - 4));

        Debug.WriteLine($"Calculated position: X={x}, Y={y}");
        return new Point(x, y);
    }

    public static string GetArrowGlyph(DockEdge edge)
    {
        return edge switch
        {
            DockEdge.Top => "↑",
            DockEdge.Bottom => "↓",
            DockEdge.Left => "←",
            DockEdge.Right => "→",
            _ => "→"
        };
    }
}
