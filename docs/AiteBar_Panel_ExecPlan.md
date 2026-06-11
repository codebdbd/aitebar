# Implement AiteBar Panel as a Two-Panel File Manager Utility

This ExecPlan is a living document. The sections `Progress`, `Surprises & Discoveries`, `Decision Log`, and `Outcomes & Retrospective` must be kept up to date as work proceeds.

This plan follows `PLANS.md` from the repository root. Keep this document self-contained whenever it is revised: a future contributor should be able to implement the feature by reading this file and the current working tree only.

## Purpose / Big Picture

AiteBar currently provides a hidden Windows edge panel with several focused utilities. This plan adds `AiteBar Panel`, a compact two-panel file manager utility for Windows 11 that launches from the existing AiteBar utility system and follows the contract in `docs/AiteBar_Panel_contract_clean.md`.

After this work, a user can open AiteBar Panel from the main AiteBar panel, see two file lists side by side, switch the active side with `Tab`, navigate folders with keyboard or mouse, select files, and run basic commands with `F3` through `F8`. File operations such as copy, move, delete, and rename must use Windows Shell behavior instead of a custom copy engine, so the utility behaves close to File Explorer.

The implementation must fit the current AiteBar architecture: register a utility through `UtilityRegistry`, use WPF windows and existing dark styling resources, keep non-UI logic in helper or service classes, localize user-facing strings through `Resources/Strings*.resx`, and add unit tests for logic that can run without UI.

## Progress

- [x] (2026-06-11 06:00Z) Read `docs/AiteBar_Panel_contract_clean.md`, `docs/UTILITIES.md`, `AiteBar/UtilityRegistry.cs`, `AiteBar/App.xaml.cs`, `AiteBar/FileSorterWindow.xaml`, and `AiteBar/FileSorterWindow.xaml.cs` to align this plan with the repository.
- [x] (2026-06-11 06:00Z) Created the initial implementation plan as `docs/AiteBar_Panel_ExecPlan.md`.
- [ ] Implement the minimal registered utility shell and main window.
- [ ] Implement file system browsing model and asynchronous directory loading.
- [ ] Implement keyboard and mouse navigation, active panel state, and selection behavior.
- [ ] Implement native Shell-backed file operations and confirmation dialogs.
- [ ] Implement native Explorer context menu integration.
- [ ] Implement drag-and-drop between panels and with File Explorer.
- [ ] Implement settings, persistence, localization, tests, and final validation.

## Surprises & Discoveries

- Observation: Existing utility documentation in `docs/UTILITIES.md` still shows an older `async void Launch` example, but the actual `IUtility` interface in `AiteBar/UtilityRegistry.cs` uses `Task LaunchAsync(...)`.
  Evidence: `UtilityRegistry.cs` defines `Task LaunchAsync(AppSettingsService settingsService, Window? owner, Func<Task>? onBeforeExecute = null);`.

- Observation: Existing utility windows use `DarkWindow`, local resources, border radius 4 to 8, dark panel backgrounds, and the accent color `#007ACC`. AiteBar Panel should reuse this style but needs a larger resizable window than current compact popups.
  Evidence: `FileSorterWindow.xaml` uses `local:DarkWindow`, `PanelBackground`, `AccentColor`, `CornerRadius="4"` for controls, and `CornerRadius="8"` for outer panels.

## Decision Log

- Decision: Implement AiteBar Panel as a normal registered utility, not as a separate executable in the first version.
  Rationale: The user asked for a utility that matches AiteBar's style and architecture. `UtilityRegistry` is the existing extension point for utilities, and registering there keeps launch behavior, localization, and panel integration consistent.
  Date/Author: 2026-06-11 / Codex

- Decision: Split file-manager logic into services and models instead of placing everything in `AiteBarPanelWindow.xaml.cs`.
  Rationale: The contract is large and includes testable logic such as formatting sizes, resolving active selections, filtering parent navigation rows, and drive info formatting. AiteBar guidance says pure calculation and normalization logic should live in helper classes when possible.
  Date/Author: 2026-06-11 / Codex

