using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace AiteBar.IptvPlayer;

[SupportedOSPlatform("windows6.1")]
public static class IptvLogoCache
{
    private static readonly ConcurrentDictionary<string, ImageSource?> s_memoryCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> s_failedUrls = new(StringComparer.OrdinalIgnoreCase);
    private static readonly object s_failedLock = new();
    private static readonly SemaphoreSlim s_downloadSemaphore = new(2, 2);
    private static readonly HttpClient s_httpClient = new(new HttpClientHandler { AllowAutoRedirect = false });
    private static string? s_cacheDir;

    public static void Initialize(string cacheBaseDirectory)
    {
        try
        {
            s_cacheDir = Path.Combine(cacheBaseDirectory, "Logos");
            if (!Directory.Exists(s_cacheDir))
            {
                Directory.CreateDirectory(s_cacheDir);
            }
        }
        catch (Exception ex)
        {
            Logger.Log(ex);
        }
    }

    internal static bool TryGetCachedLogo(string url, out ImageSource? image) =>
        s_memoryCache.TryGetValue(url, out image);

    public static async Task<ImageSource?> LoadLogoAsync(string? url, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;

        if (s_memoryCache.TryGetValue(url, out var cached))
        {
            return cached;
        }

        lock (s_failedLock)
        {
            if (s_failedUrls.Contains(url)) return null;
        }

        string hash = ComputeMd5(url);
        string? filePath = s_cacheDir != null ? Path.Combine(s_cacheDir, $"{hash}.png") : null;

        if (filePath != null && File.Exists(filePath))
        {
            try
            {
                var bitmap = LoadFromDisk(filePath);
                if (bitmap != null)
                {
                    s_memoryCache[url] = bitmap;
                    return bitmap;
                }
            }
            catch
            {
                // Игнорируем
            }
        }

        if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        await s_downloadSemaphore.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (s_memoryCache.TryGetValue(url, out cached)) return cached;
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(2.5));
            var (bytes, _, _) = await IptvPlaylistService.DownloadRemoteBytesAsync(
                s_httpClient, url, 5 * 1024 * 1024, timeout.Token).ConfigureAwait(false);

            var bitmap = DecodeBytes(bytes);
            if (bitmap == null)
            {
                RecordFailed(url);
                return null;
            }

            if (filePath != null)
            {
                try
                {
                    await File.WriteAllBytesAsync(filePath, bytes, ct).ConfigureAwait(false);
                }
                catch
                {
                    // Игнорируем
                }
            }

            s_memoryCache[url] = bitmap;
            return bitmap;
        }
        catch
        {
            RecordFailed(url);
        }
        finally
        {
            s_downloadSemaphore.Release();
        }

        return null;
    }

    private static void RecordFailed(string url)
    {
        lock (s_failedLock)
        {
            s_failedUrls.Add(url);
        }
        s_memoryCache[url] = null;
    }

    private static BitmapSource? LoadFromDisk(string filePath)
    {
        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        var bi = new BitmapImage();
        bi.BeginInit();
        bi.DecodePixelWidth = 28;
        bi.CacheOption = BitmapCacheOption.OnLoad;
        bi.StreamSource = stream;
        bi.EndInit();
        bi.Freeze();
        return bi;
    }

    private static BitmapSource? DecodeBytes(byte[] bytes)
    {
        using var ms = new MemoryStream(bytes);
        var bi = new BitmapImage();
        bi.BeginInit();
        bi.DecodePixelWidth = 28;
        bi.CacheOption = BitmapCacheOption.OnLoad;
        bi.StreamSource = ms;
        bi.EndInit();
        bi.Freeze();
        return bi;
    }

    private static string ComputeMd5(string input)
    {
        byte[] hash = MD5.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
