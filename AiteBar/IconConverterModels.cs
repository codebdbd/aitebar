using System;
using System.Collections.Generic;

namespace AiteBar;

public enum IconBackgroundMode
{
    Transparent,
    SolidColor
}

public enum IconFitMode
{
    Fit,
    Fill
}

public sealed class IconConversionOptions
{
    public static IReadOnlyList<int> DefaultSizes { get; } = [16, 24, 32, 48, 64, 128, 256];
    public static IReadOnlyList<int> WindowsDpiSizes { get; } = [16, 20, 24, 32, 40, 48, 64, 128, 256];

    public IReadOnlyList<int> Sizes { get; init; } = DefaultSizes;
    public double PaddingPercent { get; init; } = 8;
    public IconBackgroundMode BackgroundMode { get; init; } = IconBackgroundMode.Transparent;
    public string BackgroundColor { get; init; } = "#000000";
    public IconFitMode FitMode { get; init; } = IconFitMode.Fit;
}

public sealed class IconPreviewImage
{
    public int Size { get; init; }
    public byte[] PngBytes { get; init; } = [];
}

public sealed class IconConversionResult
{
    public byte[] IcoBytes { get; init; } = [];
    public IReadOnlyList<IconPreviewImage> Previews { get; init; } = [];
    public IReadOnlyList<string> Warnings { get; init; } = [];
    public int SourceWidth { get; init; }
    public int SourceHeight { get; init; }
}

internal sealed record IcoImageEntry(int Size, byte[] PngBytes);