- Decision: Use Windows Shell APIs for destructive and transfer operations, with a thin adapter interface for tests.
  Rationale: The contract explicitly forbids a custom copy engine as the primary mechanism. A wrapper keeps the WPF layer independent from COM details and lets unit tests verify command routing without performing real file operations.
  Date/Author: 2026-06-11 / Codex

- Decision: Treat the first implementation milestone as a working MVP and defer full Explorer-quality conflict UI until after Shell operations and context menus are proven.
  Rationale: Windows Shell can already show native conflict and progress UI for many operations. A custom conflict dialog is useful only where Shell behavior cannot satisfy the contract cleanly, and should not block the first working version.
  Date/Author: 2026-06-11 / Codex

## Outcomes & Retrospective

No implementation has been completed yet. The expected outcome is a new AiteBar Panel utility that can be launched from AiteBar, supports the two-panel file manager contract, and passes Release build and unit tests.

## Context and Orientation

The repository root is `D:\01_Codebdbd\01_projects\aitebar`. The solution is `AiteBar.sln`. The main WPF application project is `AiteBar/AiteBar.csproj`, and tests live in `AiteBar.Tests/AiteBar.Tests.csproj`.

Utilities are registered in `AiteBar/App.xaml.cs` through `RegisterUtilities()`. Each utility implements `IUtility` from `AiteBar/UtilityRegistry.cs`. Existing utility examples include `QuickNoteUtility`, `TimerStopwatchUtility`, `ColorPickerUtility`, and `FileSorterUtility`.

The current UI style uses WPF, dark themed resources, and `DarkWindow`. Important resources are in `AiteBar/FormControlsResources.xaml`, `AiteBar/SettingsResources.xaml`, and `AiteBar/SettingsWindowResources.xaml`. User-facing text is localized through `LocalizationService` and resource files in `AiteBar/Resources/Strings.resx`, `Strings.ru.resx`, `Strings.uk.resx`, and `Strings.de.resx`.

`AiteBar Panel` means the new file manager utility, not the existing hidden edge panel `MainWindow`. `Active panel` means the left or right file pane that receives keyboard commands. `Current item` means the row with keyboard focus. `Selected items` means rows explicitly marked for file operations. `Native Shell operation` means calling Windows Shell facilities such as `IFileOperation` or Shell verbs so copy, move, delete, rename, conflicts, recycle bin, UAC prompts, and progress behave like File Explorer.

## Plan of Work

Create a new utility class `AiteBar/AiteBarPanelUtility.cs`. It should inherit from `UtilityBase<AiteBarPanelWindow>` if that base class fits the window lifecycle. Its `Id` should be `AiteBarPanel`, `DisplayNameKey` should be `Tool_AiteBarPanel`, `IconColor` should be the existing accent color `#007ACC`, and the glyph should come from the existing Fluent System Icons font. Register it in `App.RegisterUtilities()` after the existing utilities.

Create `AiteBar/AiteBarPanelWindow.xaml` and `AiteBar/AiteBarPanelWindow.xaml.cs`. The window title must be `AiteBar Panel`. Use `DarkWindow`, the existing app icon, dark theme resources, and Windows 11-style compact visuals. Unlike small tools such as File Sorter, this window should be resizable and visible in the taskbar because it is a full file manager. Start with a practical size such as 1100 by 680, a minimum size that keeps both panels usable, and no top menu or toolbar.

