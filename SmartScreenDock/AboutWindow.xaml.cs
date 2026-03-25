using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;

namespace SmartScreenDock
{
    public partial class AboutWindow : DarkWindow
    {
        private const string ProductUrl = "https://codebdbd.github.io/intro/en/products/aitebar/";
        private const string RepositoryUrl = "https://github.com/codebdbd/aitebar";

        public AboutWindow()
        {
            InitializeComponent();

            var version = Assembly.GetExecutingAssembly().GetName().Version;
            TxtVersion.Text = $"Версия {version?.Major}.{version?.Minor}.{version?.Build}";
            TxtExePath.Text = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName ?? "Недоступно";
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

        private void BtnLicenses_Click(object sender, RoutedEventArgs e)
        {
            string noticesPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "THIRD_PARTY_NOTICES.txt");
            if (!File.Exists(noticesPath))
                noticesPath = Path.Combine(AppContext.BaseDirectory, "THIRD_PARTY_NOTICES.txt");
            if (!File.Exists(noticesPath))
                noticesPath = Path.Combine(Directory.GetCurrentDirectory(), "THIRD_PARTY_NOTICES.txt");

            if (!File.Exists(noticesPath))
            {
                new DarkDialog("Файл THIRD_PARTY_NOTICES.txt не найден.") { Owner = this }.ShowDialog();
                return;
            }

            OpenTarget(noticesPath);
        }

        private void BtnOpenFolder_Click(object sender, RoutedEventArgs e)
        {
            string? exeDir = Path.GetDirectoryName(Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName);
            if (string.IsNullOrWhiteSpace(exeDir) || !Directory.Exists(exeDir))
            {
                new DarkDialog("Не удалось определить папку программы.") { Owner = this }.ShowDialog();
                return;
            }

            OpenTarget(exeDir);
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
