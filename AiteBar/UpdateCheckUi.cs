using System;
using System.Threading.Tasks;
using System.Windows;

namespace AiteBar;

internal static class UpdateCheckUi
{
    public static async Task CheckForUpdatesAsync(Window owner)
    {
        var service = new UpdateCheckService();

        try
        {
            var result = await service.CheckLatestReleaseAsync();
            if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
            {
                new DarkDialog(LocalizationService.Format("Update_CheckFailed", result.ErrorMessage)) { Owner = owner }.ShowDialog();
                return;
            }

            if (!result.IsUpdateAvailable)
            {
                new DarkDialog(LocalizationService.Format("Update_Current", UpdateCheckService.FormatVersion(result.CurrentVersion))) { Owner = owner }.ShowDialog();
                return;
            }

            string latest = UpdateCheckService.FormatVersion(result.LatestVersion);
            string current = UpdateCheckService.FormatVersion(result.CurrentVersion);
            var dialog = new DarkDialog(LocalizationService.Format("Update_Available", latest, current), isConfirm: true) { Owner = owner };
            if (dialog.ShowDialog() == true)
            {
                service.OpenReleasePage(result);
            }
        }
        catch (Exception ex)
        {
            TelemetryService.CaptureException(ex, "update_check_ui");
            new DarkDialog(LocalizationService.Format("Update_CheckFailed", ex.Message)) { Owner = owner }.ShowDialog();
        }
    }
}
