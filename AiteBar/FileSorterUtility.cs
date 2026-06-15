using System.Windows;
using System.Runtime.Versioning;

namespace AiteBar;

[SupportedOSPlatform("windows6.1")]
[Utility]
public class FileSorterUtility : UtilityBase<FileSorterWindow>
{
    public override string Id => "FileSorter";
    public override string DisplayNameKey => "Tool_FileSorter";
    public override string IconGlyph => "\uF202";
    public override string IconColor => "#60A5FA";

    protected override FileSorterWindow CreateWindow(AppSettingsService settingsService, Window? owner)
    {
        return new FileSorterWindow(settingsService) { Owner = owner };
    }

    protected override void ShowWindow(FileSorterWindow window, AppSettingsService settingsService)
    {
        window.ShowNearPanel(settingsService);
    }
}
