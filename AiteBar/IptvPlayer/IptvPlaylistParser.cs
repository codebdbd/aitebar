using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;

namespace AiteBar.IptvPlayer;

public static class IptvPlaylistParser
{
    private static readonly HashSet<string> AllowedRemoteSchemes = new(StringComparer.OrdinalIgnoreCase)
    {
        "http", "https", "rtsp", "rtsps", "rtp", "udp", "mms", "mmsh"
    };

    private static readonly Regex AttributeRegex = new(
        @"(?<key>tvg-id|tvg-name|tvg-logo|group-title)=[""'](?<val>.*?)[""']",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex UnquotedAttributeRegex = new(
        @"(?<key>tvg-id|tvg-name|tvg-logo|group-title)=(?<val>[^\s,""']+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ChannelQualitySuffixRegex = new(
        @"\s*\[?(?:FHD|HD|SD|4K|8K|HEVC|H\.?264|H\.?265|RAW)\]?\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static bool IsHlsManifestContent(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return false;
        }

        return content.Contains("#EXT-X-STREAM-INF", StringComparison.OrdinalIgnoreCase) ||
               content.Contains("#EXT-X-TARGETDURATION", StringComparison.OrdinalIgnoreCase) ||
               content.Contains("#EXT-X-MEDIA-SEQUENCE", StringComparison.OrdinalIgnoreCase);
    }

    public static (List<IptvCachedChannelEntry> Channels, int SkippedCount) ParseM3u(
        string content,
        IptvSourceKind sourceKind,
        string? baseLocation = null)
    {
        var channels = new List<IptvCachedChannelEntry>();
        int skippedCount = 0;

        if (string.IsNullOrWhiteSpace(content))
        {
            return (channels, 0);
        }

        // Если это HLS манифест одного стрима, возвращаем его как 1 стрим
        if (IsHlsManifestContent(content))
        {
            string streamUrl = baseLocation ?? string.Empty;
            if (IsValidStreamUrl(streamUrl, sourceKind))
            {
                channels.Add(new IptvCachedChannelEntry
                {
                    Title = CleanChannelTitle(Path.GetFileNameWithoutExtension(streamUrl) ?? "HLS Stream"),
                    Group = "Общие",
                    StreamUrl = streamUrl,
                    SortOrder = 0
                });
                return (channels, 0);
            }
        }

        using var reader = new StringReader(content);
        string? line;
        string? currentExtInf = null;
        string? currentExtGrp = null;
        int order = 0;

        while ((line = reader.ReadLine()) != null)
        {
            line = line.Trim();
            if (line.Length == 0)
            {
                continue;
            }

            if (line.StartsWith("#EXTM3U", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (line.StartsWith("#EXTINF:", StringComparison.OrdinalIgnoreCase))
            {
                currentExtInf = line;
                continue;
            }

            if (line.StartsWith("#EXTGRP:", StringComparison.OrdinalIgnoreCase))
            {
                currentExtGrp = line[8..].Trim();
                continue;
            }

            if (line.StartsWith("#", StringComparison.Ordinal))
            {
                // прочие комментарии или метаданные
                continue;
            }

            // Это строка URL/пути стрима
            string rawUrl = line;
            string resolvedUrl = ResolveUrl(rawUrl, baseLocation);

            if (!IsValidStreamUrl(resolvedUrl, sourceKind))
            {
                skippedCount++;
                currentExtInf = null;
                currentExtGrp = null;
                continue;
            }

            var entry = ExtractChannelMetadata(currentExtInf, currentExtGrp, resolvedUrl, order++);
            channels.Add(entry);

            currentExtInf = null;
            currentExtGrp = null;
        }

        return (channels, skippedCount);
    }

    public static IptvCachedChannelEntry ExtractChannelMetadata(
        string? extInf,
        string? extGrp,
        string streamUrl,
        int order)
    {
        string title = string.Empty;
        string? group = null;
        string? logoUrl = null;
        string? tvgId = null;
        string? tvgName = null;

        if (!string.IsNullOrWhiteSpace(extInf))
        {
            // Парсинг атрибутов tvg-id, tvg-name, tvg-logo, group-title
            foreach (Match m in AttributeRegex.Matches(extInf))
            {
                string key = m.Groups["key"].Value.ToLowerInvariant();
                string val = m.Groups["val"].Value.Trim();
                switch (key)
                {
                    case "tvg-id": tvgId = val; break;
                    case "tvg-name": tvgName = val; break;
                    case "tvg-logo": logoUrl = val; break;
                    case "group-title": group = val; break;
                }
            }

            foreach (Match m in UnquotedAttributeRegex.Matches(extInf))
            {
                string key = m.Groups["key"].Value.ToLowerInvariant();
                string val = m.Groups["val"].Value.Trim();
                switch (key)
                {
                    case "tvg-id": tvgId ??= val; break;
                    case "tvg-name": tvgName ??= val; break;
                    case "tvg-logo": logoUrl ??= val; break;
                    case "group-title": group ??= val; break;
                }
            }

            int commaIndex = extInf.IndexOf(',');
            if (commaIndex >= 0 && commaIndex < extInf.Length - 1)
            {
                title = extInf[(commaIndex + 1)..].Trim();
            }
        }

        if (!string.IsNullOrWhiteSpace(extGrp))
        {
            group = extGrp.Trim();
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            title = !string.IsNullOrWhiteSpace(tvgName)
                ? tvgName
                : Path.GetFileNameWithoutExtension(streamUrl);
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            title = $"Channel {order + 1}";
        }

        if (string.IsNullOrWhiteSpace(group))
        {
            group = "Общие";
        }

        return new IptvCachedChannelEntry
        {
            Title = title,
            Group = group,
            LogoUrl = string.IsNullOrWhiteSpace(logoUrl) ? null : logoUrl,
            TvgId = string.IsNullOrWhiteSpace(tvgId) ? null : tvgId,
            TvgName = string.IsNullOrWhiteSpace(tvgName) ? null : tvgName,
            StreamUrl = streamUrl,
            SortOrder = order
        };
    }

    public static string CleanChannelTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return string.Empty;
        }

        string cleaned = ChannelQualitySuffixRegex.Replace(title.Trim(), string.Empty).Trim();
        return cleaned.Length > 0 ? cleaned : title.Trim();
    }

