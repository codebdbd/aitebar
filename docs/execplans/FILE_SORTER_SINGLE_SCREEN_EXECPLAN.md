# Convert File Sorter to a single operational screen

This ExecPlan is a living document. The sections `Progress`, `Surprises & Discoveries`, `Decision Log`, and `Outcomes & Retrospective` must be kept up to date as work proceeds.

This document must be maintained in accordance with `PLANS.md` at the repository root.

## Purpose / Big Picture

The File Sorter currently replaces its folder list with separate sorting and completion screens. After this change, the folder list always remains visible. Each folder row lets the user select the folder, open it, undo the last real sorting operation for that folder, and see that folder's progress or result. A single primary button at the bottom sorts all selected rows, while a compact footer reports the overall state. The behavior is visible by opening File Sorter, selecting multiple folders, starting a sort, and watching progress move from row to row without leaving the list.

## Progress

- [x] (2026-08-01 06:34Z) Reviewed `PLANS.md` and the current multi-folder File Sorter implementation.
- [x] (2026-08-01 06:34Z) Chose the single-screen row state model and per-folder Undo persistence strategy.
- [x] (2026-08-01 06:52Z) Added file-level progress contracts and reporting to `FileSorterService` with focused tests.
- [x] (2026-08-01 06:52Z) Replaced the three-state WPF layout with the 520-pixel-wide single-screen folder list and inline controls.
- [x] (2026-08-01 06:52Z) Implemented per-row progress, open-folder, Undo, status, localization refresh, and persistence behavior.
- [x] (2026-08-01 06:52Z) Updated all localization resources and UI/source contract tests.
- [x] (2026-08-01 07:09Z) Ran Release build, 1092-test complete suite, STA runtime verification, and rendered WPF visual verification.
- [x] (2026-08-01 07:09Z) Rebuilt the installer and recorded final evidence and retrospective.
- [x] (2026-08-01 07:39Z) Removed the row-height-changing progress bar after user validation and moved the overall result below Sort.
- [x] (2026-08-01 07:50Z) Eliminated whole-window disabled-state flashing and placed Add Folder and Sort in equal-width columns on one row.
- [x] (2026-08-01 07:50Z) Added runtime busy-state coverage, passed 1093 tests, visually rendered the final action row, and rebuilt the installer.
- [x] (2026-08-01 08:03Z) Replaced the mismatched secondary style with the shared utility `CommandButtonStyle`; both bottom actions now inherit identical geometry from `CommandButtonBaseStyle` and differ only in color.

## Surprises & Discoveries

- Observation: The current `SortMultipleFoldersAsync` progress callback reports only the folder index before sorting starts, so it cannot drive a truthful percentage bar.
  Evidence: `AiteBar/FileSorterService.cs` reports `(rootPath, i, total)` immediately before calling `SortFilesAsync`.

- Observation: Undo data already contains one `FileSortUndoState` per changed folder, but the window exposes it through one global completed-state button.
  Evidence: `MultiFileSortUndoState.PerFolder` in `AiteBar/Models.cs` is sufficient for locating Undo by normalized root path.

- Observation: File moves usually complete synchronously even though the service API returns `Task`, so running the service directly on the WPF dispatcher would update progress values without giving WPF a render opportunity.
  Evidence: The single-screen implementation uses `Task.Run` for filesystem work and a dispatcher-backed progress adapter; focused tests still pass with 45/45.

- Observation: Dispatching every file update would make large folders pay for one synchronous UI round trip per file.
  Evidence: `FileSorterUiProgress` reports folder boundaries and completion immediately while throttling intermediate UI updates to at most once every 50 milliseconds.

- Observation: The initially selected bundled Fluent glyph codepoints rendered as missing-glyph squares in the real WPF preview host even when the embedded font family was assigned explicitly.
  Evidence: The rendered preview showed squares for both row actions; switching only those actions to Windows `Segoe MDL2 Assets` glyphs `E7A7` and `E838` produced recognizable Undo and Open Folder icons.

- Observation: Showing and hiding a progress grid below the folder path changed the row height at operation start, and its thin blue bar visually merged with the divider as a phantom line.
  Evidence: User validation of the running utility showed both the vertical jump and blue line; the row now reports `processed/total` in its existing fixed-width status column without adding controls to the visual tree.

