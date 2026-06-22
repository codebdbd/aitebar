using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using QRCoder;
using SkiaSharp;

namespace AiteBar;

public sealed class QRCodeService
{
    private const int MinOutputSize = 128;
    private const int MaxOutputSize = 4096;
    private const double MinRecommendedContrastRatio = 4.5d;
    private static readonly SKSamplingOptions LogoSampling = new(SKCubicResampler.Mitchell);

    public Task<QRCodeGenerationResult> GenerateAsync(
        QRCodeGenerationOptions options,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            QRCodeGenerationOptions normalizedOptions = NormalizeOptions(options);
            string payload = BuildPayload(normalizedOptions);
            using QRCodeData qrData = GenerateQrDataUnchecked(payload, normalizedOptions.EccLevel);

            cancellationToken.ThrowIfCancellationRequested();
            byte[] pngBytes = RenderPng(qrData, normalizedOptions, exactOutputSize: true);

            cancellationToken.ThrowIfCancellationRequested();
            string svg = RenderSvg(qrData, normalizedOptions, exactOutputSize: true);
            double contrastRatio = CalculateContrastRatio(normalizedOptions.DarkColor, normalizedOptions.LightColor);

            return new QRCodeGenerationResult
            {
                PngBytes = pngBytes,
                SvgContent = svg,
                ModuleCount = qrData.ModuleMatrix.Count,
                Version = GetVersion(qrData),
                PixelWidth = normalizedOptions.OutputSize,
                PixelHeight = normalizedOptions.OutputSize,
                Payload = payload,
                ContrastRatio = contrastRatio,
                Warnings = BuildWarnings(normalizedOptions, contrastRatio)
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

        var options = new QRCodeGenerationOptions
        {
            Text = "preview",
            PixelSize = pixelSize,
            OutputSize = Math.Max(MinOutputSize, (data.ModuleMatrix.Count + (margin * 2)) * pixelSize),
            Margin = margin,
            DarkColor = darkColor,
            LightColor = lightColor
        };

        return RenderPng(data, NormalizeOptions(options), exactOutputSize: false);
    }

    public string RenderSvg(QRCodeData data, int pixelSize, string darkColor, string lightColor, int margin)
    {
        ArgumentNullException.ThrowIfNull(data);
        ValidateRenderOptions(pixelSize, margin);

        var options = new QRCodeGenerationOptions
        {
            Text = "preview",
            PixelSize = pixelSize,
            OutputSize = Math.Max(MinOutputSize, (data.ModuleMatrix.Count + (margin * 2)) * pixelSize),
            Margin = margin,
            DarkColor = darkColor,
            LightColor = lightColor
        };

        return RenderSvg(data, NormalizeOptions(options), exactOutputSize: false);
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    public System.Windows.Media.DrawingImage RenderXaml(QRCodeData data, int pixelSize, string darkColor, string lightColor, int margin)
    {
        ArgumentNullException.ThrowIfNull(data);
        ValidateRenderOptions(pixelSize, margin);

        var renderer = new QRCoder.Xaml.XamlQRCode(data);
        var image = renderer.GetGraphic(
            pixelSize,
            NormalizeColor(darkColor, "#000000"),
            NormalizeColor(lightColor, "#FFFFFF"),
            drawQuietZones: margin > 0);
        image.Freeze();
        return image;
    }

    public static int GetVersion(QRCodeData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        int moduleCount = data.ModuleMatrix.Count;
        return moduleCount <= 21 ? 1 : ((moduleCount - 21) / 4) + 1;
    }

    internal static QRCodeGenerationOptions NormalizeOptions(QRCodeGenerationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        QRCodeQualityPreset preset = options.LogoPath is { Length: > 0 }
            ? QRCodeQualityPreset.Logo
            : options.QualityPreset;
        QRCodeEccLevel eccLevel = preset == QRCodeQualityPreset.Logo ? QRCodeEccLevel.H : options.EccLevel;
        int outputSize = options.OutputSize;
        if (outputSize <= 0)
        {
            outputSize = preset switch
            {
                QRCodeQualityPreset.Print => 1200,
                QRCodeQualityPreset.Logo => 1000,
                _ => 800
            };
        }

        outputSize = Math.Clamp(outputSize, MinOutputSize, MaxOutputSize);

        var normalized = new QRCodeGenerationOptions
        {
            Text = options.Text.Trim(),
            ContentType = options.ContentType,
            WifiSsid = options.WifiSsid.Trim(),
            WifiPassword = options.WifiPassword,
            WifiSecurity = options.WifiSecurity,
            WifiHidden = options.WifiHidden,
            QualityPreset = preset,
            OutputSize = outputSize,
            PixelSize = Math.Clamp(options.PixelSize, 1, 100),
            Margin = Math.Clamp(options.Margin, 0, 10),
            EccLevel = eccLevel,
            DarkColor = NormalizeColor(options.DarkColor, "#000000"),
            LightColor = NormalizeColor(options.LightColor, "#FFFFFF"),
            ModuleShape = options.ModuleShape,
            EyeStyle = options.EyeStyle,
            LogoPath = string.IsNullOrWhiteSpace(options.LogoPath) ? null : options.LogoPath.Trim(),
            LogoSizePercent = Math.Clamp(options.LogoSizePercent, 8, 20)
        };

        ValidatePayloadSource(normalized);
        if (normalized.LogoPath != null && !File.Exists(normalized.LogoPath))
        {
            throw new FileNotFoundException(LocalizationService.Get("QRCodeGenerator_ErrorLogoNotFound"), normalized.LogoPath);
        }

        return normalized;
    }

    internal static string BuildPayload(QRCodeGenerationOptions options)
    {
        QRCodeGenerationOptions normalized = NormalizeOptions(options);
        return normalized.ContentType switch
        {
            QRCodeContentType.Url => NormalizeUrlPayload(normalized.Text),
            QRCodeContentType.Wifi => BuildWifiPayload(normalized),
            _ => normalized.Text
        };
    }

    internal static double CalculateContrastRatio(string darkColor, string lightColor)
    {
        SKColor dark = ParseSkColor(darkColor, "#000000");
        SKColor light = ParseSkColor(lightColor, "#FFFFFF");
        double l1 = RelativeLuminance(dark);
        double l2 = RelativeLuminance(light);
        double lighter = Math.Max(l1, l2);
        double darker = Math.Min(l1, l2);
        return (lighter + 0.05d) / (darker + 0.05d);
    }

    private static byte[] RenderPng(QRCodeData data, QRCodeGenerationOptions options, bool exactOutputSize)
    {
        int outputSize = exactOutputSize
            ? options.OutputSize
            : Math.Max(1, (data.ModuleMatrix.Count + (options.Margin * 2)) * options.PixelSize);
        using SKSurface surface = SKSurface.Create(new SKImageInfo(outputSize, outputSize, SKColorType.Rgba8888, SKAlphaType.Premul))
            ?? throw new InvalidOperationException(LocalizationService.Get("QRCodeGenerator_ErrorGeneric"));
        SKCanvas canvas = surface.Canvas;
        SKColor darkColor = ParseSkColor(options.DarkColor, "#000000");
        SKColor lightColor = ParseSkColor(options.LightColor, "#FFFFFF");
        canvas.Clear(lightColor);

        using var paint = new SKPaint { IsAntialias = options.ModuleShape != QRCodeModuleShape.Square, Color = darkColor, Style = SKPaintStyle.Fill };
        using var lightPaint = new SKPaint { IsAntialias = true, Color = lightColor, Style = SKPaintStyle.Fill };
        float moduleSize = outputSize / (float)(data.ModuleMatrix.Count + (options.Margin * 2));
        DrawFinderPatterns(canvas, data.ModuleMatrix.Count, options.Margin, moduleSize, paint, lightPaint, options.EyeStyle);
        DrawModules(canvas, data, options.Margin, moduleSize, paint, options.ModuleShape);
        DrawLogo(canvas, outputSize, options, lightColor);
        canvas.Flush();

        using SKImage image = surface.Snapshot();
        using SKData png = image.Encode(SKEncodedImageFormat.Png, 100)
            ?? throw new InvalidOperationException(LocalizationService.Get("QRCodeGenerator_ErrorGeneric"));
        return png.ToArray();
    }

    private static string RenderSvg(QRCodeData data, QRCodeGenerationOptions options, bool exactOutputSize)
    {
        int outputSize = exactOutputSize
            ? options.OutputSize
            : Math.Max(1, (data.ModuleMatrix.Count + (options.Margin * 2)) * options.PixelSize);
        float moduleSize = outputSize / (float)(data.ModuleMatrix.Count + (options.Margin * 2));
        var svg = new StringBuilder();
        svg.Append(CultureInfo.InvariantCulture, $"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{outputSize}\" height=\"{outputSize}\" viewBox=\"0 0 {outputSize} {outputSize}\" shape-rendering=\"geometricPrecision\">\n");
        svg.Append(CultureInfo.InvariantCulture, $"  <rect width=\"100%\" height=\"100%\" fill=\"{options.LightColor}\"/>\n");
        AppendFinderPatterns(svg, data.ModuleMatrix.Count, options.Margin, moduleSize, options.DarkColor, options.LightColor, options.EyeStyle);
        AppendModules(svg, data, options.Margin, moduleSize, options.DarkColor, options.ModuleShape);
        AppendLogo(svg, outputSize, options, moduleSize);
        svg.Append("</svg>");
        return svg.ToString();
    }

    private static void DrawModules(SKCanvas canvas, QRCodeData data, int margin, float moduleSize, SKPaint paint, QRCodeModuleShape shape)
    {
        int count = data.ModuleMatrix.Count;
        for (int y = 0; y < count; y++)
        {
            for (int x = 0; x < count; x++)
            {
                if (!data.ModuleMatrix[y][x] || IsFinderPatternModule(x, y, count))
                {
                    continue;
                }

                DrawModule(canvas, (margin + x) * moduleSize, (margin + y) * moduleSize, moduleSize, paint, shape);
            }
        }
    }

    private static void DrawModule(SKCanvas canvas, float x, float y, float size, SKPaint paint, QRCodeModuleShape shape)
    {
        float inset = shape == QRCodeModuleShape.Square ? 0 : size * 0.08f;
        var rect = new SKRect(x + inset, y + inset, x + size - inset, y + size - inset);
        switch (shape)
        {
            case QRCodeModuleShape.Circle:
                canvas.DrawCircle(rect.MidX, rect.MidY, Math.Min(rect.Width, rect.Height) / 2f, paint);
                break;
            case QRCodeModuleShape.Rounded:
                canvas.DrawRoundRect(rect, size * 0.28f, size * 0.28f, paint);
                break;
            default:
                canvas.DrawRect(rect, paint);
                break;
        }
    }

    private static void DrawFinderPatterns(SKCanvas canvas, int count, int margin, float moduleSize, SKPaint darkPaint, SKPaint lightPaint, QRCodeEyeStyle eyeStyle)
    {
        DrawFinderPattern(canvas, margin, margin, moduleSize, darkPaint, lightPaint, eyeStyle);
        DrawFinderPattern(canvas, margin + count - 7, margin, moduleSize, darkPaint, lightPaint, eyeStyle);
        DrawFinderPattern(canvas, margin, margin + count - 7, moduleSize, darkPaint, lightPaint, eyeStyle);
    }

    private static void DrawFinderPattern(SKCanvas canvas, int moduleX, int moduleY, float moduleSize, SKPaint darkPaint, SKPaint lightPaint, QRCodeEyeStyle eyeStyle)
    {
        DrawEyeLayer(canvas, moduleX, moduleY, 7, moduleSize, darkPaint, eyeStyle);
        DrawEyeLayer(canvas, moduleX + 1, moduleY + 1, 5, moduleSize, lightPaint, eyeStyle);
        DrawEyeLayer(canvas, moduleX + 2, moduleY + 2, 3, moduleSize, darkPaint, eyeStyle);
    }

    private static void DrawEyeLayer(SKCanvas canvas, int moduleX, int moduleY, int modules, float moduleSize, SKPaint paint, QRCodeEyeStyle eyeStyle)
    {
        var rect = new SKRect(moduleX * moduleSize, moduleY * moduleSize, (moduleX + modules) * moduleSize, (moduleY + modules) * moduleSize);
        if (eyeStyle == QRCodeEyeStyle.Rounded)
        {
            canvas.DrawRoundRect(rect, moduleSize * 1.1f, moduleSize * 1.1f, paint);
        }
        else
        {
            canvas.DrawRect(rect, paint);
        }
    }

    private static void DrawLogo(SKCanvas canvas, int outputSize, QRCodeGenerationOptions options, SKColor backgroundColor)
    {
        if (options.LogoPath == null)
        {
            return;
        }

        using SKBitmap? logo = SKBitmap.Decode(options.LogoPath);
        if (logo == null || logo.Width <= 0 || logo.Height <= 0)
        {
            throw new InvalidDataException(LocalizationService.Get("QRCodeGenerator_ErrorInvalidLogo"));
        }

        float boxSize = outputSize * Math.Clamp(options.LogoSizePercent, 8, 20) / 100f;
        float backingSize = boxSize * 1.22f;
        var backing = CenterRect(outputSize, backingSize);
        var target = FitRect(logo.Width, logo.Height, CenterRect(outputSize, boxSize));
        using var backingPaint = new SKPaint { IsAntialias = true, Color = backgroundColor, Style = SKPaintStyle.Fill };
        canvas.DrawRoundRect(backing, backingSize * 0.16f, backingSize * 0.16f, backingPaint);
        using SKImage image = SKImage.FromBitmap(logo);
        canvas.DrawImage(image, target, LogoSampling);
    }

    private static void AppendModules(StringBuilder svg, QRCodeData data, int margin, float moduleSize, string darkColor, QRCodeModuleShape shape)
    {
        int count = data.ModuleMatrix.Count;
        for (int y = 0; y < count; y++)
        {
            for (int x = 0; x < count; x++)
            {
                if (!data.ModuleMatrix[y][x] || IsFinderPatternModule(x, y, count))
                {
                    continue;
                }

                float px = (margin + x) * moduleSize;
                float py = (margin + y) * moduleSize;
                AppendModule(svg, px, py, moduleSize, darkColor, shape);
            }
        }
    }

    private static void AppendModule(StringBuilder svg, float x, float y, float size, string color, QRCodeModuleShape shape)
    {
        float inset = shape == QRCodeModuleShape.Square ? 0 : size * 0.08f;
        float px = x + inset;
        float py = y + inset;
        float module = size - (inset * 2f);
        if (shape == QRCodeModuleShape.Circle)
        {
            svg.Append(CultureInfo.InvariantCulture, $"  <circle cx=\"{px + (module / 2f):0.###}\" cy=\"{py + (module / 2f):0.###}\" r=\"{module / 2f:0.###}\" fill=\"{color}\"/>\n");
        }
        else
        {
            float radius = shape == QRCodeModuleShape.Rounded ? size * 0.28f : 0;
            string radiusAttributes = radius > 0 ? FormattableString.Invariant($" rx=\"{radius:0.###}\" ry=\"{radius:0.###}\"") : string.Empty;
            svg.Append(CultureInfo.InvariantCulture, $"  <rect x=\"{px:0.###}\" y=\"{py:0.###}\" width=\"{module:0.###}\" height=\"{module:0.###}\"{radiusAttributes} fill=\"{color}\"/>\n");
        }
    }

    private static void AppendFinderPatterns(StringBuilder svg, int count, int margin, float moduleSize, string darkColor, string lightColor, QRCodeEyeStyle eyeStyle)
    {
        AppendFinderPattern(svg, margin, margin, moduleSize, darkColor, lightColor, eyeStyle);
        AppendFinderPattern(svg, margin + count - 7, margin, moduleSize, darkColor, lightColor, eyeStyle);
        AppendFinderPattern(svg, margin, margin + count - 7, moduleSize, darkColor, lightColor, eyeStyle);
    }

    private static void AppendFinderPattern(StringBuilder svg, int moduleX, int moduleY, float moduleSize, string darkColor, string lightColor, QRCodeEyeStyle eyeStyle)
    {
        AppendEyeLayer(svg, moduleX, moduleY, 7, moduleSize, darkColor, eyeStyle);
        AppendEyeLayer(svg, moduleX + 1, moduleY + 1, 5, moduleSize, lightColor, eyeStyle);
        AppendEyeLayer(svg, moduleX + 2, moduleY + 2, 3, moduleSize, darkColor, eyeStyle);
    }

    private static void AppendEyeLayer(StringBuilder svg, int moduleX, int moduleY, int modules, float moduleSize, string color, QRCodeEyeStyle eyeStyle)
    {
        float x = moduleX * moduleSize;
        float y = moduleY * moduleSize;
        float size = modules * moduleSize;
        string radiusAttributes = eyeStyle == QRCodeEyeStyle.Rounded
            ? FormattableString.Invariant($" rx=\"{moduleSize * 1.1f:0.###}\" ry=\"{moduleSize * 1.1f:0.###}\"")
            : string.Empty;
        svg.Append(CultureInfo.InvariantCulture, $"  <rect x=\"{x:0.###}\" y=\"{y:0.###}\" width=\"{size:0.###}\" height=\"{size:0.###}\"{radiusAttributes} fill=\"{color}\"/>\n");
    }

    private static void AppendLogo(StringBuilder svg, int outputSize, QRCodeGenerationOptions options, float moduleSize)
    {
        if (options.LogoPath == null)
        {
            return;
        }

        byte[] logoPngBytes = LoadLogoPngBytes(options.LogoPath, out int logoWidth, out int logoHeight);
        float boxSize = outputSize * Math.Clamp(options.LogoSizePercent, 8, 20) / 100f;
        float backingSize = boxSize * 1.22f;
        SKRect backing = CenterRect(outputSize, backingSize);
        SKRect target = FitRect(logoWidth, logoHeight, CenterRect(outputSize, boxSize));
        float radius = backingSize * 0.16f;
        svg.Append(CultureInfo.InvariantCulture, $"  <rect x=\"{backing.Left:0.###}\" y=\"{backing.Top:0.###}\" width=\"{backing.Width:0.###}\" height=\"{backing.Height:0.###}\" rx=\"{radius:0.###}\" ry=\"{radius:0.###}\" fill=\"{options.LightColor}\"/>\n");
        svg.Append(CultureInfo.InvariantCulture, $"  <image x=\"{target.Left:0.###}\" y=\"{target.Top:0.###}\" width=\"{target.Width:0.###}\" height=\"{target.Height:0.###}\" href=\"data:image/png;base64,{Convert.ToBase64String(logoPngBytes)}\" preserveAspectRatio=\"xMidYMid meet\"/>\n");
    }

    private static byte[] LoadLogoPngBytes(string logoPath, out int width, out int height)
    {
        using SKBitmap? logo = SKBitmap.Decode(logoPath);
        if (logo == null || logo.Width <= 0 || logo.Height <= 0)
        {
            throw new InvalidDataException(LocalizationService.Get("QRCodeGenerator_ErrorInvalidLogo"));
        }

        width = logo.Width;
        height = logo.Height;
        using SKImage image = SKImage.FromBitmap(logo);
        using SKData data = image.Encode(SKEncodedImageFormat.Png, 100)
            ?? throw new InvalidDataException(LocalizationService.Get("QRCodeGenerator_ErrorInvalidLogo"));
        return data.ToArray();
    }

    private static bool IsFinderPatternModule(int x, int y, int count) =>
        x < 7 && y < 7 ||
        x >= count - 7 && y < 7 ||
        x < 7 && y >= count - 7;

    private static SKRect CenterRect(int outputSize, float size)
    {
        float start = (outputSize - size) / 2f;
        return new SKRect(start, start, start + size, start + size);
    }

    private static SKRect FitRect(int sourceWidth, int sourceHeight, SKRect bounds)
    {
        float scale = Math.Min(bounds.Width / sourceWidth, bounds.Height / sourceHeight);
        float width = sourceWidth * scale;
        float height = sourceHeight * scale;
        return new SKRect(
            bounds.Left + ((bounds.Width - width) / 2f),
            bounds.Top + ((bounds.Height - height) / 2f),
            bounds.Left + ((bounds.Width + width) / 2f),
            bounds.Top + ((bounds.Height + height) / 2f));
    }

    private static IReadOnlyList<string> BuildWarnings(QRCodeGenerationOptions options, double contrastRatio)
    {
        var warnings = new List<string>();
        if (contrastRatio < MinRecommendedContrastRatio)
        {
            warnings.Add(LocalizationService.Format("QRCodeGenerator_WarningLowContrast", contrastRatio.ToString("0.0", CultureInfo.CurrentCulture)));
        }

        if (options.LogoPath != null && options.EccLevel != QRCodeEccLevel.H)
        {
            warnings.Add(LocalizationService.Get("QRCodeGenerator_WarningLogoRequiresHighEcc"));
        }

        return warnings;
    }

    private static void ValidatePayloadSource(QRCodeGenerationOptions options)
    {
        if (options.ContentType == QRCodeContentType.Wifi)
        {
            ValidateText(options.WifiSsid, nameof(options.WifiSsid));
            return;
        }

        ValidateText(options.Text, nameof(options.Text));
    }

    private static string NormalizeUrlPayload(string text)
    {
        string candidate = text.Trim();
        if (candidate.Contains("://", StringComparison.Ordinal))
        {
            return candidate;
        }

        return $"https://{candidate}";
    }

    private static string BuildWifiPayload(QRCodeGenerationOptions options)
    {
        string security = options.WifiSecurity switch
        {
            QRCodeWifiSecurity.None => "nopass",
            QRCodeWifiSecurity.Wep => "WEP",
            _ => "WPA"
        };

        string passwordPart = options.WifiSecurity == QRCodeWifiSecurity.None
            ? string.Empty
            : $"P:{EscapeWifiValue(options.WifiPassword)};";
        string hiddenPart = options.WifiHidden ? "H:true;" : string.Empty;
        return $"WIFI:T:{security};S:{EscapeWifiValue(options.WifiSsid)};{passwordPart}{hiddenPart};";
    }

    private static string EscapeWifiValue(string value)
    {
        var escaped = new StringBuilder(value.Length);
        foreach (char ch in value)
        {
            if (ch is '\\' or ';' or ',' or ':')
            {
                escaped.Append('\\');
            }

            escaped.Append(ch);
        }

        return escaped.ToString();
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

    private static SKColor ParseSkColor(string color, string fallback)
    {
        string normalized = NormalizeColor(color, fallback);
        return new SKColor(
            Convert.ToByte(normalized.Substring(1, 2), 16),
            Convert.ToByte(normalized.Substring(3, 2), 16),
            Convert.ToByte(normalized.Substring(5, 2), 16));
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

    private static double RelativeLuminance(SKColor color)
    {
        static double Channel(byte value)
        {
            double normalized = value / 255d;
            return normalized <= 0.03928d
                ? normalized / 12.92d
                : Math.Pow((normalized + 0.055d) / 1.055d, 2.4d);
        }

        return (0.2126d * Channel(color.Red)) + (0.7152d * Channel(color.Green)) + (0.0722d * Channel(color.Blue));
    }
}
