using System;
using System.IO;

namespace AiteBar.Tests;

public sealed class RuntimeLocalizationWindowSourceTests
{
    [Fact]
    public void AppSettingsWindow_RebuildsDynamicLocalizedUiOnCultureChange()
    {
        string code = ReadCode("AiteBar", "AppSettingsWindow.xaml.cs");

        Assert.Contains("private void RefreshLocalizedUi()", code);
        Assert.Contains("ReloadLocalizedChoiceLists();", code);
        Assert.Contains("LoadLanguageList();", code);
        Assert.Contains("LoadKeyList();", code);
        Assert.Contains("ReloadEdgeList(edgeTag);", code);
        Assert.Contains("ReloadMonitorList(monitorTag);", code);
        Assert.Contains("RefreshContextRowTooltips();", code);
        Assert.Contains("protected override void OnLocalizationChanged()", code);
    }

    [Fact]
    public void SettingsWindow_RebuildsDynamicListsOnCultureChange()
    {
        string code = ReadCode("AiteBar", "SettingsWindow.xaml.cs");
        // Собираем код из всех partial-классов MainWindow
        string repoRoot = FindRepoRoot();
        string mainWindowCode = ReadCode("AiteBar", "MainWindow.xaml.cs");
        mainWindowCode += File.ReadAllText(Path.Combine(repoRoot, "AiteBar", "MainWindow.PanelDragHandler.cs"));
        mainWindowCode += File.ReadAllText(Path.Combine(repoRoot, "AiteBar", "MainWindow.DragAndDropHandler.cs"));
        mainWindowCode += File.ReadAllText(Path.Combine(repoRoot, "AiteBar", "MainWindow.ImportExportHandler.cs"));
        mainWindowCode += File.ReadAllText(Path.Combine(repoRoot, "AiteBar", "MainWindow.DropHandler.cs"));
        mainWindowCode += File.ReadAllText(Path.Combine(repoRoot, "AiteBar", "MainWindow.KeyboardNavigationHandler.cs"));
        mainWindowCode += File.ReadAllText(Path.Combine(repoRoot, "AiteBar", "MainWindow.TrayMenuHandler.cs"));

        Assert.Contains("private void RefreshLocalizedUi()", code);
        Assert.Contains("LoadKeyList();", code);
        Assert.Contains("LoadContexts();", code);
        Assert.Contains("LoadActionTypeList();", code);
        Assert.Contains("LoadBrowserList();", code);
        Assert.Contains("LoadProfilesAsync(selectedProfile)", code);
        Assert.Contains("protected override void OnLocalizationChanged()", code);

        Assert.Contains("foreach (PanelContext context in GetContextsSnapshot())", mainWindowCode);
        Assert.Contains("List<MenuItem> moveTargets = GetContextsSnapshot()", mainWindowCode);
        Assert.Contains("string activeContextName = GetContextDisplayName(AppSettings.ActiveContextId);", mainWindowCode);
        Assert.Contains("BuildUnifiedButtonContextMenu", mainWindowCode);
    }

    [Fact]
    public void UtilityWindows_ReapplyRuntimeLocalizedStateOnCultureChange()
    {
        string fileSorterCode = ReadCode("AiteBar", "FileSorterWindow.xaml.cs");
        string timerCode = ReadCode("AiteBar", "TimerStopwatchWindow.xaml.cs");
        string iconConverterCode = ReadCode("AiteBar", "IconConverterWindow.xaml.cs");
        string iconPickerCode = ReadCode("AiteBar", "IconPickerWindow.xaml.cs");
        string quickNoteCode = ReadCode("AiteBar", "QuickNoteWindow.xaml.cs");
        string rotationCode = ReadCode("AiteBar", "RotationProfileSelectionWindow.xaml.cs");
        string aboutCode = ReadCode("AiteBar", "AboutWindow.xaml.cs");
        string dialogCode = ReadCode("AiteBar", "DarkDialog.xaml.cs");
        string promptCode = ReadCode("AiteBar", "TextPromptDialog.xaml.cs");
        string screenPickerCode = ReadCode("AiteBar", "ScreenColorPickerWindow.cs");

        Assert.Contains("private void RefreshLocalizedUi()", fileSorterCode);
        Assert.Contains("LoadLocationOptions();", fileSorterCode);
        Assert.Contains("protected override void OnLocalizationChanged()", fileSorterCode);
        Assert.Contains("ApplyUndoStatus();", fileSorterCode);

        Assert.Contains("protected override void OnLocalizationChanged()", timerCode);
        Assert.Contains("UpdateDisplay();", timerCode);

        Assert.Contains("private void RefreshLocalizedUi()", iconConverterCode);
        Assert.Contains("protected override void OnLocalizationChanged()", iconConverterCode);

        Assert.Contains("private void UpdateSearchHint()", iconPickerCode);
        Assert.Contains("UpdateSearchHint();", iconPickerCode);
        Assert.Contains("protected override void OnLocalizationChanged()", iconPickerCode);

        Assert.Contains("SetStatus(_statusKind, _statusArgument);", quickNoteCode);
        Assert.Contains("protected override void OnLocalizationChanged()", quickNoteCode);

        Assert.Contains("RenderProfiles();", rotationCode);
        Assert.Contains("protected override void OnLocalizationChanged()", rotationCode);

        Assert.Contains("UpdateVersionText();", aboutCode);
        Assert.Contains("protected override void OnLocalizationChanged()", aboutCode);

        Assert.Contains("private readonly bool _isConfirmDialog;", dialogCode);
        Assert.Contains("protected override void OnLocalizationChanged()", dialogCode);

        Assert.Contains("_titleResourceKey", promptCode);
        Assert.Contains("protected override void OnLocalizationChanged()", promptCode);

        Assert.Contains("LocalizationService.CultureChanged += HandleCultureChanged;", screenPickerCode);
        Assert.Contains("LocalizationService.CultureChanged -= HandleCultureChanged;", screenPickerCode);
    }

    [Fact]
    public void RuntimeLocalizedWindowFiles_DeclareRefreshHooks()
    {
        string[] filesRequiringHook =
        [
            "AiteBar\\AppSettingsWindow.xaml.cs",
            "AiteBar\\SettingsWindow.xaml.cs",
            "AiteBar\\FileSorterWindow.xaml.cs",
            "AiteBar\\IconPickerWindow.xaml.cs",
            "AiteBar\\IconConverterWindow.xaml.cs",
            "AiteBar\\TimerStopwatchWindow.xaml.cs",
            "AiteBar\\QuickNoteWindow.xaml.cs",
            "AiteBar\\RotationProfileSelectionWindow.xaml.cs",
            "AiteBar\\AboutWindow.xaml.cs",
            "AiteBar\\DarkDialog.xaml.cs",
            "AiteBar\\TextPromptDialog.xaml.cs"
        ];

        string repoRoot = FindRepoRoot();

        foreach (string relativePath in filesRequiringHook)
        {
            string code = File.ReadAllText(Path.Combine(repoRoot, relativePath));
            Assert.Contains("OnLocalizationChanged", code);
        }
    }

    private static string ReadCode(params string[] parts)
    {
        string repoRoot = FindRepoRoot();
        return File.ReadAllText(Path.Combine([repoRoot, .. parts]));
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
