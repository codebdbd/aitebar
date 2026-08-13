using System.Runtime.Versioning;
using System.Windows;

namespace AiteBar.AiteProfilesUtility;

[SupportedOSPlatform("windows6.1")]
[Utility]
public sealed class AiteProfilesUtility : UtilityBase<AiteProfilesWindow>
{
    public override string Id => "AiteProfiles";
    public override string DisplayNameKey => "Tool_AiteProfiles";
    public override string IconGlyph => "\uE716";
    public override string IconColor => UtilityIconColors.SearchAndNavigation;

    protected override AiteProfilesWindow CreateWindow(AppSettingsService settingsService, Window? owner)
    {
        return new AiteProfilesWindow(settingsService) { Owner = owner };
    }

    protected override void ShowWindow(AiteProfilesWindow window, AppSettingsService settingsService)
    {
        window.ShowNearPanel(settingsService);
    }

    protected override bool RestoreExistingWindow(AiteProfilesWindow window)
    {
        window.RestoreFromAiteBar();
        return true;
    }
}
