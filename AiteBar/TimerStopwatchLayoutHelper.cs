using System;

namespace AiteBar;

internal static class TimerStopwatchLayoutHelper
{
    public const double TimerWindowWidth = 420;
    public const double TimerWindowHeight = 420;
    public const double StopwatchWindowWidth = 420;
    public const double StopwatchWindowHeight = 420;
    public const double CompactWindowWidth = 264;
    public const double CompactWindowHeight = 54;
    public const double FullMinHeight = 330;
    public const int MaxProgressSegmentCount = 7200;
    public const double ProgressFallbackWidth = 356;
    public const string CompactToggleGlyph = "\uEA1A";
    public const string CompactPlayGlyph = "\uF606";
    public const string CompactPauseGlyph = "\uF5A2";
    public const string CompactExpandGlyph = "\uE685";
    public const string TimerModeGlyph = "\uED88";
    public const string StopwatchModeGlyph = "\uF2DD";

    public static readonly TimeSpan[] PresetDurations =
    [
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
    ];

    public static TimerStopwatchWindowMetrics GetWindowMetrics(bool isCompactMode, bool isStopwatchMode)
    {
        if (isCompactMode)
        {
            return new TimerStopwatchWindowMetrics(
                CompactWindowWidth,
                CompactWindowHeight,
                CompactWindowWidth,
                CompactWindowHeight);
        }

        return new TimerStopwatchWindowMetrics(
            isStopwatchMode ? StopwatchWindowWidth : TimerWindowWidth,
            isStopwatchMode ? StopwatchWindowHeight : TimerWindowHeight,
            TimerWindowWidth,
            FullMinHeight);
    }

    public static int GetProgressSegmentCount(TimeSpan duration) =>
        Math.Clamp((int)Math.Ceiling(duration.TotalSeconds), 1, MaxProgressSegmentCount);

    public static TimerStopwatchProgressTickMetrics GetProgressTickMetrics(int segmentCount, double actualWidth)
    {
        double tickWidth = segmentCount > 300 ? 0.5 : (segmentCount > 90 ? 1 : 2);
        double tickHeight = segmentCount > 300 ? 10 : (segmentCount > 90 ? 12 : 14);
        double availableWidth = actualWidth > 0 ? actualWidth : ProgressFallbackWidth;
        double maxLeft = Math.Max(0, availableWidth - tickWidth);
        double step = segmentCount > 1 ? maxLeft / (segmentCount - 1) : 0;

        return new TimerStopwatchProgressTickMetrics(tickWidth, tickHeight, step);
    }

    public static string GetModeGlyph(bool isStopwatchMode) =>
        isStopwatchMode ? StopwatchModeGlyph : TimerModeGlyph;
}

internal readonly record struct TimerStopwatchWindowMetrics(
    double Width,
    double Height,
    double MinWidth,
    double MinHeight);

internal readonly record struct TimerStopwatchProgressTickMetrics(
    double TickWidth,
    double TickHeight,
    double Step);
