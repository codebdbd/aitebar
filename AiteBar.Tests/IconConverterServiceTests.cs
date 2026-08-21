using System;
using System.Buffers.Binary;
using System.Drawing;
using System.IO;
using System.Windows;
using AiteBar;
using SkiaSharp;

namespace AiteBar.Tests;

public sealed class IconConverterServiceTests
{
    [Fact]
    public async Task WriteIcoAtomicallyAsync_ReplacesDestinationOnlyAfterSuccessfulWrite()
    {
        string path = Path.ChangeExtension(Path.GetTempFileName(), ".ico");
        try
        {
            File.WriteAllBytes(path, [1, 2, 3]);

            await IconConverterService.WriteIcoAtomicallyAsync(path, [4, 5, 6]);

            Assert.Equal([4, 5, 6], File.ReadAllBytes(path));
            Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(path)!, $"{Path.GetFileName(path)}.*.tmp"));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task WriteIcoAtomicallyAsync_CancellationKeepsExistingDestination()
    {
        string path = Path.ChangeExtension(Path.GetTempFileName(), ".ico");
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();
        try
        {
            File.WriteAllBytes(path, [1, 2, 3]);

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                IconConverterService.WriteIcoAtomicallyAsync(path, [4, 5, 6], cancellationSource.Token));

            Assert.Equal([1, 2, 3], File.ReadAllBytes(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void MaxInputPixels_StaysWithinInteractiveMemoryBudget()
    {
        Assert.Equal(20L * 1000 * 1000, IconConverterService.MaxInputPixels);
    }

    [Fact]
    public void NormalizeOptions_SortsAndDeduplicatesSizes()
    {
        var options = IconConverterService.NormalizeOptions(new IconConversionOptions
        {
            Sizes = [48, 16, 32, 16],
            PaddingPercent = 8
        });

        Assert.Equal([16, 32, 48], options.Sizes);
    }

    [Fact]
    public void NormalizeOptions_RejectsEmptySizes()
    {
        Assert.Throws<InvalidOperationException>(() =>
            IconConverterService.NormalizeOptions(new IconConversionOptions { Sizes = [] }));
    }

    [Fact]
    public void NormalizeOptions_ClampsPadding()
    {
        var options = IconConverterService.NormalizeOptions(new IconConversionOptions
        {
            Sizes = [32],
            PaddingPercent = 80
        });

        Assert.Equal(40, options.PaddingPercent);
    }

    [Fact]
    public void CalculateTargetRect_FitPreservesAspectRatioWithPadding()
    {
        Rect rect = IconConverterService.CalculateTargetRect(200, 100, 100, 10, IconFitMode.Fit);

        Assert.Equal(10, rect.X, precision: 3);
        Assert.Equal(30, rect.Y, precision: 3);
        Assert.Equal(80, rect.Width, precision: 3);
        Assert.Equal(40, rect.Height, precision: 3);
    }

    [Fact]
    public void CalculateTargetRect_FillCoversContentBox()
    {
        Rect rect = IconConverterService.CalculateTargetRect(200, 100, 100, 10, IconFitMode.Fill);

        Assert.Equal(-30, rect.X, precision: 3);
        Assert.Equal(10, rect.Y, precision: 3);
        Assert.Equal(160, rect.Width, precision: 3);
        Assert.Equal(80, rect.Height, precision: 3);
    }

    [Fact]
    public async Task ConvertAsync_TransparentPng_PreservesAlphaAndWritesSelectedSizes()
    {
        string path = WriteTempImage(".png", SKEncodedImageFormat.Png, withAlpha: true);
        try
        {
            var service = new IconConverterService();
            IconConversionResult result = await service.ConvertAsync(path, new IconConversionOptions { Sizes = [16, 32, 256] });
            IcoEntry[] entries = ReadEntries(result.IcoBytes);

            Assert.Equal([16, 32, 256], entries.Select(entry => entry.Size));
            Assert.All(entries.Where(entry => entry.Size < 256), entry => Assert.True(HasDibHeader(entry.Payload)));
            Assert.True(HasPngSignature(entries.Single(entry => entry.Size == 256).Payload));
            Assert.Contains(entries, entry => entry.Size == 256 && entry.WidthByte == 0 && entry.HeightByte == 0);

            using SKBitmap? bitmap = SKBitmap.Decode(entries.First(entry => entry.Size == 256).Payload);
            Assert.NotNull(bitmap);
            Assert.True(HasTransparentPixel(bitmap!));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ConvertAsync_WritesIcoThatLoadsAsWindowsIcon()
    {
        string path = WriteTempImage(".png", SKEncodedImageFormat.Png, withAlpha: true);
        try
        {
            var service = new IconConverterService();
            IconConversionResult result = await service.ConvertAsync(path, new IconConversionOptions { Sizes = IconConversionOptions.DefaultSizes });

            using var stream = new MemoryStream(result.IcoBytes);
            using var icon = new Icon(stream);

            Assert.True(icon.Width > 0);
            Assert.True(icon.Height > 0);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ConvertAsync_Jpg_ProducesOpaqueDibPayload()
    {
        string path = WriteTempImage(".jpg", SKEncodedImageFormat.Jpeg, withAlpha: false);
        try
        {
            var service = new IconConverterService();
            IconConversionResult result = await service.ConvertAsync(path, new IconConversionOptions { Sizes = [32] });
            IcoEntry entry = Assert.Single(ReadEntries(result.IcoBytes));

            Assert.True(HasDibHeader(entry.Payload));
            Assert.All(Enumerable.Range(0, 32), x =>
            {
                for (int y = 0; y < 32; y++)
                {
                    Assert.Equal(255, ReadDibPixelAlpha(entry.Payload, 32, x, y));
                }
            });
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ConvertAsync_Svg_RendersEverySelectedSize()
    {
        string path = WriteTempSvg();
        try
        {
            var service = new IconConverterService();
            IconConversionResult result = await service.ConvertAsync(path, new IconConversionOptions
            {
                Sizes = IconConversionOptions.WindowsDpiSizes
            });

            IcoEntry[] entries = ReadEntries(result.IcoBytes);
            Assert.Equal(IconConversionOptions.WindowsDpiSizes, entries.Select(entry => entry.Size).ToArray());
            Assert.All(entries.Where(entry => entry.Size < 256), entry =>
            {
                Assert.True(HasDibHeader(entry.Payload));
                Assert.Equal(entry.Size, ReadDibWidth(entry.Payload));
                Assert.Equal(entry.Size * 2, ReadDibHeight(entry.Payload));
            });
            IcoEntry size256 = entries.Single(entry => entry.Size == 256);
            Assert.Equal(256, ReadPngWidth(size256.Payload));
            Assert.Equal(256, ReadPngHeight(size256.Payload));
            Assert.Empty(result.Warnings);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task GeneratePreviewResultAsync_DoesNotBuildIcoPayload()
    {
        string path = WriteTempImage(".png", SKEncodedImageFormat.Png, withAlpha: true);
        try
        {
            var service = new IconConverterService();
            IconConversionResult result = await service.GeneratePreviewResultAsync(path, new IconConversionOptions { Sizes = [16, 32] });

            Assert.Empty(result.IcoBytes);
            Assert.Equal([16, 32], result.Previews.Select(preview => preview.Size));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ConvertAsync_SourceSmallerThanTarget_ReturnsWarning()
    {
        string path = WriteTempImage(".png", SKEncodedImageFormat.Png, withAlpha: false, size: 32);
        try
        {
            var service = new IconConverterService();
            IconConversionResult result = await service.ConvertAsync(path, new IconConversionOptions { Sizes = [256] });

            Assert.NotEmpty(result.Warnings);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ConvertAsync_InvalidImage_Fails()
    {
        string path = Path.ChangeExtension(Path.GetTempFileName(), ".png");
        await File.WriteAllTextAsync(path, "not an image");
        try
        {
            var service = new IconConverterService();
            await Assert.ThrowsAsync<InvalidDataException>(() => service.ConvertAsync(path, new IconConversionOptions { Sizes = [32] }));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ConvertAsync_InvalidSolidBackgroundColor_Fails()
    {
        string path = WriteTempImage(".png", SKEncodedImageFormat.Png, withAlpha: true);
        try
        {
            var service = new IconConverterService();
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.ConvertAsync(path, new IconConversionOptions
            {
                Sizes = [32],
                BackgroundMode = IconBackgroundMode.SolidColor,
                BackgroundColor = "not-a-color"
            }));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ConvertAsync_UnsafeSvg_Fails()
    {
        string path = Path.ChangeExtension(Path.GetTempFileName(), ".svg");
        await File.WriteAllTextAsync(path, """
            <svg xmlns="http://www.w3.org/2000/svg" width="64" height="64">
              <image href="https://example.com/icon.png" width="64" height="64"/>
            </svg>
            """);
        try
        {
            var service = new IconConverterService();
            await Assert.ThrowsAsync<InvalidDataException>(() => service.ConvertAsync(path, new IconConversionOptions { Sizes = [32] }));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ConvertAsync_UnsafeSvgXlinkReference_Fails()
    {
        string path = Path.ChangeExtension(Path.GetTempFileName(), ".svg");
        await File.WriteAllTextAsync(path, """
            <svg xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink" width="64" height="64">
              <image xlink:href="file:///C:/temp/icon.png" width="64" height="64"/>
            </svg>
            """);
        try
        {
            var service = new IconConverterService();
            await Assert.ThrowsAsync<InvalidDataException>(() => service.ConvertAsync(path, new IconConversionOptions { Sizes = [32] }));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData("""
        <svg xmlns="http://www.w3.org/2000/svg" width="64" height="64">
          <circle cx="32" cy="32" r="20" fill="#007ACC" onclick="alert(1)"/>
        </svg>
        """)]
    [InlineData("""
        <svg xmlns="http://www.w3.org/2000/svg" width="64" height="64">
          <foreignObject width="64" height="64"><div xmlns="http://www.w3.org/1999/xhtml">unsafe</div></foreignObject>
        </svg>
        """)]
    [InlineData("""
        <svg xmlns="http://www.w3.org/2000/svg" width="64" height="64">
          <style>@import url("https://example.com/icon.css");</style>
          <circle cx="32" cy="32" r="20" fill="#007ACC"/>
        </svg>
        """)]
    [InlineData("""
        <svg xmlns="http://www.w3.org/2000/svg" width="64" height="64">
          <circle cx="32" cy="32" r="20" fill="url(data:image/svg+xml;base64,AAAA)"/>
        </svg>
        """)]
    public async Task ConvertAsync_UnsafeSvgContent_Fails(string svgContent)
    {
        string path = Path.ChangeExtension(Path.GetTempFileName(), ".svg");
        await File.WriteAllTextAsync(path, svgContent);
        try
        {
            var service = new IconConverterService();
            await Assert.ThrowsAsync<InvalidDataException>(() => service.ConvertAsync(path, new IconConversionOptions { Sizes = [32] }));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ConvertAsync_SvgLocalUseReference_IsAllowed()
    {
        string path = Path.ChangeExtension(Path.GetTempFileName(), ".svg");
        await File.WriteAllTextAsync(path, """
            <svg xmlns="http://www.w3.org/2000/svg" width="64" height="64" viewBox="0 0 64 64">
              <defs>
                <circle id="dot" cx="32" cy="32" r="20" fill="#007ACC"/>
              </defs>
              <use href="#dot"/>
            </svg>
            """);
        try
        {
            var service = new IconConverterService();
            IconConversionResult result = await service.ConvertAsync(path, new IconConversionOptions { Sizes = [32] });

            IcoEntry entry = Assert.Single(ReadEntries(result.IcoBytes));
            Assert.Equal(32, entry.Size);
            Assert.True(HasDibHeader(entry.Payload));
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static string WriteTempImage(string extension, SKEncodedImageFormat format, bool withAlpha, int size = 96)
    {
        string path = Path.ChangeExtension(Path.GetTempFileName(), extension);
        using var surface = SKSurface.Create(new SKImageInfo(size, size, SKColorType.Rgba8888, SKAlphaType.Premul));
        surface.Canvas.Clear(withAlpha ? SKColors.Transparent : SKColors.White);
        using var paint = new SKPaint { Color = withAlpha ? new SKColor(20, 120, 220, 180) : SKColors.DodgerBlue, IsAntialias = true };
        surface.Canvas.DrawCircle(size / 2f, size / 2f, size * 0.35f, paint);
        using SKImage image = surface.Snapshot();
        using SKData data = image.Encode(format, 95);
        File.WriteAllBytes(path, data.ToArray());
        return path;
    }

    private static string WriteTempSvg()
    {
        string path = Path.ChangeExtension(Path.GetTempFileName(), ".svg");
        File.WriteAllText(path, """
            <svg xmlns="http://www.w3.org/2000/svg" width="128" height="128" viewBox="0 0 128 128">
              <rect width="128" height="128" fill="none"/>
              <circle cx="64" cy="64" r="44" fill="#007ACC"/>
              <path d="M38 66 L56 84 L92 44" fill="none" stroke="white" stroke-width="12" stroke-linecap="round" stroke-linejoin="round"/>
            </svg>
            """);
        return path;
    }

    private static IcoEntry[] ReadEntries(byte[] ico)
    {
        int count = BitConverter.ToUInt16(ico, 4);
        var entries = new IcoEntry[count];
        for (int i = 0; i < count; i++)
        {
            int entryOffset = 6 + (i * 16);
            byte widthByte = ico[entryOffset];
            byte heightByte = ico[entryOffset + 1];
            int size = widthByte == 0 ? 256 : widthByte;
            int payloadLength = (int)BitConverter.ToUInt32(ico, entryOffset + 8);
            int payloadOffset = (int)BitConverter.ToUInt32(ico, entryOffset + 12);
            byte[] payload = ico.AsSpan(payloadOffset, payloadLength).ToArray();
            entries[i] = new IcoEntry(size, widthByte, heightByte, payload);
        }

        return entries;
    }

    private static bool HasPngSignature(byte[] data) =>
        data.Length >= 8 &&
        data[0] == 0x89 &&
        data[1] == 0x50 &&
        data[2] == 0x4E &&
        data[3] == 0x47 &&
        data[4] == 0x0D &&
        data[5] == 0x0A &&
        data[6] == 0x1A &&
        data[7] == 0x0A;

    private static bool HasDibHeader(byte[] data) =>
        data.Length >= 40 && BitConverter.ToUInt32(data, 0) == 40;

    private static int ReadPngWidth(byte[] png) => BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(16, 4));

    private static int ReadPngHeight(byte[] png) => BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(20, 4));

    private static int ReadDibWidth(byte[] dib) => BitConverter.ToInt32(dib, 4);

    private static int ReadDibHeight(byte[] dib) => BitConverter.ToInt32(dib, 8);

    private static byte ReadDibPixelAlpha(byte[] dib, int size, int x, int y)
    {
        int bottomUpY = size - 1 - y;
        int offset = 40 + (bottomUpY * size * 4) + (x * 4) + 3;
        return dib[offset];
    }

    private static bool HasTransparentPixel(SKBitmap bitmap)
    {
        for (int y = 0; y < bitmap.Height; y++)
        {
            for (int x = 0; x < bitmap.Width; x++)
            {
                if (bitmap.GetPixel(x, y).Alpha < 255)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private sealed record IcoEntry(int Size, byte WidthByte, byte HeightByte, byte[] Payload);
}
