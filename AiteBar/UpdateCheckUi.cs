using System;
using System.Collections.Generic;
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
            if (!string.IsNullOrEmpty(result.ErrorMessage))
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
            
            // Если инсталлятор доступен - показываем диалог с опциями
            if (!string.IsNullOrEmpty(result.InstallerUrl))
            {
                var buttons = new List<DialogButton>
                {
                    new() { Text = LocalizationService.Get("Update_DownloadAndInstall"), Value = "download", IsPrimary = true },
                    new() { Text = LocalizationService.Get("Update_OpenReleasePage"), Value = "openpage", IsPrimary = false },
                    new() { Text = LocalizationService.Get("Common_Cancel"), Value = "cancel", IsPrimary = false }
                };
                
                var dialog = new DarkDialog(LocalizationService.Format("Update_AutoInstallPrompt", latest, current), buttons, LocalizationService.Get("Common_Confirmation")) { Owner = owner };
                dialog.ShowDialog();
                
                if (dialog.Tag as string == "download")
                {
                    await DownloadAndInstallAsync(service, result, owner);
                }
                else if (dialog.Tag as string == "openpage")
                {
                    service.OpenReleasePage(result);
                }
            }
            else
            {
                // Если инсталлятор не доступен - стандартное поведение
                var dialog = new DarkDialog(LocalizationService.Format("Update_Available", latest, current), isConfirm: true) { Owner = owner };
                if (dialog.ShowDialog() == true)
                {
                    service.OpenReleasePage(result);
                }
            }
        }
        catch (Exception ex)
        {
            TelemetryService.CaptureException(ex, "update_check_ui");
            new DarkDialog(LocalizationService.Format("Update_CheckFailed", ex.Message)) { Owner = owner }.ShowDialog();
        }
    }

    private static async Task DownloadAndInstallAsync(UpdateCheckService service, UpdateCheckResult result, Window owner)
    {
        var downloadingDialog = new DarkDialog(LocalizationService.Get("Update_Downloading")) { Owner = owner };
        downloadingDialog.Show();
        
        try
        {
            string? installerPath = await service.DownloadInstallerAsync(result);
            
            downloadingDialog.Close();
            
            if (string.IsNullOrEmpty(installerPath))
            {
                service.OpenReleasePage(result);
                return;
            }
            
            // Показываем сообщение, что запускаем инсталлятор
            var installingDialog = new DarkDialog(LocalizationService.Get("Update_Installing")) { Owner = owner };
            installingDialog.Show();
            
            // Запускаем инсталлятор и закрываем приложение
            service.RunInstaller(installerPath);
            
            // Даем время инсталлятору запуститься
            await Task.Delay(1000);
            
            installingDialog.Close();
            System.Windows.Application.Current.Shutdown();
        }
        catch (Exception ex)
        {
            TelemetryService.CaptureException(ex, "update_install");
            downloadingDialog.Close();
            
            var errorDialog = new DarkDialog(LocalizationService.Format("Update_DownloadFailed", ex.Message), isConfirm: true) { Owner = owner };
            if (errorDialog.ShowDialog() == true)
            {
                service.OpenReleasePage(result);
            }
        }
    }
}
