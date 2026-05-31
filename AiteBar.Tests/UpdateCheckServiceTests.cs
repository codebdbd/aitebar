using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
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

    [Theory]
    [InlineData("https://github.com/codebdbd/aitebar/releases")]
    [InlineData("https://github.com/codebdbd/aitebar/releases/tag/v1.7.0")]
    [InlineData("https://github.com/codebdbd/aitebar/releases/download/v1.7.0/AiteBarSetup.exe")]
    public void IsTrustedGitHubReleaseUrl_AcceptsRepositoryUrls(string url)
    {
        Assert.True(UpdateCheckService.IsTrustedGitHubReleaseUrl(url));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-url")]
    [InlineData("http://github.com/codebdbd/aitebar/releases")]
    [InlineData("https://github.com/other/aitebar/releases")]
    [InlineData("https://github.com/codebdbd/other/releases")]
    [InlineData("https://example.com/codebdbd/aitebar/releases")]
    public void IsTrustedGitHubReleaseUrl_RejectsUnexpectedUrls(string url)
    {
        Assert.False(UpdateCheckService.IsTrustedGitHubReleaseUrl(url));
    }

    private sealed class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpResponseMessage> _responseFactory;
        public MockHttpMessageHandler(Func<HttpResponseMessage> responseFactory)
        {
            _responseFactory = responseFactory;
        }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_responseFactory());
        }
    }
}
