using System.Runtime.Versioning;
using System.Windows;

namespace AiteBar.IptvPlayer;

[SupportedOSPlatform("windows6.1")]
[Utility]
public sealed class IptvPlayerUtility : UtilityBase<IptvPlayerWindow>
{
    public override string Id => "IptvPlayer";
    public override string DisplayNameKey => "Tool_IptvPlayer";
    public override string IconGlyph => "\uEDC3";
    public override string IconColor => UtilityIconColors.ScreenCapture;

    protected override IptvPlayerWindow CreateWindow(AppSettingsService settingsService, Window? owner)
    {
        MainWindow? mainWindow = owner as MainWindow;
        return new IptvPlayerWindow(settingsService, mainWindow);
    }

    protected override void ShowWindow(IptvPlayerWindow window, AppSettingsService settingsService)
    {
        window.Show();
        if (window.WindowState == WindowState.Minimized)
        {
            window.WindowState = WindowState.Normal;
        }
        window.Activate();
    }
}
