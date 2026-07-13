namespace AiteBar;

internal static class AppSettingsSelectionHelper
{
    public static double ResolveSegmentedValue(double currentValue, int selectedValue, bool selectionChanged)
    {
        return selectionChanged ? selectedValue : currentValue;
    }

    public static int ResolveSegmentedValue(int currentValue, int selectedValue, bool selectionChanged)
    {
        return selectionChanged ? selectedValue : currentValue;
    }

    public static int ResolveMonitorIndex(int currentMonitorIndex, bool showOnSecondaryMonitor)
    {
        return showOnSecondaryMonitor ? Math.Max(1, currentMonitorIndex) : 0;
    }
}
