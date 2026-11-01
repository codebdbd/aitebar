using System;
using System.IO;
using System.Threading;
using System.Windows;
using System.Xml.Linq;
using Xunit;

namespace AiteBar.Tests;

[Collection("WpfTestCollection")]
[Trait("Category", "WPF")]
public sealed class TaskbarPositionIndicatorWindowTests
{
    [Fact]
    public void TaskbarPositionIndicatorXaml_DeclaresExplicitTitle()
    {
        string repoRoot = FindRepoRoot();
        string xamlPath = Path.Combine(repoRoot, "AiteBar", "TaskbarPositionIndicatorWindow.xaml");
        Assert.True(File.Exists(xamlPath), $"XAML not found at {xamlPath}");

        XDocument doc = XDocument.Load(xamlPath);
        XElement root = doc.Root ?? throw new InvalidOperationException("XAML root is null");

        var titleAttr = root.Attribute("Title");
        Assert.NotNull(titleAttr);
        Assert.Equal("AiteBar Taskbar Indicator", titleAttr.Value);
    }

    [Fact]
    public void TaskbarPositionIndicatorCode_SetsToolWindowStyleOnSourceInitialized()
    {
        string repoRoot = FindRepoRoot();
        string csPath = Path.Combine(repoRoot, "AiteBar", "TaskbarPositionIndicatorWindow.xaml.cs");
        Assert.True(File.Exists(csPath), $"Code not found at {csPath}");

        string code = File.ReadAllText(csPath);
        Assert.Contains("protected override void OnSourceInitialized(EventArgs e)", code);
        Assert.Contains("NativeMethods.WS_EX_TOOLWINDOW", code);
        Assert.Contains("NativeMethods.GWL_EXSTYLE", code);
    }

    [Fact]
    public void TaskbarPositionIndicatorWindow_InstantiatesWithExplicitTitle()
    {
        EnsureApplicationResources();

        string? title = null;
        var thread = new Thread(() =>
        {
            var window = new TaskbarPositionIndicatorWindow();
            title = window.Title;
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        Assert.Equal("AiteBar Taskbar Indicator", title);
    }

    private static void EnsureApplicationResources()
    {
        if (Application.ResourceAssembly == null)
        {
            Application.ResourceAssembly = typeof(App).Assembly;
        }

        if (Application.Current == null)
        {
            var app = new App();
            app.InitializeComponent();
        }
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
            current = Path.GetDirectoryName(current) ?? string.Empty;
        }

        throw new DirectoryNotFoundException("Could not find repository root containing AiteBar.sln");
    }
}
