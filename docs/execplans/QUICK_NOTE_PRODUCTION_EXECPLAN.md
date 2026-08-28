# Harden Quick Note for production without changing its single-note contract

This is a living ExecPlan maintained under `PLANS.md`. Progress, Surprises & Discoveries, Decision Log, and Outcomes & Retrospective must be updated throughout implementation.

## Purpose / Big Picture


Quick Note should remain a small, fast Windows note window, with its existing formatting, code blocks, tasks, links, images, themes, undo/redo, pinning, and remembered bounds. This work makes saving trustworthy during concurrent editing, preserves unreadable originals, removes avoidable UI-thread file work, and gives the existing compact interface clear accessible controls and status. Success means a user can type during saving, close/reopen, and recover from file errors without losing content. It does not mean adding notebooks, cloud sync, telemetry, a new editor framework, or a new storage format.

## Progress


- [x] (2026-08-27) Read the handbook and PLANS.md; inspected current service, persistence adapter, save controller, window, themes, and tests. Recorded existing dirty files without reverting them.
- [x] (2026-08-27) Attempted baseline Release solution build; observed failure with zero diagnostics, requiring investigation.
- [x] (2026-08-27) Milestone 1: baseline Quick Note run passed 168/171 with failures for missing task controls and lost task state; serial Release build succeeded without warnings. Added conflict-version, disposal, retry, malformed-file, and equal-metadata regression tests.
- [x] (2026-08-27) Milestone 2: version-aware single active save, codec/file store boundary, unreadable-source protection, flushed atomic writes, recovery-folder action, package task projection. Tests cover inline image position, nested tasks, and real window closure during delayed normal/conflict saves.
- [x] (2026-08-27) Milestone 3: direct theme resource bindings replace cached visual-tree mutation; native fonts use assembly-qualified resource paths. All 15 toolbar buttons fit at minimum/default size, with accessible names, focus/pressed/disabled states, title-bar undo/redo, theme names, and status truncation/tooltips. WPF renders inspected in dark and sage themes. Removed unused cancellation sources; selection-only movement does not invalidate full-document statistics; corrected words being displayed as line count.
- [x] (2026-08-27) Milestone 4: final combined Release suite passed 1469/1469, including 188 Quick Note tests; zero skipped/failed. Serial solution build passed with zero warnings/errors. Reviewed actual WPF PNGs in both sizes/themes, recorded performance measurement, updated README/manual/function map/architecture/release checklist, and verified a clean whitespace check.

## Surprises & Discoveries


The worktree already contains substantial changes in Quick Note and unrelated utilities. Treat these as user work. Existing `QuickNoteDocumentContract.cs` consolidates inline cloning; do not replace it or roll back its semantics. The actual default format in `QuickNoteService` is WPF XamlPackage under `.aite-note`, with `.rtf` compatibility; there is no current QuickNoteMarkdown class to migrate.

`QuickNoteSaveController.SaveNowAsync` clears all pending changes after an asynchronous conflict save without checking the change version. An edit made during that save can therefore be reported as saved although it is absent from the copy. `Dispose` disposes a semaphore that an active save later releases. `LoadPackage` and `LoadRtf` swallow parsing failures and expose an empty document; a later save can replace the unreadable original. `HasExternalChanges` skips content comparison for equal length and modification time.

Baseline command `dotnet build .\AiteBar.sln -c Release` returned exit code 1 with zero warnings and zero errors before implementation. Diagnose the environment/build path rather than treating that result as success.

The serial command `dotnet build .\AiteBar.sln -c Release -m:1` succeeded with zero warnings/errors. Baseline Quick Note tests passed 168/171: existing toolbar/menu expectations disagreed with the actual controls, and a physical package test lost its task checkbox. The service refactor initially caught IOException but omitted InvalidDataException (a distinct exception hierarchy); regression tests exposed this and the catch was corrected.

The adapter's portable image export splits inline images into separate paragraphs. Packages support native images, so package serialization must use native image clones to retain their positions, while legacy RTF keeps its portable marker path. A focused task-plus-image round-trip test was added.

