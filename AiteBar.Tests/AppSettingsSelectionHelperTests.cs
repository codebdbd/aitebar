using AiteBar;

namespace AiteBar.Tests;

public sealed class AppSettingsSelectionHelperTests
{
    [Fact]
    public void ResolveSegmentedValue_Unchanged_PreservesContinuousSetting()
    {
        Assert.Equal(80, AppSettingsSelectionHelper.ResolveSegmentedValue(80, 70, selectionChanged: false));
        Assert.Equal(150, AppSettingsSelectionHelper.ResolveSegmentedValue(150, 100, selectionChanged: false));
    }

    [Fact]
    public void ResolveSegmentedValue_Changed_UsesSelectedPreset()
    {
        Assert.Equal(90, AppSettingsSelectionHelper.ResolveSegmentedValue(80, 90, selectionChanged: true));
        Assert.Equal(300, AppSettingsSelectionHelper.ResolveSegmentedValue(150, 300, selectionChanged: true));
    }

    [Theory]
    [InlineData(0, true, 1)]
    [InlineData(1, true, 1)]
    [InlineData(2, true, 2)]
    [InlineData(3, false, 0)]
    public void ResolveMonitorIndex_PreservesExistingSecondaryMonitor(int current, bool showSecondary, int expected)
    {
        Assert.Equal(expected, AppSettingsSelectionHelper.ResolveMonitorIndex(current, showSecondary));
    }
}
