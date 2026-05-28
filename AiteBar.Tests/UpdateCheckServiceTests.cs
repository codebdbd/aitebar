using System;
using System.IO;
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

    [Fact]
    public async Task DownloadInstallerAsync_NullInstallerUrl_ReturnsNull()
    {
        var mockHandler = new MockHttpMessageHandler(() => new HttpResponseMessage(System.Net.HttpStatusCode.OK));
        var httpClient = new HttpClient(mockHandler);
        var service = new UpdateCheckService(httpClient);
        var result = new UpdateCheckResult(false, new Version(1,0,0), null, null, null, null);

        string? path = await service.DownloadInstallerAsync(result);
        
        Assert.Null(path);
    }

    [Fact]
    public async Task DownloadInstallerAsync_ValidInstallerUrl_DownloadsFile()
    {
        var testContent = new byte[] { 0x00, 0x01, 0x02, 0x03, 0x04 };
        var mockHandler = new MockHttpMessageHandler(() => new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(testContent)
        });
        var httpClient = new HttpClient(mockHandler);
        var service = new UpdateCheckService(httpClient);
        var result = new UpdateCheckResult(
            true, 
            new Version(1,0,0), 
            new Version(1,1,0), 
            "https://github.com/codebdbd/aitebar/releases/tag/v1.1.0", 
            "https://github.com/codebdbd/aitebar/releases/download/v1.1.0/AiteBarSetup.exe", 
            null);

        string? path = await service.DownloadInstallerAsync(result);
        
        Assert.NotNull(path);
        Assert.True(File.Exists(path));
        byte[] downloadedContent = await File.ReadAllBytesAsync(path);
        Assert.Equal(testContent, downloadedContent);
        
        File.Delete(path);
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
