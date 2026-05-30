using System;
using System.Drawing;

namespace AiteBar;

internal static class QuickNoteLayoutHelper
{
    public const double EdgeClearance = PanelLayoutHelper.ButtonOuterSize + PanelLayoutHelper.PanelChrome + 18;

    public static (double HiddenX, double HiddenY, double ShownX, double ShownY) GetSlideCoordinates(
        DockEdge edge,
        Rectangle workArea,
        double width,
        double height)
    {
        width = Math.Max(0, width);
        height = Math.Max(0, height);

        double centeredX = workArea.Left + Math.Max(0, (workArea.Width - width) / 2);
        double centeredY = workArea.Top + Math.Max(0, (workArea.Height - height) / 2);

        return edge switch
        {
            DockEdge.Bottom => (centeredX, workArea.Bottom + EdgeClearance, centeredX, workArea.Bottom - height - EdgeClearance),
            DockEdge.Left => (workArea.Left - width - EdgeClearance, centeredY, workArea.Left + EdgeClearance, centeredY),
            DockEdge.Right => (workArea.Right + EdgeClearance, centeredY, workArea.Right - width - EdgeClearance, centeredY),
            _ => (centeredX, workArea.Top - height - EdgeClearance, centeredX, workArea.Top + EdgeClearance)
        };
    }
}
