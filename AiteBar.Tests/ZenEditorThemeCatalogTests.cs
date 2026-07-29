using AiteBar;

namespace AiteBar.Tests;

public sealed class ZenEditorThemeCatalogTests
{
    [Fact]
    public void All_ContainsExactlyFiveFixedThemes()
    {
        Assert.Equal(
            ["paper", "ivory", "mist", "graphite", "night"],
            ZenEditorThemeCatalog.All.Select(theme => theme.Id));
    }

    [Fact]
    public void Get_UnknownThemeFallsBackToPaper()
    {
        Assert.Equal("paper", ZenEditorThemeCatalog.Get("unknown").Id);
        Assert.Equal("paper", ZenEditorThemeCatalog.Get(null).Id);
    }

    [Theory]
    [InlineData("paper", 1, "ivory")]
    [InlineData("ivory", -1, "paper")]
    [InlineData("night", 1, "paper")]
    [InlineData("paper", -1, "night")]
    public void GetAdjacent_CyclesInBothDirections(
        string current,
        int direction,
        string expected)
    {
        Assert.Equal(expected, ZenEditorThemeCatalog.GetAdjacent(current, direction).Id);
    }
}
