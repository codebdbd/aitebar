using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace AiteBar;

internal sealed record UtilityButtonDefinition(
    string Id,
    string Icon,
    string Color,
    string TooltipKey,
    Func<AppSettings, bool> VisibilityGetter,
    Action<AppSettings, bool> VisibilitySetter)
{
    public bool IsVisible(AppSettings settings) => VisibilityGetter(settings);

    public void SetVisible(AppSettings settings, bool visible) => VisibilitySetter(settings, visible);
}

internal static class UtilityIconColors
{
    public const string SearchAndNavigation = "#93C5FD";
    public const string ScreenCapture = "#93C5FD";
    public const string Productivity = "#CDEBB0";
    public const string FolderAccess = "#FFD166";
    public const string AssetCreation = "#5EEAD4";
    public const string TextWorkspace = "#86EFAC";
    public const string AiTools = "#93C5FD";
}

internal static class UtilityButtonCatalog
{
    public static UtilityButtonDefinition Search { get; } = new(
        "Search", "\uEA7C", UtilityIconColors.SearchAndNavigation, "Main_SearchTooltip",
        settings => settings.ShowPresetSearch,
        (settings, visible) => settings.ShowPresetSearch = visible);

    public static UtilityButtonDefinition Screenshot { get; } = new(
        "Screenshot", "\uF68E", UtilityIconColors.ScreenCapture, "Main_ScreenshotTooltip",
        settings => settings.ShowPresetScreenshot,
        (settings, visible) => settings.ShowPresetScreenshot = visible);

    public static UtilityButtonDefinition Record { get; } = new(
        "Record", "\uF535", UtilityIconColors.ScreenCapture, "Main_RecordTooltip",
        settings => settings.ShowPresetVideo,
        (settings, visible) => settings.ShowPresetVideo = visible);

    public static UtilityButtonDefinition Calculator { get; } = new(
        "Calc", "\uF06C", UtilityIconColors.Productivity, "Main_CalcTooltip",
        settings => settings.ShowPresetCalc,
        (settings, visible) => settings.ShowPresetCalc = visible);

    public static UtilityButtonDefinition Explorer { get; } = new(
        "Explorer", "\uF42F", UtilityIconColors.FolderAccess, "Main_ExplorerTooltip",
        settings => settings.ShowPresetExplorer,
        (settings, visible) => settings.ShowPresetExplorer = visible);

    public static UtilityButtonDefinition Downloads { get; } = new(
        "Downloads", "\uF151", UtilityIconColors.FolderAccess, "Main_DownloadsTooltip",
        settings => settings.ShowPresetDownloads,
        (settings, visible) => settings.ShowPresetDownloads = visible);

    public static UtilityButtonDefinition FileSorter { get; } = new(
        "FileSorter", "\uF18B", UtilityIconColors.FolderAccess, "Main_FileSorterTooltip",
        settings => settings.ShowPresetFileSorter,
        (settings, visible) => settings.ShowPresetFileSorter = visible);

    public static UtilityButtonDefinition IconConverter { get; } = new(
        "IconConverter", "\uE721", UtilityIconColors.AssetCreation, "Main_IconConverterTooltip",
        settings => settings.ShowPresetIconConverter,
        (settings, visible) => settings.ShowPresetIconConverter = visible);

    public static UtilityButtonDefinition TimerStopwatch { get; } = new(
        "TimerStopwatch", "\uED88", UtilityIconColors.Productivity, "Main_TimerStopwatchTooltip",
        settings => settings.ShowPresetTimerStopwatch,
        (settings, visible) => settings.ShowPresetTimerStopwatch = visible);

    public static UtilityButtonDefinition ColorPicker { get; } = new(
        "ColorPicker", "\uE5FE", UtilityIconColors.ScreenCapture, "Main_ColorPickerTooltip",
        settings => settings.ShowPresetColorPicker,
        (settings, visible) => settings.ShowPresetColorPicker = visible);

