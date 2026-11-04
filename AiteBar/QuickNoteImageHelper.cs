using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace AiteBar;

[System.Runtime.Versioning.SupportedOSPlatform("windows6.1")]
internal static class QuickNoteImageHelper
{
    internal const int MaxEncodedBytes = 8 * 1024 * 1024;
    internal const int MaxPixels = 16_000_000;
    internal const int MaxLongestSide = 1600;
    internal const int MaxTotalEmbeddedBytes = 24 * 1024 * 1024;
    private const string MarkerPrefix = "\uE000AiteBar:image:v1:";
    private const string MarkerSuffix = "\uE001";
    private static readonly DependencyProperty PngPayloadProperty = DependencyProperty.RegisterAttached(
        "QuickNotePngPayload",
        typeof(byte[]),
        typeof(QuickNoteImageHelper),
        new PropertyMetadata(null));

    static QuickNoteImageHelper()
    {
        EnableXamlDeserializationForUndo();
    }

    internal static void EnableXamlDeserializationForUndo()
    {
        try
        {
            var prop = typeof(FrameworkCompatibilityPreferences).GetProperty(
                "DisableLegacyDangerousXamlDeserializationMode",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            prop?.GetSetMethod(true)?.Invoke(null, [false]);
        }
        catch (Exception ex)
        {
            Logger.Log(ex);
        }
    }

    internal static bool TryCreateInlineImage(byte[] bytes, out InlineUIContainer? container)
    {
        container = null;
        if (bytes.Length == 0 || bytes.Length > MaxEncodedBytes)
        {
            return false;
        }

        try
        {
            using var stream = new MemoryStream(bytes, writable: false);
            // First decode with DelayCreation to check dimensions safely without allocating full pixel memory
            var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None);
            if (decoder.Frames.Count == 0) return false;
            var frameHeader = decoder.Frames[0];
            if (frameHeader.PixelWidth <= 0 || frameHeader.PixelHeight <= 0 ||
                (long)frameHeader.PixelWidth * frameHeader.PixelHeight > MaxPixels)
            {
                return false;
            }

            stream.Position = 0;
            var frame = BitmapFrame.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);

            if (frame.PixelWidth <= MaxLongestSide && frame.PixelHeight <= MaxLongestSide)
            {
                return CreateContainer(frame, bytes, out container);
            }

            BitmapSource normalized = ResizeIfNeeded(frame);
            byte[] png = EncodePng(normalized);
            if (png.Length == 0 || png.Length > MaxEncodedBytes)
            {
                return false;
            }

            return CreateContainer(normalized, png, out container);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or InvalidOperationException or IOException or System.Windows.Markup.XamlParseException)
        {
            return false;
        }
    }

    internal static bool TryCreateInlineImage(BitmapSource source, out InlineUIContainer? container)
    {
        container = null;
        try
        {
            if (source.PixelWidth <= 0 || source.PixelHeight <= 0 ||
                (long)source.PixelWidth * source.PixelHeight > MaxPixels)
            {
                return false;
            }

            BitmapSource normalized = ResizeIfNeeded(source);
            byte[] png = EncodePng(normalized);
            if (png.Length == 0 || png.Length > MaxEncodedBytes)
            {
                return false;
            }

            return CreateContainer(normalized, png, out container);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or NotSupportedException or IOException or System.Windows.Markup.XamlParseException)
        {
            return false;
        }
    }

    internal static bool TryGetPngBytes(InlineUIContainer container, out byte[]? png)
    {
        png = null;
        if (!TryGetImageControl(container, out Image? image) || image is null || image.Source is not BitmapSource source)
        {
            return false;
        }

        try
        {
            png = container.GetValue(PngPayloadProperty) as byte[];
            if (png == null && image.Source is BitmapImage { UriSource: { IsFile: true } uri } && File.Exists(uri.LocalPath))
            {
                try
                {
                    png = File.ReadAllBytes(uri.LocalPath);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    Logger.Log(ex);
                }
            }

            if (png == null && image is QuickNoteImage quickImage && !string.IsNullOrEmpty(quickImage.PngBase64))
            {
                try
                {
                    png = Convert.FromBase64String(quickImage.PngBase64);
                }
                catch (FormatException ex)
                {
                    Logger.Log(new InvalidOperationException("Failed to decode QuickNoteImage.PngBase64", ex));
                }
            }

            png ??= EncodePng(source);
            if (png.Length == 0 || png.Length > MaxEncodedBytes)
            {
                png = null;
                return false;
            }

            container.SetValue(PngPayloadProperty, png);
            return true;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or NotSupportedException)
        {
            png = null;
            return false;
        }
    }

    internal static bool TryGetMarker(InlineUIContainer container, out string marker, out int payloadBytes)
    {
        marker = string.Empty;
        payloadBytes = 0;
        if (!TryGetPngBytes(container, out byte[]? png) || png == null)
        {
            return false;
        }

        payloadBytes = png.Length;
        marker = $"{MarkerPrefix}{Convert.ToBase64String(png)}{MarkerSuffix}";
        return true;
    }

    internal static bool TryGetPngPayload(InlineUIContainer container, out byte[]? png) =>
        TryGetPngBytes(container, out png);

    internal static bool TryGetImageControl(InlineUIContainer container, out Image? image)
    {
        if (container.Child is Image directImage)
        {
            image = directImage;
            return true;
        }

        if (container.Child is Border { Child: Image wrappedImage })
        {
            image = wrappedImage;
            return true;
        }

        // When WPF Undo/Redo falls back to a dummy Grid because restrictive XAML blocked Image deserialization,
        // restore the Image control from the container's preserved PngPayload.
        if (container.Child is Grid && container.GetValue(PngPayloadProperty) is byte[] png && png.Length > 0)
        {
            try
            {
                string cachePath = GetCacheImagePath(png);
                var bitmapImage = new BitmapImage();
                bitmapImage.BeginInit();
                bitmapImage.UriSource = new Uri(cachePath, UriKind.Absolute);
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapImage.EndInit();
                bitmapImage.Freeze();

                double scale = Math.Min(1, Math.Min(640d / bitmapImage.PixelWidth, 480d / bitmapImage.PixelHeight));
                double width = Math.Max(1, bitmapImage.PixelWidth * scale);
                double height = Math.Max(1, bitmapImage.PixelHeight * scale);

                var restoredImage = new Image
                {
                    Source = bitmapImage,
                    Width = width,
                    Height = height,
                    Stretch = Stretch.Uniform,
                    ToolTip = "Embedded image"
                };

                container.Child = restoredImage;
                image = restoredImage;
                return true;
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
        }

        image = null;
        return false;
    }

    internal static bool CanAddToDocument(FlowDocument document, InlineUIContainer candidate)
    {
        if (!TryGetPngBytes(candidate, out byte[]? candidatePng) || candidatePng == null)
        {
            return false;
        }

        int total = candidatePng.Length;
        foreach (InlineUIContainer existing in EnumerateImageContainers(document.Blocks))
        {
            if (TryGetPngBytes(existing, out byte[]? existingPng) && existingPng != null)
            {
                total += existingPng.Length;
                if (total > MaxTotalEmbeddedBytes)
                {
                    return false;
                }
            }
        }

        return total <= MaxTotalEmbeddedBytes;
    }

    internal static IEnumerable<InlineUIContainer> EnumerateImageContainers(BlockCollection blocks)
    {
        foreach (Block block in blocks.ToArray())
        {
            if (block is Paragraph paragraph)
            {
                foreach (InlineUIContainer image in EnumerateImageContainers(paragraph.Inlines))
                {
                    yield return image;
                }
            }
            else if (block is Section section)
            {
                foreach (InlineUIContainer image in EnumerateImageContainers(section.Blocks))
                {
                    yield return image;
                }
            }
            else if (block is System.Windows.Documents.List list)
            {
                foreach (ListItem item in list.ListItems.ToArray())
                {
                    foreach (InlineUIContainer image in EnumerateImageContainers(item.Blocks))
                    {
                        yield return image;
                    }
                }
            }
        }
    }

    internal static IEnumerable<InlineUIContainer> EnumerateImageContainers(InlineCollection inlines)
    {
        foreach (Inline inline in inlines.ToArray())
        {
            if (inline is InlineUIContainer image && TryGetImageControl(image, out _))
            {
                yield return image;
            }

            if (inline is Span span)
            {
                foreach (InlineUIContainer nestedImage in EnumerateImageContainers(span.Inlines))
                {
                    yield return nestedImage;
                }
            }
        }
    }

    internal static bool TryCreateInlineImageFromMarker(string text, ref int totalPayloadBytes, out InlineUIContainer? container)
    {
        container = null;
        if (!text.StartsWith(MarkerPrefix, StringComparison.Ordinal) || !text.EndsWith(MarkerSuffix, StringComparison.Ordinal))
        {
            return false;
        }

        try
        {
            string payload = text[MarkerPrefix.Length..^MarkerSuffix.Length];
            byte[] png = Convert.FromBase64String(payload);
            if (png.Length > MaxEncodedBytes || png.Length > MaxTotalEmbeddedBytes - totalPayloadBytes ||
                !TryCreateInlineImage(png, out container))
            {
                container = null;
                return false;
            }

            totalPayloadBytes += png.Length;
            return true;
        }
        catch (FormatException ex)
        {
            Logger.Log(new InvalidOperationException("Failed to decode image marker payload", ex));
            return false;
        }
    }

    private static byte[] EncodePng(BitmapSource source)
    {
        BitmapSource normalized = ResizeIfNeeded(source);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(normalized));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        return stream.ToArray();
    }

    private static string GetCacheImagePath(byte[] png)
    {
        string hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(png));
        string dir = Path.Combine(Path.GetTempPath(), "AiteBar", "ImageCache");
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, $"{hash}.png");
        if (!File.Exists(path))
        {
            File.WriteAllBytes(path, png);
        }
        return path;
    }

    private static bool CreateContainer(BitmapSource source, byte[] png, out InlineUIContainer? container)
    {
        string cachePath = GetCacheImagePath(png);
        var bitmapImage = new BitmapImage();
        bitmapImage.BeginInit();
        bitmapImage.UriSource = new Uri(cachePath, UriKind.Absolute);
        bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
        bitmapImage.EndInit();
        bitmapImage.Freeze();

        // Keep a portrait image from consuming the whole note while never enlarging small images.
        double scale = Math.Min(1, Math.Min(640d / bitmapImage.PixelWidth, 480d / bitmapImage.PixelHeight));
        double width = Math.Max(1, bitmapImage.PixelWidth * scale);
        double height = Math.Max(1, bitmapImage.PixelHeight * scale);

        var imageControl = new Image
        {
            Source = bitmapImage,
            Width = width,
            Height = height,
            Stretch = Stretch.Uniform,
            ToolTip = "Embedded image"
        };

        container = new InlineUIContainer(imageControl);
        container.SetValue(PngPayloadProperty, png);
        return true;
    }

    private static BitmapSource ResizeIfNeeded(BitmapSource source)
    {
        if (source.PixelWidth <= MaxLongestSide && source.PixelHeight <= MaxLongestSide)
        {
            return source;
        }

        double scale = Math.Min((double)MaxLongestSide / source.PixelWidth, (double)MaxLongestSide / source.PixelHeight);
        return new TransformedBitmap(source, new System.Windows.Media.ScaleTransform(scale, scale));
    }
}
