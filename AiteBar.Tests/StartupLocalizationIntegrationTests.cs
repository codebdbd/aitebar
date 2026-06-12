using System;
using System.IO;
using System.Xml.Linq;

namespace AiteBar.Tests;

public sealed class StartupLocalizationIntegrationTests
{
    [Fact]
    public void AppStartup_PreloadsSettingsBeforeCreatingMainWindow()
    {
        string repoRoot = FindRepoRoot();
        string appCode = File.ReadAllText(Path.Combine(repoRoot, "AiteBar", "App.xaml.cs"));
        string mainWindowCode = File.ReadAllText(Path.Combine(repoRoot, "AiteBar", "MainWindow.xaml.cs"));
        XDocument appXaml = XDocument.Load(Path.Combine(repoRoot, "AiteBar", "App.xaml"));

        Assert.DoesNotContain("StartupUri", appXaml.Root?.ToString() ?? string.Empty, StringComparison.Ordinal);
        Assert.Contains("protected override async void OnStartup", appCode);
        Assert.Contains("AppSettingsService settingsService = await LoadSettingsAndApplyCultureAsync();", appCode);
        Assert.Contains("await settingsService.LoadAsync();", appCode);
        Assert.Contains("LocalizationService.ApplyCulture(settingsService.Settings.UiCulture);", appCode);
        Assert.Contains("var mainWindow = new MainWindow(settingsService);", appCode);
        Assert.DoesNotContain(".GetAwaiter().GetResult()", appCode, StringComparison.Ordinal);
        Assert.Contains("public MainWindow(AppSettingsService settingsService)", mainWindowCode);
        Assert.Contains(": this(settingsService, settingsPreloaded: true)", mainWindowCode);
    }

    private static string FindRepoRoot()
    {
        string? current = AppContext.BaseDirectory;

        while (!string.IsNullOrEmpty(current))
        {
            if (File.Exists(Path.Combine(current, "AiteBar.sln")))
            {
                return current;
            }

            current = Directory.GetParent(current)?.FullName;
        }

        throw new DirectoryNotFoundException("Repository root with AiteBar.sln was not found.");
    }
}
