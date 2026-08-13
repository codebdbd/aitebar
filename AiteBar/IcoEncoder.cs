using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SkiaSharp;

namespace AiteBar;

internal static class IcoEncoder
{
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private const int IconDirSize = 6;
    private const int IconDirEntrySize = 16;
    private const ushort IconType = 1;
    private const ushort Planes = 1;
    private const ushort BitCount = 32;
    private const int BitmapInfoHeaderSize = 40;
    private const int PngOnlySize = 256;

    public static byte[] Encode(IReadOnlyList<IcoImageEntry> images)
    {
        ArgumentNullException.ThrowIfNull(images);

        if (images.Count == 0)
        {
            throw new ArgumentException("At least one image is required.", nameof(images));
        }

        var ordered = images.OrderBy(image => image.Size).ToArray();
        var sizes = new HashSet<int>();
        foreach (IcoImageEntry image in ordered)
        {
            if (image.Size < 1 || image.Size > 256)
            {
                throw new ArgumentOutOfRangeException(nameof(images), "ICO image sizes must be between 1 and 256.");
            }

            if (!sizes.Add(image.Size))
            {
                throw new ArgumentException("ICO image sizes must be unique.", nameof(images));
            }

            if (image.PngBytes == null || image.PngBytes.Length == 0)
            {
                throw new ArgumentException("ICO image data cannot be empty.", nameof(images));
            }

            if (!HasPngSignature(image.PngBytes))
            {
                throw new ArgumentException("ICO image data must be PNG.", nameof(images));
            }

            (int width, int height) = ReadPngDimensions(image.PngBytes);
            if (width != image.Size || height != image.Size)
            {
                throw new ArgumentException("ICO image PNG dimensions must match the declared icon size.", nameof(images));
            }
        }

        EncodedIcoImage[] encodedImages = ordered
            .Select(image => new EncodedIcoImage(image.Size, image.Size == PngOnlySize ? image.PngBytes : EncodeDibPayload(image)))
            .ToArray();

        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);

        writer.Write((ushort)0);
        writer.Write(IconType);
        writer.Write((ushort)encodedImages.Length);

        int dataOffset = IconDirSize + (IconDirEntrySize * encodedImages.Length);
        foreach (EncodedIcoImage image in encodedImages)
        {
            byte sizeByte = image.Size == 256 ? (byte)0 : (byte)image.Size;
            writer.Write(sizeByte);
            writer.Write(sizeByte);
            writer.Write((byte)0);
            writer.Write((byte)0);
            writer.Write(Planes);
            writer.Write(BitCount);
            writer.Write((uint)image.Payload.Length);
            writer.Write((uint)dataOffset);
            dataOffset += image.Payload.Length;
        }

        foreach (EncodedIcoImage image in encodedImages)
        {
            writer.Write(image.Payload);
        }

        writer.Flush();
        return stream.ToArray();
    }

    private static byte[] EncodeDibPayload(IcoImageEntry image)
    {
        using SKBitmap bitmap = SKBitmap.Decode(image.PngBytes)
            ?? throw new ArgumentException("ICO image data must be a decodable PNG.", nameof(image));

        int size = image.Size;
        int xorStride = size * 4;
        int andStride = ((size + 31) / 32) * 4;
        using var stream = new MemoryStream(BitmapInfoHeaderSize + (xorStride * size) + (andStride * size));
        using var writer = new BinaryWriter(stream);

        writer.Write(BitmapInfoHeaderSize);
        writer.Write(size);
        writer.Write(size * 2);
        writer.Write(Planes);
        writer.Write(BitCount);
        writer.Write(0);
        writer.Write(xorStride * size);
        writer.Write(0);
        writer.Write(0);
        writer.Write(0);
        writer.Write(0);

        for (int y = size - 1; y >= 0; y--)
        {
            for (int x = 0; x < size; x++)
            {
                SKColor pixel = bitmap.GetPixel(x, y);
                writer.Write(pixel.Blue);
                writer.Write(pixel.Green);
                writer.Write(pixel.Red);
                writer.Write(pixel.Alpha);
            }
        }

        Span<byte> maskRow = stackalloc byte[andStride];
        for (int y = size - 1; y >= 0; y--)
        {
            maskRow.Clear();
            for (int x = 0; x < size; x++)
            {
                if (bitmap.GetPixel(x, y).Alpha == 0)
                {
                    maskRow[x / 8] |= (byte)(0x80 >> (x % 8));
                }
            }

            writer.Write(maskRow);
        }

        writer.Flush();
        return stream.ToArray();
    }

    private static bool HasPngSignature(byte[] data)
    {
        return data.Length >= PngSignature.Length &&
               data.AsSpan(0, PngSignature.Length).SequenceEqual(PngSignature);
    }

    private static (int Width, int Height) ReadPngDimensions(byte[] data)
    {
        if (data.Length < 24 ||
            data[12] != (byte)'I' ||
            data[13] != (byte)'H' ||
            data[14] != (byte)'D' ||
            data[15] != (byte)'R')
        {
            throw new ArgumentException("ICO image data must contain a PNG IHDR chunk.", nameof(data));
        }

        return (
            BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(16, 4)),
            BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(20, 4)));
    }

    private sealed record EncodedIcoImage(int Size, byte[] Payload);
}
