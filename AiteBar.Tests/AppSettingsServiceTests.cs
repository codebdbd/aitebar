using System.Linq;
using AiteBar;
using Xunit;

namespace AiteBar.Tests;

public sealed class AppSettingsServiceTests
{
    [Fact]
    public void NormalizeAppState_MigratesLegacyWinZShowHotkeyToAlt4()
    {
        var settingsService = new AppSettingsService();
        AppSettings settings = settingsService.Settings;
        settings.GlobalHotkeyCtrl = false;
        settings.GlobalHotkeyAlt = false;
        settings.GlobalHotkeyShift = false;
        settings.GlobalHotkeyWin = true;
        settings.GlobalHotkeyKey = "Z";

        bool changed = settingsService.NormalizeAppState();

        Assert.True(changed);
        Assert.False(settings.GlobalHotkeyCtrl);
        Assert.True(settings.GlobalHotkeyAlt);
        Assert.False(settings.GlobalHotkeyShift);
        Assert.False(settings.GlobalHotkeyWin);
        Assert.Equal("D4", settings.GlobalHotkeyKey);
    }

    [Fact]
    public void NormalizeAppState_MigratesCtrlAltZShowHotkeyToAlt4()
    {
        var settingsService = new AppSettingsService();
        AppSettings settings = settingsService.Settings;
        settings.GlobalHotkeyCtrl = true;
        settings.GlobalHotkeyAlt = true;
        settings.GlobalHotkeyShift = false;
        settings.GlobalHotkeyWin = false;
        settings.GlobalHotkeyKey = "Z";

        bool changed = settingsService.NormalizeAppState();

        Assert.True(changed);
        Assert.False(settings.GlobalHotkeyCtrl);
        Assert.True(settings.GlobalHotkeyAlt);
        Assert.False(settings.GlobalHotkeyShift);
        Assert.False(settings.GlobalHotkeyWin);
        Assert.Equal("D4", settings.GlobalHotkeyKey);
    }

    [Fact]
    public void NormalizeAppState_PreservesCustomShowHotkey()
    {
        var settingsService = new AppSettingsService();
        AppSettings settings = settingsService.Settings;
        settings.GlobalHotkeyCtrl = true;
        settings.GlobalHotkeyAlt = false;
        settings.GlobalHotkeyShift = true;
        settings.GlobalHotkeyWin = false;
        settings.GlobalHotkeyKey = "Space";

        settingsService.NormalizeAppState();

        Assert.True(settings.GlobalHotkeyCtrl);
        Assert.False(settings.GlobalHotkeyAlt);
        Assert.True(settings.GlobalHotkeyShift);
        Assert.False(settings.GlobalHotkeyWin);
        Assert.Equal("Space", settings.GlobalHotkeyKey);
    }

    [Fact]
    public void NormalizeAppState_CreatesEightContextsWithOnlyFirstEnabledByDefault()
    {
        var settingsService = new AppSettingsService();

        settingsService.NormalizeAppState();

        Assert.Equal(8, settingsService.Settings.Contexts.Count);
        Assert.True(settingsService.Settings.Contexts[0].IsEnabled);
        Assert.All(settingsService.Settings.Contexts.Skip(1), context => Assert.False(context.IsEnabled));
        Assert.Equal("context-1", settingsService.Settings.ActiveContextId);
    }

    [Fact]
    public void NormalizeAppState_PreservesExistingContextNamesIconsAndEnabledState()
    {
        var settingsService = new AppSettingsService();
        settingsService.Settings.Contexts =
        [
            new() { Id = "context-1", Name = "Main", IconGlyph = "\uE111", IsEnabled = true },
            new() { Id = "context-2", Name = "Work", IconGlyph = "\uE222", IsEnabled = true },
            new() { Id = "context-3", Name = "Hidden", IconGlyph = "\uE333", IsEnabled = false }
        ];

        settingsService.NormalizeAppState();

        Assert.Equal(8, settingsService.Settings.Contexts.Count);
        Assert.Equal("Main", settingsService.Settings.Contexts[0].Name);
        Assert.Equal("\uE111", settingsService.Settings.Contexts[0].IconGlyph);
        Assert.True(settingsService.Settings.Contexts[0].IsEnabled);
        Assert.Equal("Work", settingsService.Settings.Contexts[1].Name);
        Assert.Equal("\uE222", settingsService.Settings.Contexts[1].IconGlyph);
        Assert.True(settingsService.Settings.Contexts[1].IsEnabled);
        Assert.Equal("Hidden", settingsService.Settings.Contexts[2].Name);
        Assert.Equal("\uE333", settingsService.Settings.Contexts[2].IconGlyph);
        Assert.False(settingsService.Settings.Contexts[2].IsEnabled);
    }

    [Fact]
    public void NormalizeAppState_KeepsElementsAssignedToDisabledPanels()
    {
        var settingsService = new AppSettingsService();
        settingsService.Settings.Contexts =
        [
            new() { Id = "context-1", Name = "Main", IsEnabled = true },
            new() { Id = "context-2", Name = "Hidden", IsEnabled = false }
        ];
        settingsService.Settings.Elements =
        [
            new() { Id = "button-1", Name = "Hidden button", ContextId = "context-2" }
        ];

        settingsService.NormalizeAppState();

        Assert.Single(settingsService.Elements);
        Assert.Equal("context-2", settingsService.Elements[0].ContextId);
        Assert.Equal("context-1", settingsService.Settings.ActiveContextId);
    }
}
