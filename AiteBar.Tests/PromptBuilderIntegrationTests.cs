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
        Assert.Contains("x:Name=\"ModeIdeas\"", xaml);
        Assert.Contains("x:Name=\"ModeGraphics\"", xaml);
        Assert.Contains("x:Name=\"TextOptionsHost\"", xaml);
        Assert.Contains("x:Name=\"CmbTextType\"", xaml);
        Assert.Contains("x:Name=\"CmbTextTone\"", xaml);
        Assert.Contains("x:Name=\"VideoDirectionHost\"", xaml);
        Assert.Contains("x:Name=\"CmbVideoDirection\"", xaml);
        Assert.Contains("x:Name=\"ProgrammingTaskHost\"", xaml);
        Assert.Contains("x:Name=\"CmbProgrammingProjectType\"", xaml);
        Assert.Contains("x:Name=\"CmbProgrammingStyle\"", xaml);
        Assert.Contains("ProgrammingProjectType_Label", xaml);
        Assert.Contains("ProgrammingStyle_Label", xaml);
        Assert.Contains("x:Name=\"TxtVideoDirectionOutcome\"", xaml);
        Assert.Contains("x:Name=\"TxtTextOptionsOutcome\"", xaml);
        Assert.Contains("x:Name=\"VisualOptionsHost\"", xaml);
        Assert.Contains("x:Name=\"CmbVisualTarget\"", xaml);
        Assert.Contains("x:Name=\"GraphicOptionsHost\"", xaml);
        Assert.Contains("x:Name=\"CmbGraphicType\"", xaml);
        Assert.Contains("x:Name=\"CmbGraphicStyle\"", xaml);
        Assert.Contains("PromptBuilder_StyleLabel", xaml);
        Assert.Contains("x:Name=\"CmbPhotoSection\"", xaml);
        Assert.Contains("x:Name=\"CmbAnimationSection\"", xaml);
        Assert.Contains("AnimationSection_Label", xaml);
        Assert.Contains("x:Name=\"CmbPaintingSection\"", xaml);
        Assert.Contains("x:Name=\"CmbThemeSection\"", xaml);
        Assert.Contains("ThemeSection_Label", xaml);
        Assert.Contains("PaintingSection_Artists", Read("AiteBar", "Resources", "Strings.ru.resx"));
        Assert.Contains("PaintingSection_Classical", Read("AiteBar", "Resources", "Strings.resx"));
        Assert.Contains("PaintingSection_Modern", Read("AiteBar", "Resources", "Strings.ru.resx"));
        Assert.Contains("PaintingSection_Eastern", Read("AiteBar", "Resources", "Strings.uk.resx"));
        Assert.Contains("PaintingArtist_JMWTurner", Read("AiteBar", "Resources", "Strings.de.resx"));
        Assert.Contains("ThemeStyle_JapaneseHorror", Read("AiteBar", "Resources", "Strings.ru.resx"));
        Assert.Contains("ThemeSection_SciFi", Read("AiteBar", "Resources", "Strings.resx"));
        Assert.Contains("ThemeSection_War", Read("AiteBar", "Resources", "Strings.ru.resx"));
        Assert.Contains("ThemeSection_Sports", Read("AiteBar", "Resources", "Strings.uk.resx"));
        Assert.DoesNotContain("new(ThemeSection.Professions", Read("AiteBar", "PromptBuilderService.cs"));
        Assert.DoesNotContain("ThemeStyle_PilotCockpit", Read("AiteBar", "PromptBuilderService.cs"));
        Assert.Contains("x:Name=\"AnalysisDirectionHost\"", xaml);
        Assert.Contains("x:Name=\"CmbAnalysisDirection\"", xaml);
        Assert.Contains("x:Name=\"TxtAnalysisDirectionOutcome\"", xaml);
        Assert.Contains("x:Name=\"ModeTexts\"", xaml);
        Assert.Contains("x:Name=\"ModeVideo\"", xaml);
        Assert.Contains("x:Name=\"ModeMusic\"", xaml);
        Assert.Contains("x:Name=\"ModeAnalytics\"", xaml);
        Assert.DoesNotContain("x:Name=\"ModeAnalysis\"", xaml);
        Assert.DoesNotContain("x:Name=\"ModeIcons\"", xaml);
        Assert.DoesNotContain("x:Name=\"IconOptionsHost\"", xaml);
        Assert.Contains("x:Name=\"ModeStatusHost\" DockPanel.Dock=\"Top\" Height=\"52\"", xaml);
        Assert.DoesNotContain("x:Name=\"ModeVideoAudio\"", xaml);
        Assert.DoesNotContain("x:Name=\"ModeAnalysisIdeas\"", xaml);
        Assert.DoesNotContain("Panel.ZIndex=\"10\"", xaml);
        Assert.DoesNotContain("Grid.ColumnSpan=\"3\"", xaml);
    }

    [Fact]
    public void ContextMenuGlyphs_UseFontMetricsInsideTheFixedIconCell()
    {
        string xaml = Read("AiteBar", "App.xaml");
        string factory = Read("AiteBar", "AppContextMenuFactory.cs");

        Assert.Contains("x:Key=\"ContextMenuIconTextStyle\"", xaml);
        Assert.Contains("TargetType=\"{x:Type local:CenteredGlyphTextBlock}\"", xaml);
        Assert.Contains("new CenteredGlyphTextBlock", factory);
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
    public void ClearAndNewVariant_DoNotShowConfirmationDialogs()
    {
        string code = Read("AiteBar", "PromptBuilderWindow.xaml.cs");

        Assert.DoesNotContain("TextProcessing_ConfirmClear", code);
        Assert.DoesNotContain("TextProcessing_ConfirmRepeat", code);
        Assert.DoesNotContain("new DarkDialog", code);
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
        int start = code.IndexOf("private async Task ProcessAsync", StringComparison.Ordinal);
        int end = code.IndexOf("private static AiChatRequest CopyRequestWithModel", start, StringComparison.Ordinal);

        Assert.True(start >= 0 && end > start);
        string processMethod = code[start..end];

        Assert.Contains("GeneratePromptBuilderStreamingAsync", processMethod);
        Assert.DoesNotContain("model.Tier == TextProcessingModelTier.CertifiedAutomatic", processMethod);
        Assert.DoesNotContain("ViolatesContentPreservation", processMethod);
        Assert.DoesNotContain("ProtectTechnicalFragments", processMethod);
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
        Assert.Contains("RestoreEditorText(_settingsService.Settings, allowLastTextFallback: false);", handler);
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
    public void AutoOptions_AreAlwaysFirstBeforeLocalizedAlphabeticalOrdering()
    {
        string code = Read("AiteBar", "PromptBuilderWindow.xaml.cs");

        Assert.Contains("OrderAutoFirst(PromptBuilderService.GetAnimationStyles(_animationSection)", code);
        Assert.Contains("OrderAutoFirst(PromptBuilderService.PaintingArtists", code);
        Assert.Contains("OrderAutoFirst(PromptBuilderService.GetThemeStyles(_themeSection)", code);
        Assert.Contains("OrderAutoFirst(PromptBuilderService.GetProgrammingStyles(_programmingProjectType)", code);
        Assert.DoesNotContain("ProgrammingTaskType", code);
        Assert.Contains("PaintingStyleSection.Artists", code);
        Assert.Contains("OrderAutoFirst(definitions", code);
        Assert.Contains(".OrderBy(item => isAuto(item) ? 0 : 1)", code);
        Assert.Contains(".ThenBy(item => LocalizationService.Get(localizationKey(item))", code);
    }

    [Fact]
    public void PaintingsMode_PersistsAndRefreshesArtistFilter()
    {
        string code = Read("AiteBar", "PromptBuilderWindow.xaml.cs");
        string models = Read("AiteBar", "Models.cs");
        string settings = Read("AiteBar", "AppSettingsService.cs");

        Assert.Contains("private PaintingArtist _paintingArtist = PaintingArtist.Auto;", code);
        Assert.Contains("settings.PromptBuilderPaintingArtist = _paintingArtist;", code);
        Assert.Contains("_paintingArtist = _settingsService.Settings.PromptBuilderPaintingArtist;", code);
        Assert.Contains("PaintingStyleSection.Artists", code);
        Assert.Contains("TxtVisualStyleLabel.Text", code);
        Assert.DoesNotContain("private void CmbPaintingArtist_SelectionChanged", code);
        Assert.Contains("public PaintingArtist PromptBuilderPaintingArtist { get; set; } = PaintingArtist.Auto;", models);
        Assert.Contains("PromptBuilderPaintingArtist = original.PromptBuilderPaintingArtist", settings);
    }

    [Fact]
    public void GraphicsMode_AbsorbsLegacyIconsAndUsesIconStylesForIconType()
    {
        string code = Read("AiteBar", "PromptBuilderWindow.xaml.cs");
        string service = Read("AiteBar", "PromptBuilderService.cs");

        Assert.Contains("(int)PromptBuilderCategory.Icons => PromptBuilderCategory.Graphics", code);
        Assert.Contains("if (storedMode == (int)PromptBuilderCategory.Icons && _graphicType == GraphicType.Auto)", code);
        Assert.Contains("GraphicType.Icon", service);
        Assert.Contains("PromptBuilderService.IconStyles", code);
        Assert.Contains("PromptBuilderService.GetGraphicStyles(_graphicType)", code);
        Assert.DoesNotContain("RefreshIconOptions()", code);
    }

    [Fact]
    public void ImagesMode_PersistsAndFiltersStylesByPhotoSection()
    {
        string code = Read("AiteBar", "PromptBuilderWindow.xaml.cs");
        string models = Read("AiteBar", "Models.cs");
        string settings = Read("AiteBar", "AppSettingsService.cs");
        string service = Read("AiteBar", "PromptBuilderService.cs");

        Assert.Contains("private PhotoSection _photoSection = PhotoSection.All;", code);
        Assert.Contains("settings.PromptBuilderPhotoSection = _photoSection;", code);
        Assert.Contains("_photoSection = _settingsService.Settings.PromptBuilderPhotoSection;", code);
        Assert.Contains("RefreshPhotoSections();", code);
        Assert.Contains("PromptBuilderService.GetPhotoStyles(_photoSection)", code);
        Assert.Contains("public PhotoSection PromptBuilderPhotoSection { get; set; } = PhotoSection.All;", models);
        Assert.Contains("PromptBuilderPhotoSection = original.PromptBuilderPhotoSection", settings);
        Assert.Contains("public enum PhotoSection", service);
    }

    [Fact]
    public void ThemesMode_PersistsAndFiltersStylesByThemeSection()
    {
        string code = Read("AiteBar", "PromptBuilderWindow.xaml.cs");
        string models = Read("AiteBar", "Models.cs");
        string settings = Read("AiteBar", "AppSettingsService.cs");
        string service = Read("AiteBar", "PromptBuilderService.cs");

        Assert.Contains("private ThemeSection _themeSection = ThemeSection.All;", code);
        Assert.Contains("private ThemeStyle _themeStyle = ThemeStyle.Auto;", code);
        Assert.Contains("settings.PromptBuilderThemeSection = _themeSection;", code);
        Assert.Contains("settings.PromptBuilderThemeStyle = _themeStyle;", code);
        Assert.Contains("_themeSection = _settingsService.Settings.PromptBuilderThemeSection;", code);
        Assert.Contains("RefreshThemeSections();", code);
        Assert.Contains("PromptBuilderService.GetThemeStyles(_themeSection)", code);
        Assert.Contains("public ThemeSection PromptBuilderThemeSection { get; set; } = ThemeSection.All;", models);
        Assert.Contains("public ThemeStyle PromptBuilderThemeStyle { get; set; } = ThemeStyle.Auto;", models);
        Assert.Contains("PromptBuilderThemeSection = original.PromptBuilderThemeSection", settings);
        Assert.Contains("public enum ThemeSection", service);
    }

    [Fact]
    public void ProgrammingMode_UsesTypeAndStyleInsteadOfEngineeringTaskKinds()
    {
        string code = Read("AiteBar", "PromptBuilderWindow.xaml.cs");
        string models = Read("AiteBar", "Models.cs");
        string settings = Read("AiteBar", "AppSettingsService.cs");
        string service = Read("AiteBar", "PromptBuilderService.cs");

        Assert.Contains("private ProgrammingProjectType _programmingProjectType = ProgrammingProjectType.Auto;", code);
        Assert.Contains("private ProgrammingPromptStyle _programmingStyle = ProgrammingPromptStyle.Auto;", code);
        Assert.Contains("settings.PromptBuilderProgrammingProjectType = _programmingProjectType;", code);
        Assert.Contains("settings.PromptBuilderProgrammingStyle = _programmingStyle;", code);
        Assert.Contains("_programmingProjectType = _settingsService.Settings.PromptBuilderProgrammingProjectType;", code);
        Assert.Contains("PromptBuilderService.GetProgrammingStyles(_programmingProjectType)", code);
        Assert.Contains("public ProgrammingProjectType PromptBuilderProgrammingProjectType { get; set; } = ProgrammingProjectType.Auto;", models);
        Assert.Contains("public ProgrammingPromptStyle PromptBuilderProgrammingStyle { get; set; } = ProgrammingPromptStyle.Auto;", models);
        Assert.Contains("PromptBuilderProgrammingProjectType = original.PromptBuilderProgrammingProjectType", settings);
        Assert.Contains("public enum ProgrammingProjectType", service);
        Assert.DoesNotContain("TxtProgrammingTaskOutcome", Read("AiteBar", "PromptBuilderWindow.xaml"));
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
        Assert.Contains("private void RestoreEditorText(AppSettings settings, bool allowLastTextFallback = true)", code);
        Assert.Contains("allowLastTextFallback && _currentMode == (PromptBuilderCategory)settings.PromptBuilderLastMode", code);
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
