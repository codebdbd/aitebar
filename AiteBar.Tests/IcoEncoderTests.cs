using System;
using AiteBar;
using SkiaSharp;

namespace AiteBar.Tests;

public sealed class IcoEncoderTests
{
    [Fact]
    public void Encode_WritesIconDirectoryAndEntries()
    {
        byte[] png16 = CreatePng(16);
        byte[] png32 = CreatePng(32);
        byte[] ico = IcoEncoder.Encode(
        [
            new IcoImageEntry(32, png32),
            new IcoImageEntry(16, png16)
        ]);

        Assert.Equal(0, ReadUInt16(ico, 0));
        Assert.Equal(1, ReadUInt16(ico, 2));
        Assert.Equal(2, ReadUInt16(ico, 4));

        Assert.Equal(16, ico[6]);
        Assert.Equal(16, ico[7]);
        Assert.Equal(1, ReadUInt16(ico, 10));
        Assert.Equal(32, ReadUInt16(ico, 12));
        uint png16DibLength = GetDibPayloadLength(16);
        Assert.Equal(png16DibLength, ReadUInt32(ico, 14));
        Assert.Equal(38u, ReadUInt32(ico, 18));

        Assert.Equal(32, ico[22]);
        Assert.Equal(32, ico[23]);
        Assert.Equal(GetDibPayloadLength(32), ReadUInt32(ico, 30));
        Assert.Equal(38u + png16DibLength, ReadUInt32(ico, 34));
    }

    [Fact]
    public void Encode_WritesAllWindowsDpiSizes()
    {
        byte[] ico = IcoEncoder.Encode(
            IconConversionOptions.WindowsDpiSizes
                .Select(size => new IcoImageEntry(size, CreatePng(size)))
                .ToArray());

        Assert.Equal(IconConversionOptions.WindowsDpiSizes.Count, ReadUInt16(ico, 4));
        Assert.Equal(0, ico[6 + (8 * 16)]);
        Assert.Equal(0, ico[7 + (8 * 16)]);
    }

    [Fact]
    public void Encode_WritesWindowsCompatibleDibPayloadsBelow256()
    {
        byte[] ico = IcoEncoder.Encode(
        [
            new IcoImageEntry(16, CreatePng(16)),
            new IcoImageEntry(32, CreatePng(32)),
            new IcoImageEntry(256, CreatePng(256))
        ]);

        IcoEntry[] entries = ReadEntries(ico);

        Assert.Equal([16, 32, 256], entries.Select(entry => entry.Size));
        Assert.All(entries.Where(entry => entry.Size < 256), entry =>
        {
            Assert.False(HasPngSignature(entry.Payload));
            Assert.Equal(40u, ReadUInt32(entry.Payload, 0));
            Assert.Equal(entry.Size, (int)ReadUInt32(entry.Payload, 4));
            Assert.Equal(entry.Size * 2, (int)ReadUInt32(entry.Payload, 8));
            Assert.Equal(1, ReadUInt16(entry.Payload, 12));
            Assert.Equal(32, ReadUInt16(entry.Payload, 14));
            Assert.Equal(GetDibPayloadLength(entry.Size), (uint)entry.Payload.Length);
        });
        Assert.True(HasPngSignature(entries.Single(entry => entry.Size == 256).Payload));
    }

    [Fact]
    public void Encode_WritesSize256AsZero()
    {
        byte[] ico = IcoEncoder.Encode([new IcoImageEntry(256, CreatePng(256))]);

        Assert.Equal(0, ico[6]);
        Assert.Equal(0, ico[7]);
    }

    [Fact]
    public void Encode_RejectsDuplicateSizes()
    {
        Assert.Throws<ArgumentException>(() =>
            IcoEncoder.Encode([new IcoImageEntry(32, CreatePng(32)), new IcoImageEntry(32, CreatePng(32))]));
    }

    [Fact]
    public void Encode_RejectsEmptyPayload()
    {
        Assert.Throws<ArgumentException>(() =>
            IcoEncoder.Encode([new IcoImageEntry(32, [])]));
    }

    [Fact]
    public void Encode_RejectsNonPngPayload()
    {
        Assert.Throws<ArgumentException>(() =>
            IcoEncoder.Encode([new IcoImageEntry(32, [1, 2, 3, 4])]));
    }

    [Fact]
    public void Encode_RejectsPngWithMismatchedDimensions()
    {
        Assert.Throws<ArgumentException>(() =>
            IcoEncoder.Encode([new IcoImageEntry(32, CreatePng(16))]));
    }

    private static ushort ReadUInt16(byte[] data, int offset) => BitConverter.ToUInt16(data, offset);

    private static uint ReadUInt32(byte[] data, int offset) => BitConverter.ToUInt32(data, offset);

    private static IcoEntry[] ReadEntries(byte[] ico)
    {
        int count = ReadUInt16(ico, 4);
        var entries = new IcoEntry[count];
        for (int i = 0; i < count; i++)
        {
            int entryOffset = 6 + (i * 16);
            byte widthByte = ico[entryOffset];
            int size = widthByte == 0 ? 256 : widthByte;
            int payloadLength = (int)ReadUInt32(ico, entryOffset + 8);
            int payloadOffset = (int)ReadUInt32(ico, entryOffset + 12);
            entries[i] = new IcoEntry(size, ico.AsSpan(payloadOffset, payloadLength).ToArray());
        }

        return entries;
    }

    private static uint GetDibPayloadLength(int size)
    {
        int xorStride = size * 4;
        int andStride = ((size + 31) / 32) * 4;
        return (uint)(40 + (xorStride * size) + (andStride * size));
    }

    private static bool HasPngSignature(byte[] data) =>
        data.Length >= 8 &&
        data[0] == 0x89 &&
        data[1] == 0x50 &&
        data[2] == 0x4E &&
        data[3] == 0x47 &&
        data[4] == 0x0D &&
        data[5] == 0x0A &&
        data[6] == 0x1A &&
        data[7] == 0x0A;

    private static byte[] CreatePng(int size)
    {
        using var surface = SKSurface.Create(new SKImageInfo(size, size, SKColorType.Rgba8888, SKAlphaType.Premul));
        surface.Canvas.Clear(SKColors.Transparent);
        using var paint = new SKPaint { Color = SKColors.DodgerBlue, IsAntialias = true };
        surface.Canvas.DrawCircle(size / 2f, size / 2f, size * 0.35f, paint);
        using SKImage image = surface.Snapshot();
        using SKData data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private sealed record IcoEntry(int Size, byte[] Payload);
}
