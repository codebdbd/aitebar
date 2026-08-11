using System;
using System.Drawing;
using System.IO;
using AiteBar;
using Xunit;

namespace AiteBar.Tests;

public sealed class IconHelperTests
{
    [Fact]
    public void ExtractAndSaveIcon_NullPath_ReturnsNull()
    {
        var result = IconHelper.ExtractAndSaveIcon(null!);
        Assert.Null(result);
    }

    [Fact]
    public void ExtractAndSaveIcon_EmptyPath_ReturnsNull()
    {
        var result = IconHelper.ExtractAndSaveIcon("");
        Assert.Null(result);
    }

    [Fact]
    public void ExtractAndSaveIcon_NonExistentFile_ReturnsNull()
    {
        var result = IconHelper.ExtractAndSaveIcon("C:\\nonexistent\\file.exe");
        Assert.Null(result);
    }

    [Fact]
    public void SaveCustomIcon_NullPath_ReturnsNull()
    {
        var result = IconHelper.SaveCustomIcon(null!);
        Assert.Null(result);
    }

    [Fact]
    public void SaveCustomIcon_EmptyPath_ReturnsNull()
    {
        var result = IconHelper.SaveCustomIcon("");
        Assert.Null(result);
    }

    [Fact]
    public void SaveCustomIcon_NonExistentFile_ReturnsNull()
    {
        var result = IconHelper.SaveCustomIcon("C:\\nonexistent\\image.png");
        Assert.Null(result);
    }

    [Fact]
    public async Task DownloadFaviconAsync_NullUrl_ReturnsNull()
    {
        var result = await IconHelper.DownloadFaviconAsync(null!);
        Assert.Null(result);
    }

    [Fact]
    public async Task DownloadFaviconAsync_EmptyUrl_ReturnsNull()
    {
        var result = await IconHelper.DownloadFaviconAsync("");
        Assert.Null(result);
    }

    [Fact]
    public async Task DownloadFaviconAsync_InvalidUrl_ReturnsNull()
    {
        var result = await IconHelper.DownloadFaviconAsync("not-a-valid-url");
        Assert.Null(result);
    }

