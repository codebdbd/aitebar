namespace AiteBar;

internal static class HotkeyCaptureHelper
{
    public static bool TryCreateBinding(Key key, ModifierKeys modifiers, out HotkeyBinding binding)
    {
        binding = new HotkeyBinding();
        string? token = NormalizeKey(key);
        if (token == null)
        {
            return false;
        }

        binding.Ctrl = modifiers.HasFlag(ModifierKeys.Control);
        binding.Shift = modifiers.HasFlag(ModifierKeys.Shift);
        binding.Alt = modifiers.HasFlag(ModifierKeys.Alt);
        binding.Win = modifiers.HasFlag(ModifierKeys.Windows);
        binding.Key = token;
        return true;
    }

    public static string Format(HotkeyBinding? binding, string notAssignedText)
    {
        if (!HotkeyValidationHelper.HasAssignedKey(binding))
        {
            return notAssignedText;
        }

        var parts = new List<string>(5);
        if (binding!.Ctrl) parts.Add("Ctrl");
        if (binding.Shift) parts.Add("Shift");
        if (binding.Alt) parts.Add("Alt");
        if (binding.Win) parts.Add("Win");

        string keyDisplay = HotkeyKeyCatalog.GlobalHotkeyKeys
            .FirstOrDefault(option => string.Equals(option.Key, binding.Key, StringComparison.OrdinalIgnoreCase))
            ?.DisplayName ?? binding.Key;
        parts.Add(keyDisplay);
        return string.Join(" + ", parts);
    }

    public static HotkeyBinding Clone(HotkeyBinding? binding) => new()
    {
        Ctrl = binding?.Ctrl ?? false,
        Shift = binding?.Shift ?? false,
        Alt = binding?.Alt ?? false,
        Win = binding?.Win ?? false,
        Key = string.IsNullOrWhiteSpace(binding?.Key) ? "None" : binding.Key
    };

    private static string? NormalizeKey(Key key)
    {
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftShift or Key.RightShift
            or Key.LeftAlt or Key.RightAlt or Key.LWin or Key.RWin)
        {
            return null;
        }

        string candidate = key switch
        {
            Key.OemOpenBrackets => "Oem4",
            Key.OemCloseBrackets => "Oem6",
            _ => key.ToString()
        };

        return HotkeyKeyCatalog.GlobalHotkeyKeys
            .FirstOrDefault(option => string.Equals(option.Key, candidate, StringComparison.OrdinalIgnoreCase))
            ?.Key;
    }
}
