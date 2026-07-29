namespace AiteBar;

internal static class ActivationZoneHelper
{
    public const int DefaultEdgeTolerancePixels = 2;

    public static bool IsInActivationZone(
        DockEdge edge,
        double screenLeft,
        double screenTop,
        double screenWidth,
        double screenHeight,
        double activationZoneSizePercent,
        double pointerX,
        double pointerY,
        int edgeTolerancePixels = DefaultEdgeTolerancePixels)
    {
        double zoneSizePercent = Math.Clamp(activationZoneSizePercent, 0, 100) / 100.0;
        double tolerance = Math.Max(0, edgeTolerancePixels);

        return edge switch
        {
            DockEdge.Top =>
                pointerY >= screenTop &&
                pointerY <= screenTop + tolerance &&
                IsWithinCenteredSpan(pointerX, screenLeft, screenWidth, zoneSizePercent),

            DockEdge.Bottom =>
                pointerY >= screenTop + screenHeight - 1 - tolerance &&
                pointerY <= screenTop + screenHeight - 1 &&
                IsWithinCenteredSpan(pointerX, screenLeft, screenWidth, zoneSizePercent),

            DockEdge.Left =>
                pointerX >= screenLeft &&
                pointerX <= screenLeft + tolerance &&
                IsWithinCenteredSpan(pointerY, screenTop, screenHeight, zoneSizePercent),

            DockEdge.Right =>
                pointerX >= screenLeft + screenWidth - 1 - tolerance &&
                pointerX <= screenLeft + screenWidth - 1 &&
                IsWithinCenteredSpan(pointerY, screenTop, screenHeight, zoneSizePercent),

            _ => false
        };
    }

    private static bool IsWithinCenteredSpan(double value, double start, double length, double percent)
    {
        double halfSpan = length * percent / 2;
        double center = start + length / 2;
        return value > center - halfSpan && value < center + halfSpan;
    }
}

internal sealed class ActivationDwellTracker
{
    public const double DefaultMovementTolerancePixels = 6;

    private DateTime? _startedAt;
    private double _anchorX;
    private double _anchorY;

    public bool Update(
        bool isInActivationZone,
        double pointerX,
        double pointerY,
        DateTime now,
        int delayMs,
        double movementTolerancePixels = DefaultMovementTolerancePixels)
    {
        if (!isInActivationZone)
        {
            Reset();
            return false;
        }

        double tolerance = Math.Max(0, movementTolerancePixels);
        if (_startedAt == null ||
            DistanceSquared(pointerX, pointerY, _anchorX, _anchorY) > tolerance * tolerance)
        {
            _startedAt = now;
            _anchorX = pointerX;
            _anchorY = pointerY;
            return false;
        }

        return (now - _startedAt.Value).TotalMilliseconds >= Math.Max(0, delayMs);
    }

    public void Reset()
    {
        _startedAt = null;
        _anchorX = 0;
        _anchorY = 0;
    }

    private static double DistanceSquared(double x1, double y1, double x2, double y2)
    {
        double dx = x1 - x2;
        double dy = y1 - y2;
        return dx * dx + dy * dy;
    }
}
