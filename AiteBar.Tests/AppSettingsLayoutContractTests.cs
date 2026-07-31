using System.IO;
using System.Xml.Linq;

namespace AiteBar.Tests;

public sealed class AppSettingsLayoutContractTests
{
    private static readonly XNamespace XamlNamespace = "http://schemas.microsoft.com/winfx/2006/xaml";
    private static readonly XNamespace PresentationNamespace = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

    private static readonly string[] SettingsControlNames =
    [
        "CmbLanguage",
        "SegEdgeTop", "SegEdgeBottom", "SegEdgeLeft", "SegEdgeRight",
        "SliderPanelSize", "LblPanelSize50", "LblPanelSize70", "LblPanelSize90", "LblPanelSize100",
        "SliderActivationZone", "LblActivationZone10", "LblActivationZone30", "LblActivationZone50", "LblActivationZone100",
        "SliderActivationDelay", "LblActivationDelay100", "LblActivationDelay200", "LblActivationDelay300", "LblActivationDelay500",
        "TxtAboutVersion", "SettingsFooter", "BtnKeepOnTop", "AiConnectionsList", "TxtAiConnectionsEmpty",
        "ChkShowTaskbarPositionIndicator", "ChkSecondaryMonitor", "ChkCheckForUpdatesEnabled",
        "PanelContextsList",
        "HotkeyShowPanel", "HotkeyNextContext", "HotkeyPreviousContext", "HotkeyAddButton",
        "HotkeyFileSorter", "HotkeyIconConverter", "HotkeyQuickNote", "HotkeyColorPicker", "HotkeyTimerStopwatch", "HotkeyQRCodeGenerator", "HotkeyClipboardManager",
        "ChkShowPresetSearch", "ChkShowPresetScreenshot", "ChkShowPresetVideo", "ChkShowPresetCalc",
        "ChkShowPresetExplorer", "ChkShowPresetDownloads", "ChkShowPresetFileSorter", "ChkShowPresetIconConverter",
        "ChkShowPresetTimerStopwatch", "ChkShowPresetColorPicker", "ChkShowPresetQuickNote", "ChkShowPresetQRCodeGenerator",
        "ChkShowPresetClipboardManager", "ChkShowPresetShowDesktop", "ChkShowPresetAppsFolder", "ChkShowPresetCopilot",
        "ChkShowPresetTextProcessing",
        "ChkClipboardManagerPersistHistory", "QuickToolsList",
        "QuickToolRowQuickNote", "QuickToolRowQRCodeGenerator", "QuickToolRowDownloads", "QuickToolRowVideo",
        "QuickToolRowCopilot", "QuickToolRowCalculator", "QuickToolRowIconConverter", "QuickToolRowClipboardManager",
        "QuickToolRowColorPicker", "QuickToolRowSearch", "QuickToolRowShowDesktop", "QuickToolRowAppsFolder",
        "QuickToolRowExplorer", "QuickToolRowScreenshot", "QuickToolRowFileSorter", "QuickToolRowTimerStopwatch",
        "QuickToolRowTextProcessing"
    ];

    [Fact]
    public void SettingsInventory_ContainsEveryExistingNamedControlExactlyOnce()
    {
        XDocument window = LoadWindow();

        foreach (string controlName in SettingsControlNames)
        {
            Assert.Single(
                window.Descendants(),
                element => string.Equals(element.Attribute(XamlNamespace + "Name")?.Value, controlName, StringComparison.Ordinal));
        }
    }

    [Fact]
    public void ExistingSettingsHandlers_RemainWired()
    {
        XDocument window = LoadWindow();

        AssertHandlerCount(window, "CmbLanguage_SelectionChanged", 1);
        AssertHandlerCount(window, "SegEdge_Click", 4);
        AssertHandlerCount(window, "SliderPanelSize_ValueChanged", 1);
        AssertHandlerCount(window, "SliderActivationZone_ValueChanged", 1);
        AssertHandlerCount(window, "SliderActivationDelay_ValueChanged", 1);
        AssertHandlerCount(window, "BtnKeepOnTop_Click", 1);
        AssertHandlerCount(window, "BtnCancel_Click", 1);
        AssertHandlerCount(window, "BtnSave_Click", 1);
    }

