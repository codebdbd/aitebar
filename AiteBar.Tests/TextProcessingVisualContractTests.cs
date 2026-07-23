using System.IO;

namespace AiteBar.Tests;

public sealed class TextProcessingVisualContractTests
{
    [Fact]
    public void Window_ContainsReleaseActionsAndTopSystemStatus()
    {
        string xaml = ReadWindowXaml();

        Assert.Contains("x:Name=\"BtnPaste\"", xaml);
        Assert.Contains("x:Name=\"BtnCopy\"", xaml);
        Assert.Contains("x:Name=\"BtnRepeat\"", xaml);
        Assert.Contains("x:Name=\"BtnToggleVersion\"", xaml);
        Assert.Contains("x:Name=\"BtnClear\"", xaml);
        Assert.Contains("x:Name=\"BtnProcess\"", xaml);
        Assert.Contains("x:Name=\"TxtModelState\" Grid.Column=\"1\"", xaml);
        Assert.DoesNotContain("x:Name=\"ModelStateBorder\"", xaml);
        Assert.DoesNotContain("TextProcessing_DataWarning", xaml);
    }

    [Fact]
    public void Window_PreservesOversizedTextAndUsesFixedEditorWidth()
    {
        string xaml = ReadWindowXaml();

        Assert.DoesNotContain("MaxLength=", xaml);
        Assert.Contains("<ColumnDefinition Width=\"738\"/>", xaml);
        Assert.DoesNotContain("WindowState=\"Maximized\"", xaml);
        Assert.Contains("Width=\"1280\" Height=\"780\"", xaml);
        Assert.Contains("MinWidth=\"1000\" MinHeight=\"700\"", xaml);
        Assert.Contains("<DockPanel x:Name=\"ContentHost\" Width=\"974\"", xaml);
        Assert.Contains("HorizontalAlignment=\"Center\" VerticalAlignment=\"Stretch\"", xaml);
    }

    [Fact]
    public void Window_ExposesAutomationMetadataAndKeyboardHandlers()
    {
        string xaml = ReadWindowXaml();

        Assert.Contains("AutomationProperties.Name=", xaml);
        Assert.Contains("AutomationProperties.HelpText=", xaml);
        Assert.Contains("AutomationProperties.LiveSetting=", xaml);
        Assert.Contains("PreviewKeyDown=\"Window_PreviewKeyDown\"", xaml);
        Assert.Contains("<Trigger Property=\"IsKeyboardFocused\" Value=\"True\">", xaml);
    }

    [Fact]
    public void Window_UsesCompactAlignedControlsAndSystemGlyphs()
    {
        string xaml = ReadWindowXaml();

        Assert.Contains("Padding=\"12,10\"", xaml);
        Assert.Contains("Margin=\"13,11,0,0\"", xaml);
        Assert.Contains("x:Name=\"PART_ContentHost\" Margin=\"0\"", xaml);
        Assert.DoesNotContain("Margin=\"{TemplateBinding Padding}\"", xaml);
        Assert.Contains("Property=\"local:KeyboardFocusVisualService.ShowKeyboardFocusCue\" Value=\"True\"", xaml);
        Assert.Contains("TargetName=\"EditorFocusBorder\" Property=\"BorderBrush\" Value=\"#3ABEFF\"", xaml);
        Assert.Contains("<ColumnDefinition Width=\"10\"/>", xaml);
        Assert.Contains("FontFamily=\"Segoe MDL2 Assets\"", xaml);
        Assert.Contains("FontSize=\"18\" FontWeight=\"Normal\"", xaml);
        Assert.Contains("<ColumnDefinition Width=\"220\"/>", xaml);
        Assert.DoesNotContain("IconRailButtonStyle", xaml);
        Assert.DoesNotContain("Width=\"220\" Height=\"52\"", xaml);
        Assert.Equal(5, xaml.Split(
            "Style=\"{StaticResource CommandButtonStyle}\"",
            StringSplitOptions.None).Length - 1);
        Assert.Contains("Style=\"{StaticResource PrimaryCommandButtonStyle}\"", xaml);
        Assert.Contains("<Grid MinHeight=\"284\">", xaml);
        Assert.DoesNotContain("FluentSystemIcons", xaml);
        Assert.DoesNotContain("Padding=\"24,20\"", xaml);
        Assert.DoesNotContain("Padding=\"25,21\"", xaml);
    }

