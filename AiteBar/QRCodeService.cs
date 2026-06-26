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
            var context = PrepareGenerationContext(options);

            cancellationToken.ThrowIfCancellationRequested();
            byte[] pngBytes = RenderPng(
                context.QrData, 
                context.NormalizedOptions.OutputSize, 
                exactSize: true,
                context.SkDarkColor, 
                context.SkLightColor, 
                context.NormalizedOptions.Margin, 
                context.NormalizedOptions.ModuleShape, 
                context.NormalizedOptions.EyeStyle, 
                context.LogoData, 
                context.NormalizedOptions.LogoSizePercent,
                cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();
            string svg = RenderSvg(
                context.QrData, 
                context.NormalizedOptions.OutputSize, 
                exactSize: true,
                context.NormalizedOptions.DarkColor, 
                context.NormalizedOptions.LightColor, 
                context.NormalizedOptions.Margin, 
                context.NormalizedOptions.ModuleShape, 
                context.NormalizedOptions.EyeStyle, 
                context.LogoData, 
                context.NormalizedOptions.LogoSizePercent,
                cancellationToken);

            return new QRCodeGenerationResult
            {
                PngBytes = pngBytes,
                SvgContent = svg,
                ModuleCount = context.ModuleCount,
                Version = context.Version,
                PixelWidth = context.PixelSize,
                PixelHeight = context.PixelSize,
                Payload = context.Payload,
                ContrastRatio = context.ContrastRatio,
                Warnings = context.Warnings
            };
        }, cancellationToken);
    }

    public Task<byte[]> GeneratePngAsync(
        QRCodeGenerationOptions options,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            var context = PrepareGenerationContext(options);

            cancellationToken.ThrowIfCancellationRequested();
            return RenderPng(
                context.QrData, 
                context.NormalizedOptions.OutputSize, 
                exactSize: true,
                context.SkDarkColor, 
                context.SkLightColor, 
                context.NormalizedOptions.Margin, 
                context.NormalizedOptions.ModuleShape, 
                context.NormalizedOptions.EyeStyle, 
                context.LogoData, 
                context.NormalizedOptions.LogoSizePercent,
                cancellationToken);
        }, cancellationToken);
    }

    public Task<string> GenerateSvgAsync(
        QRCodeGenerationOptions options,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            var context = PrepareGenerationContext(options);

            cancellationToken.ThrowIfCancellationRequested();
            return RenderSvg(
                context.QrData, 
                context.NormalizedOptions.OutputSize, 
                exactSize: true,
                context.NormalizedOptions.DarkColor, 
                context.NormalizedOptions.LightColor, 
                context.NormalizedOptions.Margin, 
                context.NormalizedOptions.ModuleShape, 
                context.NormalizedOptions.EyeStyle, 
                context.LogoData, 
                context.NormalizedOptions.LogoSizePercent,
                cancellationToken);
        }, cancellationToken);
    }

    private sealed class GenerationContext
    {
        public QRCodeGenerationOptions NormalizedOptions { get; init; } = null!;
        public QRCodeData QrData { get; init; } = null!;
        public int ModuleCount { get; init; }
        public int Version { get; init; }
        public int PixelSize { get; init; }
        public string Payload { get; init; } = null!;
        public double ContrastRatio { get; init; }
        public IReadOnlyList<string> Warnings { get; init; } = null!;
        public LogoData? LogoData { get; init; }
        public SKColor SkDarkColor { get; init; }
        public SKColor SkLightColor { get; init; }
    }

    private sealed class LogoData
    {
        public byte[] PngBytes { get; }
        public int Width { get; }
        public int Height { get; }

        public LogoData(byte[] pngBytes, int width, int height)
        {
            PngBytes = pngBytes;
            Width = width;
            Height = height;
        }
    }

    private GenerationContext PrepareGenerationContext(QRCodeGenerationOptions options)
    {
        var (normalizedOptions, darkInvalid, lightInvalid) = NormalizeOptions(options);
        string payload = BuildPayload(normalizedOptions);
        QRCodeData qrData = GenerateQrDataUnchecked(payload, normalizedOptions.EccLevel);
        
        SKColor skDarkColor = ParseNormalizedSkColor(normalizedOptions.DarkColor);
        SKColor skLightColor = ParseNormalizedSkColor(normalizedOptions.LightColor);
        double contrastRatio = CalculateContrastRatio(skDarkColor, skLightColor);
        
        LogoData? logoData = normalizedOptions.LogoPath != null ? LoadLogoData(normalizedOptions.LogoPath) : null;

        return new GenerationContext
        {
            NormalizedOptions = normalizedOptions,
            QrData = qrData,
            ModuleCount = qrData.ModuleMatrix.Count,
            Version = GetVersion(qrData),
            PixelSize = normalizedOptions.OutputSize,
            Payload = payload,
            ContrastRatio = contrastRatio,
            Warnings = BuildWarnings(normalizedOptions, contrastRatio, darkInvalid, lightInvalid),
            LogoData = logoData,
            SkDarkColor = skDarkColor,
            SkLightColor = skLightColor
        };
    }

    private static LogoData LoadLogoData(string logoPath)
    {
        using SKBitmap? logo = SKBitmap.Decode(logoPath);
        if (logo == null || logo.Width <= 0 || logo.Height <= 0)
        {
            throw new InvalidDataException(LocalizationService.Get("QRCodeGenerator_ErrorInvalidLogo"));
        }

        int width = logo.Width;
        int height = logo.Height;
        using SKImage image = SKImage.FromBitmap(logo);
        using SKData data = image.Encode(SKEncodedImageFormat.Png, 100)
            ?? throw new InvalidDataException(LocalizationService.Get("QRCodeGenerator_ErrorInvalidLogo"));
        return new LogoData(data.ToArray(), width, height);
    }

    public QRCodeData GenerateQrData(string text, QRCodeEccLevel eccLevel = QRCodeEccLevel.Q)
    {
        ValidateText(text, nameof(text));

        return GenerateQrDataUnchecked(text, eccLevel);
    }

    public byte[] RenderPng(QRCodeData data, int pixelSize, string darkColor, string lightColor, int margin, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(data);
        ValidateRenderOptions(pixelSize, margin);

        return RenderPng(data, pixelSize, exactSize: false, darkColor, lightColor, margin, QRCodeModuleShape.Square, QRCodeEyeStyle.Square, logoData: null, cancellationToken: cancellationToken);
    }

    public string RenderSvg(QRCodeData data, int pixelSize, string darkColor, string lightColor, int margin, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(data);
        ValidateRenderOptions(pixelSize, margin);

        return RenderSvg(data, pixelSize, exactSize: false, darkColor, lightColor, margin, QRCodeModuleShape.Square, QRCodeEyeStyle.Square, logoData: null, cancellationToken: cancellationToken);
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    public System.Windows.Media.DrawingImage RenderXaml(QRCodeData data, int pixelSize, string darkColor, string lightColor, int margin)
    {
        ArgumentNullException.ThrowIfNull(data);
        ValidateRenderOptions(pixelSize, margin);

        var renderer = new QRCoder.Xaml.XamlQRCode(data);
        var (dark, _) = NormalizeColor(darkColor, "#000000");
        var (light, _) = NormalizeColor(lightColor, "#FFFFFF");
        var image = renderer.GetGraphic(
            pixelSize,
            dark,
            light,
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

    internal static (QRCodeGenerationOptions options, bool darkInvalid, bool lightInvalid) NormalizeOptions(QRCodeGenerationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        QRCodeQualityPreset preset = options.LogoPath is { Length: > 0 }
            ? QRCodeQualityPreset.Logo
            : options.QualityPreset;
        QRCodeEccLevel eccLevel = preset == QRCodeQualityPreset.Logo ? QRCodeEccLevel.H : options.EccLevel;
        int outputSize = options.OutputSize;
        if (outputSize <= 0)
        {
            outputSize = preset.GetOutputSize();
        }

        outputSize = Math.Clamp(outputSize, MinOutputSize, MaxOutputSize);

        var (darkColor, darkInvalid) = NormalizeColor(options.DarkColor, "#000000");
        var (lightColor, lightInvalid) = NormalizeColor(options.LightColor, "#FFFFFF");

        var normalized = new QRCodeGenerationOptions
        {
            Text = options.Text.Trim(),
            ContentType = options.ContentType,
            WifiSsid = options.WifiSsid.Trim(),
            WifiPassword = options.WifiPassword,
            WifiSecurity = options.WifiSecurity,
            WifiHidden = options.WifiHidden,
            EmailAddress = options.EmailAddress.Trim(),
            EmailSubject = options.EmailSubject.Trim(),
            EmailBody = options.EmailBody,
            PhoneNumber = options.PhoneNumber.Trim(),
            SmsMessage = options.SmsMessage,
            VCardFirstName = options.VCardFirstName.Trim(),
            VCardLastName = options.VCardLastName.Trim(),
            VCardPhone = options.VCardPhone.Trim(),
            VCardEmail = options.VCardEmail.Trim(),
            VCardCompany = options.VCardCompany.Trim(),
            VCardJobTitle = options.VCardJobTitle.Trim(),
            VCardWebsite = options.VCardWebsite.Trim(),
            QualityPreset = preset,
            OutputSize = outputSize,
            PixelSize = Math.Clamp(options.PixelSize, 1, 100),
            Margin = Math.Clamp(options.Margin, 0, 10),
            EccLevel = eccLevel,
            DarkColor = darkColor,
            LightColor = lightColor,
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

        return (normalized, darkInvalid, lightInvalid);
    }

    internal static string BuildPayload(QRCodeGenerationOptions normalizedOptions)
    {
        return normalizedOptions.ContentType switch
        {
            QRCodeContentType.Url => NormalizeUrlPayload(normalizedOptions.Text),
            QRCodeContentType.Wifi => BuildWifiPayload(normalizedOptions),
            QRCodeContentType.Email => BuildEmailPayload(normalizedOptions),
            QRCodeContentType.Phone => string.IsNullOrWhiteSpace(normalizedOptions.PhoneNumber) ? string.Empty : $"tel:{normalizedOptions.PhoneNumber}",
            QRCodeContentType.Sms => BuildSmsPayload(normalizedOptions),
            QRCodeContentType.VCard => BuildVCardPayload(normalizedOptions),
            _ => normalizedOptions.Text
        };
    }

    internal static double CalculateContrastRatio(string darkColor, string lightColor)
    {
        SKColor dark = ParseSkColor(darkColor, "#000000");
        SKColor light = ParseSkColor(lightColor, "#FFFFFF");
        return CalculateContrastRatio(dark, light);
    }

    private static double CalculateContrastRatio(SKColor dark, SKColor light)
    {
        double l1 = RelativeLuminance(dark);
        double l2 = RelativeLuminance(light);
        double lighter = Math.Max(l1, l2);
        double darker = Math.Min(l1, l2);
        return (lighter + 0.05d) / (darker + 0.05d);
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
            case QRCodeModuleShape.Dot:
                canvas.DrawCircle(x + size / 2f, y + size / 2f, size * 0.25f, paint);
                break;
            case QRCodeModuleShape.Circle:
                canvas.DrawCircle(rect.MidX, rect.MidY, Math.Min(rect.Width, rect.Height) / 2f, paint);
                break;
            case QRCodeModuleShape.Diamond:
                var diamondPath = new SKPath();
                diamondPath.MoveTo(rect.MidX, rect.Top);
                diamondPath.LineTo(rect.Right, rect.MidY);
                diamondPath.LineTo(rect.MidX, rect.Bottom);
                diamondPath.LineTo(rect.Left, rect.MidY);
                diamondPath.Close();
                canvas.DrawPath(diamondPath, paint);
                diamondPath.Dispose();
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
        if (eyeStyle == QRCodeEyeStyle.Diamond)
        {
            var diamondPath = new SKPath();
            diamondPath.MoveTo(rect.MidX, rect.Top);
            diamondPath.LineTo(rect.Right, rect.MidY);
            diamondPath.LineTo(rect.MidX, rect.Bottom);
            diamondPath.LineTo(rect.Left, rect.MidY);
            diamondPath.Close();
            canvas.DrawPath(diamondPath, paint);
            diamondPath.Dispose();
        }
        else if (eyeStyle == QRCodeEyeStyle.Circle)
        {
            canvas.DrawCircle(rect.MidX, rect.MidY, Math.Min(rect.Width, rect.Height) / 2f, paint);
        }
        else if (eyeStyle == QRCodeEyeStyle.Rounded)
        {
            canvas.DrawRoundRect(rect, moduleSize * 1.1f, moduleSize * 1.1f, paint);
        }
        else
        {
            canvas.DrawRect(rect, paint);
        }
    }

    private static void DrawLogo(SKCanvas canvas, int outputSize, int logoSizePercent, SKColor backgroundColor, LogoData? logoData)
    {
        if (logoData == null)
        {
            return;
        }

        using SKBitmap? logo = SKBitmap.Decode(logoData.PngBytes);
        if (logo == null || logo.Width <= 0 || logo.Height <= 0)
        {
            throw new InvalidDataException(LocalizationService.Get("QRCodeGenerator_ErrorInvalidLogo"));
        }

        float boxSize = outputSize * Math.Clamp(logoSizePercent, 8, 20) / 100f;
        float backingSize = boxSize * 1.22f;
        var backing = CenterRect(outputSize, backingSize);
        var target = FitRect(logo.Width, logo.Height, CenterRect(outputSize, boxSize));
        using var backingPaint = new SKPaint { IsAntialias = true, Color = backgroundColor, Style = SKPaintStyle.Fill };
        canvas.DrawRoundRect(backing, backingSize * 0.16f, backingSize * 0.16f, backingPaint);
        using SKImage image = SKImage.FromBitmap(logo);
        canvas.DrawImage(image, target, LogoSampling);
    }

    private static byte[] RenderPng(QRCodeData data, int size, bool exactSize, string darkColor, string lightColor, int margin, QRCodeModuleShape moduleShape, QRCodeEyeStyle eyeStyle, LogoData? logoData, int logoSizePercent = 20, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        SKColor skDark = ParseSkColor(darkColor, "#000000");
        SKColor skLight = ParseSkColor(lightColor, "#FFFFFF");
        return RenderPng(data, size, exactSize, skDark, skLight, margin, moduleShape, eyeStyle, logoData, logoSizePercent, cancellationToken);
    }

    private static byte[] RenderPng(QRCodeData data, int size, bool exactSize, SKColor skDarkColor, SKColor skLightColor, int margin, QRCodeModuleShape moduleShape, QRCodeEyeStyle eyeStyle, LogoData? logoData, int logoSizePercent = 20, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        int outputSize = exactSize 
            ? size 
            : Math.Max(1, (data.ModuleMatrix.Count + (margin * 2)) * size);
        using SKSurface surface = SKSurface.Create(new SKImageInfo(outputSize, outputSize, SKColorType.Rgba8888, SKAlphaType.Premul))
            ?? throw new InvalidDataException(LocalizationService.Get("QRCodeGenerator_ErrorGeneric"));
        SKCanvas canvas = surface.Canvas;
        canvas.Clear(skLightColor);

        using var paint = new SKPaint { IsAntialias = moduleShape != QRCodeModuleShape.Square, Color = skDarkColor, Style = SKPaintStyle.Fill };
        using var lightPaint = new SKPaint { IsAntialias = true, Color = skLightColor, Style = SKPaintStyle.Fill };
        float moduleSize = outputSize / (float)(data.ModuleMatrix.Count + (margin * 2));
        DrawFinderPatterns(canvas, data.ModuleMatrix.Count, margin, moduleSize, paint, lightPaint, eyeStyle);
        cancellationToken.ThrowIfCancellationRequested();
        DrawModules(canvas, data, margin, moduleSize, paint, moduleShape);
        cancellationToken.ThrowIfCancellationRequested();
        DrawLogo(canvas, outputSize, logoSizePercent, skLightColor, logoData);
        cancellationToken.ThrowIfCancellationRequested();
        canvas.Flush();

        using SKImage image = surface.Snapshot();
        using SKData png = image.Encode(SKEncodedImageFormat.Png, 100)
            ?? throw new InvalidDataException(LocalizationService.Get("QRCodeGenerator_ErrorGeneric"));
        return png.ToArray();
    }



    private static void AppendLogo(StringBuilder svg, int outputSize, int logoSizePercent, string lightColor, float moduleSize, LogoData? logoData)
    {
        if (logoData == null)
        {
            return;
        }

        float boxSize = outputSize * Math.Clamp(logoSizePercent, 8, 20) / 100f;
        float backingSize = boxSize * 1.22f;
        SKRect backing = CenterRect(outputSize, backingSize);
        SKRect target = FitRect(logoData.Width, logoData.Height, CenterRect(outputSize, boxSize));
        float radius = backingSize * 0.16f;
        svg.Append(CultureInfo.InvariantCulture, $"  <rect x=\"{backing.Left:0.###}\" y=\"{backing.Top:0.###}\" width=\"{backing.Width:0.###}\" height=\"{backing.Height:0.###}\" rx=\"{radius:0.###}\" ry=\"{radius:0.###}\" fill=\"{lightColor}\"/>\n");
        svg.Append(CultureInfo.InvariantCulture, $"  <image x=\"{target.Left:0.###}\" y=\"{target.Top:0.###}\" width=\"{target.Width:0.###}\" height=\"{target.Height:0.###}\" href=\"data:image/png;base64,{Convert.ToBase64String(logoData.PngBytes)}\" preserveAspectRatio=\"xMidYMid meet\"/>\n");
    }

    private static string RenderSvg(QRCodeData data, int size, bool exactSize, string darkColor, string lightColor, int margin, QRCodeModuleShape moduleShape, QRCodeEyeStyle eyeStyle, LogoData? logoData, int logoSizePercent = 20, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        int outputSize = exactSize 
            ? size 
            : Math.Max(1, (data.ModuleMatrix.Count + (margin * 2)) * size);
        float moduleSize = outputSize / (float)(data.ModuleMatrix.Count + (margin * 2));
        var svg = new StringBuilder();
        svg.Append(CultureInfo.InvariantCulture, $"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{outputSize}\" height=\"{outputSize}\" viewBox=\"0 0 {outputSize} {outputSize}\" shape-rendering=\"geometricPrecision\">\n");
        svg.Append(CultureInfo.InvariantCulture, $"  <rect width=\"100%\" height=\"100%\" fill=\"{lightColor}\"/>\n");
        AppendFinderPatterns(svg, data.ModuleMatrix.Count, margin, moduleSize, darkColor, lightColor, eyeStyle);
        cancellationToken.ThrowIfCancellationRequested();
        AppendModules(svg, data, margin, moduleSize, darkColor, moduleShape);
        cancellationToken.ThrowIfCancellationRequested();
        AppendLogo(svg, outputSize, logoSizePercent, lightColor, moduleSize, logoData);
        svg.Append("</svg>");
        return svg.ToString();
    }

    private static void AppendModules(StringBuilder svg, QRCodeData data, int margin, float moduleSize, string darkColor, QRCodeModuleShape shape)
    {
        if (shape == QRCodeModuleShape.Square)
        {
            AppendOptimizedSquareModules(svg, data, margin, moduleSize, darkColor);
        }
        else
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
    }

    private static void AppendOptimizedSquareModules(StringBuilder svg, QRCodeData data, int margin, float moduleSize, string darkColor)
    {
        int count = data.ModuleMatrix.Count;
        bool[,] visited = new bool[count, count];

        for (int y = 0; y < count; y++)
        {
            for (int x = 0; x < count; x++)
            {
                if (visited[y, x] || !data.ModuleMatrix[y][x] || IsFinderPatternModule(x, y, count))
                {
                    continue;
                }

                // Expand right as much as possible
                int width = 1;
                while (x + width < count && 
                       !visited[y, x + width] && 
                       data.ModuleMatrix[y][x + width] && 
                       !IsFinderPatternModule(x + width, y, count))
                {
                    width++;
                }

                // Now expand down as much as possible for this width
                int height = 1;
                bool canExpandDown = true;
                while (canExpandDown && y + height < count)
                {
                    for (int dx = 0; dx < width; dx++)
                    {
                        if (visited[y + height, x + dx] || 
                            !data.ModuleMatrix[y + height][x + dx] || 
                            IsFinderPatternModule(x + dx, y + height, count))
                        {
                            canExpandDown = false;
                            break;
                        }
                    }

                    if (canExpandDown)
                    {
                        height++;
                    }
                }

                // Mark all modules in this rectangle as visited
                for (int dy = 0; dy < height; dy++)
                {
                    for (int dx = 0; dx < width; dx++)
                    {
                        visited[y + dy, x + dx] = true;
                    }
                }

                // Append the merged rectangle
                float px = (margin + x) * moduleSize;
                float py = (margin + y) * moduleSize;
                float w = width * moduleSize;
                float h = height * moduleSize;
                svg.Append(CultureInfo.InvariantCulture, $"  <rect x=\"{px:0.###}\" y=\"{py:0.###}\" width=\"{w:0.###}\" height=\"{h:0.###}\" fill=\"{darkColor}\"/>\n");
            }
        }
    }

    private static void AppendModule(StringBuilder svg, float x, float y, float size, string color, QRCodeModuleShape shape)
    {
        float inset = shape == QRCodeModuleShape.Square ? 0 : size * 0.08f;
        float px = x + inset;
        float py = y + inset;
        float module = size - (inset * 2f);
        
        if (shape == QRCodeModuleShape.Dot)
        {
            float cx = x + size / 2f;
            float cy = y + size / 2f;
            float r = size * 0.25f;
            svg.Append(CultureInfo.InvariantCulture, $"  <circle cx=\"{cx:0.###}\" cy=\"{cy:0.###}\" r=\"{r:0.###}\" fill=\"{color}\"/>\n");
        }
        else if (shape == QRCodeModuleShape.Circle)
        {
            svg.Append(CultureInfo.InvariantCulture, $"  <circle cx=\"{px + (module / 2f):0.###}\" cy=\"{py + (module / 2f):0.###}\" r=\"{module / 2f:0.###}\" fill=\"{color}\"/>\n");
        }
        else if (shape == QRCodeModuleShape.Diamond)
        {
            float cx = px + module / 2f;
            float cy = py + module / 2f;
            float half = module / 2f;
            string points = FormattableString.Invariant($"{cx:0.###},{py:0.###} {px + module:0.###},{cy:0.###} {cx:0.###},{py + module:0.###} {px:0.###},{cy:0.###}");
            svg.Append(CultureInfo.InvariantCulture, $"  <polygon points=\"{points}\" fill=\"{color}\"/>\n");
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
        
        if (eyeStyle == QRCodeEyeStyle.Diamond)
        {
            float cx = x + size / 2f;
            float cy = y + size / 2f;
            string points = FormattableString.Invariant($"{cx:0.###},{y:0.###} {x + size:0.###},{cy:0.###} {cx:0.###},{y + size:0.###} {x:0.###},{cy:0.###}");
            svg.Append(CultureInfo.InvariantCulture, $"  <polygon points=\"{points}\" fill=\"{color}\"/>\n");
        }
        else if (eyeStyle == QRCodeEyeStyle.Circle)
        {
            float cx = x + (size / 2f);
            float cy = y + (size / 2f);
            float r = size / 2f;
            svg.Append(CultureInfo.InvariantCulture, $"  <circle cx=\"{cx:0.###}\" cy=\"{cy:0.###}\" r=\"{r:0.###}\" fill=\"{color}\"/>\n");
        }
        else
        {
            string radiusAttributes = eyeStyle == QRCodeEyeStyle.Rounded
                ? FormattableString.Invariant($" rx=\"{moduleSize * 1.1f:0.###}\" ry=\"{moduleSize * 1.1f:0.###}\"")
                : string.Empty;
            svg.Append(CultureInfo.InvariantCulture, $"  <rect x=\"{x:0.###}\" y=\"{y:0.###}\" width=\"{size:0.###}\" height=\"{size:0.###}\"{radiusAttributes} fill=\"{color}\"/>\n");
        }
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

    private static IReadOnlyList<string> BuildWarnings(QRCodeGenerationOptions options, double contrastRatio, bool darkInvalid, bool lightInvalid)
    {
        var warnings = new List<string>();
        if (contrastRatio < MinRecommendedContrastRatio)
        {
            warnings.Add(LocalizationService.Format("QRCodeGenerator_WarningLowContrast", contrastRatio.ToString("0.0", CultureInfo.CurrentCulture)));
        }

        if (options.LogoPath != null)
        {
            if (options.EccLevel != QRCodeEccLevel.H)
            {
                warnings.Add(LocalizationService.Get("QRCodeGenerator_WarningLogoRequiresHighEcc"));
            }
            if (options.LogoSizePercent > 15)
            {
                warnings.Add(LocalizationService.Format("QRCodeGenerator_WarningLargeLogo", options.LogoSizePercent.ToString(CultureInfo.CurrentCulture)));
            }
        }

        if (darkInvalid)
        {
            warnings.Add(LocalizationService.Get("QRCodeGenerator_WarningInvalidDarkColor"));
        }

        if (lightInvalid)
        {
            warnings.Add(LocalizationService.Get("QRCodeGenerator_WarningInvalidLightColor"));
        }

        return warnings;
    }

    private static void ValidatePayloadSource(QRCodeGenerationOptions options)
    {
        string payload = BuildPayload(options);
        
        if (string.IsNullOrWhiteSpace(payload))
        {
            string parameterName = options.ContentType switch
            {
                QRCodeContentType.Wifi => nameof(options.WifiSsid),
                QRCodeContentType.Email => nameof(options.EmailAddress),
                QRCodeContentType.Phone => nameof(options.PhoneNumber),
                QRCodeContentType.Sms => nameof(options.PhoneNumber),
                QRCodeContentType.VCard => nameof(options.VCardFirstName),
                _ => nameof(options.Text)
            };
            throw new ArgumentException(LocalizationService.Get("QRCodeGenerator_ErrorEmptyText"), parameterName);
        }

        // Максимальное значение для QR-кода версии 40 (самой большой) с уровнем коррекции ошибок L (Low),
        // в режиме кодирования alphanumeric (наиболее распространенный общий случай).
        // В других режимах лимит может быть больше (numeric: 7089, byte: 2953, kanji: 1817),
        // но для универсальности используем наиболее строгий лимит среди общих сценариев.
        if (payload.Length > 4296)
        {
            string parameterName = options.ContentType switch
            {
                QRCodeContentType.Wifi => nameof(options.WifiSsid),
                QRCodeContentType.Email => nameof(options.EmailAddress),
                QRCodeContentType.Phone => nameof(options.PhoneNumber),
                QRCodeContentType.Sms => nameof(options.PhoneNumber),
                QRCodeContentType.VCard => nameof(options.VCardFirstName),
                _ => nameof(options.Text)
            };
            throw new ArgumentException(LocalizationService.Format("QRCodeGenerator_ErrorTextTooLong", 4296), parameterName);
        }
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

    private static string BuildEmailPayload(QRCodeGenerationOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.EmailAddress))
            return string.Empty;
        var uri = new StringBuilder($"mailto:{options.EmailAddress}");
        bool hasSubject = !string.IsNullOrWhiteSpace(options.EmailSubject);
        bool hasBody = !string.IsNullOrWhiteSpace(options.EmailBody);
        
        if (hasSubject || hasBody)
        {
            uri.Append('?');
            if (hasSubject)
            {
                uri.Append($"subject={Uri.EscapeDataString(options.EmailSubject)}");
            }
            if (hasBody)
            {
                if (hasSubject) uri.Append('&');
                uri.Append($"body={Uri.EscapeDataString(options.EmailBody)}");
            }
        }
        return uri.ToString();
    }

    private static string BuildSmsPayload(QRCodeGenerationOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.PhoneNumber))
            return string.Empty;
        return $"SMSTO:{options.PhoneNumber}:{options.SmsMessage}";
    }

    private static string BuildVCardPayload(QRCodeGenerationOptions options)
    {
        bool hasFirst = !string.IsNullOrWhiteSpace(options.VCardFirstName);
        bool hasLast = !string.IsNullOrWhiteSpace(options.VCardLastName);
        if (!hasFirst && !hasLast && string.IsNullOrWhiteSpace(options.VCardCompany))
            return string.Empty;

        var sb = new StringBuilder();
        sb.Append("BEGIN:VCARD\nVERSION:3.0\n");
        sb.Append($"N:{options.VCardLastName};{options.VCardFirstName};;;\n");
        
        string fn = string.Join(" ", new[] { options.VCardFirstName, options.VCardLastName }
            .Where(s => !string.IsNullOrWhiteSpace(s)));
            
        if (!string.IsNullOrWhiteSpace(fn))
            sb.Append($"FN:{fn}\n");
            
        if (!string.IsNullOrWhiteSpace(options.VCardCompany))
            sb.Append($"ORG:{options.VCardCompany}\n");
            
        if (!string.IsNullOrWhiteSpace(options.VCardJobTitle))
            sb.Append($"TITLE:{options.VCardJobTitle}\n");
            
        if (!string.IsNullOrWhiteSpace(options.VCardPhone))
            sb.Append($"TEL;TYPE=CELL:{options.VCardPhone}\n");
            
        if (!string.IsNullOrWhiteSpace(options.VCardEmail))
            sb.Append($"EMAIL;TYPE=PREF,INTERNET:{options.VCardEmail}\n");
            
        if (!string.IsNullOrWhiteSpace(options.VCardWebsite))
            sb.Append($"URL:{options.VCardWebsite}\n");

        sb.Append("END:VCARD\n");
        return sb.ToString();
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

        // Максимальное значение для QR-кода версии 40 (самой большой) с уровнем коррекции ошибок L (Low),
        // в режиме кодирования alphanumeric (наиболее распространенный общий случай).
        // В других режимах лимит может быть больше (numeric: 7089, byte: 2953, kanji: 1817),
        // но для универсальности используем наиболее строгий лимит среди общих сценариев.
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
        var (normalized, _) = NormalizeColor(color, fallback);
        return ParseNormalizedSkColor(normalized);
    }

    private static SKColor ParseNormalizedSkColor(string normalizedColor)
    {
        return new SKColor(
            Convert.ToByte(normalizedColor.Substring(1, 2), 16),
            Convert.ToByte(normalizedColor.Substring(3, 2), 16),
            Convert.ToByte(normalizedColor.Substring(5, 2), 16));
    }

    private static (string normalizedColor, bool wasInvalid) NormalizeColor(string? color, string fallback)
    {
        string candidate = string.IsNullOrWhiteSpace(color) ? fallback : color.Trim();
        if (!candidate.StartsWith('#'))
        {
            candidate = $"#{candidate}";
        }

        bool invalid = false;
        if (candidate.Length != 7)
        {
            invalid = true;
        }
        else
        {
            for (int i = 1; i < candidate.Length; i++)
            {
                char ch = candidate[i];
                bool isHex = ch is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F';
                if (!isHex)
                {
                    invalid = true;
                    break;
                }
            }
        }

        if (invalid)
        {
            return (fallback, true);
        }

        return (candidate.ToUpperInvariant(), false);
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
