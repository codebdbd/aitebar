using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.Versioning;

namespace AiteBar.IptvPlayer;

[SupportedOSPlatform("windows6.1")]
public sealed class IptvPlaylistService : IDisposable
{
    private const int MaxResponseBytes = 10 * 1024 * 1024; // 10 MiB
    private const int RequestTimeoutSeconds = 25;
    private const int MaxRedirectHops = 5;

    private readonly IptvPlayerStore _store;
    private readonly HttpClient _httpClient;
    private static readonly SemaphoreSlim RefreshGate = new(1, 1);
    private bool _disposed;

    public IptvPlaylistService(IptvPlayerStore store, HttpMessageHandler? handler = null)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));

        var clientHandler = handler ?? new HttpClientHandler
        {
            AllowAutoRedirect = false, // ручная проверка редиректов для безопасности
            AutomaticDecompression = DecompressionMethods.All
        };

        _httpClient = new HttpClient(clientHandler)
        {
            Timeout = TimeSpan.FromSeconds(RequestTimeoutSeconds)
        };
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("AiteBar-IPTV/1.0");
    }

    public Task<IptvSourceCheckResult> CheckSourceAsync(IptvPlaylistSource source, CancellationToken cancellationToken = default)
        => CheckSourceAsync(source, null, cancellationToken);

    internal async Task<IptvSourceCheckResult> CheckSourceAsync(
        IptvPlaylistSource source, string? locationOverride, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var (channels, _, _) = await ReadSourceAsync(source, locationOverride, cancellationToken);
            return new IptvSourceCheckResult
            {
                IsSuccess = true,
                TotalCount = channels.Count,
                FirstChannels = channels.Take(5).Select(channel => channel.Title).ToList(),
                ElapsedMs = stopwatch.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            return new IptvSourceCheckResult
            {
                ErrorMessage = ex is OperationCanceledException ? "Operation canceled or timed out" : ex.Message,
                ElapsedMs = stopwatch.ElapsedMilliseconds
            };
        }
    }

    public Task<bool> RefreshSourceAsync(IptvPlaylistSource source, CancellationToken cancellationToken = default)
        => RefreshSourceAsync(source, null, cancellationToken);

    internal async Task<bool> RefreshSourceAsync(
        IptvPlaylistSource source, string? locationOverride, CancellationToken cancellationToken = default)
    {
        if (source == null || string.IsNullOrWhiteSpace(source.Id))
        {
            return false;
        }

        try
        {
            await RefreshGate.WaitAsync(cancellationToken);
            try
            {
                var (channels, skipped, sourceCopy) = await ReadSourceAsync(source, locationOverride, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                if (sourceCopy is not null)
                {
                    _store.SaveSourceFileContent(source.SourceStorageId, sourceCopy);
                }

                var refreshedAt = DateTime.UtcNow;
                _store.SaveCache(new IptvPlaylistCache
                {
                    PlaylistId = source.Id,
                    SourceHash = source.ContentRevision,
                    RefreshedAtUtc = refreshedAt,
                    Channels = channels,
                    InvalidEntryCount = skipped
                });

                source.LastRefreshedUtc = refreshedAt;
                source.LastStatus = "Ok";
                source.LastErrorMessage = null;
                source.ChannelCount = channels.Count;
                return true;
            }
            finally
            {
                RefreshGate.Release();
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return false;
        }
        catch (Exception ex)
        {
            source.LastStatus = "Error";
            source.LastErrorMessage = ex is OperationCanceledException ? "Operation timed out" : ex.Message;
            return false;
        }
    }

    private async Task<(List<IptvCachedChannelEntry> Channels, int Skipped, string? SourceCopy)> ReadSourceAsync(
        IptvPlaylistSource source, string? locationOverride, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(RequestTimeoutSeconds));
        CancellationToken token = timeout.Token;
        string? baseLocation = null;
        string? sourceCopy = null;
        string content;

        switch (source.Kind)
        {
            case IptvSourceKind.SingleStream:
                string streamUrl = locationOverride ?? GetEffectiveUrl(source);
                if (!IptvPlaylistParser.IsValidStreamUrl(streamUrl, source.Kind))
                {
                    throw new InvalidDataException("Invalid stream URL");
                }

                return ([new IptvCachedChannelEntry
                {
                    Title = string.IsNullOrWhiteSpace(source.Name) ? "Single Stream" : source.Name,
                    Group = "Общие",
                    StreamUrl = streamUrl
                }], 0, null);

            case IptvSourceKind.Url:
                baseLocation = locationOverride ?? GetEffectiveUrl(source);
                if (!Uri.TryCreate(baseLocation, UriKind.Absolute, out Uri? uri) ||
                    (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                {
                    throw new InvalidDataException("Invalid HTTP/HTTPS URL");
                }

                (content, baseLocation) = await DownloadStringHardenedAsync(baseLocation, token);
                break;

            case IptvSourceKind.LocalFile:
                baseLocation = source.Location;
                if (File.Exists(baseLocation))
                {
                    if (new FileInfo(baseLocation).Length > MaxResponseBytes)
                    {
                        throw new InvalidDataException("Playlist file exceeds size limit");
                    }

                    content = await File.ReadAllTextAsync(baseLocation, Encoding.UTF8, token);
                    sourceCopy = source.CopyLocalFileToStore ? content : null;
                }
                else
                {
                    content = _store.ReadSourceFileContent(source.SourceStorageId) ?? throw new FileNotFoundException("Local playlist file not found");
                }
                break;

            case IptvSourceKind.InlineText:
                content = source.InlineContent ?? _store.ReadSourceFileContent(source.SourceStorageId) ?? string.Empty;
                break;

            default:
                throw new InvalidDataException("Unknown playlist source kind");
        }

        var (channels, skipped) = IptvPlaylistParser.ParseM3u(content, source.Kind, baseLocation);
        if (channels.Count == 0)
        {
            throw new InvalidDataException("No valid channels found in playlist");
        }

        return (channels, skipped, sourceCopy);
    }

    public bool IsAutoUpdateDue(IptvSettings settings)
    {
        if (!settings.AutoUpdateEnabled)
        {
            return false;
        }

        int interval = Math.Max(1, settings.UpdateIntervalHours);
        return !settings.LastAutoUpdateUtc.HasValue ||
            (DateTime.UtcNow - settings.LastAutoUpdateUtc.Value).TotalHours >= interval;
    }

    private string GetEffectiveUrl(IptvPlaylistSource source)
    {
        if (IptvPlaylistParser.IsMaskedSensitiveUrl(source.Location))
        {
            return _store.ReadSecretLocation(source.SourceStorageId)
                ?? throw new InvalidDataException("Playlist credentials are missing. Edit the source and enter the complete URL.");
        }

        return source.Location ?? string.Empty;
    }

    private async Task<(string Content, string Location)> DownloadStringHardenedAsync(string initialUrl, CancellationToken cancellationToken)
    {
        var (bytes, location, charset) = await DownloadRemoteBytesAsync(
            _httpClient, initialUrl, MaxResponseBytes, cancellationToken);
        return (DetectEncoding(bytes, charset).GetString(bytes), location);
    }

    internal static async Task<(byte[] Bytes, string Location, string? Charset)> DownloadRemoteBytesAsync(
        HttpClient client, string initialUrl, int maxBytes, CancellationToken cancellationToken)
    {
        string currentUrl = initialUrl;
        int hops = 0;

        while (hops < MaxRedirectHops)
        {
            if (!Uri.TryCreate(currentUrl, UriKind.Absolute, out Uri? uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                throw new InvalidOperationException("Invalid URI during download.");
            }

            await ValidateDestinationSecurityAsync(uri, cancellationToken);

            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            if ((int)response.StatusCode >= 300 && (int)response.StatusCode <= 399)
            {
                Uri? location = response.Headers.Location;
                if (location == null)
                {
                    throw new InvalidOperationException("Redirect response without Location header.");
                }

                if (!location.IsAbsoluteUri)
                {
                    location = new Uri(uri, location);
                }

                currentUrl = location.ToString();
                hops++;
                continue;
            }

            response.EnsureSuccessStatusCode();

            long? contentLength = response.Content.Headers.ContentLength;
            if (contentLength > maxBytes)
            {
                throw new InvalidOperationException($"Download exceeds the limit of {maxBytes / 1024 / 1024} MiB.");
            }

            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var ms = new MemoryStream();
            byte[] buffer = new byte[8192];
            int read;
            long totalRead = 0;

            while ((read = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
            {
                totalRead += read;
                if (totalRead > maxBytes)
                {
                    throw new InvalidOperationException($"Download exceeds the limit of {maxBytes / 1024 / 1024} MiB.");
                }
                ms.Write(buffer, 0, read);
            }

            return (ms.ToArray(), currentUrl, response.Content.Headers.ContentType?.CharSet);
        }

        throw new InvalidOperationException("Too many redirects.");
    }

    private static async Task ValidateDestinationSecurityAsync(Uri uri, CancellationToken cancellationToken)
    {
        if (uri.IsLoopback)
        {
            throw new InvalidOperationException("Loopback addresses are not permitted for remote playlists.");
        }

        IPAddress[] addresses;
        try
        {
            addresses = await Dns.GetHostAddressesAsync(uri.DnsSafeHost, cancellationToken);
        }
        catch (SocketException)
        {
            // Если DNS не резолвится, оставляем HttpClient обработать ошибку сети
            return;
        }

        foreach (var ip in addresses)
        {
            if (IPAddress.IsLoopback(ip) || IsPrivateOrReservedIp(ip))
            {
                throw new InvalidOperationException($"Connecting to private or internal address {ip} is blocked for remote playlists.");
            }
        }
    }

    private static bool IsPrivateOrReservedIp(IPAddress ip)
    {
        if (ip.AddressFamily == AddressFamily.InterNetwork)
        {
            byte[] bytes = ip.GetAddressBytes();
            // 10.0.0.0/8
            if (bytes[0] == 10) return true;
            // 172.16.0.0/12
            if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) return true;
            // 192.168.0.0/16
            if (bytes[0] == 192 && bytes[1] == 168) return true;
            // 127.0.0.0/8 (loopback)
            if (bytes[0] == 127) return true;
            // 169.254.0.0/16 (link local)
            if (bytes[0] == 169 && bytes[1] == 254) return true;
        }
        else if (ip.AddressFamily == AddressFamily.InterNetworkV6)
        {
            if (ip.IsIPv6SiteLocal || ip.IsIPv6LinkLocal || IPAddress.IPv6Loopback.Equals(ip))
            {
                return true;
            }
        }

        return false;
    }

    private static Encoding DetectEncoding(byte[] bytes, string? charsetHeader)
    {
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
        {
            return Encoding.UTF8;
        }

        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
        {
            return Encoding.Unicode;
        }

        if (!string.IsNullOrWhiteSpace(charsetHeader))
        {
            try
            {
                return Encoding.GetEncoding(charsetHeader);
            }
            catch
            {
                // ignore
            }
        }

        return Encoding.UTF8;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _httpClient.Dispose();
        }
    }
}
