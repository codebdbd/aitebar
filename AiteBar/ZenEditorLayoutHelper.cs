using System;

namespace AiteBar;

internal static class ZenEditorLayoutHelper
{
    public const double DefaultSideSafetyMargin = 48.0;
    public const double MinEffectiveZoneWidth = 20.0;
    public const double HorizontalMarginPadding = 64.0;
    public const double TopMarginRatio = 0.18;
    public const double BottomMargin = 32.0;
    public const double SideMargin = 32.0;

    public static double CalculateEditorWidth(double containerWidth, double themeColumnWidth)
    {
        if (containerWidth <= 0 || double.IsNaN(containerWidth))
        {
            return 0;
        }

        double availableWidth = Math.Max(0, containerWidth - HorizontalMarginPadding);
        return Math.Min(themeColumnWidth, availableWidth);
    }

    /// <summary>
    /// Calculates the width of the exit zone along the screen edge so that it covers
    /// the empty margin area up to the text editor, minus the safety frame (<paramref name="safetyMargin"/>).
    /// </summary>
    public static double CalculateExitZoneWidth(
        double containerWidth,
        double editorWidth,
        double safetyMargin = DefaultSideSafetyMargin,
        double minEffectiveWidth = MinEffectiveZoneWidth)
    {
        if (containerWidth <= 0 || editorWidth <= 0 || double.IsNaN(containerWidth) || double.IsNaN(editorWidth))
        {
            return 0;
        }

        if (double.IsNaN(safetyMargin) || safetyMargin < 0)
        {
            safetyMargin = DefaultSideSafetyMargin;
        }

        double sideMargin = Math.Max(0, (containerWidth - editorWidth) / 2);
        double effectiveZoneWidth = Math.Max(0, sideMargin - safetyMargin);

        return effectiveZoneWidth < minEffectiveWidth ? 0 : effectiveZoneWidth;
    }
}
