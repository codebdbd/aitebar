using AiteBar;
using Xunit;

namespace AiteBar.Tests;

public sealed class ActionTargetHelperUrlTests
{
    [Theory]
    [InlineData("example.com", "https://example.com/")]
    [InlineData("https://example.com", "https://example.com/")]
    [InlineData("http://example.com", "http://example.com/")]
    [InlineData("localhost:5000", "https://localhost:5000/")]
    [InlineData("http://192.168.1.10", "http://192.168.1.10/")]
    [InlineData("intranet", "https://intranet/")]
    public void TryNormalizeWebUrl_AcceptsHttpHttpsAndBareDomains(string input, string expected)
    {
        bool ok = ActionTargetHelper.TryNormalizeWebUrl(input, out string normalized);

        Assert.True(ok);
        Assert.Equal(expected, normalized);
    }

    [Theory]
    [InlineData("ftp://example.com")]
    [InlineData("not a url")]
    [InlineData("https://")]
    [InlineData("")]
    public void TryNormalizeWebUrl_RejectsUnsupportedOrInvalidValues(string input)
    {
        bool ok = ActionTargetHelper.TryNormalizeWebUrl(input, out string normalized);

        Assert.False(ok);
        Assert.Equal("", normalized);
    }
}