Cloning a whole runtime document through XamlWriter/XamlReader failed on task-checkbox templates (`BoxBorder` target missing). The final package projection copies native block structure explicitly and replaces task controls before serialization. This avoids both template serialization and the previous plain-text fallback. Task restoration snapshots the paragraph sequence before modifying WPF text elements; live enumeration was invalidated even when only inline content changed. Both findings were reproduced in physical round-trip tests before correction.

Rendered evidence exposed that `QuickNote_Stats` has two placeholders (characters, lines), but the controller passed character, word, and line counts, displaying words as lines. It also exposed font URI resolution depending on the host assembly and light-theme chrome depending on a late visual-tree scan. Explicit resource bindings fix those issues without traversing editor descendants. Final disk-store status getters are lock-free volatile reads, so typing does not block behind a background disk flush through a status update.

## Decision Log


Decision (2026-08-27, Codex): preserve WPF FlowDocument as the editor model and the existing helper/controller boundaries. A FlowDocument is WPF's structured text tree; its UI objects must stay on their owning thread. Serialize that document on its owner thread, but move filesystem operations on the resulting bytes off that thread. This is a concrete separation of responsibilities, not a wholesale framework migration.

Decision (2026-08-27, Codex): protect unreadable original files and surface load failure. Do not silently authorize overwrite merely because the UI recovered to an empty document. Subsequent local text must go to the existing conflict-copy recovery path.

Decision (2026-08-27, Codex): keep native toolbar controls, theme choices, and minimal single-note layout. Improve keyboard focus, disabled/pressed states, semantic labels, compact spacing, and status legibility instead of adding costly blur effects or unrelated navigation. Current Microsoft Windows keyboard guidance calls for predictable focus and accessible names; WPF threading guidance requires UI work items to remain short. These principles are implemented within the existing stack.

Decision (2026-08-27, Codex): do not change release version, installer, external utility registrations, or unrelated settings. Review the integration points for compatibility and update user-facing Quick Note documentation only where behavior changes.

Decision (2026-08-27, user clarification): do not put checklists in the context menu. Keep the checklist command only in the formatting toolbar, and update the obsolete context-menu test to forbid it.

Decision (2026-08-27, Codex): replace the save semaphore with one tracked completion task. All callers share the active save result, forced callers wait up to ten seconds for an already-active operation, and disposal cannot race semaphore cleanup. Both normal and conflict saves drain newer document versions before reporting success.

Decision (2026-08-27, Codex): primary package serialization fails visibly on unsupported content instead of silently replacing it with plain text; runtime task controls become portable markers, while native inline images and structured blocks stay in place. Legacy RTF export remains its separate compatibility projection.

Decision (2026-08-27, Codex): update inaccurate Quick Note documentation to match actual controls. The existing window had no Markdown/TXT import/export or Open in Editor command despite documentation claiming them. This task did not remove those commands from code. Existing utility IDs, settings, model defaults, hotkey and registry integration remain unchanged after inspection.

## Outcomes & Retrospective


Implementation and automated verification are complete. The editor remains one local note with existing theme IDs, settings, registry identity, images, links, tasks, lists, code, pinning, undo/redo, and geometry handling. Document serialization and disk state now have separate components; save completion tracks exactly which edit version was persisted, including conflict copies. Closing drains edits both during active saves and during settings persistence. Failed loads protect the original and route new content to recovery copies, and failed writes retain the editor for retry. The package preserves native inline image positions and nested task state without serializing runtime checkbox templates or using silent plain-text fallbacks for the primary document.

UI chrome uses direct theme resources rather than a cache of visual descendants. The compact toolbar fits at 460×320 as well as 580×430. Undo/Redo, theme selection, Ctrl+S retry, error status, recovery-folder access, localized theme names, and keyboard focus are exposed. The user's context-menu requirement is enforced by a regression test: no checklist command is present there. Status/statistics fixes include correct line counts and no placeholder over an image-only document. Existing unrelated dirty worktree changes were not reverted or committed.

Final evidence on this machine: serial Release solution build succeeded with zero warnings/errors; the combined test project passed 1469/1469 with 188 Quick Note tests, no failures and no skips. A synthetic 1000-paragraph document (about 110,000 characters) serialized in 205 ms, serialized plus loaded in 1385 ms, produced 4816 compressed bytes, and allocated 13,574,360 bytes on the serialization thread. This is an observed sample, not a performance guarantee. WPF serialization remains on its owning UI thread; first window load remains synchronous for compatibility. Those are explicit tradeoffs, not claims that arbitrarily large notes cannot pause the UI.

