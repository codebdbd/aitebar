using System.Drawing;
using System.Windows;
using AiteBar;

namespace AiteBar.Tests;

public sealed class PanelPositionHelperTests
{
    private static readonly Rect WorkArea = new(10, 20, 1000, 700);
    private static readonly Rect Bounds = new(0, 0, 1200, 800);

    [Theory]
    [InlineData(DockEdge.Top, false, 410, 32)]
    [InlineData(DockEdge.Top, true, 410, -80)]
    [InlineData(DockEdge.Bottom, false, 410, 640)]
    [InlineData(DockEdge.Bottom, true, 410, 800)]
    [InlineData(DockEdge.Left, false, 10, 330)]
    [InlineData(DockEdge.Left, true, -200, 330)]
    [InlineData(DockEdge.Right, false, 810, 330)]
    [InlineData(DockEdge.Right, true, 1200, 330)]
    public void GetDockCoordinates_PositionsPanelForEachEdge(DockEdge edge, bool hide, double expectedX, double expectedY)
    {
        var coordinates = PanelPositionHelper.GetDockCoordinates(
            edge,
            WorkArea,
            Bounds,
            panelWidth: 200,
            panelHeight: 80,
            topPanelVisibleOffset: 12,
            hide: hide);

        Assert.Equal(expectedX, coordinates.X);
        Assert.Equal(expectedY, coordinates.Y);
    }

    [Fact]
    public void GetDockCoordinates_DoesNotCenterToNegativeOffsetWhenPanelIsWiderThanWorkArea()
    {
        var coordinates = PanelPositionHelper.GetDockCoordinates(
            DockEdge.Top,
            WorkArea,
            Bounds,
            panelWidth: 1400,
            panelHeight: 80,
            topPanelVisibleOffset: 12,
            hide: false);

        Assert.Equal(WorkArea.Left, coordinates.X);
        Assert.Equal(WorkArea.Top + 12, coordinates.Y);
    }

    [Fact]
    public void GetClosestDockEdge_UsesNearestEdge()
    {
        var workArea = new Rectangle(0, 0, 1000, 800);

        DockEdge edge = PanelPositionHelper.GetClosestDockEdge(workArea, cursorX: 995, cursorY: 500, DockEdge.Top);

        Assert.Equal(DockEdge.Right, edge);
    }

    [Fact]
    public void GetClosestDockEdge_AppliesHysteresisToCurrentEdge()
    {
        var workArea = new Rectangle(0, 0, 1000, 800);

        DockEdge edge = PanelPositionHelper.GetClosestDockEdge(workArea, cursorX: 70, cursorY: 20, DockEdge.Left);

        Assert.Equal(DockEdge.Left, edge);
    }

    [Fact]
    public void FindScreenIndex_ReturnsMatchingIndexIgnoringCase()
    {
        int index = PanelPositionHelper.FindScreenIndex(
            ["\\\\.\\DISPLAY1", "\\\\.\\DISPLAY2"],
            "\\\\.\\display2");

        Assert.Equal(1, index);
    }

    [Fact]
    public void FindScreenIndex_ReturnsPrimaryIndexWhenTargetIsMissing()
    {
        int index = PanelPositionHelper.FindScreenIndex(
            ["\\\\.\\DISPLAY1", "\\\\.\\DISPLAY2"],
            "\\\\.\\DISPLAY3");

        Assert.Equal(0, index);
    }
}
