using AiteBar;
using Xunit;

namespace AiteBar.Tests;

public sealed class HotkeyValidationHelperTests
{
    [Fact]
    public void IsRegisterableGlobalHotkey_AllowsUnassignedBinding()
    {
        Assert.True(HotkeyValidationHelper.IsRegisterableGlobalHotkey(new HotkeyBinding { Key = "None" }));
        Assert.True(HotkeyValidationHelper.IsRegisterableGlobalHotkey(new HotkeyBinding { Key = "" }));
    }

    [Fact]
    public void IsRegisterableGlobalHotkey_RejectsAssignedKeyWithoutModifier()
    {
        var binding = new HotkeyBinding { Key = "Space" };

        Assert.False(HotkeyValidationHelper.IsRegisterableGlobalHotkey(binding));
    }

    [Fact]
    public void IsRegisterableGlobalHotkey_AllowsAssignedKeyWithModifier()
    {
        var binding = new HotkeyBinding { Ctrl = true, Key = "Space" };

        Assert.True(HotkeyValidationHelper.IsRegisterableGlobalHotkey(binding));
    }
}
