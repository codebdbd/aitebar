using System;
using System.Globalization;
using System.IO;
using System.Windows.Media;

namespace AiteBar;

internal static class QuickNoteBrush
{
    public static Brush FromHex(string color)
    {
        var brush = (Brush)new BrushConverter().ConvertFromInvariantString(color)!;
        if (brush.CanFreeze)
        {
            brush.Freeze();
        }
        return brush;
    }
}

internal static class QuickNoteUrlValidator
{
    public static bool IsSafeHttpUrl(string? url) =>
        !string.IsNullOrWhiteSpace(url) &&
        Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) &&
        (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}

internal static class QuickNoteFonts
{
    public const string DefaultFamilyName = "Segoe UI";
    public const string CodeFamilyName = "JetBrains Mono";

    public static FontFamily Default { get; } = new(DefaultFamilyName);
    public static FontFamily Code { get; } = new(
        new Uri(Path.Combine(AppContext.BaseDirectory, "Resources", "Fonts", "JetBrainsMono") + Path.DirectorySeparatorChar, UriKind.Absolute),
        "./#" + CodeFamilyName);
}

internal static class QuickNoteTags
{
    private const string HeadingPrefix = "heading:";
    private const string IndentPrefix = "indent:";
    private const string LinkPrefix = "link:";

    public const string Code = "code";
    public const string CodeHeader = "code-header";

    public static string Heading(int level) =>
        HeadingPrefix + level.ToString(CultureInfo.InvariantCulture);

    public static bool TryGetHeadingLevel(object? tag, out int level)
    {
        level = 0;
        return TryGetValue(tag, HeadingPrefix, out string value) &&
               int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out level) &&
               level is >= 1 and <= 6;
    }

    public static string? Indent(string value) =>
        string.IsNullOrEmpty(value) ? null : IndentPrefix + value;

    public static string GetIndent(object? tag, string fallback = "") =>
        TryGetValue(tag, IndentPrefix, out string value) ? value : fallback;

    public static string Link(string url) => LinkPrefix + url;

    public static string? GetLink(object? tag) =>
        TryGetValue(tag, LinkPrefix, out string value) ? value : null;

    private static bool TryGetValue(object? tag, string prefix, out string value)
    {
        value = string.Empty;
        if (tag is not string text || !text.StartsWith(prefix, StringComparison.Ordinal))
        {
            return false;
        }

        value = text[prefix.Length..];
        return true;
    }
}

internal enum QuickNoteResizeEdge
{
    Left,
    Right,
    Top,
    TopLeft,
    TopRight,
    Bottom,
    BottomLeft,
    BottomRight
}

internal static class QuickNoteResizeEdges
{
    public static bool TryParse(string? value, out QuickNoteResizeEdge edge) =>
        Enum.TryParse(value, ignoreCase: false, out edge) &&
        Enum.IsDefined(edge);
}
