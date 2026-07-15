using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml.Linq;
using AiteBar;
using Xunit;

namespace AiteBar.Tests;

[Collection("LocalizationStateTestCollection")]
public sealed class LocalizationServiceTests
{
    private static readonly string[] LocalizedCultures = ["de", "uk", "ru"];

    [Fact]
    public void NormalizeCultureName_Null_ReturnsAuto()
    {
        Assert.Equal(LocalizationService.AutoCulture, LocalizationService.NormalizeCultureName(null));
    }

    [Fact]
    public void NormalizeCultureName_Empty_ReturnsAuto()
    {
        Assert.Equal(LocalizationService.AutoCulture, LocalizationService.NormalizeCultureName(""));
    }

    [Fact]
    public void NormalizeCultureName_Whitespace_ReturnsAuto()
    {
        Assert.Equal(LocalizationService.AutoCulture, LocalizationService.NormalizeCultureName("   "));
    }

    [Theory]
    [InlineData("en")]
    [InlineData("EN")]
    [InlineData("En")]
    public void NormalizeCultureName_EnglishVariants_ReturnsEn(string input)
    {
        Assert.Equal("en", LocalizationService.NormalizeCultureName(input));
    }

    [Theory]
    [InlineData("de")]
    [InlineData("uk")]
    [InlineData("ru")]
    public void NormalizeCultureName_SupportedCultures_ReturnsNormalized(string input)
    {
        Assert.Equal(input, LocalizationService.NormalizeCultureName(input));
    }

    [Theory]
    [InlineData("DE")]
    [InlineData("UK")]
    [InlineData("RU")]
    public void NormalizeCultureName_SupportedCulturesUpperCase_ReturnsNormalized(string input)
    {
        Assert.Equal(input.ToLower(), LocalizationService.NormalizeCultureName(input));
    }

    [Fact]
    public void NormalizeCultureName_UnsupportedCulture_ReturnsAuto()
    {
        Assert.Equal(LocalizationService.AutoCulture, LocalizationService.NormalizeCultureName("fr"));
    }

    [Fact]
    public void NormalizeCultureName_InvalidCulture_ReturnsAuto()
    {
        Assert.Equal(LocalizationService.AutoCulture, LocalizationService.NormalizeCultureName("invalid-culture"));
    }

    [Fact]
    public void NormalizeCultureName_Auto_ReturnsAuto()
    {
        Assert.Equal(LocalizationService.AutoCulture, LocalizationService.NormalizeCultureName(LocalizationService.AutoCulture));
    }

    [Fact]
    public void ResolveCulture_Auto_ReturnsOSSupportedOrDefault()
    {
        var culture = LocalizationService.ResolveCulture(LocalizationService.AutoCulture);
        Assert.NotNull(culture);
        Assert.Contains(culture.TwoLetterISOLanguageName, new[] { "en", "de", "uk", "ru" });
    }

    [Fact]
    public void ResolveCulture_En_ReturnsEnglishCulture()
    {
        var culture = LocalizationService.ResolveCulture("en");
        Assert.Equal("en", culture.TwoLetterISOLanguageName);
    }

    [Fact]
    public void ResolveCulture_SupportedCulture_ReturnsCorrectCulture()
    {
        var culture = LocalizationService.ResolveCulture("de");
        Assert.Equal("de", culture.TwoLetterISOLanguageName);
    }

    [Fact]
    public void ApplyCulture_UpdatesResolvedCultureWithoutMutatingThreadCultures()
    {
        string originalResolvedCulture = LocalizationService.ResolvedCulture.Name;
        CultureInfo originalCulture = CultureInfo.CurrentCulture;
        CultureInfo originalUiCulture = CultureInfo.CurrentUICulture;
        CultureInfo? originalDefaultCulture = CultureInfo.DefaultThreadCurrentCulture;
        CultureInfo? originalDefaultUiCulture = CultureInfo.DefaultThreadCurrentUICulture;

        try
        {
            LocalizationService.ApplyCulture("de");

            Assert.Equal("de", LocalizationService.ResolvedCulture.TwoLetterISOLanguageName);
            Assert.Equal(originalCulture, CultureInfo.CurrentCulture);
            Assert.Equal(originalUiCulture, CultureInfo.CurrentUICulture);
            Assert.Equal(originalDefaultCulture, CultureInfo.DefaultThreadCurrentCulture);
            Assert.Equal(originalDefaultUiCulture, CultureInfo.DefaultThreadCurrentUICulture);
        }
        finally
        {
            LocalizationService.ApplyCulture(originalResolvedCulture);
        }
    }

