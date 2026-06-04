# File Sorter Utility For The Quick Access Panel

This ExecPlan is a living document. The sections `Progress`, `Surprises & Discoveries`, `Decision Log`, and `Outcomes & Retrospective` must be kept up to date as work proceeds.

This document must be maintained in accordance with [PLANS.md](../PLANS.md).

## Purpose / Big Picture

The goal is to add a new built-in quick utility to `AiteBar`: a compact file sorter opened from the panel itself. After this work, a user must be able to click one new panel button, pick `Рабочий стол`, `Загрузки`, or a custom folder, press `Сортировать`, and have only the top-level files from that location automatically moved into category folders such as `Изображения`, `Документы`, `Видео`, and `Прочее`.

The user experience must stay as simple as the approved reference. This is not a file manager and not a settings-heavy organizer. The popup must be one small owned window with three internal states: selection, progress, and result. The completed state must show how many files were sorted and give two actions: `Открыть папку` and `Откатить`. The feature is only complete when it is visible in the installed program, so the plan includes the installer rebuild.

## Progress

- [x] (2026-06-04 11:15Z) Created the first saved ExecPlan file in `docs/`.
- [x] (2026-06-04 11:30Z) Rewrote the plan to remove ambiguity and make it explicitly tied to the real `AiteBar` architecture and the exact UI requirements.
- [ ] Add new file-sorter settings and persisted undo state in `AiteBar/Models.cs`.
- [ ] Add the new hotkey command and panel integration points.
- [ ] Implement the non-UI sorting service with all skip rules and safe rename behavior.
- [ ] Implement the popup window as a single three-state compact tool window.
- [ ] Localize all new UI strings in every existing resource file.
- [ ] Add automated tests for sorting, undo, hotkeys, and window reuse.
- [ ] Build, test, manually verify the popup flow, and rebuild the installer.

## Surprises & Discoveries

- Observation: The current committed repository already has the visual and behavioral pattern for compact owned tools in `QuickNoteWindow` and `TimerStopwatchWindow`, but it does not contain a committed `FileSorterWindow`.
  Evidence: `AiteBar/QuickNoteWindow.xaml(.cs)` and `AiteBar/TimerStopwatchWindow.xaml(.cs)` exist; `AiteBar/FileSorterWindow.xaml` does not.

- Observation: System quick tools are not configured in one central registry. Their integration is spread across several files.
  Evidence: the button exists in `AiteBar/MainWindow.xaml`, visibility and click logic in `AiteBar/MainWindow.xaml.cs`, hotkey mapping in `AiteBar/HotkeyService.cs`, settings persistence in `AiteBar/Models.cs`, and settings UI in `AiteBar/AppSettingsWindow.xaml(.cs)`.

- Observation: Localization files live under `AiteBar/Resources`, not in the root of the project.
  Evidence: `AiteBar/Resources/Strings.resx`, `Strings.ru.resx`, `Strings.uk.resx`, and `Strings.de.resx`.

## Decision Log

- Decision: The utility will use one popup window with internal state switching rather than separate windows or dialogs.
  Rationale: the reference clearly shows one compact panel-styled popup. One window also keeps activation, ownership, and panel positioning predictable.
  Date/Author: 2026-06-04 / Codex

- Decision: The file-moving logic will live in a dedicated service file separate from the WPF window.
  Rationale: sorting rules, conflict-safe renaming, and undo are easier to test and reason about outside UI code.
  Date/Author: 2026-06-04 / Codex

- Decision: Only the last successful sort operation will be stored for undo.
  Rationale: the specification asks for `Откатить`, but the UI must stay extremely simple. Multi-operation history would complicate the interface and persistence model with no requirement for it.
  Date/Author: 2026-06-04 / Codex

- Decision: The sorter will never delete anything and never overwrite anything. All collisions will be solved by safe numbered names.
  Rationale: this is a hard product rule from the user requirements and must shape both sorting and undo behavior.
  Date/Author: 2026-06-04 / Codex

## Outcomes & Retrospective

At this stage the plan is complete, but the feature is not yet implemented in the committed code. The expected end result is a panel-native quick tool that looks and behaves like the other built-in utilities and that can be installed through the normal installer flow. The key lesson captured here is that this feature cannot be treated as “just a popup”: it touches data persistence, panel layout, hotkeys, localization, undo safety, tests, and release packaging.

## Context and Orientation

