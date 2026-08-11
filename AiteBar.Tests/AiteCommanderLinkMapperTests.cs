using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using AiteBar;
using Xunit;

namespace AiteBar.Tests;

public sealed class AiteCommanderLinkMapperTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static JsonDocument Parse(string json) =>
        JsonDocument.Parse(json);

    [Fact]
    public void EnumerateLinks_FlatLinksArray_ReturnsAllLinks()
    {
        using JsonDocument doc = Parse(@"
        {
            ""links"": [
                { ""name"": ""A"", ""url"": ""https://a.com"", ""type"": ""web"" },
                { ""name"": ""B"", ""url"": ""https://b.com"", ""type"": ""web"" },
                { ""name"": ""C"", ""url"": ""https://c.com"", ""type"": ""web"" }
            ]
        }");

        List<JsonElement> result = AiteCommanderLinkMapper.EnumerateLinks(doc.RootElement).ToList();

        Assert.Equal(3, result.Count);
        Assert.Equal("A", result[0].GetProperty("name").GetString());
        Assert.Equal("B", result[1].GetProperty("name").GetString());
        Assert.Equal("C", result[2].GetProperty("name").GetString());
    }

    [Fact]
    public void EnumerateLinks_EmptyLinksArray_ReturnsEmpty()
    {
        using JsonDocument doc = Parse(@"
        {
            ""links"": []
        }");

        List<JsonElement> result = AiteCommanderLinkMapper.EnumerateLinks(doc.RootElement).ToList();

        Assert.Empty(result);
    }

    [Fact]
    public void EnumerateLinks_NoLinksProperty_ReturnsEmpty()
    {
        using JsonDocument doc = Parse(@"
        {
            ""something"": ""else""
        }");

        List<JsonElement> result = AiteCommanderLinkMapper.EnumerateLinks(doc.RootElement).ToList();

        Assert.Empty(result);
    }

    [Fact]
    public void EnumerateLinks_CategoriesWithLinks_ReturnsAllLinks()
    {
        using JsonDocument doc = Parse(@"
        {
            ""categories"": [
                {
                    ""category"": { ""id"": 1, ""name"": ""Tools"" },
                    ""links"": [
                        { ""name"": ""T1"", ""url"": ""https://t1.com"", ""type"": ""web"" },
                        { ""name"": ""T2"", ""url"": ""https://t2.com"", ""type"": ""web"" }
                    ]
                },
                {
                    ""category"": { ""id"": 2, ""name"": ""Games"" },
                    ""links"": [
                        { ""name"": ""G1"", ""url"": ""https://g1.com"", ""type"": ""web"" }
                    ]
                }
            ]
        }");

        List<JsonElement> result = AiteCommanderLinkMapper.EnumerateLinks(doc.RootElement).ToList();

        Assert.Equal(3, result.Count);
        Assert.Equal("T1", result[0].GetProperty("name").GetString());
        Assert.Equal("T2", result[1].GetProperty("name").GetString());
        Assert.Equal("G1", result[2].GetProperty("name").GetString());
    }

    [Fact]
    public void EnumerateLinks_CategoriesWithEmptyLinks_SkipsThem()
    {
        using JsonDocument doc = Parse(@"
        {
            ""categories"": [
                {
                    ""category"": { ""id"": 1, ""name"": ""Empty"" },
                    ""links"": []
                },
                {
                    ""category"": { ""id"": 2, ""name"": ""Stuff"" },
                    ""links"": [
                        { ""name"": ""S1"", ""url"": ""https://s1.com"", ""type"": ""web"" }
                    ]
                }
            ]
        }");

        List<JsonElement> result = AiteCommanderLinkMapper.EnumerateLinks(doc.RootElement).ToList();

        Assert.Single(result);
        Assert.Equal("S1", result[0].GetProperty("name").GetString());
    }

    [Fact]
    public void EnumerateLinks_SpheresWithFlatCategoriesAndLinks_MapsLinksByCategoryId()
    {
        using JsonDocument doc = Parse(@"
        {
            ""spheres"": [
                { ""id"": 1, ""name"": ""Work"" }
            ],
            ""categories"": [
                { ""id"": 10, ""name"": ""Dev"" },
                { ""id"": 20, ""name"": ""Design"" }
            ],
            ""links"": [
                { ""name"": ""VS Code"", ""url"": ""https://code.visualstudio.com"", ""category_id"": 10, ""type"": ""web"" },
                { ""name"": ""Figma"", ""url"": ""https://figma.com"", ""category_id"": 20, ""type"": ""web"" },
                { ""name"": ""GitHub"", ""url"": ""https://github.com"", ""category_id"": 10, ""type"": ""web"" },
                { ""name"": ""Orphan"", ""url"": ""https://orphan.com"", ""category_id"": 99, ""type"": ""web"" }
            ]
        }");

        List<JsonElement> result = AiteCommanderLinkMapper.EnumerateLinks(doc.RootElement).ToList();

        Assert.Equal(3, result.Count);
        IEnumerable<string> names = result.Select(e => e.GetProperty("name").GetString()!);
        Assert.Contains("VS Code", names);
        Assert.Contains("Figma", names);
        Assert.Contains("GitHub", names);
        Assert.DoesNotContain("Orphan", names);
    }

    [Fact]
    public void EnumerateLinks_SpheresButNoCategories_ReturnsFlatLinksOnly()
    {
        using JsonDocument doc = Parse(@"
        {
            ""spheres"": [
                { ""id"": 1, ""name"": ""Work"" }
            ],
            ""links"": [
                { ""name"": ""L1"", ""url"": ""https://l1.com"", ""type"": ""web"" }
            ]
        }");

        List<JsonElement> result = AiteCommanderLinkMapper.EnumerateLinks(doc.RootElement).ToList();

        Assert.Single(result);
        Assert.Equal("L1", result[0].GetProperty("name").GetString());
    }

    [Fact]
    public void EnumerateLinks_FlatPlusNestedCategories_NoDuplicates()
    {
        using JsonDocument doc = Parse(@"
        {
            ""links"": [
                { ""name"": ""Flat1"", ""url"": ""https://flat1.com"", ""type"": ""web"" }
            ],
            ""categories"": [
                {
                    ""category"": { ""id"": 1, ""name"": ""Cat1"" },
                    ""links"": [
                        { ""name"": ""Cat1Link"", ""url"": ""https://cat1.com"", ""type"": ""web"" }
                    ]
                }
            ]
        }");

        List<JsonElement> result = AiteCommanderLinkMapper.EnumerateLinks(doc.RootElement).ToList();

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void EnumerateLinks_SpheresMode_NoDuplicatesFromFlatLinks()
    {
        using JsonDocument doc = Parse(@"
        {
            ""spheres"": [{ ""id"": 1, ""name"": ""Work"" }],
            ""categories"": [
                { ""id"": 10, ""name"": ""Dev"" },
                { ""id"": 20, ""name"": ""Design"" }
            ],
            ""links"": [
                { ""name"": ""VS Code"", ""url"": ""https://code.visualstudio.com"", ""category_id"": 10, ""type"": ""web"" },
                { ""name"": ""Figma"", ""url"": ""https://figma.com"", ""category_id"": 20, ""type"": ""web"" },
                { ""name"": ""GitHub"", ""url"": ""https://github.com"", ""category_id"": 10, ""type"": ""web"" }
            ]
        }");

        List<JsonElement> result = AiteCommanderLinkMapper.EnumerateLinks(doc.RootElement).ToList();

        Assert.Equal(3, result.Count);
    }

    [Fact]
    public void CountLinks_FlatLinksArray_ReturnsCorrectCount()
    {
        using JsonDocument doc = Parse(@"
        {
            ""links"": [
                { ""name"": ""A"", ""url"": ""https://a.com"", ""type"": ""web"" },
                { ""name"": ""B"", ""url"": ""https://b.com"", ""type"": ""web"" }
            ]
        }");

        int count = AiteCommanderLinkMapper.CountLinks(doc.RootElement);

        Assert.Equal(2, count);
    }

    [Fact]
    public void CountLinks_EmptyDocument_ReturnsZero()
    {
        using JsonDocument doc = Parse(@"{}");

        int count = AiteCommanderLinkMapper.CountLinks(doc.RootElement);

        Assert.Equal(0, count);
    }

    [Fact]
    public void GetDisplayName_CategoryName_ReturnsCategoryName()
    {
        using JsonDocument doc = Parse(@"
        {
            ""category"": { ""id"": 1, ""name"": ""Developer Tools"" },
            ""links"": []
        }");

        string name = AiteCommanderLinkMapper.GetDisplayName(doc.RootElement);

        Assert.Equal("Developer Tools", name);
    }

    [Fact]
    public void GetDisplayName_SectionName_ReturnsSectionName()
    {
        using JsonDocument doc = Parse(@"
        {
            ""section"": { ""id"": 1, ""name"": ""Work Section"" },
            ""links"": []
        }");

        string name = AiteCommanderLinkMapper.GetDisplayName(doc.RootElement);

        Assert.Equal("Work Section", name);
    }

    [Fact]
    public void GetDisplayName_CategoryTakesPriorityOverSection()
    {
        using JsonDocument doc = Parse(@"
        {
            ""category"": { ""id"": 1, ""name"": ""PriorityCategory"" },
            ""section"": { ""id"": 2, ""name"": ""FallbackSection"" },
            ""links"": []
        }");

        string name = AiteCommanderLinkMapper.GetDisplayName(doc.RootElement);

        Assert.Equal("PriorityCategory", name);
    }

    [Fact]
    public void GetDisplayName_FirstCategoriesCategoryName_ReturnsIt()
    {
        using JsonDocument doc = Parse(@"
        {
            ""categories"": [
                {
                    ""category"": { ""id"": 1, ""name"": ""FirstCategory"" },
                    ""links"": []
                },
                {
                    ""category"": { ""id"": 2, ""name"": ""SecondCategory"" },
                    ""links"": []
                }
            ]
        }");

        string name = AiteCommanderLinkMapper.GetDisplayName(doc.RootElement);

        Assert.Equal("FirstCategory", name);
    }

    [Fact]
    public void GetDisplayName_NoNames_ReturnsEmpty()
    {
        using JsonDocument doc = Parse(@"
        {
            ""links"": []
        }");

        string name = AiteCommanderLinkMapper.GetDisplayName(doc.RootElement);

        Assert.Equal("", name);
    }

    [Fact]
    public void MapActionType_Web_ReturnsWeb()
    {
        Assert.Equal(nameof(ActionType.Web), AiteCommanderLinkMapper.MapActionType("web"));
    }

    [Fact]
    public void MapActionType_File_ReturnsFile()
    {
        Assert.Equal(nameof(ActionType.File), AiteCommanderLinkMapper.MapActionType("file"));
    }

    [Fact]
    public void MapActionType_Folder_ReturnsFolder()
    {
        Assert.Equal(nameof(ActionType.Folder), AiteCommanderLinkMapper.MapActionType("folder"));
    }

    [Fact]
    public void MapActionType_Program_ReturnsProgram()
    {
        Assert.Equal(nameof(ActionType.Program), AiteCommanderLinkMapper.MapActionType("program"));
    }

    [Fact]
    public void MapActionType_Script_ReturnsScriptFile()
    {
        Assert.Equal(nameof(ActionType.ScriptFile), AiteCommanderLinkMapper.MapActionType("script"));
    }

    [Fact]
    public void MapActionType_Unknown_ReturnsWeb()
    {
        Assert.Equal(nameof(ActionType.Web), AiteCommanderLinkMapper.MapActionType("something_unknown"));
    }

    [Fact]
    public void MapActionType_Empty_ReturnsWeb()
    {
        Assert.Equal(nameof(ActionType.Web), AiteCommanderLinkMapper.MapActionType(""));
    }

    [Fact]
    public void MapActionType_Null_ReturnsWeb()
    {
        Assert.Equal(nameof(ActionType.Web), AiteCommanderLinkMapper.MapActionType(null!));
    }

    [Fact]
    public void MapActionType_UpperCase_IsCaseInsensitive()
    {
        Assert.Equal(nameof(ActionType.Web), AiteCommanderLinkMapper.MapActionType("WEB"));
        Assert.Equal(nameof(ActionType.Program), AiteCommanderLinkMapper.MapActionType("Program"));
    }

    [Fact]
    public void PickGlyphForActionType_Web_ReturnsWebGlyph()
    {
        Assert.Equal("\uE754", AiteCommanderLinkMapper.PickGlyphForActionType(nameof(ActionType.Web)));
    }

    [Fact]
    public void PickGlyphForActionType_File_ReturnsFileGlyph()
    {
        Assert.Equal("\uE8A5", AiteCommanderLinkMapper.PickGlyphForActionType(nameof(ActionType.File)));
    }

    [Fact]
    public void PickGlyphForActionType_Folder_ReturnsFolderGlyph()
    {
        Assert.Equal("\uE8B7", AiteCommanderLinkMapper.PickGlyphForActionType(nameof(ActionType.Folder)));
    }

    [Fact]
    public void PickGlyphForActionType_Program_ReturnsProgramGlyph()
    {
        Assert.Equal("\uE7F6", AiteCommanderLinkMapper.PickGlyphForActionType(nameof(ActionType.Program)));
    }

    [Fact]
    public void PickGlyphForActionType_ScriptFile_ReturnsScriptGlyph()
    {
        Assert.Equal("\uE943", AiteCommanderLinkMapper.PickGlyphForActionType(nameof(ActionType.ScriptFile)));
    }

    [Fact]
    public void PickGlyphForActionType_Unknown_ReturnsDefaultGlyph()
    {
        Assert.Equal("\uF45B", AiteCommanderLinkMapper.PickGlyphForActionType("UnknownType"));
    }

    [Fact]
    public void PickGlyphForActionType_Null_ReturnsDefaultGlyph()
    {
        Assert.Equal("\uF45B", AiteCommanderLinkMapper.PickGlyphForActionType(null!));
    }

    [Fact]
    public void ToCustomElement_WebType_MapsCorrectly()
    {
        using JsonDocument doc = Parse(@"
        {
            ""name"": ""GitHub"",
            ""url"": ""https://github.com"",
            ""type"": ""web""
        }");

        CustomElement result = AiteCommanderLinkMapper.ToCustomElement(
            doc.RootElement,
            "ctx-1",
            _ => "");

        Assert.NotNull(result.Id);
        Assert.NotEmpty(result.Id);
        Assert.Equal("GitHub", result.Name);
        Assert.Equal(nameof(ActionType.Web), result.ActionType);
        Assert.Equal("https://github.com", result.ActionValue);
        Assert.Equal("ctx-1", result.ContextId);
        Assert.Equal("\uE754", result.Icon);
        Assert.Equal(FontHelper.FluentKey, result.IconFont);
        Assert.Equal("#E3E3E3", result.Color);
        Assert.Equal("", result.ImagePath);
        Assert.Equal(BrowserType.Chrome, result.Browser);
        Assert.False(result.IsAppMode);
        Assert.False(result.IsIncognito);
        Assert.False(result.Ctrl);
        Assert.False(result.Alt);
        Assert.False(result.Shift);
        Assert.False(result.Win);
        Assert.Equal("None", result.Key);
    }

    [Fact]
    public void ToCustomElement_FileType_MapsCorrectly()
    {
        using JsonDocument doc = Parse(@"
        {
            ""name"": ""Report"",
            ""url"": ""C:\\Docs\\report.pdf"",
            ""type"": ""file""
        }");

        CustomElement result = AiteCommanderLinkMapper.ToCustomElement(
            doc.RootElement,
            "ctx-1",
            _ => "");

        Assert.Equal(nameof(ActionType.File), result.ActionType);
        Assert.Equal(@"C:\Docs\report.pdf", result.ActionValue);
        Assert.Equal("\uE8A5", result.Icon);
    }

    [Fact]
    public void ToCustomElement_FolderType_MapsCorrectly()
    {
        using JsonDocument doc = Parse(@"
        {
            ""name"": ""Projects"",
            ""url"": ""D:\\Projects"",
            ""type"": ""folder""
        }");

        CustomElement result = AiteCommanderLinkMapper.ToCustomElement(
            doc.RootElement,
            "ctx-1",
            _ => "");

        Assert.Equal(nameof(ActionType.Folder), result.ActionType);
        Assert.Equal(@"D:\Projects", result.ActionValue);
        Assert.Equal("\uE8B7", result.Icon);
    }

    [Fact]
    public void ToCustomElement_ProgramType_MapsCorrectly()
    {
        using JsonDocument doc = Parse(@"
        {
            ""name"": ""VS Code"",
            ""url"": ""C:\\Program Files\\Code.exe"",
            ""type"": ""program""
        }");

        CustomElement result = AiteCommanderLinkMapper.ToCustomElement(
            doc.RootElement,
            "ctx-1",
            _ => "");

        Assert.Equal(nameof(ActionType.Program), result.ActionType);
        Assert.Equal(@"C:\Program Files\Code.exe", result.ActionValue);
        Assert.Equal("\uE7F6", result.Icon);
    }

    [Fact]
    public void ToCustomElement_ScriptType_MapsToScriptFile()
    {
        using JsonDocument doc = Parse(@"
        {
            ""name"": ""Build"",
            ""url"": ""C:\\Scripts\\build.ps1"",
            ""type"": ""script""
        }");

        CustomElement result = AiteCommanderLinkMapper.ToCustomElement(
            doc.RootElement,
            "ctx-1",
            _ => "");

        Assert.Equal(nameof(ActionType.ScriptFile), result.ActionType);
        Assert.Equal(@"C:\Scripts\build.ps1", result.ActionValue);
        Assert.Equal("\uE943", result.Icon);
    }

    [Fact]
    public void ToCustomElement_UnknownType_DefaultsToWeb()
    {
        using JsonDocument doc = Parse(@"
        {
            ""name"": ""Strange"",
            ""url"": ""https://strange.com"",
            ""type"": ""weirdo""
        }");

        CustomElement result = AiteCommanderLinkMapper.ToCustomElement(
            doc.RootElement,
            "ctx-1",
            _ => "");

        Assert.Equal(nameof(ActionType.Web), result.ActionType);
        Assert.Equal("https://strange.com", result.ActionValue);
        Assert.Equal("\uE754", result.Icon);
    }

    [Fact]
    public void ToCustomElement_MissingType_DefaultsToWeb()
    {
        using JsonDocument doc = Parse(@"
        {
            ""name"": ""NoType"",
            ""url"": ""https://notype.com""
        }");

        CustomElement result = AiteCommanderLinkMapper.ToCustomElement(
            doc.RootElement,
            "ctx-1",
            _ => "");

        Assert.Equal(nameof(ActionType.Web), result.ActionType);
    }

    [Fact]
    public void ToCustomElement_WithIconPath_ResolvesImageAndFallsBackGlyphToDefault()
    {
        using JsonDocument doc = Parse(@"
        {
            ""name"": ""Custom Icon"",
            ""url"": ""https://custom.com"",
            ""type"": ""web"",
            ""icon_path"": ""icons/my_icon.png""
        }");

        var iconMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["my_icon.png"] = @"C:\Icons\local_my_icon.png"
        };

        CustomElement result = AiteCommanderLinkMapper.ToCustomElement(
            doc.RootElement,
            "ctx-1",
            iconRef =>
            {
                if (string.IsNullOrWhiteSpace(iconRef))
                    return "";
                string key = System.IO.Path.GetFileName(iconRef) ?? "";
                return iconMap.TryGetValue(key, out string? p) ? p : "";
            });

        Assert.Equal(@"C:\Icons\local_my_icon.png", result.ImagePath);
        Assert.Equal("\uF45B", result.Icon);
    }

    [Fact]
    public void ToCustomElement_WithUnresolvableIconPath_ImagePathIsEmpty()
    {
        using JsonDocument doc = Parse(@"
        {
            ""name"": ""Missing Icon"",
            ""url"": ""https://miss.com"",
            ""type"": ""web"",
            ""icon_path"": ""icons/nope.png""
        }");

        CustomElement result = AiteCommanderLinkMapper.ToCustomElement(
            doc.RootElement,
            "ctx-1",
            _ => "");

        Assert.Equal("", result.ImagePath);
        Assert.Equal("\uE754", result.Icon);
    }

    [Fact]
    public void ToCustomElement_TrimsNameAndUrl()
    {
        using JsonDocument doc = Parse(@"
        {
            ""name"": ""   Padded   "",
            ""url"": ""   https://pad.com   "",
            ""type"": ""web""
        }");

        CustomElement result = AiteCommanderLinkMapper.ToCustomElement(
            doc.RootElement,
            "ctx-1",
            _ => "");

        Assert.Equal("Padded", result.Name);
        Assert.Equal("https://pad.com", result.ActionValue);
    }

    [Fact]
    public void ToCustomElement_GeneratesUniqueId()
    {
        using JsonDocument doc = Parse(@"
        {
            ""name"": ""A"",
            ""url"": ""https://a.com"",
            ""type"": ""web""
        }");

        CustomElement a = AiteCommanderLinkMapper.ToCustomElement(doc.RootElement, "ctx-1", _ => "");
        CustomElement b = AiteCommanderLinkMapper.ToCustomElement(doc.RootElement, "ctx-1", _ => "");

        Assert.NotEqual(a.Id, b.Id);
    }
}
