namespace AiteBar.Tests;

public sealed class PanelOverflowHelperTests
{
    [Fact]
    public void Calculate_HorizontalOverflow_ReservesLastSlotForMoreButton()
    {
        var metrics = CreateMetrics(isVertical: false, userWidth: 132, userHeight: 88);

        PanelOverflowHelper.OverflowPlan plan = PanelOverflowHelper.Calculate(metrics, 10);

        Assert.Equal(6, plan.Capacity);
        Assert.Equal(5, plan.VisibleItemCount);
        Assert.Equal(5, plan.HiddenItemCount);
        Assert.True(plan.HasOverflow);
    }

    [Fact]
    public void Calculate_VerticalOverflow_AccountsForReservedSpaceInEveryColumn()
    {
        var metrics = CreateMetrics(
            isVertical: true,
            userWidth: 132,
            userHeight: 176,
            leadingReserve: 44,
            overflowReserve: 88);

        PanelOverflowHelper.OverflowPlan plan = PanelOverflowHelper.Calculate(metrics, 10);

        Assert.Equal(7, plan.Capacity);
        Assert.Equal(6, plan.VisibleItemCount);
        Assert.Equal(4, plan.HiddenItemCount);
    }

    [Fact]
    public void Calculate_WhenEverythingFits_DoesNotReplaceAnAction()
    {
        var metrics = CreateMetrics(isVertical: false, userWidth: 132, userHeight: 88);

        PanelOverflowHelper.OverflowPlan plan = PanelOverflowHelper.Calculate(metrics, 6);

        Assert.Equal(6, plan.VisibleItemCount);
        Assert.Equal(0, plan.HiddenItemCount);
        Assert.False(plan.HasOverflow);
    }

    private static PanelLayoutHelper.PanelLayoutMetrics CreateMetrics(
        bool isVertical,
        double userWidth,
        double userHeight,
        double leadingReserve = 0,
        double overflowReserve = 0) =>
        new(
            IsVertical: isVertical,
            PanelWidth: userWidth,
            PanelHeight: userHeight,
            FixedWidth: 0,
            FixedHeight: 0,
            TrailingWidth: 0,
            TrailingHeight: 0,
            UserWidth: userWidth,
            UserHeight: userHeight,
            UserBands: isVertical ? (int)(userWidth / PanelLayoutHelper.ButtonOuterSize) : (int)(userHeight / PanelLayoutHelper.ButtonOuterSize),
            SystemWidth: 0,
            SystemHeight: 0,
            UserLeadingReserve: leadingReserve,
            UserOverflowReserve: overflowReserve,
            UseMultiColumnControls: false);
}
