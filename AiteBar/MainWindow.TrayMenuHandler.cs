

namespace AiteBar;

public partial class MainWindow
{
    private void InitTrayIcon()
    {
        _notifyIcon = new NotifyIcon
        {
            Text = "AiteBar",
            Visible = true
        };
        try
        {
            var iconUri = new Uri("pack://application:,,,/Resources/app.ico");
            var streamInfo = Application.GetResourceStream(iconUri);
            if (streamInfo != null)
            {
                using var stream = streamInfo.Stream;
                _notifyIcon.Icon = new Icon(stream);
            }
            else
            {
                _notifyIcon.Icon = SystemIcons.Application;
            }
        }
        catch (Exception ex)
        {
            Logger.Log(ex);
            _notifyIcon.Icon = SystemIcons.Application;
        }

        _notifyIcon.MouseClick += (s, e) =>
        {
            if (e.Button == MouseButtons.Left)
            {
                ToggleDock();
            }
            else if (e.Button == MouseButtons.Right)
            {
                ShowTrayContextMenu();
            }
        };
    }

    private void ShowTrayContextMenu()
    {
        LocalizationService.EnsureAppliedCulture();
        ContextMenu menu = AppContextMenuFactory.CreateMenu(this);

        menu.Items.Add(CreateMenuItem(FluentGlyph(MenuIcons.Open), LocalizationService.Get("Menu_Open"), (s, e) => ShowDock()));
        menu.Items.Add(CreateMenuItem(FluentGlyph(MenuIcons.Settings), LocalizationService.Get("Menu_ProgramSettings"), async (s, e) => await ShowAppSettingsWindow(AppSettingsSection.General)));
        menu.Items.Add(CreateMenuItem(FluentGlyph(MenuIcons.Update), LocalizationService.Get("Update_Check"), async (s, e) => await UpdateCheckUi.CheckForUpdatesAsync(this)));
        menu.Items.Add(CreateMenuItem(FluentGlyph(MenuIcons.Info), LocalizationService.Get("Menu_About"), async (s, e) => await ShowAppSettingsWindow(AppSettingsSection.About)));
        menu.Items.Add(CreateMenuItem(FluentGlyph(MenuIcons.Donate), LocalizationService.Get("Menu_Donate"), (s, e) => OpenUrl(DonatePageUrl)));
        menu.Items.Add(CreateMenuItem(FluentGlyph(MenuIcons.Exit), LocalizationService.Get("Menu_Exit"), (s, e) =>
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            Application.Current.Shutdown();
        }));

        menu.Placement = PlacementMode.MousePoint;
        menu.IsOpen = true;

        var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        NativeMethods.SetForegroundWindow(hwnd);
    }
}
