using System.Windows;
using System.Runtime.Versioning;

namespace AiteBar;

[SupportedOSPlatform("windows6.1")]
public class QuickNoteUtility : UtilityBase<QuickNoteWindow>
{
    public override string Id => "QuickNote";
    public override string DisplayNameKey => "Tool_QuickNote";
    public override string IconGlyph => "\uF56F";
    public override string IconColor => "#22D3EE";

    protected override QuickNoteWindow CreateWindow(AppSettingsService settingsService, Window? owner)
    {
        return new QuickNoteWindow(new QuickNoteService(), settingsService) { Owner = owner };
    }

    protected override void ShowWindow(QuickNoteWindow window, AppSettingsService settingsService)
    {
        window.ShowSliding(settingsService.Settings);
    }
}
