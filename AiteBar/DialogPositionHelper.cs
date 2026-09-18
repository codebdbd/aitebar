using System;
using System.Windows;

namespace AiteBar;

internal static class DialogPositionHelper
{
    public static (double Left, double Top) CalculatePosition(
        Rect? ownerBounds,
        bool isOwnerVisible,
        Rect workArea,
        double dialogWidth,
        double dialogHeight)
    {
        double safeWidth = Math.Max(0, dialogWidth);
        double safeHeight = Math.Max(0, dialogHeight);

        double screenCenterX = workArea.Left + Math.Max(0, (workArea.Width - safeWidth) / 2);
        double screenCenterY = workArea.Top + Math.Max(0, (workArea.Height - safeHeight) / 2);

        if (!ownerBounds.HasValue || !isOwnerVisible ||
            ownerBounds.Value.Left < workArea.Left - 5 ||
            ownerBounds.Value.Top < workArea.Top - 5 ||
            ownerBounds.Value.Right > workArea.Right + 5 ||
            ownerBounds.Value.Bottom > workArea.Bottom + 5 ||
            ownerBounds.Value.Width <= 0 ||
            ownerBounds.Value.Height <= 0)
        {
            return (screenCenterX, screenCenterY);
        }

        double targetX = ownerBounds.Value.Left + (ownerBounds.Value.Width - safeWidth) / 2;
        double targetY = ownerBounds.Value.Top + (ownerBounds.Value.Height - safeHeight) / 2;

        double maxLeft = Math.Max(workArea.Left, workArea.Right - safeWidth);
        double maxTop = Math.Max(workArea.Top, workArea.Bottom - safeHeight);

        double clampedX = Math.Clamp(targetX, workArea.Left, maxLeft);
        double clampedY = Math.Clamp(targetY, workArea.Top, maxTop);

        return (clampedX, clampedY);
    }
}
