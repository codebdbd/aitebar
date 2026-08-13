using System.Runtime.Versioning;
using System.Windows;

namespace AiteBar;

[SupportedOSPlatform("windows6.1")]
[Utility]
public sealed class QRCodeGeneratorUtility : UtilityBase<QRCodeGeneratorWindow>
{
    public override string Id => "QRCodeGenerator";
    public override string DisplayNameKey => "Tool_QRCodeGenerator";
    public override string IconGlyph => "\uF635";
    public override string IconColor => UtilityIconColors.AssetCreation;

    protected override QRCodeGeneratorWindow CreateWindow(AppSettingsService settingsService, Window? owner)
    {
        return new QRCodeGeneratorWindow() { Owner = owner };
    }

    protected override void ShowWindow(QRCodeGeneratorWindow window, AppSettingsService settingsService)
    {
        window.ShowNearPanel(settingsService);
    }
}
