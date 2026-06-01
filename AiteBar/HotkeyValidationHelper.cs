using System;
using System.Collections.Generic;
using System.Linq;

namespace AiteBar;

public static class HotkeyValidationHelper
{
    private static readonly HashSet<(bool Ctrl, bool Alt, bool Shift, bool Win, string Key)> ReservedHotkeys = new()
    {
        (true, true, false, false, "Delete"), // Ctrl+Alt+Del
        (false, false, false, true, "L"),     // Win+L (заблокировать экран)
        (false, false, false, true, "R"),     // Win+R (выполнить)
        (false, false, false, true, "Tab"),   // Win+Tab (переключение задач)
        (true, false, false, false, "Escape"),// Ctrl+Esc
        (true, false, false, true, "Escape"), // Ctrl+Win+Esc
        (false, false, false, true, "D"),     // Win+D (показать рабочий стол)
        (false, false, false, true, "E"),     // Win+E (проводник)
        (false, false, false, true, "M"),     // Win+M (свернуть все окна)
        (false, false, true, true, "M"),      // Win+Shift+M (восстановить окна)
        (false, false, false, true, "P"),     // Win+P (проекция)
        (false, false, false, true, "I"),     // Win+I (параметры)
        (false, false, false, true, "A"),     // Win+A (центр уведомлений)
        (false, false, false, true, "V"),     // Win+V (буфер обмена)
        (false, false, false, true, "X"),     // Win+X (меню WinX)
        (false, false, false, true, "Pause"), // Win+Pause (системные свойства)
        (false, false, false, true, "Print"), // Win+PrintScreen (скриншот в файл)
        (true, false, false, true, "D"),      // Ctrl+Win+D (новый виртуальный рабочий стол)
        (true, false, false, true, "F4"),     // Ctrl+Win+F4 (закрыть виртуальный рабочий стол)
        (true, false, false, true, "Left"),   // Ctrl+Win+← (переключиться на левый рабочий стол)
        (true, false, false, true, "Right"),  // Ctrl+Win+→ (переключиться на правый рабочий стол)
    };

    public static bool HasAssignedKey(HotkeyBinding? binding) =>
        binding != null
        && !string.IsNullOrWhiteSpace(binding.Key)
        && !string.Equals(binding.Key, "None", StringComparison.OrdinalIgnoreCase);

    public static bool HasModifier(HotkeyBinding? binding) =>
        binding != null && (binding.Ctrl || binding.Alt || binding.Shift || binding.Win);

    public static bool IsRegisterableGlobalHotkey(HotkeyBinding? binding) =>
        !HasAssignedKey(binding) || HasModifier(binding);

    public static bool IsReservedHotkey(HotkeyBinding binding)
    {
        return ReservedHotkeys.Contains((binding.Ctrl, binding.Alt, binding.Shift, binding.Win, binding.Key));
    }

    public static bool HasConflicts(HotkeyBinding binding, IEnumerable<HotkeyBinding> existingBindings)
    {
        return existingBindings.Any(b =>
            b.Ctrl == binding.Ctrl &&
            b.Alt == binding.Alt &&
            b.Shift == binding.Shift &&
            b.Win == binding.Win &&
            string.Equals(b.Key, binding.Key, StringComparison.OrdinalIgnoreCase));
    }

    public static bool IsValidKey(string key)
    {
        return Enum.TryParse(typeof(System.Windows.Input.Key), key, true, out _);
    }
}
