using System.Drawing;
using AiteBar;

namespace AiteBar.Tests;

public sealed class QuickNoteLayoutHelperTests
{
    private static readonly Rectangle WorkArea = new(100, 200, 1200, 800);

    [Fact]
    public void EdgeClearance_LeavesRoomForPanelChromeAndButton()
    {
        Assert.Equal(70, QuickNoteLayoutHelper.EdgeClearance);
    }

    [Theory]
    [InlineData(DockEdge.Top)]
    [InlineData(DockEdge.Bottom)]
    public void GetSlideCoordinates_HorizontalEdges_CentersWindowOnX(DockEdge edge)
    {
        var coordinates = QuickNoteLayoutHelper.GetSlideCoordinates(edge, WorkArea, width: 580, height: 430);

        Assert.Equal(410, coordinates.ShownX);
        Assert.Equal(coordinates.ShownX, coordinates.HiddenX);
    }

    [Theory]
    [InlineData(DockEdge.Left)]
    [InlineData(DockEdge.Right)]
    public void GetSlideCoordinates_VerticalEdges_CentersWindowOnY(DockEdge edge)
    {
        var coordinates = QuickNoteLayoutHelper.GetSlideCoordinates(edge, WorkArea, width: 580, height: 430);

        Assert.Equal(385, coordinates.ShownY);
        Assert.Equal(coordinates.ShownY, coordinates.HiddenY);
    }

    [Fact]
    public void GetSlideCoordinates_Top_PlacesShownWindowBelowPanelClearance()
    {
        var coordinates = QuickNoteLayoutHelper.GetSlideCoordinates(DockEdge.Top, WorkArea, width: 580, height: 430);

        Assert.Equal(270, coordinates.ShownY);
        Assert.Equal(-300, coordinates.HiddenY);
    }

    [Fact]
    public void GetSlideCoordinates_Bottom_PlacesShownWindowAbovePanelClearance()
    {
        var coordinates = QuickNoteLayoutHelper.GetSlideCoordinates(DockEdge.Bottom, WorkArea, width: 580, height: 430);

        Assert.Equal(500, coordinates.ShownY);
        Assert.Equal(1070, coordinates.HiddenY);
    }

    [Fact]
    public void GetSlideCoordinates_Left_PlacesShownWindowRightOfPanelClearance()
    {
        var coordinates = QuickNoteLayoutHelper.GetSlideCoordinates(DockEdge.Left, WorkArea, width: 580, height: 430);

        Assert.Equal(170, coordinates.ShownX);
        Assert.Equal(-550, coordinates.HiddenX);
    }

    [Fact]
    public void GetSlideCoordinates_Right_PlacesShownWindowLeftOfPanelClearance()
    {
        var coordinates = QuickNoteLayoutHelper.GetSlideCoordinates(DockEdge.Right, WorkArea, width: 580, height: 430);

        Assert.Equal(650, coordinates.ShownX);
        Assert.Equal(1370, coordinates.HiddenX);
    }

    [Fact]
    public void ClampBoundsToWorkArea_UsesDefaultsWhenBoundsAreMissing()
    {
        var bounds = QuickNoteLayoutHelper.ClampBoundsToWorkArea(WorkArea, null, null, null, null);

        Assert.Equal(410, bounds.Left);
        Assert.Equal(385, bounds.Top);
        Assert.Equal(QuickNoteLayoutHelper.DefaultWidth, bounds.Width);
        Assert.Equal(QuickNoteLayoutHelper.DefaultHeight, bounds.Height);
    }

    [Fact]
    public void ClampBoundsToWorkArea_KeepsWindowInsideWorkArea()
    {
        var bounds = QuickNoteLayoutHelper.ClampBoundsToWorkArea(WorkArea, left: 2000, top: -100, width: 700, height: 500);

        Assert.Equal(600, bounds.Left);
        Assert.Equal(200, bounds.Top);
        Assert.Equal(700, bounds.Width);
        Assert.Equal(500, bounds.Height);
    }

    [Fact]
    public void ClampBoundsToWorkArea_EnforcesMinimumSize()
    {
        var bounds = QuickNoteLayoutHelper.ClampBoundsToWorkArea(WorkArea, left: 100, top: 200, width: 100, height: 100);

        Assert.Equal(QuickNoteLayoutHelper.MinWidth, bounds.Width);
        Assert.Equal(QuickNoteLayoutHelper.MinHeight, bounds.Height);
    }

    [Fact]
    public void SelectWorkArea_UsesSecondaryMonitorContainingSavedBounds()
    {
        Rectangle primary = new(0, 0, 1920, 1080);
        Rectangle secondary = new(1920, 0, 2560, 1440);

        Rectangle selected = QuickNoteLayoutHelper.SelectWorkArea(
            [primary, secondary],
            left: 2300,
            top: 200,
            width: 580,
            height: 430);

        Assert.Equal(secondary, selected);
    }

    [Fact]
    public void SelectWorkArea_SupportsNegativeMonitorCoordinates()
    {
        Rectangle primary = new(0, 0, 1920, 1080);
        Rectangle leftMonitor = new(-1920, 0, 1920, 1080);

        Rectangle selected = QuickNoteLayoutHelper.SelectWorkArea(
            [primary, leftMonitor],
            left: -1500,
            top: 100,
            width: 580,
            height: 430);

        Assert.Equal(leftMonitor, selected);
    }

    [Fact]
    public void SelectWorkArea_UsesNearestMonitorWhenSavedMonitorWasRemoved()
    {
        Rectangle primary = new(0, 0, 1920, 1080);
        Rectangle rightMonitor = new(1920, 0, 1920, 1080);

        Rectangle selected = QuickNoteLayoutHelper.SelectWorkArea(
            [primary, rightMonitor],
            left: 5000,
            top: 100,
            width: 580,
            height: 430);

        Assert.Equal(rightMonitor, selected);
    }
}
