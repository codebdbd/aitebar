using System.Windows;

namespace AiteBar;

public partial class BackupPasswordDialog : DarkWindow
{
    private readonly bool _isRestore;

    public bool IncludeSecrets => ChkFullBackup.IsChecked == true;
    public bool IncludeClipboard => IncludeSecrets && ChkClipboard.IsChecked == true;
    public string Password => PwdPassword.Password;

    public BackupPasswordDialog(bool isRestore)
    {
        InitializeComponent();
        _isRestore = isRestore;
        TxtDescription.Text = LocalizationService.Get(isRestore ? "Backup_RestorePasswordHint" : "Backup_CreateHint");
        if (isRestore)
        {
            ChkFullBackup.Visibility = Visibility.Collapsed;
            SensitiveOptions.Visibility = Visibility.Visible;
            ChkClipboard.Visibility = Visibility.Collapsed;
        }
    }

    private void ChkFullBackup_Changed(object sender, RoutedEventArgs e) => SensitiveOptions.Visibility = IncludeSecrets ? Visibility.Visible : Visibility.Collapsed;

    private void BtnOk_Click(object sender, RoutedEventArgs e)
    {
        if ((!_isRestore && IncludeSecrets || _isRestore) && string.IsNullOrWhiteSpace(Password))
        {
            new DarkDialog(LocalizationService.Get("Backup_PasswordRequired")) { Owner = this }.ShowDialog();
            return;
        }
        DialogResult = true;
    }
}