    public static UtilityButtonDefinition QuickNote { get; } = new(
        "QuickNote", "\uF56F", UtilityIconColors.TextWorkspace, "Main_QuickNoteTooltip",
        settings => settings.ShowPresetQuickNote,
        (settings, visible) => settings.ShowPresetQuickNote = visible);

    public static UtilityButtonDefinition QRCodeGenerator { get; } = new(
        "QRCodeGenerator", "\uF635", UtilityIconColors.AssetCreation, "Main_QRCodeGeneratorTooltip",
        settings => settings.ShowPresetQRCodeGenerator,
        (settings, visible) => settings.ShowPresetQRCodeGenerator = visible);

    public static UtilityButtonDefinition ClipboardManager { get; } = new(
        "ClipboardManager", "\uE34E", UtilityIconColors.TextWorkspace, "Main_ClipboardManagerTooltip",
        settings => settings.ShowPresetClipboardManager,
        (settings, visible) => settings.ShowPresetClipboardManager = visible);

    public static UtilityButtonDefinition ShowDesktop { get; } = new(
        "ShowDesktop", "\uE4AB", UtilityIconColors.FolderAccess, "Main_ShowDesktopTooltip",
        settings => settings.ShowPresetShowDesktop,
        (settings, visible) => settings.ShowPresetShowDesktop = visible);

    public static UtilityButtonDefinition AppsFolder { get; } = new(
        "AppsFolder", "\uF732", UtilityIconColors.FolderAccess, "Main_AppsFolderTooltip",
        settings => settings.ShowPresetAppsFolder,
        (settings, visible) => settings.ShowPresetAppsFolder = visible);

    public static UtilityButtonDefinition Copilot { get; } = new(
        "Copilot", "\uF1F9", UtilityIconColors.AiTools, "Main_CopilotTooltip",
        settings => settings.ShowPresetCopilot,
        (settings, visible) => settings.ShowPresetCopilot = visible);

    public static UtilityButtonDefinition TextProcessing { get; } = new(
        "TextProcessing", "\uF7DA", UtilityIconColors.TextWorkspace, "Main_TextProcessingTooltip",
        settings => settings.ShowPresetTextProcessing,
        (settings, visible) => settings.ShowPresetTextProcessing = visible);

    public static UtilityButtonDefinition PromptBuilder { get; } = new(
        "PromptBuilder", "\uF6A6", UtilityIconColors.AiTools, "Main_PromptBuilderTooltip",
        settings => settings.ShowPresetPromptBuilder,
        (settings, visible) => settings.ShowPresetPromptBuilder = visible);

    public static UtilityButtonDefinition ZenEditor { get; } = new(
        "ZenEditor", "\uF1EC", UtilityIconColors.TextWorkspace, "Main_ZenEditorTooltip",
        settings => settings.ShowPresetZenEditor,
        (settings, visible) => settings.ShowPresetZenEditor = visible);

    public static UtilityButtonDefinition AiteProfiles { get; } = new(
        "AiteProfiles", "\uF409", UtilityIconColors.SearchAndNavigation, "Main_AiteProfilesTooltip",
        settings => settings.ShowPresetAiteProfiles,
        (settings, visible) => settings.ShowPresetAiteProfiles = visible);

    public static IReadOnlyList<UtilityButtonDefinition> All { get; } =
    [
        Search,
        Screenshot,
        Record,
        Calculator,
        Explorer,
        Downloads,
        FileSorter,
        IconConverter,
        TimerStopwatch,
        ColorPicker,
        QuickNote,
        QRCodeGenerator,
        ClipboardManager,
        ShowDesktop,
        AppsFolder,
        Copilot,
        TextProcessing,
        PromptBuilder,
        ZenEditor,
        AiteProfiles
    ];

    private static readonly IReadOnlyDictionary<string, UtilityButtonDefinition> ById =
        All.ToDictionary(definition => definition.Id, StringComparer.Ordinal);

    public static bool TryGet(
        string id,
        [NotNullWhen(true)] out UtilityButtonDefinition? definition) =>
        ById.TryGetValue(id, out definition);

    public static int CountVisible(AppSettings settings) =>
        All.Count(definition => definition.IsVisible(settings));
}
