using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using QRCoder;
using SkiaSharp;
using Svg.Skia;
using System.Xml.Linq;

namespace AiteBar;

public sealed class QRCodeService
{
    private const int MinOutputSize = 128;
    private const int MaxOutputSize = 4096;
    // Conservative cross-content payload limit to fail fast before QRCoder throws on oversized inputs.
    private const int MaxPayloadLength = 4296;
    private const double MinRecommendedContrastRatio = 4.5d;
    private static readonly SKSamplingOptions LogoSampling = new(SKCubicResampler.Mitchell);

    public Task<QRCodeGenerationResult> GenerateAsync(
        QRCodeGenerationOptions options,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            using var context = PrepareGenerationContext(options);

            cancellationToken.ThrowIfCancellationRequested();
            byte[] pngBytes = RenderPng(
                context.QrData,
                context.NormalizedOptions.OutputSize,
                exactSize: true,
                context.SkDarkColor,
                context.SkLightColor,
                context.NormalizedOptions.Margin,
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
            using var context = PrepareGenerationContext(options);

            cancellationToken.ThrowIfCancellationRequested();
            return RenderPng(
                context.QrData,
                context.NormalizedOptions.OutputSize,
                exactSize: true,
                context.SkDarkColor,
                context.SkLightColor,
                context.NormalizedOptions.Margin,
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
            using var context = PrepareGenerationContext(options);

            cancellationToken.ThrowIfCancellationRequested();
            return RenderSvg(
                context.QrData,
                context.NormalizedOptions.OutputSize,
                exactSize: true,
                context.NormalizedOptions.DarkColor,
                context.NormalizedOptions.LightColor,
                context.NormalizedOptions.Margin,
                context.LogoData,
                context.NormalizedOptions.LogoSizePercent,
                cancellationToken);
        }, cancellationToken);
    }

    private sealed class GenerationContext : IDisposable
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