    [Fact]
    public void ModernControls_ReplaceLegacyEditorsAndPreserveStagedSwitches()
    {
        XDocument window = LoadWindow();

        Assert.Single(window.Descendants(PresentationNamespace + "ComboBox"),
            element => element.Attribute(XamlNamespace + "Name")?.Value == "CmbLanguage");
        Assert.Equal(3, window.Descendants(PresentationNamespace + "Slider").Count());
        Assert.DoesNotContain(window.Descendants(), element =>
            element.Attribute(XamlNamespace + "Name")?.Value is "TxtPanelSizeValue" or "TxtActivationZoneValue" or "TxtActivationDelayValue");
        Assert.DoesNotContain(window.Descendants(PresentationNamespace + "Style"), element =>
            element.Attribute(XamlNamespace + "Key")?.Value is "ModernValueChipStyle" or "ModernValueChipTextStyle");
        Assert.Equal(13, window.Descendants().Count(element => element.Name.LocalName == "HotkeyCaptureBox"));
        Assert.DoesNotContain(window.Descendants(), element =>
            (element.Attribute(XamlNamespace + "Name")?.Value ?? string.Empty).EndsWith("Key", StringComparison.Ordinal));

        string[] staticSwitchNames = SettingsControlNames
            .Where(name => name.StartsWith("Chk", StringComparison.Ordinal))
            .ToArray();
        foreach (string switchName in staticSwitchNames)
        {
            Assert.Equal("{StaticResource ModernSwitchStyle}", FindNamedElement(window, switchName).Attribute("Style")?.Value);
        }
    }

