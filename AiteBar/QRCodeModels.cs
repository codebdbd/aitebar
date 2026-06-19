namespace AiteBar;

public enum QRCodeEccLevel
{
    L,
    M,
    Q,
    H
}

public sealed class QRCodeGenerationOptions
{
    public string Text { get; init; } = string.Empty;
    public int PixelSize { get; init; } = 20;
    public int Margin { get; init; } = 4;
    public QRCodeEccLevel EccLevel { get; init; } = QRCodeEccLevel.Q;
    public string DarkColor { get; init; } = "#000000";
    public string LightColor { get; init; } = "#FFFFFF";
}

public sealed class QRCodeGenerationResult
{
    public byte[] PngBytes { get; init; } = [];
    public string SvgContent { get; init; } = string.Empty;
    public int ModuleCount { get; init; }
    public int Version { get; init; }
}
