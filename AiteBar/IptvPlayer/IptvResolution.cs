using System;
using System.Text.RegularExpressions;

namespace AiteBar.IptvPlayer;

[Flags]
public enum IptvResolution
{
    Any = 0,
    Sd = 1,
    Hd = 2,
    Fhd = 4,
    Qhd = 8,
    Uhd = 16
}

internal static class IptvResolutionLabels
{
    private static readonly Regex NumericLabel = new(
        @"(?<![\p{L}\p{N}])(?:[0-9]{3,5}\s*[x×]\s*(?<height>[0-9]{3,4})|(?<height>[0-9]{3,4})\s*[pi](?:[0-9]{2})?)(?![\p{L}\p{N}])",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex NamedSuffix = new(
        @"(?<![\p{L}\p{N}])(?<label>Full[ -]?HD|FHD|QHD|UHD|HD|SD|[48]K)[\s\])]*(?:\[[^\]]*\]\s*)*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    internal static IptvResolution Read(string title)
    {
        var numeric = NumericLabel.Match(title);
        if (numeric.Success && int.TryParse(numeric.Groups["height"].Value, out int height))
        {
            return height switch
            {
                >= 2160 => IptvResolution.Uhd,
                >= 1440 => IptvResolution.Qhd,
                >= 1080 => IptvResolution.Fhd,
                >= 720 => IptvResolution.Hd,
                > 0 => IptvResolution.Sd,
                _ => IptvResolution.Any
            };
        }

        return NamedSuffix.Match(title).Groups["label"].Value.ToUpperInvariant() switch
        {
            "SD" => IptvResolution.Sd,
            "HD" => IptvResolution.Hd,
            "FHD" or "FULLHD" or "FULL HD" or "FULL-HD" => IptvResolution.Fhd,
            "QHD" => IptvResolution.Qhd,
            "UHD" or "4K" or "8K" => IptvResolution.Uhd,
            _ => IptvResolution.Any
        };
    }
}
