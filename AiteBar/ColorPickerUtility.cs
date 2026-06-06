using System.Windows;
using System.Threading.Tasks;
using System.Runtime.Versioning;

namespace AiteBar;

[SupportedOSPlatform("windows6.1")]
public class ColorPickerUtility : IUtility
{
    public string Id => "ColorPicker";
    public string DisplayNameKey => "Tool_ColorPicker";
    public string IconGlyph => "\uE5FE";
    public string IconColor => "#A855F7";

    public async Task LaunchAsync(AppSettingsService settingsService, Window? owner, Func<Task>? onBeforeExecute = null)
    {
        if (onBeforeExecute != null)
        {
            await onBeforeExecute();
        }

        await Task.Delay(120);
        new ScreenColorPickerWindow() { Owner = owner }.ShowDialog();
    }
}
