using AiteBar;
using Xunit;

namespace AiteBar.Tests;

public sealed class ActivationZoneHelperTests
{
    [Theory]
    [InlineData(DockEdge.Top, 960, 1)]
    [InlineData(DockEdge.Bottom, 960, 1078)]
    [InlineData(DockEdge.Left, 1, 500)]
    [InlineData(DockEdge.Right, 1918, 500)]
    public void IsInActivationZone_AllowsSmallEdgeTolerance(DockEdge edge, double x, double y)
    {
        bool result = ActivationZoneHelper.IsInActivationZone(
            edge,
            screenLeft: 0,
            screenTop: 0,
            screenWidth: 1920,
            screenHeight: 1080,
            activationZoneSizePercent: 30,
            pointerX: x,
            pointerY: y,
            edgeTolerancePixels: 2);

        Assert.True(result);
    }

    [Theory]
    [InlineData(DockEdge.Top, 960, 4)]
    [InlineData(DockEdge.Bottom, 960, 1075)]
    [InlineData(DockEdge.Left, 4, 500)]
    [InlineData(DockEdge.Right, 1915, 500)]
    public void IsInActivationZone_RejectsPointsOutsideEdgeTolerance(DockEdge edge, double x, double y)
    {
        bool result = ActivationZoneHelper.IsInActivationZone(
            edge,
            screenLeft: 0,
            screenTop: 0,
            screenWidth: 1920,
            screenHeight: 1080,
            activationZoneSizePercent: 30,
            pointerX: x,
            pointerY: y,
            edgeTolerancePixels: 2);

        Assert.False(result);
    }

    [Fact]
    public void IsInActivationZone_RespectsCenteredPercentSpan()
    {
        Assert.True(ActivationZoneHelper.IsInActivationZone(
            DockEdge.Top, 0, 0, 1000, 800, 20, 500, 0));

        Assert.False(ActivationZoneHelper.IsInActivationZone(
            DockEdge.Top, 0, 0, 1000, 800, 20, 300, 0));
    }
}