    [Fact]
    public void Layout_UsesComfortableResizableWindowAndOneScrollablePageWithoutTabs()
    {
        XDocument window = LoadWindow();
        XElement root = Assert.IsType<XElement>(window.Root);

        Assert.Equal("1200", root.Attribute("Width")?.Value);
        Assert.Equal("680", root.Attribute("Height")?.Value);
        Assert.Equal("960", root.Attribute("MinWidth")?.Value);
        Assert.Equal("600", root.Attribute("MinHeight")?.Value);
        Assert.Equal("CanResize", root.Attribute("ResizeMode")?.Value);
        Assert.Equal("False", root.Attribute("Topmost")?.Value);
        Assert.Equal("True", root.Attribute("ShowInTaskbar")?.Value);
        Assert.Equal("CenterScreen", root.Attribute("WindowStartupLocation")?.Value);
        Assert.Empty(window.Descendants(PresentationNamespace + "TabControl"));
        Assert.Empty(window.Descendants(PresentationNamespace + "TabItem"));

        XElement layoutRoot = FindNamedElement(window, "SettingsLayoutRoot");
        Assert.Equal("16", layoutRoot.Attribute("Margin")?.Value);
        Assert.Equal(["255", "24", "*"], layoutRoot
            .Element(PresentationNamespace + "Grid.ColumnDefinitions")!
            .Elements(PresentationNamespace + "ColumnDefinition")
            .Select(column => column.Attribute("Width")?.Value ?? string.Empty)
            .ToArray());
        XElement contentHost = FindNamedElement(window, "SettingsContentHost");
        Assert.Equal("2", contentHost.Attribute("Grid.Column")?.Value);
        Assert.Equal("2", contentHost.Attribute("Grid.RowSpan")?.Value);
        Assert.Equal("1000", contentHost.Attribute("MaxWidth")?.Value);
        Assert.Equal("Stretch", contentHost.Attribute("HorizontalAlignment")?.Value);
        Assert.Equal("0", FindNamedElement(window, "SettingsNavigationPanel").Attribute("Padding")?.Value);
        XElement navigationPanel = FindNamedElement(window, "SettingsNavigationPanel");
        Assert.Equal("Transparent", navigationPanel.Attribute("Background")?.Value);
        Assert.Equal("0", navigationPanel.Attribute("BorderThickness")?.Value);

        XElement navigation = FindNamedElement(window, "SettingsNavigationList");
        XElement[] navigationItems = navigation.Elements(PresentationNamespace + "ListBoxItem").ToArray();
        Assert.Equal(6, navigationItems.Length);
        Assert.Equal(["\uF587", "\uE8B0", "\uF4B8", "\uF82E", "\uF15A", "\uF4A2"],
            navigationItems.Select(item => item.Attribute("Tag")?.Value ?? string.Empty).ToArray());
        Assert.All(navigationItems, item =>
        {
            Assert.NotNull(item.Attribute("ToolTip"));
            Assert.NotNull(item.Attribute("AutomationProperties.Name"));
        });

        XElement scrollViewer = FindNamedElement(window, "SettingsScrollViewer");
        Assert.Single(window.Descendants(PresentationNamespace + "ScrollViewer"));
        Assert.Empty(scrollViewer.Descendants(PresentationNamespace + "ScrollViewer"));

        string[] expectedSections =
        [
            "GeneralSettingsSection",
            "ContextSettingsSection",
            "HotkeySettingsSection",
            "QuickToolsSettingsSection",
            "AiProvidersSettingsSection",
            "AboutSettingsSection"
        ];
        string[] actualSections = scrollViewer
            .Descendants(PresentationNamespace + "Border")
            .Select(element => element.Attribute(XamlNamespace + "Name")?.Value)
            .Where(name => expectedSections.Contains(name, StringComparer.Ordinal))
            .Cast<string>()
            .ToArray();
        Assert.Equal(expectedSections, actualSections);

        XElement scrollContent = FindNamedElement(window, "SettingsScrollContent");
        Assert.Equal("0,0,14,0", scrollContent.Attribute("Margin")?.Value);
        string[] directSectionChildren = scrollContent
            .Elements(PresentationNamespace + "Border")
            .Select(element => element.Attribute(XamlNamespace + "Name")?.Value)
            .Where(name => expectedSections.Contains(name, StringComparer.Ordinal))
            .Cast<string>()
            .ToArray();
        Assert.Equal(expectedSections, directSectionChildren);
        Assert.Equal(["0,0,0,32", "0,0,0,32", "0,0,0,32", "0,0,0,32", "0,0,0,32", "0"], scrollContent
            .Elements(PresentationNamespace + "Border")
            .Where(element => expectedSections.Contains(element.Attribute(XamlNamespace + "Name")?.Value, StringComparer.Ordinal))
            .Select(element => element.Attribute("Margin")?.Value ?? string.Empty)
            .ToArray());

        XElement clipboardPersistence = FindNamedElement(window, "ChkClipboardManagerPersistHistory");
        XElement generalSection = FindNamedElement(window, "GeneralSettingsSection");
        XElement quickToolsSection = FindNamedElement(window, "QuickToolsSettingsSection");
        Assert.Contains(clipboardPersistence, generalSection.Descendants());
        Assert.DoesNotContain(clipboardPersistence, quickToolsSection.Descendants());

        XElement cancel = FindNamedElement(window, "BtnCancel");
        XElement save = FindNamedElement(window, "BtnSave");
        XElement keepOnTop = FindNamedElement(window, "BtnKeepOnTop");
        XElement footer = FindNamedElement(window, "SettingsFooter");
        Assert.DoesNotContain(cancel, scrollViewer.Descendants());
        Assert.DoesNotContain(save, scrollViewer.Descendants());
        Assert.DoesNotContain(keepOnTop, scrollViewer.Descendants());
        Assert.Equal("1", footer.Attribute("Grid.Row")?.Value);
        Assert.Equal("False", keepOnTop.Attribute("IsChecked")?.Value);
        Assert.Contains(keepOnTop, footer.Descendants());
        Assert.Contains(cancel, footer.Descendants());
        Assert.Contains(save, footer.Descendants());
        Assert.Contains(cancel, contentHost.Descendants());
        Assert.Contains(save, contentHost.Descendants());
    }

