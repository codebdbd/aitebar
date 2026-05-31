using AiteBar;

namespace AiteBar.Tests;

public sealed class TimerStopwatchLayoutHelperTests
{
    [Fact]
    public void PresetDurations_MatchTimerButtons()
    {
        var expected = new[]
        {
            TimeSpan.FromMinutes(1),
            TimeSpan.FromMinutes(3),
            TimeSpan.FromMinutes(5),
            TimeSpan.FromMinutes(10),
            TimeSpan.FromMinutes(15),
            TimeSpan.FromMinutes(30),
            TimeSpan.FromMinutes(45),
            TimeSpan.FromMinutes(60),
            TimeSpan.FromMinutes(90),
            TimeSpan.FromMinutes(120),
        };

        Assert.Equal(expected, TimerStopwatchLayoutHelper.PresetDurations);
    }

    [Theory]
    [InlineData(false, false, 420, 420, 420, 330)]
    [InlineData(false, true, 420, 420, 420, 330)]
    [InlineData(true, false, 264, 54, 264, 54)]
    [InlineData(true, true, 264, 54, 264, 54)]
    public void GetWindowMetrics_ReturnsStableFullAndCompactSizes(
        bool isCompactMode,
        bool isStopwatchMode,
        double expectedWidth,
        double expectedHeight,
        double expectedMinWidth,
        double expectedMinHeight)
    {
        var metrics = TimerStopwatchLayoutHelper.GetWindowMetrics(isCompactMode, isStopwatchMode);

        Assert.Equal(expectedWidth, metrics.Width);
        Assert.Equal(expectedHeight, metrics.Height);
        Assert.Equal(expectedMinWidth, metrics.MinWidth);
        Assert.Equal(expectedMinHeight, metrics.MinHeight);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 1)]
    [InlineData(60, 60)]
    [InlineData(90, 90)]
    [InlineData(120, 120)]
    [InlineData(7200, 7200)]
    [InlineData(7201, 7200)]
    public void GetProgressSegmentCount_TracksSecondsAndCapsLongTimers(int seconds, int expectedSegments)
    {
        var count = TimerStopwatchLayoutHelper.GetProgressSegmentCount(TimeSpan.FromSeconds(seconds));

        Assert.Equal(expectedSegments, count);
    }

    [Theory]
    [InlineData(90, 356, 2, 14, 354d / 89d)]
    [InlineData(120, 356, 1, 12, 355d / 119d)]
    [InlineData(301, 356, 0.5, 10, 355.5d / 300d)]
    public void GetProgressTickMetrics_ScalesTicksWithoutWrappingRows(
        int segmentCount,
        double actualWidth,
        double expectedTickWidth,
        double expectedTickHeight,
        double expectedStep)
    {
        var metrics = TimerStopwatchLayoutHelper.GetProgressTickMetrics(segmentCount, actualWidth);

        Assert.Equal(expectedTickWidth, metrics.TickWidth);
        Assert.Equal(expectedTickHeight, metrics.TickHeight);
        Assert.Equal(expectedStep, metrics.Step, precision: 8);
    }

    [Fact]
    public void GetProgressTickMetrics_UsesFallbackWidthBeforeLayout()
    {
        var metrics = TimerStopwatchLayoutHelper.GetProgressTickMetrics(segmentCount: 60, actualWidth: 0);

        Assert.Equal(2, metrics.TickWidth);
        Assert.Equal(354d / 59d, metrics.Step, precision: 8);
    }

    [Theory]
    [InlineData(false, "\uED88")]
    [InlineData(true, "\uF2DD")]
    public void GetModeGlyph_ReturnsTimerOrStopwatchGlyph(bool isStopwatchMode, string expectedGlyph)
    {
        Assert.Equal(expectedGlyph, TimerStopwatchLayoutHelper.GetModeGlyph(isStopwatchMode));
    }

    [Fact]
    public void CompactToggleGlyph_UsesRestoreStyleGlyph()
    {
        Assert.Equal("\uE923", TimerStopwatchLayoutHelper.CompactToggleGlyph);
    }
}