- Observation: `SetBusy(true)` changed `IsEnabled` on every switch and action, so WPF simultaneously applied disabled opacity across the entire window and looked like a flash.
  Evidence: Busy-state interaction is now blocked with `IsHitTestVisible` and keyboard event handling while actual availability remains in `IsEnabled`; beginning a sort no longer changes row opacity or clears all existing row statuses.

## Decision Log

- Decision: Keep one window surface and remove `SortingStatePanel` and `CompletedStatePanel` rather than hiding the folder list during work.
  Rationale: The user explicitly requested a single screen, and keeping row context visible makes multi-folder progress and per-folder actions understandable.
  Date/Author: 2026-08-01 / Codex

- Decision: Use icon-only row actions with localized tooltips and accessible names, while widening the fixed window from 360 to approximately 520 device-independent pixels.
  Rationale: Full button labels do not fit reliably in Russian, Ukrainian, German, and English alongside a switch, path, and status. Icons preserve the compact utility style.
  Date/Author: 2026-08-01 / Codex

- Decision: Treat Undo as the last sorting operation that actually moved files for each folder. Sorting a folder that has no movable files does not erase its previous usable Undo state.
  Rationale: A disabled Undo immediately after a no-op sort would discard a still-valid recovery action without changing the filesystem.
  Date/Author: 2026-08-01 / Codex

- Decision: Disable selection and row actions while a sort or Undo is running.
  Rationale: The settings model stores one Undo state per folder and is not designed for concurrent filesystem mutations. A single global busy state prevents conflicting operations.
  Date/Author: 2026-08-01 / Codex

- Decision: Run sorting and Undo on a worker thread and marshal throttled progress synchronously to the WPF dispatcher.
  Rationale: This keeps the window responsive and makes inline progress observable without overwhelming the dispatcher for folders containing many files.
  Date/Author: 2026-08-01 / Codex

- Decision: Extract per-folder Undo merging and replacement into `AiteBar/FileSorterUndoStateHelper.cs`.
  Rationale: The rules for preserving unrelated rows and retaining a usable Undo after a no-op sort are pure non-UI behavior and need direct unit coverage.
  Date/Author: 2026-08-01 / Codex

- Decision: Use Segoe MDL2 Assets for the two row-action glyphs.
  Rationale: A rendered WPF preview, not just source inspection, proved that this choice is legible in the actual Windows rendering path used by the application.
  Date/Author: 2026-08-01 / Codex

- Decision: Keep row geometry invariant during sorting by using muted textual progress in a 96-pixel status column, and place the overall result after the action row.
  Rationale: Stable geometry removes the visible jump and phantom divider, while the result reads naturally as feedback to the action that produced it. Add Folder and Sort use equal-width columns on one row as requested.
  Date/Author: 2026-08-01 / Codex

- Decision: Block busy-state input without changing control `IsEnabled` solely because work is running.
  Rationale: Hit testing and keyboard guards prevent concurrent actions without triggering WPF's disabled visual states across the whole interface.
  Date/Author: 2026-08-01 / Codex

- Decision: Use the existing utility style pair `CommandButtonStyle` and `PrimaryCommandButtonStyle` for the two equal actions.
  Rationale: Both inherit `CommandButtonBaseStyle`, guaranteeing the same height, minimum width, padding, font size, and control template while retaining neutral and accent colors.
  Date/Author: 2026-08-01 / Codex

## Outcomes & Retrospective

The File Sorter now remains on one operational screen. Desktop appears first, Downloads second, and every row has a selection switch, stable inline textual file progress, a localized result or error, an independently enabled Undo action, and Open Folder. Custom-folder removal remains in the right-click context menu. Sorting and Undo run away from the WPF dispatcher, conflicting input is blocked without changing the interface appearance, and closing is blocked until the operation safely finishes.

Per-folder Undo survives sorts of unrelated rows and no-op sorts because `FileSorterUndoStateHelper` merges only new non-null Undo states. Partial batch failure retains completed rows' Undo and marks the failed row. Runtime localization rebuilds rows while preserving selection, progress, and status.

