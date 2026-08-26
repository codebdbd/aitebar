namespace AiteBar.Tests;

public sealed class PanelInteractionHelperTests
{
    [Fact]
    public void IsBackgroundDoubleClick_AllowsOnlyDoubleClickOutsideInteractivePanelControls()
    {
        Assert.True(PanelInteractionHelper.IsBackgroundDoubleClick(clickCount: 2, isOverButton: false, isOverDragHandle: false));
        Assert.False(PanelInteractionHelper.IsBackgroundDoubleClick(clickCount: 1, isOverButton: false, isOverDragHandle: false));
        Assert.False(PanelInteractionHelper.IsBackgroundDoubleClick(clickCount: 2, isOverButton: true, isOverDragHandle: false));
        Assert.False(PanelInteractionHelper.IsBackgroundDoubleClick(clickCount: 2, isOverButton: false, isOverDragHandle: true));
    }
}
