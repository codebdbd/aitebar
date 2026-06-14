using System.Linq;
using AiteBar;

namespace AiteBar.Tests;

public sealed class HotkeyKeyCatalogTests
{
    [Fact]
    public void GlobalHotkeyKeys_ContainExpectedKeysAndNoDuplicates()
    {
        string[] keys = HotkeyKeyCatalog.GlobalHotkeyKeys.Select(option => option.Key).ToArray();

        Assert.Contains("Space", keys);
        Assert.Contains("Oem4", keys);
        Assert.Contains("Oem6", keys);
        Assert.Contains("A", keys);
        Assert.Contains("D0", keys);
        Assert.Contains("NumPad0", keys);
        Assert.Contains("F12", keys);
        Assert.Equal(keys.Length, keys.Distinct().Count());
        Assert.All(HotkeyKeyCatalog.GlobalHotkeyKeys, option => Assert.False(string.IsNullOrWhiteSpace(option.DisplayName)));
    }

    [Fact]
    public void ActionKeys_ContainExpectedKeysAndNoDuplicates()
    {
        string[] keys = HotkeyKeyCatalog.ActionKeys.Select(option => option.Key).ToArray();

        Assert.Contains("A", keys);
        Assert.Contains("D9", keys);
        Assert.Contains("F1", keys);
        Assert.Contains("F12", keys);
        Assert.Contains("PrintScreen", keys);
        Assert.DoesNotContain("Space", keys);
        Assert.Equal(keys.Length, keys.Distinct().Count());
        Assert.All(HotkeyKeyCatalog.ActionKeys, option => Assert.False(string.IsNullOrWhiteSpace(option.DisplayName)));
    }
}