Validation completed with a zero-warning Release build, 1093 of 1093 tests passing before the style-only correction, and 29 of 29 targeted command-style and File Sorter window tests passing afterward. STA tests construct the real window and verify four-column rows, per-folder action state, visually stable input blocking, and equal effective geometry for the two bottom actions. A rendered WPF preview was inspected for stable rows, equal action sizes, spacing, long paths, switches, footer order, and glyph legibility. Two later attempts to repeat the entire WPF suite stalled in `testhost` without reporting a failed test; each repository-specific process was identified before being stopped. `installer/Build-Installer.ps1` rebuilt `artifacts/installer/AiteBar-Setup.exe`; signing was skipped because no PFX certificate was supplied. No required implementation work remains.

## Context and Orientation

`AiteBar/FileSorterWindow.xaml` defines the WPF window. It currently contains `IdleStatePanel`, `SortingStatePanel`, and `CompletedStatePanel`, only one of which is visible at a time. `AiteBar/FileSorterWindow.xaml.cs` creates folder rows dynamically because the first two folders are Windows Downloads and Desktop and additional folders come from `AppSettings.SavedFileSortFolders`. The same code-behind starts sorting, opens folders, persists settings, and invokes Undo.

`AiteBar/FileSorterService.cs` performs filesystem work. `SortFilesAsync` enumerates top-level files, moves eligible files into localized category subdirectories, and returns `FileSortResult` with counts and an optional `FileSortUndoState`. `SortMultipleFoldersAsync` calls that method sequentially and wraps partial success in `MultiFileSortException` if a later folder fails. Undo moves files back using `UndoLastSortAsync`.

`AiteBar/Models.cs` contains result and Undo types. `AppSettings.LastMultiFileSortOperation` stores the current per-folder Undo states, while `LastFileSortOperation` remains a compatibility representation for a single state. `AiteBar/AppSettingsService.cs` deep-clones these settings before updates and saves them to the normal application settings file.

The folder list uses a local `ScrollViewer`; the whole fixed-height utility window must not gain a global vertical scrollbar. A row action is an icon button on the right. A determinate progress bar is truthful only when the service reports processed and total top-level file counts.

## Plan of Work

First, add `FileSortProgress` and `MultiFileSortProgress` value types to `AiteBar/Models.cs`. Extend `FileSorterService.SortFilesAsync` with an optional `IProgress<FileSortProgress>` parameter. Materialize the top-level file list once, report zero processed files, then report after every file whether it was moved or skipped. Extend `SortMultipleFoldersAsync` to accept `IProgress<MultiFileSortProgress>` and adapt the inner progress with the current folder index and total folder count. Preserve the partial-result exception behavior. Add tests proving monotonic file counts, the terminal count, empty-folder reporting, and multi-folder identity.

Second, replace the three panels in `AiteBar/FileSorterWindow.xaml` with one vertical surface. It will contain a heading with the selected count, the existing locally scrollable folder card, equal-width Add Folder and Sort actions on one row, and a compact overall status line below them. Increase the width to 520 while preserving fixed size, dark colors, standard title bar, Escape behavior, and existing corner-radius conventions. Define compact icon-button styles in the window resources.

Third, rebuild `AddFolderRow` in `AiteBar/FileSorterWindow.xaml.cs` so each row has four logical areas: selection switch, name/path/progress, result status, and icon actions. Store the generated controls in `FolderListEntry`. The Open button launches only that row's path. The Undo button finds that path's state, calls `UndoLastSortAsync`, replaces or removes the persisted state, saves settings, and updates only the row and footer.

Sorting will keep every row visible. The window sets a global busy flag, disables mutable controls, clears statuses only for selected rows, and passes progress into `SortMultipleFoldersAsync`. Progress updates the matching row's fixed-width status text without changing row height. Completed results update their matching rows, merge new non-null Undo states with existing states for unselected or unchanged folders, persist settings, and enable Undo where appropriate. A partial exception applies completed results, retains Undo for the failed folder, marks that row as failed, persists completed Undo states, and shows the existing error dialog.

Fourth, update all four `AiteBar/Resources/Strings*.resx` files with selected-count, ready, progress, row-result, row-error, row Undo, and tooltip strings. Runtime localization will capture selected paths and transient row state, rebuild localized rows, and reapply those states. Update `AiteBar.Tests/FileSorterWindowLayoutTests.cs` and `AiteBar.Tests/RuntimeLocalizationWindowSourceTests.cs` to assert the single-screen contract without relying on visual screenshots.

## Concrete Steps

Run all commands from `D:\01_Codebdbd\01_projects\aitebar`.

After the service milestone, run:

    dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release --filter "FullyQualifiedName~FileSorterServiceTests"

