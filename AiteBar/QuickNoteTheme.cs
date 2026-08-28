using System;
using System.Collections.Generic;
using System.Globalization;

namespace AiteBar
{
    public sealed record QuickNoteTheme(
        string Id,
        string Background,
        string Border,
        string Text,
        string MutedText,
        string Accent,
        string CodeBackground,
        string CodeText,
        string Link,
        bool IsDark,
        string? HeaderBackground = null,
        string? SwatchColor = null);

    public static class QuickNoteThemeCatalog
    {
        public const string DefaultThemeId = "dark";
        public const string CodeBackground = "#25213B";
        public const string CodeText = "#E3DFF2";
        public const string CodeHeaderBackground = "#302B49";
        public const string CodeBorder = "#433C61";

        private static readonly QuickNoteTheme DefaultTheme = new(
            DefaultThemeId, "#333333", "#515151", "#F3F3F3", "#B3B3B3", "#60CDFF",
            CodeBackground, CodeText, "#60CDFF", true, "#2C2C2C", "#767676");

        public static IReadOnlyList<QuickNoteTheme> Themes { get; } =
        [
            // Header and swatch are different roles. The purple header/body match the Windows reference;
            // other light headers use the same 40% white tint, without changing the palette samples.
            new("lemon",    "#FFF8D4", "#E9DA74", "#202020", "#595959", "#0067C0", CodeBackground, CodeText, "#0067C0", false, "#FFF0A8", "#FFE66E"),
            new("sage",     "#E4F9E0", "#B5DB9E", "#202020", "#595959", "#0067C0", CodeBackground, CodeText, "#0067C0", false, "#C7F5C3", "#A1EF9B"),
            new("rose",     "#FFE7F5", "#EBAECB", "#202020", "#595959", "#0067C0", CodeBackground, CodeText, "#0067C0", false, "#FFCFEC", "#FFAFDF"),
            new("lavender", "#F2E6FF", "#CCB2EB", "#202020", "#595959", "#0067C0", CodeBackground, CodeText, "#0067C0", false, "#E7CFFF", "#D7AFFF"),
            new("sky",      "#E2F5FF", "#AACFE9", "#202020", "#595959", "#0067C0", CodeBackground, CodeText, "#0067C0", false, "#C5ECFF", "#9EDFFF"),
            new("stone",    "#F6F6F6", "#D6D6D6", "#202020", "#595959", "#0067C0", CodeBackground, CodeText, "#0067C0", false, "#ECECEC", "#E0E0E0"),
            DefaultTheme
        ];

        public static QuickNoteTheme Find(string? id)
        {
            // Keep saved theme IDs usable when upgrading from the old palette.
            id = id switch
            {
                "graphite" => "dark",
                "clay" or "sand" => "lemon",
                "mist" => "sky",
                "mauve" => "rose",
                _ => id
            };
            foreach (var theme in Themes)
            {
                if (theme.Id == id)
                {
                    return theme;
                }
            }

            return DefaultTheme;
        }

        public static string GetSwatchColor(QuickNoteTheme theme)
        {
            ArgumentNullException.ThrowIfNull(theme);
            return theme.SwatchColor ?? GetHeaderBackground(theme);
        }

        public static string GetHeaderBackground(QuickNoteTheme theme)
        {
            ArgumentNullException.ThrowIfNull(theme);

            if (!string.IsNullOrWhiteSpace(theme.HeaderBackground))
            {
                return theme.HeaderBackground;
            }

            return theme.Background;
        }

        public static string GetQuoteBackground(QuickNoteTheme theme)
        {
            ArgumentNullException.ThrowIfNull(theme);
            if (!theme.IsDark) return GetSwatchColor(theme);
            const double factor = 0.85;
            return $"#{DarkenChannel(theme.Background, 1, factor):X2}{DarkenChannel(theme.Background, 3, factor):X2}{DarkenChannel(theme.Background, 5, factor):X2}";
        }

        public static string GetCodeHeaderBackground(QuickNoteTheme theme)
        {
            ArgumentNullException.ThrowIfNull(theme);

            return CodeHeaderBackground;
        }

        private static byte DarkenChannel(string color, int startIndex, double factor)
        {
            int channel = int.Parse(color.AsSpan(startIndex, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            return (byte)Math.Round(channel * factor, MidpointRounding.AwayFromZero);
        }

    }
}
