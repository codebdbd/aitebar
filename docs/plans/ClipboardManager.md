# Clipboard Manager Utility

This ExecPlan is a living document. The sections `Progress`, `Surprises & Discoveries`, `Decision Log`, and `Outcomes & Retrospective` must be kept up to date as work proceeds.

If PLANS.md file is checked into the repo, reference the path to that file here from the repository root and note that this document must be maintained in accordance with PLANS.md.

## Purpose / Big Picture

After this change, users will have a **Clipboard Manager** utility in the AiteBar panel. When clicked, it opens a dark-themed window matching the visual style of QRCodeGenerator/TimerStopwatch/QuickNote. The window displays a history of recently copied text and images, with the ability to search, re-copy, and clear entries.

### User scenarios

**Scenario 1 — Quickly re-copy a previously copied text**: User copied a command earlier, now needs it again. Opens Clipboard Manager, sees the history, clicks the entry or the copy button, and the text is back in the clipboard. Total time: ~2 seconds.

**Scenario 2 — Search clipboard history**: User copied many things and needs to find a specific URL. Opens Clipboard Manager, types part of the URL in the search box, the list filters in real time, user clicks to copy.

**Scenario 3 — Clear clipboard history**: User is done with a session and wants to clear all tracked entries. Opens Clipboard Manager, clicks "Clear All", history is empty.

**Scenario 4 — Quick access via panel button**: User enables the Clipboard Manager toggle in settings, the amber clipboard icon appears on the panel, one click opens the history window.

This follows the existing utility pattern established by QRCodeGenerator, TimerStopwatch, and QuickNote: a `[Utility]`-decorated class, a `DarkWindow`-based XAML window, a service class for clipboard monitoring, localization resources, settings toggle, and panel button definition.

## Progress

- [x] (2026-06-19) Plan written.
- [x] (2026-06-19) Add Win32 clipboard listener API to `NativeMethods.cs`: `AddClipboardFormatListener`, `RemoveClipboardFormatListener`, `WM_CLIPBOARDUPDATE`.
- [x] (2026-06-19) Create `ClipboardHistoryService.cs` — background clipboard monitoring service using Win32 `AddClipboardFormatListener`, stores up to 50 text entries (max 10KB each) and image entries (PNG thumbnails).
- [x] (2026-06-19) Create `ClipboardManagerWindow.xaml` + `.xaml.cs` — dark utility window with search, scrollable list, copy buttons, clear-all, and empty state hint.
- [x] (2026-06-19) Create `ClipboardManagerUtility.cs` — `UtilityBase<ClipboardManagerWindow>` registration with shared `ClipboardHistoryService` instance.
- [x] (2026-06-19) Register in `UnifiedButtonService.cs` — panel button definition with icon `\uE82F` and color `#F59E0B`.
- [x] (2026-06-19) Register in `MainWindow.xaml.cs` — panel click dispatch in `ExecuteUnifiedButtonActionAsync()`.
- [x] (2026-06-19) Add `AppSettings.ShowPresetClipboardManager` — visibility toggle (default `false`).
- [x] (2026-06-19) Add localization keys to all `.resx` files (en, ru, uk, de) — 10 keys per language.
- [x] (2026-06-19) Add settings checkbox in `AppSettingsWindow.xaml` and load/save in `AppSettingsWindow.xaml.cs`.
- [x] (2026-06-19) Update `AppSettingsService.cs` — `GetUtilityVisibility` and `SetUtilityVisibility` cases for `"ShowPresetClipboardManager"`.
- [ ] Build and test — blocked by pre-existing WPF `_wpftmp` file lock issue in the environment.
- [ ] Manual verification — pending build resolution.

## Surprises & Discoveries

