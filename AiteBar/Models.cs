using System;

namespace AiteBar;

public class PanelContext
{
    public string Id { get; set; } = "context-1";
    public string Name { get; set; } = LocalizationService.Format("Panel_DefaultNameFormat", 1);
    public string IconGlyph { get; set; } = "\uE8B7"; // Fluent "Folder" по умолчанию
    public bool IsEnabled { get; set; } = true;
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
    public string SourcePath { get; set; } = string.Empty;
    public string DestinationPath { get; set; } = string.Empty;
}

public sealed class FileSortUndoState
{
    public string RootPath { get; set; } = string.Empty;
    public DateTime CompletedAtUtc { get; set; } = DateTime.UtcNow;
    public List<FileSortOperationEntry> Entries { get; set; } = [];
}

public sealed class FileSortResult
{
    public string RootPath { get; set; } = string.Empty;
    public int SortedCount { get; set; }
    public int SkippedCount { get; set; }
    public FileSortUndoState? UndoState { get; set; }
}

public sealed class FileSortUndoResult
{
    public int RestoredCount { get; set; }
    public int SkippedCount { get; set; }
    public FileSortUndoState? RemainingUndoState { get; set; }
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
    public string ContextId { get; set; } = "context-1";
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
    public bool ShowPresetColorPicker { get; set; } = false;
    public bool ShowPresetQuickNote { get; set; } = false;
    public bool ShowPresetTimerStopwatch { get; set; } = true;
    public string QuickNoteThemeId { get; set; } = "dark";
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
    public string ActiveContextId { get; set; } = "context-1";
    public HotkeyBinding NextContextHotkey { get; set; } = new();
    public HotkeyBinding PreviousContextHotkey { get; set; } = new();
    public HotkeyBinding AddButtonHotkey { get; set; } = new();
    public HotkeyBinding FileSorterHotkey { get; set; } = new();
    public HotkeyBinding QuickNoteHotkey { get; set; } = new();
    public HotkeyBinding ColorPickerHotkey { get; set; } = new();
    public HotkeyBinding TimerStopwatchHotkey { get; set; } = new();
    public FileSortUndoState? LastFileSortOperation { get; set; }

    public List<CustomElement> Elements { get; set; } = new();
    public bool CheckForUpdatesEnabled { get; set; } = true;
    public SentrySettings? Sentry { get; set; }
}

public class SentrySettings
{
    public string? Dsn { get; set; }
    public bool IsEnabled { get; set; } = false;
    public string? Environment { get; set; }
    public double TracesSampleRate { get; set; } = 0.0;
    public bool SendDefaultPii { get; set; } = false;
}
