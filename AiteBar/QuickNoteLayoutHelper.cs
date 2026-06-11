using System;
using System.Drawing;

namespace AiteBar;

internal static class QuickNoteLayoutHelper
{
    public const double EdgeClearance = PanelLayoutHelper.ButtonOuterSize + PanelLayoutHelper.PanelChrome + 18;
    public const double DefaultWidth = 580;
    public const double DefaultHeight = 430;
    public const double MinWidth = 460;
    public const double MinHeight = 320;

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

    public static (double Left, double Top, double Width, double Height) ClampBoundsToWorkArea(
        Rectangle workArea,
        double? left,
        double? top,
        double? width,
        double? height)
    {
        double safeWidth = IsUsableSize(width) ? width!.Value : DefaultWidth;
        double safeHeight = IsUsableSize(height) ? height!.Value : DefaultHeight;
        safeWidth = Math.Min(Math.Max(safeWidth, MinWidth), Math.Max(MinWidth, workArea.Width));
        safeHeight = Math.Min(Math.Max(safeHeight, MinHeight), Math.Max(MinHeight, workArea.Height));

        double maxLeft = workArea.Right - safeWidth;
        double maxTop = workArea.Bottom - safeHeight;
        double safeLeft = IsUsableCoordinate(left) ? left!.Value : workArea.Left + Math.Max(0, (workArea.Width - safeWidth) / 2);
        double safeTop = IsUsableCoordinate(top) ? top!.Value : workArea.Top + Math.Max(0, (workArea.Height - safeHeight) / 2);

        safeLeft = Math.Clamp(safeLeft, workArea.Left, Math.Max(workArea.Left, maxLeft));
        safeTop = Math.Clamp(safeTop, workArea.Top, Math.Max(workArea.Top, maxTop));

        return (safeLeft, safeTop, safeWidth, safeHeight);
    }

    private static bool IsUsableSize(double? value) =>
        value.HasValue && !double.IsNaN(value.Value) && !double.IsInfinity(value.Value) && value.Value > 0;

    private static bool IsUsableCoordinate(double? value) =>
        value.HasValue && !double.IsNaN(value.Value) && !double.IsInfinity(value.Value);
}
