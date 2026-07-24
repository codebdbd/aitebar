namespace AiteBar.Tests;

public sealed class TextProcessingTokenEstimationTests
{
    [Fact]
    public void EstimateTokens_UsesLatinCoefficient()
    {
        Assert.Equal(3, TextProcessingService.EstimateTokens("abcdefghij"));
    }

    [Fact]
    public void EstimateTokens_UsesCyrillicCoefficient()
    {
        Assert.Equal(4, TextProcessingService.EstimateTokens("абвгдеёжзи"));
    }

    [Fact]
    public void EstimateTokens_UsesMixedCoefficient()
    {
        Assert.Equal(3, TextProcessingService.EstimateTokens("abcабвгде"));
    }
}
