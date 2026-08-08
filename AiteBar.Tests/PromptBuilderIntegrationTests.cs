using System.IO;

namespace AiteBar.Tests;

public sealed class PromptBuilderIntegrationTests
{
    [Fact]
    public void Window_UsesAnalyticsCategoryAndFixedStatusArea()
    {
        string xaml = Read("AiteBar", "PromptBuilderWindow.xaml");

        Assert.Contains("Width=\"1280\" Height=\"840\"", xaml);
        Assert.Contains("<Grid Margin=\"32,16,32,24\">", xaml);
        Assert.Contains("x:Name=\"ModeProgramming\"", xaml);
        Assert.Contains("x:Name=\"ModeImages\"", xaml);
        Assert.Contains("x:Name=\"ModePaintings\"", xaml);
        Assert.Contains("x:Name=\"ModeAnimation\"", xaml);
        Assert.Contains("x:Name=\"ModeIcons\"", xaml);
        Assert.Contains("x:Name=\"ModeGraphics\"", xaml);
        Assert.Contains("x:Name=\"TextOptionsHost\"", xaml);
        Assert.Contains("x:Name=\"CmbTextType\"", xaml);
        Assert.Contains("x:Name=\"CmbTextTone\"", xaml);
        Assert.Contains("x:Name=\"VideoDirectionHost\"", xaml);
        Assert.Contains("x:Name=\"CmbVideoDirection\"", xaml);
        Assert.Contains("x:Name=\"ProgrammingTaskHost\"", xaml);
        Assert.Contains("x:Name=\"CmbProgrammingTask\"", xaml);
        Assert.Contains("x:Name=\"TxtProgrammingTaskOutcome\"", xaml);
        Assert.Contains("TxtProgrammingTaskOutcome\" Grid.Column=\"3\" Margin=\"16,0,0,0\"", xaml);
        Assert.Contains("x:Name=\"TxtVideoDirectionOutcome\"", xaml);
        Assert.Contains("x:Name=\"TxtTextOptionsOutcome\"", xaml);
        Assert.Contains("x:Name=\"VisualOptionsHost\"", xaml);
        Assert.Contains("x:Name=\"CmbVisualTarget\"", xaml);
        Assert.Contains("x:Name=\"IconOptionsHost\"", xaml);
        Assert.Contains("x:Name=\"CmbIconPlatform\"", xaml);
        Assert.Contains("x:Name=\"CmbIconStyle\"", xaml);
        Assert.Contains("x:Name=\"GraphicOptionsHost\"", xaml);
        Assert.Contains("x:Name=\"CmbGraphicType\"", xaml);
        Assert.Contains("x:Name=\"CmbGraphicStyle\"", xaml);
        Assert.Contains("PromptBuilder_StyleLabel", xaml);
        Assert.Contains("x:Name=\"AnalysisDirectionHost\"", xaml);
        Assert.Contains("x:Name=\"CmbAnalysisDirection\"", xaml);
        Assert.Contains("x:Name=\"TxtAnalysisDirectionOutcome\"", xaml);
        Assert.Contains("x:Name=\"ModeTexts\"", xaml);
        Assert.Contains("x:Name=\"ModeVideo\"", xaml);
        Assert.Contains("x:Name=\"ModeMusic\"", xaml);
        Assert.Contains("x:Name=\"ModeAnalytics\"", xaml);
        Assert.DoesNotContain("x:Name=\"ModeAnalysis\"", xaml);
        Assert.DoesNotContain("x:Name=\"ModeIdeas\"", xaml);
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
    public void SwitchingCategory_PreservesPerCategoryDraftInsteadOfClearingIt()
    {
        string code = Read("AiteBar", "PromptBuilderWindow.xaml.cs");
        int start = code.IndexOf("private void ModeTabs_SelectionChanged", StringComparison.Ordinal);
        int end = code.IndexOf("private void TxtEditor_TextChanged", start, StringComparison.Ordinal);

        Assert.True(start >= 0 && end > start);
        string handler = code[start..end];
        Assert.Contains("if (selectedMode == _currentMode)", handler);
        Assert.Contains("SaveEditorText();", handler);
        Assert.Contains("_currentMode = selectedMode;", handler);
        Assert.Contains("RestoreEditorText(_settingsService.Settings);", handler);
        Assert.DoesNotContain("Clear();", handler);

        int clearStart = code.IndexOf("private void Clear()", StringComparison.Ordinal);
        int clearEnd = code.IndexOf("private void ResetResultHistory()", clearStart, StringComparison.Ordinal);
        string clearMethod = code[clearStart..clearEnd];
        Assert.Contains("ResetResultHistory();", clearMethod);
        Assert.Contains("SetEditorText(string.Empty);", clearMethod);
        Assert.Contains("_operationHistory.Clear();", clearMethod);
        Assert.Contains("SetStatus(string.Empty);", clearMethod);
        Assert.Contains("SaveEditorText();", clearMethod);
    }

    [Fact]
    public void Drafts_PersistOriginalAndGeneratedPromptPerCategory()
    {
        string code = Read("AiteBar", "PromptBuilderWindow.xaml.cs");
        string models = Read("AiteBar", "Models.cs");

        Assert.Contains("public Dictionary<string, PromptBuilderDraft> PromptBuilderDrafts", models);
        Assert.Contains("Input = _hasSuccessfulResult ? _originalText", code);
        Assert.Contains("Result = _hasSuccessfulResult ? _processedText", code);
        Assert.Contains("_hasSuccessfulResult = true;", code);
        Assert.Contains("_lastOriginalText = draft.Input;", code);
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
