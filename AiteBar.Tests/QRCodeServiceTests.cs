using System;
using System.Threading.Tasks;
using AiteBar;
using Xunit;

namespace AiteBar.Tests;

public sealed class QRCodeServiceTests
{
    private readonly QRCodeService _service = new();

    [Fact]
    public async Task GenerateAsync_ValidText_ReturnsNonEmptyPngAndSvg()
    {
        var options = new QRCodeGenerationOptions
        {
            Text = "https://example.com",
            PixelSize = 10,
            Margin = 2,
            EccLevel = QRCodeEccLevel.Q
        };

        QRCodeGenerationResult result = await _service.GenerateAsync(options);

        Assert.NotEmpty(result.PngBytes);
        Assert.NotEmpty(result.SvgContent);
        Assert.True(result.ModuleCount > 0);
        Assert.True(result.Version > 0);
    }

    [Fact]
    public async Task GenerateAsync_PngBytes_StartWithPngHeader()
    {
        QRCodeGenerationResult result = await _service.GenerateAsync(new QRCodeGenerationOptions { Text = "Hello World" });

        Assert.Equal(0x89, result.PngBytes[0]);
        Assert.Equal(0x50, result.PngBytes[1]);
        Assert.Equal(0x4E, result.PngBytes[2]);
        Assert.Equal(0x47, result.PngBytes[3]);
    }

    [Fact]
    public async Task GenerateAsync_SvgContent_StartsWithSvgTag()
    {
        QRCodeGenerationResult result = await _service.GenerateAsync(new QRCodeGenerationOptions { Text = "Hello World" });

        Assert.StartsWith("<svg", result.SvgContent);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GenerateAsync_EmptyOrWhitespaceText_ThrowsArgumentException(string text)
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.GenerateAsync(new QRCodeGenerationOptions { Text = text }));
    }

    [Fact]
    public async Task GenerateAsync_TextTooLong_ThrowsArgumentException()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.GenerateAsync(new QRCodeGenerationOptions { Text = new string('A', 4297) }));
    }

    [Fact]
    public async Task GenerateAsync_DifferentEccLevels_ProduceValidQr()
    {
        const string text = "The quick brown fox jumps over the lazy dog. Pack my box with five dozen liquor jugs.";

        QRCodeGenerationResult resultL = await _service.GenerateAsync(new QRCodeGenerationOptions { Text = text, EccLevel = QRCodeEccLevel.L });
        QRCodeGenerationResult resultH = await _service.GenerateAsync(new QRCodeGenerationOptions { Text = text, EccLevel = QRCodeEccLevel.H });

        Assert.True(resultH.ModuleCount >= resultL.ModuleCount);
    }

    [Fact]
    public void GenerateQrData_ValidText_ReturnsNonNullData()
    {
        using var qrData = _service.GenerateQrData("test", QRCodeEccLevel.Q);

        Assert.NotNull(qrData);
        Assert.True(qrData.ModuleMatrix.Count > 0);
    }

    [Fact]
    public void RenderPng_ValidData_ReturnsNonEmptyBytes()
    {
        using var qrData = _service.GenerateQrData("test", QRCodeEccLevel.Q);

        byte[] png = _service.RenderPng(qrData, 10, "#000000", "#FFFFFF", 2);

        Assert.NotEmpty(png);
        Assert.Equal(0x89, png[0]);
    }

    [Fact]
    public void RenderSvg_ValidData_ReturnsSvgString()
    {
        using var qrData = _service.GenerateQrData("test", QRCodeEccLevel.Q);

        string svg = _service.RenderSvg(qrData, 10, "#000000", "#FFFFFF", 2);

        Assert.NotEmpty(svg);
        Assert.Contains("<svg", svg);
    }

    [Theory]
    [InlineData(QRCodeEccLevel.L)]
    [InlineData(QRCodeEccLevel.M)]
    [InlineData(QRCodeEccLevel.Q)]
    [InlineData(QRCodeEccLevel.H)]
    public async Task GenerateAsync_AllEccLevels_ProduceValidQr(QRCodeEccLevel level)
    {
        QRCodeGenerationResult result = await _service.GenerateAsync(new QRCodeGenerationOptions
        {
            Text = "https://example.com",
            EccLevel = level
        });

        Assert.NotEmpty(result.PngBytes);
        Assert.NotEmpty(result.SvgContent);
    }
}
