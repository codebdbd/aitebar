using AiteBar;

namespace AiteBar.Tests;

public sealed class TaskbarGeometryHelperTests
{
    [Theory]
    [InlineData(1920, 1.0, 1920)]
    [InlineData(1920, 1.5, 1280)]
    [InlineData(-2880, 1.25, -2304)]
    [InlineData(100, 0, 100)]
    public void PixelsToDips_UsesTheTargetMonitorScale(double pixels, double dpiScale, double expected)
    {
        Assert.Equal(expected, TaskbarGeometryHelper.PixelsToDips(pixels, dpiScale), precision: 6);
    }

    [Theory]
    [InlineData(DockEdge.Top, "\uF19B")]
    [InlineData(DockEdge.Bottom, "\uF148")]
    [InlineData(DockEdge.Left, "\uF15B")]
    [InlineData(DockEdge.Right, "\uF181")]
    public void GetArrowGlyph_ReturnsFluentDirectionGlyph(DockEdge edge, string expected)
    {
        Assert.Equal(expected, TaskbarGeometryHelper.GetArrowGlyph(edge));
    }
}
