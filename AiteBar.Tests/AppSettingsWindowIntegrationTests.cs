using System;
using System.IO;

namespace AiteBar.Tests;

public sealed class AppSettingsWindowIntegrationTests
{
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
        Assert.Contains("string language = _selectedUiCulture;", settingsCode);
        Assert.Contains("SetComboValue(CmbLanguage, _selectedUiCulture);", settingsCode);
        Assert.Contains("LocalizationService.RefreshLocalizedBindings(this);", settingsCode);
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
