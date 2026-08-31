using AiteBar;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace AiteBar.Tests;

public sealed class IconCatalogServiceTests
{
    [Fact]
    public void BuildEntries_FluentMetadata_PreservesNamesOrderTooltipsAndSearchKeys()
    {
        const string metadata = """
            {
              "ic_fluent_zeta_24_regular": 62001,
              "ic_fluent_alpha_24_regular": 62000,
              "ic_fluent_alpha_20_regular": 61999,
              "ic_fluent_alpha_alias_24_regular": 62000
            }
            """;
        var service = CreateService(metadata);
        var glyphMap = new Dictionary<int, ushort>
        {
            [61999] = 1,
            [62000] = 2,
            [62001] = 3
        };

        IReadOnlyList<IconCatalogEntry> entries = service.BuildEntries(
            FontHelper.FluentKey,
            glyphMap);

        Assert.Collection(
            entries,
            first =>
            {
                Assert.Equal(62000, first.CodePoint);
                Assert.Equal(char.ConvertFromUtf32(62000), first.Symbol);
                Assert.Equal("alpha", first.DisplayName);
                Assert.Equal("alpha  U+F230", first.Tooltip);
                Assert.Equal("f230 alpha", first.SearchKey);
            },
            second =>
            {
                Assert.Equal(62001, second.CodePoint);
                Assert.Equal("zeta", second.DisplayName);
            });
    }

    [Fact]
    public void BuildEntries_Brands_PreservesAliasesAndSupplementaryGlyphs()
    {
        var service = CreateService("{}");
        var glyphMap = new Dictionary<int, ushort>
        {
            [0x0041] = 1,
            [0xD800] = 1,
            [0xF09B] = 2,
            [0xF0AC5] = 3,
            [0xF099] = 0
        };

        IReadOnlyList<IconCatalogEntry> entries = service.BuildEntries(
            FontHelper.BrandsKey,
            glyphMap);

        Assert.Collection(
            entries,
            github =>
            {
                Assert.Equal(0xF09B, github.CodePoint);
                Assert.Null(github.DisplayName);
                Assert.Equal("U+F09B", github.Tooltip);
                Assert.Equal("f09b github", github.SearchKey);
            },
            supplementary =>
            {
                Assert.Equal(0xF0AC5, supplementary.CodePoint);
                Assert.Equal(2, supplementary.Symbol.Length);
                Assert.Equal("f0ac5", supplementary.SearchKey);
            });
    }

    [Fact]
    public void Warmup_ParsesMetadataOnlyOnce()
    {
        int openCount = 0;
        var service = new IconCatalogService(_ =>
        {
            openCount++;
            return Utf8Stream("{\"ic_fluent_add_24_regular\": 57357}");
        });

        service.Warmup();
        service.Warmup();
        _ = service.BuildEntries(
            FontHelper.FluentKey,
            new Dictionary<int, ushort> { [57357] = 1 });

        Assert.Equal(1, openCount);
    }

    [Fact]
    public void BundledFluentCatalog_ContainsExpectedUniqueRegularEntries()
    {
        string resourcePath = Path.Combine(
            FindRepoRoot(),
            "AiteBar",
            "Resources",
            "FluentSystemIcons.json");
        string metadata = File.ReadAllText(resourcePath);
        Dictionary<string, int> raw = JsonSerializer.Deserialize<Dictionary<string, int>>(metadata)!;
        var glyphMap = raw
            .Where(pair => pair.Key.EndsWith("_24_regular", StringComparison.Ordinal))
            .GroupBy(pair => pair.Value)
            .ToDictionary(group => group.Key, _ => (ushort)1);
        var service = CreateService(metadata);

        IReadOnlyList<IconCatalogEntry> entries = service.BuildEntries(
            FontHelper.FluentKey,
            glyphMap);

        Assert.Equal(2426, entries.Count);
        Assert.Equal(entries.Count, entries.Select(entry => entry.CodePoint).Distinct().Count());
        Assert.All(entries, entry => Assert.False(string.IsNullOrWhiteSpace(entry.DisplayName)));
    }

    private static IconCatalogService CreateService(string metadata) =>
        new(_ => Utf8Stream(metadata));

    private static MemoryStream Utf8Stream(string value) =>
        new(Encoding.UTF8.GetBytes(value), writable: false);

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
