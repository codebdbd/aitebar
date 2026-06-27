using System.Drawing;

namespace AiteBar;

internal static class UtilityWindowLayoutHelper
{
    public const double EdgeClearance = PanelLayoutHelper.ButtonOuterSize + PanelLayoutHelper.PanelChrome + 18;

    public static (double Left, double Top) GetCenteredCoordinates(
        DockEdge edge,
        Rectangle workArea,
        double width,
        double height)
    {
        width = Math.Max(0, width);
        height = Math.Max(0, height);

        double centeredX = workArea.Left + Math.Max(0, (workArea.Width - width) / 2);
        double centeredY = workArea.Top + Math.Max(0, (workArea.Height - height) / 2);

        // Position window centered in work area, accounting for panel edge
        return edge switch
        {
            DockEdge.Bottom => (centeredX, workArea.Bottom - height - EdgeClearance),
            DockEdge.Left => (workArea.Left + EdgeClearance, centeredY),
            DockEdge.Right => (workArea.Right - width - EdgeClearance, centeredY),
            _ => (centeredX, workArea.Top + EdgeClearance)
        };
    }
}
