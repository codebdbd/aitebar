using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AiteBar;

public interface ISettingsWindowContext
{
    IReadOnlyList<PanelContext> GetContextsSnapshot();
    AppSettings GetAppSettings();
    Task<IReadOnlyList<string>> SaveElement(CustomElement updated, string? removeId = null);
}

public class PanelContext
{
    public string Id { get; set; } = "context-0";
    public string Name { get; set; } = string.Empty;
    public bool IsNameCustomized { get; set; }
    public string IconGlyph { get; set; } = "\uE8B7"; // Fluent "Folder" по умолчанию
    public bool IsEnabled { get; set; } = true;
    public string Color { get; set; } = "#2A9CFF";
}

public class HotkeyBinding
{
    public bool Ctrl { get; set; }
    public bool Alt { get; set; }
    public bool Shift { get; set; }
    public bool Win { get; set; }
    public string Key { get; set; } = "None";
}

public enum BrowserType
{
    Chrome,
    Edge,
    Brave,
    Yandex,
    Opera,
    OperaGX,
    Vivaldi,
    Firefox
}

public enum ActionType
{
    Web,
    Hotkey,
    Program,
    File,
    Folder,
    ScriptFile,
    Command
}

public enum FileSortLocationKind
{
    Downloads,
    Desktop,
    Custom
}

public sealed class FileSortOperationEntry
{
    public string SourcePath { get; init; } = string.Empty;
    public string DestinationPath { get; init; } = string.Empty;
}

public sealed class FileSortUndoState
{
    public string RootPath { get; init; } = string.Empty;
    public DateTime CompletedAtUtc { get; init; } = DateTime.UtcNow;
    public List<FileSortOperationEntry> Entries { get; init; } = [];
}

public sealed class FileSortResult
{
    public string RootPath { get; set; } = string.Empty;
    public int SortedCount { get; set; }
    public int SkippedCount { get; set; }
    public FileSortUndoState? UndoState { get; set; }
}

public sealed record FileSortProgress(string RootPath, int ProcessedFiles, int TotalFiles);

public sealed record MultiFileSortProgress(
    string RootPath,
    int FolderIndex,
    int FolderCount,
    int ProcessedFiles,
    int TotalFiles);

public sealed class FileSortUndoResult
{
    public int RestoredCount { get; set; }
    public int SkippedCount { get; set; }
    public FileSortUndoState? RemainingUndoState { get; set; }
}

public sealed class MultiFileSortUndoState
{
    public List<FileSortUndoState> PerFolder { get; set; } = [];
}

public sealed class MultiFileSortResult
{
    public List<FileSortResult> PerFolder { get; set; } = [];
    public int TotalSorted => PerFolder.Sum(x => x.SortedCount);
    public int TotalSkipped => PerFolder.Sum(x => x.SkippedCount);
    public MultiFileSortUndoState? CombinedUndoState { get; set; }
}

public sealed class MultiFileSortUndoResult
{
    public int TotalRestored { get; set; }
    public int TotalSkipped { get; set; }
    public MultiFileSortUndoState? RemainingUndoState { get; set; }
}

public sealed class MultiFileSortException : Exception
{
    public string FailedRootPath { get; }
    public MultiFileSortResult PartialResult { get; }

    public MultiFileSortException(string failedRootPath, MultiFileSortResult partialResult, Exception innerException)
        : base(innerException.Message, innerException)
    {
        FailedRootPath = failedRootPath;
        PartialResult = partialResult;
    }
}

public class CustomElement
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "";
    public string Icon { get; set; } = "\uF45B";
    public string IconFont { get; set; } = FontHelper.FluentKey;
    public string Color { get; set; } = "#E3E3E3";
    public string ActionType { get; set; } = nameof(AiteBar.ActionType.Web);
    public string ActionValue { get; set; } = "";
    public BrowserType Browser { get; set; } = BrowserType.Chrome;
    public string ChromeProfile { get; set; } = "";
    public List<string> RotationProfilePaths { get; set; } = [];

    public bool IsAppMode { get; set; } = false;
    public bool IsIncognito { get; set; } = false;
    public bool UseRotation { get; set; } = false;
    public bool OpenFullscreen { get; set; } = false;
    public bool IsTopmost { get; set; } = false;
    public string LastUsedProfile { get; set; } = "";

    public bool Alt { get; set; }
    public bool Ctrl { get; set; }
    public bool Shift { get; set; }
    public bool Win { get; set; }
    public string Key { get; set; } = "None";
    public string ImagePath { get; set; } = "";
    public string ContextId { get; set; } = "context-0";
}