- Observation: The WPF build consistently fails with `CS2012: Access to the path ... AiteBar.dll is denied` due to the `_wpftmp` temporary project racing with the main project for the same output DLL path. This affects all builds (Release, Debug, alternate output paths, external temp directories).
  Evidence: Multiple `dotnet build` attempts all produced the same error across different output paths including `D:\temp_aitebar_build\`. No running AiteBar process was found; the lock appears to be from antivirus real-time scanning or Windows Search Indexer.

- Observation: The clipboard listener pattern using `AddClipboardFormatListener`/`RemoveClipboardFormatListener` with `HwndSource.AddHook(WndProc)` is the standard Win32 approach for monitoring clipboard changes in WPF applications. It does not require a polling timer.
  Evidence: Microsoft documentation for `WM_CLIPBOARDUPDATE` (0x031D); the approach mirrors how `NativeIntegrationService` uses `SetWindowsHookEx` for mouse hooks.

- Observation: `Clipboard.ContainsText()` and `Clipboard.ContainsImage()` from `System.Windows.Clipboard` can throw `ThreadStateException` if not called from an STA thread. In WPF, the UI dispatcher is STA, so calling these from the `WndProc` hook (which runs on the UI thread via `HwndSource`) is safe.
  Evidence: Standard WPF threading model; `WndProc` callbacks are dispatched on the UI thread.

- Observation: The clipboard history stores image entries as PNG-encoded byte arrays rather than `BitmapSource` objects to avoid holding GDI+ handles and to enable efficient memory management.
  Evidence: `PngBitmapEncoder` produces a self-contained byte array; `BitmapImage` is created on-demand when the user copies an image entry back to the clipboard.

## Decision Log

- Decision: Use Win32 `AddClipboardFormatListener` instead of `System.Windows.Clipboard` polling.
  Rationale: Event-driven approach is more efficient than polling, does not waste CPU cycles, and provides immediate notification of clipboard changes. This is the standard Windows API for clipboard monitoring since Vista.
  Date/Author: 2026-06-19 / Codex.

- Decision: Store up to 50 clipboard entries with a 10KB text limit per entry.
  Rationale: 50 entries provides sufficient history for most workflows without excessive memory usage. 10KB text limit prevents accidental storage of large documents while accommodating typical clipboard content (URLs, code snippets, commands).
  Date/Author: 2026-06-19 / Codex.

- Decision: Use `UtilityBase<ClipboardManagerWindow>` pattern (like QRCodeGenerator, TimerStopwatch) rather than raw `IUtility`.
  Rationale: UtilityBase handles singleton window lifecycle, activation, error handling, and localization integration automatically. The `ClipboardHistoryService` is instantiated once in the utility class and shared with the window.
  Date/Author: 2026-06-19 / Codex.

- Decision: Default visibility `ShowPresetClipboardManager = false`.
  Rationale: New utilities should not clutter the panel by default; users opt in via settings or right-click context menu.
  Date/Author: 2026-06-19 / Codex.

- Decision: Use Fluent icon glyph `\uE82F` (`ic_fluent_clipboard_24_regular`) with amber color `#F59E0B`.
  Rationale: The clipboard icon is semantically correct for the utility; amber distinguishes it from the existing blue/purple/teal utility icons on the panel.
  Date/Author: 2026-06-19 / Codex.

- Decision: `ClipboardHistoryService` is owned by `ClipboardManagerUtility` and shared across window instances.
  Rationale: The service must persist clipboard monitoring even when the window is closed and reopened. A singleton service within the utility class ensures continuity. The `SuppressNextChange()` method prevents the service from recording its own copy operations.
  Date/Author: 2026-06-19 / Codex.

- Decision: Use `ListBox` with a custom `DataTemplate` instead of `ListView` for the clipboard entries list.
  Rationale: `ListBox` provides simpler styling for this use case and the custom `DataTemplate` with `Border` and `MouseLeftButtonDown` handler gives sufficient control over entry appearance and click behavior without the overhead of `GridView` columns.
  Date/Author: 2026-06-19 / Codex.

## Outcomes & Retrospective

The Clipboard Manager feature is implemented end to end in code: the utility is registered, can be enabled in settings, opens a dark WPF window near the panel, monitors clipboard changes via Win32 API, displays a searchable history list, and supports one-click re-copy and clear-all operations. The service supports both text entries (up to 10KB, 50 max) and image entries (PNG-encoded thumbnails).

