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
    public void SaveCustomIcon_RegularFile_CopiesFileIntoIconsFolder()
    {
        string root = Path.Combine(Path.GetTempPath(), "AiteBarTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string sourcePath = Path.Combine(root, "icon.png");
        File.WriteAllText(sourcePath, "test");
        string? savedPath = null;

        try
        {
            savedPath = IconHelper.SaveCustomIcon(sourcePath);

            Assert.NotNull(savedPath);
            Assert.StartsWith(PathHelper.IconsFolder, savedPath!, StringComparison.OrdinalIgnoreCase);
            Assert.True(File.Exists(savedPath));
            Assert.Equal("test", File.ReadAllText(savedPath));
        }
        finally
        {
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

        string? savedPath = null;

        try
        {
            savedPath = IconHelper.ExtractAndSaveIcon(systemExe);

            Assert.NotNull(savedPath);
            Assert.StartsWith(PathHelper.IconsFolder, savedPath!, StringComparison.OrdinalIgnoreCase);
            Assert.True(File.Exists(savedPath));
            Assert.Equal(".png", Path.GetExtension(savedPath));
        }
        finally
        {
            if (savedPath != null && File.Exists(savedPath))
            {
                File.Delete(savedPath);
            }
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

            savedPath = IconHelper.SaveCustomIcon(sourcePath);

            Assert.NotNull(savedPath);
            Assert.Equal(".png", Path.GetExtension(savedPath));
            Assert.True(File.Exists(savedPath));
        }
        finally
        {
            if (savedPath != null && File.Exists(savedPath))
            {
                File.Delete(savedPath);
            }
            Directory.Delete(root, recursive: true);
        }
    }
}