public enum DockEdge { Top, Bottom, Left, Right }

public class AppSettings
{
    public bool GlobalHotkeyCtrl { get; set; } = false;
    public bool GlobalHotkeyAlt { get; set; } = true;
    public bool GlobalHotkeyShift { get; set; } = false;
    public bool GlobalHotkeyWin { get; set; } = false;
    public string GlobalHotkeyKey { get; set; } = "D4";

    public bool ShowPresetSearch { get; set; } = true;
    public bool ShowPresetScreenshot { get; set; } = true;
    public bool ShowPresetVideo { get; set; } = true;
    public bool ShowPresetCalc { get; set; } = true;
    public bool ShowPresetExplorer { get; set; } = true;
    public bool ShowPresetDownloads { get; set; } = true;
    public bool ShowPresetFileSorter { get; set; } = true;
    public bool ShowPresetIconConverter { get; set; } = true;
    public bool ShowPresetColorPicker { get; set; } = false;
    public bool ShowPresetQuickNote { get; set; } = false;
    public bool ShowPresetQRCodeGenerator { get; set; } = false;
        public bool ShowPresetClipboardManager { get; set; } = false;
        public bool ShowPresetTimerStopwatch { get; set; } = true;
        public bool ShowPresetShowDesktop { get; set; } = true;
        public bool ShowPresetAppsFolder { get; set; } = true;
        public bool ShowPresetCopilot { get; set; } = true;
        public bool ShowPresetTextProcessing { get; set; } = true;
        public bool ShowPresetPromptBuilder { get; set; } = false;
        public bool ShowPresetZenEditor { get; set; } = true;
        public bool ShowPresetAiteProfiles { get; set; } = true;
    public bool ClipboardManagerPersistHistory { get; set; } = true;
    public string QuickNoteThemeId { get; set; } = "dark";
    public bool QuickNotePinned { get; set; } = false;
    public double? QuickNoteLeft { get; set; }
    public double? QuickNoteTop { get; set; }
    public double? QuickNoteWidth { get; set; }
    public double? QuickNoteHeight { get; set; }
    public bool TimerSoundEnabled { get; set; } = true;
    public bool TimerIsStopwatchMode { get; set; } = false;
    public TimeSpan TimerDuration { get; set; } = TimeSpan.FromMinutes(5);

    public DockEdge Edge { get; set; } = DockEdge.Top;
    public int MonitorIndex { get; set; } = 0; // 0 = Primary, 1, 2...
    public double ActivationZoneSizePercent { get; set; } = 30; // % от ширины/высоты края
    public double PanelSizePercent { get; set; } = 80; // % от ширины/высоты экрана
    public int ActivationDelayMs { get; set; } = 150;
    public string UiCulture { get; set; } = "auto";
    public List<PanelContext> Contexts { get; set; } = [];
    public string ActiveContextId { get; set; } = "context-0";
    public HotkeyBinding NextContextHotkey { get; set; } = new();
    public HotkeyBinding PreviousContextHotkey { get; set; } = new();
    public HotkeyBinding AddButtonHotkey { get; set; } = new();
    public HotkeyBinding FileSorterHotkey { get; set; } = new();
    public HotkeyBinding IconConverterHotkey { get; set; } = new();
    public HotkeyBinding QuickNoteHotkey { get; set; } = new();
    public HotkeyBinding ColorPickerHotkey { get; set; } = new();
    public HotkeyBinding TimerStopwatchHotkey { get; set; } = new();
    public HotkeyBinding QRCodeGeneratorHotkey { get; set; } = new();
    public HotkeyBinding ClipboardManagerHotkey { get; set; } = new();
    public HotkeyBinding TextProcessingHotkey { get; set; } = new();
    public HotkeyBinding PromptBuilderHotkey { get; set; } = new();
    public HotkeyBinding ZenEditorHotkey { get; set; } = new();
    public FileSortUndoState? LastFileSortOperation { get; set; }
    public MultiFileSortUndoState? LastMultiFileSortOperation { get; set; }
    public List<string> SavedFileSortFolders { get; set; } = [];

