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
        private static readonly HttpClient _httpClient = new();

        public static async Task<string?> DownloadFaviconAsync(string url)
        {
            try
            {
                if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return null;

                // Используем Google Favicon Service как самый надежный и быстрый способ
                string faviconUrl = $"https://www.google.com/s2/favicons?domain={uri.Host}&sz=64";
                
                var response = await _httpClient.GetAsync(faviconUrl);
                if (!response.IsSuccessStatusCode) return null;

                byte[] data = await response.Content.ReadAsByteArrayAsync();
                
                PathHelper.EnsureDirectories();
                string fileName = $"web_{Guid.NewGuid()}.png";
                string destPath = Path.Combine(PathHelper.IconsFolder, fileName);

                await File.WriteAllBytesAsync(destPath, data);
                return destPath;
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
