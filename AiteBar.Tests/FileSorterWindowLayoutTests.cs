using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace AiteBar.Tests;

public sealed class FileSorterWindowLayoutTests
{
    [Fact]
    public void FileSorterWindow_UsesStandardWindowTitleBar()
    {
        string repoRoot = FindRepoRoot();
        string xaml = File.ReadAllText(Path.Combine(repoRoot, "AiteBar", "FileSorterWindow.xaml"));

        // Window uses standard title bar with WindowStyle="SingleBorderWindow"
        Assert.Contains("WindowStyle=\"SingleBorderWindow\"", xaml, StringComparison.Ordinal);

        // No custom close button needed
        Assert.DoesNotContain("BtnClose", xaml, StringComparison.Ordinal);

        // No focus outline triggers needed for standard title bar
        Assert.DoesNotContain("<Trigger Property=\"IsKeyboardFocused\" Value=\"True\">", xaml, StringComparison.Ordinal);
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