        public void Dispose()
        {
            QrData.Dispose();
        }
    }

    private sealed class LogoData
    {
        public byte[] PngBytes { get; }
        public int Width { get; }
        public int Height { get; }
        public string? InlineSvgContent { get; }

        public LogoData(byte[] pngBytes, int width, int height, string? inlineSvgContent = null)
        {
            PngBytes = pngBytes;
            Width = width;
            Height = height;
            InlineSvgContent = inlineSvgContent;
        }
    }

    private readonly record struct QrRenderLayout(int OutputSize, float ModuleSize, float Offset);

    private GenerationContext PrepareGenerationContext(QRCodeGenerationOptions options)
    {
        bool logoForcesHighEcc = HasLogo(options) && options.EccLevel != QRCodeEccLevel.H;
        var (normalizedOptions, payload, darkInvalid, lightInvalid) = NormalizeOptions(options);
        QRCodeData qrData = GenerateQrDataCore(payload, normalizedOptions.EccLevel);
        
        SKColor skDarkColor = ParseNormalizedSkColor(normalizedOptions.DarkColor);
        SKColor skLightColor = ParseNormalizedSkColor(normalizedOptions.LightColor);
        double contrastRatio = CalculateContrastRatio(skDarkColor, skLightColor);
        
        LogoData? logoData = !string.IsNullOrWhiteSpace(normalizedOptions.LogoSvgContent)
            ? LoadSvgLogoData(normalizedOptions.LogoSvgContent)
            : null;

        return new GenerationContext
        {
            NormalizedOptions = normalizedOptions,
            QrData = qrData,
            ModuleCount = qrData.ModuleMatrix.Count,
            Version = GetVersion(qrData),
            PixelSize = normalizedOptions.OutputSize,
            Payload = payload,
            ContrastRatio = contrastRatio,
            Warnings = BuildWarnings(normalizedOptions, contrastRatio, darkInvalid, lightInvalid, logoForcesHighEcc),
            LogoData = logoData,
            SkDarkColor = skDarkColor,
            SkLightColor = skLightColor
        };
    }

    private static LogoData LoadSvgLogoData(string svgContent)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(svgContent));
        using var svg = new SKSvg();
        using SKPicture picture = svg.Load(stream) ?? throw new InvalidDataException(LocalizationService.Get("QRCodeGenerator_ErrorInvalidLogo"));

        SKRect bounds = picture.CullRect;
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            throw new InvalidDataException(LocalizationService.Get("QRCodeGenerator_ErrorInvalidLogo"));
        }

        int width = Math.Max(1, (int)Math.Ceiling(bounds.Width));
        int height = Math.Max(1, (int)Math.Ceiling(bounds.Height));
        using SKSurface surface = SKSurface.Create(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul))
            ?? throw new InvalidDataException(LocalizationService.Get("QRCodeGenerator_ErrorInvalidLogo"));
        SKCanvas canvas = surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        canvas.Translate(-bounds.Left, -bounds.Top);
        canvas.DrawPicture(picture);
        canvas.Flush();

        using SKImage image = surface.Snapshot();
        using SKData data = image.Encode(SKEncodedImageFormat.Png, 100)
            ?? throw new InvalidDataException(LocalizationService.Get("QRCodeGenerator_ErrorInvalidLogo"));

        return new LogoData(data.ToArray(), width, height, CreateInlineSvgMarkup(svgContent));
    }

    public QRCodeData GenerateQrData(string text, QRCodeEccLevel eccLevel = QRCodeEccLevel.Q)
    {
        ValidateText(text, nameof(text));

        return GenerateQrDataCore(text, eccLevel);
    }

    public byte[] RenderPng(QRCodeData data, int pixelSize, string darkColor, string lightColor, int margin, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(data);
        ValidateRenderOptions(pixelSize, margin);

        return RenderPng(data, pixelSize, exactSize: false, darkColor, lightColor, margin, logoData: null, cancellationToken: cancellationToken);
    }

    public string RenderSvg(QRCodeData data, int pixelSize, string darkColor, string lightColor, int margin, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(data);
        ValidateRenderOptions(pixelSize, margin);

        return RenderSvg(data, pixelSize, exactSize: false, darkColor, lightColor, margin, logoData: null, cancellationToken: cancellationToken);
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

    internal static (QRCodeGenerationOptions options, string payload, bool darkInvalid, bool lightInvalid) NormalizeOptions(QRCodeGenerationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        QRCodeQualityPreset preset = HasLogo(options)
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
            Text = NormalizeText(options.Text),
            ContentType = options.ContentType,
            WifiSsid = NormalizeText(options.WifiSsid),
            WifiPassword = options.WifiPassword ?? string.Empty,
            WifiSecurity = options.WifiSecurity,
            WifiHidden = options.WifiHidden,
            EmailAddress = NormalizeText(options.EmailAddress),
            EmailSubject = NormalizeText(options.EmailSubject),
            EmailBody = options.EmailBody ?? string.Empty,
            PhoneNumber = NormalizeText(options.PhoneNumber),
            SmsMessage = options.SmsMessage ?? string.Empty,
            VCardFirstName = NormalizeText(options.VCardFirstName),
            VCardLastName = NormalizeText(options.VCardLastName),
            VCardPhone = NormalizeText(options.VCardPhone),
            VCardEmail = NormalizeText(options.VCardEmail),
            VCardCompany = NormalizeText(options.VCardCompany),
            VCardJobTitle = NormalizeText(options.VCardJobTitle),
            VCardWebsite = NormalizeText(options.VCardWebsite),
            QualityPreset = preset,
            OutputSize = outputSize,
            PixelSize = Math.Clamp(options.PixelSize, 1, 100),
            Margin = Math.Clamp(options.Margin, 0, 10),
            EccLevel = eccLevel,
            DarkColor = darkColor,
            LightColor = lightColor,
            LogoSvgContent = string.IsNullOrWhiteSpace(options.LogoSvgContent) ? null : options.LogoSvgContent.Trim(),
            LogoSizePercent = Math.Clamp(options.LogoSizePercent, 8, 20)
        };

        string payload = BuildPayload(normalized);
        ValidatePayloadText(payload, GetPayloadParameterName(normalized.ContentType));
        return (normalized, payload, darkInvalid, lightInvalid);
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

    private static string NormalizeText(string? value) => value?.Trim() ?? string.Empty;



    private static void DrawModules(SKCanvas canvas, QRCodeData data, int margin, float moduleSize, SKPaint paint)
    {
        int count = data.ModuleMatrix.Count;
        for (int y = 0; y < count; y++)
        {
            for (int x = 0; x < count; x++)
            {
                if (!data.ModuleMatrix[y][x])
                {
                    continue;
                }

                canvas.DrawRect(
                    (margin + x) * moduleSize,
                    (margin + y) * moduleSize,
                    moduleSize,
                    moduleSize,
                    paint);
            }
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

        float boxSize = (float)outputSize * Math.Clamp(logoSizePercent, 8, 20) / 100f;
        float backingSize = boxSize * 1.22f;
        var backing = CenterRect(outputSize, backingSize);
        var target = FitRect(logo.Width, logo.Height, CenterRect(outputSize, boxSize));
        using var backingPaint = new SKPaint { IsAntialias = true, Color = backgroundColor, Style = SKPaintStyle.Fill };
        canvas.DrawRect(backing, backingPaint);
        using SKImage image = SKImage.FromBitmap(logo);
        canvas.DrawImage(image, target, LogoSampling);
    }

    private static byte[] RenderPng(QRCodeData data, int size, bool exactSize, string darkColor, string lightColor, int margin, LogoData? logoData, int logoSizePercent = 20, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        SKColor skDark = ParseSkColor(darkColor, "#000000");
        SKColor skLight = ParseSkColor(lightColor, "#FFFFFF");
        return RenderPng(data, size, exactSize, skDark, skLight, margin, logoData, logoSizePercent, cancellationToken);
    }

    private static byte[] RenderPng(QRCodeData data, int size, bool exactSize, SKColor skDarkColor, SKColor skLightColor, int margin, LogoData? logoData, int logoSizePercent = 20, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        QrRenderLayout layout = CreateLayout(size, exactSize, data.ModuleMatrix.Count, margin);
        using SKSurface surface = SKSurface.Create(new SKImageInfo(layout.OutputSize, layout.OutputSize, SKColorType.Rgba8888, SKAlphaType.Premul))
            ?? throw new InvalidDataException(LocalizationService.Get("QRCodeGenerator_ErrorGeneric"));
        SKCanvas canvas = surface.Canvas;
        canvas.Clear(skLightColor);

        using var paint = new SKPaint { IsAntialias = false, Color = skDarkColor, Style = SKPaintStyle.Fill };
        if ((int)layout.Offset != 0)
        {
            canvas.Translate(layout.Offset, layout.Offset);
        }

        DrawModules(canvas, data, margin, layout.ModuleSize, paint);
        cancellationToken.ThrowIfCancellationRequested();
        if ((int)layout.Offset != 0)
        {
            canvas.Translate(-layout.Offset, -layout.Offset);
        }

        DrawLogo(canvas, layout.OutputSize, logoSizePercent, skLightColor, logoData);
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

        float boxSize = (float)outputSize * Math.Clamp(logoSizePercent, 8, 20) / 100f;
        float backingSize = boxSize * 1.22f;
        SKRect backing = CenterRect(outputSize, backingSize);
        SKRect target = FitRect(logoData.Width, logoData.Height, CenterRect(outputSize, boxSize));
        svg.Append(CultureInfo.InvariantCulture, $"  <rect x=\"{backing.Left:0.###}\" y=\"{backing.Top:0.###}\" width=\"{backing.Width:0.###}\" height=\"{backing.Height:0.###}\" fill=\"{lightColor}\"/>\n");
        if (!string.IsNullOrWhiteSpace(logoData.InlineSvgContent))
        {
            svg.Append(CultureInfo.InvariantCulture, $"  <g transform=\"translate({target.Left:0.###},{target.Top:0.###}) scale({target.Width / logoData.Width:0.######},{target.Height / logoData.Height:0.######})\">\n");
            svg.Append(logoData.InlineSvgContent);
            if (!logoData.InlineSvgContent.EndsWith('\n'))
            {
                svg.Append('\n');
            }

            svg.Append("  </g>\n");
            return;
        }

    }

    private static string RenderSvg(QRCodeData data, int size, bool exactSize, string darkColor, string lightColor, int margin, LogoData? logoData, int logoSizePercent = 20, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        QrRenderLayout layout = CreateLayout(size, exactSize, data.ModuleMatrix.Count, margin);
        var svg = new StringBuilder();
        svg.Append(CultureInfo.InvariantCulture, $"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{layout.OutputSize}\" height=\"{layout.OutputSize}\" viewBox=\"0 0 {layout.OutputSize} {layout.OutputSize}\" shape-rendering=\"crispEdges\">\n");
        svg.Append(CultureInfo.InvariantCulture, $"  <rect width=\"100%\" height=\"100%\" fill=\"{lightColor}\"/>\n");
        AppendModules(svg, data, margin, layout.ModuleSize, darkColor, layout.Offset);
        cancellationToken.ThrowIfCancellationRequested();
        AppendLogo(svg, layout.OutputSize, logoSizePercent, lightColor, layout.ModuleSize, logoData);
        svg.Append("</svg>");
        return svg.ToString();
    }

    private static void AppendModules(StringBuilder svg, QRCodeData data, int margin, float moduleSize, string darkColor, float offset)
    {
        AppendOptimizedSquareModules(svg, data, margin, moduleSize, darkColor, offset);
    }

    private static void AppendOptimizedSquareModules(StringBuilder svg, QRCodeData data, int margin, float moduleSize, string darkColor, float offset)
    {
        int count = data.ModuleMatrix.Count;
        bool[,] visited = new bool[count, count];

        for (int y = 0; y < count; y++)
        {
            for (int x = 0; x < count; x++)
            {
                if (visited[y, x] || !data.ModuleMatrix[y][x])
                {
                    continue;
                }

                // Expand right as much as possible
                int width = 1;
                while (x + width < count &&
                       !visited[y, x + width] &&
                       data.ModuleMatrix[y][x + width])
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
                            !data.ModuleMatrix[y + height][x + dx])
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
                float px = offset + ((margin + x) * moduleSize);
                float py = offset + ((margin + y) * moduleSize);
                float w = width * moduleSize;
                float h = height * moduleSize;
                svg.Append(CultureInfo.InvariantCulture, $"  <rect x=\"{px:0.###}\" y=\"{py:0.###}\" width=\"{w:0.###}\" height=\"{h:0.###}\" fill=\"{darkColor}\"/>\n");
            }
        }
    }



    private static QrRenderLayout CreateLayout(int size, bool exactSize, int moduleCount, int margin)
    {
        int totalModules = moduleCount + (margin * 2);
        if (!exactSize)
        {
            int outputSize = Math.Max(1, totalModules * size);
            return new QrRenderLayout(outputSize, size, 0);
        }

        int output = Math.Max(1, size);
        float moduleSize = Math.Max(1, (float)Math.Floor(output / (double)totalModules));
        float used = moduleSize * totalModules;
        float offset = (output - used) / 2f;
        return new QrRenderLayout(output, moduleSize, offset);
    }

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

    private static IReadOnlyList<string> BuildWarnings(QRCodeGenerationOptions options, double contrastRatio, bool darkInvalid, bool lightInvalid, bool logoForcesHighEcc)
    {
        var warnings = new List<string>();
        if (contrastRatio < MinRecommendedContrastRatio)
        {
            warnings.Add(LocalizationService.Format("QRCodeGenerator_WarningLowContrast", contrastRatio.ToString("0.0", CultureInfo.CurrentCulture)));
        }

        if (!string.IsNullOrWhiteSpace(options.LogoSvgContent))
        {
            if (logoForcesHighEcc)
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

    private static bool HasLogo(QRCodeGenerationOptions options)
    {
        return !string.IsNullOrWhiteSpace(options.LogoSvgContent);
    }

    private static string CreateInlineSvgMarkup(string svgContent)
    {
        try
        {
            var document = XDocument.Parse(svgContent, LoadOptions.PreserveWhitespace);
            XElement root = document.Root ?? throw new InvalidDataException(LocalizationService.Get("QRCodeGenerator_ErrorInvalidLogo"));
            root.Name = XName.Get("g", root.Name.NamespaceName);
            root.Attributes()
                .Where(a => a.IsNamespaceDeclaration || a.Name.LocalName is "width" or "height" or "viewBox" or "x" or "y")
                .Remove();
            return root.ToString(SaveOptions.DisableFormatting);
        }
        catch (Exception ex) when (ex is not InvalidDataException)
        {
            throw new InvalidDataException(LocalizationService.Get("QRCodeGenerator_ErrorInvalidLogo"), ex);
        }
    }

    private static string NormalizeUrlPayload(string text)
    {
        string candidate = text.Trim();
        if (candidate.IndexOfAny([' ', '\t', '\r', '\n']) >= 0)
        {
            throw new ArgumentException(LocalizationService.Get("QRCodeGenerator_ErrorInvalidUrl"), nameof(text));
        }

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
        var uri = new StringBuilder($"mailto:{Uri.EscapeDataString(options.EmailAddress)}");
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
        sb.Append($"N:{EscapeVCardValue(options.VCardLastName)};{EscapeVCardValue(options.VCardFirstName)};;;\n");
        
        string fn = string.Join(" ", new[] { options.VCardFirstName, options.VCardLastName }
            .Where(s => !string.IsNullOrWhiteSpace(s)));
            
        if (!string.IsNullOrWhiteSpace(fn))
            sb.Append($"FN:{EscapeVCardValue(fn)}\n");
            
        if (!string.IsNullOrWhiteSpace(options.VCardCompany))
            sb.Append($"ORG:{EscapeVCardValue(options.VCardCompany)}\n");
            
        if (!string.IsNullOrWhiteSpace(options.VCardJobTitle))
            sb.Append($"TITLE:{EscapeVCardValue(options.VCardJobTitle)}\n");
            
        if (!string.IsNullOrWhiteSpace(options.VCardPhone))
            sb.Append($"TEL;TYPE=CELL:{EscapeVCardValue(options.VCardPhone)}\n");
            
        if (!string.IsNullOrWhiteSpace(options.VCardEmail))
            sb.Append($"EMAIL;TYPE=PREF,INTERNET:{EscapeVCardValue(options.VCardEmail)}\n");
            
        if (!string.IsNullOrWhiteSpace(options.VCardWebsite))
            sb.Append($"URL:{EscapeVCardValue(options.VCardWebsite)}\n");

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

    private static string EscapeVCardValue(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var escaped = new StringBuilder(value.Length);
        foreach (char ch in value)
        {
            switch (ch)
            {
                case '\\':
                case ';':
                case ',':
                    escaped.Append('\\').Append(ch);
                    break;
                case '\r':
                    break;
                case '\n':
                    escaped.Append("\\n");
                    break;
                default:
                    escaped.Append(ch);
                    break;
            }
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
        ValidatePayloadText(text, parameterName);
    }

    private static void ValidatePayloadText(string text, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException(LocalizationService.Get("QRCodeGenerator_ErrorEmptyText"), parameterName);
        }

        if (text.Length > MaxPayloadLength)
        {
            throw new ArgumentException(LocalizationService.Format("QRCodeGenerator_ErrorTextTooLong", MaxPayloadLength), parameterName);
        }
    }

    private static string GetPayloadParameterName(QRCodeContentType contentType) =>
        contentType switch
        {
            QRCodeContentType.Wifi => nameof(QRCodeGenerationOptions.WifiSsid),
            QRCodeContentType.Email => nameof(QRCodeGenerationOptions.EmailAddress),
            QRCodeContentType.Phone => nameof(QRCodeGenerationOptions.PhoneNumber),
            QRCodeContentType.Sms => nameof(QRCodeGenerationOptions.PhoneNumber),
            QRCodeContentType.VCard => nameof(QRCodeGenerationOptions.VCardFirstName),
            _ => nameof(QRCodeGenerationOptions.Text)
        };

    private static QRCodeData GenerateQrDataCore(string text, QRCodeEccLevel eccLevel) =>
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

    internal static (string normalizedColor, bool wasInvalid) NormalizeColor(string? color, string fallback)
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
