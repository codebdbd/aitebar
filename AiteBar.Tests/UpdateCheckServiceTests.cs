using System;
using System.Diagnostics;
using System.Net.Http;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using AiteBar;
using Xunit;

namespace AiteBar.Tests;

public sealed class UpdateCheckServiceTests
{
    [Fact]
    public void Constructor_CreatesDefaultHttpClient()
    {
        var service = new UpdateCheckService();

        Assert.NotNull(service);
    }

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
    public async Task CheckLatestReleaseAsync_HttpFailure_ReturnsUnavailableMessage()
    {
        var httpClient = new HttpClient(new MockHttpMessageHandler(() => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)));
        var service = new UpdateCheckService(httpClient);

        UpdateCheckResult result = await service.CheckLatestReleaseAsync();

        Assert.False(result.IsUpdateAvailable);
        Assert.Null(result.LatestVersion);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public async Task CheckLatestReleaseAsync_InvalidJson_ReturnsInvalidResponse()
    {
        var httpClient = new HttpClient(new MockHttpMessageHandler(() => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{ invalid json")
        }));
        var service = new UpdateCheckService(httpClient);

        UpdateCheckResult result = await service.CheckLatestReleaseAsync();

        Assert.False(result.IsUpdateAvailable);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public async Task CheckLatestReleaseAsync_InvalidReleaseUrl_ReturnsSpecificError()
    {
        var httpClient = new HttpClient(new MockHttpMessageHandler(() => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"tag_name":"v9.9.9","html_url":"https://example.com/release","assets":[{"browser_download_url":"https://github.com/codebdbd/aitebar/releases/download/v9.9.9/AiteBar.exe"}]}""")
        }));
        var service = new UpdateCheckService(httpClient);

        UpdateCheckResult result = await service.CheckLatestReleaseAsync();

        Assert.False(result.IsUpdateAvailable);
        Assert.Equal(new Version(9, 9, 9), result.LatestVersion);
        Assert.Null(result.ReleasePageUrl);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public async Task CheckLatestReleaseAsync_ValidResponse_ReturnsTrustedUrls()
    {
        var httpClient = new HttpClient(new MockHttpMessageHandler(() => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"tag_name":"v9.9.9","html_url":"https://github.com/codebdbd/aitebar/releases/tag/v9.9.9","assets":[{"browser_download_url":"https://github.com/codebdbd/aitebar/releases/download/v9.9.9/AiteBar.exe"}]}""")
        }));
        var service = new UpdateCheckService(httpClient);

        UpdateCheckResult result = await service.CheckLatestReleaseAsync();

        Assert.Equal(new Version(9, 9, 9), result.LatestVersion);
        Assert.Equal("https://github.com/codebdbd/aitebar/releases/tag/v9.9.9", result.ReleasePageUrl);
        Assert.Equal("https://github.com/codebdbd/aitebar/releases/download/v9.9.9/AiteBar.exe", result.InstallerUrl);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void GetCurrentVersion_ReturnsInformationalVersion()
    {
        Version version = UpdateCheckService.GetCurrentVersion();

        Assert.Equal(new Version(1, 7, 6, 0), version);
    }

    [Fact]
    public void FormatVersion_NullVersion_ReturnsUnknown()
    {
        Assert.Equal(LocalizationService.Get("Update_UnknownVersion"), UpdateCheckService.FormatVersion(null));
    }

    [Fact]
    public void FormatVersion_NormalizesToThreePartVersion()
    {
        Assert.Equal("1.2.0", UpdateCheckService.FormatVersion(new Version(1, 2)));
        Assert.Equal("1.2.3", UpdateCheckService.FormatVersion(new Version(1, 2, 3, 4)));
    }

    [Fact]
    public void OpenReleasePage_UsesTrustedUrlAndFallsBackWhenNeeded()
    {
        var dispatcher = new FakeProcessStartDispatcher();
        var service = new UpdateCheckService(new HttpClient(new MockHttpMessageHandler(() => new HttpResponseMessage(HttpStatusCode.OK))), dispatcher);

        service.OpenReleasePage(new UpdateCheckResult(false, new Version(1, 0, 0), null, "https://github.com/codebdbd/aitebar/releases/tag/v1.0.0", null, null));
        service.OpenReleasePage(new UpdateCheckResult(false, new Version(1, 0, 0), null, "https://example.com/bad", null, null));

        Assert.Equal(2, dispatcher.StartCalls.Count);
        Assert.Equal("https://github.com/codebdbd/aitebar/releases/tag/v1.0.0", dispatcher.StartCalls[0].FileName);
        Assert.Equal("https://github.com/codebdbd/aitebar/releases", dispatcher.StartCalls[1].FileName);
        Assert.All(dispatcher.StartCalls, psi => Assert.True(psi.UseShellExecute));
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

    private sealed class FakeProcessStartDispatcher : IProcessStartDispatcher
    {
        public System.Collections.Generic.List<ProcessStartInfo> StartCalls { get; } = [];

        public void Start(ProcessStartInfo startInfo)
        {
            StartCalls.Add(startInfo);
        }
    }
}
