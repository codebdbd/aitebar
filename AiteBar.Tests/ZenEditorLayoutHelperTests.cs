using System;
using Xunit;

namespace AiteBar.Tests;

public sealed class ZenEditorLayoutHelperTests
{
    [Theory]
    [InlineData(1920, 760, 760)]
    [InlineData(1000, 760, 760)]
    [InlineData(800, 760, 736)]
    [InlineData(500, 760, 436)]
    [InlineData(64, 760, 0)]
    [InlineData(0, 760, 0)]
    [InlineData(-100, 760, 0)]
    [InlineData(double.NaN, 760, 0)]
    public void CalculateEditorWidth_ReturnsExpectedWidth(double containerWidth, double themeColumnWidth, double expectedWidth)
    {
        double actual = ZenEditorLayoutHelper.CalculateEditorWidth(containerWidth, themeColumnWidth);
        Assert.Equal(expectedWidth, actual);
    }

    [Theory]
    [InlineData(1920, 760, 48, 532)] // sideMargin = 580, safe = 580 - 48 = 532
    [InlineData(2560, 760, 48, 852)] // sideMargin = 900, safe = 900 - 48 = 852
    [InlineData(3840, 760, 48, 1492)] // sideMargin = 1540, safe = 1540 - 48 = 1492
    [InlineData(1366, 760, 48, 255)] // sideMargin = 303, safe = 303 - 48 = 255
    [InlineData(1024, 760, 48, 84)]  // sideMargin = 132, safe = 132 - 48 = 84
    [InlineData(1000, 760, 48, 72)]  // sideMargin = 120, safe = 120 - 48 = 72
    [InlineData(900, 760, 48, 22)]   // sideMargin = 70, safe = 70 - 48 = 22
    [InlineData(890, 760, 48, 0)]    // sideMargin = 65, safe = 17 < 20 -> 0
    [InlineData(850, 760, 48, 0)]    // sideMargin = 45, safe = 45 - 48 < 0 -> 0
    [InlineData(800, 736, 48, 0)]    // sideMargin = 32, safe = 32 - 48 < 0 -> 0
    [InlineData(1920, 760, 24, 556)] // safety margin 24: 580 - 24 = 556
    [InlineData(1920, 760, 72, 508)] // safety margin 72: 580 - 72 = 508
    [InlineData(1920, 760, 96, 484)] // safety margin 96: 580 - 96 = 484
    [InlineData(0, 760, 48, 0)]
    [InlineData(-500, 760, 48, 0)]
    [InlineData(1920, 0, 48, 0)]
    [InlineData(double.NaN, 760, 48, 0)]
    [InlineData(1920, double.NaN, 48, 0)]
    public void CalculateExitZoneWidth_ReturnsExpectedWidth(double containerWidth, double editorWidth, double safetyMargin, double expectedWidth)
    {
        double actual = ZenEditorLayoutHelper.CalculateExitZoneWidth(containerWidth, editorWidth, safetyMargin);
        Assert.Equal(expectedWidth, actual);
    }

    [Theory]
    [InlineData(-10)]
    [InlineData(double.NaN)]
    public void CalculateExitZoneWidth_InvalidSafetyMargin_FallsBackToDefault(double invalidMargin)
    {
        double defaultResult = ZenEditorLayoutHelper.CalculateExitZoneWidth(1920, 760, ZenEditorLayoutHelper.DefaultSideSafetyMargin);
        double resultWithInvalid = ZenEditorLayoutHelper.CalculateExitZoneWidth(1920, 760, invalidMargin);
        Assert.Equal(defaultResult, resultWithInvalid);
    }

    [Fact]
    public void CalculateExitZoneWidth_AlwaysGuaranteesSafetyBuffer()
    {
        double containerWidth = 980;
        double editorWidth = 760;
        double safetyMargin = 48.0;

        double zoneWidth = ZenEditorLayoutHelper.CalculateExitZoneWidth(
            containerWidth,
            editorWidth,
            safetyMargin: safetyMargin);

        double sideMargin = (containerWidth - editorWidth) / 2;
        Assert.True(sideMargin - zoneWidth >= safetyMargin);
    }
}
