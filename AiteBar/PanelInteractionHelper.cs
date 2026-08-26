namespace AiteBar;

internal static class PanelInteractionHelper
{
    internal static bool IsBackgroundDoubleClick(int clickCount, bool isOverButton, bool isOverDragHandle) =>
        clickCount == 2 && !isOverButton && !isOverDragHandle;
}
