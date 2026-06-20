using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net.Http;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace AiteBar
{
    [SupportedOSPlatform("windows")]
    internal static class IconHelper
    {
        private static readonly HttpClient _httpClient = CreateHttpClient();

        private static HttpClient CreateHttpClient()
        {
            var handler = new HttpClientHandler
            {
                AllowAutoRedirect = false
            };

            return new HttpClient(handler);
        }

        public static async Task<string?> DownloadFaviconAsync(string url, double dpi = 1.0)
        {
            try
            {
                if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return null;

                // Используем Google Favicon Service как самый надежный и быстрый способ
                int faviconSize = dpi > 1.5 ? 48 : 32;
                string faviconUrl = $"https://www.google.com/s2/favicons?domain={uri.Host}&sz={faviconSize}";
                var currentUrl = new Uri(faviconUrl);

                // Ограничим количество редиректов, чтобы избежать циклов
                const int maxRedirects = 5;
                for (int i = 0; i < maxRedirects; i++)
                {
                    // Всегда проверяем, что текущий URL — HTTPS
                    if (!string.Equals(currentUrl.Scheme, "https", StringComparison.OrdinalIgnoreCase))
                    {
                        return null;
                    }

                    var response = await _httpClient.GetAsync(currentUrl);

                    // Если это успешный ответ (не редирект), обрабатываем его
                    if (response.IsSuccessStatusCode)
                    {
                        byte[] data = await response.Content.ReadAsByteArrayAsync();

                        PathHelper.EnsureDirectories();
                        string fileName = $"web_{Guid.NewGuid()}.png";
                        string destPath = Path.Combine(PathHelper.IconsFolder, fileName);

                        await File.WriteAllBytesAsync(destPath, data);
                        return destPath;
                    }

                    // Проверяем, является ли ответ редиректом
                    if (response.StatusCode == System.Net.HttpStatusCode.MovedPermanently ||
                        response.StatusCode == System.Net.HttpStatusCode.Found ||
                        response.StatusCode == System.Net.HttpStatusCode.SeeOther ||
                        response.StatusCode == System.Net.HttpStatusCode.TemporaryRedirect ||
                        response.StatusCode == System.Net.HttpStatusCode.PermanentRedirect)
                    {
                        var locationHeader = response.Headers.Location;
                        if (locationHeader == null)
                        {
                            return null;
                        }

                        // Разрешаем относительный URL относительно текущего
                        currentUrl = new Uri(currentUrl, locationHeader);
                        continue;
                    }

                    // Если ответ не успешный и не редирект — возвращаем null
                    return null;
                }

                // Превышено максимальное количество редиректов
                return null;
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
                return null;
            }
        }

        public static string? ExtractAndSaveIcon(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) return null;

            try
            {
                using var icon = Icon.ExtractAssociatedIcon(filePath);
                if (icon == null) return null;

                PathHelper.EnsureDirectories();
                string fileName = $"auto_{Guid.NewGuid()}.png";
                string destPath = Path.Combine(PathHelper.IconsFolder, fileName);

                using (var bitmap = icon.ToBitmap())
                {
                    bitmap.Save(destPath, ImageFormat.Png);
                }

                return destPath;
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
                return null;
            }
        }

        public static string? SaveCustomIcon(string sourcePath)
        {
            if (string.IsNullOrEmpty(sourcePath) || !File.Exists(sourcePath)) return null;
            try
            {
                PathHelper.EnsureDirectories();
                string ext = Path.GetExtension(sourcePath).ToLowerInvariant();
                string fileName = $"custom_{Guid.NewGuid()}{ext}";

                if (ext == ".ico")
                {
                    using var icon = new Icon(sourcePath);
                    using var bitmap = icon.ToBitmap();
                    string destPath = Path.Combine(PathHelper.IconsFolder, Path.ChangeExtension(fileName, ".png"));
                    bitmap.Save(destPath, ImageFormat.Png);
                    return destPath;
                }
                else
                {
                    string destPath = Path.Combine(PathHelper.IconsFolder, fileName);
                    File.Copy(sourcePath, destPath, true);
                    return destPath;
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
                return null;
            }
        }
    }
}
