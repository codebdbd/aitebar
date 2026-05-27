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
    private static readonly Uri LatestReleaseUri = new(LatestReleaseApiUrl);
    private readonly HttpClient _httpClient;

    public UpdateCheckService()
        : this(CreateHttpClient())
    {
    }

    internal UpdateCheckService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<UpdateCheckResult> CheckLatestReleaseAsync(CancellationToken cancellationToken = default)
    {
        Version current = GetCurrentVersion();

        try
        {
            using var response = await _httpClient.GetAsync(LatestReleaseUri, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return new UpdateCheckResult(false, current, null, null, null, $"GitHub returned {(int)response.StatusCode} {response.ReasonPhrase}.");
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var release = await JsonSerializer.DeserializeAsync<GitHubRelease>(stream, cancellationToken: cancellationToken);
            if (release == null || !TryParseReleaseVersion(release.TagName, out Version latest))
            {
                return new UpdateCheckResult(false, current, null, release?.HtmlUrl, null, LocalizationService.Get("Update_InvalidRelease"));
            }

            string? installerUrl = release.Assets?
                .FirstOrDefault(asset => asset.BrowserDownloadUrl?.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) == true)
                ?.BrowserDownloadUrl;

            return new UpdateCheckResult(
                IsNewerVersion(latest, current),
                current,
                latest,
                release.HtmlUrl,
                installerUrl,
                null);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException)
        {
            TelemetryService.CaptureException(ex, "update_check");
            return new UpdateCheckResult(false, current, null, null, null, ex.Message);
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
        string target = result.ReleasePageUrl ?? "https://github.com/codebdbd/aitebar/releases";
        Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
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
