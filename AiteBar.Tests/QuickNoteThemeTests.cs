using AiteBar;

namespace AiteBar.Tests;

public sealed class QuickNoteThemeTests
{
    [Fact]
    public void Themes_ContainsDefaultDarkTheme()
    {
        QuickNoteTheme theme = QuickNoteThemeCatalog.Find(QuickNoteThemeCatalog.DefaultThemeId);

        Assert.Equal("dark", theme.Id);
        Assert.True(theme.IsDark);
        Assert.Equal("#202124", theme.Background);
    }

    [Fact]
    public void Find_UnknownTheme_ReturnsDefaultTheme()
    {
        QuickNoteTheme theme = QuickNoteThemeCatalog.Find("missing-theme");

        Assert.Equal(QuickNoteThemeCatalog.DefaultThemeId, theme.Id);
    }

    [Fact]
    public void Find_NullTheme_ReturnsDefaultTheme()
    {
        QuickNoteTheme theme = QuickNoteThemeCatalog.Find(null);

        Assert.Equal(QuickNoteThemeCatalog.DefaultThemeId, theme.Id);
    }

    [Fact]
    public void Themes_HaveUniqueIdsAndValidHexColors()
    {
        Assert.Equal(QuickNoteThemeCatalog.Themes.Count, QuickNoteThemeCatalog.Themes.Select(theme => theme.Id).Distinct().Count());
        Assert.All(QuickNoteThemeCatalog.Themes, theme =>
        {
            AssertColor(theme.Background);
            AssertColor(theme.Border);
            AssertColor(theme.Text);
            AssertColor(theme.MutedText);
            AssertColor(theme.Accent);
        });
    }

    private static void AssertColor(string color)
    {
        Assert.Matches("^#[0-9A-Fa-f]{6}$", color);
    }
}