    public double? TextProcessingLeft { get; set; }
    public double? TextProcessingTop { get; set; }
    public double? TextProcessingWidth { get; set; }
    public double? TextProcessingHeight { get; set; }
    public string? TextProcessingWindowState { get; set; }
    public int TextProcessingLastMode { get; set; }
    public string? TextProcessingSelectedConnectionId { get; set; }
    public string? TextProcessingSelectedModelId { get; set; }
    public string? TextProcessingSelectedProviderId { get; set; }
    public bool TextProcessingIsAutoModel { get; set; } = true;
    public string? TextProcessingLastText { get; set; }

    public double? PromptBuilderLeft { get; set; }
    public double? PromptBuilderTop { get; set; }
    public double? PromptBuilderWidth { get; set; }
    public double? PromptBuilderHeight { get; set; }
    public string? PromptBuilderWindowState { get; set; }
    public bool PromptBuilderWindowPlacementInitialized { get; set; }
    public int PromptBuilderLastMode { get; set; } = (int)PromptBuilderCategory.Programming;
    public PaintingStyle PromptBuilderPaintingStyle { get; set; } = PaintingStyle.Auto;
    public PaintingStyleSection PromptBuilderPaintingSection { get; set; } = PaintingStyleSection.All;
    public PaintingArtist PromptBuilderPaintingArtist { get; set; } = PaintingArtist.Auto;
    public AnimationStyle PromptBuilderAnimationStyle { get; set; } = AnimationStyle.Auto;
    public AnimationStyleSection PromptBuilderAnimationSection { get; set; } = AnimationStyleSection.All;
    public PhotoSection PromptBuilderPhotoSection { get; set; } = PhotoSection.All;
    public PhotoStyle PromptBuilderPhotoStyle { get; set; } = PhotoStyle.Auto;
    public ThemeSection PromptBuilderThemeSection { get; set; } = ThemeSection.All;
    public ThemeStyle PromptBuilderThemeStyle { get; set; } = ThemeStyle.Auto;
    public TextPromptType PromptBuilderTextType { get; set; } = TextPromptType.Auto;
    public TextPromptTone PromptBuilderTextTone { get; set; } = TextPromptTone.Neutral;
    public AnalysisDirection PromptBuilderAnalysisDirection { get; set; } = AnalysisDirection.Auto;
    public VideoDirection PromptBuilderVideoDirection { get; set; } = VideoDirection.Auto;
    public ProgrammingProjectType PromptBuilderProgrammingProjectType { get; set; } = ProgrammingProjectType.Auto;
    public ProgrammingPromptStyle PromptBuilderProgrammingStyle { get; set; } = ProgrammingPromptStyle.Auto;
    public VisualTargetModel PromptBuilderVisualTarget { get; set; } = VisualTargetModel.Universal;
    public IconStyle PromptBuilderIconStyle { get; set; } = IconStyle.Auto;
    public GraphicType PromptBuilderGraphicType { get; set; } = GraphicType.Auto;
    public GraphicStyle PromptBuilderGraphicStyle { get; set; } = GraphicStyle.Auto;
    public string? PromptBuilderSelectedConnectionId { get; set; }
    public string? PromptBuilderSelectedModelId { get; set; }
    public string? PromptBuilderSelectedProviderId { get; set; }
    public bool PromptBuilderIsAutoModel { get; set; } = true;
    public string? PromptBuilderLastText { get; set; }
    public Dictionary<string, PromptBuilderDraft> PromptBuilderDrafts { get; set; } = [];
    public bool SavePromptBuilderDrafts { get; set; } = true;

    public List<CustomElement> Elements { get; set; } = new();
    public List<string> UtilityButtonOrder { get; set; } = new();
    public bool CheckForUpdatesEnabled { get; set; } = true;
    public bool ShowPanelOnMouseHover { get; set; } = true;
    public bool? ShowTaskbarPositionIndicator { get; set; } = true;
    public double? TaskbarIndicatorPositionX { get; set; }
    public double? TaskbarIndicatorPositionY { get; set; }
    public AiSettings Ai { get; set; } = new();
    public SentrySettings? Sentry { get; set; }
}

public sealed class PromptBuilderDraft
{
    public string Input { get; set; } = string.Empty;
    public string Result { get; set; } = string.Empty;
    public bool HasResult { get; set; }
    public bool ShowOriginal { get; set; }
}

public class SentrySettings
{
    public string? Dsn { get; set; }
    public bool IsEnabled { get; set; } = false;
    public string? Environment { get; set; }
    public double TracesSampleRate { get; set; } = 0.0;
    public bool SendDefaultPii { get; set; } = false;
}
