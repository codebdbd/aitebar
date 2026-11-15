using System.Windows.Input;
using AiteBar;

namespace AiteBar.Tests.TelegramPostEditor;

public sealed class TelegramPostEditorIntegrationTests
{
    [Fact]
    public void UtilityRegistry_DiscoversTelegramPostEditorUtility()
    {
        UtilityRegistry.Clear();
        try
        {
            UtilityRegistry.RegisterAllFromAssembly(typeof(TelegramPostEditorUtility).Assembly);
            IUtility? utility = UtilityRegistry.GetById("TelegramPostEditor");

            Assert.NotNull(utility);
            Assert.IsType<TelegramPostEditorUtility>(utility);
            Assert.Equal("Tool_TelegramPostEditor", utility!.DisplayNameKey);
            Assert.Equal("\uF69A", utility.IconGlyph);
        }
        finally
        {
            UtilityRegistry.Clear();
        }
    }

    [Fact]
    public void UtilityButtonCatalog_ContainsTelegramPostEditor_AndTogglesVisibility()
    {
        Assert.True(UtilityButtonCatalog.TryGet("TelegramPostEditor", out UtilityButtonDefinition? definition));
        Assert.NotNull(definition);

        var settings = new AppSettings();
        Assert.True(definition!.IsVisible(settings));

        definition.SetVisible(settings, false);
        Assert.False(settings.ShowPresetTelegramPostEditor);
        Assert.False(definition.IsVisible(settings));

        definition.SetVisible(settings, true);
        Assert.True(settings.ShowPresetTelegramPostEditor);
    }

    [Fact]
    public void HotkeyService_IncludesTelegramPostEditorBinding()
    {
        var settings = new AppSettings
        {
            TelegramPostEditorHotkey = new HotkeyBinding
            {
                Ctrl = true,
                Shift = true,
                Key = nameof(Key.T)
            }
        };

        var service = new HotkeyService();
        IReadOnlyList<HotkeyDefinition> definitions = service.CreateDefinitions(settings, key => key);

        HotkeyDefinition? definition = definitions.FirstOrDefault(d => d.Command == HotkeyCommand.TelegramPostEditor);
        Assert.NotNull(definition);
        Assert.Equal(HotkeyService.TelegramPostEditorId, definition!.Id);
        Assert.Equal("Tool_TelegramPostEditor", definition.DisplayName);
        Assert.True(definition.Binding.Ctrl);
        Assert.True(definition.Binding.Shift);
        Assert.Equal(nameof(Key.T), definition.Binding.Key);
    }

    [Fact]
    public void Localization_ProvidesTelegramPostEditorStrings()
    {
        Assert.False(string.IsNullOrWhiteSpace(LocalizationService.Get("Tool_TelegramPostEditor")));
        Assert.False(string.IsNullOrWhiteSpace(LocalizationService.Get("Main_TelegramPostEditorTooltip")));
        Assert.False(string.IsNullOrWhiteSpace(LocalizationService.Get("TelegramPostEditor_Title")));
    }
}