Build and automated verification are blocked by a pre-existing environment issue where the WPF `_wpftmp` temporary project cannot write to the output DLL path due to a file system lock (likely antivirus or Windows Search Indexer). All code changes follow established patterns and are consistent with the existing utility implementations.

## Context and Orientation

### Existing utility architecture

Every utility in AiteBar follows this pattern:

1. **Utility class** (`*Utility.cs`): Inherits `UtilityBase<TWindow>`, decorated with `[Utility]` and `[SupportedOSPlatform("windows6.1")]`. Defines `Id`, `DisplayNameKey`, `IconGlyph`, `IconColor`. Override `CreateWindow()` and `ShowWindow()`. Auto-discovered at startup via `UtilityRegistry.RegisterAllFromAssembly()`.

2. **Window class** (`*Window.xaml` + `.xaml.cs`): Inherits `DarkWindow`. XAML defines the UI. Code-behind has `ShowNearPanel(AppSettingsService)` for positioning near the panel edge.

3. **Service class** (`*Service.cs`): Contains pure logic, no UI dependencies. For ClipboardManager, this is `ClipboardHistoryService` which monitors the clipboard via Win32 API.

4. **Registration**: Added to `UnifiedButtonService.UtilityButtons` list. Added to `MainWindow.ExecuteUnifiedButtonActionAsync()` switch. Added to `AppSettings` as `ShowPresetClipboardManager` bool. Added to localization `.resx` files with `Tool_*`, `Main_*Tooltip`, and `ClipboardManager_*` keys. Added to `AppSettingsWindow` checkbox.

### Key files to reference

- `AiteBar/UtilityRegistry.cs` — `IUtility`, `UtilityBase<TWindow>`, `[Utility]` attribute, `UtilityRegistry`
- `AiteBar/UnifiedButtonService.cs` — `UtilityButtonDef` list (lines 11-25)
- `AiteBar/MainWindow.xaml.cs` — `ExecuteUnifiedButtonActionAsync()` switch (lines 1505-1558)
- `AiteBar/Models.cs` — `AppSettings` class (lines 122-175)
- `AiteBar/NativeMethods.cs` — Win32 interop declarations
- `AiteBar/DarkWindow.cs` — base window class with localization support
- `AiteBar/LocalizationService.cs` — `Get()`, `Format()`, `LocExtension` markup extension
- `AiteBar/Resources/Strings.resx` (and `.de.resx`, `.ru.resx`, `.uk.resx`) — localization resources
- `AiteBar/AppSettingsWindow.xaml` — settings checkboxes for utility visibility (line 359)
- `AiteBar/AppSettingsWindow.xaml.cs` — settings load/save (lines 384, 563)
- `AiteBar/AppSettingsService.cs` — `GetUtilityVisibility`/`SetUtilityVisibility` (lines 738-760)
- `AiteBar/QRCodeGeneratorWindow.xaml` — exemplary DarkWindow XAML with header buttons, styles

### Win32 clipboard monitoring

