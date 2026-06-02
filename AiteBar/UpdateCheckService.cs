using System;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace AiteBar;

internal interface IProcessStartDispatcher
{
    void Start(ProcessStartInfo startInfo);
}

internal sealed class ProcessStartDispatcher : IProcessStartDispatcher
{
    public void Start(ProcessStartInfo startInfo)
    {
        Process.Start(startInfo);
    }
}

public sealed record UpdateCheckResult(
    bool IsUpdateAvailable,
    Version CurrentVersion,
    Version? LatestVersion,
    string? ReleasePageUrl,
    string? InstallerUrl,
    string? ErrorMessage);

public sealed class UpdateCheckService
{
    private const string LatestReleaseApiUrl = "https://api.github.com/repos/codebdbd/aitebar/releases/latest";
    private const string ReleasesFallbackUrl = "https://github.com/codebdbd/aitebar/releases";
    private const string GitHubHost = "github.com";
    private const string RepositoryPathPrefix = "/codebdbd/aitebar/";
    private static readonly Uri LatestReleaseUri = new(LatestReleaseApiUrl);
    private readonly HttpClient _httpClient;
    private readonly IProcessStartDispatcher _processStartDispatcher;

    public UpdateCheckService()
        : this(CreateHttpClient(), new ProcessStartDispatcher())
    {
    }

    internal UpdateCheckService(HttpClient httpClient)
        : this(httpClient, new ProcessStartDispatcher())
    {
    }

    internal UpdateCheckService(HttpClient httpClient, IProcessStartDispatcher processStartDispatcher)
    {
        _httpClient = httpClient;
        _processStartDispatcher = processStartDispatcher;
    }

    public async Task<UpdateCheckResult> CheckLatestReleaseAsync(CancellationToken cancellationToken = default)
    {
        Version current = GetCurrentVersion();

        try
        {
            using var response = await _httpClient.GetAsync(LatestReleaseUri, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return new UpdateCheckResult(false, current, null, null, null, LocalizationService.Format("Update_GitHubUnavailable", (int)response.StatusCode));
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var release = await JsonSerializer.DeserializeAsync<GitHubRelease>(stream, cancellationToken: cancellationToken);
            string? releasePageUrl = GetTrustedGitHubUrl(release?.HtmlUrl);
            if (release == null || !TryParseReleaseVersion(release.TagName, out Version latest))
            {
                return new UpdateCheckResult(false, current, null, releasePageUrl, null, LocalizationService.Get("Update_InvalidRelease"));
            }

            string? installerUrl = release.Assets?
                .FirstOrDefault(asset => asset.BrowserDownloadUrl?.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) == true)
                ?.BrowserDownloadUrl;
            installerUrl = GetTrustedGitHubUrl(installerUrl);

            if (releasePageUrl == null)
            {
                return new UpdateCheckResult(false, current, latest, null, installerUrl, LocalizationService.Get("Update_InvalidReleaseUrl"));
            }

            return new UpdateCheckResult(
                IsNewerVersion(latest, current),
                current,
                latest,
                releasePageUrl,
                installerUrl,
                null);
        }
        catch (HttpRequestException ex)
        {
            TelemetryService.CaptureException(ex, "update_check");
            return new UpdateCheckResult(false, current, null, null, null, LocalizationService.Get("Update_NetworkUnavailable"));
        }
        catch (TaskCanceledException ex)
        {
            TelemetryService.CaptureException(ex, "update_check");
            return new UpdateCheckResult(false, current, null, null, null, LocalizationService.Get("Update_Timeout"));
        }
        catch (JsonException ex)
        {
            TelemetryService.CaptureException(ex, "update_check");
            return new UpdateCheckResult(false, current, null, null, null, LocalizationService.Get("Update_InvalidResponse"));
        }
    }

    internal static bool TryParseReleaseVersion(string? tagName, out Version version)
    {
        version = new Version(0, 0, 0);
        if (string.IsNullOrWhiteSpace(tagName))
        {
            return false;
        }

        string normalized = tagName.Trim();
        if (normalized.StartsWith("v", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[1..];
        }

        int prereleaseIndex = normalized.IndexOf('-', StringComparison.Ordinal);
        if (prereleaseIndex >= 0)
        {
            normalized = normalized[..prereleaseIndex];
        }

        return Version.TryParse(normalized, out version!);
    }

    internal static bool IsNewerVersion(Version latest, Version current)
    {
        return Normalize(latest).CompareTo(Normalize(current)) > 0;
    }

    internal static Version Normalize(Version version)
    {
        return new Version(
            Math.Max(version.Major, 0),
            Math.Max(version.Minor, 0),
            Math.Max(version.Build, 0),
            Math.Max(version.Revision, 0));
    }

    internal static Version GetCurrentVersion()
    {
        var assembly = Assembly.GetExecutingAssembly();
        string? informationalVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (TryParseReleaseVersion(informationalVersion, out Version version))
        {
            return Normalize(version);
        }

        return Normalize(assembly.GetName().Version ?? new Version(0, 0, 0));
    }

    internal static string FormatVersion(Version? version)
    {
        if (version == null)
        {
            return "unknown";
        }

        var normalized = Normalize(version);
        return $"{normalized.Major}.{normalized.Minor}.{normalized.Build}";
    }

    public void OpenReleasePage(UpdateCheckResult result)
    {
        string target = GetTrustedGitHubUrl(result.ReleasePageUrl) ?? ReleasesFallbackUrl;
        _processStartDispatcher.Start(new ProcessStartInfo(target) { UseShellExecute = true });
    }

    internal static bool IsTrustedGitHubReleaseUrl(string? url)
    {
        return GetTrustedGitHubUrl(url) != null;
    }

    private static string? GetTrustedGitHubUrl(string? url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri))
        {
            return null;
        }

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(uri.Host, GitHubHost, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        string path = uri.AbsolutePath;
        if (!path.StartsWith(RepositoryPathPrefix, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(path.TrimEnd('/'), RepositoryPathPrefix.TrimEnd('/'), StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return uri.ToString();
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("AiteBar", FormatVersion(GetCurrentVersion())));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        return client;
    }

    private sealed class GitHubRelease
    {
        [JsonPropertyName("tag_name")]
        public string? TagName { get; set; }

        [JsonPropertyName("html_url")]
        public string? HtmlUrl { get; set; }

        [JsonPropertyName("assets")]
        public GitHubReleaseAsset[]? Assets { get; set; }
    }

    private sealed class GitHubReleaseAsset
    {
        [JsonPropertyName("browser_download_url")]
        public string? BrowserDownloadUrl { get; set; }
    }
}
