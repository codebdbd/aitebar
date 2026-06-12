using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace AiteBar;

internal static class IcoEncoder
{
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private const int IconDirSize = 6;
    private const int IconDirEntrySize = 16;
    private const ushort IconType = 1;
    private const ushort Planes = 1;
    private const ushort BitCount = 32;

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

        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);

        writer.Write((ushort)0);
        writer.Write(IconType);
        writer.Write((ushort)ordered.Length);

        int dataOffset = IconDirSize + (IconDirEntrySize * ordered.Length);
        foreach (IcoImageEntry image in ordered)
        {
            byte sizeByte = image.Size == 256 ? (byte)0 : (byte)image.Size;
            writer.Write(sizeByte);
            writer.Write(sizeByte);
            writer.Write((byte)0);
            writer.Write((byte)0);
            writer.Write(Planes);
            writer.Write(BitCount);
            writer.Write((uint)image.PngBytes.Length);
            writer.Write((uint)dataOffset);
            dataOffset += image.PngBytes.Length;
        }

        foreach (IcoImageEntry image in ordered)
        {
            writer.Write(image.PngBytes);
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
}
