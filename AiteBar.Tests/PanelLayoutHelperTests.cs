using System.Collections.Generic;
using AiteBar;
using Xunit;

namespace AiteBar.Tests;

public sealed class PanelLayoutHelperTests
{
    [Fact]
    public void Calculate_Horizontal_UsesWidestContextForPanelWidth()
    {
        var metrics = PanelLayoutHelper.Calculate(
            isVertical: false,
            availablePrimary: 600,
            panelPercent: 100,
            visibleSystemButtonCount: 4,
            controlButtonCount: 1,
            contextCounts: [2, 8, 1, 0],
            activeContextIndex: 0);

        Assert.Equal(413, metrics.PanelWidth);
        Assert.Equal(52, metrics.PanelHeight);
        Assert.Equal(88, metrics.UserWidth);
        Assert.Equal(44, metrics.UserHeight);
        Assert.Equal(1, metrics.UserBands);
    }

    [Fact]
    public void Calculate_Horizontal_LimitsUserAreaToTwoRows()
    {
        var metrics = PanelLayoutHelper.Calculate(
            isVertical: false,
            availablePrimary: 400,
            panelPercent: 100,
            visibleSystemButtonCount: 0,
            controlButtonCount: 1,
            contextCounts: [14],
            activeContextIndex: 0);

        Assert.Equal(2, metrics.UserBands);
        Assert.Equal(369, metrics.PanelWidth);
        Assert.Equal(96, metrics.PanelHeight);
        Assert.Equal(308, metrics.UserWidth);
        Assert.Equal(88, metrics.UserHeight);
    }

    [Fact]
    public void Calculate_Vertical_MirrorsGeometryAcrossAxes()
    {
        var metrics = PanelLayoutHelper.Calculate(
            isVertical: true,
            availablePrimary: 500,
            panelPercent: 80,
            visibleSystemButtonCount: 4,
            controlButtonCount: 1,
            contextCounts: [3, 6, 1, 0],
            activeContextIndex: 0);

        Assert.Equal(52, metrics.PanelWidth);
        Assert.Equal(378, metrics.PanelHeight);
        Assert.Equal(44, metrics.UserWidth);
        Assert.Equal(132, metrics.UserHeight);
        Assert.Equal(1, metrics.UserBands);
    }

    [Fact]
    public void Calculate_NoUserButtons_UsesOnlyFixedBlock()
    {
        var metrics = PanelLayoutHelper.Calculate(
            isVertical: false,
            availablePrimary: 500,
            panelPercent: 80,
            visibleSystemButtonCount: 4,
            controlButtonCount: 1,
            contextCounts: [0, 0, 0, 0],
            activeContextIndex: 0);

        Assert.Equal(237, metrics.PanelWidth);
        Assert.Equal(52, metrics.PanelHeight);
        Assert.Equal(0, metrics.UserWidth);
        Assert.Equal(0, metrics.UserHeight);
        Assert.Equal(0, metrics.UserBands);
    }

    [Fact]
    public void Calculate_Horizontal_KeepsPrimaryUtilityWidthForPrimaryContextButUsesWidestTotalContext()
    {
        var metrics = PanelLayoutHelper.Calculate(
            isVertical: false,
            availablePrimary: 600,
            panelPercent: 100,
            visibleSystemButtonCount: 4,
            controlButtonCount: 1,
            contextCounts: [2, 8, 1, 0],
            activeContextIndex: 1);

        Assert.Equal(413, metrics.PanelWidth);
        Assert.Equal(52, metrics.PanelHeight);
        Assert.Equal(352, metrics.UserWidth);
        Assert.Equal(44, metrics.UserHeight);
        Assert.Equal(1, metrics.UserBands);
        Assert.Equal(238, metrics.FixedWidth);
    }
}
