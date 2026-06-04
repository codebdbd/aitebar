using System.Collections.Generic;

namespace AiteBar;

public sealed record HotkeyKeyOption(string Key, string DisplayName);

public static class HotkeyKeyCatalog
{
    public static IReadOnlyList<HotkeyKeyOption> GlobalHotkeyKeys { get; } = BuildGlobalHotkeyKeys();

    public static IReadOnlyList<HotkeyKeyOption> ActionKeys { get; } = BuildActionKeys();

    private static IReadOnlyList<HotkeyKeyOption> BuildGlobalHotkeyKeys()
    {
        var keys = new List<HotkeyKeyOption>
        {
            new("Space", "Space"),
            new("Oem4", "["),
            new("Oem6", "]")
        };

        AddLettersAndDigits(keys);
        for (int i = 0; i <= 9; i++) keys.Add(new HotkeyKeyOption($"NumPad{i}", $"NumPad {i}"));
        keys.Add(new HotkeyKeyOption("Add", "NumPad +"));
        keys.Add(new HotkeyKeyOption("Subtract", "NumPad -"));
        keys.Add(new HotkeyKeyOption("Multiply", "NumPad *"));
        keys.Add(new HotkeyKeyOption("Divide", "NumPad /"));
        keys.Add(new HotkeyKeyOption("Decimal", "NumPad ."));
        AddFunctionKeys(keys);
        return keys;
    }

    private static IReadOnlyList<HotkeyKeyOption> BuildActionKeys()
    {
        var keys = new List<HotkeyKeyOption>();
        AddLettersAndDigits(keys);
        AddFunctionKeys(keys);
        keys.Add(new HotkeyKeyOption("PrintScreen", "PrntSc"));
        return keys;
    }

    private static void AddLettersAndDigits(List<HotkeyKeyOption> keys)
    {
        for (char c = 'A'; c <= 'Z'; c++) keys.Add(new HotkeyKeyOption(c.ToString(), c.ToString()));
        for (int i = 0; i <= 9; i++) keys.Add(new HotkeyKeyOption($"D{i}", i.ToString()));
    }

    private static void AddFunctionKeys(List<HotkeyKeyOption> keys)
    {
        for (int i = 1; i <= 12; i++) keys.Add(new HotkeyKeyOption($"F{i}", $"F{i}"));
    }
}
