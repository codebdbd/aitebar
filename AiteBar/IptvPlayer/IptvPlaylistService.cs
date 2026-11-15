using System;
using System.Collections.Concurrent;
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
    private readonly ConcurrentDictionary<string, (SemaphoreSlim Lock, CancellationTokenSource? Cts, long Generation)> _sourceLocks = new();
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

    public async Task<IptvSourceCheckResult> CheckSourceAsync(IptvPlaylistSource source, CancellationToken cancellationToken = default)
    {
        if (source == null)
        {
            return new IptvSourceCheckResult { IsSuccess = false, ErrorMessage = "Source is null" };
        }

        var sw = Stopwatch.StartNew();
        try
        {
            string content;
            string? baseLocation = null;

            switch (source.Kind)
            {
                case IptvSourceKind.Url:
                    string effectiveUrl = GetEffectiveUrl(source);
                    if (!Uri.TryCreate(effectiveUrl, UriKind.Absolute, out Uri? uri) ||
                        (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                    {
                        return new IptvSourceCheckResult
                        {
                            IsSuccess = false,
                            ErrorMessage = "Invalid HTTP/HTTPS URL",
                            ElapsedMs = sw.ElapsedMilliseconds
                        };
                    }

                    baseLocation = effectiveUrl;
                    content = await DownloadStringHardenedAsync(effectiveUrl, cancellationToken);
                    break;

                case IptvSourceKind.LocalFile:
                    string filePath = source.Location;
                    if (!File.Exists(filePath))
                    {
                        // Проверяем внутреннюю копию
                        string storePath = _store.GetSourceFilePath(source.Id);
                        if (File.Exists(storePath))
                        {
                            filePath = storePath;
                        }
                    }

                    if (!File.Exists(filePath))
                    {
                        return new IptvSourceCheckResult
                        {
                            IsSuccess = false,
                            ErrorMessage = "Local file not found",
                            ElapsedMs = sw.ElapsedMilliseconds
                        };
                    }

                    baseLocation = filePath;
                    content = await File.ReadAllTextAsync(filePath, Encoding.UTF8, cancellationToken);
                    break;

                case IptvSourceKind.InlineText:
                    content = source.InlineContent ?? string.Empty;
                    break;

                case IptvSourceKind.SingleStream:
                    string streamUrl = GetEffectiveUrl(source);
                    if (!IptvPlaylistParser.IsValidStreamUrl(streamUrl, IptvSourceKind.SingleStream))
                    {
                        return new IptvSourceCheckResult
                        {
                            IsSuccess = false,
                            ErrorMessage = "Invalid stream URL",
                            ElapsedMs = sw.ElapsedMilliseconds
                        };
                    }

                    sw.Stop();
                    return new IptvSourceCheckResult
                    {
                        IsSuccess = true,
                        TotalCount = 1,
                        FirstChannels = [string.IsNullOrWhiteSpace(source.Name) ? streamUrl : source.Name],
                        ElapsedMs = sw.ElapsedMilliseconds
                    };

                default:
                    return new IptvSourceCheckResult
                    {
                        IsSuccess = false,
                        ErrorMessage = "Unknown source kind",
                        ElapsedMs = sw.ElapsedMilliseconds
                    };
            }

            var (channels, _) = IptvPlaylistParser.ParseM3u(content, source.Kind, baseLocation);
            sw.Stop();

            if (channels.Count == 0)
            {
                return new IptvSourceCheckResult
                {
                    IsSuccess = false,
                    ErrorMessage = "No valid channels found in playlist",
                    ElapsedMs = sw.ElapsedMilliseconds
                };
            }

            return new IptvSourceCheckResult
            {
                IsSuccess = true,
                TotalCount = channels.Count,
                FirstChannels = channels.Take(5).Select(c => c.Title).ToList(),
                ElapsedMs = sw.ElapsedMilliseconds
            };
        }
        catch (OperationCanceledException)
        {
            sw.Stop();
            return new IptvSourceCheckResult
            {
                IsSuccess = false,
                ErrorMessage = "Operation canceled or timed out",
                ElapsedMs = sw.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new IptvSourceCheckResult
            {
                IsSuccess = false,
                ErrorMessage = ex.Message,
                ElapsedMs = sw.ElapsedMilliseconds
            };
        }
    }

    public async Task<bool> RefreshSourceAsync(IptvPlaylistSource source, CancellationToken callerToken = default)
    {
        if (source == null || string.IsNullOrWhiteSpace(source.Id))
        {
            return false;
        }

        var entry = _sourceLocks.GetOrAdd(source.Id, id => (new SemaphoreSlim(1, 1), null, 0));
        entry.Cts?.Cancel();
        var myCts = CancellationTokenSource.CreateLinkedTokenSource(callerToken);
        long currentGen = unchecked(entry.Generation + 1);
        _sourceLocks[source.Id] = (entry.Lock, myCts, currentGen);

        await entry.Lock.WaitAsync(callerToken);
        try
        {
            // Проверяем, не вытеснила ли нас более новая операция
            if (_sourceLocks.TryGetValue(source.Id, out var active) && active.Generation != currentGen)
            {
                return false;
            }

            var result = await CheckSourceAsync(source, myCts.Token);
            if (!result.IsSuccess)
            {
                source.LastStatus = "Error";
                source.LastErrorMessage = result.ErrorMessage;
                return false;
            }

            // Получаем контент и парсим в кэш
            string content = string.Empty;
            string? baseLocation = null;

            if (source.Kind == IptvSourceKind.Url)
            {
                string effectiveUrl = GetEffectiveUrl(source);
                baseLocation = effectiveUrl;
                content = await DownloadStringHardenedAsync(effectiveUrl, myCts.Token);
            }
            else if (source.Kind == IptvSourceKind.LocalFile)
            {
                string filePath = source.Location;
                if (!File.Exists(filePath))
                {
                    string storePath = _store.GetSourceFilePath(source.Id);
                    if (File.Exists(storePath)) filePath = storePath;
                }
                baseLocation = filePath;
                content = await File.ReadAllTextAsync(filePath, Encoding.UTF8, myCts.Token);

                if (source.CopyLocalFileToStore)
                {
                    _store.SaveSourceFileContent(source.Id, content);
                }
            }
            else if (source.Kind == IptvSourceKind.InlineText)
            {
                content = source.InlineContent ?? string.Empty;
                _store.SaveSourceFileContent(source.Id, content);
            }
            else if (source.Kind == IptvSourceKind.SingleStream)
            {
                string streamUrl = GetEffectiveUrl(source);
                var singleEntry = new IptvCachedChannelEntry
                {
                    Title = string.IsNullOrWhiteSpace(source.Name) ? "Single Stream" : source.Name,
                    Group = "Общие",
                    StreamUrl = streamUrl,
                    SortOrder = 0
                };

                var singleCache = new IptvPlaylistCache
                {
                    PlaylistId = source.Id,
                    RefreshedAtUtc = DateTime.UtcNow,
                    Channels = [singleEntry],
                    InvalidEntryCount = 0
                };
                _store.SaveCache(singleCache);

                source.LastRefreshedUtc = DateTime.UtcNow;
                source.LastStatus = "Ok";
                source.LastErrorMessage = null;
                source.ChannelCount = 1;
                return true;
            }

            var (channels, skipped) = IptvPlaylistParser.ParseM3u(content, source.Kind, baseLocation);

            var cache = new IptvPlaylistCache
            {
                PlaylistId = source.Id,
                RefreshedAtUtc = DateTime.UtcNow,
                Channels = channels,
                InvalidEntryCount = skipped
            };
            _store.SaveCache(cache);

            source.LastRefreshedUtc = DateTime.UtcNow;
            source.LastStatus = "Ok";
            source.LastErrorMessage = null;
            source.ChannelCount = channels.Count;
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (Exception ex)
        {
            source.LastStatus = "Error";
            source.LastErrorMessage = ex.Message;
            return false;
        }
        finally
        {
            entry.Lock.Release();
        }
    }

    public async Task<int> RefreshAllUrlSourcesAsync(IEnumerable<IptvPlaylistSource> sources, CancellationToken cancellationToken = default)
    {
        if (sources == null)
        {
            return 0;
        }

        var urlSources = sources.Where(s => s.IsEnabled && s.Kind == IptvSourceKind.Url).ToList();
        int successCount = 0;

        foreach (var source in urlSources)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            bool ok = await RefreshSourceAsync(source, cancellationToken);
            if (ok)
            {
                successCount++;
            }
        }

        return successCount;
    }

    public bool IsAutoUpdateDue(IptvSettings settings)
    {
        if (settings == null || !settings.AutoUpdateEnabled)
        {
            return false;
        }

        if (!settings.LastAutoUpdateUtc.HasValue)
        {
            return true;
        }

        int interval = Math.Max(1, settings.UpdateIntervalHours);
        return (DateTime.UtcNow - settings.LastAutoUpdateUtc.Value).TotalHours >= interval;
    }

    private string GetEffectiveUrl(IptvPlaylistSource source)
    {
        string? secret = _store.ReadSecretLocation(source.Id);
        if (!string.IsNullOrWhiteSpace(secret))
        {
            return secret;
        }

        return source.Location ?? string.Empty;
    }

    private async Task<string> DownloadStringHardenedAsync(string initialUrl, CancellationToken cancellationToken)
    {
        string currentUrl = initialUrl;
        int hops = 0;

        while (hops < MaxRedirectHops)
        {
            if (!Uri.TryCreate(currentUrl, UriKind.Absolute, out Uri? uri))
            {
                throw new InvalidOperationException("Invalid URI during download.");
            }

            await ValidateDestinationSecurityAsync(uri, cancellationToken);

            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

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
            if (contentLength.HasValue && contentLength.Value > MaxResponseBytes)
            {
                throw new InvalidOperationException($"Playlist exceeds maximum size limit of {MaxResponseBytes / 1024 / 1024} MiB.");
            }

            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var ms = new MemoryStream();
            byte[] buffer = new byte[8192];
            int read;
            long totalRead = 0;

            while ((read = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
            {
                totalRead += read;
                if (totalRead > MaxResponseBytes)
                {
                    throw new InvalidOperationException($"Playlist payload exceeded {MaxResponseBytes / 1024 / 1024} MiB limit.");
                }
                ms.Write(buffer, 0, read);
            }

            byte[] bytes = ms.ToArray();
            Encoding encoding = DetectEncoding(bytes, response.Content.Headers.ContentType?.CharSet);
            return encoding.GetString(bytes);
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
        catch
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
            foreach (var val in _sourceLocks.Values)
            {
                try { val.Cts?.Cancel(); } catch { }
                try { val.Lock.Dispose(); } catch { }
            }
            _sourceLocks.Clear();
        }
    }
}
