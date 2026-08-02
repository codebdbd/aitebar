using System.Windows;

namespace AiteBar.Tests;

public sealed class PanelFocusNavigationHelperTests
{
    private static readonly Rect[] Grid =
    [
        new Rect(0, 0, 44, 44),
        new Rect(44, 0, 44, 44),
        new Rect(88, 0, 44, 44),
        new Rect(0, 44, 44, 44),
        new Rect(44, 44, 44, 44),
        new Rect(88, 44, 44, 44)
    ];

    [Theory]
    [InlineData(4, PanelNavigationDirection.Left, 3)]
    [InlineData(4, PanelNavigationDirection.Right, 5)]
    [InlineData(4, PanelNavigationDirection.Up, 1)]
    [InlineData(1, PanelNavigationDirection.Down, 4)]
    public void FindNextIndex_SelectsSpatialNeighbour(
        int current,
        PanelNavigationDirection direction,
        int expected)
    {
        int result = PanelFocusNavigationHelper.FindNextIndex(Grid, current, direction);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void FindNextIndex_AtOuterEdge_DoesNotWrapToUnrelatedButton()
    {
        int result = PanelFocusNavigationHelper.FindNextIndex(
            Grid,
            currentIndex: 0,
            PanelNavigationDirection.Left);

        Assert.Equal(-1, result);
    }

    [Fact]
    public void FindNextIndex_WithoutCurrentFocus_StartsAtFirstButton()
    {
        int result = PanelFocusNavigationHelper.FindNextIndex(
            Grid,
            currentIndex: -1,
            PanelNavigationDirection.Down);

        Assert.Equal(0, result);
    }
}
