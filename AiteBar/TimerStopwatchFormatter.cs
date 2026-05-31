using System;

namespace AiteBar;

internal static class TimerStopwatchFormatter
{
    public static TimeSpan ClampTimerDuration(int hours, int minutes, int seconds)
    {
        hours = Math.Clamp(hours, 0, 23);
        minutes = Math.Clamp(minutes, 0, 59);
        seconds = Math.Clamp(seconds, 0, 59);
        return new TimeSpan(hours, minutes, seconds);
    }

    public static TimeSpan ParseTimerInput(string? value)
    {
        string[] parts = (value ?? string.Empty).Split(':', StringSplitOptions.TrimEntries);
        if (parts.Length == 1)
        {
            return ClampTimerDuration(TimeSpan.FromMinutes(ParsePart(parts[0])));
        }

        if (parts.Length == 2)
        {
            return ClampTimerDuration(TimeSpan.FromMinutes(ParsePart(parts[0])) + TimeSpan.FromSeconds(ParsePart(parts[1])));
        }

        return ClampTimerDuration(ParsePart(parts[0]), ParsePart(parts[1]), ParsePart(parts[2]));
    }

    private static TimeSpan ClampTimerDuration(TimeSpan value)
    {
        if (value <= TimeSpan.Zero)
        {
            return TimeSpan.Zero;
        }

        var max = new TimeSpan(23, 59, 59);
        return value > max ? max : value;
    }

    private static int ParsePart(string? value) =>
        int.TryParse(value, out int parsed) ? Math.Max(0, parsed) : 0;

    public static string FormatClock(TimeSpan value)
    {
        if (value < TimeSpan.Zero)
        {
            value = TimeSpan.Zero;
        }

        int totalHours = (int)Math.Min(99, Math.Floor(value.TotalHours));
        return $"{totalHours:00}:{value.Minutes:00}:{value.Seconds:00}";
    }

    public static string FormatMilliseconds(TimeSpan value)
    {
        if (value < TimeSpan.Zero)
        {
            value = TimeSpan.Zero;
        }

        return $"{value.Milliseconds / 10:00}";
    }

    public static string FormatTimer(TimeSpan value)
    {
        if (value < TimeSpan.Zero)
        {
            value = TimeSpan.Zero;
        }

        if (value.TotalHours >= 1)
        {
            return $"{(int)value.TotalHours:00}:{value.Minutes:00}:{value.Seconds:00}";
        }

        return $"{value.Minutes:00}:{value.Seconds:00}";
    }

    public static string FormatStopwatch(TimeSpan value)
    {
        if (value < TimeSpan.Zero)
        {
            value = TimeSpan.Zero;
        }

        return $"{(int)Math.Min(99, value.TotalHours):00}:{value.Minutes:00}:{value.Seconds:00}.{value.Milliseconds / 10:00}";
    }
}
