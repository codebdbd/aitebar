using System.Windows;
using System.Runtime.Versioning;

namespace AiteBar;

[SupportedOSPlatform("windows6.1")]
[Utility]
public class TimerStopwatchUtility : UtilityBase<TimerStopwatchWindow>
{
    public override string Id => "TimerStopwatch";
    public override string DisplayNameKey => "Tool_TimerStopwatch";
    public override string IconGlyph => "\uED88";
    public override string IconColor => UtilityIconColors.Productivity;

    protected override TimerStopwatchWindow CreateWindow(AppSettingsService settingsService, Window? owner)
    {
        return new TimerStopwatchWindow() { Owner = owner };
    }

    protected override void ShowWindow(TimerStopwatchWindow window, AppSettingsService settingsService)
    {
        window.ShowNearPanel(settingsService);
    }
}
