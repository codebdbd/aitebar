using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AiteBar.AiteProfilesUtility;

internal sealed class AiteProfilesQuickLinkService
{
    private readonly string _snippetsPath;
    private readonly string _lastLaunchPath;
    private readonly object _sync = new();
    private AiteProfileSnippet? _activeSnippet;
    private string _preparedText = string.Empty;
    private bool _rememberEnabled;

    public AiteProfilesQuickLinkService(string rootDirectory)
    {
        _snippetsPath = Path.Combine(rootDirectory, "snippets.json");
        _lastLaunchPath = Path.Combine(rootDirectory, "terminal_last_launch.json");
    }

    public async Task<IReadOnlyList<AiteProfileSnippet>> LoadAsync(CancellationToken cancellationToken = default)
    {
        var data = await AiteProfilesJsonStore.ReadAsync<List<AiteProfileSnippet>>(_snippetsPath, cancellationToken).ConfigureAwait(false);
        return NormalizeSnippets(data ?? []);
    }

    public Task SaveAsync(IReadOnlyList<AiteProfileSnippet> snippets, CancellationToken cancellationToken = default) =>
        AiteProfilesJsonStore.WriteAsync(_snippetsPath, NormalizeSnippets(snippets), cancellationToken);

    public AiteProfileSnippet? GetActiveSnippet()
    {
        lock (_sync)
        {
            return _activeSnippet?.Clone();
        }
    }

    public void SetActiveSnippet(AiteProfileSnippet? snippet)
    {
        lock (_sync)
        {
            _activeSnippet = snippet?.Clone();
        }
    }

    public void UpdatePreparedText(string preparedText)
    {
        lock (_sync)
        {
            _preparedText = (preparedText ?? string.Empty).Trim();
        }
    }

    public string GetPreparedText()
    {
        lock (_sync)
        {
            return _preparedText;
        }
    }

    public void SetRememberEnabled(bool rememberEnabled)
    {
        lock (_sync)
        {
            _rememberEnabled = rememberEnabled;
        }
    }

    public bool GetRememberEnabled()
    {
        lock (_sync)
        {
            return _rememberEnabled;
        }
    }

    public async Task<string> MarkLaunchedAsync(AiteProfileSnippet snippet, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snippet);
        string launchText = string.Join('|', snippet.Urls);
        await AiteProfilesJsonStore.WriteAsync(_lastLaunchPath, new LastLaunchRecord
        {
            Text = launchText,
            Urls = [.. snippet.Urls]
        }, cancellationToken).ConfigureAwait(false);