The XAML should contain a root dark shell, a compact custom header or standard window frame depending on existing `DarkWindow` behavior, a two-column content grid, and a bottom command bar. Each file panel should have a top strip with a drive `ComboBox`, current path text, and disk usage text in the format `Занято: 103,2 ГБ / 363,1 ГБ`. Below that, use a `DataGrid` or `ListView/GridView` with columns `Имя`, `Тип`, `Размер`, `Дата`, and `Атрибуты`. Add a panel-local selection status row at the bottom. Add the global bottom buttons exactly as text buttons: `F3 Просмотр`, `F4 Правка`, `F5 Копировать`, `F6 Переместить`, `F7 Каталог`, `F8 Удалить`, and `Alt+F4 Выход`.

Create model and service files to avoid a large code-behind. Suggested files are `AiteBar/AiteBarPanelModels.cs`, `AiteBar/AiteBarPanelFileSystemService.cs`, `AiteBar/AiteBarPanelSelectionHelper.cs`, `AiteBar/AiteBarPanelFormatHelper.cs`, `AiteBar/AiteBarPanelShellOperationService.cs`, and `AiteBar/AiteBarPanelContextMenuService.cs`. Keep names if implementation reveals better local naming, but preserve this separation: data models, file browsing, formatting, selection resolution, Shell operations, and Shell context menu integration should not be mixed into one class.

For browsing, define a row model with at least full path, display name, item kind, extension/type label, size, modified date, attributes, and a boolean for the synthetic parent row `..`. Directory loading must run asynchronously and must not compute folder sizes recursively. The service should enumerate directories and files for a path, sort folders before files, insert `..` when a parent exists, and return display rows. Handle access denied, unavailable drives, and long path errors by returning a clear error result that the window can show without crashing.

For drive selection, enumerate available drives with `DriveInfo.GetDrives()`. Include fixed, removable, network, and ready virtual drives when available. Show labels like `C: Локальный диск`, `D: Данные`, or a fallback using the drive root. When a drive is selected, navigate that panel to the drive root and update disk usage using gigabytes. If a drive is not ready, show a localized message and leave the previous path unchanged.

For active panel and selection, keep independent state for left and right panes. Clicking inside a pane makes it active. `Tab` toggles active pane. `Space` toggles the current item. `Insert` toggles the current item and moves the current row down. `Ctrl+A` selects all real items except `..`. `Esc` clears selection when no modal dialog is open. Operations use marked selected items; if none are marked, operations use the current real item. The parent row `..` must never be copied, moved, edited, deleted, or selected for file operations.

For navigation, implement `Enter`, `Backspace`, double-click, and `..`. `Enter` on a directory or `..` changes the current panel path. `Enter` on a file opens it with `Process.Start(new ProcessStartInfo(path) { UseShellExecute = true })`. `Backspace` navigates to the parent directory. Use cancellation tokens or a version counter so slow directory loads cannot overwrite newer navigation results.

For viewing and editing, implement the pragmatic first version. `F3` opens a read-only viewer window for text-like files if that can be built quickly; otherwise open the file with the system default and show a clear message for unsupported preview formats. `F4` opens the system editor by default. If a configurable editor is added in settings, route `F4` through that setting. Keep the view/edit logic in a small service so tests can cover text-file classification and command selection.

For native copy, move, delete, and rename, add a Shell operation adapter. The preferred route is `IFileOperation` from `shell32.dll` through COM interop. It should support copy items to the opposite panel path, move items to the opposite panel path, rename within the same parent when requested, and delete to recycle bin by default. If COM interop is too large for the first pass, use `Microsoft.VisualBasic.FileIO.FileSystem` only as a temporary compatibility fallback for recycle-bin delete and document the limitation in this plan before proceeding. Do not implement primary copy or move through manual `FileStream`.

Before copy, move, and delete, show compact dark confirmation dialogs consistent with `DarkDialog`, but with enough detail for source and target. For delete, the default is recycle bin. After each completed operation, refresh both panels. If Shell shows native progress and conflict UI, use it; otherwise add `AiteBarPanelOperationProgressWindow` with current file, overall progress, source, destination, processed count, speed when available, remaining time when available, and a cancel button.

