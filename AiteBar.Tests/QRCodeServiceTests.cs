using System;
using System.IO;
using System.Threading.Tasks;
using AiteBar;
using SkiaSharp;
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

    [Fact]
    public async Task GenerateAsync_OutputSize_ControlsPngDimensions()
    {
        QRCodeGenerationResult result = await _service.GenerateAsync(new QRCodeGenerationOptions
        {
            Text = "https://example.com",
            OutputSize = 512
        });

        using SKBitmap bitmap = SKBitmap.Decode(result.PngBytes);
        Assert.Equal(512, bitmap.Width);
        Assert.Equal(512, bitmap.Height);
        Assert.Equal(512, result.PixelWidth);
        Assert.Equal(512, result.PixelHeight);
    }

    [Fact]
    public async Task GenerateAsync_UrlType_AddsHttpsSchemeWhenMissing()
    {
        QRCodeGenerationResult result = await _service.GenerateAsync(new QRCodeGenerationOptions
        {
            ContentType = QRCodeContentType.Url,
            Text = "example.com"
        });

        Assert.Equal("https://example.com", result.Payload);
    }

    [Fact]
    public async Task GenerateAsync_WifiType_BuildsEscapedWifiPayload()
    {
        QRCodeGenerationResult result = await _service.GenerateAsync(new QRCodeGenerationOptions
        {
            ContentType = QRCodeContentType.Wifi,
            WifiSsid = "Cafe:Main;1",
            WifiPassword = "pa,ss\\word",
            WifiSecurity = QRCodeWifiSecurity.Wpa,
            WifiHidden = true
        });

        Assert.Equal("WIFI:T:WPA;S:Cafe\\:Main\\;1;P:pa\\,ss\\\\word;H:true;;", result.Payload);
    }

    [Fact]
    public async Task GenerateAsync_LowContrast_ReturnsWarning()
    {
        QRCodeGenerationResult result = await _service.GenerateAsync(new QRCodeGenerationOptions
        {
            Text = "contrast",
            DarkColor = "#777777",
            LightColor = "#888888"
        });

        Assert.True(result.ContrastRatio < 4.5d);
        Assert.NotEmpty(result.Warnings);
    }

    [Fact]
    public async Task GenerateAsync_LogoPath_ForcesHighErrorCorrectionAndEmbedsSvgImage()
    {
        string logoPath = Path.Combine(Path.GetTempPath(), $"aitebar-qr-logo-{Guid.NewGuid():N}.png");
        CreateTestLogo(logoPath);

        try
        {
            QRCodeGenerationResult result = await _service.GenerateAsync(new QRCodeGenerationOptions
            {
                Text = "https://example.com",
                EccLevel = QRCodeEccLevel.L,
                LogoPath = logoPath,
                OutputSize = 384
            });

            Assert.NotEmpty(result.PngBytes);
            Assert.Contains("data:image/png;base64,", result.SvgContent);
            Assert.Equal(QRCodeEccLevel.H, QRCodeService.NormalizeOptions(new QRCodeGenerationOptions
            {
                Text = "https://example.com",
                EccLevel = QRCodeEccLevel.L,
                LogoPath = logoPath
            }).options.EccLevel);
        }
        finally
        {
            if (File.Exists(logoPath))
            {
                File.Delete(logoPath);
            }
        }
    }

    [Theory]
    [InlineData(QRCodeModuleShape.Square)]
    [InlineData(QRCodeModuleShape.Rounded)]
    [InlineData(QRCodeModuleShape.Circle)]
    public async Task GenerateAsync_ModuleShapes_ProduceValidSvg(QRCodeModuleShape shape)
    {
        QRCodeGenerationResult result = await _service.GenerateAsync(new QRCodeGenerationOptions
        {
            Text = "shape",
            ModuleShape = shape,
            EyeStyle = QRCodeEyeStyle.Rounded
        });

        Assert.StartsWith("<svg", result.SvgContent);
        Assert.NotEmpty(result.PngBytes);
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

    private static void CreateTestLogo(string path)
    {
        using SKSurface surface = SKSurface.Create(new SKImageInfo(32, 32, SKColorType.Rgba8888, SKAlphaType.Premul))!;
        surface.Canvas.Clear(SKColors.Transparent);
        using var paint = new SKPaint { Color = SKColors.DeepSkyBlue, IsAntialias = true };
        surface.Canvas.DrawCircle(16, 16, 14, paint);
        using SKImage image = surface.Snapshot();
        using SKData data = image.Encode(SKEncodedImageFormat.Png, 100)!;
        File.WriteAllBytes(path, data.ToArray());
    }
}
