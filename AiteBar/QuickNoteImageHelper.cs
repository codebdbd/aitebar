using System;
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
            var frame = BitmapFrame.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            if (frame.PixelWidth <= 0 || frame.PixelHeight <= 0 ||
                (long)frame.PixelWidth * frame.PixelHeight > MaxPixels)
            {
                return false;
            }

            return TryCreateInlineImage(frame, out container);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or InvalidOperationException or IOException)
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
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or NotSupportedException)
        {
            return false;
        }
    }

    internal static bool TryGetMarker(InlineUIContainer container, out string marker, out int payloadBytes)
    {
        marker = string.Empty;
        payloadBytes = 0;
        if (!TryGetImageControl(container, out Image? image) || image is null || image.Source is not BitmapSource source)
        {
            return false;
        }

        try
        {
            byte[] png = container.GetValue(PngPayloadProperty) as byte[] ?? EncodePng(source);
            if (png.Length == 0 || png.Length > MaxEncodedBytes)
            {
                return false;
            }

            payloadBytes = png.Length;
            container.SetValue(PngPayloadProperty, png);
            marker = $"{MarkerPrefix}{Convert.ToBase64String(png)}{MarkerSuffix}";
            return true;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or NotSupportedException)
        {
            return false;
        }
    }

    internal static bool TryGetPngPayload(InlineUIContainer container, out byte[]? png)
    {
        png = null;
        if (!TryGetMarker(container, out string marker, out _))
        {
            return false;
        }

        try
        {
            png = Convert.FromBase64String(marker[MarkerPrefix.Length..^MarkerSuffix.Length]);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    internal static bool TryGetImageControl(InlineUIContainer container, out Image? image)
    {
        image = container.Child switch
        {
            Image directImage => directImage,
            Border { Child: Image wrappedImage } => wrappedImage,
            _ => null
        };
        return image != null;
    }

    internal static bool CanAddToDocument(FlowDocument document, InlineUIContainer candidate)
    {
        if (!TryGetMarker(candidate, out _, out int candidateBytes))
        {
            return false;
        }

        int total = candidateBytes;
        foreach (InlineUIContainer existing in EnumerateImageContainers(document.Blocks).ToList())
        {
            if (TryGetMarker(existing, out _, out int bytes))
            {
                total += bytes;
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
        foreach (Block block in blocks)
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
                foreach (ListItem item in list.ListItems)
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
        foreach (Inline inline in inlines)
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
        catch (FormatException)
        {
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

    private static bool CreateContainer(BitmapSource source, byte[] png, out InlineUIContainer? container)
    {
        if (source.CanFreeze)
        {
            source.Freeze();
        }

        // Keep a portrait image from consuming the whole note while never enlarging small images.
        double scale = Math.Min(1, Math.Min(640d / source.PixelWidth, 480d / source.PixelHeight));
        double width = Math.Max(1, source.PixelWidth * scale);
        double height = Math.Max(1, source.PixelHeight * scale);
        
        var quickImage = new QuickNoteImage
        {
            Width = width,
            Height = height,
            Stretch = System.Windows.Media.Stretch.Uniform,
            ToolTip = "Embedded image",
            PngBase64 = Convert.ToBase64String(png)
        };

        container = new InlineUIContainer(quickImage);
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
