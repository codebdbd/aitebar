using System.IO;
using System.Windows.Media;

namespace AiteBar.Tests;

public sealed class ZenEditorIntegrationTests
{
    [Fact]
    public void PanelCatalog_UsesRequestedFluentGlyph()
    {
        Assert.Equal("\uE367", UtilityButtonCatalog.ZenEditor.Icon);
        Assert.Equal("ZenEditor", UtilityButtonCatalog.ZenEditor.Id);
    }

    [Fact]
    public void Window_PreservesMinimalFullscreenContract()
    {
        string xaml = File.ReadAllText(Path.Combine(FindRepoRoot(), "AiteBar", "ZenEditorWindow.xaml"));

        Assert.Contains("WindowStyle=\"None\"", xaml);
        Assert.Contains("ResizeMode=\"NoResize\"", xaml);
        Assert.Contains("ShowInTaskbar=\"True\"", xaml);
        Assert.Contains("SpellCheck.IsEnabled=\"False\"", xaml);
        Assert.Contains("x:Key=\"ZenEditorTextBoxStyle\"", xaml);
        Assert.Contains("Style=\"{StaticResource ZenEditorTextBoxStyle}\"", xaml);
        Assert.Contains("<ScrollViewer x:Name=\"PART_ContentHost\"", xaml);
        Assert.Contains("Background=\"{x:Null}\"", xaml);
        Assert.DoesNotContain("CornerRadius=", ExtractEditorStyle(xaml));
        Assert.Contains("UndoLimit=\"500\"", xaml);
        Assert.Contains("VerticalScrollBarVisibility=\"Hidden\"", xaml);
        Assert.DoesNotContain("StatusBar", xaml);
        Assert.DoesNotContain("ToolBar", xaml);
        Assert.DoesNotContain("TabControl", xaml);
        Assert.DoesNotContain("x:Name=\"Header\"", xaml);
        Assert.DoesNotContain("Minimize_Click", xaml);
    }

    [Fact]
    public void Window_CreatesContextMenuBeforeFirstOpening()
    {
        string windowCode = File.ReadAllText(Path.Combine(
            FindRepoRoot(),
            "AiteBar",
            "ZenEditorWindow.xaml.cs"));
        string menuFactoryCode = File.ReadAllText(Path.Combine(
            FindRepoRoot(),
            "AiteBar",
            "AppContextMenuFactory.cs"));
        string mainWindowCode = File.ReadAllText(Path.Combine(
            FindRepoRoot(),
            "AiteBar",
            "MainWindow.xaml.cs"));
        string trayMenuCode = File.ReadAllText(Path.Combine(
            FindRepoRoot(),
            "AiteBar",
            "MainWindow.TrayMenuHandler.cs"));
        string indicatorCode = File.ReadAllText(Path.Combine(
            FindRepoRoot(),
            "AiteBar",
            "TaskbarPositionIndicatorWindow.xaml.cs"));

        int constructor = windowCode.IndexOf(
            "public ZenEditorWindow(ZenEditorStore store",
            StringComparison.Ordinal);
        int showFullScreen = windowCode.IndexOf(
            "public void ShowFullScreen()",
            constructor,
            StringComparison.Ordinal);
        Assert.True(constructor >= 0 && showFullScreen > constructor);
        Assert.Contains("RefreshContextMenu();", windowCode[constructor..showFullScreen]);
        Assert.Contains("ZenEditor_NewDocument", windowCode);
        Assert.Contains("ZenEditor_OpenDocument", windowCode);
        Assert.Contains("ZenEditor_ExportTxt", windowCode);
        Assert.Contains("ZenEditor_Undo", windowCode);
        Assert.Contains("ZenEditor_Redo", windowCode);
        Assert.Contains("ZenEditor_Cut", windowCode);
        Assert.Contains("ZenEditor_Copy", windowCode);
        Assert.Contains("ZenEditor_Paste", windowCode);
        Assert.Contains("ZenEditor_SelectAll", windowCode);
        Assert.Contains("ZenEditor_Theme", windowCode);
        Assert.DoesNotContain("CreateMenuItem(\"ZenEditor_Exit\"", windowCode);
        Assert.Contains("else if (e.Key == Key.Escape)", windowCode);
        Assert.DoesNotContain("shift && !control && !alt && e.Key is Key.Up or Key.Down", windowCode);
        Assert.Contains("ZenEditorShortcutResolver.Resolve(e.Key, modifiers)", windowCode);
        Assert.Contains("ZenEditorThemeCatalog.GetAdjacent(_theme.Id, -1)", windowCode);
        Assert.Contains("ZenEditorThemeCatalog.GetAdjacent(_theme.Id, 1)", windowCode);
        Assert.Contains("AppContextMenuFactory.CreateMenu(this)", windowCode);
        Assert.Contains("AppContextMenuFactory.CreateItem(", windowCode);
        Assert.Contains("AppContextMenuFactory.CreateSeparator(this)", windowCode);
        Assert.Contains("Style = (Style)resourceOwner.FindResource(\"DarkContextMenu\")", menuFactoryCode);
        Assert.Contains("Style = (Style)resourceOwner.FindResource(\"DarkMenuItem\")", menuFactoryCode);
        Assert.Contains("Style = (Style)resourceOwner.FindResource(\"ContextMenuIconTextStyle\")", menuFactoryCode);
        Assert.Contains("AppContextMenuFactory.CreateItem(", mainWindowCode);
        Assert.Contains("AppContextMenuFactory.CreateMenu(this)", mainWindowCode);
        Assert.Contains("AppContextMenuFactory.CreateMenu(this)", trayMenuCode);
        Assert.Contains("AppContextMenuFactory.CreateItem(", indicatorCode);
        Assert.Contains("AppContextMenuFactory.CreateMenu(this)", indicatorCode);
        int menuStart = windowCode.IndexOf("private ContextMenu BuildContextMenu()", StringComparison.Ordinal);
        int menuEnd = windowCode.IndexOf("private void RefreshContextMenu()", menuStart, StringComparison.Ordinal);
        Assert.True(menuStart >= 0 && menuEnd > menuStart);
        string menuCode = windowCode[menuStart..menuEnd];
        Assert.DoesNotContain("Background = background", menuCode);
        Assert.DoesNotContain("Foreground = foreground", menuCode);
    }

