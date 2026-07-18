using AiteBar;

namespace AiteBar.Tests;

public sealed class AppSettingsSectionNavigationHelperTests
{
    [Theory]
    [InlineData(-50, 800, 0)]
    [InlineData(320, 800, 320)]
    [InlineData(950, 800, 800)]
    [InlineData(double.NaN, 800, 0)]
    [InlineData(320, double.NaN, 0)]
    public void GetTargetOffset_ClampsToScrollableRange(double sectionTop, double scrollableHeight, double expected)
    {
        Assert.Equal(expected, AppSettingsSectionNavigationHelper.GetTargetOffset(sectionTop, scrollableHeight));
    }

    [Fact]
    public void GetActiveSectionIndex_ReturnsMinusOneForNoSections()
    {
        Assert.Equal(
            -1,
            AppSettingsSectionNavigationHelper.GetActiveSectionIndex([], 0, 400, 400));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(190, 0)]
    [InlineData(200, 1)]
    [InlineData(610, 2)]
    [InlineData(980, 3)]
    public void GetActiveSectionIndex_UsesViewportMarker(double offset, int expectedIndex)
    {
        double[] sectionTops = [0, 224, 624, 1000];

        int actual = AppSettingsSectionNavigationHelper.GetActiveSectionIndex(
            sectionTops,
            offset,
            viewportHeight: 300,
            extentHeight: 1600,
            activationInset: 24);

        Assert.Equal(expectedIndex, actual);
    }

    [Fact]
    public void GetActiveSectionIndex_ChoosesLastSectionAtBottomWhenItsTopCannotReachMarker()
    {
        double[] sectionTops = [0, 300, 700, 1150];

        int actual = AppSettingsSectionNavigationHelper.GetActiveSectionIndex(
            sectionTops,
            verticalOffset: 900,
            viewportHeight: 500,
            extentHeight: 1400);

        Assert.Equal(3, actual);
    }

    [Fact]
    public void GetActiveSectionIndex_ShortPageSelectsLastSectionAtBottom()
    {
        Assert.Equal(
            1,
            AppSettingsSectionNavigationHelper.GetActiveSectionIndex(
                [0, 120],
                verticalOffset: 0,
                viewportHeight: 300,
                extentHeight: 240));
    }
}
