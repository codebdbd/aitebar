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
        string mainWindowCode = File.ReadAllText(Path.Combine(repoRoot, "AiteBar", "MainWindow.xaml.cs"));
        string settingsCode = File.ReadAllText(Path.Combine(repoRoot, "AiteBar", "AppSettingsWindow.xaml.cs"));
        string modelsCode = File.ReadAllText(Path.Combine(repoRoot, "AiteBar", "Models.cs"));

        Assert.Contains("UtilityRegistry.Register(new IconConverterUtility())", appXaml);
        Assert.Contains("public bool ShowPresetIconConverter { get; set; } = true;", modelsCode);
        Assert.Contains("BtnIconConverter.ToolTip = LocalizationService.Get(\"Main_IconConverterTooltip\")", mainWindowCode);
        Assert.Contains("BtnIconConverter.ContextMenu = BuildSystemUtilityContextMenu(() => AppSettings.ShowPresetIconConverter = false)", mainWindowCode);
        Assert.Contains("if (AppSettings.ShowPresetIconConverter) count++", mainWindowCode);
        Assert.Contains("yield return BtnIconConverter", mainWindowCode);
        Assert.Contains("BtnIconConverter.Visibility = showSystemUtils && AppSettings.ShowPresetIconConverter ? Visibility.Visible : Visibility.Collapsed", mainWindowCode);
        Assert.Contains("BtnIconConverter.Visibility == Visibility.Visible", mainWindowCode);
        Assert.Contains("_actionService.LaunchUtilityAsync(\"IconConverter\", HideDock)", mainWindowCode);
        Assert.Contains("ChkShowPresetIconConverter.IsChecked = _settings.ShowPresetIconConverter", settingsCode);
        Assert.Contains("_settings.ShowPresetIconConverter = ChkShowPresetIconConverter.IsChecked ?? false", settingsCode);
    }

    [Fact]
    public void IconConverter_ButtonAndSettingsCheckboxExistInXaml()
    {
        string repoRoot = FindRepoRoot();
        XDocument mainWindow = XDocument.Load(Path.Combine(repoRoot, "AiteBar", "MainWindow.xaml"));
        XDocument settingsWindow = XDocument.Load(Path.Combine(repoRoot, "AiteBar", "AppSettingsWindow.xaml"));

        XElement panelButton = FindNamedElement(mainWindow, "BtnIconConverter");
        Assert.Equal("BtnIconConverter_Click", panelButton.Attribute("Click")?.Value);
        Assert.Equal("\uF12F", panelButton.Attribute("Content")?.Value);

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