    [Fact]
    public void Window_HasNoSlideOutHeaderOrMinimizeAction()
    {
        string xaml = File.ReadAllText(Path.Combine(FindRepoRoot(), "AiteBar", "ZenEditorWindow.xaml"));
        string windowCode = File.ReadAllText(Path.Combine(
            FindRepoRoot(),
            "AiteBar",
            "ZenEditorWindow.xaml.cs"));

        Assert.DoesNotContain("HeaderTransform", xaml);
        Assert.DoesNotContain("PreviewMouseMove=", xaml);
        Assert.DoesNotContain("ShowHeader", windowCode);
        Assert.DoesNotContain("HideHeader", windowCode);
        Assert.DoesNotContain("Minimize_Click", windowCode);
    }

    [Fact]
    public void Window_UsesActiveOnlyTopmostAndAccessibleTemporarySurfaces()
    {
        string xaml = File.ReadAllText(Path.Combine(
            FindRepoRoot(),
            "AiteBar",
            "ZenEditorWindow.xaml"));
        string windowCode = File.ReadAllText(Path.Combine(
            FindRepoRoot(),
            "AiteBar",
            "ZenEditorWindow.xaml.cs"));

        Assert.Contains("Topmost=\"False\"", xaml);
        Assert.Contains("x:Name=\"SearchOverlay\"", xaml);
        Assert.Contains("Visibility=\"Collapsed\"", xaml);
        Assert.Contains("AutomationProperties.LiveSetting=\"Assertive\"", xaml);
        Assert.Contains("x:Name=\"RetrySaveButton\"", xaml);
        Assert.Contains("HwndNotTopmost", windowCode);
        Assert.Contains("SetFullscreenTopmost(isTopmost: true)", windowCode);
        Assert.Contains("SetFullscreenTopmost(isTopmost: false)", windowCode);
        Assert.Contains("RetrySaveButton.Focus()", windowCode);
        Assert.Contains("_suppressSelectionCopy = true", windowCode);
    }

    [Fact]
    public void Window_ExplicitlySuppressesAiteBarPositionIndicator()
    {
        string windowCode = File.ReadAllText(Path.Combine(
            FindRepoRoot(),
            "AiteBar",
            "ZenEditorWindow.xaml.cs"));
        string indicatorCode = File.ReadAllText(Path.Combine(
            FindRepoRoot(),
            "AiteBar",
            "TaskbarPositionIndicatorService.cs"));

        Assert.Contains("SetUtilityFullscreenSuppressed(true)", windowCode);
        Assert.Contains("SetUtilityFullscreenSuppressed(false)", windowCode);
        Assert.Contains("if (_isSuppressedByUtilityFullscreen)", indicatorCode);
    }

    [Fact]
    public void AppSettingsClone_PreservesZenEditorVisibility()
    {
        var service = new AppSettingsService();
        service.UpdateSettings(settings => settings.ShowPresetZenEditor = false);

        Assert.False(service.Settings.ShowPresetZenEditor);
        UtilityButtonCatalog.ZenEditor.SetVisible(service.Settings, true);
        Assert.False(service.Settings.ShowPresetZenEditor);
    }

    [Fact]
    public void BundledFonts_ContainRequiredRussianUkrainianAndEnglishGlyphs()
    {
        string directory = Path.Combine(
            FindRepoRoot(),
            "AiteBar",
            "Resources",
            "ZenEditor",
            "Fonts");
        string[] files =
        [
            "Literata[opsz,wght].ttf",
            "SourceSerif4-Regular.ttf",
            "NotoSans[wdth,wght].ttf",
            "IBMPlexSans-Regular.ttf",
            "Inter[opsz,wght].ttf"
        ];
        const string required = "AaЯяІіЇїЄєҐґ";

        foreach (string file in files)
        {
            var glyphTypeface = new GlyphTypeface(new Uri(Path.Combine(directory, file)));
            foreach (char character in required)
            {
                Assert.True(
                    glyphTypeface.CharacterToGlyphMap.ContainsKey(character),
                    $"{file} does not contain U+{(int)character:X4}.");
            }
        }
    }

    private static string ExtractEditorStyle(string xaml)
        => ExtractStyle(xaml, "ZenEditorTextBoxStyle");

    private static string ExtractStyle(string xaml, string key)
    {
        int start = xaml.IndexOf($"<Style x:Key=\"{key}\"", StringComparison.Ordinal);
        int end = xaml.IndexOf("</Style>", start, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start);
        return xaml[start..(end + "</Style>".Length)];
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

        throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
