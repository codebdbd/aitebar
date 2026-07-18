namespace AiteBar.Tests;

public sealed class AppSettingsDiscreteChoiceHelperTests
{
    [Theory]
    [InlineData(50, 0)]
    [InlineData(70, 1)]
    [InlineData(90, 2)]
    [InlineData(100, 3)]
    [InlineData(82, 2)]
    public void GetNearestIndex_ReturnsExpectedPanelSizeIndex(double value, int expected)
    {
        Assert.Equal(expected, AppSettingsDiscreteChoiceHelper.GetNearestIndex(
            AppSettingsDiscreteChoiceHelper.PanelSizeValues,
            value));
    }

    [Theory]
    [InlineData(10, 0)]
    [InlineData(30, 1)]
    [InlineData(50, 2)]
    [InlineData(100, 3)]
    [InlineData(42, 2)]
    public void GetNearestIndex_ReturnsExpectedActivationZoneIndex(double value, int expected)
    {
        Assert.Equal(expected, AppSettingsDiscreteChoiceHelper.GetNearestIndex(
            AppSettingsDiscreteChoiceHelper.ActivationZoneValues,
            value));
    }

    [Theory]
    [InlineData(-2, 100)]
    [InlineData(0, 100)]
    [InlineData(1, 200)]
    [InlineData(2, 300)]
    [InlineData(3, 500)]
    [InlineData(8, 500)]
    public void GetValue_ClampsAndMapsActivationDelayIndex(double index, int expected)
    {
        Assert.Equal(expected, AppSettingsDiscreteChoiceHelper.GetValue(
            AppSettingsDiscreteChoiceHelper.ActivationDelayValues,
            index));
    }

    [Fact]
    public void EmptyChoices_ReturnSafeDefaults()
    {
        Assert.Equal(0, AppSettingsDiscreteChoiceHelper.GetNearestIndex([], 100));
        Assert.Equal(0, AppSettingsDiscreteChoiceHelper.GetValue([], 2));
    }

    [Theory]
    [InlineData(80, 1)] // equidistant from 70 (idx 1) and 90 (idx 2), picks first (70)
    [InlineData(60, 0)] // equidistant from 50 (idx 0) and 70 (idx 1), picks first (50)
    public void GetNearestIndex_TieBreaksToFirstMatch(double value, int expected)
    {
        Assert.Equal(expected, AppSettingsDiscreteChoiceHelper.GetNearestIndex(
            AppSettingsDiscreteChoiceHelper.PanelSizeValues,
            value));
    }

    [Theory]
    [InlineData(65, 70)]  // non-standard -> snaps to 70
    [InlineData(85, 90)]  // non-standard -> snaps to 90
    [InlineData(55, 50)]  // non-standard -> snaps to 50
    [InlineData(92, 90)]  // closer to 90 than 100
    public void GetNearestIndex_NormalizesNonStandardPanelSizeValues(double input, int expectedValue)
    {
        int index = AppSettingsDiscreteChoiceHelper.GetNearestIndex(
            AppSettingsDiscreteChoiceHelper.PanelSizeValues, input);
        Assert.Equal(expectedValue, AppSettingsDiscreteChoiceHelper.PanelSizeValues[index]);
    }

    [Theory]
    [InlineData(20, 10)]  // non-standard -> snaps to 10
    [InlineData(40, 30)]  // non-standard -> snaps to 30
    [InlineData(60, 50)]  // non-standard -> snaps to 50
    public void GetNearestIndex_NormalizesNonStandardActivationZoneValues(double input, int expectedValue)
    {
        int index = AppSettingsDiscreteChoiceHelper.GetNearestIndex(
            AppSettingsDiscreteChoiceHelper.ActivationZoneValues, input);
        Assert.Equal(expectedValue, AppSettingsDiscreteChoiceHelper.ActivationZoneValues[index]);
    }

    [Theory]
    [InlineData(150, 100)] // closer to 100 than 200
    [InlineData(250, 200)] // closer to 200 than 300
    [InlineData(350, 300)] // closer to 300 than 500
    public void GetNearestIndex_NormalizesNonStandardActivationDelayValues(double input, int expectedValue)
    {
        int index = AppSettingsDiscreteChoiceHelper.GetNearestIndex(
            AppSettingsDiscreteChoiceHelper.ActivationDelayValues, input);
        Assert.Equal(expectedValue, AppSettingsDiscreteChoiceHelper.ActivationDelayValues[index]);
    }
}
