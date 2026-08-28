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
        Assert.Equal("#333333", theme.Background);
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
    public void HeaderBackground_UsesExplicitColorWithoutAutomaticDimming()
    {
        Assert.Equal("#C7F5C3", QuickNoteThemeCatalog.GetHeaderBackground(QuickNoteThemeCatalog.Find("sage")));
        Assert.Equal("#2C2C2C", QuickNoteThemeCatalog.GetHeaderBackground(QuickNoteThemeCatalog.Find("dark")));
        Assert.NotEqual(QuickNoteThemeCatalog.Find("sage").Accent, QuickNoteThemeCatalog.GetHeaderBackground(QuickNoteThemeCatalog.Find("sage")));

        var customTheme = new QuickNoteTheme("custom", "#FFFFFF", "#8A8A8A", "#202020", "#706866", "#0969DA", "#DEBAB6", "#4A1525", "#0969DA", false, "#EAEAEA");
        Assert.Equal("#EAEAEA", QuickNoteThemeCatalog.GetHeaderBackground(customTheme));
        Assert.Equal(customTheme.Background, QuickNoteThemeCatalog.GetHeaderBackground(customTheme with { HeaderBackground = null }));
    }

    [Fact]
    public void PurpleNote_MatchesWindowsScreenshotWithoutChangingSwatchOrQuote()
    {
        var theme = QuickNoteThemeCatalog.Find("lavender");
        Assert.Equal("#E7CFFF", QuickNoteThemeCatalog.GetHeaderBackground(theme));
        Assert.Equal("#F2E6FF", theme.Background);
        Assert.Equal("#D7AFFF", QuickNoteThemeCatalog.GetSwatchColor(theme));
        Assert.Equal("#D7AFFF", QuickNoteThemeCatalog.GetQuoteBackground(theme));
    }

    [Fact]
    public void LightHeaders_UseSeparateLighterColorsThanPaletteSamples()
    {
        Assert.Equal(new[] { "#FFF0A8", "#C7F5C3", "#FFCFEC", "#E7CFFF", "#C5ECFF", "#ECECEC" },
            QuickNoteThemeCatalog.Themes.Where(theme => !theme.IsDark).Select(QuickNoteThemeCatalog.GetHeaderBackground));
    }

    [Theory]
    [InlineData("graphite", "dark")]
    [InlineData("clay", "lemon")]
    [InlineData("sand", "lemon")]
    [InlineData("mist", "sky")]
    [InlineData("mauve", "rose")]
    public void Find_PreservesSavedThemeSelectionThroughPaletteMigration(string savedId, string newId)
    {
        Assert.Same(QuickNoteThemeCatalog.Find(newId), QuickNoteThemeCatalog.Find(savedId));
    }

    [Fact]
    public void Palette_ContainsSevenDistinctStickyNoteColors()
    {
        Assert.Equal(new[] { "lemon", "sage", "rose", "lavender", "sky", "stone", "dark" },
            QuickNoteThemeCatalog.Themes.Select(theme => theme.Id));
        Assert.Equal(7, QuickNoteThemeCatalog.Themes.Select(theme => theme.Background).Distinct().Count());
        Assert.All(QuickNoteThemeCatalog.Themes, theme => Assert.NotNull(theme.HeaderBackground));
        Assert.Equal(new[] { "#FFE66E", "#A1EF9B", "#FFAFDF", "#D7AFFF", "#9EDFFF", "#E0E0E0", "#767676" },
            QuickNoteThemeCatalog.Themes.Select(QuickNoteThemeCatalog.GetSwatchColor));
        Assert.Equal("#E4F9E0", QuickNoteThemeCatalog.Find("sage").Background);
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
            AssertColor(theme.Link);
            AssertColor(theme.CodeBackground);
            AssertColor(theme.CodeText);
        });
    }

    [Fact]
    public void Themes_HaveDistinctSemanticRoles()
    {
        Assert.All(QuickNoteThemeCatalog.Themes, theme =>
        {
            Assert.NotEqual(theme.Text, theme.MutedText);
            Assert.False(string.IsNullOrWhiteSpace(theme.Link));
            Assert.False(string.IsNullOrWhiteSpace(theme.Accent));
        });
    }

    [Fact]
    public void QuoteBackground_HarmonizesWithTheme()
    {
        var sage = QuickNoteThemeCatalog.Find("sage");
        var quoteBgSage = QuickNoteThemeCatalog.GetQuoteBackground(sage);
        AssertColor(quoteBgSage);
        Assert.Equal("#A1EF9B", quoteBgSage);

        var dark = QuickNoteThemeCatalog.Find("dark");
        var quoteBgDark = QuickNoteThemeCatalog.GetQuoteBackground(dark);
        AssertColor(quoteBgDark);
        Assert.Equal("#2B2B2B", quoteBgDark);
    }

    [Fact]
    public void CodePalette_IsIdenticalAndDarkForEveryTheme()
    {
        Assert.All(QuickNoteThemeCatalog.Themes, theme =>
        {
            string headerBackground = QuickNoteThemeCatalog.GetCodeHeaderBackground(theme);
            AssertColor(headerBackground);
            Assert.NotEqual(theme.CodeBackground, headerBackground);
            Assert.Equal("#25213B", theme.CodeBackground);
            Assert.Equal("#E3DFF2", theme.CodeText);
            Assert.Equal("#302B49", headerBackground);
        });
    }

    private static void AssertColor(string color)
    {
        Assert.Matches("^#[0-9A-Fa-f]{6}$", color);
    }
}
