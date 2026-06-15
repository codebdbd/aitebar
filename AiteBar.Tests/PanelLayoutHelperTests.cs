using System.Collections.Generic;
using AiteBar;
using Xunit;

namespace AiteBar.Tests;

public sealed class PanelLayoutHelperTests
{
    [Fact]
    public void Calculate_Horizontal_UsesTotalButtonsForPanelWidth()
    {
        var metrics = PanelLayoutHelper.Calculate(
            isVertical: false,
            availablePrimary: 600,
            panelPercent: 100,
            totalButtonCount: 6,
            controlButtonCount: 1,
            trailingControlButtonCount: 0);

        Assert.Equal(325, metrics.PanelWidth); // 44 (control) + 9 (sep) + (6*44) + 8 (chrome)
        Assert.Equal(52, metrics.PanelHeight);
        Assert.Equal(264, metrics.UserWidth);
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
            totalButtonCount: 14,
            controlButtonCount: 1,
            trailingControlButtonCount: 0);

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
            totalButtonCount: 7,
            controlButtonCount: 1,
            trailingControlButtonCount: 0);

        Assert.Equal(52, metrics.PanelWidth);
        Assert.Equal(369, metrics.PanelHeight);
        Assert.Equal(44, metrics.UserWidth);
        Assert.Equal(361, metrics.UserHeight);
        Assert.Equal(1, metrics.UserBands);
        Assert.Equal(53, metrics.UserLeadingReserve);
    }

    [Fact]
    public void Calculate_Vertical_UsesOneColumnWhenButtonsFit()
    {
        var metrics = PanelLayoutHelper.Calculate(
            isVertical: true,
            availablePrimary: 800,
            panelPercent: 100,
            totalButtonCount: 12,
            controlButtonCount: 1,
            trailingControlButtonCount: 0);

        Assert.Equal(52, metrics.PanelWidth);
        Assert.Equal(589, metrics.PanelHeight);
        Assert.Equal(44, metrics.UserWidth);
        Assert.Equal(581, metrics.UserHeight);
        Assert.Equal(1, metrics.UserBands);
        Assert.Equal(53, metrics.UserLeadingReserve);
    }

    [Fact]
    public void Calculate_Vertical_AddsSecondColumnOnlyOnOverflow()
    {
        var metrics = PanelLayoutHelper.Calculate(
            isVertical: true,
            availablePrimary: 800,
            panelPercent: 50, // Минимально допустимое значение
            totalButtonCount: 12,
            controlButtonCount: 1,
            trailingControlButtonCount: 0);

        Assert.Equal(96, metrics.PanelWidth);
        Assert.Equal(369, metrics.PanelHeight);
        Assert.Equal(88, metrics.UserWidth);
        Assert.Equal(361, metrics.UserHeight);
        Assert.Equal(2, metrics.UserBands);
        Assert.Equal(53, metrics.UserLeadingReserve);
        Assert.Equal(metrics.UserLeadingReserve, metrics.UserOverflowReserve);
    }

    [Fact]
    public void Calculate_NoButtons_UsesOnlyControlButtons()
    {
        var metrics = PanelLayoutHelper.Calculate(
            isVertical: false,
            availablePrimary: 500,
            panelPercent: 80,
            totalButtonCount: 0,
            controlButtonCount: 1,
            trailingControlButtonCount: 0);

        Assert.Equal(52, metrics.PanelWidth);
        Assert.Equal(52, metrics.PanelHeight);
        Assert.Equal(0, metrics.UserWidth);
        Assert.Equal(0, metrics.UserHeight);
        Assert.Equal(0, metrics.UserBands);
    }

    [Fact]
    public void Calculate_Horizontal_ReservesTrailingSettingsButton()
    {
        var metrics = PanelLayoutHelper.Calculate(
            isVertical: false,
            availablePrimary: 600,
            panelPercent: 100,
            totalButtonCount: 4,
            controlButtonCount: 1,
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
            totalButtonCount: 0,
            controlButtonCount: 1,
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
            totalButtonCount: 4,
            controlButtonCount: 1,
            trailingControlButtonCount: 1);

        Assert.Equal(53, metrics.FixedHeight);
        Assert.Equal(53, metrics.TrailingHeight);
        Assert.Equal(53 + 4 * 44, metrics.UserHeight);
        Assert.Equal(290, metrics.PanelHeight);
        Assert.Equal(53, metrics.UserLeadingReserve);
    }

    [Fact]
    public void Calculate_Vertical_KeepsTrailingSettingsButtonWhenPanelIsEmpty()
    {
        var metrics = PanelLayoutHelper.Calculate(
            isVertical: true,
            availablePrimary: 600,
            panelPercent: 100,
            totalButtonCount: 0,
            controlButtonCount: 1,
            trailingControlButtonCount: 1);

        Assert.Equal(44, metrics.TrailingHeight);
        Assert.Equal(44, metrics.UserHeight);
        Assert.Equal(96, metrics.PanelHeight);
    }

    [Fact]
    public void Calculate_Vertical_WithManyButtons_ReservesTrailingSettingsButtonAtBottom()
    {
        var metrics = PanelLayoutHelper.Calculate(
            isVertical: true,
            availablePrimary: 1200,
            panelPercent: 100,
            totalButtonCount: 20,
            controlButtonCount: 1,
            trailingControlButtonCount: 1);

        Assert.Equal(53, metrics.FixedHeight);
        Assert.Equal(53, metrics.TrailingHeight);
        Assert.Equal(53 + 20 * 44, metrics.UserHeight);
        Assert.Equal(994, metrics.PanelHeight);
        Assert.Equal(53, metrics.UserLeadingReserve);
    }
}
