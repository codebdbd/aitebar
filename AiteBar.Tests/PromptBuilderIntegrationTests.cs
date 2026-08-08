using System.IO;

namespace AiteBar.Tests;

public sealed class PromptBuilderIntegrationTests
{
    [Fact]
    public void Window_UsesSevenCategoriesAndFixedStatusArea()
    {
        string xaml = Read("AiteBar", "PromptBuilderWindow.xaml");

        Assert.Contains("Width=\"1280\" Height=\"840\"", xaml);
        Assert.Contains("<Grid Margin=\"32,16,32,24\">", xaml);
        Assert.Contains("x:Name=\"ModeProgramming\"", xaml);
        Assert.Contains("x:Name=\"ModeImages\"", xaml);
        Assert.Contains("x:Name=\"ModeTexts\"", xaml);
        Assert.Contains("x:Name=\"ModeVideo\"", xaml);
        Assert.Contains("x:Name=\"ModeMusic\"", xaml);
        Assert.Contains("x:Name=\"ModeAnalysis\"", xaml);
        Assert.Contains("x:Name=\"ModeIdeas\"", xaml);
        Assert.Contains("x:Name=\"ModeStatusHost\" DockPanel.Dock=\"Top\" Height=\"52\"", xaml);
        Assert.DoesNotContain("x:Name=\"ModeVideoAudio\"", xaml);
        Assert.DoesNotContain("x:Name=\"ModeAnalysisIdeas\"", xaml);
        Assert.DoesNotContain("Panel.ZIndex=\"10\"", xaml);
        Assert.DoesNotContain("Grid.ColumnSpan=\"3\"", xaml);
    }

    [Fact]
    public void CopyNotification_IsTransientAndCannotChangeLayout()
    {
        string code = Read("AiteBar", "PromptBuilderWindow.xaml.cs");

        Assert.Contains("ShowTransientInfoStatus(LocalizationService.Get(\"TextProcessing_Copied\"))", code);
        Assert.Contains("await Task.Delay(TimeSpan.FromSeconds(2));", code);
        Assert.DoesNotContain("SetStatus(LocalizationService.Get(\"TextProcessing_Copied\"))", code);
    }

    [Fact]
    public void Utility_IsIntegratedIntoPanelSettingsHotkeysAndLaunchPaths()
    {
        Assert.Contains("PromptBuilder", Read("AiteBar", "UtilityButtonCatalog.cs"));
        Assert.Contains("HotkeyCommand.PromptBuilder", Read("AiteBar", "HotkeyService.cs"));
        Assert.Contains("ChkShowPresetPromptBuilder", Read("AiteBar", "AppSettingsWindow.xaml"));
        Assert.Contains("LaunchUtilityAsync(\"PromptBuilder\"", Read("AiteBar", "MainWindow.xaml.cs"));
        Assert.Contains("public sealed class PromptBuilderUtility", Read("AiteBar", "PromptBuilderUtility.cs"));
    }

    [Fact]
    public void PromptGeneration_DoesNotApplyTextPreservationRejection()
    {
        string code = Read("AiteBar", "PromptBuilderWindow.xaml.cs");

        Assert.Contains("GeneratePromptBuilderStreamingAsync", code);
        Assert.DoesNotContain("ViolatesContentPreservation", code);
        Assert.DoesNotContain("ProtectTechnicalFragments", code);
    }

    [Fact]
    public void FirstLaunch_InheritsTextProcessingPlacementThenPersistsItsOwn()
    {
        string code = Read("AiteBar", "PromptBuilderWindow.xaml.cs");

        Assert.Contains("settings.PromptBuilderWindowPlacementInitialized", code);
        Assert.Contains("settings.TextProcessingWidth", code);
        Assert.Contains("settings.TextProcessingHeight", code);
        Assert.Contains("settings.PromptBuilderWindowPlacementInitialized = true;", code);
    }

    [Fact]
    public void SwitchingCategory_ClearsInputResultHistoryAndStatus()
    {
        string code = Read("AiteBar", "PromptBuilderWindow.xaml.cs");
        int start = code.IndexOf("private void ModeTabs_SelectionChanged", StringComparison.Ordinal);
        int end = code.IndexOf("private void TxtEditor_TextChanged", start, StringComparison.Ordinal);

        Assert.True(start >= 0 && end > start);
        string handler = code[start..end];
        Assert.Contains("if (selectedMode == _currentMode)", handler);
        Assert.Contains("_currentMode = selectedMode;", handler);
        Assert.Contains("Clear();", handler);

        int clearStart = code.IndexOf("private void Clear()", StringComparison.Ordinal);
        int clearEnd = code.IndexOf("private void ResetResultHistory()", clearStart, StringComparison.Ordinal);
        string clearMethod = code[clearStart..clearEnd];
        Assert.Contains("ResetResultHistory();", clearMethod);
        Assert.Contains("SetEditorText(string.Empty);", clearMethod);
        Assert.Contains("_operationHistory.Clear();", clearMethod);
        Assert.Contains("SetStatus(string.Empty);", clearMethod);
    }

    [Fact]
    public void ShiftEnterInEditor_ProcessesWhileEnterRemainsAvailableForNewLine()
    {
        string code = Read("AiteBar", "PromptBuilderWindow.xaml.cs");

        Assert.Contains("modifiers == ModifierKeys.Shift", code);
        Assert.Contains("TxtEditor.IsKeyboardFocusWithin", code);
        Assert.Contains("modifiers == ModifierKeys.Control", code);
        Assert.DoesNotContain("modifiers == ModifierKeys.None", code);
    }

    private static string Read(params string[] segments) =>
        File.ReadAllText(Path.Combine([FindRepoRoot(), .. segments]));

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

        throw new DirectoryNotFoundException("Could not find repository root.");
    }
}