`AiteBar` is a WPF desktop utility for Windows built on `.NET 8`. The panel UI is centered around `AiteBar/MainWindow.xaml` and `AiteBar/MainWindow.xaml.cs`. Application settings are plain serializable objects defined in `AiteBar/Models.cs` and saved through `AiteBar/AppSettingsService.cs`. Global and tool hotkeys are managed by `AiteBar/HotkeyService.cs`. The quick-tools settings UI lives in `AiteBar/AppSettingsWindow.xaml` and `AiteBar/AppSettingsWindow.xaml.cs`. Launching built-in utilities goes through `AiteBar/ActionService.cs`.

In this repository, a “quick tool” means a built-in panel action that behaves like the search button, downloads button, quick note, or timer. To add a new quick tool, the work must touch all of these areas:

1. `Models.cs` for persisted settings and hotkey binding storage.
2. `MainWindow.xaml` for the panel button itself.
3. `MainWindow.xaml.cs` for visibility, tooltip, click handling, keyboard navigation, and context-menu hide action.
4. `HotkeyService.cs` for a new `HotkeyCommand` and mapping.
5. `AppSettingsWindow.xaml` and `.cs` for the new quick-tools checkbox and hotkey row.
6. `ActionService.cs` for creating, reusing, and showing the popup window.
7. Localized `.resx` files for every visible label.

There are already two compact tool windows that define the house style and ownership pattern:

- `AiteBar/QuickNoteWindow.xaml` and `.cs`
- `AiteBar/TimerStopwatchWindow.xaml` and `.cs`

There is also an existing position helper:

- `AiteBar/QuickNoteLayoutHelper.cs`

That helper already knows how to place a compact window near the active panel edge and monitor. The file sorter must reuse this logic instead of inventing new coordinate math unless an actual bug forces a new helper.

## Functional Requirements

The utility must implement this exact user flow:

1. The user clicks the file-sorter icon in the panel.
2. A compact popup opens near the panel.
3. The popup shows the title `Сортировка файлов`.
4. The popup shows one field labeled `Где сортировать`.
5. The popup offers three choices:
   `Загрузки`, `Рабочий стол`, `Выбрать папку...`
6. The popup shows one primary button: `Сортировать`.
7. After clicking the button, the same popup switches to a progress state.
8. After completion, the same popup switches to a result state:
   `Сортировка завершена`
   `<N> файлов разложены по папкам`
   `Открыть папку`
   `Откатить`

The utility must not expose extra settings in the main popup. No recursive mode, no checklists of categories, no previews, no tables, and no advanced configuration screen belong in this flow.

The sorting logic must follow these exact product rules:

- Scan only files in the selected root folder.
- Never recurse into subfolders.
- Never move or rename the subfolders themselves.
- Skip hidden files.
- Skip system files.
- Skip shortcuts such as `.lnk`.
- Skip temporary files.
- Skip downloading or incomplete files.
- Skip files currently used by another program.
- Skip files created very recently, using a safety threshold of 2 minutes.
- Never delete anything.
- Never overwrite anything.

The category folders must be created inside the selected root folder and must use these exact names:

- `Изображения`
- `Документы`
- `Видео`
- `Аудио`
- `Архивы`
- `Установщики`
- `Проекты`
- `Веб`
- `Прочее`

Extensions must map exactly as follows:

- `Изображения`: `jpg`, `jpeg`, `png`, `webp`, `gif`, `bmp`, `tiff`, `tif`, `heic`, `heif`, `avif`, `svg`, `ico`
- `Документы`: `pdf`, `doc`, `docx`, `txt`, `rtf`, `odt`, `md`, `xls`, `xlsx`, `csv`, `ppt`, `pptx`, `epub`, `fb2`, `djvu`
- `Видео`: `mp4`, `mov`, `avi`, `mkv`, `webm`, `m4v`, `mpg`, `mpeg`, `wmv`, `flv`, `3gp`, `mts`, `m2ts`
- `Аудио`: `mp3`, `wav`, `flac`, `aac`, `m4a`, `ogg`, `opus`, `wma`, `aiff`, `mid`, `midi`
- `Архивы`: `zip`, `rar`, `7z`, `tar`, `gz`, `bz2`, `xz`, `zst`, `tgz`, `cab`, `iso`
- `Установщики`: `exe`, `msi`, `msix`, `appx`, `deb`, `rpm`, `pkg`, `apk`, `ipa`, `dmg`
- `Проекты`: `psd`, `psb`, `ai`, `eps`, `fig`, `sketch`, `xd`, `indd`, `cdr`, `afdesign`, `afphoto`, `prproj`, `aep`, `drp`, `blend`, `obj`, `fbx`, `stl`, `step`, `dwg`, `dxf`
- `Веб`: `html`, `htm`, `css`, `scss`, `js`, `jsx`, `ts`, `tsx`, `php`, `json`, `xml`, `yaml`, `yml`, `vue`, `svelte`, `astro`
- `Прочее`: everything else

