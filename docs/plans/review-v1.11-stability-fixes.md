# Fix review v1.11 stability and UX defects

This ExecPlan is a living document. The sections `Progress`, `Surprises & Discoveries`, `Decision Log`, and `Outcomes & Retrospective` must be kept up to date as work proceeds.

This document follows `PLANS.md` in the repository root. It is self-contained so a contributor can resume from this file without reading the chat history.

## Purpose / Big Picture

The file `CODE_REVIEW_v1.11.md` reports several user-visible defects in AiteBar: laggy color picking, clipboard manager UI churn, settings saves that can fail silently, Quick Note recursive save calls, timer behavior tied to wall-clock time, compact timer windows staying topmost, and error messages that are misleading or too raw. After this work, users should be able to use these utilities without UI stalls, false success messages, or silent loss of settings. The behavior is demonstrated by focused unit tests plus the normal release build and test commands.

## Progress

- [x] (2026-06-21 00:00Z) Read `CODE_REVIEW_v1.11.md`, confirmed current code locations, and created this ExecPlan.
- [ ] Harden settings persistence so failed writes are visible to callers and AppSettingsWindow no longer replaces unrelated settings with a stale clone.
- [ ] Remove recursive Quick Note save retry behavior while preserving conflict-copy and pending-change semantics.
- [ ] Reduce Clipboard Manager UI churn and report clipboard copy failures accurately.
- [ ] Improve Screen Color Picker pixel access and handle clipboard errors without closing falsely.
- [ ] Fix Timer/Stopwatch compact `Topmost` restoration and replace wall-clock deltas with monotonic elapsed time.
- [ ] Replace selected raw or cryptic user-facing errors with localized, clearer messages.
- [ ] Add or update focused tests for non-UI logic where practical, then run Release build and tests.

## Surprises & Discoveries

- Observation: The working tree already contained user edits to timer and shared utility XAML files before this plan started.
  Evidence: `git diff --stat` showed modified `AiteBar/TimerStopwatchWindow.xaml.cs`, several XAML files, `SettingsWindow.xaml.cs`, and `CODE_REVIEW_v1.11.md`.
- Observation: Hotkey validation in `AppSettingsWindow` is partly improved compared with the review text.
  Evidence: `ValidateHotkeyBindings(...)` runs before `_settings` fields are copied in `AiteBar/AppSettingsWindow.xaml.cs`, but failed registration after saving still closes the window.

## Decision Log

- Decision: Keep this work scoped to confirmed high-impact defects rather than broad redesigns such as a full error-handling framework or replacing all folder dialogs.
  Rationale: The user asked to be careful and not break behavior; small, focused changes lower risk in a dirty working tree.
  Date/Author: 2026-06-21 / Codex.
- Decision: Use targeted helper methods and return values for reliability defects rather than changing public UI architecture broadly.
  Rationale: The project currently keeps much UI logic in window classes and partial handlers; preserving that style avoids an accidental refactor.
  Date/Author: 2026-06-21 / Codex.

## Outcomes & Retrospective

No implementation outcome yet. This section will be updated after each major milestone and at completion.

## Context and Orientation

AiteBar is a WPF desktop utility. `AiteBar/AppSettingsService.cs` owns JSON settings persistence and exposes `Settings`, `Elements`, and update methods. `AiteBar/AppSettingsWindow.xaml.cs` edits global app settings. `AiteBar/QuickNoteWindow.xaml.cs` controls the Quick Note editor and auto-save behavior. `AiteBar/ClipboardHistoryService.cs` listens for clipboard updates and stores runtime history; `AiteBar/ClipboardManagerWindow.xaml.cs` renders that history. `AiteBar/ScreenColorPickerWindow.cs` displays a full-screen color picker. `AiteBar/TimerStopwatchWindow.xaml.cs` implements timer and stopwatch UI.

The word "monotonic" in this plan means time measured by `System.Diagnostics.Stopwatch`, which only moves forward for elapsed-time measurement and is not affected by the user or the network changing the system clock.

## Plan of Work

First, change settings persistence. `AppSettingsService.SaveAsync` must stop swallowing write failures. Callers that currently expect a completed save should receive an exception when the JSON cannot be written. `AppSettingsWindow` should avoid assigning a stale full `AppSettings` object back into the service; instead it should update a fresh settings clone immediately before saving, preserving unrelated runtime state such as custom elements and utility order. Existing callers that save after small updates should continue to work.