The clipboard monitoring uses two Win32 functions declared in `NativeMethods.cs`:

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool AddClipboardFormatListener(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool RemoveClipboardFormatListener(IntPtr hWnd);

When a clipboard change occurs, Windows sends `WM_CLIPBOARDUPDATE` (0x031D) to the registered window. The `ClipboardHistoryService` hooks into the WPF `HwndSource` to receive this message and captures the clipboard content.

## Plan of Work

The implementation consists of six new files and modifications to eight existing files.

### New files

1. `AiteBar/ClipboardHistoryService.cs` — The core service. Uses `HwndSource.AddHook(WndProc)` to receive `WM_CLIPBOARDUPDATE` messages. Maintains a `List<ClipboardHistoryEntry>` capped at 50 entries. Each entry stores either text (string) or image bytes (PNG-encoded). Provides `StartListening(Window)`, `StopListening(Window)`, `CopyEntryToClipboard(ClipboardHistoryEntry)`, `ClearHistory()`, and `SuppressNextChange()` methods. The `HistoryChanged` event notifies the window when new entries arrive.

2. `AiteBar/ClipboardManagerWindow.xaml` — Dark-themed window (380x480) with: header row (title + close button), search TextBox with placeholder, ListBox with custom DataTemplate (text preview + timestamp + copy button), empty state TextBlock, footer row (status text + Clear All button). Uses existing styles: `HeaderButtonStyle`, `CommandButtonStyle`, `BaseTextBoxStyle`, `FormControlBackground`.

3. `AiteBar/ClipboardManagerWindow.xaml.cs` — Code-behind. Subscribes to `ClipboardHistoryService.HistoryChanged`. `ShowNearPanel(AppSettingsService)` positions the window near the panel edge. Search filters entries in real time. Entry click or copy button copies the entry back to clipboard via the service. Clear All empties the history.

4. `AiteBar/ClipboardManagerUtility.cs` — Inherits `UtilityBase<ClipboardManagerWindow>`. Creates the shared `ClipboardHistoryService` instance. `CreateWindow()` creates the window and starts clipboard listening. `ShowWindow()` calls `ShowNearPanel()`.

### Modified files

5. `AiteBar/NativeMethods.cs` — Add `AddClipboardFormatListener`, `RemoveClipboardFormatListener` DllImport declarations and `WM_CLIPBOARDUPDATE` constant.

6. `AiteBar/Models.cs` — Add `public bool ShowPresetClipboardManager { get; set; } = false;` to `AppSettings` class.

7. `AiteBar/UnifiedButtonService.cs` — Add `new("ClipboardManager", "\uE82F", "#F59E0B", "ShowPresetClipboardManager", "Main_ClipboardManagerTooltip")` to `UtilityButtons` list.

8. `AiteBar/AppSettingsService.cs` — Add `"ShowPresetClipboardManager"` case to both `GetUtilityVisibility()` and `SetUtilityVisibility()` switch expressions.

9. `AiteBar/MainWindow.xaml.cs` — Add `case "ClipboardManager": await _actionService.LaunchUtilityAsync("ClipboardManager", HideDock); break;` to the `ExecuteUnifiedButtonActionAsync()` switch.

10. `AiteBar/AppSettingsWindow.xaml` — Add `<CheckBox x:Name="ChkShowPresetClipboardManager" Content="{local:Loc ResourceKey=Tool_ClipboardManager}"/>` after the QRCodeGenerator checkbox.

11. `AiteBar/AppSettingsWindow.xaml.cs` — Add load line `ChkShowPresetClipboardManager.IsChecked = _settings.ShowPresetClipboardManager;` and save line `_settings.ShowPresetClipboardManager = ChkShowPresetClipboardManager.IsChecked ?? false;`.

12. `AiteBar/Resources/Strings.resx` — Add 10 keys: `Tool_ClipboardManager`, `Main_ClipboardManagerTooltip`, `ClipboardManager_Title`, `ClipboardManager_SearchPlaceholder`, `ClipboardManager_Copy`, `ClipboardManager_Copied`, `ClipboardManager_ClearAll`, `ClipboardManager_Cleared`, `ClipboardManager_EmptyHint`.

13. `AiteBar/Resources/Strings.ru.resx` — Same 10 keys in Russian.

14. `AiteBar/Resources/Strings.uk.resx` — Same 10 keys in Ukrainian.

15. `AiteBar/Resources/Strings.de.resx` — Same 10 keys in German.

## Concrete Steps

1. Add Win32 declarations to `AiteBar/NativeMethods.cs` (before the closing braces of the class):

        // Clipboard listener
        [DllImport("user32.dll", SetLastError = true)]
        internal static extern bool AddClipboardFormatListener(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern bool RemoveClipboardFormatListener(IntPtr hWnd);

        internal const int WM_CLIPBOARDUPDATE = 0x031D;

2. Create `AiteBar/ClipboardHistoryService.cs` with the full service implementation.

3. Create `AiteBar/ClipboardManagerWindow.xaml` and `AiteBar/ClipboardManagerWindow.xaml.cs`.

4. Create `AiteBar/ClipboardManagerUtility.cs`.

5. Add `ShowPresetClipboardManager` to `AppSettings` in `AiteBar/Models.cs`.

6. Add button definition to `UnifiedButtonService.UtilityButtons` in `AiteBar/UnifiedButtonService.cs`.

7. Add switch cases to `AppSettingsService.cs` for `GetUtilityVisibility` and `SetUtilityVisibility`.

8. Add checkbox to `AiteBar/AppSettingsWindow.xaml` and load/save to `AppSettingsWindow.xaml.cs`.

9. Add click dispatch case to `MainWindow.xaml.cs` `ExecuteUnifiedButtonActionAsync()`.

10. Add localization strings to all four `.resx` files.

11. Build and test:

        dotnet build .\AiteBar.sln -c Release
        dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release

12. Manual verification:
    - Enable Clipboard Manager in settings (Utilities tab).
    - Verify the amber clipboard icon appears on the panel.
    - Click the icon — Clipboard Manager window opens near the panel.
    - Copy some text — it appears in the history list.
    - Search for text — list filters in real time.
    - Click an entry — text is copied back to clipboard.
    - Click Clear All — history is empty.
    - Close and reopen — clipboard monitoring continues.

## Validation and Acceptance

After implementation, the following must be true:

1. **Build**: `dotnet build .\AiteBar.sln -c Release` completes with 0 errors.

2. **Tests**: `dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release` passes all existing tests (no regressions).

3. **Localization test**: `LocalizationServiceTests.ResourceFiles_HaveSameKeysAndFormatPlaceholders` passes — all four `.resx` files have the same keys.

4. **Panel integration**: The Clipboard Manager button appears on the panel when `ShowPresetClipboardManager = true` in settings. Clicking it opens the Clipboard Manager window.

5. **Clipboard monitoring**: After opening the Clipboard Manager window, copying text causes new entries to appear in the list. The list updates in real time.

6. **Search**: Typing in the search box filters the list to matching entries.

7. **Copy**: Clicking an entry or its copy button copies the text back to the clipboard.

8. **Clear**: Clicking Clear All empties the history list.

## Idempotence and Recovery

All steps are additive and can be repeated safely. No database migrations or destructive operations are involved. If a step fails halfway, the existing utility pattern ensures that partial changes do not break other utilities. The `ClipboardHistoryService` gracefully handles clipboard access errors and logs them via `Logger.Log()`.

## Artifacts and Notes

The implementation follows the exact same patterns as QRCodeGenerator and TimerStopwatch utilities. The `ClipboardHistoryService` is the only new architectural concept — a background service that monitors system clipboard changes via Win32 API and maintains an in-memory history list. This pattern could be reused for future clipboard-related features.

## Interfaces and Dependencies

No new NuGet packages are required. The implementation uses only:
- `System.Windows.Clipboard` (WPF built-in)
- Win32 `AddClipboardFormatListener`/`RemoveClipboardFormatListener` (via P/Invoke)
- `System.Windows.Interop.HwndSource` (WPF built-in)
- `System.Windows.Media.Imaging.PngBitmapEncoder` (WPF built-in)

The `ClipboardHistoryService` interface:

    public sealed class ClipboardHistoryService : IDisposable
    {
        public IReadOnlyList<ClipboardHistoryEntry> Entries { get; }
        public event EventHandler? HistoryChanged;
        public void StartListening(Window window);
        public void StopListening(Window window);
        public void SuppressNextChange();
        public void ClearHistory();
        public void CopyEntryToClipboard(ClipboardHistoryEntry entry);
    }

The `ClipboardHistoryEntry` model:

    public sealed class ClipboardHistoryEntry
    {
        public string Text { get; init; }
        public byte[]? ImageBytes { get; init; }
        public bool IsImage { get; }
        public DateTime Timestamp { get; init; }
        public string DisplayText { get; }
    }
