using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Xml;
using System.Xml.Linq;
using SkiaSharp;
using Svg.Skia;

namespace AiteBar;

public sealed class IconConverterService
{
    private const long MaxInputFileBytes = 50L * 1024 * 1024;
    private const long MaxSvgFileBytes = 10L * 1024 * 1024;
    private const long MaxInputPixels = 80L * 1000 * 1000;
    private static readonly XNamespace SvgNamespace = "http://www.w3.org/2000/svg";
    private static readonly XNamespace XLinkNamespace = "http://www.w3.org/1999/xlink";

    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png",
        ".jpg",
        ".jpeg",
        ".webp",
        ".bmp",
        ".tif",
        ".tiff",
        ".svg"
    };

    private static readonly HashSet<string> SafeSvgElements = new(StringComparer.OrdinalIgnoreCase)
    {
        "svg",
        "g",
        "defs",
        "desc",
        "title",
        "metadata",
        "symbol",
        "use",
        "path",
        "rect",
        "circle",
        "ellipse",
        "line",
        "polyline",
        "polygon",
        "text",
        "tspan",
        "linearGradient",
        "radialGradient",
        "stop",
        "clipPath",
        "mask",
        "pattern",
        "filter",
        "feBlend",
        "feColorMatrix",
        "feComposite",
        "feDropShadow",
        "feFlood",
        "feGaussianBlur",
        "feMerge",
        "feMergeNode",
        "feOffset",
        "feOpacity",
        "feTurbulence",
        "feDisplacementMap",
        "feMorphology",
        "style"
    };

    private static readonly HashSet<string> SafeSvgAttributes = new(StringComparer.OrdinalIgnoreCase)
    {
        "accent-height",
        "alignment-baseline",
        "baseline-shift",
        "class",
        "clip",
        "clip-path",
        "clip-rule",
        "color",
        "color-interpolation",
        "color-interpolation-filters",
        "cx",
        "cy",
        "d",
        "direction",
        "display",
        "dominant-baseline",
        "dx",
        "dy",
        "fill",
        "fill-opacity",
        "fill-rule",
        "filter",
        "filterUnits",
        "font",
        "font-family",
        "font-size",
        "font-stretch",
        "font-style",
        "font-variant",
        "font-weight",
        "fx",
        "fy",
        "gradientTransform",
        "gradientUnits",
        "height",
        "id",
        "letter-spacing",
        "marker-end",
        "marker-mid",
        "marker-start",
        "mask",
        "maskContentUnits",
        "maskUnits",
        "offset",
        "opacity",
        "overflow",
        "points",
        "preserveAspectRatio",
        "r",
        "rx",
        "ry",
        "shape-rendering",
        "spreadMethod",
        "stop-color",
        "stop-opacity",
        "stroke",
        "stroke-dasharray",
        "stroke-dashoffset",
        "stroke-linecap",
        "stroke-linejoin",
        "stroke-miterlimit",
        "stroke-opacity",
        "stroke-width",
        "style",
        "text-anchor",
        "text-decoration",
        "text-rendering",
        "transform",
        "unicode-bidi",
        "version",
        "viewBox",
        "visibility",
        "width",
        "word-spacing",
        "x",
        "x1",
        "x2",
        "y",
        "y1",
        "y2"
    };

    private static readonly SKSamplingOptions ResizeSampling = new SKSamplingOptions(SKCubicResampler.Mitchell);

    public Task<IconConversionResult> ConvertAsync(
        string sourcePath,
        IconConversionOptions options,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            IconConversionContext context = LoadAndProcessSource(sourcePath, options, cancellationToken);
            byte[] icoBytes = IcoEncoder.Encode(context.Previews.Select(preview => new IcoImageEntry(preview.Size, preview.PngBytes)).ToList());

            return new IconConversionResult
            {
                IcoBytes = icoBytes,
                Previews = context.Previews,
                Warnings = context.Warnings,
                SourceWidth = context.SourceWidth,
                SourceHeight = context.SourceHeight
            };
        }, cancellationToken);
    }

    public Task<IReadOnlyList<IconPreviewImage>> GeneratePreviewsAsync(
        string sourcePath,
        IconConversionOptions options,
        CancellationToken cancellationToken = default)
    {
        return Task.Run<IReadOnlyList<IconPreviewImage>>(() =>
        {
            IconConversionContext context = LoadAndProcessSource(sourcePath, options, cancellationToken);
            return context.Previews;
        }, cancellationToken);
    }

    public Task<IconConversionResult> GeneratePreviewResultAsync(
        string sourcePath,
        IconConversionOptions options,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            IconConversionContext context = LoadAndProcessSource(sourcePath, options, cancellationToken);

            return new IconConversionResult
            {
                Previews = context.Previews,
                Warnings = context.Warnings,
                SourceWidth = context.SourceWidth,
                SourceHeight = context.SourceHeight
            };
        }, cancellationToken);
    }

    private sealed record IconConversionContext(
        IReadOnlyList<IconPreviewImage> Previews,
        IReadOnlyList<string> Warnings,
        int SourceWidth,
        int SourceHeight);

    private static IconConversionContext LoadAndProcessSource(
        string sourcePath,
        IconConversionOptions options,
        CancellationToken cancellationToken)
    {
        ValidateSourcePath(sourcePath);
        IconConversionOptions normalizedOptions = NormalizeOptions(options);
        using IconSource source = LoadSourceImage(sourcePath);
        IReadOnlyList<IconPreviewImage> previews = RenderPreviews(source, normalizedOptions, cancellationToken);
        IReadOnlyList<string> warnings = BuildWarnings(source, normalizedOptions);

        return new IconConversionContext(previews, warnings, source.Width, source.Height);
    }

    internal static IconConversionOptions NormalizeOptions(IconConversionOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var sizes = options.Sizes
            .Distinct()
            .Order()
            .ToArray();

        if (sizes.Length == 0)
        {
            throw new InvalidOperationException(LocalizationService.Get("IconConverter_ErrorNoSizesSelected"));
        }

        if (sizes.Any(size => size < 1 || size > 256))
        {
            throw new InvalidOperationException(LocalizationService.Get("IconConverter_ErrorInvalidSize"));
        }

        return new IconConversionOptions
        {
            Sizes = sizes,
            PaddingPercent = Math.Clamp(options.PaddingPercent, 0, 40),
            BackgroundMode = options.BackgroundMode,
            BackgroundColor = options.BackgroundColor,
            FitMode = options.FitMode
        };
    }

    private static IReadOnlyList<string> BuildWarnings(IconSource source, IconConversionOptions options)
    {
        if (source.IsVector)
        {
            return [];
        }

        int maxTargetSize = options.Sizes.Count == 0 ? 0 : options.Sizes.Max();
        if (source.Width < maxTargetSize || source.Height < maxTargetSize)
        {
            return [LocalizationService.Format("IconConverter_WarningSourceTooSmall", source.Width, source.Height, maxTargetSize)];
        }

        return [];
    }

    private static void ValidateSourcePath(string sourcePath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
        {
            throw new FileNotFoundException(LocalizationService.Get("IconConverter_ErrorFileNotFound"), sourcePath);
        }

        string extension = Path.GetExtension(sourcePath);
        if (!SupportedExtensions.Contains(extension))
        {
            throw new NotSupportedException(LocalizationService.Get("IconConverter_ErrorUnsupportedFormat"));
        }

        long fileLength = new FileInfo(sourcePath).Length;
        long maxBytes = extension.Equals(".svg", StringComparison.OrdinalIgnoreCase) ? MaxSvgFileBytes : MaxInputFileBytes;
        if (fileLength > maxBytes)
        {
            throw new InvalidDataException(LocalizationService.Format("IconConverter_ErrorFileTooLarge", maxBytes / (1024 * 1024)));
        }
    }

    private static IconSource LoadSourceImage(string sourcePath)
    {
        try
        {
            return Path.GetExtension(sourcePath).Equals(".svg", StringComparison.OrdinalIgnoreCase)
                ? LoadSvgSource(sourcePath)
                : LoadRasterSource(sourcePath);
        }
        catch (NotSupportedException ex)
        {
            throw new NotSupportedException(LocalizationService.Get("IconConverter_ErrorUnsupportedFormat"), ex);
        }
        catch (InvalidDataException)
        {
            throw;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new InvalidDataException(LocalizationService.Get("IconConverter_ErrorInvalidImage"), ex);
        }
    }

    private static IconSource LoadRasterSource(string sourcePath)
    {
        using var stream = File.OpenRead(sourcePath);
        using SKCodec? codec = SKCodec.Create(stream);
        if (codec == null)
        {
            throw new InvalidDataException(LocalizationService.Get("IconConverter_ErrorInvalidImage"));
        }

        SKImageInfo info = codec.Info;
        if (info.Width <= 0 || info.Height <= 0)
        {
            throw new InvalidDataException(LocalizationService.Get("IconConverter_ErrorInvalidImage"));
        }

        if ((long)info.Width * info.Height > MaxInputPixels)
        {
            throw new InvalidDataException(LocalizationService.Format("IconConverter_ErrorImageTooLarge", info.Width, info.Height));
        }

        var bitmap = new SKBitmap(new SKImageInfo(info.Width, info.Height, SKColorType.Rgba8888, SKAlphaType.Premul));
        SKCodecResult result = codec.GetPixels(bitmap.Info, bitmap.GetPixels());
        if (result != SKCodecResult.Success && result != SKCodecResult.IncompleteInput)
        {
            bitmap.Dispose();
            throw new InvalidDataException(LocalizationService.Get("IconConverter_ErrorInvalidImage"));
        }

        bitmap = ApplyEncodedOrigin(bitmap, codec.EncodedOrigin);
        return IconSource.FromBitmap(bitmap, HasTransparentPixels(bitmap));
    }

    private static IconSource LoadSvgSource(string sourcePath)
    {
        ValidateSvgSafety(sourcePath);

        var svg = new SKSvg();
        try
        {
            SKPicture? picture = svg.Load(sourcePath);
            if (picture == null)
            {
                svg.Dispose();
                throw new InvalidDataException(LocalizationService.Get("IconConverter_ErrorInvalidSvg"));
            }

            try
            {
                SKRect bounds = picture.CullRect;
                if (bounds.Width <= 0 || bounds.Height <= 0)
                {
                    picture.Dispose();
                    svg.Dispose();
                    throw new InvalidDataException(LocalizationService.Get("IconConverter_ErrorInvalidSvg"));
                }

                return IconSource.FromSvg(svg, picture, bounds);
            }
            catch
            {
                picture.Dispose();
                throw;
            }
        }
        catch
        {
            svg.Dispose();
            throw;
        }
    }

    private static void ValidateSvgSafety(string sourcePath)
    {
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersInDocument = MaxSvgFileBytes
        };

        XDocument document;
        try
        {
            using XmlReader reader = XmlReader.Create(sourcePath, settings);
            document = XDocument.Load(reader, LoadOptions.None);
        }
        catch (Exception ex) when (ex is XmlException or InvalidOperationException)
        {
            throw new InvalidDataException(LocalizationService.Get("IconConverter_ErrorInvalidSvg"), ex);
        }

        bool unsafeContent =
            document.Root == null ||
            !document.Root.Name.LocalName.Equals("svg", StringComparison.OrdinalIgnoreCase) ||
            document.Descendants().Any(IsUnsafeSvgElement);

        if (unsafeContent)
        {
            throw new InvalidDataException(LocalizationService.Get("IconConverter_ErrorUnsafeSvg"));
        }
    }

    private static bool IsUnsafeSvgElement(XElement element)
    {
        string localName = element.Name.LocalName;
        XNamespace elementNamespace = element.Name.Namespace;
        if ((elementNamespace != XNamespace.None && elementNamespace != SvgNamespace) ||
            !SafeSvgElements.Contains(localName))
        {
            return true;
        }

        if (localName.Equals("style", StringComparison.OrdinalIgnoreCase) &&
            IsUnsafeSvgText(element.Value))
        {
            return true;
        }

        return element.Attributes().Any(IsUnsafeSvgAttribute);
    }

    private static bool IsUnsafeSvgAttribute(XAttribute attribute)
    {
        if (attribute.IsNamespaceDeclaration)
        {
            return false;
        }

        XNamespace attributeNamespace = attribute.Name.Namespace;
        if (attributeNamespace != XNamespace.None && attributeNamespace != XLinkNamespace)
        {
            return true;
        }

        string name = attribute.Name.LocalName;
        string value = attribute.Value.Trim();
        if (name.StartsWith("on", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!SafeSvgAttributes.Contains(name) && !name.Equals("href", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        if ((name.Equals("href", StringComparison.OrdinalIgnoreCase) ||
             name.Equals("src", StringComparison.OrdinalIgnoreCase)) &&
            !IsLocalFragmentReference(value))
        {
            return true;
        }

        return IsUnsafeSvgText(value);
    }

    private static bool IsLocalFragmentReference(string value)
    {
        return value.StartsWith("#", StringComparison.Ordinal);
    }

    private static bool IsUnsafeSvgText(string value)
    {
        return value.Contains("@import", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("url(http://", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("url(https://", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("url(file://", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("url(data:", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("http://", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("https://", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("file://", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("data:", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("javascript:", StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<IconPreviewImage> RenderPreviews(
        IconSource source,
        IconConversionOptions options,
        CancellationToken cancellationToken)
    {
        var previews = new List<IconPreviewImage>(options.Sizes.Count);
        foreach (int size in options.Sizes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            previews.Add(new IconPreviewImage
            {
                Size = size,
                PngBytes = RenderPng(source, size, options)
            });
        }

        return previews;
    }

    private static byte[] RenderPng(IconSource source, int size, IconConversionOptions options)
    {
        var imageInfo = new SKImageInfo(size, size, SKColorType.Rgba8888, SKAlphaType.Premul);
        using SKSurface surface = SKSurface.Create(imageInfo) ?? throw new InvalidOperationException(LocalizationService.Get("IconConverter_ErrorRenderFailed"));
        SKCanvas canvas = surface.Canvas;
        canvas.Clear(GetBackgroundColor(source, options));

        SKRect target = CalculateTargetSKRect(source.Width, source.Height, size, options.PaddingPercent, options.FitMode);
        if (source.Bitmap != null)
        {
            using SKImage rasterImage = SKImage.FromBitmap(source.Bitmap);
            canvas.DrawImage(rasterImage, target, ResizeSampling);
        }
        else if (source.Picture != null)
        {
            RenderSvgPicture(canvas, source.Picture, source.Bounds, target);
        }

        canvas.Flush();
        using SKImage image = surface.Snapshot();
        using SKData data = image.Encode(SKEncodedImageFormat.Png, 100) ?? throw new InvalidOperationException(LocalizationService.Get("IconConverter_ErrorRenderFailed"));
        return data.ToArray();
    }

    private static SKColor GetBackgroundColor(IconSource source, IconConversionOptions options)
    {
        if (options.BackgroundMode == IconBackgroundMode.SolidColor)
        {
            return TryParseBackgroundColor(options.BackgroundColor, out SKColor color)
                ? color
                : throw new InvalidOperationException(LocalizationService.Get("IconConverter_ErrorInvalidColor"));
        }

        return source.SupportsTransparentBackground ? SKColors.Transparent : SKColors.White;
    }

    private static void RenderSvgPicture(SKCanvas canvas, SKPicture picture, SKRect bounds, SKRect target)
    {
        canvas.Save();

        float scale = Math.Min(target.Width / bounds.Width, target.Height / bounds.Height);
        canvas.Translate(target.Left - (bounds.Left * scale), target.Top - (bounds.Top * scale));
        canvas.Scale(scale);
        canvas.DrawPicture(picture);
        canvas.Restore();
    }

    internal static Rect CalculateTargetRect(int sourceWidth, int sourceHeight, int canvasSize, double paddingPercent, IconFitMode fitMode)
    {
        SKRect rect = CalculateTargetSKRect(sourceWidth, sourceHeight, canvasSize, paddingPercent, fitMode);
        return new Rect(rect.Left, rect.Top, rect.Width, rect.Height);
    }

    private static SKRect CalculateTargetSKRect(int sourceWidth, int sourceHeight, int canvasSize, double paddingPercent, IconFitMode fitMode)
    {
        double padding = canvasSize * Math.Clamp(paddingPercent, 0, 40) / 100d;
        double contentSize = Math.Max(1, canvasSize - (padding * 2));
        double scaleX = contentSize / sourceWidth;
        double scaleY = contentSize / sourceHeight;
        double scale = fitMode == IconFitMode.Fill ? Math.Max(scaleX, scaleY) : Math.Min(scaleX, scaleY);
        double width = sourceWidth * scale;
        double height = sourceHeight * scale;
        return new SKRect(
            (float)((canvasSize - width) / 2d),
            (float)((canvasSize - height) / 2d),
            (float)((canvasSize + width) / 2d),
            (float)((canvasSize + height) / 2d));
    }

    internal static bool TryParseBackgroundColor(string color, out SKColor colorValue)
    {
        if (SKColor.TryParse(color?.Trim(), out colorValue))
        {
            return true;
        }

        try
        {
            if (System.Windows.Media.ColorConverter.ConvertFromString(color?.Trim() ?? "#000000") is System.Windows.Media.Color wpfColor)
            {
                colorValue = new SKColor(wpfColor.R, wpfColor.G, wpfColor.B, wpfColor.A);
                return true;
            }
        }
        catch (Exception ex) when (ex is FormatException or NotSupportedException)
        {
        }

        colorValue = default;
        return false;
    }

    private static SKBitmap ApplyEncodedOrigin(SKBitmap bitmap, SKEncodedOrigin origin)
    {
        if (origin == SKEncodedOrigin.TopLeft)
        {
            return bitmap;
        }

        bool swapDimensions = origin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;
        int width = swapDimensions ? bitmap.Height : bitmap.Width;
        int height = swapDimensions ? bitmap.Width : bitmap.Height;
        var oriented = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));
        using var canvas = new SKCanvas(oriented);

        switch (origin)
        {
            case SKEncodedOrigin.TopRight:
                canvas.Translate(width, 0);
                canvas.Scale(-1, 1);
                break;
            case SKEncodedOrigin.BottomRight:
                canvas.Translate(width, height);
                canvas.RotateDegrees(180);
                break;
            case SKEncodedOrigin.BottomLeft:
                canvas.Translate(0, height);
                canvas.Scale(1, -1);
                break;
            case SKEncodedOrigin.LeftTop:
                canvas.RotateDegrees(90);
                canvas.Scale(1, -1);
                break;
            case SKEncodedOrigin.RightTop:
                canvas.Translate(width, 0);
                canvas.RotateDegrees(90);
                break;
            case SKEncodedOrigin.RightBottom:
                canvas.Translate(width, height);
                canvas.RotateDegrees(90);
                canvas.Scale(-1, 1);
                break;
            case SKEncodedOrigin.LeftBottom:
                canvas.Translate(0, height);
                canvas.RotateDegrees(-90);
                break;
        }

        canvas.DrawBitmap(bitmap, 0, 0);
        canvas.Flush();
        bitmap.Dispose();
        return oriented;
    }

    private static bool HasTransparentPixels(SKBitmap bitmap)
    {
        if (bitmap.AlphaType == SKAlphaType.Opaque)
        {
            return false;
        }

        IntPtr pixelsPtr = bitmap.GetPixels();
        int rowBytes = bitmap.RowBytes;
        int height = bitmap.Height;
        int width = bitmap.Width;
        int totalBytes = rowBytes * height;

        unsafe
        {
            ReadOnlySpan<byte> pixels = new ReadOnlySpan<byte>(pixelsPtr.ToPointer(), totalBytes);
            for (int y = 0; y < height; y++)
            {
                ReadOnlySpan<byte> row = pixels.Slice(y * rowBytes, rowBytes);
                for (int x = 0; x < width; x++)
                {
                    // В RGBA8888 альфа-канал - 4-й байт (индекс 3)
                    if (row[(x * 4) + 3] < 255)
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private sealed class IconSource : IDisposable
    {
        private readonly SKSvg? _svg;

        private IconSource(SKBitmap? bitmap, SKSvg? svg, SKPicture? picture, SKRect bounds, bool supportsTransparentBackground, bool isVector)
        {
            Bitmap = bitmap;
            _svg = svg;
            Picture = picture;
            Bounds = bounds;
            SupportsTransparentBackground = supportsTransparentBackground;
            IsVector = isVector;
            Width = Math.Max(1, (int)Math.Ceiling(bounds.Width));
            Height = Math.Max(1, (int)Math.Ceiling(bounds.Height));
        }

        public SKBitmap? Bitmap { get; }
        public SKPicture? Picture { get; }
        public SKRect Bounds { get; }
        public bool SupportsTransparentBackground { get; }
        public bool IsVector { get; }
        public int Width { get; }
        public int Height { get; }

        public static IconSource FromBitmap(SKBitmap bitmap, bool supportsTransparentBackground) =>
            new(bitmap, null, null, new SKRect(0, 0, bitmap.Width, bitmap.Height), supportsTransparentBackground, isVector: false);

        public static IconSource FromSvg(SKSvg svg, SKPicture picture, SKRect bounds) =>
            new(null, svg, picture, bounds, supportsTransparentBackground: true, isVector: true);

        public void Dispose()
        {
            Bitmap?.Dispose();
            Picture?.Dispose();
            _svg?.Dispose();
        }
    }
}
