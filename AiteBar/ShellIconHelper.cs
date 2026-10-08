using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Media.Imaging;

namespace AiteBar;

[SupportedOSPlatform("windows")]
internal static class ShellIconHelper
{
    [ComImport]
    [Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItemImageFactory
    {
        [PreserveSig]
        int GetImage(
            [In] SIZE size,
            [In] SIIGBF flags,
            [Out] out IntPtr phbm);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SIZE
    {
        public int cx;
        public int cy;
        public SIZE(int cx, int cy) { this.cx = cx; this.cy = cy; }
    }

    [Flags]
    private enum SIIGBF
    {
        SIIGBF_RESIZETOFIT = 0x00,
        SIIGBF_BIGGERSIZEOK = 0x01,
        SIIGBF_MEMORYONLY = 0x02,
        SIIGBF_ICONONLY = 0x04,
        SIIGBF_THUMBNAILONLY = 0x08,
        SIIGBF_INCACHEONLY = 0x10,
        SIIGBF_CROPTOSQUARE = 0x20,
        SIIGBF_EXACTDATE = 0x40,
        SIIGBF_SCALEUP = 0x100
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct SHFILEINFO
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string szTypeName;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAP
    {
        public int bmType;
        public int bmWidth;
        public int bmHeight;
        public int bmWidthBytes;
        public ushort bmPlanes;
        public ushort bmBitsPixel;
        public IntPtr bmBits;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFOHEADER
    {
        public uint biSize;
        public int biWidth;
        public int biHeight;
        public ushort biPlanes;
        public ushort biBitCount;
        public uint biCompression;
        public uint biSizeImage;
        public int biXPelsPerMeter;
        public int biYPelsPerMeter;
        public uint biClrUsed;
        public uint biClrImportant;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFO
    {
        public BITMAPINFOHEADER bmiHeader;
        public uint bmiColors;
    }

    private const uint SHGFI_ICON = 0x000000100;
    private const uint SHGFI_LARGEICON = 0x000000000;

    [DllImport("shell32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, out SHFILEINFO psfi, uint cbFileInfo, uint uFlags);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHCreateItemFromParsingName(
        [In, MarshalAs(UnmanagedType.LPWStr)] string pszPath,
        [In] IntPtr pbc,
        [In, MarshalAs(UnmanagedType.LPStruct)] Guid riid,
        [Out, MarshalAs(UnmanagedType.Interface)] out object ppv);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern bool DeleteObject(IntPtr hObject);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern int GetObject(IntPtr hgdiobj, int cbBuffer, out BITMAP lpvObject);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern int GetDIBits(IntPtr hdc, IntPtr hbmp, uint uStartScan, uint cScanLines, [Out] byte[] lpvBits, ref BITMAPINFO lpbi, uint uUsage);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern bool DeleteDC(IntPtr hdc);

    private static readonly Guid IShellItemImageFactoryGuid = new("bcc18b79-ba16-442f-80c4-8a59c30c463b");

    /// <summary>
    /// Сохраняет 32-битный HBITMAP с сохранением альфа-канала (прозрачности) в PNG файл.
    /// Предотвращает появление черного фона вместо прозрачности при использовании стандартного GDI+.
    /// </summary>
    public static bool SaveHBitmapAsPng(IntPtr hBitmap, string destFile)
    {
        if (hBitmap == IntPtr.Zero) return false;

        if (GetObject(hBitmap, Marshal.SizeOf<BITMAP>(), out BITMAP bm) == 0)
            return false;

        int width = bm.bmWidth;
        int height = Math.Abs(bm.bmHeight);
        if (width <= 0 || height <= 0)
            return false;

        var bi = new BITMAPINFO();
        bi.bmiHeader.biSize = (uint)Marshal.SizeOf<BITMAPINFOHEADER>();
        bi.bmiHeader.biWidth = width;
        bi.bmiHeader.biHeight = -height; // Верхний край сверху вниз (top-down)
        bi.bmiHeader.biPlanes = 1;
        bi.bmiHeader.biBitCount = 32;
        bi.bmiHeader.biCompression = 0; // BI_RGB

        IntPtr hdc = CreateCompatibleDC(IntPtr.Zero);
        if (hdc == IntPtr.Zero) return false;

        try
        {
            byte[] pixels = new byte[width * height * 4];
            int lines = GetDIBits(hdc, hBitmap, 0, (uint)height, pixels, ref bi, 0);
            if (lines == 0) return false;

            bool hasAlpha = false;
            for (int i = 3; i < pixels.Length; i += 4)
            {
                if (pixels[i] > 0)
                {
                    hasAlpha = true;
                    break;
                }
            }

            if (!hasAlpha)
            {
                for (int i = 3; i < pixels.Length; i += 4)
                {
                    pixels[i] = 255;
                }
            }

            var bitmapSource = BitmapSource.Create(
                width,
                height,
                96,
                96,
                hasAlpha ? System.Windows.Media.PixelFormats.Pbgra32 : System.Windows.Media.PixelFormats.Bgr32,
                null,
                pixels,
                width * 4);

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmapSource));
            using var stream = File.Create(destFile);
            encoder.Save(stream);
            return true;
        }
        catch (Exception ex)
        {
            Logger.Log(ex);
            return false;
        }
        finally
        {
            DeleteDC(hdc);
        }
    }

    /// <summary>
    /// Сохраняет HICON с сохранением полной прозрачности в PNG файл.
    /// </summary>
    public static bool SaveHIconAsPng(IntPtr hIcon, string destFile)
    {
        if (hIcon == IntPtr.Zero) return false;

        try
        {
            var bitmapSource = System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(
                hIcon,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmapSource));
            using var stream = File.Create(destFile);
            encoder.Save(stream);
            return true;
        }
        catch (Exception ex)
        {
            Logger.Log(ex);
            return false;
        }
    }

    /// <summary>
    /// Извлекает чистую иконку высокого разрешения (без значка ярлыка) с сохранением альфа-прозрачности
    /// для любого пути Windows (.exe, файл, папка или shell:AppsFolder\...) и сохраняет в PNG.
    /// </summary>
    public static string? ExtractAndSaveShellIcon(string path, int targetSize = 48)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        try
        {
            PathHelper.EnsureDirectories();

            string cleanPath = path.Trim();
            // Стабильный файл иконки на основе хеша пути
            string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(cleanPath.ToLowerInvariant())))[..16];
            string destFile = Path.Combine(PathHelper.IconsFolder, $"icon_{hash}.png");

            if (File.Exists(destFile) && new FileInfo(destFile).Length > 0)
            {
                return destFile;
            }

            // 1. Попытка через IShellItemImageFactory (работает для shell:AppsFolder, папок, файлов и дает чистый HBITMAP без стрелочек)
            IntPtr hBitmap = IntPtr.Zero;
            try
            {
                int hr = SHCreateItemFromParsingName(cleanPath, IntPtr.Zero, IShellItemImageFactoryGuid, out object factoryObj);
                if (hr == 0 && factoryObj is IShellItemImageFactory factory)
                {
                    hr = factory.GetImage(new SIZE(targetSize, targetSize), SIIGBF.SIIGBF_BIGGERSIZEOK | SIIGBF.SIIGBF_ICONONLY, out hBitmap);
                    if (hr == 0 && hBitmap != IntPtr.Zero)
                    {
                        if (SaveHBitmapAsPng(hBitmap, destFile))
                        {
                            return destFile;
                        }
                    }
                }
            }
            finally
            {
                if (hBitmap != IntPtr.Zero)
                {
                    DeleteObject(hBitmap);
                }
            }

            // 2. Fallback через SHGetFileInfo (для папок, дисков и файлов, если IShellItemImageFactory не сработал)
            if (File.Exists(cleanPath) || Directory.Exists(cleanPath))
            {
                try
                {
                    if (SHGetFileInfo(cleanPath, 0, out SHFILEINFO shfi, (uint)Marshal.SizeOf<SHFILEINFO>(), SHGFI_ICON | SHGFI_LARGEICON) != IntPtr.Zero && shfi.hIcon != IntPtr.Zero)
                    {
                        try
                        {
                            if (SaveHIconAsPng(shfi.hIcon, destFile))
                            {
                                return destFile;
                            }
                        }
                        finally
                        {
                            DestroyIcon(shfi.hIcon);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Log(ex);
                }
            }

            // 3. Fallback для физических файлов (.exe, .ico)
            if (File.Exists(cleanPath))
            {
                string? fallback = IconHelper.ExtractAndSaveIcon(cleanPath);
                if (!string.IsNullOrEmpty(fallback) && File.Exists(fallback))
                {
                    if (destFile != fallback)
                    {
                        File.Copy(fallback, destFile, true);
                    }
                    return destFile;
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Log(ex);
        }

        return null;
    }
}