    [Fact]
    public void ModernSlider_UsesDiscreteTrackTicksAndInteractiveVisualStates()
    {
        XDocument resources = LoadXaml("FormControlsResources.xaml");
        XElement style = Assert.Single(resources.Descendants(PresentationNamespace + "Style"),
            element => element.Attribute(XamlNamespace + "Key")?.Value == "BaseSliderStyle");

        Assert.Contains(style.Elements(PresentationNamespace + "Setter"), setter =>
            setter.Attribute("Property")?.Value == "Height" && setter.Attribute("Value")?.Value == "32");
        Assert.Contains(style.Elements(PresentationNamespace + "Setter"), setter =>
            setter.Attribute("Property")?.Value == "IsMoveToPointEnabled" && setter.Attribute("Value")?.Value == "True");
        Assert.Single(style.Descendants(), element => element.Attribute(XamlNamespace + "Name")?.Value == "SliderTrackBackground");
        XElement activeTrack = Assert.Single(style.Descendants(PresentationNamespace + "ProgressBar"),
            element => element.Attribute(XamlNamespace + "Name")?.Value == "SliderActiveTrack");
        Assert.Equal("13,0", activeTrack.Attribute("Margin")?.Value);
        XElement track = Assert.Single(style.Descendants(PresentationNamespace + "Track"),
            element => element.Attribute(XamlNamespace + "Name")?.Value == "PART_Track");
        Assert.Null(track.Attribute("Margin"));
        XElement thumb = Assert.Single(track.Descendants(PresentationNamespace + "Thumb"));
        Assert.Equal("26", thumb.Attribute("Width")?.Value);
        Assert.Equal("26", thumb.Attribute("Height")?.Value);
        Assert.Equal(4, style.Descendants().Count(element =>
            (element.Attribute(XamlNamespace + "Name")?.Value ?? string.Empty).StartsWith("SliderTick", StringComparison.Ordinal)));
        Assert.Single(style.Descendants(), element => element.Attribute(XamlNamespace + "Name")?.Value == "SliderThumbHalo");
        Assert.Single(style.Descendants(), element => element.Attribute(XamlNamespace + "Name")?.Value == "SliderThumbCore");
        Assert.Contains(style.Descendants(PresentationNamespace + "Trigger"), trigger =>
            trigger.Attribute("Property")?.Value == "IsDragging" && trigger.Attribute("Value")?.Value == "True");
    }

    [Fact]
    public void NavigationItems_RenderFluentIconsThroughTheSharedTemplate()
    {
        XDocument window = LoadWindow();
        XElement style = Assert.Single(window.Descendants(PresentationNamespace + "Style"),
            element => element.Attribute(XamlNamespace + "Key")?.Value == "SettingsNavigationItemStyle");
        XElement icon = Assert.Single(style.Descendants(PresentationNamespace + "TextBlock"),
            element => element.Attribute(XamlNamespace + "Name")?.Value == "NavigationIcon");

        Assert.Contains(style.Elements(PresentationNamespace + "Setter"), setter =>
            setter.Attribute("Property")?.Value == "Height" && setter.Attribute("Value")?.Value == "36");
        Assert.Equal("{TemplateBinding Tag}", icon.Attribute("Text")?.Value);
        Assert.Equal("pack://application:,,,/Resources/#FluentSystemIcons-Regular", icon.Attribute("FontFamily")?.Value);
        Assert.Equal("18", icon.Attribute("FontSize")?.Value);
    }

