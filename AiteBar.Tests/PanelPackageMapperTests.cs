using AiteBar;
using Xunit;

namespace AiteBar.Tests;

public sealed class PanelPackageMapperTests
{
    [Fact]
    public void BuildPackageImagePath_WithPngExtension_ReturnsCorrectPath()
    {
        var result = PanelPackageMapper.BuildPackageImagePath("C:\\path\\to\\image.png", 5);
        Assert.Equal("icons/005.png", result);
    }

    [Fact]
    public void BuildPackageImagePath_WithJpgExtension_ReturnsCorrectPath()
    {
        var result = PanelPackageMapper.BuildPackageImagePath("C:\\path\\to\\image.jpg", 10);
        Assert.Equal("icons/010.jpg", result);
    }

    [Fact]
    public void BuildPackageImagePath_WithoutExtension_AddsPng()
    {
        var result = PanelPackageMapper.BuildPackageImagePath("C:\\path\\to\\image", 1);
        Assert.Equal("icons/001.png", result);
    }

    [Fact]
    public void BuildPackageImagePath_WithUpperCaseExtension_ConvertsToLower()
    {
        var result = PanelPackageMapper.BuildPackageImagePath("C:\\path\\to\\image.PNG", 3);
        Assert.Equal("icons/003.png", result);
    }

    [Fact]
    public void BuildPackageImagePath_ZeroIndex_PadsToThreeDigits()
    {
        var result = PanelPackageMapper.BuildPackageImagePath("C:\\path\\to\\image.png", 0);
        Assert.Equal("icons/000.png", result);
    }

    [Fact]
    public void BuildPackageImagePath_LargeIndex_PadsToThreeDigits()
    {
        var result = PanelPackageMapper.BuildPackageImagePath("C:\\path\\to\\image.png", 999);
        Assert.Equal("icons/999.png", result);
    }

    [Fact]
    public void IsPackagedImagePathSafe_Null_ReturnsFalse()
    {
        Assert.False(PanelPackageMapper.IsPackagedImagePathSafe(null));
    }

    [Fact]
    public void IsPackagedImagePathSafe_Empty_ReturnsFalse()
    {
        Assert.False(PanelPackageMapper.IsPackagedImagePathSafe(""));
    }

    [Fact]
    public void IsPackagedImagePathSafe_Whitespace_ReturnsFalse()
    {
        Assert.False(PanelPackageMapper.IsPackagedImagePathSafe("   "));
    }

    [Fact]
    public void IsPackagedImagePathSafe_ValidPath_ReturnsTrue()
    {
        Assert.True(PanelPackageMapper.IsPackagedImagePathSafe("icons/001.png"));
    }

    [Fact]
    public void IsPackagedImagePathSafe_WithBackslash_ReturnsTrue()
    {
        Assert.True(PanelPackageMapper.IsPackagedImagePathSafe("icons\\001.png"));
    }

    [Fact]
    public void IsPackagedImagePathSafe_WithoutIconsPrefix_ReturnsFalse()
    {
        Assert.False(PanelPackageMapper.IsPackagedImagePathSafe("images/001.png"));
    }

    [Fact]
    public void IsPackagedImagePathSafe_WithPathTraversal_ReturnsFalse()
    {
        Assert.False(PanelPackageMapper.IsPackagedImagePathSafe("icons/../001.png"));
    }

    [Fact]
    public void IsPackagedImagePathSafe_WithBackslashTraversal_ReturnsFalse()
    {
        Assert.False(PanelPackageMapper.IsPackagedImagePathSafe("icons\\..\\001.png"));
    }

    [Fact]
    public void IsPackagedImagePathSafe_AbsolutePath_ReturnsFalse()
    {
        Assert.False(PanelPackageMapper.IsPackagedImagePathSafe("C:\\icons\\001.png"));
    }

    [Fact]
    public void IsPackagedImagePathSafe_RootedPath_ReturnsFalse()
    {
        Assert.False(PanelPackageMapper.IsPackagedImagePathSafe("/icons/001.png"));
    }

    [Fact]
    public void FromCustomElement_WithMinimalData_SetsDefaults()
    {
        var element = new CustomElement
        {
            Name = "Test Button",
            ActionType = nameof(ActionType.Web),
            ActionValue = "https://example.com"
        };

        var result = PanelPackageMapper.FromCustomElement(element, _ => null);

        Assert.Equal("Test Button", result.Name);
        Assert.Equal(nameof(ActionType.Web), result.ActionType);
        Assert.Equal("https://example.com", result.ActionValue);
        Assert.Equal("\uE710", result.Icon);
        Assert.Equal(FontHelper.FluentKey, result.IconFont);
        Assert.Equal("#E3E3E3", result.Color);
        Assert.Equal("None", result.Key);
    }