    [Fact]
    public void ApplyCulture_SameCulture_DoesNotRaiseCultureChangedTwice()
    {
        string originalPreference = LocalizationService.ResolvedCulture.Name;
        int eventCount = 0;

        void Handler(object? sender, EventArgs e) => Interlocked.Increment(ref eventCount);

        LocalizationService.CultureChanged += Handler;
        try
        {
            LocalizationService.ApplyCulture("en");
            eventCount = 0;

            LocalizationService.ApplyCulture("ru");
            LocalizationService.ApplyCulture("ru");

            Assert.Equal(1, eventCount);
        }
        finally
        {
            LocalizationService.CultureChanged -= Handler;
            LocalizationService.ApplyCulture(originalPreference);
        }
    }

    [Fact]
    public void Get_ExistingKey_ReturnsValue()
    {
        var result = LocalizationService.Get("Common_Cancel");
        Assert.NotNull(result);
        Assert.NotEqual("[[Common_Cancel]]", result);
    }

    [Fact]
    public void Get_NonExistingKey_ReturnsPlaceholder()
    {
        var result = LocalizationService.Get("NonExistingKey_12345");
        Assert.Equal("[[NonExistingKey_12345]]", result);
    }

    [Fact]
    public void Format_WithArgs_FormatsCorrectly()
    {
        var result = LocalizationService.Format("DeleteButtonConfirm", "Test Button");
        Assert.Contains("Test Button", result);
    }

    [Fact]
    public void Format_WithMultipleArgs_FormatsCorrectly()
    {
        var result = LocalizationService.Format("Element_CopySuffixFormat", 2);
        Assert.Contains("2", result);
    }

    [Fact]
    public void ResourceFiles_HaveSameKeysAndFormatPlaceholders()
    {
        string resourcesDirectory = Path.Combine(FindRepoRoot(), "AiteBar", "Resources");
        Dictionary<string, string> neutral = LoadResources(Path.Combine(resourcesDirectory, "Strings.resx"));
        Assert.All(neutral, entry => Assert.False(string.IsNullOrWhiteSpace(entry.Value), $"Neutral resource '{entry.Key}' is empty."));

        foreach (string culture in LocalizedCultures)
        {
            Dictionary<string, string> localized = LoadResources(Path.Combine(resourcesDirectory, $"Strings.{culture}.resx"));

            Assert.Equal(neutral.Keys.Order(), localized.Keys.Order());
            foreach ((string key, string neutralValue) in neutral)
            {
                Assert.False(string.IsNullOrWhiteSpace(localized[key]), $"Localized resource '{culture}:{key}' is empty.");
                Assert.Equal(
                    ExtractFormatPlaceholders(neutralValue),
                    ExtractFormatPlaceholders(localized[key]));
            }
        }
    }

    [Fact]
    public void XamlTextProperties_DoNotContainTranslatableLiteralText()
    {
        string appDirectory = Path.Combine(FindRepoRoot(), "AiteBar");
        HashSet<string> allowedTechnicalText =
        [
            "AiteBar", "© 2026 Codebdbd", ".NET 10", "WPF",
            "Ctrl", "Shift", "Alt", "Win",
            "Chrome", "Edge", "Brave", "Yandex", "Firefox",
            "B", "I", "U", "Tx",
            "TXT", "16 px", "32 px", "48 px", "256 px"
        ];
        HashSet<string> textProperties = ["Text", "Content", "Header", "Title", "ToolTip", "Description"];

        var violations = Directory.GetFiles(appDirectory, "*.xaml")
            .SelectMany(path => XDocument.Load(path).Descendants()
                .SelectMany(element => element.Attributes()
                    .Where(attribute => textProperties.Contains(attribute.Name.LocalName))
                    .Select(attribute => (Path: path, Value: attribute.Value))))
            .Where(candidate =>
                !candidate.Value.StartsWith('{') &&
                Regex.IsMatch(candidate.Value, @"[\p{L}]") &&
                !allowedTechnicalText.Contains(candidate.Value.Trim()))
            .Select(candidate => $"{Path.GetFileName(candidate.Path)}: {candidate.Value.Trim()}")
            .ToArray();

        Assert.Empty(violations);
    }

    private static Dictionary<string, string> LoadResources(string path)
    {
        return XDocument.Load(path)
            .Root!
            .Elements("data")
            .ToDictionary(
                element => element.Attribute("name")!.Value,
                element => element.Element("value")?.Value ?? string.Empty,
                StringComparer.Ordinal);
    }

    private static string[] ExtractFormatPlaceholders(string value)
    {
        return Regex.Matches(value, @"\{\d+(?:[^}]*)\}")
            .Select(match => match.Value)
            .Order()
            .ToArray();
    }

    private static string FindRepoRoot()
    {
        string? current = AppContext.BaseDirectory;

        while (!string.IsNullOrEmpty(current))
        {
            if (File.Exists(Path.Combine(current, "AiteBar.sln")))
            {
                return current;
            }

            current = Directory.GetParent(current)?.FullName;
        }

        throw new DirectoryNotFoundException("Repository root with AiteBar.sln was not found.");
    }
}
