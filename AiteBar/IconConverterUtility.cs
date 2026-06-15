using System.Runtime.Versioning;
using System.Windows;

namespace AiteBar;

[SupportedOSPlatform("windows6.1")]
[Utility]
public sealed class IconConverterUtility : UtilityBase<IconConverterWindow>
{
    public override string Id => "IconConverter";
    public override string DisplayNameKey => "Tool_IconConverter";
    public override string IconGlyph => "\uF12F";
    public override string IconColor => "#2DD4BF";

    protected override IconConverterWindow CreateWindow(AppSettingsService settingsService, Window? owner)
    {
        return new IconConverterWindow(settingsService) { Owner = owner };
    }

    protected override void ShowWindow(IconConverterWindow window, AppSettingsService settingsService)
    {
        window.ShowNearPanel(settingsService);
    }
}