If the target folder already contains a file with the same name, the sorter must create the next safe numbered name:

    photo.jpg
    photo (1).jpg
    photo (2).jpg

Undo must remember, for each moved file:

- the original full path
- the final full path

Undo must move files back in reverse order and must use the same safe numbering strategy if the original target name is no longer free.

## UI Requirements

The popup must visually match the existing panel ecosystem. That means:

- dark restrained background matching the panel palette
- compact size, no full-size dialog behavior
- owned window, not taskbar-visible
- no WPF default white controls
- typography and spacing that fit the panel style
- one selector block, one primary button in the first state
- one loading block in the second state
- one success/result block in the third state

The second and third screens are not separate windows. They are state changes of the same popup.

The popup must stay usable on all panel edges:

- `Top`
- `Bottom`
- `Left`
- `Right`

The implementation must therefore reuse the edge-aware placement that existing quick tools already use.

## Required Code Changes By Area

### 1. Data Model And Persistence

Edit `AiteBar/Models.cs`.

Add these new members to `AppSettings`:

- `bool ShowPresetFileSorter { get; set; } = true`
- `HotkeyBinding FileSorterHotkey { get; set; } = new()`
- `FileSortUndoState? LastFileSortOperation { get; set; }`

Also define the small data types needed by the feature. They should remain simple serializable classes or records because `AppSettingsService` persists plain settings objects.

Required types:

- `enum FileSortLocationKind { Downloads, Desktop, Custom }`
- `sealed class FileSortOperationEntry`
- `sealed class FileSortUndoState`
- `sealed class FileSortResult`
- `sealed class FileSortUndoResult`

`FileSortResult` must contain enough data for the popup to show the sorted count, root folder path, and whether an undo snapshot should be saved. `FileSortUndoResult` must contain enough data to show full or partial success after undo.

### 2. Sorting Service

Create `AiteBar/FileSorterService.cs`.

This file must contain all non-UI logic:

- location-independent sort routine
- extension-to-category mapping
- skip-rule detection
- safe name generation
- undo routine

Recommended public surface:

    public sealed class FileSorterService
    {
        public FileSortResult SortFiles(string rootPath);
        public FileSortUndoResult UndoLastSort(FileSortUndoState undoState);
    }

The implementation must:

- enumerate only top-level files with `Directory.EnumerateFiles(rootPath)`
- inspect file attributes for hidden/system skip behavior
- reject `.lnk`
- reject temp/incomplete patterns such as `.tmp`, `.temp`, `.part`, `.partial`, `.crdownload`, `.download`, and names starting with `~$`
- reject files younger than 2 minutes based on file timestamps
- reject files that cannot be opened with exclusive access
- create category directories on demand
- move eligible files safely
- build the undo snapshot only from successful moves
- perform undo in reverse order
- never remove directories during undo

### 3. Popup Window

Create two files:

- `AiteBar/FileSorterWindow.xaml`
- `AiteBar/FileSorterWindow.xaml.cs`

This is standard WPF structure: `.xaml` holds the visual layout and `.xaml.cs` holds behavior. The popup must inherit from `DarkWindow`, like the other compact tools.

The code-behind must:

- keep the current UI state (`Idle`, `Sorting`, `Completed`)
- resolve the chosen location
- open a folder picker for `Выбрать папку...`
- call `FileSorterService`
- save the resulting undo snapshot into `_settingsService.Settings.LastFileSortOperation`
- save settings after a successful sort and after undo updates
- implement `Открыть папку`
- implement `Откатить`
- expose a method used by `ActionService` to show the window near the panel

The XAML must expose only the controls needed by the design. The selection state should not contain any extra panels or debug information. The progress state should replace the content with a loader and waiting text. The result state should show the success icon, count, and the two buttons.

### 4. ActionService Integration

Edit `AiteBar/ActionService.cs`.

Add a new owned-window interface similar to the existing ones:

    internal interface IFileSorterToolWindow
    {
        bool IsVisible { get; }
        event EventHandler? Closed;
        bool Activate();
        void ShowNearPanel(AppSettingsService settingsService);
    }