Expect every FileSorter service test to pass, including new progress and partial-Undo tests.

After the UI milestone, run:

    dotnet build .\AiteBar.sln -c Release
    dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release

The build must complete with zero warnings and zero errors. The complete test count may increase as tests are added; all discovered tests must pass.

For manual verification, start the Release application, open File Sorter from its panel or tray entry, and observe that the folder list never disappears. Select Desktop and Downloads, start sorting, observe inline progress moving through the selected rows, then use each row's Open and Undo buttons. Right-click a custom row and verify that removal remains available. Switch application language and verify that selection and row states remain visible with translated text.

## Validation and Acceptance

The change is accepted when File Sorter has no separate loading or completion screen; every folder row contains a selection switch and Open action; a row with persisted Undo has an enabled Undo action; and a row without persisted Undo has a disabled Undo action. Starting a multi-folder sort leaves the rows visible, disables conflicting controls, displays real processed/total file progress on the active row, and records a result on each completed row. Undo for one row does not remove Undo availability from unrelated rows. Closing and reopening the window restores per-folder Undo availability from settings.

If the second folder fails after the first has moved files, the first row must retain an enabled Undo action and the failed row must show an error state. Runtime language changes must not alter which switches are selected. Custom-folder removal must remain available through the localized dark context menu.

## Idempotence and Recovery

Build and test commands are safe to repeat. Service tests create isolated temporary directories and delete them in `finally` blocks. The implementation must not delete user files; sorting only moves eligible top-level files and Undo uses the existing guarded restore logic. If a build is interrupted and `testhost.exe` holds test DLLs, identify the exact testhost whose command line points into this repository before stopping only that process, then rerun the build.

The existing working tree already contains the multi-folder feature and belongs to the user. Do not reset or overwrite unrelated modifications. Apply changes on top of the current diff and use `git diff --check` before completion.

## Artifacts and Notes

Baseline validation before this plan showed a successful Release build and 1086 passing tests after the prior multi-folder fixes. Final post-refinement validation produced 1093 passing tests, followed by 29 passing targeted tests after the style-only correction, and rebuilt `artifacts/installer/AiteBar-Setup.exe` at 79,451,632 bytes with SHA-256 `B4A3049A98DF4442C5B5E623B6E1D0FB77AFB366994090B22954FD10C242ADF6`.

## Interfaces and Dependencies

No new NuGet dependency is required. Use WPF controls already available in the application, `AppContextMenuFactory` for custom-row removal, `LocalizationService` for all visible strings, and `AppSettingsService` for persistence.

At the end, `AiteBar/Models.cs` must expose progress values equivalent to:

    public sealed record FileSortProgress(string RootPath, int ProcessedFiles, int TotalFiles);
    public sealed record MultiFileSortProgress(string RootPath, int FolderIndex, int FolderCount, int ProcessedFiles, int TotalFiles);

`FileSorterService.SortFilesAsync` must accept an optional `IProgress<FileSortProgress>`. `FileSorterService.SortMultipleFoldersAsync` must accept an optional `IProgress<MultiFileSortProgress>`. Folder indices are zero-based in service code; visible labels add one.

`FileSorterWindow` must maintain one `FolderListEntry` per visible row containing references to its switch, fixed-width status text, Undo button, and Open button. Undo lookup and replacement compare normalized root paths using `StringComparison.OrdinalIgnoreCase` because the application targets Windows.

Revision note (2026-08-01 06:34Z): Created the initial self-contained plan after reviewing the current implementation and the user's approved single-screen concept.

Revision note (2026-08-01 06:52Z): Recorded completed service, UI, localization, and focused-test milestones; documented the worker-thread rendering discovery, throttling decision, and extracted Undo helper.

Revision note (2026-08-01 07:09Z): Completed the plan after full automated validation, real WPF rendering inspection, glyph correction, and installer rebuild; recorded final evidence and outcomes.

Revision note (2026-08-01 07:39Z): Incorporated user validation by eliminating the height-changing row progress grid, stabilizing the status column, reordering action feedback, and compacting Add Folder.

Revision note (2026-08-01 07:50Z): Removed busy-state opacity flashing and changed the bottom actions to equal-width buttons on one row following additional user feedback.

Revision note (2026-08-01 08:03Z): Corrected the Add Folder button to use the repository's existing unified utility command style pair and added geometry assertions.
