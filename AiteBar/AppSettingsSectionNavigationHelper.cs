namespace AiteBar;

public enum AppSettingsSection
{
    General,
    Contexts,
    Hotkeys,
    QuickTools,
    AiProviders,
    About
}

internal static class AppSettingsSectionNavigationHelper
{
    private const double BottomTolerance = 1d;

    public static double GetTargetOffset(double sectionTop, double scrollableHeight)
    {
        double safeTop = double.IsFinite(sectionTop) ? sectionTop : 0d;
        double safeScrollableHeight = double.IsFinite(scrollableHeight)
            ? Math.Max(0d, scrollableHeight)
            : 0d;

        return Math.Clamp(safeTop, 0d, safeScrollableHeight);
    }

    public static int GetActiveSectionIndex(
        IReadOnlyList<double> sectionTops,
        double verticalOffset,
        double viewportHeight,
        double extentHeight,
        double activationInset = 24d)
    {
        if (sectionTops.Count == 0)
        {
            return -1;
        }

        double safeOffset = double.IsFinite(verticalOffset) ? Math.Max(0d, verticalOffset) : 0d;
        double safeViewportHeight = double.IsFinite(viewportHeight) ? Math.Max(0d, viewportHeight) : 0d;
        double safeExtentHeight = double.IsFinite(extentHeight) ? Math.Max(0d, extentHeight) : 0d;
        double safeActivationInset = double.IsFinite(activationInset) ? Math.Max(0d, activationInset) : 0d;

        if (safeExtentHeight > 0d && safeOffset + safeViewportHeight >= safeExtentHeight - BottomTolerance)
        {
            return sectionTops.Count - 1;
        }

        double marker = safeOffset + safeActivationInset;
        int activeIndex = 0;
        for (int i = 1; i < sectionTops.Count; i++)
        {
            double sectionTop = double.IsFinite(sectionTops[i]) ? sectionTops[i] : double.PositiveInfinity;
            if (sectionTop > marker)
            {
                break;
            }

            activeIndex = i;
        }

        return activeIndex;
    }
}
