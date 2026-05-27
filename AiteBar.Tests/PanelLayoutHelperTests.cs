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
        Assert.Equal(370, metrics.UserHeight);
        Assert.Equal(1, metrics.UserBands);
        Assert.Equal(238, metrics.UserLeadingReserve);
    }

    [Fact]
    public void Calculate_Vertical_UsesOneUserColumnWhenButtonsFit()
    {
        var metrics = PanelLayoutHelper.Calculate(
            isVertical: true,
            availablePrimary: 800,
            panelPercent: 100,
            visibleSystemButtonCount: 0,
            controlButtonCount: 1,
            contextCounts: [12],
            activeContextIndex: 0);

        Assert.Equal(52, metrics.PanelWidth);
        Assert.Equal(589, metrics.PanelHeight);
        Assert.Equal(44, metrics.UserWidth);
        Assert.Equal(581, metrics.UserHeight);
        Assert.Equal(1, metrics.UserBands);
        Assert.Equal(53, metrics.UserLeadingReserve);
    }

    [Fact]
    public void Calculate_Vertical_AddsSecondUserColumnOnlyOnOverflow()
    {
        var metrics = PanelLayoutHelper.Calculate(
            isVertical: true,
            availablePrimary: 800,
            panelPercent: 40,
            visibleSystemButtonCount: 0,
            controlButtonCount: 1,
            contextCounts: [12],
            activeContextIndex: 0);

        Assert.Equal(96, metrics.PanelWidth);
        Assert.Equal(320, metrics.PanelHeight);
        Assert.Equal(88, metrics.UserWidth);
        Assert.Equal(312, metrics.UserHeight);
        Assert.Equal(2, metrics.UserBands);
        Assert.Equal(53, metrics.UserLeadingReserve);
        Assert.Equal(53, metrics.UserOverflowReserve);
    }

    [Fact]
    public void Calculate_Vertical_UsesActiveContextWidth()
    {
        var metrics = PanelLayoutHelper.Calculate(
            isVertical: true,
            availablePrimary: 800,
            panelPercent: 40,
            visibleSystemButtonCount: 0,
            controlButtonCount: 1,
            contextCounts: [12, 4],
            activeContextIndex: 1);

        Assert.Equal(52, metrics.PanelWidth);
        Assert.Equal(44, metrics.UserWidth);
        Assert.Equal(1, metrics.UserBands);
    }

    [Fact]
    public void Calculate_Vertical_UsesOneSystemColumnWhenButtonsFit()
    {
        var metrics = PanelLayoutHelper.Calculate(
            isVertical: true,
            availablePrimary: 800,
            panelPercent: 100,
            visibleSystemButtonCount: 8,
            controlButtonCount: 1,
            contextCounts: [0],
            activeContextIndex: 0);

        Assert.Equal(52, metrics.PanelWidth);
        Assert.Equal(413, metrics.PanelHeight);
        Assert.Equal(44, metrics.FixedWidth);
        Assert.Equal(405, metrics.FixedHeight);
    }

    [Fact]
    public void Calculate_Vertical_WrapsSystemButtonsIntoTwoColumnsOnOverflow()
    {
        var metrics = PanelLayoutHelper.Calculate(
            isVertical: true,
            availablePrimary: 250,
            panelPercent: 100,
            visibleSystemButtonCount: 8,
            controlButtonCount: 1,
            contextCounts: [0],
            activeContextIndex: 0);

        Assert.Equal(96, metrics.PanelWidth);
        Assert.Equal(237, metrics.PanelHeight);
        Assert.Equal(88, metrics.FixedWidth);
        Assert.Equal(229, metrics.FixedHeight);
    }

    [Fact]
    public void Calculate_Vertical_KeepsSystemButtonsInOneColumnWhenOnlyUserButtonsOverflow()
    {
        var metrics = PanelLayoutHelper.Calculate(
            isVertical: true,
            availablePrimary: 800,
            panelPercent: 70,
            visibleSystemButtonCount: 8,
            controlButtonCount: 1,
            contextCounts: [12],
            activeContextIndex: 0);

        Assert.Equal(96, metrics.PanelWidth);
        Assert.Equal(44, metrics.FixedWidth);
        Assert.Equal(414, metrics.FixedHeight);
        Assert.Equal(88, metrics.UserWidth);
        Assert.Equal(546, metrics.UserHeight);
        Assert.Equal(2, metrics.UserBands);
        Assert.Equal(414, metrics.UserLeadingReserve);
    }

    [Fact]
    public void Calculate_Vertical_DoesNotForceUserWidthToMatchSystemWidth()
    {
        var metrics = PanelLayoutHelper.Calculate(
            isVertical: true,
            availablePrimary: 800,
            panelPercent: 60,
            visibleSystemButtonCount: 8,
            controlButtonCount: 1,
            contextCounts: [10],
            activeContextIndex: 0);

        Assert.Equal(96, metrics.PanelWidth);
        Assert.Equal(44, metrics.FixedWidth);
        Assert.Equal(88, metrics.UserWidth);
        Assert.Equal(2, metrics.UserBands);
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
        Assert.Equal(53, metrics.FixedWidth);
    }

    [Fact]
    public void Calculate_Horizontal_ReservesPrimaryContextUtilityWidthForOtherContexts()
    {
        var metrics = PanelLayoutHelper.Calculate(
            isVertical: false,
            availablePrimary: 600,
            panelPercent: 100,
            visibleSystemButtonCount: 4,
            controlButtonCount: 1,
            contextCounts: [1, 2, 0, 0],
            activeContextIndex: 1);

        Assert.Equal(290, metrics.PanelWidth);
        Assert.Equal(52, metrics.PanelHeight);
        Assert.Equal(88, metrics.UserWidth);
        Assert.Equal(44, metrics.UserHeight);
        Assert.Equal(53, metrics.FixedWidth);
    }

    [Fact]
    public void Calculate_Horizontal_ReservesTrailingSettingsButton()
    {
        var metrics = PanelLayoutHelper.Calculate(
            isVertical: false,
            availablePrimary: 600,
            panelPercent: 100,
            visibleSystemButtonCount: 0,
            controlButtonCount: 1,
            contextCounts: [4],
            activeContextIndex: 0,
            trailingControlButtonCount: 1);

        Assert.Equal(53, metrics.FixedWidth);
        Assert.Equal(53, metrics.TrailingWidth);
        Assert.Equal(176, metrics.UserWidth);
        Assert.Equal(290, metrics.PanelWidth);
    }

    [Fact]
    public void Calculate_Horizontal_KeepsTrailingSettingsButtonWhenPanelIsEmpty()
    {
        var metrics = PanelLayoutHelper.Calculate(
            isVertical: false,
            availablePrimary: 600,
            panelPercent: 100,
            visibleSystemButtonCount: 0,
            controlButtonCount: 1,
            contextCounts: [0],
            activeContextIndex: 0,
            trailingControlButtonCount: 1);

        Assert.Equal(44, metrics.TrailingWidth);
        Assert.Equal(0, metrics.UserWidth);
        Assert.Equal(96, metrics.PanelWidth);
    }

    [Fact]
    public void Calculate_Vertical_ReservesTrailingSettingsButtonAtBottom()
    {
        var metrics = PanelLayoutHelper.Calculate(
            isVertical: true,
            availablePrimary: 600,
            panelPercent: 100,
            visibleSystemButtonCount: 0,
            controlButtonCount: 1,
            contextCounts: [4],
            activeContextIndex: 0,
            trailingControlButtonCount: 1);

        Assert.Equal(53, metrics.FixedHeight);
        Assert.Equal(53, metrics.TrailingHeight);
        Assert.Equal(229, metrics.UserHeight);
        Assert.Equal(290, metrics.PanelHeight);
        Assert.Equal(53, metrics.UserLeadingReserve);
    }
}
