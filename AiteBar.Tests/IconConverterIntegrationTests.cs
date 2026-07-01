using System;
using System.IO;
using System.Xml.Linq;

namespace AiteBar.Tests;

public sealed class IconConverterIntegrationTests
{
    [Fact]
    public void IconConverter_IsWiredIntoPanelSettingsAndUtilityRegistry()
    {
        string repoRoot = FindRepoRoot();
        string appXaml = File.ReadAllText(Path.Combine(repoRoot, "AiteBar", "App.xaml.cs"));
        string unifiedButtonServiceCode = File.ReadAllText(Path.Combine(repoRoot, "AiteBar", "UnifiedButtonService.cs"));
        string settingsCode = File.ReadAllText(Path.Combine(repoRoot, "AiteBar", "AppSettingsWindow.xaml.cs"));
        string modelsCode = File.ReadAllText(Path.Combine(repoRoot, "AiteBar", "Models.cs"));
        string iconConverterUtilityCode = File.ReadAllText(Path.Combine(repoRoot, "AiteBar", "IconConverterUtility.cs"));

        Assert.Contains("RegisterAllFromAssembly", appXaml);
        Assert.Contains("[Utility]", iconConverterUtilityCode);
        Assert.Contains("public bool ShowPresetIconConverter { get; set; } = true;", modelsCode);
        Assert.Contains("ShowPresetIconConverter", unifiedButtonServiceCode);
        Assert.Contains("ChkShowPresetIconConverter.IsChecked = _settings.ShowPresetIconConverter", settingsCode);
        Assert.Contains("settings.ShowPresetIconConverter = ChkShowPresetIconConverter.IsChecked ?? false", settingsCode);
    }

    [Fact]
    public void IconConverter_SettingsCheckboxExistInXaml()
    {
        string repoRoot = FindRepoRoot();
        XDocument settingsWindow = XDocument.Load(Path.Combine(repoRoot, "AiteBar", "AppSettingsWindow.xaml"));

        XElement settingsCheckbox = FindNamedElement(settingsWindow, "ChkShowPresetIconConverter");
        Assert.Equal("{local:Loc ResourceKey=Tool_IconConverter}", settingsCheckbox.Attribute("Content")?.Value);
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
