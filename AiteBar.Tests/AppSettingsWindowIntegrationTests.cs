using System;
using System.IO;

namespace AiteBar.Tests;

public sealed class AppSettingsWindowIntegrationTests
{
    [Fact]
    public void ProgramSettings_IsNormalByDefaultAndOffersAnExplicitSessionPin()
    {
        string repoRoot = FindRepoRoot();
        string settingsCode = File.ReadAllText(Path.Combine(repoRoot, "AiteBar", "AppSettingsWindow.xaml.cs"));
        string mainWindowCode = File.ReadAllText(Path.Combine(repoRoot, "AiteBar", "MainWindow.xaml.cs"));

        Assert.Contains("private void BtnKeepOnTop_Click", settingsCode);
        Assert.Contains("Topmost = BtnKeepOnTop.IsChecked == true;", settingsCode);
        Assert.DoesNotContain("EnsureAlwaysOnTop", settingsCode);
        Assert.DoesNotContain("NativeMethods.SetWindowPos", settingsCode);
        Assert.Contains("settingsWindow.Show();", mainWindowCode);
        Assert.DoesNotContain("new AppSettingsWindow(this).ShowDialog();", mainWindowCode);
        Assert.Contains("private AppSettingsWindow? _appSettingsWindow;", mainWindowCode);
        Assert.Contains("_appSettingsWindow.Activate();", mainWindowCode);
    }

    [Fact]
    public void About_UsesTheReusableFinalSettingsSectionInsteadOfASeparateWindow()
    {
        string repoRoot = FindRepoRoot();
        string settingsXaml = File.ReadAllText(Path.Combine(repoRoot, "AiteBar", "AppSettingsWindow.xaml"));
        string settingsCode = File.ReadAllText(Path.Combine(repoRoot, "AiteBar", "AppSettingsWindow.xaml.cs"));
        string mainWindowCode = File.ReadAllText(Path.Combine(repoRoot, "AiteBar", "MainWindow.xaml.cs"));
        string trayCode = File.ReadAllText(Path.Combine(repoRoot, "AiteBar", "MainWindow.TrayMenuHandler.cs"));

        Assert.Contains("x:Name=\"AboutSettingsSection\"", settingsXaml);
        Assert.Contains("public void NavigateToSection(AppSettingsSection section)", settingsCode);
        Assert.Contains("ShowAppSettingsWindow(AppSettingsSection section = AppSettingsSection.General)", mainWindowCode);
        Assert.Contains("ShowAppSettingsWindow(AppSettingsSection.About)", trayCode);
        Assert.DoesNotContain("new AboutWindow", trayCode);
        Assert.False(File.Exists(Path.Combine(repoRoot, "AiteBar", "AboutWindow.xaml")));
        Assert.False(File.Exists(Path.Combine(repoRoot, "AiteBar", "AboutWindow.xaml.cs")));
    }

    [Fact]
    public void LanguageSelection_PersistsUiCultureImmediately()
    {
        string repoRoot = FindRepoRoot();
        string settingsCode = File.ReadAllText(Path.Combine(repoRoot, "AiteBar", "AppSettingsWindow.xaml.cs"));

        Assert.Contains("private string _selectedUiCulture = LocalizationService.AutoCulture;", settingsCode);
        Assert.Contains("private async void CmbLanguage_SelectionChanged", settingsCode);
        Assert.Contains("_selectedUiCulture = LocalizationService.NormalizeCultureName(selectedCulture);", settingsCode);
        Assert.Contains("_settings.UiCulture = _selectedUiCulture;", settingsCode);
        Assert.Contains("_mainWindow.GetSettingsService().NormalizeAppState();", settingsCode);
        Assert.Contains("await _mainWindow.GetSettingsService().SaveAsync();", settingsCode);
        Assert.Contains("LocalizationService.EnsureAppliedCulture();", settingsCode);
        Assert.Contains("LocalizationService.RefreshLocalizedBindings(this);", settingsCode);
        Assert.Contains("string language = _selectedUiCulture;", settingsCode);
        Assert.Contains("SetComboValue(CmbLanguage, language);", settingsCode);
        Assert.Contains("private void RefreshLocalizedUi()", settingsCode);
        Assert.Contains("CaptureContextRowDrafts()", settingsCode);
        Assert.Contains("ApplyContextRowDrafts(drafts);", settingsCode);
        Assert.Contains("ContextStateHelper.IsCustomizedContextNameInput", settingsCode);
        Assert.Contains("protected override void OnLocalizationChanged()", settingsCode);
    }

