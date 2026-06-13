using System.Diagnostics;
using System.IO;
using System.Reflection;
using System;
using System.Windows;

namespace AiteBar;

public partial class AboutWindow : DarkWindow
{
    private const string ProductUrl = "https://codebdbd.github.io/products/aitebar";
    private const string RepositoryUrl = "https://github.com/codebdbd/aitebar";
    private readonly string _dataDirectory;

    public AboutWindow()
    {
        InitializeComponent();
        _dataDirectory = PathHelper.AppDataFolder;
        UpdateVersionText();
    }

    private void UpdateVersionText()
    {
        Version? version = Assembly.GetExecutingAssembly().GetName().Version;
        TxtVersion.Text = LocalizationService.Format("About_VersionFormat", version?.Major, version?.Minor, version?.Build);
    }

    private static void OpenTarget(string target)
    {
        Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
    }

    private void BtnWebsite_Click(object sender, RoutedEventArgs e)
    {
        OpenTarget(ProductUrl);
    }

    private void BtnRepository_Click(object sender, RoutedEventArgs e)
    {
        OpenTarget(RepositoryUrl);
    }

    private async void BtnCheckUpdates_Click(object sender, RoutedEventArgs e)
    {
        await UpdateCheckUi.CheckForUpdatesAsync(this);
    }

    private void BtnLicenses_Click(object sender, RoutedEventArgs e)
    {
        string noticesPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "THIRD_PARTY_NOTICES.txt");
        if (!File.Exists(noticesPath))
            noticesPath = Path.Combine(AppContext.BaseDirectory, "THIRD_PARTY_NOTICES.txt");
        if (!File.Exists(noticesPath))
            noticesPath = Path.Combine(Directory.GetCurrentDirectory(), "THIRD_PARTY_NOTICES.txt");

        if (!File.Exists(noticesPath))
        {
            new DarkDialog(LocalizationService.Get("About_NoticesMissing")) { Owner = this }.ShowDialog();
            return;
        }

        OpenTarget(noticesPath);
    }

    private void BtnOpenDataFolder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Directory.CreateDirectory(_dataDirectory);
            OpenTarget(_dataDirectory);
        }
        catch (Exception ex)
        {
            new DarkDialog(LocalizationService.Format("About_OpenDataFolderFailed", ex.Message)) { Owner = this }.ShowDialog();
        }
    }

    private void BtnOpenProgramFolder_Click(object sender, RoutedEventArgs e)
    {
        string? exeDir = Path.GetDirectoryName(Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName);
        if (string.IsNullOrWhiteSpace(exeDir) || !Directory.Exists(exeDir))
        {
            new DarkDialog(LocalizationService.Get("About_ProgramFolderUnknown")) { Owner = this }.ShowDialog();
            return;
        }

        OpenTarget(exeDir);
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    protected override void OnLocalizationChanged()
    {
        UpdateVersionText();
    }
}
