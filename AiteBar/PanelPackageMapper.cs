using System;
using System.IO;

namespace AiteBar;

internal static class PanelPackageMapper
{
    private const string DefaultIcon = "\uE710";
    private const string DefaultColor = "#E3E3E3";

    public static PanelPackageElement FromCustomElement(CustomElement element, Func<string, string?> mapImagePathToPackagePath)
    {
        string? packageImagePath = string.IsNullOrWhiteSpace(element.ImagePath)
            ? null
            : mapImagePathToPackagePath(element.ImagePath);

        return new PanelPackageElement
        {
            Name = element.Name ?? "",
            ActionType = NormalizeActionType(element.ActionType),
            ActionValue = element.ActionValue ?? "",
            Browser = element.Browser,
            ChromeProfile = element.ChromeProfile ?? "",
            IsAppMode = element.IsAppMode,
            IsIncognito = element.IsIncognito,
            UseRotation = element.UseRotation,
            OpenFullscreen = element.OpenFullscreen,
            IsTopmost = element.IsTopmost,
            Alt = element.Alt,
            Ctrl = element.Ctrl,
            Shift = element.Shift,
            Win = element.Win,
            Key = string.IsNullOrWhiteSpace(element.Key) ? "None" : element.Key,
            Icon = string.IsNullOrWhiteSpace(element.Icon) ? DefaultIcon : element.Icon,
            IconFont = string.IsNullOrWhiteSpace(element.IconFont) ? FontHelper.FluentKey : element.IconFont,
            Color = string.IsNullOrWhiteSpace(element.Color) ? DefaultColor : element.Color,
            Image = string.IsNullOrWhiteSpace(packageImagePath)
                ? null
                : new PanelPackageImageInfo
                {
                    PackagePath = packageImagePath,
                    Kind = "file"
                }
        };
    }

    public static CustomElement ToImportedCustomElement(
        PanelPackageElement source,
        string targetContextId,
        Func<PanelPackageImageInfo?, string> resolveImportedImagePath)
    {
        return new CustomElement
        {
            Id = Guid.NewGuid().ToString(),
            Name = source.Name?.Trim() ?? "",
            ActionType = NormalizeActionType(source.ActionType),
            ActionValue = source.ActionValue ?? "",
            Browser = source.Browser,
            ChromeProfile = source.ChromeProfile ?? "",
            IsAppMode = source.IsAppMode,
            IsIncognito = source.IsIncognito,
            UseRotation = source.UseRotation,
            OpenFullscreen = source.OpenFullscreen,
            IsTopmost = source.IsTopmost,
            LastUsedProfile = "",
            Alt = source.Alt,
            Ctrl = source.Ctrl,
            Shift = source.Shift,
            Win = source.Win,
            Key = string.IsNullOrWhiteSpace(source.Key) ? "None" : source.Key,
            Icon = string.IsNullOrWhiteSpace(source.Icon) ? DefaultIcon : source.Icon,
            IconFont = string.IsNullOrWhiteSpace(source.IconFont) ? FontHelper.FluentKey : source.IconFont,
            Color = string.IsNullOrWhiteSpace(source.Color) ? DefaultColor : source.Color,
            ImagePath = resolveImportedImagePath(source.Image),
            ContextId = targetContextId
        };
    }

    public static string BuildPackageImagePath(string sourceImagePath, int index)
    {
        string extension = Path.GetExtension(sourceImagePath);
        if (string.IsNullOrWhiteSpace(extension))
        {
            extension = ".png";
        }

        string safeExtension = extension.StartsWith('.') ? extension.ToLowerInvariant() : "." + extension.ToLowerInvariant();
        return $"icons/{index:D3}{safeExtension}";
    }

    public static bool IsPackagedImagePathSafe(string? packagePath)
    {
        if (string.IsNullOrWhiteSpace(packagePath))
        {
            return false;
        }

        string normalized = packagePath.Replace('\\', '/');
        if (!normalized.StartsWith("icons/", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (normalized.Contains("../", StringComparison.Ordinal) || normalized.Contains("..\\", StringComparison.Ordinal))
        {
            return false;
        }

        return !Path.IsPathRooted(packagePath);
    }

    private static string NormalizeActionType(string? actionType)
    {
        if (string.IsNullOrWhiteSpace(actionType))
        {
            return nameof(ActionType.Web);
        }

        return Enum.TryParse<ActionType>(actionType, out var parsed)
            ? parsed.ToString()
            : nameof(ActionType.Web);
    }
}
