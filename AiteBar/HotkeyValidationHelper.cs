using System;

namespace AiteBar;

public static class HotkeyValidationHelper
{
    public static bool HasAssignedKey(HotkeyBinding? binding) =>
        binding != null
        && !string.IsNullOrWhiteSpace(binding.Key)
        && !string.Equals(binding.Key, "None", StringComparison.OrdinalIgnoreCase);

    public static bool HasModifier(HotkeyBinding? binding) =>
        binding != null && (binding.Ctrl || binding.Alt || binding.Shift || binding.Win);

    public static bool IsRegisterableGlobalHotkey(HotkeyBinding? binding) =>
        !HasAssignedKey(binding) || HasModifier(binding);
}