    [Fact]
    public void AboutActions_UseContextualFluentIconsInsteadOfDiagonalTextArrows()
    {
        XDocument window = LoadWindow();
        XElement aboutSection = FindNamedElement(window, "AboutSettingsSection");
        Dictionary<string, string> expectedGlyphs = new(StringComparer.Ordinal)
        {
            ["BtnAboutCheckUpdates_Click"] = "\uF190",
            ["BtnAboutWebsite_Click"] = "\uF45A",
            ["BtnAboutRepository_Click"] = "\uF2EF",
            ["BtnAboutLicenses_Click"] = "\uE557",
            ["BtnAboutOpenDataFolder_Click"] = "\uF42E",
            ["BtnAboutOpenProgramFolder_Click"] = "\uF42E"
        };

        XElement[] actionButtons = aboutSection.Descendants(PresentationNamespace + "Button")
            .Where(button => expectedGlyphs.ContainsKey(button.Attribute("Click")?.Value ?? string.Empty))
            .ToArray();
        Assert.Equal(expectedGlyphs.Count, actionButtons.Length);
        Assert.DoesNotContain(aboutSection.Descendants(PresentationNamespace + "TextBlock"), text =>
            text.Attribute("Text")?.Value is "↗" or "›");

        foreach (XElement button in actionButtons)
        {
            string handler = button.Attribute("Click")!.Value;
            XElement icon = Assert.Single(button.Descendants(PresentationNamespace + "TextBlock"), text =>
                text.Attribute("FontFamily")?.Value == "pack://application:,,,/Resources/#FluentSystemIcons-Regular");
            Assert.Equal(expectedGlyphs[handler], icon.Attribute("Text")?.Value);
        }
    }

    [Fact]
    public void SectionSurfaces_UseBorderlessWindowsStyleCards()
    {
        XDocument window = LoadWindow();

        XElement cardStyle = Assert.Single(window.Descendants(PresentationNamespace + "Style"),
            element => element.Attribute(XamlNamespace + "Key")?.Value == "ModernSettingsCardStyle");
        Assert.Contains(cardStyle.Elements(PresentationNamespace + "Setter"), setter =>
            setter.Attribute("Property")?.Value == "BorderThickness" && setter.Attribute("Value")?.Value == "0");
        Assert.Contains(cardStyle.Elements(PresentationNamespace + "Setter"), setter =>
            setter.Attribute("Property")?.Value == "BorderBrush" && setter.Attribute("Value")?.Value == "Transparent");

        XElement selectionStyle = Assert.Single(window.Descendants(PresentationNamespace + "Style"),
            element => element.Attribute(XamlNamespace + "Key")?.Value == "ModernSelectionCardStyle");
        Assert.Contains(selectionStyle.Elements(PresentationNamespace + "Setter"), setter =>
            setter.Attribute("Property")?.Value == "BorderThickness" && setter.Attribute("Value")?.Value == "0");
        Assert.Contains(selectionStyle.Elements(PresentationNamespace + "Setter"), setter =>
            setter.Attribute("Property")?.Value == "BorderBrush" && setter.Attribute("Value")?.Value == "Transparent");
    }

    [Fact]
    public void HotkeyCaptureBox_ClearButton_VisualTreeCheckExists()
    {
        XDocument window = LoadWindow();
        string code = File.ReadAllText(Path.Combine(FindRepoRoot(), "AiteBar", "HotkeyCaptureBox.cs"));

        Assert.Contains("IsClickInsideClearButton", code);
        Assert.Contains("VisualTreeHelper.GetParent", code);
        Assert.Contains("HasAssignedBinding = HotkeyValidationHelper.HasAssignedKey(_binding);", code);

        XElement clearButton = Assert.Single(window.Descendants(PresentationNamespace + "Button"),
            element => element.Attribute(XamlNamespace + "Name")?.Value == "ClearButton");
        Assert.Equal("\uF368", clearButton.Attribute("Content")?.Value);
        Assert.Equal("32", clearButton.Attribute("Width")?.Value);
        Assert.Equal("32", clearButton.Attribute("Height")?.Value);
        Assert.Equal("pack://application:,,,/Resources/#FluentSystemIcons-Regular", clearButton.Attribute("FontFamily")?.Value);
        Assert.Contains(window.Descendants(PresentationNamespace + "Trigger"), trigger =>
            trigger.Attribute("Property")?.Value == "HasAssignedBinding" && trigger.Attribute("Value")?.Value == "False");
    }

