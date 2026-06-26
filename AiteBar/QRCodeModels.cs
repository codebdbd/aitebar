namespace AiteBar;

public sealed class ComboItem<T>
{
    public string DisplayText { get; }
    public T Value { get; }

    public ComboItem(string displayText, T value)
    {
        DisplayText = displayText;
        Value = value;
    }

    public override string ToString() => DisplayText;
}

public static class QRCodeQualityPresetExtensions
{
    public static int GetOutputSize(this QRCodeQualityPreset preset)
    {
        return preset switch
        {
            QRCodeQualityPreset.ScreenHD => 1200,
            QRCodeQualityPreset.Print => 1200,
            QRCodeQualityPreset.PrintHigh => 2000,
            QRCodeQualityPreset.Logo => 1000,
            _ => 800
        };
    }
}

public enum QRCodeEccLevel
{
    L,
    M,
    Q,
    H
}

public enum QRCodeContentType
{
    Text,
    Url,
    Wifi
}

public enum QRCodeQualityPreset
{
    Screen,
    ScreenHD,
    Print,
    PrintHigh,
    Logo
}

public enum QRCodeModuleShape
{
    Square,
    Rounded,
    Circle,
    Dot,
    Diamond
}

public enum QRCodeEyeStyle
{
    Square,
    Rounded,
    Circle,
    Diamond
}

public enum QRCodeWifiSecurity
{
    Wpa,
    Wep,
    None
}

public sealed class QRCodeGenerationOptions
{
    public string Text { get; init; } = string.Empty;
    public QRCodeContentType ContentType { get; init; } = QRCodeContentType.Text;
    public string WifiSsid { get; init; } = string.Empty;
    public string WifiPassword { get; init; } = string.Empty;
    public QRCodeWifiSecurity WifiSecurity { get; init; } = QRCodeWifiSecurity.Wpa;
    public bool WifiHidden { get; init; }
    public QRCodeQualityPreset QualityPreset { get; init; } = QRCodeQualityPreset.Screen;
    public int OutputSize { get; init; } = 800;
    public int PixelSize { get; init; } = 20;
    public int Margin { get; init; } = 4;
    public QRCodeEccLevel EccLevel { get; init; } = QRCodeEccLevel.Q;
    public string DarkColor { get; init; } = "#000000";
    public string LightColor { get; init; } = "#FFFFFF";
    public QRCodeModuleShape ModuleShape { get; init; } = QRCodeModuleShape.Square;
    public QRCodeEyeStyle EyeStyle { get; init; } = QRCodeEyeStyle.Square;
    public string? LogoPath { get; init; }
    public int LogoSizePercent { get; init; } = 18;
}

public sealed class QRCodeGenerationResult
{
    public byte[] PngBytes { get; init; } = [];
    public string SvgContent { get; init; } = string.Empty;
    public int ModuleCount { get; init; }
    public int Version { get; init; }
    public int PixelWidth { get; init; }
    public int PixelHeight { get; init; }
    public string Payload { get; init; } = string.Empty;
    public double ContrastRatio { get; init; }
    public IReadOnlyList<string> Warnings { get; init; } = [];
}
