using System;
using System.IO;

namespace AiteBar.Tests;

public sealed class SupportLinksTests
{
    [Fact]
    public void TrayAndAboutWindow_UseProjectSupportUrl()
    {
        string repoRoot = FindRepoRoot();
        string mainWindowCode = File.ReadAllText(Path.Combine(repoRoot, "AiteBar", "MainWindow.xaml.cs"));
        string aboutCode = File.ReadAllText(Path.Combine(repoRoot, "AiteBar", "AboutWindow.xaml.cs"));

        Assert.Contains("private const string DonatePageUrl = \"https://codebdbd.github.io/\";", mainWindowCode);
        Assert.Contains("private const string SupportUrl = \"https://codebdbd.github.io/\";", aboutCode);
        Assert.Contains("OpenTarget(SupportUrl);", aboutCode);
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
