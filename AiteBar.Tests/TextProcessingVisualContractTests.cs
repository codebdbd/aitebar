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
        Assert.Contains("x:Name=\"BtnShowDiff\"", xaml);
        Assert.Contains("x:Name=\"BtnClear\"", xaml);
        Assert.Contains("x:Name=\"BtnProcess\"", xaml);
        Assert.Contains("x:Name=\"TxtModelState\" Grid.Column=\"1\"", xaml);
        Assert.DoesNotContain("x:Name=\"ModelStateBorder\"", xaml);
        Assert.DoesNotContain("x:Name=\"InfoStatusBorder\"", xaml);
        Assert.DoesNotContain("x:Name=\"TxtInfoMessage\"", xaml);
        Assert.Contains("TextProcessing_DataWarning", xaml);
    }

    [Fact]
    public void Window_PreservesOversizedTextAndUsesFixedEditorWidth()
    {
        string xaml = ReadWindowXaml();

        Assert.DoesNotContain("MaxLength=", xaml);
        Assert.Contains("<ColumnDefinition Width=\"738\"/>", xaml);
        Assert.DoesNotContain("WindowState=\"Maximized\"", xaml);
        Assert.Contains("Width=\"1280\" Height=\"840\"", xaml);
        Assert.Contains("MinWidth=\"1000\" MinHeight=\"700\"", xaml);
        Assert.Contains("<DockPanel x:Name=\"ContentHost\"", xaml);
        Assert.DoesNotContain("x:Name=\"ContentHost\" Width=", xaml);
        Assert.Contains("HorizontalAlignment=\"Center\" VerticalAlignment=\"Stretch\"", xaml);
        Assert.Contains("x:Name=\"LayoutViewport\"", xaml);
        Assert.Contains("HorizontalScrollBarVisibility=\"Auto\"", xaml);
    }

    [Fact]
    public void Window_ExposesAutomationMetadataAndKeyboardHandlers()
    {
        string xaml = ReadWindowXaml();

        Assert.Contains("AutomationProperties.Name=", xaml);
        Assert.Contains("AutomationProperties.HelpText=", xaml);
        Assert.Contains("AutomationProperties.LiveSetting=", xaml);
        Assert.Contains("PreviewKeyDown=\"Window_PreviewKeyDown\"", xaml);
        Assert.Contains("x:Name=\"BtnShowDiff\"", xaml);
        Assert.Equal(2, xaml.Split(
            "ContextMenu=\"{StaticResource TextEditingContextMenu}\"",
            StringSplitOptions.None).Length - 1);
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
        Assert.Contains("x:Name=\"FooterCommandColumn\" Width=\"150\"", xaml);
        Assert.Contains("x:Name=\"RailCommandColumn\" Width=\"150\"", xaml);
        Assert.Contains("<Button x:Name=\"BtnProcess\" Grid.Column=\"5\"", xaml);
        Assert.DoesNotContain("Text=\"&#xE945;\"", xaml);
        Assert.Contains("Width=\"36\" Height=\"36\" MinWidth=\"36\" MaxWidth=\"36\"", xaml);
        Assert.DoesNotContain("IconRailButtonStyle", xaml);
        Assert.DoesNotContain("Width=\"220\" Height=\"52\"", xaml);
        Assert.Equal(7, xaml.Split(
            "Style=\"{StaticResource CommandButtonStyle}\"",
            StringSplitOptions.None).Length - 1);
        Assert.Contains("Style=\"{StaticResource PrimaryCommandButtonStyle}\"", xaml);
        Assert.Contains("StreamingUiUpdateInterval = TimeSpan.FromMilliseconds(50)",
            File.ReadAllText(Path.Combine(FindRepoRoot(), "AiteBar", "TextProcessingWindow.xaml.cs")));
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
        Assert.Contains("ShowInTaskbar=\"True\"", xaml);
        Assert.Contains("DropDownOpened=\"CmbModels_DropDownOpened\"", xaml);
        Assert.Contains("TextTrimming=\"CharacterEllipsis\"", xaml);
        Assert.DoesNotContain("DisplayMemberPath=", xaml);
    }

    [Fact]
    public void RestoringUnavailableSavedModel_ShowsNotificationAndFallsBack()
    {
        string code = File.ReadAllText(Path.Combine(FindRepoRoot(), "AiteBar", "TextProcessingWindow.xaml.cs"));
        int start = code.IndexOf("private void RestoreModelSelection()", StringComparison.Ordinal);
        int end = code.IndexOf("private void SaveModelSelection()", start, StringComparison.Ordinal);

        Assert.True(start >= 0 && end > start);
        string restoreMethod = code[start..end];
        Assert.Contains("SetStatus(LocalizationService.Get(\"TextProcessing_ModelUnavailable\"))", restoreMethod);
        Assert.Contains("SaveModelSelection();", restoreMethod);
    }

    [Fact]
    public void UnavailableModel_ShowsNotificationWhenFallingBack()
    {
        string code = File.ReadAllText(Path.Combine(FindRepoRoot(), "AiteBar", "TextProcessingWindow.xaml.cs"));

        Assert.Contains("SetStatus(LocalizationService.Get(\"TextProcessing_ModelUnavailable\"))", code);
    }

    [Fact]
    public void PrimaryAction_UsesOneGenericProcessCaptionForEveryMode()
    {
        string code = File.ReadAllText(Path.Combine(FindRepoRoot(), "AiteBar", "TextProcessingWindow.xaml.cs"));

        Assert.Contains("LocalizationService.Get(\"TextProcessing_ButtonProcess\")", code);
        Assert.DoesNotContain("GetModeButtonText", code);
    }

    [Fact]
    public void ModelUsageAndProgress_UseEditorFooterWithoutChangingEditorHeight()
    {
        string code = File.ReadAllText(Path.Combine(FindRepoRoot(), "AiteBar", "TextProcessingWindow.xaml.cs"));

        Assert.Contains("TxtModelState.Text = _inlineInfoStatus;", code);
        Assert.Contains("TxtModelState.ToolTip = _inlineInfoStatus;", code);
        Assert.Contains("SetInfoStatus(LocalizationService.Format(\"TextProcessing_ModelUsed\"", code);
        Assert.Contains("SetInfoStatus(LocalizationService.Format(\"TextProcessing_Progress\"", code);
        Assert.DoesNotContain("InfoStatusBorder", code);
        Assert.DoesNotContain("TxtInfoMessage", code);
    }

    [Fact]
    public void SuccessfulResult_CanSwitchBetweenOriginalAndProcessedText()
    {
        string code = File.ReadAllText(Path.Combine(FindRepoRoot(), "AiteBar", "TextProcessingWindow.xaml.cs"));

        Assert.Contains("private void BtnToggleVersion_Click", code);
        Assert.Contains("_isShowingOriginal = _isShowingDiff || !_isShowingOriginal;", code);
        Assert.Contains("SetEditorText(_isShowingOriginal ? _originalText : _processedText);", code);
        Assert.Contains("TxtEditor.IsReadOnly = _isShowingOriginal;", code);
        Assert.Contains("BtnToggleVersion.IsEnabled = state.CanSwitchVersion", code);
        Assert.Contains("private void BtnShowDiff_Click", code);
        Assert.Contains("RenderDiff();", code);
        Assert.Contains("\"TextProcessing_ButtonShowOriginal\"", code);
        Assert.Contains("\"TextProcessing_ButtonShowResult\"", code);
        Assert.Contains("\"TextProcessing_ButtonShowDiff\"", code);
        Assert.Contains("\"TextProcessing_ButtonHideDiff\"", code);
        Assert.Contains("AutomationProperties.SetName(BtnShowDiff, ShowDiffLabel.Text)", code);
    }

    [Fact]
    public void Clear_RemovesOperationHistoryInsteadOfRecordingRecoverableText()
    {
        string code = File.ReadAllText(Path.Combine(FindRepoRoot(), "AiteBar", "TextProcessingWindow.xaml.cs"));
        int start = code.IndexOf("private void Clear()", StringComparison.Ordinal);
        int end = code.IndexOf("private void ResetResultHistory()", start, StringComparison.Ordinal);

        Assert.True(start >= 0 && end > start);
        string clearMethod = code[start..end];
        Assert.Contains("SetEditorText(string.Empty);", clearMethod);
        Assert.Contains("_operationHistory.Clear();", clearMethod);
        Assert.DoesNotContain("recordUndo: true", clearMethod);
    }

    [Fact]
    public void Minimize_UsesNormalTaskbarBehaviorInsteadOfHidingWindow()
    {
        string code = File.ReadAllText(Path.Combine(FindRepoRoot(), "AiteBar", "TextProcessingWindow.xaml.cs"));
        int start = code.IndexOf("private void Window_StateChanged", StringComparison.Ordinal);
        int end = code.IndexOf("private void Window_SizeChanged", start, StringComparison.Ordinal);

        Assert.True(start >= 0 && end > start);
        string stateChangedMethod = code[start..end];
        Assert.DoesNotContain("Hide();", stateChangedMethod);
        Assert.DoesNotContain("WindowState = WindowState.Normal;", stateChangedMethod);
        Assert.Contains("WindowState != WindowState.Minimized", stateChangedMethod);

        string utilityCode = File.ReadAllText(Path.Combine(
            FindRepoRoot(),
            "AiteBar",
            "TextProcessingUtility.cs"));
        Assert.DoesNotContain("{ Owner = owner }", utilityCode);
        Assert.Contains("owner as MainWindow", utilityCode);
    }

    private static string ReadWindowXaml() => File.ReadAllText(Path.Combine(
        FindRepoRoot(),
        "AiteBar",
        "TextProcessingWindow.xaml"));

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

        throw new DirectoryNotFoundException(
            "Repository root with AiteBar.sln was not found.");
    }
}
