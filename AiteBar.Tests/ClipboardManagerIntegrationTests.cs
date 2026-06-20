using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;

namespace AiteBar.Tests;

public sealed class ClipboardManagerIntegrationTests : IDisposable
{
    public ClipboardManagerIntegrationTests()
    {
        UtilityRegistry.Clear();
    }

    public void Dispose()
    {
        UtilityRegistry.Clear();
    }

    [Fact]
    public void ClipboardManager_IsWiredIntoPanelSettingsAndUtilityRegistry()
    {
        string repoRoot = FindRepoRoot();
        string appXaml = File.ReadAllText(Path.Combine(repoRoot, "AiteBar", "App.xaml.cs"));
        string mainWindowCode = File.ReadAllText(Path.Combine(repoRoot, "AiteBar", "MainWindow.xaml.cs"));
        string unifiedButtonServiceCode = File.ReadAllText(Path.Combine(repoRoot, "AiteBar", "UnifiedButtonService.cs"));
        string settingsCode = File.ReadAllText(Path.Combine(repoRoot, "AiteBar", "AppSettingsWindow.xaml.cs"));
        string modelsCode = File.ReadAllText(Path.Combine(repoRoot, "AiteBar", "Models.cs"));
        string clipboardUtilityCode = File.ReadAllText(Path.Combine(repoRoot, "AiteBar", "ClipboardManagerUtility.cs"));

        Assert.Contains("RegisterAllFromAssembly", appXaml);
        Assert.Contains("[Utility]", clipboardUtilityCode);
        Assert.Contains("public bool ShowPresetClipboardManager { get; set; } = false;", modelsCode);
        Assert.Contains("ShowPresetClipboardManager", unifiedButtonServiceCode);
        Assert.Contains("if (AppSettings.ShowPresetClipboardManager) count++;", mainWindowCode);
        Assert.Contains("case \"ClipboardManager\":", mainWindowCode);
        Assert.Contains("LaunchUtilityAsync(\"ClipboardManager\", HideDock)", mainWindowCode);
        Assert.Contains("ChkShowPresetClipboardManager.IsChecked = _settings.ShowPresetClipboardManager", settingsCode);
        Assert.Contains("_settings.ShowPresetClipboardManager = ChkShowPresetClipboardManager.IsChecked ?? false", settingsCode);

        UtilityRegistry.RegisterAllFromAssembly(typeof(ClipboardManagerUtility).Assembly);
        Assert.Contains(UtilityRegistry.GetAll(), utility => utility.Id == "ClipboardManager");
    }

    [Fact]
    public void ClipboardManager_SettingsCheckboxExistsInXaml()
    {
        string repoRoot = FindRepoRoot();
        XDocument settingsWindow = XDocument.Load(Path.Combine(repoRoot, "AiteBar", "AppSettingsWindow.xaml"));

        XElement settingsCheckbox = FindNamedElement(settingsWindow, "ChkShowPresetClipboardManager");
        Assert.Equal("{local:Loc ResourceKey=Tool_ClipboardManager}", settingsCheckbox.Attribute("Content")?.Value);
    }

    private static XElement FindNamedElement(XDocument document, string name)
    {
        XName xName = XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml");
        return Assert.Single(document.Descendants(),
            element => string.Equals(element.Attribute(xName)?.Value, name, StringComparison.Ordinal));
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
