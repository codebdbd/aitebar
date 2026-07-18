using System;
using System.IO;

namespace AiteBar.Tests;

public sealed class SupportLinksTests
{
    [Fact]
    public void TrayAndAboutSettings_UseProjectSupportUrl()
    {
        string repoRoot = FindRepoRoot();
        string mainWindowCode = File.ReadAllText(Path.Combine(repoRoot, "AiteBar", "MainWindow.xaml.cs"));
        string settingsCode = File.ReadAllText(Path.Combine(repoRoot, "AiteBar", "AppSettingsWindow.xaml.cs"));

        Assert.Contains("private const string DonatePageUrl = \"https://codebdbd.github.io/\";", mainWindowCode);
        Assert.Contains("private const string SupportUrl = \"https://codebdbd.github.io/\";", settingsCode);
        Assert.Contains("OpenAboutTarget(SupportUrl);", settingsCode);
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
