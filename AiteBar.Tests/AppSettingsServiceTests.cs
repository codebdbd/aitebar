using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using AiteBar;
using Xunit;

namespace AiteBar.Tests;

[Collection("LocalizationStateTestCollection")]
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
    public void GetContextDisplayName_NonCustomizedDefaultContext_ReturnsAppliedCultureDisplayName()
    {
        string originalCulture = LocalizationService.ResolvedCulture.Name;
        try
        {
            LocalizationService.ApplyCulture("de");

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
            LocalizationService.ApplyCulture(originalCulture);
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
        string originalCulture = LocalizationService.ResolvedCulture.Name;
        try
        {
            LocalizationService.ApplyCulture("uk");

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
            LocalizationService.ApplyCulture(originalCulture);
        }
    }

    [Fact]
    public void GetAllContextsSnapshot_LocalizesNonCustomizedDefaultNamesAtReadTime()
    {
        string originalCulture = LocalizationService.ResolvedCulture.Name;
        try
        {
            LocalizationService.ApplyCulture("de");

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
            LocalizationService.ApplyCulture(originalCulture);
        }
    }

    [Fact]
    public void NormalizeAppState_LocalizesNonCustomizedDefaultContextNamesToAppliedCulture()
    {
        string originalCulture = LocalizationService.ResolvedCulture.Name;
        try
        {
            LocalizationService.ApplyCulture("ru");

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
            LocalizationService.ApplyCulture(originalCulture);
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
    public void UpdateSettings_DoesNotPublishElementMutationsOrRetainedCallbackState()
    {
        var service = new AppSettingsService();
        var settings = service.Settings;
        settings.Elements =
        [
            new CustomElement
            {
                Id = "button-1",
                Name = "Original",
                ContextId = "context-1"
            }
        ];
        service.Settings = settings;

        AppSettings? retained = null;
        service.UpdateSettings(next =>
        {
            retained = next;
            next.PanelSizePercent = 75;
            next.Elements[0].Name = "Callback mutation";
        });

        retained!.PanelSizePercent = 50;
        retained.Elements[0].Name = "Late mutation";

        Assert.Equal(75, service.Settings.PanelSizePercent);
        Assert.Equal("Original", Assert.Single(service.Elements).Name);
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

    [Fact]
    public void FileSorterWindow_LastFileSortOperationHelpers_PersistUndoStateInSettingsService()
    {
        var service = new AppSettingsService();
        var undoState = new FileSortUndoState
        {
            RootPath = @"C:\Temp\SortRoot",
            Entries =
            [
                new FileSortOperationEntry
                {
                    SourcePath = @"C:\Temp\SortRoot\photo.jpg",
                    DestinationPath = @"C:\Temp\SortRoot\Images\photo.jpg"
                }
            ]
        };

        FileSorterWindow.SetLastFileSortOperation(service, undoState);

        FileSortUndoState? persisted = FileSorterWindow.GetLastFileSortOperation(service);
        Assert.NotNull(persisted);
        Assert.Equal(undoState.RootPath, persisted.RootPath);
        Assert.Single(persisted.Entries);
        Assert.Equal(undoState.Entries[0].DestinationPath, persisted.Entries[0].DestinationPath);

        FileSorterWindow.SetLastFileSortOperation(service, null);

        Assert.Null(FileSorterWindow.GetLastFileSortOperation(service));
    }

    [Fact]
    public void CloneAppSettings_CopiesAllProperties()
    {
        var original = new AppSettings
        {
            GlobalHotkeyCtrl = true,
            GlobalHotkeyAlt = false,
            GlobalHotkeyShift = true,
            GlobalHotkeyWin = true,
            GlobalHotkeyKey = "F5",

            ShowPresetSearch = false,
            ShowPresetScreenshot = false,
            ShowPresetVideo = false,
            ShowPresetCalc = false,
            ShowPresetExplorer = false,
            ShowPresetDownloads = false,
            ShowPresetFileSorter = false,
            ShowPresetIconConverter = false,
            ShowPresetColorPicker = true,
            ShowPresetQuickNote = true,
            ShowPresetQRCodeGenerator = true,
            ShowPresetClipboardManager = true,
            ShowPresetTimerStopwatch = false,
            ShowPresetShowDesktop = false,
            ShowPresetAppsFolder = false,
            ShowPresetCopilot = false,
            ShowPresetTextProcessing = false,

            TextProcessingLeft = 100.0,
            TextProcessingTop = 200.0,
            TextProcessingWidth = 1280.0,
            TextProcessingHeight = 840.0,
            TextProcessingWindowState = "Maximized",
            TextProcessingLastMode = 1,
            TextProcessingSelectedConnectionId = "connection-1",
            TextProcessingSelectedModelId = "model-1",
            TextProcessingSelectedProviderId = "provider-1",
            TextProcessingIsAutoModel = false,

            ClipboardManagerPersistHistory = false,
            QuickNoteThemeId = "light",
            QuickNotePinned = true,
            QuickNoteLeft = 100.5,
            QuickNoteTop = 200.5,
            QuickNoteWidth = 400.0,
            QuickNoteHeight = 300.0,
            TimerSoundEnabled = false,
            TimerIsStopwatchMode = true,
            TimerDuration = TimeSpan.FromHours(1),

            Edge = DockEdge.Right,
            MonitorIndex = 2,
            ActivationZoneSizePercent = 50,
            PanelSizePercent = 65,
            ActivationDelayMs = 300,
            UiCulture = "de",
            ActiveContextId = "context-3",
            CheckForUpdatesEnabled = false,
            ShowTaskbarPositionIndicator = false,
            TaskbarIndicatorPositionX = 0.75,
            TaskbarIndicatorPositionY = 0.25,

            NextContextHotkey = new HotkeyBinding { Ctrl = true, Alt = false, Shift = true, Win = false, Key = "A" },
            PreviousContextHotkey = new HotkeyBinding { Ctrl = false, Alt = true, Shift = false, Win = true, Key = "B" },
            AddButtonHotkey = new HotkeyBinding { Ctrl = true, Alt = true, Shift = false, Win = false, Key = "C" },
            FileSorterHotkey = new HotkeyBinding { Ctrl = false, Alt = false, Shift = true, Win = true, Key = "D" },
            QuickNoteHotkey = new HotkeyBinding { Ctrl = true, Alt = false, Shift = false, Win = true, Key = "E" },
            ColorPickerHotkey = new HotkeyBinding { Ctrl = false, Alt = true, Shift = true, Win = false, Key = "F" },
            TimerStopwatchHotkey = new HotkeyBinding { Ctrl = true, Alt = true, Shift = true, Win = false, Key = "G" },
            QRCodeGeneratorHotkey = new HotkeyBinding { Ctrl = false, Alt = false, Shift = false, Win = true, Key = "H" },
            IconConverterHotkey = new HotkeyBinding { Ctrl = true, Alt = false, Shift = true, Win = false, Key = "I" },
            ClipboardManagerHotkey = new HotkeyBinding { Ctrl = true, Alt = true, Shift = false, Win = false, Key = "V" },
            TextProcessingHotkey = new HotkeyBinding { Ctrl = true, Alt = false, Shift = false, Win = true, Key = "T" },
            ZenEditorHotkey = new HotkeyBinding { Ctrl = false, Alt = true, Shift = true, Win = false, Key = "Z" },

            Contexts =
            [
                new PanelContext { Id = "ctx-1", Name = "Custom", IsNameCustomized = true, IconGlyph = "\uE111", IsEnabled = true, Color = "#FF0000" },
                new PanelContext { Id = "ctx-2", Name = "Panel 2", IsNameCustomized = false, IconGlyph = "\uE222", IsEnabled = false, Color = "#00FF00" }
            ],

            Elements =
            [
                new CustomElement
                {
                    Id = "el-1",
                    Name = "Button 1",
                    Icon = "\uF001",
                    IconFont = "test-font",
                    Color = "#123456",
                    ActionType = nameof(ActionType.Web),
                    ActionValue = "https://example.com",
                    Browser = BrowserType.Firefox,
                    ChromeProfile = "Profile 1",
                    RotationProfilePaths = ["path/a", "path/b"],
                    IsAppMode = true,
                    IsIncognito = true,
                    UseRotation = true,
                    OpenFullscreen = true,
                    IsTopmost = true,
                    LastUsedProfile = "Profile 1",
                    Alt = true,
                    Ctrl = true,
                    Shift = true,
                    Win = true,
                    Key = "X",
                    ImagePath = "/tmp/icon.png",
                    ContextId = "ctx-2"
                }
            ],

            UtilityButtonOrder = ["Search", "Calculator", "FileSorter"],

            LastFileSortOperation = new FileSortUndoState
            {
                RootPath = @"C:\Downloads",
                Entries = [new FileSortOperationEntry { SourcePath = "a.txt", DestinationPath = "b.txt" }]
            },

            Sentry = new SentrySettings
            {
                Dsn = "https://example@sentry.io/123",
                IsEnabled = true,
                Environment = "production",
                TracesSampleRate = 0.5,
                SendDefaultPii = true
            }
        };

        AppSettings clone = InvokeCloneAppSettings(original);

        // Scalar properties
        Assert.True(clone.GlobalHotkeyCtrl);
        Assert.False(clone.GlobalHotkeyAlt);
        Assert.True(clone.GlobalHotkeyShift);
        Assert.True(clone.GlobalHotkeyWin);
        Assert.Equal("F5", clone.GlobalHotkeyKey);

        Assert.False(clone.ShowPresetSearch);
        Assert.False(clone.ShowPresetScreenshot);
        Assert.False(clone.ShowPresetVideo);
        Assert.False(clone.ShowPresetCalc);
        Assert.False(clone.ShowPresetExplorer);
        Assert.False(clone.ShowPresetDownloads);
        Assert.False(clone.ShowPresetFileSorter);
        Assert.False(clone.ShowPresetIconConverter);
        Assert.True(clone.ShowPresetColorPicker);
        Assert.True(clone.ShowPresetQuickNote);
        Assert.True(clone.ShowPresetQRCodeGenerator);
        Assert.True(clone.ShowPresetClipboardManager);
        Assert.False(clone.ShowPresetTimerStopwatch);
        Assert.False(clone.ShowPresetShowDesktop);
        Assert.False(clone.ShowPresetAppsFolder);
        Assert.False(clone.ShowPresetCopilot);
        Assert.False(clone.ShowPresetTextProcessing);

        Assert.Equal(100.0, clone.TextProcessingLeft);
        Assert.Equal(200.0, clone.TextProcessingTop);
        Assert.Equal(1280.0, clone.TextProcessingWidth);
        Assert.Equal(840.0, clone.TextProcessingHeight);
        Assert.Equal("Maximized", clone.TextProcessingWindowState);
        Assert.Equal(1, clone.TextProcessingLastMode);
        Assert.Equal("connection-1", clone.TextProcessingSelectedConnectionId);
        Assert.Equal("model-1", clone.TextProcessingSelectedModelId);
        Assert.Equal("provider-1", clone.TextProcessingSelectedProviderId);
        Assert.False(clone.TextProcessingIsAutoModel);

        Assert.False(clone.ClipboardManagerPersistHistory);
        Assert.Equal("light", clone.QuickNoteThemeId);
        Assert.True(clone.QuickNotePinned);
        Assert.Equal(100.5, clone.QuickNoteLeft);
        Assert.Equal(200.5, clone.QuickNoteTop);
        Assert.Equal(400.0, clone.QuickNoteWidth);
        Assert.Equal(300.0, clone.QuickNoteHeight);
        Assert.False(clone.TimerSoundEnabled);
        Assert.True(clone.TimerIsStopwatchMode);
        Assert.Equal(TimeSpan.FromHours(1), clone.TimerDuration);

        Assert.Equal(DockEdge.Right, clone.Edge);
        Assert.Equal(2, clone.MonitorIndex);
        Assert.Equal(50, clone.ActivationZoneSizePercent);
        Assert.Equal(65, clone.PanelSizePercent);
        Assert.Equal(300, clone.ActivationDelayMs);
        Assert.Equal("de", clone.UiCulture);
        Assert.Equal("context-3", clone.ActiveContextId);
        Assert.False(clone.CheckForUpdatesEnabled);
        Assert.False(clone.ShowTaskbarPositionIndicator);
        Assert.Equal(0.75, clone.TaskbarIndicatorPositionX);
        Assert.Equal(0.25, clone.TaskbarIndicatorPositionY);

        // HotkeyBinding deep copies
        AssertCloneHotkeyBinding(original.NextContextHotkey, clone.NextContextHotkey);
        AssertCloneHotkeyBinding(original.PreviousContextHotkey, clone.PreviousContextHotkey);
        AssertCloneHotkeyBinding(original.AddButtonHotkey, clone.AddButtonHotkey);
        AssertCloneHotkeyBinding(original.FileSorterHotkey, clone.FileSorterHotkey);
        AssertCloneHotkeyBinding(original.QuickNoteHotkey, clone.QuickNoteHotkey);
        AssertCloneHotkeyBinding(original.ColorPickerHotkey, clone.ColorPickerHotkey);
        AssertCloneHotkeyBinding(original.TimerStopwatchHotkey, clone.TimerStopwatchHotkey);
        AssertCloneHotkeyBinding(original.QRCodeGeneratorHotkey, clone.QRCodeGeneratorHotkey);
        AssertCloneHotkeyBinding(original.IconConverterHotkey, clone.IconConverterHotkey);
        AssertCloneHotkeyBinding(original.ClipboardManagerHotkey, clone.ClipboardManagerHotkey);
        AssertCloneHotkeyBinding(original.TextProcessingHotkey, clone.TextProcessingHotkey);
        AssertCloneHotkeyBinding(original.ZenEditorHotkey, clone.ZenEditorHotkey);

        // Contexts deep copy
        Assert.Equal(2, clone.Contexts.Count);
        Assert.Equal("ctx-1", clone.Contexts[0].Id);
        Assert.Equal("Custom", clone.Contexts[0].Name);
        Assert.True(clone.Contexts[0].IsNameCustomized);
        Assert.Equal("\uE111", clone.Contexts[0].IconGlyph);
        Assert.True(clone.Contexts[0].IsEnabled);
        Assert.Equal("#FF0000", clone.Contexts[0].Color);

        Assert.Equal("ctx-2", clone.Contexts[1].Id);
        Assert.False(clone.Contexts[1].IsEnabled);
        Assert.Equal("#00FF00", clone.Contexts[1].Color);

        // Verify context list is a new instance
        Assert.NotSame(original.Contexts, clone.Contexts);

        // Elements deep copy
        Assert.Single(clone.Elements);
        var el = clone.Elements[0];
        Assert.Equal("el-1", el.Id);
        Assert.Equal("Button 1", el.Name);
        Assert.Equal("\uF001", el.Icon);
        Assert.Equal("test-font", el.IconFont);
        Assert.Equal("#123456", el.Color);
        Assert.Equal(nameof(ActionType.Web), el.ActionType);
        Assert.Equal("https://example.com", el.ActionValue);
        Assert.Equal(BrowserType.Firefox, el.Browser);
        Assert.Equal("Profile 1", el.ChromeProfile);
        Assert.Equal(["path/a", "path/b"], el.RotationProfilePaths);
        Assert.True(el.IsAppMode);
        Assert.True(el.IsIncognito);
        Assert.True(el.UseRotation);
        Assert.True(el.OpenFullscreen);
        Assert.True(el.IsTopmost);
        Assert.Equal("Profile 1", el.LastUsedProfile);
        Assert.True(el.Alt);
        Assert.True(el.Ctrl);
        Assert.True(el.Shift);
        Assert.True(el.Win);
        Assert.Equal("X", el.Key);
        Assert.Equal("/tmp/icon.png", el.ImagePath);
        Assert.Equal("ctx-2", el.ContextId);

        // Verify element list and rotation paths are new instances
        Assert.NotSame(original.Elements, clone.Elements);
        Assert.NotSame(original.Elements[0].RotationProfilePaths, clone.Elements[0].RotationProfilePaths);

        // UtilityButtonOrder deep copy
        Assert.Equal(["Search", "Calculator", "FileSorter"], clone.UtilityButtonOrder);
        Assert.NotSame(original.UtilityButtonOrder, clone.UtilityButtonOrder);

        // LastFileSortOperation deep copy
        Assert.NotNull(clone.LastFileSortOperation);
        Assert.Equal(@"C:\Downloads", clone.LastFileSortOperation.RootPath);
        Assert.Single(clone.LastFileSortOperation.Entries);
        Assert.Equal("a.txt", clone.LastFileSortOperation.Entries[0].SourcePath);
        Assert.Equal("b.txt", clone.LastFileSortOperation.Entries[0].DestinationPath);
        Assert.NotSame(original.LastFileSortOperation, clone.LastFileSortOperation);
        Assert.NotSame(original.LastFileSortOperation.Entries, clone.LastFileSortOperation.Entries);

        // Sentry deep copy
        Assert.NotNull(clone.Sentry);
        Assert.Equal("https://example@sentry.io/123", clone.Sentry.Dsn);
        Assert.True(clone.Sentry.IsEnabled);
        Assert.Equal("production", clone.Sentry.Environment);
        Assert.Equal(0.5, clone.Sentry.TracesSampleRate);
        Assert.True(clone.Sentry.SendDefaultPii);
        Assert.NotSame(original.Sentry, clone.Sentry);
    }

    [Fact]
    public void CloneAppSettings_MutatingCloneDoesNotAffectOriginal()
    {
        var original = new AppSettings
        {
            GlobalHotkeyKey = "D4",
            Edge = DockEdge.Left,
            NextContextHotkey = new HotkeyBinding { Ctrl = true, Key = "1" },
            Contexts = [new PanelContext { Id = "ctx-1", Name = "Original" }],
            Elements = [new CustomElement { Id = "el-1", Name = "Original", RotationProfilePaths = ["a"] }],
            Sentry = new SentrySettings { Dsn = "dsn" }
        };

        AppSettings clone = InvokeCloneAppSettings(original);

        // Mutate clone
        clone.GlobalHotkeyKey = "F12";
        clone.Edge = DockEdge.Bottom;
        clone.NextContextHotkey.Key = "9";
        clone.NextContextHotkey.Ctrl = false;
        clone.Contexts[0].Name = "Changed";
        clone.Elements[0].Name = "Changed";
        clone.Elements[0].RotationProfilePaths.Add("b");
        clone.Sentry!.Dsn = "changed";

        // Original must be unaffected
        Assert.Equal("D4", original.GlobalHotkeyKey);
        Assert.Equal(DockEdge.Left, original.Edge);
        Assert.Equal("1", original.NextContextHotkey.Key);
        Assert.True(original.NextContextHotkey.Ctrl);
        Assert.Equal("Original", original.Contexts[0].Name);
        Assert.Equal("Original", original.Elements[0].Name);
        Assert.Equal(["a"], original.Elements[0].RotationProfilePaths);
        Assert.Equal("dsn", original.Sentry.Dsn);
    }

    private static void AssertCloneHotkeyBinding(HotkeyBinding original, HotkeyBinding clone)
    {
        Assert.NotSame(original, clone);
        Assert.Equal(original.Ctrl, clone.Ctrl);
        Assert.Equal(original.Alt, clone.Alt);
        Assert.Equal(original.Shift, clone.Shift);
        Assert.Equal(original.Win, clone.Win);
        Assert.Equal(original.Key, clone.Key);
    }

    private static AppSettings InvokeCloneAppSettings(AppSettings settings)
    {
        MethodInfo method = typeof(AppSettingsService).GetMethod(
            "CloneAppSettings",
            BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("CloneAppSettings method not found");

        return (AppSettings)method.Invoke(null, [settings])!;
    }
}
