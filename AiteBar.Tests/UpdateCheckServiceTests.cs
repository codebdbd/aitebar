using System;
using AiteBar;
using Xunit;

namespace AiteBar.Tests;

public sealed class UpdateCheckServiceTests
{
    [Theory]
    [InlineData("v1.6.1", 1, 6, 1)]
    [InlineData("1.7.0", 1, 7, 0)]
    [InlineData("v2.0.0-beta.1", 2, 0, 0)]
    public void TryParseReleaseVersion_AcceptsReleaseTagFormats(string tag, int major, int minor, int build)
    {
        bool parsed = UpdateCheckService.TryParseReleaseVersion(tag, out Version version);

        Assert.True(parsed);
        Assert.Equal(new Version(major, minor, build), version);
    }

    [Theory]
    [InlineData("")]
    [InlineData("release")]
    [InlineData("vnext")]
    public void TryParseReleaseVersion_RejectsInvalidTags(string tag)
    {
        Assert.False(UpdateCheckService.TryParseReleaseVersion(tag, out _));
    }

    [Theory]
    [InlineData("1.6.2", "1.6.1", true)]
    [InlineData("1.7.0", "1.6.9", true)]
    [InlineData("1.6.1", "1.6.1", false)]
    [InlineData("1.6.0", "1.6.1", false)]
    public void IsNewerVersion_ComparesNormalizedVersions(string latestText, string currentText, bool expected)
    {
        var latest = Version.Parse(latestText);
        var current = Version.Parse(currentText);

        Assert.Equal(expected, UpdateCheckService.IsNewerVersion(latest, current));
    }
}