        lock (_sync)
        {
            if (_rememberEnabled)
            {
                if (string.IsNullOrWhiteSpace(_preparedText))
                {
                    _preparedText = launchText;
                }

                return _preparedText;
            }

            _preparedText = string.Empty;
            _activeSnippet = null;
            return string.Empty;
        }
    }

    public bool TryParseCommand(string input, out AiteProfileSnippet snippet)
    {
        snippet = new AiteProfileSnippet();
        string raw = NormalizePart(input);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        string[] parts = raw.Split(':', 3, StringSplitOptions.None);
        if (parts.Length != 3)
        {
            return false;
        }

        List<string> tags = ParseTags(parts[0]);
        string name = NormalizePart(parts[1]);
        List<string> urls = ParseUrls(parts[2]);
        if (tags.Count == 0 || string.IsNullOrWhiteSpace(name) || urls.Count == 0)
        {
            return false;
        }

        snippet = new AiteProfileSnippet
        {
            Name = name,
            Tags = tags,
            Urls = urls
        };
        return true;
    }

    public bool TryParseDirectUrls(string input, out List<string> urls)
    {
        urls = ParseUrls(input);
        return urls.Count > 0;
    }

    public bool TryResolveSnippet(
        string candidate,
        AiteProfileSnippet? chosenSnippet,
        IReadOnlyList<AiteProfileSnippet> snippets,
        out AiteProfileSnippet snippet,
        out bool shouldSaveToDatabase)
    {
        snippet = new AiteProfileSnippet();
        shouldSaveToDatabase = false;

        if (chosenSnippet is not null)
        {
            snippet = chosenSnippet.Clone();
            return true;
        }

        string normalized = NormalizePart(candidate);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        if (TryParseCommand(normalized, out snippet))
        {
            shouldSaveToDatabase = true;
            return true;
        }

        if (TryParseDirectUrls(normalized, out List<string> urls))
        {
            snippet = new AiteProfileSnippet
            {
                Name = urls.Count == 1 ? "direct" : "group",
                Tags = [],
                Urls = urls
            };
            return true;
        }

        AiteProfileSnippet? fallback = RankSnippets(snippets, normalized).FirstOrDefault();
        if (fallback is null)
        {
            return false;
        }

        snippet = fallback.Clone();
        return true;
    }

    public bool TryNormalizeUrl(string rawInput, out string normalizedUrl) =>
        TryNormalizeAndValidateUrl(rawInput, out normalizedUrl);

    public static bool TryNormalizeUrlInput(string rawInput, out string normalizedUrl) =>
        TryNormalizeAndValidateUrl(rawInput, out normalizedUrl);

    public IReadOnlyList<AiteProfileSnippet> ParseImportLines(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return [];
        }

        var parsed = new List<AiteProfileSnippet>();
        foreach (string line in content.Split(["\r\n", "\n"], StringSplitOptions.None))
        {
            if (TryParseCommand(line, out AiteProfileSnippet snippet))
            {
                parsed.Add(snippet);
            }
        }

        return NormalizeSnippets(parsed);
    }

    public string BuildTextExport(IReadOnlyList<AiteProfileSnippet> snippets)
    {
        var sb = new StringBuilder();
        foreach (AiteProfileSnippet snippet in NormalizeSnippets(snippets))
        {
            string tag = snippet.Tags.Count > 0 ? snippet.Tags[0] : "misc";
            sb.Append(tag);
            sb.Append(':');
            sb.Append(snippet.Name);
            sb.Append(':');
            sb.Append(string.Join('|', snippet.Urls));
            sb.AppendLine();
        }

        return sb.ToString();
    }

    public string BuildJsonExport(IReadOnlyList<AiteProfileSnippet> snippets) =>
        System.Text.Json.JsonSerializer.Serialize(NormalizeSnippets(snippets), AiteProfilesJsonStore.Options);

    public IReadOnlyList<AiteProfileSnippet> RankSnippets(IEnumerable<AiteProfileSnippet> snippets, string query)
    {
        string normalizedQuery = NormalizePart(query).ToLowerInvariant();
        var normalizedSnippets = NormalizeSnippets(snippets);
        var emitted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(normalizedQuery))
        {
            return normalizedSnippets
                .OrderBy(static snippet => snippet.Name, StringComparer.OrdinalIgnoreCase)
                .Where(snippet => emitted.Add(BuildSnippetKey(snippet)))
                .Select(static snippet => snippet.Clone())
                .ToList();
        }

        var ranked = new List<(AiteProfileSnippet Snippet, int Rank)>();
        foreach (AiteProfileSnippet snippet in normalizedSnippets)
        {
            bool tagHit = snippet.Tags.Any(tag => tag.Contains(normalizedQuery, StringComparison.Ordinal));
            bool nameHit = snippet.Name.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase);
            bool urlHit = snippet.Urls.Any(url => url.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase));

            if (tagHit)
            {
                ranked.Add((snippet, 0));
                continue;
            }

            if (nameHit)
            {
                ranked.Add((snippet, 1));
                continue;
            }

            if (urlHit)
            {
                ranked.Add((snippet, 2));
            }
        }

        return ranked
            .OrderBy(static item => item.Rank)
            .ThenBy(static item => item.Snippet.Name, StringComparer.OrdinalIgnoreCase)
            .Where(item => emitted.Add(BuildSnippetKey(item.Snippet)))
            .Select(static item => item.Snippet.Clone())
            .ToList();
    }

    public static IReadOnlyList<AiteProfileSnippet> NormalizeSnippets(IEnumerable<AiteProfileSnippet> snippets)
    {
        var map = new Dictionary<string, AiteProfileSnippet>(StringComparer.OrdinalIgnoreCase);
        foreach (AiteProfileSnippet item in snippets)
        {
            string name = NormalizePart(item.Name);
            List<string> tags = ParseTags(string.Join(',', item.Tags ?? []));
            List<string> urls = ParseUrls(string.Join('|', item.Urls ?? []));
            if (string.IsNullOrWhiteSpace(name) || urls.Count == 0)
            {
                continue;
            }

            if (tags.Count == 0)
            {
                tags = ["misc"];
            }

            var normalized = new AiteProfileSnippet
            {
                Name = name,
                Tags = tags,
                Urls = urls
            };
            map[$"{normalized.Name}|{string.Join('|', normalized.Urls)}"] = normalized;
        }

        return map.Values.OrderBy(static x => x.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static List<string> ParseTags(string value) =>
        (value ?? string.Empty)
            .Split([',', ';', '|', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(NormalizePart)
            .Where(static x => !string.IsNullOrWhiteSpace(x))
            .Select(static x => x.ToLowerInvariant())
            .Distinct(StringComparer.Ordinal)
            .ToList();

    private static List<string> ParseUrls(string value)
    {
        var list = new List<string>();
        foreach (string part in (value ?? string.Empty).Split('|', StringSplitOptions.RemoveEmptyEntries))
        {
            if (TryNormalizeAndValidateUrl(part, out string normalizedUrl))
            {
                list.Add(normalizedUrl);
            }
        }

        return list.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static bool TryNormalizeAndValidateUrl(string rawInput, out string normalizedUrl)
    {
        normalizedUrl = string.Empty;
        string candidate = NormalizePart(rawInput);
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return false;
        }

        if (!candidate.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !candidate.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            candidate = $"https://{candidate}";
        }

        if (!Uri.TryCreate(candidate, UriKind.Absolute, out Uri? uri))
        {
            return false;
        }

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string host = uri.Host ?? string.Empty;
        if (string.IsNullOrWhiteSpace(host) || (!string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase) && !IPAddress.TryParse(host, out _) && !host.Contains('.', StringComparison.Ordinal)))
        {
            return false;
        }

        normalizedUrl = uri.ToString();
        return true;
    }

    private static string NormalizePart(string value) => (value ?? string.Empty).Trim(' ', '\t', '\r', '\n');

    private static string BuildSnippetKey(AiteProfileSnippet snippet) =>
        $"{snippet.Name}|{string.Join('|', snippet.Urls)}";

    private sealed class LastLaunchRecord
    {
        public string Text { get; set; } = string.Empty;
        public List<string> Urls { get; set; } = [];
    }
}