For Windows context menus, implement `AiteBarPanelContextMenuService` using Shell interfaces such as `IShellFolder`, `IContextMenu`, `IContextMenu2`, and `IContextMenu3`, or a proven minimal interop implementation local to this project. It must support files, folders, multiple selected items, and empty panel space. Default right-click behavior should be file marking. `Shift+Right Click` and `Shift+F10` should show the context menu. Add a setting later to switch right click between marking and context menu.

For drag-and-drop, use WPF drag/drop APIs with `DataObject` file drop format and Shell operations. Dragging between panels should copy across drives and move within the same drive. `Ctrl` forces copy, `Shift` forces move. Support drops from File Explorer into a panel path and drags from AiteBar Panel to File Explorer. Confirm operations if the setting says so.

For settings, extend `AppSettingsService` and the settings model with only the contract-required minimal options: theme mode for AiteBar Panel if not already global, right mouse button mode, delete mode, drag-and-drop confirmation mode, and editor mode/path for `F4`. Surface settings in the existing tabbed `SettingsWindow` without removing the tab structure. Keep button parameters and order management logically separate, as required by the repository instructions.

For localization, add keys to all resource files. At minimum include the utility name, window title, column headers, command labels, confirmation dialog labels, status formats, drive usage format, error messages, settings labels, and empty folder text. Russian text should match the contract. English, Ukrainian, and German can be clear direct translations if exact product wording is not yet available.

For tests, add unit tests for formatting and selection logic. Suggested tests are `AiteBar.Tests/AiteBarPanelFormatHelperTests.cs`, `AiteBar.Tests/AiteBarPanelSelectionHelperTests.cs`, and `AiteBar.Tests/AiteBarPanelFileSystemServiceTests.cs` where practical. Test that size formatting returns `Б`, `КБ`, `МБ`, and `ГБ`; disk usage formats used and total gigabytes; `..` is excluded from operations; selected items take precedence over current item; and current item is used when no items are selected.

## Milestones

Milestone 1 creates a launchable utility shell. Add `AiteBarPanelUtility`, register it in `App.xaml.cs`, create a resizable `AiteBarPanelWindow`, add localized `Tool_AiteBarPanel`, and add an entry point from the existing utility system. The observable result is that AiteBar can launch an empty AiteBar Panel window titled `AiteBar Panel` with the correct dark style.

Milestone 2 adds browsing. Implement drive enumeration, path display, disk usage display, asynchronous directory loading, the `..` row, and the two side-by-side file tables. The observable result is that each pane can open a different drive or folder, large folders do not freeze the UI, and errors appear as readable messages instead of crashes.

Milestone 3 adds navigation and selection. Implement `Tab`, click activation, `Enter`, `Backspace`, double-click, `Space`, `Insert`, `Ctrl+A`, `Esc`, and per-pane status rows. The observable result is that the active panel is visually distinct, selection count/size updates, and commands resolve the expected items.

Milestone 4 adds basic commands. Implement `F3`, `F4`, `F5`, `F6`, `F7`, `F8`, and `Alt+F4`. Use native Shell operations for copy, move, delete, and rename. The observable result is that a user can create a test folder, copy files from left to right, move files back, open a file, edit a file through the system editor, and delete to recycle bin with confirmation.

Milestone 5 adds Shell integration. Implement Explorer context menu, right-click marking mode, `Shift+Right Click`, `Shift+F10`, multi-item context menus, empty-area context menus, and drag-and-drop. The observable result is that Explorer extensions and properties are available from AiteBar Panel, and files can be dragged between panels and File Explorer.

Milestone 6 finishes settings, polish, tests, and release validation. Add the minimal settings, localize all strings, finish progress/cancel UX if Shell UI is insufficient, add unit tests, run Release build and tests, and manually verify the file manager contract.

## Concrete Steps

Work from the repository root:

    D:\01_Codebdbd\01_projects\aitebar

Before editing, inspect the current worktree:

    git status --short

