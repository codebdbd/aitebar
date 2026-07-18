using System.Windows.Input;

namespace AiteBar.Tests;

public sealed class HotkeyCaptureHelperTests
{
    [Fact]
    public void TryCreateBinding_CapturesCatalogKeyAndModifiersInStableOrder()
    {
        bool captured = HotkeyCaptureHelper.TryCreateBinding(
            Key.P,
            ModifierKeys.Control | ModifierKeys.Alt,
            out HotkeyBinding binding);

        Assert.True(captured);
        Assert.True(binding.Ctrl);
        Assert.True(binding.Alt);
        Assert.False(binding.Shift);
        Assert.Equal("P", binding.Key);
        Assert.Equal("Ctrl + Alt + P", HotkeyCaptureHelper.Format(binding, "Not assigned"));
    }

    [Theory]
    [InlineData(Key.D4, ModifierKeys.Shift, "D4", "Shift + 4")]
    [InlineData(Key.OemOpenBrackets, ModifierKeys.Control, "Oem4", "Ctrl + [")]
    [InlineData(Key.NumPad7, ModifierKeys.Windows, "NumPad7", "Win + NumPad 7")]
    [InlineData(Key.F12, ModifierKeys.Control | ModifierKeys.Shift, "F12", "Ctrl + Shift + F12")]
    public void TryCreateBinding_NormalizesSupportedKeyFamilies(
        Key key,
        ModifierKeys modifiers,
        string expectedToken,
        string expectedDisplay)
    {
        Assert.True(HotkeyCaptureHelper.TryCreateBinding(key, modifiers, out HotkeyBinding binding));
        Assert.Equal(expectedToken, binding.Key);
        Assert.Equal(expectedDisplay, HotkeyCaptureHelper.Format(binding, "Not assigned"));
    }

    [Theory]
    [InlineData(Key.LeftCtrl)]
    [InlineData(Key.RightCtrl)]
    [InlineData(Key.LeftShift)]
    [InlineData(Key.RightShift)]
    [InlineData(Key.LeftAlt)]
    [InlineData(Key.RightAlt)]
    [InlineData(Key.LWin)]
    [InlineData(Key.RWin)]
    [InlineData(Key.Escape)]
    [InlineData(Key.Tab)]
    [InlineData(Key.Delete)]
    [InlineData(Key.Back)]
    public void TryCreateBinding_RejectsModifierAndUnsupportedKeys(Key key)
    {
        Assert.False(HotkeyCaptureHelper.TryCreateBinding(key, ModifierKeys.None, out _));
    }

    [Theory]
    [InlineData(ModifierKeys.Control)]
    [InlineData(ModifierKeys.Shift)]
    [InlineData(ModifierKeys.Windows)]
    public void TryCreateBinding_AcceptsSingleModifierWithKey(ModifierKeys modifier)
    {
        bool captured = HotkeyCaptureHelper.TryCreateBinding(
            Key.A,
            modifier,
            out HotkeyBinding binding);

        Assert.True(captured);
        Assert.Equal("A", binding.Key);
    }

    [Fact]
    public void Clone_DoesNotShareMutableBinding()
    {
        var source = new HotkeyBinding { Ctrl = true, Key = "A" };
        HotkeyBinding clone = HotkeyCaptureHelper.Clone(source);

        clone.Key = "B";

        Assert.Equal("A", source.Key);
        Assert.Equal("B", clone.Key);
    }

    [Fact]
    public void Format_UsesNotAssignedTextForEmptyBinding()
    {
        Assert.Equal("Не назначено", HotkeyCaptureHelper.Format(new HotkeyBinding(), "Не назначено"));
    }

    [Theory]
    [InlineData(false, false, false, true, "E")]  // Win+E
    [InlineData(false, false, false, true, "R")]  // Win+R
    [InlineData(false, false, false, true, "L")]  // Win+L
    [InlineData(true, true, false, false, "Delete")] // Ctrl+Alt+Del
    public void IsReservedHotkey_DetectsSystemCombinations(bool ctrl, bool alt, bool shift, bool win, string key)
    {
        var binding = new HotkeyBinding { Ctrl = ctrl, Alt = alt, Shift = shift, Win = win, Key = key };
        Assert.True(HotkeyValidationHelper.IsReservedHotkey(binding));
    }

    [Theory]
    [InlineData(true, false, false, false, "P")]  // Ctrl+P (not reserved)
    [InlineData(false, true, false, false, "A")]  // Alt+A (not reserved)
    [InlineData(true, false, false, false, "E")]  // Ctrl+E (not reserved)
    public void IsReservedHotkey_AllowsNonReservedCombinations(bool ctrl, bool alt, bool shift, bool win, string key)
    {
        var binding = new HotkeyBinding { Ctrl = ctrl, Alt = alt, Shift = shift, Win = win, Key = key };
        Assert.False(HotkeyValidationHelper.IsReservedHotkey(binding));
    }
}
