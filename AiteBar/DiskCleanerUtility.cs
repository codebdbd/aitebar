using System.Runtime.Versioning;
using System.Windows;

namespace AiteBar;

[SupportedOSPlatform("windows6.1")]
[Utility]
public sealed class DiskCleanerUtility : UtilityBase<DiskCleanerWindow>
{
    public override string Id => "DiskCleaner";
    public override string DisplayNameKey => "Tool_DiskCleaner";
    public override string IconGlyph => "\uF202";
    public override string IconColor => UtilityIconColors.FolderAccess;

    protected override DiskCleanerWindow CreateWindow(AppSettingsService settingsService, Window? owner)
    {
        return new DiskCleanerWindow(settingsService) { Owner = owner };
    }

    protected override void ShowWindow(DiskCleanerWindow window, AppSettingsService settingsService)
    {
        window.ShowNearPanel(settingsService);
    }
}
