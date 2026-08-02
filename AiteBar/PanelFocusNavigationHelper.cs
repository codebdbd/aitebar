using System.Windows;

namespace AiteBar;

public enum PanelNavigationDirection
{
    Left,
    Right,
    Up,
    Down
}

public static class PanelFocusNavigationHelper
{
    public static int FindNextIndex(
        IReadOnlyList<Rect> bounds,
        int currentIndex,
        PanelNavigationDirection direction)
    {
        if (currentIndex < 0 || currentIndex >= bounds.Count)
        {
            return bounds.Count > 0 ? 0 : -1;
        }

        Point currentCenter = GetCenter(bounds[currentIndex]);
        int bestIndex = -1;
        double bestScore = double.MaxValue;

        for (int index = 0; index < bounds.Count; index++)
        {
            if (index == currentIndex)
            {
                continue;
            }

            Point candidateCenter = GetCenter(bounds[index]);
            double deltaX = candidateCenter.X - currentCenter.X;
            double deltaY = candidateCenter.Y - currentCenter.Y;
            (double primary, double cross) = direction switch
            {
                PanelNavigationDirection.Left => (-deltaX, Math.Abs(deltaY)),
                PanelNavigationDirection.Right => (deltaX, Math.Abs(deltaY)),
                PanelNavigationDirection.Up => (-deltaY, Math.Abs(deltaX)),
                PanelNavigationDirection.Down => (deltaY, Math.Abs(deltaX)),
                _ => (double.NegativeInfinity, double.PositiveInfinity)
            };

            if (primary <= 0)
            {
                continue;
            }

            double score = primary + (cross * 2);
            if (score < bestScore)
            {
                bestScore = score;
                bestIndex = index;
            }
        }

        return bestIndex;
    }

    private static Point GetCenter(Rect bounds) =>
        new(bounds.Left + (bounds.Width / 2), bounds.Top + (bounds.Height / 2));
}
