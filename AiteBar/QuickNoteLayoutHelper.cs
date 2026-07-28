using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

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

    public static Rectangle SelectWorkArea(
        IReadOnlyList<Rectangle> workAreas,
        double? left,
        double? top,
        double? width,
        double? height)
    {
        ArgumentNullException.ThrowIfNull(workAreas);
        if (workAreas.Count == 0)
        {
            return Rectangle.Empty;
        }

        if (!IsUsableCoordinate(left) || !IsUsableCoordinate(top))
        {
            return workAreas[0];
        }

        double safeWidth = IsUsableSize(width) ? width!.Value : DefaultWidth;
        double safeHeight = IsUsableSize(height) ? height!.Value : DefaultHeight;
        var savedBounds = new Rectangle(
            (int)Math.Round(left!.Value),
            (int)Math.Round(top!.Value),
            Math.Max(1, (int)Math.Round(safeWidth)),
            Math.Max(1, (int)Math.Round(safeHeight)));

        Rectangle intersecting = workAreas
            .Select(area => (Area: area, Intersection: Rectangle.Intersect(area, savedBounds)))
            .OrderByDescending(candidate => (long)candidate.Intersection.Width * candidate.Intersection.Height)
            .First()
            .Area;

        if (workAreas.Any(area => Rectangle.Intersect(area, savedBounds) is { Width: > 0, Height: > 0 }))
        {
            return intersecting;
        }

        double centerX = left.Value + safeWidth / 2;
        double centerY = top.Value + safeHeight / 2;
        return workAreas
            .OrderBy(area => DistanceSquaredToRectangle(centerX, centerY, area))
            .First();
    }

    private static double DistanceSquaredToRectangle(double x, double y, Rectangle rectangle)
    {
        double dx = x < rectangle.Left ? rectangle.Left - x : x > rectangle.Right ? x - rectangle.Right : 0;
        double dy = y < rectangle.Top ? rectangle.Top - y : y > rectangle.Bottom ? y - rectangle.Bottom : 0;
        return dx * dx + dy * dy;
    }

    private static bool IsUsableSize(double? value) =>
        value.HasValue && !double.IsNaN(value.Value) && !double.IsInfinity(value.Value) && value.Value > 0;

    private static bool IsUsableCoordinate(double? value) =>
        value.HasValue && !double.IsNaN(value.Value) && !double.IsInfinity(value.Value);
}
