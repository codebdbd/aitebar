using System;
using System.Collections.Generic;
using System.Linq;

namespace AiteBar;

internal sealed class UnifiedButtonService
{
    private readonly AppSettingsService _settingsService;

    private static readonly List<UtilityButtonDef> UtilityButtons = new()
    {
        new("Search", "\uEA7C", "#3ABEFF", "ShowPresetSearch", "Main_SearchTooltip"),
        new("Screenshot", "\uF68E", "#60A5FA", "ShowPresetScreenshot", "Main_ScreenshotTooltip"),
        new("Record", "\uF535", "#FB7185", "ShowPresetVideo", "Main_RecordTooltip"),
        new("Calc", "\uF06C", "#A3E635", "ShowPresetCalc", "Main_CalcTooltip"),
        new("Explorer", "\uF42F", "#F59E0B", "ShowPresetExplorer", "Main_ExplorerTooltip"),
        new("Downloads", "\uF151", "#34D399", "ShowPresetDownloads", "Main_DownloadsTooltip"),
        new("FileSorter", "\uF202", "#60A5FA", "ShowPresetFileSorter", "Main_FileSorterTooltip"),
        new("IconConverter", "\uF12F", "#2DD4BF", "ShowPresetIconConverter", "Main_IconConverterTooltip"),
        new("TimerStopwatch", "\uED88", "#38BDF8", "ShowPresetTimerStopwatch", "Main_TimerStopwatchTooltip"),
        new("ColorPicker", "\uE5FE", "#A855F7", "ShowPresetColorPicker", "Main_ColorPickerTooltip"),
        new("QuickNote", "\uF56F", "#22D3EE", "ShowPresetQuickNote", "Main_QuickNoteTooltip")
    };

    public UnifiedButtonService(AppSettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    public List<UnifiedButton> BuildUnifiedList(string activeContextId)
    {
        var result = new List<UnifiedButton>();
        bool isPrimaryContext = string.Equals(activeContextId, _settingsService.GetPrimaryContextId(), StringComparison.Ordinal);

        // Add utilities only in primary context
        if (isPrimaryContext)
        {
            // Get visible utility definitions, ordered by UtilityButtonOrder, then the rest
            var visibleUtilityDefs = UtilityButtons.Where(def => GetUtilityVisibility(def.SettingsKey)).ToList();
            
            // Order by UtilityButtonOrder if exists
            var orderedUtilityDefs = new List<UtilityButtonDef>();
            var remainingUtilityDefs = new List<UtilityButtonDef>(visibleUtilityDefs);
            
            foreach (var id in _settingsService.Settings.UtilityButtonOrder)
            {
                var def = remainingUtilityDefs.FirstOrDefault(d => d.Id == id);
                if (def != null)
                {
                    orderedUtilityDefs.Add(def);
                    remainingUtilityDefs.Remove(def);
                }
            }
            
            orderedUtilityDefs.AddRange(remainingUtilityDefs);

            foreach (var def in orderedUtilityDefs)
            {
                result.Add(new UnifiedButton
                {
                    Id = def.Id,
                    Name = LocalizationService.Get(def.TooltipKey),
                    Icon = def.Icon,
                    IconFont = FontHelper.FluentKey,
                    Color = def.Color,
                    Type = UnifiedButtonType.Utility,
                    Order = result.Count,
                    IsVisible = true,
                    SettingsKey = def.SettingsKey
                });
            }
        }

        // Add user buttons
        var userElements = _settingsService.Elements
            .Where(e => e.ContextId == activeContextId)
            .ToList();
        foreach (var el in userElements)
        {
            result.Add(new UnifiedButton
            {
                Id = el.Id,
                Name = el.Name,
                Icon = el.Icon,
                IconFont = el.IconFont,
                Color = el.Color,
                ImagePath = el.ImagePath,
                Type = UnifiedButtonType.User,
                Order = result.Count,
                SourceElement = el
            });
        }

        return result;
    }

    private bool GetUtilityVisibility(string settingsKey)
    {
        var settings = _settingsService.Settings;
        return settingsKey switch
        {
            "ShowPresetSearch" => settings.ShowPresetSearch,
            "ShowPresetScreenshot" => settings.ShowPresetScreenshot,
            "ShowPresetVideo" => settings.ShowPresetVideo,
            "ShowPresetCalc" => settings.ShowPresetCalc,
            "ShowPresetExplorer" => settings.ShowPresetExplorer,
            "ShowPresetDownloads" => settings.ShowPresetDownloads,
            "ShowPresetFileSorter" => settings.ShowPresetFileSorter,
            "ShowPresetIconConverter" => settings.ShowPresetIconConverter,
            "ShowPresetTimerStopwatch" => settings.ShowPresetTimerStopwatch,
            "ShowPresetColorPicker" => settings.ShowPresetColorPicker,
            "ShowPresetQuickNote" => settings.ShowPresetQuickNote,
            _ => false
        };
    }
}

public record UtilityButtonDef(string Id, string Icon, string Color, string SettingsKey, string TooltipKey);
