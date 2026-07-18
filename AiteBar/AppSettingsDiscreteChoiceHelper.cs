namespace AiteBar;

internal static class AppSettingsDiscreteChoiceHelper
{
    public static IReadOnlyList<int> PanelSizeValues { get; } = [50, 70, 90, 100];
    public static IReadOnlyList<int> ActivationZoneValues { get; } = [10, 30, 50, 100];
    public static IReadOnlyList<int> ActivationDelayValues { get; } = [100, 200, 300, 500];

    public static int GetNearestIndex(IReadOnlyList<int> values, double value)
    {
        if (values.Count == 0)
        {
            return 0;
        }

        int nearestIndex = 0;
        double nearestDistance = Math.Abs(value - values[0]);
        for (int i = 1; i < values.Count; i++)
        {
            double distance = Math.Abs(value - values[i]);
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearestIndex = i;
            }
        }

        return nearestIndex;
    }

    public static int GetValue(IReadOnlyList<int> values, double index)
    {
        if (values.Count == 0)
        {
            return 0;
        }

        int safeIndex = Math.Clamp((int)Math.Round(index), 0, values.Count - 1);
        return values[safeIndex];
    }
}
