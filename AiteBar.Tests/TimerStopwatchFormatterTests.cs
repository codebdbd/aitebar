using AiteBar;

namespace AiteBar.Tests;

public class TimerStopwatchFormatterTests
{
    [Theory]
    [InlineData(1, 2, 3, "01:02:03")]
    [InlineData(0, 0, 0, "00:00:00")]
    [InlineData(125, 0, 0, "99:00:00")]
    public void FormatClock_ReturnsTwoDigitClock(int hours, int minutes, int seconds, string expected)
    {
        var value = new TimeSpan(hours, minutes, seconds);

        Assert.Equal(expected, TimerStopwatchFormatter.FormatClock(value));
    }

    [Fact]
    public void FormatClock_ClampsNegativeValuesToZero()
    {
        Assert.Equal("00:00:00", TimerStopwatchFormatter.FormatClock(TimeSpan.FromSeconds(-1)));
    }

    [Theory]
    [InlineData(0, 0, 59, "00:59")]
    [InlineData(0, 1, 0, "01:00")]
    [InlineData(1, 0, 0, "01:00:00")]
    public void FormatTimer_UsesCompactMinuteFormatUntilOneHour(int hours, int minutes, int seconds, string expected)
    {
        var value = new TimeSpan(hours, minutes, seconds);

        Assert.Equal(expected, TimerStopwatchFormatter.FormatTimer(value));
    }

    [Theory]
    [InlineData(0, 0, 0, 90, "00:00:00.09")]
    [InlineData(1, 2, 3, 450, "01:02:03.45")]
    public void FormatStopwatch_IncludesCentiseconds(int hours, int minutes, int seconds, int milliseconds, string expected)
    {
        var value = new TimeSpan(0, hours, minutes, seconds, milliseconds);

        Assert.Equal(expected, TimerStopwatchFormatter.FormatStopwatch(value));
    }

    [Theory]
    [InlineData(2, 70, 90, 2, 59, 59)]
    [InlineData(-1, -2, -3, 0, 0, 0)]
    public void ClampTimerDuration_RestrictsInputsToTimerBounds(
        int hours,
        int minutes,
        int seconds,
        int expectedHours,
        int expectedMinutes,
        int expectedSeconds)
    {
        var value = TimerStopwatchFormatter.ClampTimerDuration(hours, minutes, seconds);

        Assert.Equal(new TimeSpan(expectedHours, expectedMinutes, expectedSeconds), value);
    }

    [Theory]
    [InlineData("120", 2, 0, 0)]
    [InlineData("10:30", 0, 10, 30)]
    [InlineData("2:00:00", 2, 0, 0)]
    [InlineData("9999", 23, 59, 59)]
    public void ParseTimerInput_SupportsFastTimerEntry(string input, int expectedHours, int expectedMinutes, int expectedSeconds)
    {
        var value = TimerStopwatchFormatter.ParseTimerInput(input);

        Assert.Equal(new TimeSpan(expectedHours, expectedMinutes, expectedSeconds), value);
    }
}