No installer, release version, telemetry, main-panel layout, or utility settings contract was changed. Real WPF close/save tests and offscreen WPF rendering were exercised. Physical multi-monitor/DPI transitions, tray entry points, Narrator, and an installed release were not manually exercised; the release checklist retains those environment-dependent checks. This work is a tested implementation, not a claim of exhaustive production certification or a published release.

## Context and Orientation


Run commands from `D:\01_Codebdbd\01_projects\aitebar`. `AiteBar/QuickNoteUtility.cs` opens the window. `AiteBar/QuickNoteWindow.xaml` owns controls; `.xaml.cs`, `.Editor.cs`, and `.Presentation.cs` handle lifecycle, editing, and styling. `QuickNoteSaveController.cs` delays autosave until typing pauses and coordinates close-time saving. `QuickNotePersistence.cs` adapts the concrete service for tests. `QuickNoteService.cs` currently combines serialization, migration, file comparison, atomic replacement, and conflict copies. Atomic replacement means writing a new temporary file then renaming it over the original, never truncating the original in place. `QuickNoteRtfAdapter.cs` and `QuickNoteDocumentContract.cs` preserve document structure. Footer and link controllers handle derived statistics and link detection. Tests are in existing `AiteBar.Tests/QuickNote*Tests.cs`, with STA helpers for WPF. STA means a dedicated Windows UI-compatible thread with its own dispatcher.

## Plan of Work


### Milestone 1: executable failure cases and baseline


Extend `QuickNoteSaveControllerTests.cs` using delayed persistence completions to exercise edits during ordinary and conflict saves, failed writes, repeated forced saves, and disposal while a save is active. Extend `QuickNoteServiceTests.cs` with equal-size/equal-timestamp external edits, malformed package/RTF preservation, and physical package round trips. Run the existing Quick Note tests before altering runtime behavior. Investigate a serial solution build and diagnostics if the initial build anomaly persists. The tests must assert persisted content and pending state, not merely private-field configuration.

### Milestone 2: reliable document and disk boundary


Refactor `QuickNoteSaveController` so every save captures a change version and clears dirty state only for that version, including conflict copies. Closing must drain the latest edits, failed writes must retain pending changes, and disposal must not race semaphore release or restart timers. Keep wait time bounded. In `QuickNoteService`, isolate byte serialization/deserialization from file operations with a small storage component, preserve package/RTF behavior, and serialize file operations. Capture hashes from the actual bytes loaded/saved and compare content even when metadata is unchanged. On parse failure retain the original and indicate load failure through the persistence adapter; local edits use conflict copies. Any temporary file must be cleaned on failure. Add no NuGet dependencies.

### Milestone 3: focused UI and performance work


Use `QuickNoteWindow.xaml`, `QuickNoteTheme.cs`, and `.Presentation.cs` for coherent surface roles, clear keyboard focus and disabled states, accessible names, and status visibility without removing tools. Keep all existing theme IDs. Avoid styling document-owned controls via window chrome. Consolidate duplicate status formatting and avoid allocating regex match collections for counts where a streaming scan suffices. Preserve large-note link-highlighting limits. Test minimum and default window sizes, theme changes, undo/redo, and save/reload using existing WPF tests, extending their natural fixtures. Render actual WPF windows to PNG for layout inspection if possible; these are verification artifacts, not a substitute mockup.

### Milestone 4: evidence and documentation


Update README and `docs/functions.md`/`docs/USER_MANUAL.md` for confirmed recovery and interaction changes; keep release checklist current. Inspect registry, model, settings, and localized resources to ensure no integration contract changed. Run required build/test commands and record exact results. Exercise real WPF window close/save and layout through tests; manual desktop interactions unavailable in the environment must remain explicitly unverified. Never label unrun monitor/tray checks as passed.

## Concrete Steps


From the repository root run:

    dotnet build .\AiteBar.sln -c Release
    dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release --filter QuickNote
    dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release

Expected final output: Build succeeded and all tests passed, with no new warnings. If WPF/MSBuild generated files break the test command, first build the test project and then run:

    dotnet vstest .\AiteBar.Tests\bin\Release\net10.0-windows\AiteBar.Tests.dll

