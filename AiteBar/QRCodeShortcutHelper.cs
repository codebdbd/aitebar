namespace AiteBar;

internal static class QRCodeShortcutHelper
{
    public static bool ShouldCopyImage(Key key, ModifierKeys modifiers, bool isTextEditingControl)
    {
        return key == Key.C
            && modifiers == ModifierKeys.Control
            && !isTextEditingControl;
    }
}