Extend `IActionServiceRuntime` with a factory:

    IFileSorterToolWindow CreateFileSorterWindow(AppSettingsService settingsService, Window? owner);

Add a new field to `ActionService` for the cached window instance and add:

    public async Task StartFileSorterAsync(Func<Task>? onBeforeExecute = null)

Behavior must match the existing quick tools:

- run `onBeforeExecute` first if provided
- if the file sorter window is already visible, activate it
- otherwise create it once through the runtime factory
- set `Owner`
- clear the cached reference on `Closed`
- position it near the panel with `ShowNearPanel`

The runtime implementation at the bottom of the file must create:

    new FileSorterWindow(settingsService) { Owner = owner }

### 5. Panel Button Integration

Edit `AiteBar/MainWindow.xaml`.

Add one new system utility button inside `SystemUtilsPanel`. It must follow the same visual style as the other quick-tool buttons and use a Fluent icon that reads as file sorting or organizing.

Edit `AiteBar/MainWindow.xaml.cs`.

The new button must be integrated in all the same places as the other preset tools:

- tooltip localization
- context menu entry for “hide this preset tool”
- visible system-button count
- visibility application logic
- keyboard focus enumeration
- click handler
- hotkey dispatch if the tool gets a hotkey command

The click handler must call:

    _actionService.StartFileSorterAsync(HideDock)

through the same pattern used by the other preset actions.

### 6. Hotkeys

Edit `AiteBar/HotkeyService.cs`.

Add:

- `HotkeyCommand.FileSorter`
- a new hotkey registration id constant
- a new descriptor with display key `Tool_FileSorter`
- mapping from `settings.FileSorterHotkey`

The new command must become part of `CreateDefinitions(...)` so it appears beside the other built-in tool hotkeys.

### 7. Settings Window

Edit:

- `AiteBar/AppSettingsWindow.xaml`
- `AiteBar/AppSettingsWindow.xaml.cs`

In the Hotkeys tab, add one row for the new built-in tool following the same structure used by `Tool_QuickNote`, `Tool_ColorPicker`, and `Tool_TimerStopwatch`.

In the Quick Tools tab, add one checkbox for showing or hiding the file sorter button.

In the code-behind, load and save:

- `ShowPresetFileSorter`
- `FileSorterHotkey`

Also include the new hotkey in the duplicate/conflict validation list.

### 8. Localization

Edit these files:

- `AiteBar/Resources/Strings.resx`
- `AiteBar/Resources/Strings.ru.resx`
- `AiteBar/Resources/Strings.uk.resx`
- `AiteBar/Resources/Strings.de.resx`

Required string keys:

- `Tool_FileSorter`
- `Main_FileSorterTooltip`
- `FileSorter_Title`
- `FileSorter_CompletedTitle`
- `FileSorter_LocationLabel`
- `FileSorter_LocationDownloads`
- `FileSorter_LocationDesktop`
- `FileSorter_SelectFolder`
- `FileSorter_SelectFolderDialogTitle`
- `FileSorter_SortButton`
- `FileSorter_OpenFolder`
- `FileSorter_Undo`
- `FileSorter_ResultFormat`
- `FileSorter_LoadingTitle`
- `FileSorter_LoadingSubtitle`
- `FileSorter_UndoCompleted`
- `FileSorter_UndoPartial`
- `FileSorter_ErrorFormat`

The Russian strings must match the product wording exactly. Other locales should be translated consistently but do not change the behavior contract.

### 9. Tests

Create `AiteBar.Tests/FileSorterServiceTests.cs`.

Minimum test coverage:

- category mapping for known and unknown extensions
- top-level-only behavior
- skip hidden/system files
- skip `.lnk`
- skip temp and incomplete names
- skip very recent files
- skip locked files if practical to simulate
- destination folder auto-creation
- safe conflict naming on sort
- reverse-order undo
- safe conflict naming on undo
- partial undo behavior

Edit `AiteBar.Tests/HotkeyServiceTests.cs`.

Add assertions that `FileSorter` appears in created command definitions and maps to the correct id.

Edit `AiteBar.Tests/ActionServiceTests.cs`.

Extend the fake runtime with:

- a `FakeFileSorterToolWindow`
- a file-sorter window factory
- counters for creation calls

Add tests that prove:

- `StartFileSorterAsync` creates the window the first time
- reuses the same visible instance
- creates a new instance after close

## Implementation Order

The safest order is:

1. Extend `Models.cs`
2. Add `FileSorterService.cs`
3. Add hotkey enum and `ActionService` interface surface
4. Add `FileSorterWindow.xaml` and `.cs`
5. Integrate the panel button in `MainWindow`
6. Integrate the settings window
7. Add localization
8. Add tests
9. Run build and tests
10. Manually verify UI
11. Build installer

This order keeps the domain logic available before the UI depends on it and keeps compile errors localized while the feature is assembled.

## Concrete Steps

Run all commands from the repository root:

    D:\01_Codebdbd\01_projects\aitebar

Inspect existing quick-tool patterns before coding:

    rg -n "QuickNote|TimerStopwatch|ShowPreset|HotkeyCommand" AiteBar AiteBar.Tests

After implementing the new files and edits, build:

    dotnet build .\AiteBar.sln -c Release

Run tests:

    dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release

If `dotnet test` fails due to WPF temporary build issues, use:

    dotnet vstest .\AiteBar.Tests\bin\Release\net8.0-windows\AiteBar.Tests.dll

When code and tests are green, manually verify:

1. Start the app.
2. Confirm the new quick-tool button is visible in the panel.
3. Open the popup from the panel.
4. Verify the first state shows only the title, location selector, and `Сортировать`.
5. Choose a disposable test folder and start sorting.
6. Verify the same popup changes to progress state.
7. Verify the same popup changes to result state.
8. Click `Открыть папку` and confirm the correct folder opens.
9. Click `Откатить` and confirm files return safely.
10. Repeat on all four panel edges.

Finally build the installer:

    .\installer\Build-Installer.ps1

Then verify that a current installer exists in:

    artifacts\installer

## Validation and Acceptance

The feature is accepted only if all of the following are true.

The panel shows a new file sorter tool when enabled. The settings window contains a new quick-tool checkbox and hotkey row. The popup opens as a compact owned window near the active panel edge. The popup is one window with three internal states, not multiple windows.

Sorting acceptance:

- only top-level eligible files move
- category folders are created automatically
- subfolders are untouched
- hidden, system, shortcut, temp, downloading, locked, and too-new files are skipped
- no file is deleted
- no file is overwritten
- name conflicts are resolved with numbered names

Undo acceptance:

- the last successful sort operation can be undone
- files move back in reverse order
- undo also avoids overwrite by generating numbered names
- partial undo is reported honestly

Release acceptance:

- `dotnet build .\AiteBar.sln -c Release` succeeds
- the test command succeeds
- the installer script succeeds
- the rebuilt installer in `artifacts\installer` contains the feature

## Idempotence and Recovery

The implementation is additive and can be repeated safely if files are edited carefully. The service itself must be safe by construction because it only moves eligible files and never overwrites. Manual verification must always use disposable test folders, never the real desktop or real downloads folder.

If a compile step fails, fix the failing area and rerun the same build command. If the installer build fails after a successful publish and no source files changed, rerun `Build-Installer.ps1` with `-SkipPublish` only when the publish output is confirmed current. Do not remove user files or clean arbitrary directories as part of this work.

## Artifacts and Notes

Expected implementation files:

- `AiteBar/Models.cs`
- `AiteBar/FileSorterService.cs`
- `AiteBar/FileSorterWindow.xaml`
- `AiteBar/FileSorterWindow.xaml.cs`
- `AiteBar/ActionService.cs`
- `AiteBar/MainWindow.xaml`
- `AiteBar/MainWindow.xaml.cs`
- `AiteBar/HotkeyService.cs`
- `AiteBar/AppSettingsWindow.xaml`
- `AiteBar/AppSettingsWindow.xaml.cs`
- `AiteBar/Resources/Strings.resx`
- `AiteBar/Resources/Strings.ru.resx`
- `AiteBar/Resources/Strings.uk.resx`
- `AiteBar/Resources/Strings.de.resx`
- `AiteBar.Tests/FileSorterServiceTests.cs`
- `AiteBar.Tests/ActionServiceTests.cs`
- `AiteBar.Tests/HotkeyServiceTests.cs`

Expected proof artifacts after implementation:

- successful `Release` build output
- passing tests including the new sorter tests
- manual confirmation of the popup flow
- fresh installer artifact in `artifacts\installer`

Plan revision note: this file was rewritten on 2026-06-04 to make it a direct, detailed implementation plan for `AiteBar` rather than a generic feature note. The rewrite explicitly captures the exact UI states, sorting rules, integration points, and acceptance criteria requested by the user.
