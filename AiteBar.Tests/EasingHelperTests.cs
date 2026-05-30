using System.Windows.Media.Animation;
using AiteBar;
using Xunit;

namespace AiteBar.Tests;

public sealed class EasingHelperTests
{
    [Fact]
    public void DefaultEasing_ReturnsCubicEaseEaseOut()
    {
        Assert.IsType<CubicEase>(EasingHelper.DefaultEasing);
        Assert.Equal(EasingMode.EaseOut, ((CubicEase)EasingHelper.DefaultEasing).EasingMode);
    }

    [Fact]
    public void HideEasing_ReturnsCubicEaseEaseIn()
    {
        Assert.IsType<CubicEase>(EasingHelper.HideEasing);
        Assert.Equal(EasingMode.EaseIn, ((CubicEase)EasingHelper.HideEasing).EasingMode);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ForToggle_ReturnsCorrectEasing(bool hide)
    {
        var result = EasingHelper.ForToggle(hide);
        Assert.IsType<CubicEase>(result);
        Assert.Equal(hide ? EasingMode.EaseIn : EasingMode.EaseOut, ((CubicEase)result).EasingMode);
    }
}
