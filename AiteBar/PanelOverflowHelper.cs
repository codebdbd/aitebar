namespace AiteBar;

public static class PanelOverflowHelper
{
    public readonly record struct OverflowPlan(
        int Capacity,
        int VisibleItemCount,
        int HiddenItemCount)
    {
        public bool HasOverflow => HiddenItemCount > 0;
    }

    public static OverflowPlan Calculate(
        PanelLayoutHelper.PanelLayoutMetrics metrics,
        int itemCount)
    {
        int normalizedCount = Math.Max(0, itemCount);
        int capacity = metrics.IsVertical
            ? CalculateVerticalCapacity(metrics)
            : CalculateHorizontalCapacity(metrics);

        capacity = Math.Max(1, capacity);
        if (normalizedCount <= capacity)
        {
            return new OverflowPlan(capacity, normalizedCount, 0);
        }

        int visibleItemCount = Math.Max(0, capacity - 1);
        return new OverflowPlan(
            capacity,
            visibleItemCount,
            normalizedCount - visibleItemCount);
    }

    private static int CalculateHorizontalCapacity(
        PanelLayoutHelper.PanelLayoutMetrics metrics)
    {
        int columns = GetCapacity(metrics.UserWidth);
        int rows = Math.Min(
            PanelLayoutHelper.MaxUserBands,
            GetCapacity(metrics.UserHeight));
        return columns * rows;
    }

    private static int CalculateVerticalCapacity(
        PanelLayoutHelper.PanelLayoutMetrics metrics)
    {
        int bands = Math.Min(
            PanelLayoutHelper.MaxUserBands,
            GetCapacity(metrics.UserWidth));
        int firstBand = GetCapacity(metrics.UserHeight - metrics.UserLeadingReserve);
        if (bands == 1)
        {
            return firstBand;
        }

        int overflowBand = GetCapacity(metrics.UserHeight - metrics.UserOverflowReserve);
        return firstBand + ((bands - 1) * overflowBand);
    }

    private static int GetCapacity(double available) =>
        Math.Max(1, (int)Math.Floor(Math.Max(0, available) / PanelLayoutHelper.ButtonOuterSize));
}