    [Fact]
    public void SaveCustomIcon_InvalidImageFile_ReturnsNull()
    {
        string root = Path.Combine(Path.GetTempPath(), "AiteBarTests", Guid.NewGuid().ToString("N"));
        string testRoot = Path.Combine(root, "test");
        Directory.CreateDirectory(root);
        string sourcePath = Path.Combine(root, "icon.png");
        File.WriteAllText(sourcePath, "test");

        try
        {
            PathHelper.SetAppDataFolderOverride(testRoot);
            var savedPath = IconHelper.SaveCustomIcon(sourcePath);

            Assert.Null(savedPath);
        }
        finally
        {
            PathHelper.ClearAppDataFolderOverride();
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void SaveCustomIcon_RegularFile_CopiesFileIntoIconsFolder()
    {
        string root = Path.Combine(Path.GetTempPath(), "AiteBarTests", Guid.NewGuid().ToString("N"));
        string testRoot = Path.Combine(root, "test");
        Directory.CreateDirectory(root);
        string sourcePath = Path.Combine(root, "icon.png");
        string? savedPath = null;

        try
        {
            using (var bitmap = new Bitmap(8, 8))
            {
                bitmap.SetPixel(0, 0, Color.Red);
                bitmap.Save(sourcePath, System.Drawing.Imaging.ImageFormat.Png);
            }

            PathHelper.SetAppDataFolderOverride(testRoot);
            savedPath = IconHelper.SaveCustomIcon(sourcePath);

            Assert.NotNull(savedPath);
            Assert.StartsWith(PathHelper.IconsFolder, savedPath!, StringComparison.OrdinalIgnoreCase);
            Assert.True(File.Exists(savedPath));
            Assert.True(IconHelper.IsSupportedImageFile(savedPath!));
        }
        finally
        {
            PathHelper.ClearAppDataFolderOverride();
            if (savedPath != null && File.Exists(savedPath))
            {
                File.Delete(savedPath);
            }
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ExtractAndSaveIcon_ExecutableWithAssociatedIcon_SavesPng()
    {
        string systemExe = Path.Combine(Environment.SystemDirectory, "notepad.exe");
        if (!File.Exists(systemExe))
        {
            return;
        }

        string root = Path.Combine(Path.GetTempPath(), "AiteBarTests", Guid.NewGuid().ToString("N"));
        string testRoot = Path.Combine(root, "test");
        Directory.CreateDirectory(root);
        string? savedPath = null;

        try
        {
            PathHelper.SetAppDataFolderOverride(testRoot);
            savedPath = IconHelper.ExtractAndSaveIcon(systemExe);

            Assert.NotNull(savedPath);
            Assert.StartsWith(PathHelper.IconsFolder, savedPath!, StringComparison.OrdinalIgnoreCase);
            Assert.True(File.Exists(savedPath));
            Assert.Equal(".png", Path.GetExtension(savedPath));
        }
        finally
        {
            PathHelper.ClearAppDataFolderOverride();
            if (savedPath != null && File.Exists(savedPath))
            {
                File.Delete(savedPath);
            }
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void SaveCustomIcon_IcoFile_ConvertsToPng()
    {
        string systemExe = Path.Combine(Environment.SystemDirectory, "notepad.exe");
        if (!File.Exists(systemExe))
        {
            return;
        }

        string root = Path.Combine(Path.GetTempPath(), "AiteBarTests", Guid.NewGuid().ToString("N"));
        string testRoot = Path.Combine(root, "test");
        Directory.CreateDirectory(root);
        string sourcePath = Path.Combine(root, "icon.ico");
        string? savedPath = null;

        try
        {
            using var icon = Icon.ExtractAssociatedIcon(systemExe);
            Assert.NotNull(icon);

            using (var stream = File.Create(sourcePath))
            {
                icon!.Save(stream);
            }

            PathHelper.SetAppDataFolderOverride(testRoot);
            savedPath = IconHelper.SaveCustomIcon(sourcePath);

            Assert.NotNull(savedPath);
            Assert.Equal(".png", Path.GetExtension(savedPath));
            Assert.True(File.Exists(savedPath));
        }
        finally
        {
            PathHelper.ClearAppDataFolderOverride();
            if (savedPath != null && File.Exists(savedPath))
            {
                File.Delete(savedPath);
            }
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void SaveCustomIcon_SvgFile_ConvertsToPng()
    {
        string root = Path.Combine(Path.GetTempPath(), "AiteBarTests", Guid.NewGuid().ToString("N"));
        string testRoot = Path.Combine(root, "test");
        Directory.CreateDirectory(root);
        string sourcePath = Path.Combine(root, "icon.svg");
        File.WriteAllText(sourcePath, """
            <svg xmlns="http://www.w3.org/2000/svg" width="64" height="64" viewBox="0 0 64 64">
              <circle cx="32" cy="32" r="24" fill="#007ACC"/>
              <path d="M20 33 L29 42 L45 22" fill="none" stroke="white" stroke-width="6" stroke-linecap="round" stroke-linejoin="round"/>
            </svg>
            """);
        string? savedPath = null;

        try
        {
            PathHelper.SetAppDataFolderOverride(testRoot);
            savedPath = IconHelper.SaveCustomIcon(sourcePath);

            Assert.NotNull(savedPath);
            Assert.Equal(".png", Path.GetExtension(savedPath));
            Assert.True(File.Exists(savedPath));
            Assert.True(IconHelper.IsSupportedImageFile(savedPath!));
        }
        finally
        {
            PathHelper.ClearAppDataFolderOverride();
            if (savedPath != null && File.Exists(savedPath))
            {
                File.Delete(savedPath);
            }
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void SaveCustomIcon_UnsafeSvgFile_ReturnsNull()
    {
        string root = Path.Combine(Path.GetTempPath(), "AiteBarTests", Guid.NewGuid().ToString("N"));
        string testRoot = Path.Combine(root, "test");
        Directory.CreateDirectory(root);
        string sourcePath = Path.Combine(root, "icon.svg");
        File.WriteAllText(sourcePath, """
            <svg xmlns="http://www.w3.org/2000/svg" width="64" height="64">
              <image href="https://example.com/icon.png" width="64" height="64"/>
            </svg>
            """);

        try
        {
            PathHelper.SetAppDataFolderOverride(testRoot);
            var savedPath = IconHelper.SaveCustomIcon(sourcePath);

            Assert.Null(savedPath);
        }
        finally
        {
            PathHelper.ClearAppDataFolderOverride();
            Directory.Delete(root, recursive: true);
        }
    }
}