After each milestone, run:

    dotnet build .\AiteBar.sln -c Release
    dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release

If `dotnet test` fails due to WPF temporary generated file issues under `wpftmp`, `obj`, or `*.g.cs`, use the documented fallback:

    dotnet vstest .\AiteBar.Tests\bin\Release\net8.0-windows\AiteBar.Tests.dll

For manual verification, start the built application and launch AiteBar Panel from the AiteBar panel. If the executable path changes by configuration, use the Release output under `AiteBar\bin\Release\net8.0-windows\win-x64`.

Use a disposable test directory such as:

    %TEMP%\AiteBarPanelManualTest

Create subfolders and small text files there, then verify copy, move, rename, delete-to-recycle-bin, context menu, and drag-and-drop. Do not test destructive operations on important user folders.

## Validation and Acceptance

The implementation is acceptable when these behaviors can be observed on Windows 11:

The utility launches from AiteBar and the window title is `AiteBar Panel`. The main window always shows exactly two file panels and a bottom text command bar with `F3 Просмотр`, `F4 Правка`, `F5 Копировать`, `F6 Переместить`, `F7 Каталог`, `F8 Удалить`, and `Alt+F4 Выход`. There is no top menu, no top toolbar, no refresh button, and no command line like `C:\>`.

Each panel has a drive selector, current path, disk usage in gigabytes, file table columns `Имя`, `Тип`, `Размер`, `Дата`, and `Атрибуты`, a `..` parent row when applicable, and a selection status row. The left and right panels can show different folders at the same time.

Keyboard and mouse navigation work: `Tab` switches active panel, clicking a panel activates it, `Enter` opens folders and files, `Backspace` goes to parent, double-click opens folders and files, and selecting `..` goes up one level.

Selection works: `Space`, `Insert`, right-click marking mode, mouse selection, and `Ctrl+A` select real files and folders. `Esc` clears selection. Operations use selected items first and current item only when no items are selected.

Native operations work: `F5` copies from active panel to the opposite panel, `F6` moves to the opposite panel or renames when appropriate, `F7` creates a directory in the active panel, and `F8` deletes to the Windows recycle bin by default. Confirmations appear before dangerous actions. The UI stays responsive during operations and both panels refresh after operations.

Explorer integration works: `Shift+Right Click` and `Shift+F10` open a native Windows context menu for files, folders, multiple selected items, and empty panel space. Drag-and-drop works between panels and between AiteBar Panel and File Explorer.

Automated validation passes:

    dotnet build .\AiteBar.sln -c Release
    dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release

## Idempotence and Recovery

All implementation steps should be additive until a milestone is verified. Creating new files for AiteBar Panel is safe to repeat if the files are updated in place. Resource keys must be unique and should not replace unrelated strings.

Manual file operation tests must use disposable folders under `%TEMP%`. If a Shell operation test leaves files behind, delete only that disposable directory after verifying its absolute path. Do not run delete or move tests against repository files or user profile folders.

If Shell COM interop causes crashes, isolate it behind the adapter interface and temporarily disable only the failing operation while keeping browsing and selection functional. Record the failure and evidence in `Surprises & Discoveries`, then continue with a smaller proof of concept for that Shell API before wiring it back into the UI.

If the WPF window becomes too large or visually noisy, reduce controls rather than adding menus. The contract requires a minimal two-panel interface, not a full Total Commander clone.

## Artifacts and Notes

Relevant existing files:

    AiteBar/UtilityRegistry.cs
    AiteBar/App.xaml.cs
    AiteBar/FileSorterUtility.cs
    AiteBar/FileSorterWindow.xaml
    AiteBar/FileSorterWindow.xaml.cs
    AiteBar/DarkWindow.cs
    AiteBar/DarkDialog.xaml
    AiteBar/AppSettingsService.cs
    AiteBar/SettingsWindow.xaml
    AiteBar/SettingsWindow.xaml.cs
    AiteBar/Resources/Strings.resx
    AiteBar/Resources/Strings.ru.resx
    AiteBar/Resources/Strings.uk.resx
    AiteBar/Resources/Strings.de.resx
    AiteBar.Tests/AiteBar.Tests.csproj

