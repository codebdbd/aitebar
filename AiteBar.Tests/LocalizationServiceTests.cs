using System.Globalization;
using AiteBar;
using Xunit;

namespace AiteBar.Tests;

public sealed class LocalizationServiceTests
{
    [Fact]
    public void NormalizeCultureName_Null_ReturnsAuto()
    {
        Assert.Equal(LocalizationService.AutoCulture, LocalizationService.NormalizeCultureName(null));
    }

    [Fact]
    public void NormalizeCultureName_Empty_ReturnsAuto()
    {
        Assert.Equal(LocalizationService.AutoCulture, LocalizationService.NormalizeCultureName(""));
    }

    [Fact]
    public void NormalizeCultureName_Whitespace_ReturnsAuto()
    {
        Assert.Equal(LocalizationService.AutoCulture, LocalizationService.NormalizeCultureName("   "));
    }

    [Theory]
    [InlineData("en")]
    [InlineData("EN")]
    [InlineData("En")]
    public void NormalizeCultureName_EnglishVariants_ReturnsEn(string input)
    {
        Assert.Equal("en", LocalizationService.NormalizeCultureName(input));
    }

    [Theory]
    [InlineData("de")]
    [InlineData("uk")]
    [InlineData("ru")]
    public void NormalizeCultureName_SupportedCultures_ReturnsNormalized(string input)
    {
        Assert.Equal(input, LocalizationService.NormalizeCultureName(input));
    }

    [Theory]
    [InlineData("DE")]
    [InlineData("UK")]
    [InlineData("RU")]
    public void NormalizeCultureName_SupportedCulturesUpperCase_ReturnsNormalized(string input)
    {
        Assert.Equal(input.ToLower(), LocalizationService.NormalizeCultureName(input));
    }

    [Fact]
    public void NormalizeCultureName_UnsupportedCulture_ReturnsAuto()
    {
        Assert.Equal(LocalizationService.AutoCulture, LocalizationService.NormalizeCultureName("fr"));
    }

    [Fact]
    public void NormalizeCultureName_InvalidCulture_ReturnsAuto()
    {
        Assert.Equal(LocalizationService.AutoCulture, LocalizationService.NormalizeCultureName("invalid-culture"));
    }

    [Fact]
    public void NormalizeCultureName_Auto_ReturnsAuto()
    {
        Assert.Equal(LocalizationService.AutoCulture, LocalizationService.NormalizeCultureName(LocalizationService.AutoCulture));
    }

    [Fact]
    public void ResolveCulture_Auto_ReturnsOSSupportedOrDefault()
    {
        var culture = LocalizationService.ResolveCulture(LocalizationService.AutoCulture);
        Assert.NotNull(culture);
        Assert.Contains(culture.TwoLetterISOLanguageName, new[] { "en", "de", "uk", "ru" });
    }

    [Fact]
    public void ResolveCulture_En_ReturnsEnglishCulture()
    {
        var culture = LocalizationService.ResolveCulture("en");
        Assert.Equal("en", culture.TwoLetterISOLanguageName);
    }

    [Fact]
    public void ResolveCulture_SupportedCulture_ReturnsCorrectCulture()
    {
        var culture = LocalizationService.ResolveCulture("de");
        Assert.Equal("de", culture.TwoLetterISOLanguageName);
    }

    [Fact]
    public void Get_ExistingKey_ReturnsValue()
    {
        var result = LocalizationService.Get("Common_Cancel");
        Assert.NotNull(result);
        Assert.NotEqual("[[Common_Cancel]]", result);
    }

    [Fact]
    public void Get_NonExistingKey_ReturnsPlaceholder()
    {
        var result = LocalizationService.Get("NonExistingKey_12345");
        Assert.Equal("[[NonExistingKey_12345]]", result);
    }

    [Fact]
    public void Format_WithArgs_FormatsCorrectly()
    {
        var result = LocalizationService.Format("DeleteButtonConfirm", "Test Button");
        Assert.Contains("Test Button", result);
    }

    [Fact]
    public void Format_WithMultipleArgs_FormatsCorrectly()
    {
        var result = LocalizationService.Format("Element_CopySuffixFormat", 2);
        Assert.Contains("2", result);
    }
}
