using System.Collections.Generic;

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
        bool IsDark);

    public static class QuickNoteThemeCatalog
    {
        public const string DefaultThemeId = "dark";

        public static IReadOnlyList<QuickNoteTheme> Themes { get; } =
        [
            new("dark", "#202124", "#3A3B40", "#F6F0E6", "#74757A", "#70B7FF", "#2A2C30", "#E0E0E0", "#70B7FF", true),
            new("graphite", "#2A2B2E", "#44464A", "#F2EEE7", "#8B8D92", "#70B7FF", "#34363A", "#E0E0E0", "#70B7FF", true),
            new("rose", "#E9C7C3", "#CFAEA9", "#222222", "#65514F", "#0067B8", "#D4B4AF", "#1A1A1A", "#0067B8", false),
            new("clay", "#E5C6AE", "#C9AA91", "#222222", "#63503F", "#0067B8", "#D0B399", "#1A1A1A", "#0067B8", false),
            new("sand", "#E8D8B8", "#CCBC9A", "#222222", "#625842", "#0067B8", "#D3C4A2", "#1A1A1A", "#0067B8", false),
            new("lemon", "#E9E0B4", "#CDC48F", "#222222", "#5F5A3E", "#0067B8", "#D4CB9E", "#1A1A1A", "#0067B8", false),
            new("sage", "#C9DDC5", "#ACC0A8", "#222222", "#4E604C", "#0067B8", "#B5D0B0", "#1A1A1A", "#0067B8", false),
            new("mist", "#D7E1E5", "#BAC5CA", "#222222", "#4F5E64", "#0067B8", "#C3CED2", "#1A1A1A", "#0067B8", false),
            new("sky", "#C9DCEC", "#AEC0D0", "#222222", "#4A5D6C", "#0067B8", "#B5CAD8", "#1A1A1A", "#0067B8", false),
            new("lavender", "#D8CCE8", "#BCB0CF", "#222222", "#584F68", "#0067B8", "#C4BAD4", "#1A1A1A", "#0067B8", false),
            new("mauve", "#E4C8DD", "#C8ACC1", "#222222", "#604F5B", "#0067B8", "#D0B4C9", "#1A1A1A", "#0067B8", false),
            new("stone", "#D7D4CC", "#BBB8B0", "#222222", "#5B5850", "#0067B8", "#C3C0B8", "#1A1A1A", "#0067B8", false)
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
    }
}
