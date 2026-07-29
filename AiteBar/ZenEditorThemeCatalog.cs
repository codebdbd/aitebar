using System;
using System.Collections.Generic;
using System.Linq;

namespace AiteBar;

internal static class ZenEditorThemeCatalog
{
    public const string PaperId = "paper";
    public const string IvoryId = "ivory";
    public const string MistId = "mist";
    public const string GraphiteId = "graphite";
    public const string NightId = "night";

    public static IReadOnlyList<ZenEditorTheme> All { get; } =
    [
        new(PaperId, "ZenEditor_ThemePaper", "Literata", 20, 760,
            "#F4F0E7", "#282622", "#D8D0C2", "#1F1D1A", "#282622", "#E8E2D7", "#CFC8BC"),
        new(IvoryId, "ZenEditor_ThemeIvory", "Source Serif 4", 20, 760,
            "#FBF8F1", "#25231F", "#DED6C9", "#1D1B18", "#25231F", "#EEE9DF", "#D3CCC0"),
        new(MistId, "ZenEditor_ThemeMist", "Noto Sans", 19, 740,
            "#F1F4F2", "#222624", "#CDD8D3", "#18201C", "#222624", "#E4E9E6", "#C8D0CC"),
        new(GraphiteId, "ZenEditor_ThemeGraphite", "IBM Plex Sans", 19.5, 750,
            "#1E2023", "#E4E2DC", "#41474D", "#FFFFFF", "#E4E2DC", "#282B2F", "#3B3F44"),
        new(NightId, "ZenEditor_ThemeNight", "Inter", 19, 730,
            "#13171C", "#DCE3E8", "#34424D", "#FFFFFF", "#DCE3E8", "#1C2229", "#303943")
    ];

    public static ZenEditorTheme Get(string? id) =>
        All.FirstOrDefault(theme => string.Equals(theme.Id, id, StringComparison.Ordinal))
        ?? All[0];

    public static ZenEditorTheme GetAdjacent(string? currentId, int direction)
    {
        ZenEditorTheme current = Get(currentId);
        int currentIndex = Enumerable.Range(0, All.Count)
            .First(index => ReferenceEquals(All[index], current));
        int offset = direction < 0 ? -1 : 1;
        int nextIndex = (currentIndex + offset + All.Count) % All.Count;
        return All[nextIndex];
    }
}
