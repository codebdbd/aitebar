

namespace AiteBar;

public partial class MainWindow
{
    private async Task ExportCurrentPanelAsync()
    {
        try
        {
            string activeContextName = GetContextDisplayName(AppSettings.ActiveContextId);

            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = LocalizationService.Get("Export_Title"),
                Filter = LocalizationService.Get("Export_Filter"),
                DefaultExt = PanelPackageService.PackageExtension,
                AddExtension = true,
                OverwritePrompt = true,
                FileName = BuildPanelPackageFileName(activeContextName)
            };

            if (dialog.ShowDialog(this) != true)
            {
                return;
            }

            PanelExportResult result = await _panelPackageService.ExportCurrentPanelAsync(dialog.FileName);
            new DarkDialog(LocalizationService.Format("Export_Success", result.ExportedCount), false) { Owner = this }.ShowDialog();
        }
        catch (Exception ex)
        {
            Logger.Log(ex);
            new DarkDialog(LocalizationService.Format("Export_Failed", ex.Message), false) { Owner = this }.ShowDialog();
        }
    }

    private async Task ImportIntoCurrentPanelAsync()
    {
        try
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = LocalizationService.Get("Import_Title"),
                Filter = LocalizationService.Get("Export_Filter"),
                DefaultExt = PanelPackageService.PackageExtension,
                CheckFileExists = true,
                Multiselect = false
            };

            if (dialog.ShowDialog(this) != true)
            {
                return;
            }

            PanelImportPreview preview = await _panelPackageService.ReadImportPreviewAsync(dialog.FileName);
            string targetPanelName = GetContextDisplayName(AppSettings.ActiveContextId);
            var confirm = new DarkDialog(
                LocalizationService.Format("Import_Confirm", preview.ElementCount, targetPanelName),
                isConfirm: true)
            {
                Owner = this
            };

            if (confirm.ShowDialog() != true)
            {
                return;
            }

            PanelImportResult result = await _panelPackageService.ImportIntoCurrentPanelAsync(dialog.FileName);
            IReadOnlyList<string> failedHotkeys = RegisterGlobalHotkey();
            RefreshPanel();
            new DarkDialog(LocalizationService.Format("Import_Success", result.ImportedCount), false) { Owner = this }.ShowDialog();
            if (failedHotkeys.Count > 0)
            {
                new DarkDialog(LocalizationService.Format("HotkeyRegistrationFailed", string.Join("\n", failedHotkeys))) { Owner = this }.ShowDialog();
            }
        }
        catch (Exception ex)
        {
            Logger.Log(ex);
            new DarkDialog(LocalizationService.Format("Import_Failed", ex.Message), false) { Owner = this }.ShowDialog();
        }
    }

    private static string BuildPanelPackageFileName(string panelName)
    {
        string sanitized = string.Concat(panelName.Select(ch =>
            System.IO.Path.GetInvalidFileNameChars().Contains(ch) ? '_' : ch)).Trim();

        if (string.IsNullOrWhiteSpace(sanitized))
        {
            sanitized = "Panel";
        }

        return sanitized + PanelPackageService.PackageExtension;
    }
}
