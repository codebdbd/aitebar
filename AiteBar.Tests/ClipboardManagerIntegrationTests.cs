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
        string settingsCode = File.ReadAllText(Path.Combine(repoRoot, "AiteBar", "AppSettingsWindow.xaml.cs"));
        string modelsCode = File.ReadAllText(Path.Combine(repoRoot, "AiteBar", "Models.cs"));
        string clipboardUtilityCode = File.ReadAllText(Path.Combine(repoRoot, "AiteBar", "ClipboardManagerUtility.cs"));
        string clipboardWindowCode = File.ReadAllText(Path.Combine(repoRoot, "AiteBar", "ClipboardManagerWindow.xaml.cs"));

        Assert.Contains("RegisterAllFromAssembly", appXaml);
        Assert.Contains("[Utility]", clipboardUtilityCode);
        Assert.Contains("public bool ShowPresetClipboardManager { get; set; } = false;", modelsCode);
        Assert.Contains("public bool ClipboardManagerPersistHistory { get; set; } = false;", modelsCode);
        Assert.DoesNotContain("SendKeys.SendWait", clipboardWindowCode, StringComparison.Ordinal);
        Assert.Contains("case \"ClipboardManager\":", mainWindowCode);
        Assert.Contains("LaunchUtilityAsync(\"ClipboardManager\", HideDock)", mainWindowCode);
        Assert.Contains("ChkClipboardManagerPersistHistory.IsChecked = _settings.ClipboardManagerPersistHistory", settingsCode);
        Assert.Contains("settings.ClipboardManagerPersistHistory = ChkClipboardManagerPersistHistory.IsChecked ?? true", settingsCode);

        Assert.Equal("ClipboardManager", UtilityButtonCatalog.ClipboardManager.Id);
        var settings = new AppSettings();
        Assert.False(UtilityButtonCatalog.ClipboardManager.IsVisible(settings));
        UtilityButtonCatalog.ClipboardManager.SetVisible(settings, true);
        Assert.True(settings.ShowPresetClipboardManager);

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

        XElement persistenceCheckbox = FindNamedElement(settingsWindow, "ChkClipboardManagerPersistHistory");
        Assert.Equal("{local:Loc ResourceKey=ClipboardManager_PersistHistorySetting}", persistenceCheckbox.Attribute("Content")?.Value);
    }

    [Fact]
    public void ClipboardManager_ExposesAccessibleFullWipeAction()
    {
        string repoRoot = FindRepoRoot();
        XDocument clipboardWindow = XDocument.Load(Path.Combine(repoRoot, "AiteBar", "ClipboardManagerWindow.xaml"));

        XElement wipeAllButton = FindNamedElement(clipboardWindow, "BtnWipeAll");
        Assert.Equal("BtnWipeAll_Click", wipeAllButton.Attribute("Click")?.Value);
        Assert.Equal("{local:Loc ResourceKey=ClipboardManager_WipeAll}", wipeAllButton.Attribute("AutomationProperties.Name")?.Value);

        XElement copyButton = Assert.Single(clipboardWindow.Descendants(), element => string.Equals(element.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value, "CopyButton", StringComparison.Ordinal));
        Assert.Null(copyButton.Attribute("Focusable"));
        Assert.Equal("{Binding CopyLabel}", copyButton.Attribute("AutomationProperties.Name")?.Value);
    }

    [Fact]
    public void ClipboardManager_WindowSupportsRecoverableMinimization()
    {
        string repoRoot = FindRepoRoot();
        XDocument clipboardWindow = XDocument.Load(Path.Combine(repoRoot, "AiteBar", "ClipboardManagerWindow.xaml"));
        XElement root = clipboardWindow.Root!;
        string utilityCode = File.ReadAllText(Path.Combine(repoRoot, "AiteBar", "ClipboardManagerUtility.cs"));
        string registryCode = File.ReadAllText(Path.Combine(repoRoot, "AiteBar", "UtilityRegistry.cs"));

        Assert.Equal("CanResize", root.Attribute("ResizeMode")?.Value);
        Assert.Equal("False", root.Attribute("ShowInTaskbar")?.Value);
        Assert.Equal("Window_StateChanged", root.Attribute("StateChanged")?.Value);
        Assert.Contains("Owner = owner", utilityCode, StringComparison.Ordinal);
        Assert.Contains("RestoreExistingWindow", utilityCode, StringComparison.Ordinal);
        Assert.Contains("RestoreExistingWindow(_window)", registryCode, StringComparison.Ordinal);
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
