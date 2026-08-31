using System.Text.Json;

namespace AiteBar;

internal sealed record IconCatalogEntry(
    int CodePoint,
    string Symbol,
    string? DisplayName,
    string Tooltip,
    string SearchKey);

internal sealed class IconCatalogService
{
    private static readonly IReadOnlyDictionary<int, string[]> FontAwesomeNameAliases =
        new Dictionary<int, string[]>
        {
            [0xF099] = ["twitter"],
            [0xE61B] = ["twitter", "x", "x-twitter"],
            [0xF09A] = ["facebook", "meta"],
            [0xF09B] = ["github"],
            [0xF113] = ["github-alt"],
            [0xF296] = ["gitlab"],
            [0xF0E1] = ["linkedin", "linkedin-in"],
            [0xF16D] = ["instagram"],
            [0xF167] = ["youtube"],
            [0xF1A0] = ["google"],
            [0xF179] = ["apple", "ios"],
            [0xF17A] = ["windows", "microsoft"],
            [0xF232] = ["whatsapp"],
            [0xF2C6] = ["telegram"],
            [0xF3FE] = ["telegram"],
            [0xF392] = ["discord"],
            [0xF395] = ["docker"],
            [0xF375] = ["aws", "amazon-web-services"],
            [0xF3D3] = ["node", "node-js"],
            [0xF419] = ["node"],
            [0xF841] = ["git", "git-alt"],
            [0xF198] = ["slack"],
            [0xF3EF] = ["slack"],
            [0xF413] = ["yandex"],
            [0xF414] = ["yandex-international"],
            [0xF1E8] = ["twitch"],
            [0xF1A1] = ["reddit"],
            [0xF281] = ["reddit-alien"],
            [0xF1BC] = ["spotify"],
            [0xF189] = ["vk", "vkontakte"],
            [0xF263] = ["odnoklassniki", "ok"],
            [0xF799] = ["figma"],
            [0xE7D9] = ["notion"],
            [0xE671] = ["bluesky"],
            [0xE618] = ["threads"],
            [0xE07B] = ["tiktok"]
        };

    private readonly Lazy<IReadOnlyDictionary<int, string>> _fluentMap;

    public IconCatalogService(Func<string, Stream> resourceStreamFactory)
    {
        ArgumentNullException.ThrowIfNull(resourceStreamFactory);
        _fluentMap = new Lazy<IReadOnlyDictionary<int, string>>(
            () => LoadFluentMap(resourceStreamFactory),
            LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public void Warmup() => _ = _fluentMap.Value;

    public IReadOnlyList<IconCatalogEntry> BuildEntries(
        string fontName,
        IDictionary<int, ushort> glyphMap)
    {
        ArgumentNullException.ThrowIfNull(fontName);
        ArgumentNullException.ThrowIfNull(glyphMap);

        IReadOnlyDictionary<int, string>? namedIcons = fontName == FontHelper.FluentKey
            ? _fluentMap.Value
            : null;
        int[] codes = GetDisplayCodes(glyphMap, namedIcons);
        var entries = new List<IconCatalogEntry>(codes.Length);

        foreach (int code in codes)
        {
            string symbol;
            try
            {
                symbol = char.ConvertFromUtf32(code);
            }
            catch (ArgumentOutOfRangeException)
            {
                continue;
            }

            string? iconName = null;
            namedIcons?.TryGetValue(code, out iconName);
            entries.Add(new IconCatalogEntry(
                code,
                symbol,
                iconName,
                iconName is null ? $"U+{code:X4}" : $"{iconName}  U+{code:X4}",
                BuildSearchKey(fontName, code, iconName)));
        }

        return entries;
    }

    private static int[] GetDisplayCodes(
        IDictionary<int, ushort> glyphMap,
        IReadOnlyDictionary<int, string>? namedIcons)
    {
        if (namedIcons is not null)
        {
            return [.. namedIcons.Keys
                .Where(code => glyphMap.TryGetValue(code, out ushort glyphIndex) && glyphIndex != 0)
                .OrderBy(code => code)];
        }

        return [.. glyphMap.Keys
            .Where(code => glyphMap[code] != 0)
            .Where(code => code >= 0xE000 && code <= 0x10FFFF)
            .Where(code => code < 0xD800 || code > 0xDFFF)
            .OrderBy(code => code)];
    }

    private static string BuildSearchKey(string fontName, int code, string? iconName)
    {
        List<string> keyParts = [$"{code:X4}".ToLowerInvariant()];
        if (!string.IsNullOrWhiteSpace(iconName))
        {
            keyParts.Add(iconName.ToLowerInvariant());
        }

        if (fontName == FontHelper.BrandsKey &&
            FontAwesomeNameAliases.TryGetValue(code, out string[]? aliases))
        {
            keyParts.AddRange(aliases);
        }

        return string.Join(" ", keyParts);
    }

    private static IReadOnlyDictionary<int, string> LoadFluentMap(
        Func<string, Stream> resourceStreamFactory)
    {
        using Stream stream = resourceStreamFactory(FontHelper.FluentCodepointsResource);
        using var reader = new StreamReader(stream);
        Dictionary<string, int> raw = JsonSerializer.Deserialize<Dictionary<string, int>>(
            reader.ReadToEnd())
            ?? throw new InvalidOperationException(
                LocalizationService.Get("IconPicker_InvalidFluentMetadata"));

        return raw
            .Where(pair => pair.Key.EndsWith("_24_regular", StringComparison.Ordinal))
            .GroupBy(pair => pair.Value)
            .Select(group => group.OrderBy(pair => pair.Key, StringComparer.Ordinal).First())
            .ToDictionary(pair => pair.Value, pair => FormatFluentName(pair.Key));
    }

    private static string FormatFluentName(string rawName) => rawName
        .Replace("ic_fluent_", "", StringComparison.Ordinal)
        .Replace("_24_regular", "", StringComparison.Ordinal)
        .Replace("_", " ");
}
