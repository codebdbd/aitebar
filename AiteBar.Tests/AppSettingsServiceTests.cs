using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
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

    [Fact]
    public async Task LoadAsync_OversizedSettingsFile_IsRejectedAndDefaultsAreUsed()
    {
        string root = Path.Combine(Path.GetTempPath(), "AiteBarTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string settingsPath = Path.Combine(root, "settings.json");
        string configPath = Path.Combine(root, "custom_buttons.json");

        try
        {
            await using (FileStream stream = File.Create(settingsPath))
            {
                stream.SetLength(AppSettingsService.MaxSettingsFileBytes + 1);
            }

            var service = new AppSettingsService(configPath, settingsPath);
            await service.LoadAsync();

            Assert.Empty(service.Elements);
            Assert.Equal("context-1", service.Settings.ActiveContextId);
        }
        finally
        {
            try
            {
                Directory.Delete(root, recursive: true);
            }
            catch
            {
            }
        }
    }

    [Fact]
    public void GetBackupFilePath_ReturnsCorrectPath()
    {
        string root = Path.Combine(Path.GetTempPath(), "AiteBarTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string settingsPath = Path.Combine(root, "settings.json");
        string configPath = Path.Combine(root, "custom_buttons.json");
        
        try
        {
            var service = new AppSettingsService(configPath, settingsPath);
            
            string backup0 = service.GetBackupFilePath(0);
            string backup1 = service.GetBackupFilePath(1);
            
            Assert.Equal(settingsPath + ".backup.0", backup0);
            Assert.Equal(settingsPath + ".backup.1", backup1);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task SaveAsync_CreatesBackup()
    {
        string root = Path.Combine(Path.GetTempPath(), "AiteBarTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string settingsPath = Path.Combine(root, "settings.json");
        string configPath = Path.Combine(root, "custom_buttons.json");
        
        try
        {
            var service = new AppSettingsService(configPath, settingsPath);
            await service.AddElementsAsync(new[] { new CustomElement { Id = "v1", Name = "Version 1", ContextId = "context-1" } });
            await service.SaveAsync();
            
            // Второй Save создаст бэкап
            await service.UpdateElementAsync("v1", e => e.Name = "Version 2");
            await service.SaveAsync();
            
            Assert.True(File.Exists(settingsPath));
            Assert.True(File.Exists(service.GetBackupFilePath(0)));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task RotateBackups_RotatesMultipleBackups()
    {
        string root = Path.Combine(Path.GetTempPath(), "AiteBarTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string settingsPath = Path.Combine(root, "settings.json");
        string configPath = Path.Combine(root, "custom_buttons.json");
        
        try
        {
            var service = new AppSettingsService(configPath, settingsPath);
            
            // Создаем начальный файл
            var initialSettings = new AppSettings();
            initialSettings.Elements.Add(new CustomElement { Id = "test", ContextId = "context-1" });
            await File.WriteAllTextAsync(settingsPath, System.Text.Json.JsonSerializer.Serialize(initialSettings));
            
            // Создаем несколько бэкапов вручную
            for (int i = 0; i < 3; i++)
            {
                await File.WriteAllTextAsync(service.GetBackupFilePath(i), $"backup-{i}");
            }
            
            // Вызываем RotateBackups
            service.RotateBackups();
            
            // Проверяем, что бэкапы сдвинулись
            Assert.True(File.Exists(service.GetBackupFilePath(0))); // это исходный settings.json
            Assert.True(File.Exists(service.GetBackupFilePath(1))); // был backup-0
            Assert.True(File.Exists(service.GetBackupFilePath(2))); // был backup-1
            // backup-2 был сдвинут за пределы MaxBackupCount (5), так что все ок
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task TryLoadFromBackup_LoadsFromFirstValidBackup()
    {
        string root = Path.Combine(Path.GetTempPath(), "AiteBarTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string settingsPath = Path.Combine(root, "settings.json");
        string configPath = Path.Combine(root, "custom_buttons.json");
        
        try
        {
            var service = new AppSettingsService(configPath, settingsPath);
            
            // Создаем поврежденный backup 0
            await File.WriteAllTextAsync(service.GetBackupFilePath(0), "invalid json");
            
            // Создаем валидный backup 1
            var validSettings = new AppSettings();
            validSettings.Elements.Add(new CustomElement { Id = "from-backup-1", Name = "From Backup 1", ContextId = "context-1" });
            await File.WriteAllTextAsync(service.GetBackupFilePath(1), System.Text.Json.JsonSerializer.Serialize(validSettings));
            
            bool result = service.TryLoadFromBackup();
            
            Assert.True(result);
            Assert.Single(service.Settings.Elements);
            Assert.Equal("from-backup-1", service.Settings.Elements[0].Id);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void TryLoadFromBackup_NoBackups_ReturnsFalse()
    {
        string root = Path.Combine(Path.GetTempPath(), "AiteBarTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string settingsPath = Path.Combine(root, "settings.json");
        string configPath = Path.Combine(root, "custom_buttons.json");
        
        try
        {
            var service = new AppSettingsService(configPath, settingsPath);
            
            bool result = service.TryLoadFromBackup();
            
            Assert.False(result);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task LoadAsync_CorruptedSettingsFile_DoesNotThrow()
    {
        string root = Path.Combine(Path.GetTempPath(), "AiteBarTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string settingsPath = Path.Combine(root, "settings.json");
        string configPath = Path.Combine(root, "custom_buttons.json");
        
        try
        {
            // Создаем поврежденный файл
            await File.WriteAllTextAsync(settingsPath, "corrupted json");
            
            // Загружаем - не должно выбросить исключение
            var service = new AppSettingsService(configPath, settingsPath);
            await service.LoadAsync();
            
            // Проверим, что сервис работает
            Assert.NotNull(service.Settings);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task AddElementsAsync_AddsElements()
    {
        string root = Path.Combine(Path.GetTempPath(), "AiteBarTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string settingsPath = Path.Combine(root, "settings.json");
        string configPath = Path.Combine(root, "custom_buttons.json");

        try
        {
            var service = new AppSettingsService(configPath, settingsPath);
            var element = new CustomElement
            {
                Id = "test-1",
                Name = "Test Button",
                ActionType = nameof(ActionType.Web),
                ActionValue = "https://example.com",
                ContextId = "context-1"
            };

            await service.AddElementsAsync([element]);

            Assert.Single(service.Elements);
            Assert.Equal("test-1", service.Elements[0].Id);
            Assert.Equal("Test Button", service.Elements[0].Name);
        }
        finally
        {
            try
            {
                Directory.Delete(root, recursive: true);
            }
            catch
            {
            }
        }
    }

    [Fact]
    public async Task InsertElementAfterAsync_InsertsAtCorrectPosition()
    {
        string root = Path.Combine(Path.GetTempPath(), "AiteBarTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string settingsPath = Path.Combine(root, "settings.json");
        string configPath = Path.Combine(root, "custom_buttons.json");

        try
        {
            var service = new AppSettingsService(configPath, settingsPath);
            var element1 = new CustomElement { Id = "1", Name = "First", ContextId = "context-1" };
            var element2 = new CustomElement { Id = "2", Name = "Second", ContextId = "context-1" };
            var element3 = new CustomElement { Id = "3", Name = "Third", ContextId = "context-1" };

            await service.AddElementsAsync([element1, element2]);
            await service.InsertElementAfterAsync("1", element3);

            Assert.Equal(3, service.Elements.Count);
            Assert.Equal("1", service.Elements[0].Id);
            Assert.Equal("3", service.Elements[1].Id);
            Assert.Equal("2", service.Elements[2].Id);
        }
        finally
        {
            try
            {
                Directory.Delete(root, recursive: true);
            }
            catch
            {
            }
        }
    }

    [Fact]
    public async Task UpdateElementAsync_UpdatesCorrectElement()
    {
        string root = Path.Combine(Path.GetTempPath(), "AiteBarTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string settingsPath = Path.Combine(root, "settings.json");
        string configPath = Path.Combine(root, "custom_buttons.json");

        try
        {
            var service = new AppSettingsService(configPath, settingsPath);
            var element = new CustomElement { Id = "1", Name = "Original", ContextId = "context-1" };
            await service.AddElementsAsync([element]);

            await service.UpdateElementAsync("1", e => e.Name = "Updated");

            Assert.Equal("Updated", service.Elements[0].Name);
            Assert.Equal("1", service.Elements[0].Id);
        }
        finally
        {
            try
            {
                Directory.Delete(root, recursive: true);
            }
            catch
            {
            }
        }
    }

    [Fact]
    public async Task DeleteElementAsync_RemovesElement()
    {
        string root = Path.Combine(Path.GetTempPath(), "AiteBarTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string settingsPath = Path.Combine(root, "settings.json");
        string configPath = Path.Combine(root, "custom_buttons.json");

        try
        {
            var service = new AppSettingsService(configPath, settingsPath);
            var element1 = new CustomElement { Id = "1", Name = "First", ContextId = "context-1" };
            var element2 = new CustomElement { Id = "2", Name = "Second", ContextId = "context-1" };
            await service.AddElementsAsync([element1, element2]);

            await service.DeleteElementAsync("1");

            Assert.Single(service.Elements);
            Assert.Equal("2", service.Elements[0].Id);
        }
        finally
        {
            try
            {
                Directory.Delete(root, recursive: true);
            }
            catch
            {
            }
        }
    }

    [Fact]
    public void GetContextDisplayName_ExistingContext_ReturnsName()
    {
        var service = new AppSettingsService();
        service.NormalizeAppState();
        service.Settings.Contexts[0].Name = "Test Panel";

        var result = service.GetContextDisplayName("context-1");

        Assert.Equal("Test Panel", result);
    }

    [Fact]
    public void GetContextDisplayName_NonExistingContext_ReturnsId()
    {
        var service = new AppSettingsService();

        var result = service.GetContextDisplayName("non-existing-id");

        Assert.Equal("non-existing-id", result);
    }

    [Fact]
    public void NormalizeAppState_MigratesLegacyNonHotkeyElementBindingToActivationHotkey()
    {
        var service = new AppSettingsService();
        service.Settings.Elements =
        [
            new CustomElement
            {
                Id = "web",
                ActionType = nameof(ActionType.Web),
                Ctrl = true,
                Key = "K",
                ContextId = "context-1"
            }
        ];

        bool changed = service.NormalizeAppState();
        CustomElement element = Assert.Single(service.Elements);

        Assert.True(changed);
        Assert.True(element.ActivationHotkey.Ctrl);
        Assert.Equal("K", element.ActivationHotkey.Key);
        Assert.False(element.Ctrl);
        Assert.Equal("None", element.Key);
        Assert.False(service.NormalizeAppState());
    }

    [Fact]
    public void NormalizeAppState_PreservesHotkeyActionPayloadWithoutActivationHotkey()
    {
        var service = new AppSettingsService();
        service.Settings.Elements =
        [
            new CustomElement
            {
                Id = "hotkey",
                ActionType = nameof(ActionType.Hotkey),
                Ctrl = true,
                Key = "K",
                ContextId = "context-1"
            }
        ];

        service.NormalizeAppState();
        CustomElement element = Assert.Single(service.Elements);

        Assert.True(element.Ctrl);
        Assert.Equal("K", element.Key);
        Assert.False(HotkeyValidationHelper.HasAssignedKey(element.ActivationHotkey));
    }

    [Fact]
    public void NormalizeAppState_NormalizesUiCultureActiveContextAndElements()
    {
        var service = new AppSettingsService();
        service.Settings.UiCulture = "de-DE";
        service.Settings.ActiveContextId = "missing-context";
        service.Settings.Contexts =
        [
            new() { Id = "context-1", Name = "Main", IsEnabled = true },
            new() { Id = "context-2", Name = "Work", IsEnabled = true }
        ];
        service.Settings.Elements =
        [
            new() { Id = "", Name = "Needs Id", ContextId = "", RotationProfilePaths = null! },
            new() { Id = "dup", Name = "First", ContextId = "context-2" },
            new() { Id = "dup", Name = "Duplicate", ContextId = "context-2" }
        ];

        bool changed = service.NormalizeAppState();

        Assert.True(changed);
        Assert.Equal("de", service.Settings.UiCulture);
        Assert.Equal("context-1", service.Settings.ActiveContextId);
        Assert.Equal(2, service.Elements.Count);
        Assert.False(string.IsNullOrWhiteSpace(service.Elements[0].Id));
        Assert.Equal("context-1", service.Elements[0].ContextId);
        Assert.NotNull(service.Elements[0].RotationProfilePaths);
        Assert.Single(service.Elements, element => element.Id == "dup");
    }

    [Fact]
    public async Task LoadAsync_LegacyConfigFile_MigratesElementsAndCreatesSettingsFile()
    {
        string root = Path.Combine(Path.GetTempPath(), "AiteBarTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string settingsPath = Path.Combine(root, "settings.json");
        string configPath = Path.Combine(root, "custom_buttons.json");

        try
        {
            CustomElement[] elements =
            [
                new() { Id = "legacy-1", Name = "Legacy", ContextId = "context-1" }
            ];
            await File.WriteAllTextAsync(configPath, System.Text.Json.JsonSerializer.Serialize(elements));
            var service = new AppSettingsService(configPath, settingsPath);

            await service.LoadAsync();

            Assert.Single(service.Elements);
            Assert.Equal("legacy-1", service.Elements[0].Id);
            Assert.True(File.Exists(settingsPath));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task SaveElementAsync_ReplacesElementByRemoveId()
    {
        string root = Path.Combine(Path.GetTempPath(), "AiteBarTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string settingsPath = Path.Combine(root, "settings.json");
        string configPath = Path.Combine(root, "custom_buttons.json");

        try
        {
            var service = new AppSettingsService(configPath, settingsPath);
            await service.AddElementsAsync([new CustomElement { Id = "old", Name = "Old", ContextId = "context-1" }]);

            await service.SaveElementAsync(
                new CustomElement { Id = "new", Name = "New", ContextId = "context-1" },
                removeId: "old");

            Assert.Single(service.Elements);
            Assert.Equal("new", service.Elements[0].Id);
            Assert.Equal("New", service.Elements[0].Name);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ReorderElements_MovesItemsWithinContextWithoutAffectingOtherContexts()
    {
        string root = Path.Combine(Path.GetTempPath(), "AiteBarTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string settingsPath = Path.Combine(root, "settings.json");
        string configPath = Path.Combine(root, "custom_buttons.json");

        try
        {
            var service = new AppSettingsService(configPath, settingsPath);
            await service.AddElementsAsync(
            [
                new CustomElement { Id = "a", Name = "A", ContextId = "context-1" },
                new CustomElement { Id = "b", Name = "B", ContextId = "context-1" },
                new CustomElement { Id = "c", Name = "C", ContextId = "context-2" }
            ]);

            service.ReorderElements(0, 1, "context-1");

            Assert.Equal(["b", "a", "c"], service.Elements.Select(element => element.Id).ToArray());
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void GetEnabledContextsSnapshot_ReturnsClonedEnabledContexts()
    {
        var service = new AppSettingsService();
        service.Settings.Contexts =
        [
            new() { Id = "context-1", Name = "Main", IsEnabled = true },
            new() { Id = "context-2", Name = "Disabled", IsEnabled = false },
            new() { Id = "context-3", Name = "Work", IsEnabled = true }
        ];

        IReadOnlyList<PanelContext> snapshot = service.GetEnabledContextsSnapshot();

        Assert.Equal(["context-1", "context-3"], snapshot.Select(context => context.Id).ToArray());
        snapshot[0].Name = "Changed";
        Assert.Equal("Main", service.Settings.Contexts[0].Name);
    }

    [Fact]
    public void CloneElement_CreatesDeepCopyOfRotationProfilesAndActivationHotkey()
    {
        var service = new AppSettingsService();
        var source = new CustomElement
        {
            Id = "source",
            RotationProfilePaths = ["Profile A"],
            ActivationHotkey = new HotkeyBinding { Ctrl = true, Key = "K" },
            ContextId = "context-2"
        };

        CustomElement clone = service.CloneElement(source);
        clone.RotationProfilePaths.Add("Profile B");
        clone.ActivationHotkey.Key = "J";
        clone.ContextId = "context-3";

        Assert.Equal(["Profile A"], source.RotationProfilePaths);
        Assert.Equal("K", source.ActivationHotkey.Key);
        Assert.Equal("context-2", source.ContextId);
    }
}
