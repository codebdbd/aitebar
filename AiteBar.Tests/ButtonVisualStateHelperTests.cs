using AiteBar;

namespace AiteBar.Tests;

public sealed class ButtonVisualStateHelperTests
{
    [Fact]
    public void CreateSnapshot_ChangesWhenExistingButtonIconChanges()
    {
        var original = CreateElement();
        CustomElement changed = Clone(original);
        changed.Icon = "\uF606";

        ButtonVisualState before = Assert.Single(ButtonVisualStateHelper.CreateSnapshot([original]));
        ButtonVisualState after = Assert.Single(ButtonVisualStateHelper.CreateSnapshot([changed]));
        Assert.NotEqual(before, after);
    }

    [Fact]
    public void CreateSnapshot_ChangesWhenExistingButtonImageIsRemoved()
    {
        var original = CreateElement();
        original.ImagePath = "favicon.png";
        CustomElement changed = Clone(original);
        changed.ImagePath = "";

        ButtonVisualState before = Assert.Single(ButtonVisualStateHelper.CreateSnapshot([original]));
        ButtonVisualState after = Assert.Single(ButtonVisualStateHelper.CreateSnapshot([changed]));
        Assert.NotEqual(before, after);
    }

    private static CustomElement CreateElement() => new()
    {
        Id = "button-1",
        Icon = "\uF45B",
        IconFont = FontHelper.FluentKey,
        ImagePath = ""
    };

    private static CustomElement Clone(CustomElement source) => new()
    {
        Id = source.Id,
        Icon = source.Icon,
        IconFont = source.IconFont,
        ImagePath = source.ImagePath
    };
}
