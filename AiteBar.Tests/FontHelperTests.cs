using AiteBar;
using Xunit;

namespace AiteBar.Tests;

public sealed class FontHelperTests
{
    [Fact]
    public void MaterialKey_IsExpectedValue()
    {
        Assert.Equal("Material Icons", FontHelper.MaterialKey);
    }

    [Fact]
    public void FluentKey_IsExpectedValue()
    {
        Assert.Equal("Fluent System Icons", FontHelper.FluentKey);
    }

    [Fact]
    public void BrandsKey_IsExpectedValue()
    {
        Assert.Equal("Font Awesome Brands", FontHelper.BrandsKey);
    }

    [Fact]
    public void MaterialCodepointsResource_IsExpectedValue()
    {
        Assert.Equal("pack://application:,,,/Resources/MaterialIcons.codepoints", FontHelper.MaterialCodepointsResource);
    }

    [Fact]
    public void FluentCodepointsResource_IsExpectedValue()
    {
        Assert.Equal("pack://application:,,,/Resources/FluentSystemIcons.json", FontHelper.FluentCodepointsResource);
    }
}
