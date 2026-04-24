using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AiteBar;

internal sealed class PanelPackageManifest
{
    [JsonPropertyName("formatVersion")]
    public int FormatVersion { get; set; } = 1;

    [JsonPropertyName("exportedAt")]
    public DateTime ExportedAt { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("app")]
    public PanelPackageAppInfo App { get; set; } = new();

    [JsonPropertyName("panel")]
    public PanelPackagePanelInfo Panel { get; set; } = new();

    [JsonPropertyName("elements")]
    public List<PanelPackageElement> Elements { get; set; } = [];
}

internal sealed class PanelPackageAppInfo
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "AiteBar";

    [JsonPropertyName("version")]
    public string Version { get; set; } = "";
}

internal sealed class PanelPackagePanelInfo
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("iconGlyph")]
    public string IconGlyph { get; set; } = "";
}

internal sealed class PanelPackageElement
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("actionType")]
    public string ActionType { get; set; } = nameof(AiteBar.ActionType.Web);

    [JsonPropertyName("actionValue")]
    public string ActionValue { get; set; } = "";

    [JsonPropertyName("browser")]
    public BrowserType Browser { get; set; } = BrowserType.Chrome;

    [JsonPropertyName("chromeProfile")]
    public string ChromeProfile { get; set; } = "";

    [JsonPropertyName("isAppMode")]
    public bool IsAppMode { get; set; }

    [JsonPropertyName("isIncognito")]
    public bool IsIncognito { get; set; }

    [JsonPropertyName("useRotation")]
    public bool UseRotation { get; set; }

    [JsonPropertyName("openFullscreen")]
    public bool OpenFullscreen { get; set; }

    [JsonPropertyName("isTopmost")]
    public bool IsTopmost { get; set; }

    [JsonPropertyName("alt")]
    public bool Alt { get; set; }

    [JsonPropertyName("ctrl")]
    public bool Ctrl { get; set; }

    [JsonPropertyName("shift")]
    public bool Shift { get; set; }

    [JsonPropertyName("win")]
    public bool Win { get; set; }

    [JsonPropertyName("key")]
    public string Key { get; set; } = "None";

    [JsonPropertyName("icon")]
    public string Icon { get; set; } = "\uE710";

    [JsonPropertyName("iconFont")]
    public string IconFont { get; set; } = FontHelper.FluentKey;

    [JsonPropertyName("color")]
    public string Color { get; set; } = "#E3E3E3";

    [JsonPropertyName("image")]
    public PanelPackageImageInfo? Image { get; set; }
}

internal sealed class PanelPackageImageInfo
{
    [JsonPropertyName("packagePath")]
    public string PackagePath { get; set; } = "";

    [JsonPropertyName("kind")]
    public string Kind { get; set; } = "file";
}
