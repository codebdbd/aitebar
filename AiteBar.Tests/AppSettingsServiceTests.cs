using System;
using System.Globalization;
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
        var settings = settingsService.Settings;
        settings.GlobalHotkeyCtrl = false;
        settings.GlobalHotkeyAlt = false;
        settings.GlobalHotkeyShift = false;
        settings.GlobalHotkeyWin = true;
        settings.GlobalHotkeyKey = "Z";
        settingsService.Settings = settings;

        bool changed = settingsService.NormalizeAppState();

        Assert.True(changed);
        Assert.False(settingsService.Settings.GlobalHotkeyCtrl);
        Assert.True(settingsService.Settings.GlobalHotkeyAlt);
        Assert.False(settingsService.Settings.GlobalHotkeyShift);
        Assert.False(settingsService.Settings.GlobalHotkeyWin);
        Assert.Equal("D4", settingsService.Settings.GlobalHotkeyKey);
    }

    [Fact]
    public void NormalizeAppState_MigratesCtrlAltZShowHotkeyToAlt4()
    {
        var settingsService = new AppSettingsService();
        var settings = settingsService.Settings;
        settings.GlobalHotkeyCtrl = true;
        settings.GlobalHotkeyAlt = true;
        settings.GlobalHotkeyShift = false;
        settings.GlobalHotkeyWin = false;
        settings.GlobalHotkeyKey = "Z";
        settingsService.Settings = settings;

        bool changed = settingsService.NormalizeAppState();

        Assert.True(changed);
        Assert.False(settingsService.Settings.GlobalHotkeyCtrl);
        Assert.True(settingsService.Settings.GlobalHotkeyAlt);
        Assert.False(settingsService.Settings.GlobalHotkeyShift);
        Assert.False(settingsService.Settings.GlobalHotkeyWin);
        Assert.Equal("D4", settingsService.Settings.GlobalHotkeyKey);
    }

    [Fact]
    public void NormalizeAppState_PreservesCustomShowHotkey()
    {
        var settingsService = new AppSettingsService();
        var settings = settingsService.Settings;
        settings.GlobalHotkeyCtrl = true;
        settings.GlobalHotkeyAlt = false;
        settings.GlobalHotkeyShift = true;
        settings.GlobalHotkeyWin = false;
        settings.GlobalHotkeyKey = "Space";
        settingsService.Settings = settings;

        settingsService.NormalizeAppState();

        Assert.True(settingsService.Settings.GlobalHotkeyCtrl);
        Assert.False(settingsService.Settings.GlobalHotkeyAlt);
        Assert.True(settingsService.Settings.GlobalHotkeyShift);
        Assert.False(settingsService.Settings.GlobalHotkeyWin);
        Assert.Equal("Space", settingsService.Settings.GlobalHotkeyKey);
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
        var settings = settingsService.Settings;
        settings.Contexts =
        [
            new() { Id = "context-1", Name = "Main", IconGlyph = "\uE111", IsEnabled = true },
            new() { Id = "context-2", Name = "Work", IconGlyph = "\uE222", IsEnabled = true },
            new() { Id = "context-3", Name = "Hidden", IconGlyph = "\uE333", IsEnabled = false }
        ];
        settingsService.Settings = settings;

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
        var settings = settingsService.Settings;
        settings.Contexts =
        [
            new() { Id = "context-1", Name = "Main", IsEnabled = true },
            new() { Id = "context-2", Name = "Hidden", IsEnabled = false }
        ];
        settings.Elements =
        [
            new() { Id = "button-1", Name = "Hidden button", ContextId = "context-2" }
        ];
        settingsService.Settings = settings;

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
            await service.AddElementsAsync([new CustomElement { Id = "v1", Name = "Version 1", ContextId = "context-1" }]);
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
    public async Task SaveAsync_PersistsUiCultureAcrossReload()
    {
        string root = Path.Combine(Path.GetTempPath(), "AiteBarTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string settingsPath = Path.Combine(root, "settings.json");
        string configPath = Path.Combine(root, "custom_buttons.json");

        try
        {
            var writer = new AppSettingsService(configPath, settingsPath);
            var settings = writer.Settings;
            settings.UiCulture = "de";
            writer.Settings = settings;

            await writer.SaveAsync();

            var reader = new AppSettingsService(configPath, settingsPath);
            await reader.LoadAsync();

            Assert.Equal("de", reader.Settings.UiCulture);
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
    public async Task SaveAsync_ReplacesSettingsAfterTempWriteAndKeepsPreviousVersionAsBackup()
    {
        string root = Path.Combine(Path.GetTempPath(), "AiteBarTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string settingsPath = Path.Combine(root, "settings.json");
        string configPath = Path.Combine(root, "custom_buttons.json");

        try
        {
            var service = new AppSettingsService(configPath, settingsPath);
            var settings1 = service.Settings;
            settings1.UiCulture = "en";
            service.Settings = settings1;
            await service.SaveAsync();

            var settings2 = service.Settings;
            settings2.UiCulture = "de";
            service.Settings = settings2;
            await service.SaveAsync();

            string currentJson = await File.ReadAllTextAsync(settingsPath);
            string backupJson = await File.ReadAllTextAsync(service.GetBackupFilePath(0));
            var current = System.Text.Json.JsonSerializer.Deserialize<AppSettings>(currentJson);
            var backup = System.Text.Json.JsonSerializer.Deserialize<AppSettings>(backupJson);

            Assert.Equal("de", current?.UiCulture);
            Assert.Equal("en", backup?.UiCulture);
            Assert.Empty(Directory.GetFiles(root, "*.tmp"));
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
        var settings = service.Settings;
        settings.Contexts[0].Name = "Test Panel";
        settings.Contexts[0].IsNameCustomized = true;
        service.Settings = settings;

        var result = service.GetContextDisplayName("context-1");

        Assert.Equal("Test Panel", result);
    }

    [Fact]
    public void GetContextDisplayName_NonCustomizedDefaultContext_ReturnsCurrentCultureDisplayName()
    {
        CultureInfo originalCulture = CultureInfo.CurrentCulture;
        CultureInfo originalUiCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de");
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("de");

            var service = new AppSettingsService();
            var settings = service.Settings;
            settings.Contexts =
            [
                new() { Id = "context-1", Name = "Panel 1", IsNameCustomized = false, IsEnabled = true }
            ];
            service.Settings = settings;

            var result = service.GetContextDisplayName("context-1");

            Assert.Equal("Leiste 1", result);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
        }
    }

    [Fact]
    public void GetContextDisplayName_NonExistingContext_ReturnsId()
    {
        var service = new AppSettingsService();

        var result = service.GetContextDisplayName("non-existing-id");

        Assert.Equal("non-existing-id", result);
    }



    [Fact]
    public void NormalizeAppState_NormalizesUiCultureActiveContextAndElements()
    {
        var service = new AppSettingsService();
        var settings = service.Settings;
        settings.UiCulture = "de-DE";
        settings.ActiveContextId = "missing-context";
        settings.Contexts =
        [
            new() { Id = "context-1", Name = "Main", IsEnabled = true },
            new() { Id = "context-2", Name = "Work", IsEnabled = true }
        ];
        settings.Elements =
        [
            new() { Id = "", Name = "Needs Id", ContextId = "", RotationProfilePaths = null! },
            new() { Id = "dup", Name = "First", ContextId = "context-2" },
            new() { Id = "dup", Name = "Duplicate", ContextId = "context-2" }
        ];
        service.Settings = settings;

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
        var settings = service.Settings;
        settings.Contexts =
        [
            new() { Id = "context-1", Name = "Main", IsEnabled = true },
            new() { Id = "context-2", Name = "Disabled", IsEnabled = false },
            new() { Id = "context-3", Name = "Work", IsEnabled = true }
        ];
        service.Settings = settings;

        IReadOnlyList<PanelContext> snapshot = service.GetEnabledContextsSnapshot();

        Assert.Equal(["context-1", "context-3"], snapshot.Select(context => context.Id).ToArray());
        snapshot[0].Name = "Changed";
        Assert.Equal("Main", service.Settings.Contexts[0].Name);
    }

    [Fact]
    public void GetEnabledContextsSnapshot_LocalizesNonCustomizedDefaultNamesAtReadTime()
    {
        CultureInfo originalCulture = CultureInfo.CurrentCulture;
        CultureInfo originalUiCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("uk");
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("uk");

            var service = new AppSettingsService();
            var settings = service.Settings;
            settings.Contexts =
            [
                new() { Id = "context-1", Name = "Panel 1", IsNameCustomized = false, IsEnabled = true },
                new() { Id = "context-2", Name = "Work", IsNameCustomized = true, IsEnabled = true }
            ];
            service.Settings = settings;

            IReadOnlyList<PanelContext> snapshot = service.GetEnabledContextsSnapshot();

            Assert.Equal("Панель 1", snapshot[0].Name);
            Assert.Equal("Work", snapshot[1].Name);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
        }
    }

    [Fact]
    public void GetAllContextsSnapshot_LocalizesNonCustomizedDefaultNamesAtReadTime()
    {
        CultureInfo originalCulture = CultureInfo.CurrentCulture;
        CultureInfo originalUiCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de");
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("de");

            var service = new AppSettingsService();
            var settings = service.Settings;
            settings.Contexts =
            [
                new() { Id = "context-1", Name = "Panel 1", IsNameCustomized = false, IsEnabled = true },
                new() { Id = "context-2", Name = "Work", IsNameCustomized = true, IsEnabled = false }
            ];
            service.Settings = settings;

            IReadOnlyList<PanelContext> snapshot = service.GetAllContextsSnapshot();

            Assert.Equal("Leiste 1", snapshot[0].Name);
            Assert.Equal("Work", snapshot[1].Name);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
        }
    }

    [Fact]
    public void NormalizeAppState_LocalizesNonCustomizedDefaultContextNamesToCurrentCulture()
    {
        CultureInfo originalCulture = CultureInfo.CurrentCulture;
        CultureInfo originalUiCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ru");
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("ru");

            var service = new AppSettingsService();
            var settings = service.Settings;
            settings.Contexts =
            [
                new() { Id = "context-1", Name = "Panel 1", IsEnabled = true },
                new() { Id = "context-2", Name = "Work", IsNameCustomized = true, IsEnabled = true }
            ];
            service.Settings = settings;

            bool changed = service.NormalizeAppState();

            Assert.True(changed);
            Assert.Equal("Панель 1", service.Settings.Contexts[0].Name);
            Assert.False(service.Settings.Contexts[0].IsNameCustomized);
            Assert.Equal("Work", service.Settings.Contexts[1].Name);
            Assert.True(service.Settings.Contexts[1].IsNameCustomized);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
        }
    }

    [Fact]
    public void CloneElement_CreatesDeepCopyOfRotationProfiles()
    {
        var service = new AppSettingsService();
        var source = new CustomElement
        {
            Id = "source",
            RotationProfilePaths = ["Profile A"],
            ContextId = "context-2"
        };

        CustomElement clone = service.CloneElement(source);
        clone.RotationProfilePaths.Add("Profile B");
        clone.ContextId = "context-3";

        Assert.Equal(["Profile A"], source.RotationProfilePaths);
        Assert.Equal("context-2", source.ContextId);
    }
    [Fact]
    public void UpdateSettings_PreservesCurrentElementsWhenUpdatingStaleSettingsFields()
    {
        var service = new AppSettingsService();
        var settings = service.Settings;
        settings.Elements =
        [
            new CustomElement
            {
                Id = "button-1",
                Name = "Button 1",
                ContextId = "context-1"
            }
        ];
        service.Settings = settings;

        service.UpdateSettings(next =>
        {
            next.Elements.Clear();
            next.PanelSizePercent = 75;
        });

        Assert.Single(service.Elements);
        Assert.Equal("button-1", service.Elements[0].Id);
        Assert.Equal(75, service.Settings.PanelSizePercent);
    }

    [Fact]
    public async Task SaveAsync_WhenSettingsPathIsDirectory_ThrowsInsteadOfSwallowingFailure()
    {
        string root = Path.Combine(Path.GetTempPath(), "AiteBarTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string settingsPath = Path.Combine(root, "settings.json");
        Directory.CreateDirectory(settingsPath);
        string configPath = Path.Combine(root, "custom_buttons.json");

        try
        {
            var service = new AppSettingsService(configPath, settingsPath);

            await Assert.ThrowsAnyAsync<IOException>(() => service.SaveAsync());
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
