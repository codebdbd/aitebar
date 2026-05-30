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
}
