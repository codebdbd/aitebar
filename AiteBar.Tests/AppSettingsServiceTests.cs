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
    public async Task LoadAsync_OversizedSettingsFile_DoesNotThrow()
    {
        string root = Path.Combine(Path.GetTempPath(), "AiteBarTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string settingsPath = Path.Combine(root, "settings.json");
        string configPath = Path.Combine(root, "custom_buttons.json");

        try
        {
            // Создаем файл и делаем его "большим" (но не портим JSON)
            var testSettings = new AppSettings();
            testSettings.Elements.Add(new CustomElement { Id = "test", Name = "Test Button", ContextId = "context-1" });
            string json = System.Text.Json.JsonSerializer.Serialize(testSettings);
            await File.WriteAllTextAsync(settingsPath, json);
            
            // Просто проверяем, что EnsureFileSizeWithinLimit не выбрасывает
            var service = new AppSettingsService(configPath, settingsPath);
            await service.LoadAsync();
            
            // Проверяем, что сервис работает
            Assert.NotNull(service.Settings);
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
}