    [Fact]
    public void Window_ReusesUtilityTabsAndKeepsModelPopupConstrained()
    {
        string xaml = ReadWindowXaml();

        Assert.Contains("Style=\"{StaticResource UnderlineTabControlStyle}\"", xaml);
        Assert.Contains("Style=\"{StaticResource TextProcessingTabItemStyle}\"", xaml);
        Assert.DoesNotContain("ModeRadioButtonStyle", xaml);
        Assert.Contains("ResizeMode=\"CanResize\"", xaml);
        Assert.DoesNotContain("CanResizeWithGrip", xaml);
        Assert.Contains("DropDownOpened=\"CmbModels_DropDownOpened\"", xaml);
        Assert.Contains("TextTrimming=\"CharacterEllipsis\"", xaml);
        Assert.DoesNotContain("DisplayMemberPath=", xaml);
    }

    [Fact]
    public void RestoringUnavailableSavedModel_FallsBackWithoutErrorBanner()
    {
        string code = File.ReadAllText(Path.Combine(FindRepoRoot(), "AiteBar", "TextProcessingWindow.xaml.cs"));
        int start = code.IndexOf("private void RestoreModelSelection()", StringComparison.Ordinal);
        int end = code.IndexOf("private void SaveModelSelection()", start, StringComparison.Ordinal);

        Assert.True(start >= 0 && end > start);
        string restoreMethod = code[start..end];
        Assert.DoesNotContain("SetStatus(", restoreMethod);
        Assert.Contains("SaveModelSelection();", restoreMethod);
    }

    [Fact]
    public void UnavailableModel_IsNeverRenderedAsRuntimeError()
    {
        string code = File.ReadAllText(Path.Combine(FindRepoRoot(), "AiteBar", "TextProcessingWindow.xaml.cs"));

        Assert.DoesNotContain("SetStatus(LocalizationService.Get(\"TextProcessing_ModelUnavailable\"))", code);
    }

    [Fact]
    public void PrimaryAction_UsesOneGenericProcessCaptionForEveryMode()
    {
        string code = File.ReadAllText(Path.Combine(FindRepoRoot(), "AiteBar", "TextProcessingWindow.xaml.cs"));

        Assert.Contains("LocalizationService.Get(\"TextProcessing_ButtonProcess\")", code);
        Assert.DoesNotContain("GetModeButtonText", code);
    }

    [Fact]
    public void SuccessfulResult_CanSwitchBetweenOriginalAndProcessedText()
    {
        string code = File.ReadAllText(Path.Combine(FindRepoRoot(), "AiteBar", "TextProcessingWindow.xaml.cs"));

        Assert.Contains("private void BtnToggleVersion_Click", code);
        Assert.Contains("_isShowingOriginal = !_isShowingOriginal;", code);
        Assert.Contains("SetEditorText(_isShowingOriginal ? _originalText : _processedText);", code);
        Assert.Contains("BtnToggleVersion.Visibility = _hasSuccessfulResult", code);
        Assert.Contains("\"TextProcessing_ButtonAfterProcessing\"", code);
        Assert.Contains("\"TextProcessing_ButtonBeforeProcessing\"", code);
    }

    private static string ReadWindowXaml() => File.ReadAllText(Path.Combine(
        FindRepoRoot(),
        "AiteBar",
        "TextProcessingWindow.xaml"));

    private static string FindRepoRoot(
        [System.Runtime.CompilerServices.CallerFilePath] string sourceFile = "")
    {
        return Path.GetFullPath(Path.Combine(
            Path.GetDirectoryName(sourceFile)
                ?? throw new DirectoryNotFoundException("The test source directory was not found."),
            ".."));
    }
}