Long-running commands are polled via their process session, not killed merely because a tool response times out. If sandbox access prevents build/test, request the necessary escalation. A serial diagnostic build may help isolate worker issues:

    dotnet build .\AiteBar.sln -c Release -m:1 -v:minimal

Actual final commands were:

    dotnet build .\AiteBar.sln -c Release -m:1
    $env:AITEBAR_QUICKNOTE_RENDER_DIR = Join-Path (Get-Location) 'artifacts/quicknote-validation'
    dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release --no-restore -m:1 --logger "trx;LogFileName=quicknote-production-full.trx"
    git -c core.safecrlf=false diff --check

The combined test command builds the current source first; it passed all 1469 tests in 54 seconds after compilation. The direct ordinary solution build anomaly is isolated by `-m:1`, not suppressed by ignoring an exit code. No fallback test invocation was necessary for the final run.

## Validation and Acceptance


A delayed save plus another edit must result in the newest content being saved before close. The same must hold for a conflict copy, with the original external file unchanged. A failed write must leave changes pending and allow retry. Disposing during saving must not throw or restart work. Invalid package and RTF inputs must be retained byte-for-byte after local editing; the UI must explain failure and provide a safe saved copy. Equal-size, equal-timestamp external edits must not be overwritten unnoticed. Existing tests must still prove code, tasks, links, underline/strike, images, and other formatting survive physical persistence.

At minimum/default sizes, all commands must fit and keyboard focus must be visible in both light and dark themes. Pinning, auto-dismiss, move/resize/clamping, Ctrl+Z/Ctrl+Y, URLs, theme selection, and copy/export remain. Run the manual steps in `docs/QUICK_NOTE_RELEASE_CHECKLIST.md` on a Windows desktop when accessible; record unsupported checks as remaining release verification, never infer them from compilation.

## Idempotence and Recovery


Source/test runs do not touch real user notes; tests use unique temporary directories. Do not delete unrelated artifacts or reset the dirty worktree. New storage logic keeps the original intact until a completed replacement. Repeating failed saves must remain safe. User note content must never be included in logs, screenshots, or fixtures; use synthetic documents for verification. No migrations beyond the existing RTF compatibility are introduced.

## Artifacts and Notes


This plan records this task, not a shipped release. Test evidence is `AiteBar.Tests/TestResults/quicknote-production-full.trx` (ignored local artifact). Actual WPF renders are `artifacts/quicknote-validation/quicknote-dark-460x320.png`, `quicknote-dark-580x430.png`, `quicknote-sage-460x320.png`, and `quicknote-sage-580x430.png`. They use synthetic content and contain no user note data. Reproduce them by setting the render directory variable above and running the `CompactWindow` test filter.

Final summary:

    Release solution: succeeded, 0 warnings, 0 errors (serial MSBuild).
    Combined suite: 1469 passed, 0 failed, 0 skipped.
    Quick Note subset in combined TRX: 188 passed.
    git diff --check: no whitespace errors.

## Interfaces and Dependencies


Keep public `QuickNoteService.Load`, `LoadAsync`, `SaveAsync`, `SaveConflictCopyAsync`, and `OpenConflictCopy` compatible. Extend `IQuickNotePersistence` minimally for load status if required, with a default for existing test adapters. File storage must operate on byte arrays or streams without WPF types; document codecs must operate only on the owning dispatcher. Keep `QuickNoteSaveController.SaveNowAsync(bool force = false)` and `HasPendingChanges` as the window contract. Use existing .NET file, hashing, synchronization, and WPF primitives; no new third-party library or persistent history.

Revision 2026-08-27: Created after source audit to turn the broad production-quality request into concrete, behavior-based milestones while preserving dirty user work.

Revision 2026-08-27: Recorded baseline failures, successful serial build, implemented disk/document split, image fidelity finding, and explicit user instruction prohibiting checklist commands in the context menu.

Revision 2026-08-27: Recorded native package projection, recursive task preservation, actual WPF rendering, lightweight theme bindings/statistics fixes, passed test partitions, and final combined verification in progress.

Revision 2026-08-27: Completed the combined Release run, recorded 1469 passing tests, the 1000-paragraph measurement, final WPF image locations, preserved integration contracts, and explicitly unperformed hardware/manual release checks.