Second, change Quick Note save retry behavior. In `SaveNowAsync`, if a save is already running, mark that another save is required, wait for the current save to finish, and retry via an iterative loop instead of recursive self-calls.

Third, change Clipboard Manager. The service should return a success result from `CopyEntryToClipboard`. The window should show the copied status only on success and should avoid rebuilding its entire UI for every clipboard update when nothing visible changed. The safest incremental improvement is to debounce list rebuilds on the dispatcher and skip rebuilds when the filtered entry identity snapshot has not changed.

Fourth, change Screen Color Picker. Replace repeated `Bitmap.GetPixel()` calls with a locked byte-buffer snapshot that can read pixels directly. Keep the same UI and magnifier behavior, but build the magnifier from byte reads. Wrap clipboard writes so a busy clipboard leaves the window open and gives feedback.

Fifth, change Timer/Stopwatch. Replace `DateTime.UtcNow` deltas with `Stopwatch.GetTimestamp()` deltas. Fix compact-mode topmost by recording the previous topmost value when entering compact mode and restoring it when leaving.

Finally, adjust selected user-facing error messages. Prefer localized resource strings when they exist or add small resource keys in all resx files when needed. Do not expose raw exception messages unless the message is already a deliberate localized validation message from app code.

## Concrete Steps

Run commands from `D:\01_Codebdbd\01_projects\aitebar`.

Inspect the current worktree before each edit:

    git diff -- <file>

Apply small patches with `apply_patch`. After each milestone, run focused tests where available. At the end, run:

    dotnet build .\AiteBar.sln -c Release
    dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release

If `dotnet test` fails due to WPF/MSBuild generated temporary files, run:

    dotnet vstest .\AiteBar.Tests\bin\Release\net10.0-windows\AiteBar.Tests.dll

## Validation and Acceptance

The Release build must succeed. The test project must pass or, if the known WPF/MSBuild `dotnet test` issue appears, the `dotnet vstest` fallback must pass.

Focused acceptance:

Settings persistence: a simulated write failure in tests should not be reported as success. Saving from `AppSettingsWindow` should no longer replace unrelated element/order state from a stale window clone.

Quick Note: repeated save requests while a save is running should complete without recursive self-calls and should still persist the latest document state.

Clipboard Manager: copying an entry should show success only when `Clipboard.SetText` or `Clipboard.SetImage` succeeds. Rapid clipboard updates should coalesce into fewer UI rebuilds.

Screen Color Picker: moving the mouse should still update the hex/RGB preview and magnifier. A busy clipboard should not crash the window or close as if the color was copied.

Timer/Stopwatch: changing the system clock should not affect elapsed timer deltas; leaving compact mode should restore the previous topmost state.

## Idempotence and Recovery

All edits are source changes and can be repeated safely. If a patch conflicts with existing user edits, inspect the local diff and adapt the smallest possible patch instead of reverting. Do not run destructive git commands. If tests fail, keep the failure output in this plan and either fix the cause or document why it is unrelated.

## Artifacts and Notes

Initial evidence:

    CODE_REVIEW_v1.11.md lists ColorPicker, Clipboard Manager, Quick Note save recursion, settings persistence, timer compact mode, and raw error messages as user-visible defects.
    git diff --stat showed pre-existing local modifications, especially in timer and shared XAML files.

## Interfaces and Dependencies

`AppSettingsService.SaveAsync()` should continue to be an async method, but it should no longer hide exceptions. Any new settings update method should live in `AiteBar/AppSettingsService.cs` and use the existing `_stateLock` and `_saveSemaphore`.

`ClipboardHistoryService.CopyEntryToClipboard(ClipboardHistoryEntry entry)` should return `bool` or a small result object so `ClipboardManagerWindow` can decide whether to show success or failure.

`ScreenColorPickerWindow` should keep using Windows-only APIs already present in the file. Direct pixel access should be private to this window and disposed when the window closes.

Revision note 2026-06-21 / Codex: Created the plan after confirming the review findings against the current source. The plan intentionally narrows the scope to stability, data-loss, and clear UX defects.
