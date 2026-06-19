using System;
using System.Threading;
using System.Threading.Tasks;
using QRCoder;

namespace AiteBar;

public sealed class QRCodeService
{
    public Task<QRCodeGenerationResult> GenerateAsync(
        QRCodeGenerationOptions options,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            QRCodeGenerationOptions normalizedOptions = NormalizeOptions(options);
            using QRCodeData qrData = GenerateQrDataUnchecked(normalizedOptions.Text, normalizedOptions.EccLevel);

            cancellationToken.ThrowIfCancellationRequested();
            byte[] pngBytes = RenderPng(
                qrData,
                normalizedOptions.PixelSize,
                normalizedOptions.DarkColor,
                normalizedOptions.LightColor,
                normalizedOptions.Margin);

            cancellationToken.ThrowIfCancellationRequested();
            string svg = RenderSvg(
                qrData,
                normalizedOptions.PixelSize,
                normalizedOptions.DarkColor,
                normalizedOptions.LightColor,
                normalizedOptions.Margin);

            return new QRCodeGenerationResult
            {
                PngBytes = pngBytes,
                SvgContent = svg,
                ModuleCount = qrData.ModuleMatrix.Count,
                Version = GetVersion(qrData)
            };
        }, cancellationToken);
    }

    public QRCodeData GenerateQrData(string text, QRCodeEccLevel eccLevel = QRCodeEccLevel.Q)
    {
        ValidateText(text, nameof(text));

        return GenerateQrDataUnchecked(text, eccLevel);
    }

    public byte[] RenderPng(QRCodeData data, int pixelSize, string darkColor, string lightColor, int margin)
    {
        ArgumentNullException.ThrowIfNull(data);
        ValidateRenderOptions(pixelSize, margin);

        var renderer = new PngByteQRCode(data);
        return renderer.GetGraphic(
            pixelSize,
            ParseColorBytes(darkColor),
            ParseColorBytes(lightColor),
            drawQuietZones: margin > 0);
    }

    public string RenderSvg(QRCodeData data, int pixelSize, string darkColor, string lightColor, int margin)
    {
        ArgumentNullException.ThrowIfNull(data);
        ValidateRenderOptions(pixelSize, margin);

        var renderer = new SvgQRCode(data);
        return renderer.GetGraphic(
            pixelSize,
            NormalizeColor(darkColor, "#000000"),
            NormalizeColor(lightColor, "#FFFFFF"),
            drawQuietZones: margin > 0);
    }

    public static int GetVersion(QRCodeData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        int moduleCount = data.ModuleMatrix.Count;
        return moduleCount <= 21 ? 1 : ((moduleCount - 21) / 4) + 1;
    }

    private static QRCodeGenerationOptions NormalizeOptions(QRCodeGenerationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ValidateText(options.Text, nameof(options));

        ValidateRenderOptions(options.PixelSize, options.Margin);

        return new QRCodeGenerationOptions
        {
            Text = options.Text,
            PixelSize = options.PixelSize,
            Margin = options.Margin,
            EccLevel = options.EccLevel,
            DarkColor = NormalizeColor(options.DarkColor, "#000000"),
            LightColor = NormalizeColor(options.LightColor, "#FFFFFF")
        };
    }

    private static void ValidateRenderOptions(int pixelSize, int margin)
    {
        if (pixelSize < 1 || pixelSize > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(pixelSize));
        }

        if (margin < 0 || margin > 10)
        {
            throw new ArgumentOutOfRangeException(nameof(margin));
        }
    }

    private static void ValidateText(string text, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException(LocalizationService.Get("QRCodeGenerator_ErrorEmptyText"), parameterName);
        }

        if (text.Length > 4296)
        {
            throw new ArgumentException(LocalizationService.Format("QRCodeGenerator_ErrorTextTooLong", 4296), parameterName);
        }
    }

    private static QRCodeData GenerateQrDataUnchecked(string text, QRCodeEccLevel eccLevel) =>
        QRCodeGenerator.GenerateQrCode(text, MapEccLevel(eccLevel));

    private static QRCodeGenerator.ECCLevel MapEccLevel(QRCodeEccLevel eccLevel) =>
        eccLevel switch
        {
            QRCodeEccLevel.L => QRCodeGenerator.ECCLevel.L,
            QRCodeEccLevel.M => QRCodeGenerator.ECCLevel.M,
            QRCodeEccLevel.Q => QRCodeGenerator.ECCLevel.Q,
            QRCodeEccLevel.H => QRCodeGenerator.ECCLevel.H,
            _ => QRCodeGenerator.ECCLevel.Q
        };

    private static byte[] ParseColorBytes(string color)
    {
        string normalized = NormalizeColor(color, "#000000")[1..];
        return
        [
            Convert.ToByte(normalized[..2], 16),
            Convert.ToByte(normalized.Substring(2, 2), 16),
            Convert.ToByte(normalized.Substring(4, 2), 16)
        ];
    }

    private static string NormalizeColor(string? color, string fallback)
    {
        string candidate = string.IsNullOrWhiteSpace(color) ? fallback : color.Trim();
        if (!candidate.StartsWith('#'))
        {
            candidate = $"#{candidate}";
        }

        if (candidate.Length != 7)
        {
            return fallback;
        }

        for (int i = 1; i < candidate.Length; i++)
        {
            char ch = candidate[i];
            bool isHex = ch is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F';
            if (!isHex)
            {
                return fallback;
            }
        }

        return candidate.ToUpperInvariant();
    }
}
