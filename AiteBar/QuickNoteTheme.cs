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
        string? HeaderBackground = null);

    public static class QuickNoteThemeCatalog
    {
        public const string DefaultThemeId = "dark";

        public static IReadOnlyList<QuickNoteTheme> Themes { get; } =
        [
            new("dark",      "#272727", "#8A8A8A", "#F1F1F1", "#F1F1F1", "#0067B8", "#101316", "#F4F6F8", "#F1F1F1", true),
            new("graphite",  "#2A2B2E", "#8A8A8A", "#F1F1F1", "#F1F1F1", "#0067B8", "#101316", "#F4F6F8", "#F1F1F1", true),
            new("rose",      "#E9C7C3", "#8A8A8A", "#000000", "#000000", "#0067B8", "#101316", "#F4F6F8", "#000000", false),
            new("clay",      "#E5C6AE", "#8A8A8A", "#000000", "#000000", "#0067B8", "#101316", "#F4F6F8", "#000000", false),
            new("sand",      "#E8D8B8", "#8A8A8A", "#000000", "#000000", "#0067B8", "#101316", "#F4F6F8", "#000000", false),
            new("lemon",     "#E9E0B4", "#8A8A8A", "#000000", "#000000", "#0067B8", "#101316", "#F4F6F8", "#000000", false),
            new("sage",      "#C9DDC5", "#8A8A8A", "#000000", "#000000", "#0067B8", "#101316", "#F4F6F8", "#000000", false),
            new("mist",      "#D7E1E5", "#8A8A8A", "#000000", "#000000", "#0067B8", "#101316", "#F4F6F8", "#000000", false),
            new("sky",       "#C9DCEC", "#8A8A8A", "#000000", "#000000", "#0067B8", "#101316", "#F4F6F8", "#000000", false),
            new("lavender",  "#D8CCE8", "#8A8A8A", "#000000", "#000000", "#0067B8", "#101316", "#F4F6F8", "#000000", false),
            new("mauve",     "#E4C8DD", "#8A8A8A", "#000000", "#000000", "#0067B8", "#101316", "#F4F6F8", "#000000", false),
            new("stone",     "#D7D4CC", "#8A8A8A", "#000000", "#000000", "#0067B8", "#101316", "#F4F6F8", "#000000", false)
        ];

        public static QuickNoteTheme Find(string? id)
        {
            foreach (var theme in Themes)
            {
                if (theme.Id == id)
                {
                    return theme;
                }
            }

            return Themes[0];
        }

        public static string GetHeaderBackground(QuickNoteTheme theme)
        {
            ArgumentNullException.ThrowIfNull(theme);

            if (!string.IsNullOrWhiteSpace(theme.HeaderBackground))
            {
                return theme.HeaderBackground;
            }

            if (theme.IsDark)
            {
                return $"#{DarkenChannel(theme.Background, 1):X2}{DarkenChannel(theme.Background, 3):X2}{DarkenChannel(theme.Background, 5):X2}";
            }

            return $"#{LightenChannel(theme.Background, 1):X2}{LightenChannel(theme.Background, 3):X2}{LightenChannel(theme.Background, 5):X2}";
        }

        private static byte DarkenChannel(string color, int startIndex)
        {
            int channel = int.Parse(color.AsSpan(startIndex, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            return (byte)Math.Round(channel * 0.9, MidpointRounding.AwayFromZero);
        }

        private static byte LightenChannel(string color, int startIndex)
        {
            int channel = int.Parse(color.AsSpan(startIndex, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            return (byte)Math.Clamp(Math.Round(channel + (255 - channel) * 0.25, MidpointRounding.AwayFromZero), 0, 255);
        }
    }
}