Suggested new files:

    AiteBar/AiteBarPanelUtility.cs
    AiteBar/AiteBarPanelWindow.xaml
    AiteBar/AiteBarPanelWindow.xaml.cs
    AiteBar/AiteBarPanelModels.cs
    AiteBar/AiteBarPanelFileSystemService.cs
    AiteBar/AiteBarPanelSelectionHelper.cs
    AiteBar/AiteBarPanelFormatHelper.cs
    AiteBar/AiteBarPanelShellOperationService.cs
    AiteBar/AiteBarPanelContextMenuService.cs
    AiteBar/AiteBarPanelOperationProgressWindow.xaml
    AiteBar/AiteBarPanelOperationProgressWindow.xaml.cs
    AiteBar.Tests/AiteBarPanelFormatHelperTests.cs
    AiteBar.Tests/AiteBarPanelSelectionHelperTests.cs
    AiteBar.Tests/AiteBarPanelFileSystemServiceTests.cs

## Interfaces and Dependencies

Define a panel side enum:

    public enum AiteBarPanelSide
    {
        Left,
        Right
    }

Define a file item kind enum:

    public enum AiteBarPanelItemKind
    {
        ParentDirectory,
        Directory,
        File
    }

Define a row model:

    public sealed class AiteBarPanelItem
    {
        public required string DisplayName { get; init; }
        public required string FullPath { get; init; }
        public required AiteBarPanelItemKind Kind { get; init; }
        public string TypeLabel { get; init; } = string.Empty;
        public long? SizeBytes { get; init; }
        public DateTime? ModifiedAt { get; init; }
        public string AttributesLabel { get; init; } = string.Empty;
        public bool IsMarked { get; set; }
    }

Define a file system service:

    public sealed class AiteBarPanelFileSystemService
    {
        public IReadOnlyList<AiteBarPanelDriveInfo> GetDrives();
        public Task<AiteBarPanelDirectoryLoadResult> LoadDirectoryAsync(string path, CancellationToken cancellationToken);
    }

Define a selection helper:

    public static class AiteBarPanelSelectionHelper
    {
        public static IReadOnlyList<AiteBarPanelItem> ResolveOperationItems(
            IEnumerable<AiteBarPanelItem> visibleItems,
            AiteBarPanelItem? currentItem);
    }

Define a Shell operation abstraction:

    public interface IAiteBarPanelShellOperationService
    {
        Task CopyAsync(IReadOnlyList<string> sourcePaths, string destinationDirectory, Window owner, CancellationToken cancellationToken);
        Task MoveAsync(IReadOnlyList<string> sourcePaths, string destinationDirectory, Window owner, CancellationToken cancellationToken);
        Task RenameAsync(string sourcePath, string newName, Window owner, CancellationToken cancellationToken);
        Task DeleteAsync(IReadOnlyList<string> sourcePaths, bool recycle, Window owner, CancellationToken cancellationToken);
    }

Define a context menu abstraction:

    public interface IAiteBarPanelContextMenuService
    {
        void ShowContextMenu(IReadOnlyList<string> selectedPaths, string? folderPathForEmptyArea, Window owner, Point screenPoint);
    }

At completion, the WPF window should depend on these interfaces and helpers. It should coordinate UI state and commands, but it should not contain low-level Shell COM code, path formatting algorithms, or selection resolution rules.

## Revision Notes

2026-06-11 / Codex: Initial plan created after reading the AiteBar Panel contract and existing utility architecture. The plan chooses a registered WPF utility with services for browsing, selection, formatting, Shell operations, and context menus to match AiteBar's current style and keep the large feature testable.
