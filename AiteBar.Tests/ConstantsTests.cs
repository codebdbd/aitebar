using AiteBar;
using Xunit;

namespace AiteBar.Tests;

public sealed class ConstantsTests
{
    [Fact]
    public void AnimationFadeMs_IsPositive()
    {
        Assert.True(Constants.AnimationFadeMs > 0);
    }

    [Fact]
    public void AnimationSlideMs_IsPositive()
    {
        Assert.True(Constants.AnimationSlideMs > 0);
    }

    [Fact]
    public void PanelShowAnimationMs_IsPositive()
    {
        Assert.True(Constants.PanelShowAnimationMs > 0);
    }

    [Fact]
    public void PanelHideAnimationMs_IsPositive()
    {
        Assert.True(Constants.PanelHideAnimationMs > 0);
    }

    [Fact]
    public void QuickNoteSlideMs_IsPositive()
    {
        Assert.True(Constants.QuickNoteSlideMs > 0);
    }

    [Fact]
    public void PanelShowAnimationMs_IsGreaterThanOrEqualToHideAnimationMs()
    {
        Assert.True(Constants.PanelShowAnimationMs >= Constants.PanelHideAnimationMs);
    }

    [Fact]
    public void AllAnimationConstants_AreInReasonableRange()
    {
        Assert.InRange(Constants.AnimationFadeMs, 50, 500);
        Assert.InRange(Constants.AnimationSlideMs, 50, 500);
        Assert.InRange(Constants.PanelShowAnimationMs, 100, 500);
        Assert.InRange(Constants.PanelHideAnimationMs, 100, 500);
        Assert.InRange(Constants.QuickNoteSlideMs, 50, 500);
    }
}
