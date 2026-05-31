using System;
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
}
