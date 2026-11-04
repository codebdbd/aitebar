using System;
using System.IO;
using System.Xml.Linq;
using Xunit;

namespace AiteBar.Tests;

public sealed class MainWindowWindowStyleTests
{
    [Fact]
    public void MainWindowXaml_DeclaresShowInTaskbarFalse()
    {
        string repoRoot = FindRepoRoot();
        string xamlPath = Path.Combine(repoRoot, "AiteBar", "MainWindow.xaml");
        Assert.True(File.Exists(xamlPath), $"XAML not found at {xamlPath}");

        XDocument doc = XDocument.Load(xamlPath);
        XElement root = doc.Root ?? throw new InvalidOperationException("XAML root is null");

        var showInTaskbarAttr = root.Attribute("ShowInTaskbar");
        Assert.NotNull(showInTaskbarAttr);
        Assert.Equal("False", showInTaskbarAttr.Value);
    }

    [Fact]
    public void MainWindowCode_SetsToolWindowStyleOnSourceInitialized()
    {
        string repoRoot = FindRepoRoot();
        string csPath = Path.Combine(repoRoot, "AiteBar", "MainWindow.xaml.cs");
        Assert.True(File.Exists(csPath), $"Code not found at {csPath}");

        string code = File.ReadAllText(csPath);
        Assert.Contains("private void Window_SourceInitialized(object sender, EventArgs e)", code);
        Assert.Contains("NativeMethods.WS_EX_TOOLWINDOW", code);
        Assert.Contains("NativeMethods.GWL_EXSTYLE", code);
    }

    private static string FindRepoRoot()
    {
        string current = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(current))
        {
            if (File.Exists(Path.Combine(current, "AiteBar.sln")))
            {
                return current;
            }

            current = Path.GetDirectoryName(current)!;
        }

        throw new InvalidOperationException("Repository root with AiteBar.sln could not be found.");
    }
}
