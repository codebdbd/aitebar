using Xunit;

namespace AiteBar.Tests;

public class PanelLifecycleHelperTests
{
    [Theory]
    [InlineData(DockEdge.Left, true)]
    [InlineData(DockEdge.Right, true)]
    [InlineData(DockEdge.Top, false)]
    [InlineData(DockEdge.Bottom, false)]
    public void IsHorizontalMotion_IdentifiesAxisCorrectly(DockEdge edge, bool expectedHorizontal)
    {
        bool result = PanelLifecycleHelper.IsHorizontalMotion(edge);
        Assert.Equal(expectedHorizontal, result);
    }

    [Fact]
    public void ShouldStartPointerLeaveTimer_WhenHoverActivatedAndIdle_ReturnsTrue()
    {
        bool result = PanelLifecycleHelper.ShouldStartPointerLeaveTimer(
            isShown: true,
            isAnimating: false,
            showSource: PanelShowSource.PointerHover,
            isPanelInteractionActive: false,
            isReordering: false);

        Assert.True(result);
    }

    [Theory]
    [InlineData(false, false, PanelShowSource.PointerHover, false, false, "Panel is not shown")]
    [InlineData(true, true, PanelShowSource.PointerHover, false, false, "Panel is animating")]
    [InlineData(true, false, PanelShowSource.Explicit, false, false, "Panel was explicitly opened via hotkey/tray")]
    [InlineData(true, false, PanelShowSource.PointerHover, true, false, "ContextMenu or dialog is open")]
    [InlineData(true, false, PanelShowSource.PointerHover, false, true, "Button is being dragged")]
    public void ShouldStartPointerLeaveTimer_WhenConditionsNotMet_ReturnsFalse(
        bool isShown,
        bool isAnimating,
        PanelShowSource showSource,
        bool isPanelInteractionActive,
        bool isReordering,
        string reason)
    {
        bool result = PanelLifecycleHelper.ShouldStartPointerLeaveTimer(
            isShown,
            isAnimating,
            showSource,
            isPanelInteractionActive,
            isReordering);

        Assert.False(result, reason);
    }

    [Fact]
    public void ShouldPerformPointerLeaveHide_WhenHoverActivatedAndCursorAway_ReturnsTrue()
    {
        bool result = PanelLifecycleHelper.ShouldPerformPointerLeaveHide(
            isShown: true,
            isAnimating: false,
            showSource: PanelShowSource.PointerHover,
            isPanelInteractionActive: false,
            isReordering: false,
            isCursorOverPanel: false);

        Assert.True(result);
    }

    [Fact]
    public void ShouldPerformPointerLeaveHide_WhenCursorStillOverPanel_ReturnsFalse()
    {
        bool result = PanelLifecycleHelper.ShouldPerformPointerLeaveHide(
            isShown: true,
            isAnimating: false,
            showSource: PanelShowSource.PointerHover,
            isPanelInteractionActive: false,
            isReordering: false,
            isCursorOverPanel: true);

        Assert.False(result);
    }

    [Theory]
    [InlineData(false, false, PanelShowSource.PointerHover, false, false, false, "Panel hidden")]
    [InlineData(true, true, PanelShowSource.PointerHover, false, false, false, "Panel animating")]
    [InlineData(true, false, PanelShowSource.Explicit, false, false, false, "Explicit open")]
    [InlineData(true, false, PanelShowSource.PointerHover, true, false, false, "Interaction active")]
    [InlineData(true, false, PanelShowSource.PointerHover, false, true, false, "Reordering active")]
    public void ShouldPerformPointerLeaveHide_WhenGuardsTriggered_ReturnsFalse(
        bool isShown,
        bool isAnimating,
        PanelShowSource showSource,
        bool isPanelInteractionActive,
        bool isReordering,
        bool isCursorOverPanel,
        string reason)
    {
        bool result = PanelLifecycleHelper.ShouldPerformPointerLeaveHide(
            isShown,
            isAnimating,
            showSource,
            isPanelInteractionActive,
            isReordering,
            isCursorOverPanel);

        Assert.False(result, reason);
    }
}