    public static bool IsValidStreamUrl(string url, IptvSourceKind sourceKind)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return false;
        }

        // Защита от передачи CLI аргументов и скриптов в плеер
        if (url.StartsWith("-", StringComparison.Ordinal) ||
            url.StartsWith("/", StringComparison.Ordinal) && !Path.IsPathRooted(url))
        {
            return false;
        }

        if (Uri.TryCreate(url, UriKind.Absolute, out Uri? uri))
        {
            if (AllowedRemoteSchemes.Contains(uri.Scheme))
            {
                return true;
            }

            if (string.Equals(uri.Scheme, Uri.UriSchemeFile, StringComparison.OrdinalIgnoreCase))
            {
                // file:// разрешен только для локальных файлов
                return sourceKind == IptvSourceKind.LocalFile;
            }

            return false;
        }

        // Если это локальный путь Windows без file:// (например D:\video\stream.mkv)
        if (sourceKind == IptvSourceKind.LocalFile && Path.IsPathRooted(url))
        {
            return true;
        }

        return false;
    }

    public static string ResolveUrl(string relativeOrAbsolute, string? baseLocation)
    {
        if (string.IsNullOrWhiteSpace(relativeOrAbsolute))
        {
            return string.Empty;
        }

        if (Uri.TryCreate(relativeOrAbsolute, UriKind.Absolute, out _))
        {
            return relativeOrAbsolute;
        }

        if (Path.IsPathRooted(relativeOrAbsolute))
        {
            return relativeOrAbsolute;
        }

        if (!string.IsNullOrWhiteSpace(baseLocation))
        {
            if (Uri.TryCreate(baseLocation, UriKind.Absolute, out Uri? baseUri))
            {
                if (Uri.TryCreate(baseUri, relativeOrAbsolute, out Uri? combinedUri))
                {
                    return combinedUri.ToString();
                }
            }
            else if (Path.IsPathRooted(baseLocation))
            {
                string? dir = Path.GetDirectoryName(baseLocation);
                if (!string.IsNullOrWhiteSpace(dir))
                {
                    return Path.GetFullPath(Path.Combine(dir, relativeOrAbsolute));
                }
            }
        }

        return relativeOrAbsolute;
    }

    public static bool IsSensitiveUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return false;
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri))
        {
            return false;
        }

        if (!string.IsNullOrEmpty(uri.UserInfo))
        {
            return true;
        }

        string query = uri.Query;
        if (!string.IsNullOrEmpty(query))
        {
            if (query.Contains("token=", StringComparison.OrdinalIgnoreCase) ||
                query.Contains("key=", StringComparison.OrdinalIgnoreCase) ||
                query.Contains("auth=", StringComparison.OrdinalIgnoreCase) ||
                query.Contains("pass=", StringComparison.OrdinalIgnoreCase) ||
                query.Contains("password=", StringComparison.OrdinalIgnoreCase) ||
                query.Contains("secret=", StringComparison.OrdinalIgnoreCase) ||
                query.Contains("apikey=", StringComparison.OrdinalIgnoreCase) ||
                query.Contains("api_key=", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public static string MaskSensitiveUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return string.Empty;
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri))
        {
            return url;
        }

        var builder = new UriBuilder(uri);
        if (!string.IsNullOrEmpty(builder.UserName) || !string.IsNullOrEmpty(builder.Password))
        {
            builder.UserName = "***";
            builder.Password = "***";
        }

        if (!string.IsNullOrEmpty(builder.Query))
        {
            string q = builder.Query.TrimStart('?');
            string[] parts = q.Split('&');
            for (int i = 0; i < parts.Length; i++)
            {
                int eq = parts[i].IndexOf('=');
                if (eq > 0)
                {
                    string k = parts[i][..eq];
                    if (k.Equals("token", StringComparison.OrdinalIgnoreCase) ||
                        k.Equals("key", StringComparison.OrdinalIgnoreCase) ||
                        k.Equals("auth", StringComparison.OrdinalIgnoreCase) ||
                        k.Equals("pass", StringComparison.OrdinalIgnoreCase) ||
                        k.Equals("password", StringComparison.OrdinalIgnoreCase) ||
                        k.Equals("secret", StringComparison.OrdinalIgnoreCase) ||
                        k.Equals("apikey", StringComparison.OrdinalIgnoreCase) ||
                        k.Equals("api_key", StringComparison.OrdinalIgnoreCase))
                    {
                        parts[i] = $"{k}=***";
                    }
                }
            }
            builder.Query = string.Join("&", parts);
        }

        return builder.Uri.ToString();
    }
}
