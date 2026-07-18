using AiteBar;
using Point = System.Windows.Point;
using Rect = System.Windows.Rect;
using Size = System.Windows.Size;

namespace AiteBar.Tests;

public sealed class IndicatorPositionHelperTests
{
    private static readonly Rect Screen = new(-1920, 0, 1920, 1080);
    private static readonly Size Indicator = new(35, 35);

    [Fact]
    public void FromNormalized_PreservesRelativePositionAt125PercentScale()
    {
        Point position = IndicatorPositionHelper.FromNormalized(Screen, Indicator, 0.5, 0.25);

        Assert.Equal(-977.5, position.X, 5);
        Assert.Equal(261.25, position.Y, 5);
    }

    [Theory]
    [InlineData(-5000, -5000, -1920, 0)]
    [InlineData(5000, 5000, -35, 1045)]
    public void Clamp_KeepsEntireIndicatorInsideScreen(double x, double y, double expectedX, double expectedY)
    {
        Point position = IndicatorPositionHelper.Clamp(Screen, Indicator, new Point(x, y));

        Assert.Equal(expectedX, position.X);
        Assert.Equal(expectedY, position.Y);
    }

    [Fact]
    public void RoundTrip_ReturnsSameNormalizedCoordinates()
    {
        Point position = IndicatorPositionHelper.FromNormalized(Screen, Indicator, 0.73, 0.42);
        Point normalized = IndicatorPositionHelper.ToNormalized(Screen, Indicator, position);

        Assert.Equal(0.73, normalized.X, 10);
        Assert.Equal(0.42, normalized.Y, 10);
    }

    [Fact]
    public void InvalidCoordinates_AreClampedToVisibleOrigin()
    {
        Point position = IndicatorPositionHelper.FromNormalized(Screen, Indicator, double.NaN, double.PositiveInfinity);

        Assert.Equal(Screen.TopLeft, position);
    }
}
