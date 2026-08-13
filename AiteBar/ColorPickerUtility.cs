using System.Windows;
using System.Threading.Tasks;
using System.Runtime.Versioning;
using System.Collections.Generic;

namespace AiteBar;

[SupportedOSPlatform("windows6.1")]
[Utility]
public class ColorPickerUtility : IUtility
{
    public string Id => "ColorPicker";
    public string DisplayNameKey => "Tool_ColorPicker";
    public string IconGlyph => "\uE5FE";
    public string IconColor => UtilityIconColors.ScreenCapture;

    public async Task LaunchAsync(AppSettingsService settingsService, Window? owner, Func<Task>? onBeforeExecute = null)
    {
        try
        {
            if (onBeforeExecute != null)
            {
                await onBeforeExecute();
            }

            await Task.Delay(120);
            new ScreenColorPickerWindow() { Owner = owner }.ShowDialog();
        }
        catch (Exception ex)
        {
            Logger.Log(ex);
            TelemetryService.CaptureException(ex, "utility_crash", 
                new Dictionary<string, string?> { ["utility_id"] = Id });
            
            if (System.Windows.Application.Current != null)
            {
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    new DarkDialog(LocalizationService.Format("Utility_Unavailable", Id))
                    {
                        Owner = owner
                    }.ShowDialog();
                });
            }
        }
    }
}
