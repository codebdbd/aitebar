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
        Assert.Equal("#272727", theme.Background);
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
    public void HeaderBackground_IsAdjustedBasedOnThemeDarkness()
    {
        Assert.Equal("#D7E6D4", QuickNoteThemeCatalog.GetHeaderBackground(QuickNoteThemeCatalog.Find("sage")));
        Assert.Equal("#232323", QuickNoteThemeCatalog.GetHeaderBackground(QuickNoteThemeCatalog.Find("dark")));
        Assert.NotEqual(QuickNoteThemeCatalog.Find("sage").Accent, QuickNoteThemeCatalog.GetHeaderBackground(QuickNoteThemeCatalog.Find("sage")));

        var customTheme = new QuickNoteTheme("custom", "#FFFFFF", "#8A8A8A", "#000000", "#000000", "#0067B8", "#101316", "#F4F6F8", "#000000", false, "#EAEAEA");
        Assert.Equal("#EAEAEA", QuickNoteThemeCatalog.GetHeaderBackground(customTheme));
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

    [Fact]
    public void Themes_OnlyVaryBackgroundAndSharedForeground()
    {
        QuickNoteTheme baseline = QuickNoteThemeCatalog.Themes[0];

        Assert.All(QuickNoteThemeCatalog.Themes, theme =>
        {
            string expectedForeground = theme.IsDark ? "#F1F1F1" : "#000000";
            Assert.Equal(expectedForeground, theme.Text);
            Assert.Equal(expectedForeground, theme.MutedText);
            Assert.Equal(expectedForeground, theme.Link);
            Assert.Equal(baseline.Border, theme.Border);
            Assert.Equal(baseline.Accent, theme.Accent);
            Assert.Equal(QuickNoteDocumentFormatting.CodeBackground, theme.CodeBackground);
            Assert.Equal(QuickNoteDocumentFormatting.CodeForeground, theme.CodeText);
        });
    }

    private static void AssertColor(string color)
    {
        Assert.Matches("^#[0-9A-Fa-f]{6}$", color);
    }
}
