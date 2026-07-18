using Size = System.Windows.Size;

namespace AiteBar;

internal static class IndicatorPositionHelper
{
    public static Point FromNormalized(Rect screenBounds, Size indicatorSize, double normalizedX, double normalizedY)
    {
        double availableWidth = Math.Max(0, screenBounds.Width - Math.Max(0, indicatorSize.Width));
        double availableHeight = Math.Max(0, screenBounds.Height - Math.Max(0, indicatorSize.Height));
        double x = screenBounds.Left + availableWidth * NormalizeFraction(normalizedX);
        double y = screenBounds.Top + availableHeight * NormalizeFraction(normalizedY);
        return Clamp(screenBounds, indicatorSize, new Point(x, y));
    }

    public static Point ToNormalized(Rect screenBounds, Size indicatorSize, Point position)
    {
        Point clamped = Clamp(screenBounds, indicatorSize, position);
        double availableWidth = Math.Max(0, screenBounds.Width - Math.Max(0, indicatorSize.Width));
        double availableHeight = Math.Max(0, screenBounds.Height - Math.Max(0, indicatorSize.Height));

        return new Point(
            availableWidth <= 0 ? 0 : (clamped.X - screenBounds.Left) / availableWidth,
            availableHeight <= 0 ? 0 : (clamped.Y - screenBounds.Top) / availableHeight);
    }

    public static Point Clamp(Rect screenBounds, Size indicatorSize, Point position)
    {
        double width = Math.Max(0, indicatorSize.Width);
        double height = Math.Max(0, indicatorSize.Height);
        double maxX = Math.Max(screenBounds.Left, screenBounds.Right - width);
        double maxY = Math.Max(screenBounds.Top, screenBounds.Bottom - height);
        double safeX = double.IsFinite(position.X) ? position.X : screenBounds.Left;
        double safeY = double.IsFinite(position.Y) ? position.Y : screenBounds.Top;

        return new Point(
            Math.Clamp(safeX, screenBounds.Left, maxX),
            Math.Clamp(safeY, screenBounds.Top, maxY));
    }

    private static double NormalizeFraction(double value) =>
        double.IsFinite(value) ? Math.Clamp(value, 0, 1) : 0;
}
