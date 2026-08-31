using AiteBar;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace AiteBar.Tests;

public sealed class IconCompatibilityTests
{
    [Fact]
    public async Task SettingsSaveReload_PreservesEverySupportedIconTuple()
    {
        string root = Path.Combine(Path.GetTempPath(), "AiteBarTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string settingsPath = Path.Combine(root, "settings.json");
        string configPath = Path.Combine(root, "custom_buttons.json");
        CustomElement[] expected =
        [
            CreateElement("fluent", "\uF45B", FontHelper.FluentKey, ""),
            CreateElement("brand", "\uF09B", FontHelper.BrandsKey, ""),
            CreateElement("material", "\uE8B7", FontHelper.MaterialKey, ""),
            CreateElement("supplementary", char.ConvertFromUtf32(0xF0AC5), FontHelper.FluentKey, ""),
            CreateElement("image", "", FontHelper.FluentKey, @"C:\icons\custom.png")
        ];

        try
        {
            var writer = new AppSettingsService(configPath, settingsPath);
            AppSettings settings = writer.Settings;
            settings.Elements = [.. expected];
            writer.Settings = settings;
            await writer.SaveAsync();

            var reader = new AppSettingsService(configPath, settingsPath);
            await reader.LoadAsync();
            Dictionary<string, CustomElement> actual = reader.Elements.ToDictionary(element => element.Id);

            foreach (CustomElement source in expected)
            {
                CustomElement restored = actual[source.Id];
                Assert.Equal(source.Icon, restored.Icon);
                Assert.Equal(source.IconFont, restored.IconFont);
                Assert.Equal(source.ImagePath, restored.ImagePath);
            }
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Theory]
    [InlineData("\uF45B", FontHelper.FluentKey)]
    [InlineData("\uF09B", FontHelper.BrandsKey)]
    [InlineData("\uE8B7", FontHelper.MaterialKey)]
    public void PanelPackageRoundTrip_PreservesFontGlyphTuple(string icon, string iconFont)
    {
        CustomElement source = CreateElement("source", icon, iconFont, "");

        PanelPackageElement package = PanelPackageMapper.FromCustomElement(source, _ => null);
        CustomElement restored = PanelPackageMapper.ToImportedCustomElement(
            package,
            "context-1",
            _ => "");

        Assert.Equal(source.Icon, restored.Icon);
        Assert.Equal(source.IconFont, restored.IconFont);
        Assert.Equal(source.ImagePath, restored.ImagePath);
    }

    [Fact]
    public void PanelPackageRoundTrip_PreservesSupplementaryGlyph()
    {
        string icon = char.ConvertFromUtf32(0xF0AC5);
        CustomElement source = CreateElement("supplementary", icon, FontHelper.FluentKey, "");

        PanelPackageElement package = PanelPackageMapper.FromCustomElement(source, _ => null);
        CustomElement restored = PanelPackageMapper.ToImportedCustomElement(
            package,
            "context-1",
            _ => "");

        Assert.Equal(2, restored.Icon.Length);
        Assert.Equal(icon, restored.Icon);
        Assert.Equal(FontHelper.FluentKey, restored.IconFont);
    }

    [Fact]
    public void PanelPackageRoundTrip_PreservesImageReferenceAndLegacyFallbackFields()
    {
        CustomElement source = CreateElement(
            "image",
            "\uF45B",
            FontHelper.FluentKey,
            @"C:\icons\custom.png");

        PanelPackageElement package = PanelPackageMapper.FromCustomElement(
            source,
            _ => "icons/001.png");
        CustomElement restored = PanelPackageMapper.ToImportedCustomElement(
            package,
            "context-1",
            image => image?.PackagePath == "icons/001.png" ? @"D:\imported\custom.png" : "");

        Assert.Equal(source.Icon, restored.Icon);
        Assert.Equal(source.IconFont, restored.IconFont);
        Assert.Equal(@"D:\imported\custom.png", restored.ImagePath);
    }

    private static CustomElement CreateElement(
        string id,
        string icon,
        string iconFont,
        string imagePath) => new()
        {
            Id = id,
            Name = id,
            ContextId = "context-0",
            ActionType = nameof(ActionType.Web),
            ActionValue = "https://example.com",
            Icon = icon,
            IconFont = iconFont,
            ImagePath = imagePath
        };
}
