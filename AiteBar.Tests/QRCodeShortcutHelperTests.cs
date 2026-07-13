using System.Windows.Input;
using AiteBar;

namespace AiteBar.Tests;

public sealed class QRCodeShortcutHelperTests
{
    [Fact]
    public void ShouldCopyImage_CtrlCOutsideTextEditor_ReturnsTrue()
    {
        Assert.True(QRCodeShortcutHelper.ShouldCopyImage(Key.C, ModifierKeys.Control, isTextEditingControl: false));
    }

    [Fact]
    public void ShouldCopyImage_CtrlCInsideTextEditor_ReturnsFalse()
    {
        Assert.False(QRCodeShortcutHelper.ShouldCopyImage(Key.C, ModifierKeys.Control, isTextEditingControl: true));
    }

    [Theory]
    [InlineData(Key.C, ModifierKeys.Control | ModifierKeys.Shift)]
    [InlineData(Key.V, ModifierKeys.Control)]
    [InlineData(Key.C, ModifierKeys.None)]
    public void ShouldCopyImage_OtherShortcut_ReturnsFalse(Key key, ModifierKeys modifiers)
    {
        Assert.False(QRCodeShortcutHelper.ShouldCopyImage(key, modifiers, isTextEditingControl: false));
    }
}
