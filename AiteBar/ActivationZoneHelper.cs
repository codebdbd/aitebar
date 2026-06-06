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
