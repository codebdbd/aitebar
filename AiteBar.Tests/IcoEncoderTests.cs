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
        Assert.Equal((uint)png16.Length, ReadUInt32(ico, 14));
        Assert.Equal(38u, ReadUInt32(ico, 18));

        Assert.Equal(32, ico[22]);
        Assert.Equal(32, ico[23]);
        Assert.Equal((uint)png32.Length, ReadUInt32(ico, 30));
        Assert.Equal((uint)(38 + png16.Length), ReadUInt32(ico, 34));
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
}
