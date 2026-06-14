using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows;

namespace AiteBar;

internal static class PanelPositionHelper
{
    public const int EdgeSwitchHysteresisPixels = 60;

    public static (double X, double Y) GetDockCoordinates(
        DockEdge edge,
        Rect workArea,
        Rect bounds,
        double panelWidth,
        double panelHeight,
        double topPanelVisibleOffset,
        bool hide)
    {
        double centeredX = workArea.Left + Math.Max(0, (workArea.Width - panelWidth) / 2);
        double centeredY = workArea.Top + Math.Max(0, (workArea.Height - panelHeight) / 2);

        return edge switch
        {
            DockEdge.Top => (centeredX, hide ? bounds.Top - panelHeight : workArea.Top + topPanelVisibleOffset),
            DockEdge.Bottom => (centeredX, hide ? bounds.Bottom : workArea.Bottom - panelHeight),
            DockEdge.Left => (hide ? bounds.Left - panelWidth : workArea.Left, centeredY),
            DockEdge.Right => (hide ? bounds.Right : workArea.Right - panelWidth, centeredY),
            _ => (workArea.Left, workArea.Top)
        };
    }

    public static DockEdge GetClosestDockEdge(Rectangle workArea, int cursorX, int cursorY, DockEdge currentEdge)
    {
        var distances = new Dictionary<DockEdge, int>
        {
            [DockEdge.Top] = Math.Abs(cursorY - workArea.Top),
            [DockEdge.Bottom] = Math.Abs(workArea.Bottom - cursorY),
            [DockEdge.Left] = Math.Abs(cursorX - workArea.Left),
            [DockEdge.Right] = Math.Abs(workArea.Right - cursorX)
        };

        distances[currentEdge] -= EdgeSwitchHysteresisPixels;

        return distances.OrderBy(pair => pair.Value).First().Key;
    }

    public static int FindScreenIndex(IReadOnlyList<string> screenDeviceNames, string targetDeviceName)
    {
        for (int index = 0; index < screenDeviceNames.Count; index++)
        {
            if (string.Equals(screenDeviceNames[index], targetDeviceName, StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return 0;
    }
}