    [Fact]
    public void NavigationHandlers_AreWiredAndLocalizationQueuesSectionRealignment()
    {
        XDocument window = LoadWindow();
        string code = File.ReadAllText(Path.Combine(FindRepoRoot(), "AiteBar", "AppSettingsWindow.xaml.cs"));

        Assert.Equal("SettingsNavigationList_SelectionChanged", FindNamedElement(window, "SettingsNavigationList").Attribute("SelectionChanged")?.Value);
        Assert.Equal("SettingsScrollViewer_ScrollChanged", FindNamedElement(window, "SettingsScrollViewer").Attribute("ScrollChanged")?.Value);
        Assert.Equal("Window_Loaded", window.Root?.Attribute("Loaded")?.Value);
        Assert.Contains("AppSettingsSectionNavigationHelper.GetTargetOffset", code);
        Assert.Contains("AppSettingsSectionNavigationHelper.GetActiveSectionIndex", code);
        Assert.Contains("LayoutInformation.GetLayoutSlot(section).Top", code);
        Assert.Contains("QueueScrollToSection(selectedSectionIndex);", code);
    }

    [Fact]
    public void HotkeyInstruction_IsShownOnceAboveRowsAndQuickToolsSortByLocalizedTitles()
    {
        XDocument window = LoadWindow();
        string code = File.ReadAllText(Path.Combine(FindRepoRoot(), "AiteBar", "AppSettingsWindow.xaml.cs"));
        XElement hotkeySection = FindNamedElement(window, "HotkeySettingsSection");
        XElement quickToolsList = FindNamedElement(window, "QuickToolsList");

        Assert.Single(hotkeySection.Descendants(PresentationNamespace + "TextBlock"), text =>
            text.Attribute("Text")?.Value == "{local:Loc ResourceKey=HotkeyCapture_RowHint}");
        Assert.DoesNotContain(hotkeySection.Descendants(PresentationNamespace + "Grid"), row =>
            row.Descendants(PresentationNamespace + "TextBlock").Count(text =>
                text.Attribute("Text")?.Value == "{local:Loc ResourceKey=HotkeyCapture_RowHint}") > 0);

        Assert.Equal(UtilityButtonCatalog.All.Count, quickToolsList.Elements(PresentationNamespace + "Grid").Count());
        Assert.Contains("private void SortQuickToolRows()", code);
        Assert.Contains("StringComparer.Create(LocalizationService.ResolvedCulture, ignoreCase: true)", code);
        Assert.Contains("SortQuickToolRows();", code);

        XDocument russianResources = XDocument.Load(Path.Combine(FindRepoRoot(), "AiteBar", "Resources", "Strings.ru.resx"));
        XElement searchDescription = Assert.Single(russianResources.Descendants("data"), data =>
            data.Attribute("name")?.Value == "QuickTool_Search_Description");
        Assert.Equal("Ищет в интернете текст, который сейчас находится в буфере обмена.", searchDescription.Element("value")?.Value);
    }

    private static void AssertHandlerCount(XDocument window, string handler, int expectedCount)
    {
        int actualCount = window.Descendants()
            .Count(element => element.Attributes().Any(attribute => string.Equals(attribute.Value, handler, StringComparison.Ordinal)));
        Assert.Equal(expectedCount, actualCount);
    }

    private static XElement FindNamedElement(XDocument window, string name) =>
        Assert.Single(
            window.Descendants(),
            element => string.Equals(element.Attribute(XamlNamespace + "Name")?.Value, name, StringComparison.Ordinal));

    private static XDocument LoadWindow() =>
        XDocument.Load(Path.Combine(FindRepoRoot(), "AiteBar", "AppSettingsWindow.xaml"));

    private static XDocument LoadXaml(string fileName) =>
        XDocument.Load(Path.Combine(FindRepoRoot(), "AiteBar", fileName));

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