    [Fact]
    public void FromCustomElement_WithCustomIcon_SetsIcon()
    {
        var element = new CustomElement
        {
            Name = "Test",
            ActionType = nameof(ActionType.Web),
            ActionValue = "https://example.com",
            Icon = "\uE8B7",
            IconFont = FontHelper.MaterialKey
        };

        var result = PanelPackageMapper.FromCustomElement(element, _ => null);

        Assert.Equal("\uE8B7", result.Icon);
        Assert.Equal(FontHelper.MaterialKey, result.IconFont);
    }

    [Fact]
    public void FromCustomElement_WithCustomColor_SetsColor()
    {
        var element = new CustomElement
        {
            Name = "Test",
            ActionType = nameof(ActionType.Web),
            ActionValue = "https://example.com",
            Color = "#FF0000"
        };

        var result = PanelPackageMapper.FromCustomElement(element, _ => null);

        Assert.Equal("#FF0000", result.Color);
    }

    [Fact]
    public void FromCustomElement_WithHotkey_SetsHotkeyProperties()
    {
        var element = new CustomElement
        {
            Name = "Test",
            ActionType = nameof(ActionType.Hotkey),
            ActionValue = "Ctrl+Alt+Delete",
            Alt = true,
            Ctrl = true,
            Shift = false,
            Win = false,
            Key = "Delete"
        };

        var result = PanelPackageMapper.FromCustomElement(element, _ => null);

        Assert.True(result.Alt);
        Assert.True(result.Ctrl);
        Assert.False(result.Shift);
        Assert.False(result.Win);
        Assert.Equal("Delete", result.Key);
    }

    [Fact]
    public void FromCustomElement_WithBrowserSettings_SetsBrowserProperties()
    {
        var element = new CustomElement
        {
            Name = "Test",
            ActionType = nameof(ActionType.Web),
            ActionValue = "https://example.com",
            Browser = BrowserType.Firefox,
            ChromeProfile = "Work Profile",
            IsAppMode = true,
            IsIncognito = true
        };

        var result = PanelPackageMapper.FromCustomElement(element, _ => null);

        Assert.Equal(BrowserType.Firefox, result.Browser);
        Assert.Equal("Work Profile", result.ChromeProfile);
        Assert.True(result.IsAppMode);
        Assert.True(result.IsIncognito);
    }

    [Fact]
    public void ToImportedCustomElement_GeneratesNewId()
    {
        var source = new PanelPackageElement
        {
            Name = "Test Button",
            ActionType = nameof(ActionType.Web),
            ActionValue = "https://example.com"
        };

        var result = PanelPackageMapper.ToImportedCustomElement(source, "context-1", _ => "");

        Assert.NotNull(result.Id);
        Assert.NotEmpty(result.Id);
    }

    [Fact]
    public void ToImportedCustomElement_SetsTargetContextId()
    {
        var source = new PanelPackageElement
        {
            Name = "Test Button",
            ActionType = nameof(ActionType.Web),
            ActionValue = "https://example.com"
        };

        var result = PanelPackageMapper.ToImportedCustomElement(source, "context-123", _ => "");

        Assert.Equal("context-123", result.ContextId);
    }

    [Fact]
    public void ToImportedCustomElement_SetsLastUsedProfileToEmpty()
    {
        var source = new PanelPackageElement
        {
            Name = "Test Button",
            ActionType = nameof(ActionType.Web),
            ActionValue = "https://example.com"
        };

        var result = PanelPackageMapper.ToImportedCustomElement(source, "context-1", _ => "");

        Assert.Equal("", result.LastUsedProfile);
    }

    [Fact]
    public void ToImportedCustomElement_TrimsName()
    {
        var source = new PanelPackageElement
        {
            Name = "  Test Button  ",
            ActionType = nameof(ActionType.Web),
            ActionValue = "https://example.com"
        };

        var result = PanelPackageMapper.ToImportedCustomElement(source, "context-1", _ => "");

        Assert.Equal("Test Button", result.Name);
    }

    [Fact]
    public void ToImportedCustomElement_WithNullName_SetsEmpty()
    {
        var source = new PanelPackageElement
        {
            Name = null,
            ActionType = nameof(ActionType.Web),
            ActionValue = "https://example.com"
        };

        var result = PanelPackageMapper.ToImportedCustomElement(source, "context-1", _ => "");

        Assert.Equal("", result.Name);
    }
}
