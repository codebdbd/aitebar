using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.Versioning;
using System.Windows.Media.Imaging;

namespace AiteBar
{
    [SupportedOSPlatform("windows")]
    internal static class IconHelper
    {
        public static string? ExtractAndSaveIcon(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) return null;

            try
            {
                using (var icon = Icon.ExtractAssociatedIcon(filePath))
                {
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
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
                return null;
            }
        }
    }
}