    [Fact]
    public void LanguageSelection_HandlesSaveAsyncErrorGracefully()
    {
        string repoRoot = FindRepoRoot();
        string settingsCode = File.ReadAllText(Path.Combine(repoRoot, "AiteBar", "AppSettingsWindow.xaml.cs"));

        Assert.Contains("catch (Exception ex)", settingsCode);
        Assert.Contains("Logger.Log(ex);", settingsCode);
        Assert.Contains("Settings_SaveFailed", settingsCode);
    }

    [Fact]
    public void ValidateHotkeyBindings_ChecksReservedHotkeys()
    {
        string repoRoot = FindRepoRoot();
        string settingsCode = File.ReadAllText(Path.Combine(repoRoot, "AiteBar", "AppSettingsWindow.xaml.cs"));

        Assert.Contains("HotkeyValidationHelper.IsReservedHotkey", settingsCode);
        Assert.Contains("HotkeyGlobalReservedMessage", settingsCode);
    }

    [Fact]
    public void NormalizeDiscreteSettings_NormalizesOnLoad()
    {
        string repoRoot = FindRepoRoot();
        string settingsCode = File.ReadAllText(Path.Combine(repoRoot, "AiteBar", "AppSettingsWindow.xaml.cs"));

        Assert.Contains("private void NormalizeDiscreteSettings()", settingsCode);
        Assert.Contains("NormalizeDiscreteSettings();", settingsCode);
    }

    [Fact]
    public void EveryBuiltInWindowUtility_HasASettingsBindingAndExecutionCommand()
    {
        string repoRoot = FindRepoRoot();
        string settingsCode = File.ReadAllText(Path.Combine(repoRoot, "AiteBar", "AppSettingsWindow.xaml.cs"));
        string mainWindowCode = File.ReadAllText(Path.Combine(repoRoot, "AiteBar", "MainWindow.xaml.cs"));

        Assert.Contains("HotkeyIconConverter.SetBinding(_settings.IconConverterHotkey);", settingsCode);
        Assert.Contains("settings.IconConverterHotkey = iconConverterBinding;", settingsCode);
        Assert.Contains("HotkeyClipboardManager.SetBinding(_settings.ClipboardManagerHotkey);", settingsCode);
        Assert.Contains("settings.ClipboardManagerHotkey = clipboardManagerBinding;", settingsCode);
        Assert.Contains("HotkeyTextProcessing.SetBinding(_settings.TextProcessingHotkey);", settingsCode);
        Assert.Contains("settings.TextProcessingHotkey = textProcessingBinding;", settingsCode);
        Assert.Contains("HotkeyZenEditor.SetBinding(_settings.ZenEditorHotkey);", settingsCode);
        Assert.Contains("settings.ZenEditorHotkey = zenEditorBinding;", settingsCode);
        Assert.Contains("case HotkeyCommand.IconConverter:", mainWindowCode);
        Assert.Contains("LaunchUtilityAsync(\"IconConverter\"", mainWindowCode);
        Assert.Contains("case HotkeyCommand.ClipboardManager:", mainWindowCode);
        Assert.Contains("LaunchUtilityAsync(\"ClipboardManager\"", mainWindowCode);
        Assert.Contains("case HotkeyCommand.TextProcessing:", mainWindowCode);
        Assert.Contains("LaunchUtilityAsync(\"TextProcessing\"", mainWindowCode);
        Assert.Contains("case HotkeyCommand.ZenEditor:", mainWindowCode);
        Assert.Contains("LaunchUtilityAsync(\"ZenEditor\"", mainWindowCode);
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
