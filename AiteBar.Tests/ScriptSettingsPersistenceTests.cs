using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using AiteBar;
using Xunit;

namespace AiteBar.Tests;

[Collection("LocalizationStateTestCollection")]
public sealed class ScriptSettingsPersistenceTests
{
    [Fact]
    public void SettingsSnapshotAndNormalization_PreserveScriptOptions()
    {
        var service = new AppSettingsService();
        var settings = service.Settings;
        settings.Elements = [CreateScript()];
        service.Settings = settings;

        AssertOptions(Assert.Single(service.Settings.Elements));
        AssertOptions(Assert.Single(service.Elements));

        service.NormalizeAppState();

        AssertOptions(Assert.Single(service.Settings.Elements));
        AssertOptions(Assert.Single(service.Elements));
    }

    [Fact]
    public async Task SaveElementAsync_PreservesScriptOptionsOnDiskAndAfterReload()
    {
        string root = Path.Combine(Path.GetTempPath(), "AiteBarTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string settingsPath = Path.Combine(root, "settings.json");
        string configPath = Path.Combine(root, "custom_buttons.json");
        try
        {
            var service = new AppSettingsService(configPath, settingsPath);
            await service.SaveElementAsync(CreateScript());

            AssertOptions(Assert.Single(service.Elements));
            var saved = JsonSerializer.Deserialize<AppSettings>(await File.ReadAllTextAsync(settingsPath));
            AssertOptions(Assert.Single(saved!.Elements));

            var reloaded = new AppSettingsService(configPath, settingsPath);
            await reloaded.LoadAsync();
            AssertOptions(Assert.Single(reloaded.Elements));
            AssertOptions(Assert.Single(reloaded.Settings.Elements));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static CustomElement CreateScript() => new()
    {
        Id = "script",
        Name = "Script",
        ActionType = nameof(ActionType.ScriptFile),
        ActionValue = @"C:\Scripts\sample.bat",
        ContextId = "context-0",
        ScriptArguments = "--name \"two words\"",
        SkipScriptConfirmation = true,
        HideScriptWindow = true,
        KeepScriptWindowOpen = true,
        RunAsAdmin = true
    };

    private static void AssertOptions(CustomElement element)
    {
        Assert.Equal("--name \"two words\"", element.ScriptArguments);
        Assert.True(element.SkipScriptConfirmation);
        Assert.True(element.HideScriptWindow);
        Assert.True(element.KeepScriptWindowOpen);
        Assert.True(element.RunAsAdmin);
    }
}
