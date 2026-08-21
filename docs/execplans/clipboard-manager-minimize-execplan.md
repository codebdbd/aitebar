# Harden Clipboard Manager reliability, privacy, and accessibility

This ExecPlan is a living document. The sections `Progress`, `Surprises & Discoveries`, `Decision Log`, and `Outcomes & Retrospective` must be kept up to date as work proceeds.

This document must be maintained in accordance with `PLANS.md` at the repository root.

## Purpose / Big Picture

The Clipboard Manager must remain a lightweight history picker without surprising side effects, UI freezes, or silent persistence of sensitive clipboard content. Users can copy entries without automatically pasting them, explicitly opt into history across sessions, clear every entry when needed, and use every action by keyboard or assistive technology. The existing hide-to-AiteBar minimization behavior remains unchanged.

## Progress

- [x] (2026-07-15 06:10Z) Inspected `ClipboardManagerWindow`, `ClipboardManagerUtility`, the shared utility launcher, and existing integration tests.
- [x] (2026-07-15 06:15Z) Enabled standard Windows minimization and taskbar access for Clipboard Manager.
- [x] (2026-07-15 06:15Z) Restored an already visible minimized utility when its AiteBar panel action is invoked again.
- [x] (2026-07-15 06:20Z) Added focused structural coverage, completed Release validation, rebuilt the installer, and restarted the updated publish.
- [x] (2026-07-15 06:21Z) Reopened the plan after user acceptance rejected the Windows taskbar destination.
- [x] (2026-07-15 06:24Z) Replaced taskbar minimization with hide-to-AiteBar behavior, restored window ownership, and implemented same-instance restoration.
- [x] (2026-07-15 06:26Z) Added a real WPF lifecycle test and updated structural coverage; the combined focused suite passed 7/7.
- [x] (2026-07-15 06:33Z) Completed Release build, 645-test full suite, installer rebuild with verified checksum, publish restart, and post-start log smoke.
- [x] (2026-08-21) Audited capture, storage, WPF interaction, privacy defaults, localization, and focused tests; identified auto-paste, UI-thread image/persistence work, default persistence, missing full clear, and inaccessible icon actions.
- [x] (2026-08-21) Removed auto-paste, moved clipboard PNG encoding and capture-triggered persistence off the UI path, and rejected images over a 16-megapixel budget before encoding.
- [x] (2026-08-21) Made persistent history opt-in, exposed full wipe, and restored tab/screen-reader access to entry actions.
- [x] (2026-08-21) Added contract coverage for default privacy, no auto-paste, full wipe, and accessible actions; focused Clipboard tests passed 39/39.
- [x] (2026-08-21) Completed Release build with 0 warnings and 0 errors, full test suite with 1343 passed tests, and installer rebuild with SHA-256 `F797E7BB1EB02254F912A839B0E16D4168811A4D3B1F91ACFC51F22191B80520`.
- [x] (2026-08-21) Serialized deferred persistence, discarded stale snapshots, added bounded flush on persistence disable, service disposal, and application exit; focused tests passed 40/40 and the full suite passed 1344/1344.

## Surprises & Discoveries

- Observation: Clipboard Manager currently combines `ResizeMode="NoResize"` with `ShowInTaskbar="False"`.
  Evidence: these attributes are declared on the root element in `AiteBar/ClipboardManagerWindow.xaml`, so Windows exposes no useful recoverable minimize workflow.

- Observation: the generic utility launcher treats a minimized window as visible but only calls `Activate()`.
  Evidence: `UtilityBase<TWindow>.LaunchAsync` returns immediately for `_window is { IsVisible: true }` without restoring `WindowState`.

- Observation: the first implementation was technically recoverable but violated the intended AiteBar interaction model.
  Evidence: `ShowInTaskbar="True"` sent the window to the Windows taskbar, which the user explicitly rejected during acceptance testing.

- Observation: hiding the window without changing the launcher would create a second Clipboard Manager instance on the next panel action.
  Evidence: the shared launcher reused `_window` only while `IsVisible` was true; a targeted restoration hook is required before that branch.

- Observation: the `WM_CLIPBOARDUPDATE` handler currently PNG-encodes images and rewrites the complete JSON history on the WPF UI thread.
  Evidence: `ClipboardHistoryService.OnClipboardChanged` calls `RecordClipboardData`, which directly calls `SaveHistory`; the configured maximum permits 50 images of 5 MB each.

- Observation: selecting an unpinned item sends `Ctrl+V` after the window hides despite the action being labelled Copy.
  Evidence: `ClipboardManagerWindow.CopyEntry` calls `SendKeys.SendWait("^v")` after successful copy.

## Decision Log

- Decision: use the native Windows title-bar minimize command by setting `ResizeMode="CanMinimize"` instead of creating a custom button.
  Rationale: it preserves the existing `SingleBorderWindow` style, keeps resizing and maximization disabled, and follows normal Windows behavior.
  Date/Author: 2026-07-15 / Codex

- Decision: make Clipboard Manager an unowned taskbar window while leaving other utilities unchanged.
  Rationale: superseded after user acceptance. The independent taskbar workflow is not the required product behavior.
  Date/Author: 2026-07-15 / Codex

- Decision: teach the shared utility launcher to normalize `WindowState.Minimized` before activation.
  Rationale: pressing the panel action is a second recovery path and prevents a visible-but-minimized utility from becoming unreachable.
  Date/Author: 2026-07-15 / Codex

- Decision: keep the standard title-bar minimize command, but translate `WindowState.Minimized` into `WindowState.Normal` plus `Hide()`; set `ShowInTaskbar="False"` and restore `Owner = owner`.
  Rationale: the window disappears back into AiteBar, remains in the application lifecycle, and does not masquerade as an independent Windows application.
  Date/Author: 2026-07-15 / Codex

- Decision: add a protected `RestoreExistingWindow` hook and override it only for Clipboard Manager.
  Rationale: the hidden live instance must be reused with its UI state intact, while other utilities retain their current behavior.
  Date/Author: 2026-07-15 / Codex

- Decision: copy only, never paste automatically.
  Rationale: global key injection is unsafe and contradicts the visible Copy action. Pasting remains an explicit user action in the destination application.
  Date/Author: 2026-08-21 / Codex

- Decision: persistent clipboard history is opt-in and capture persistence is deferred to a background task.
  Rationale: clipboard data often contains secrets. The UI must remain responsive when an application places a large image on the clipboard, while a short deferred save still protects data on normal shutdown.
  Date/Author: 2026-08-21 / Codex

## Outcomes & Retrospective

The earlier taskbar outcome was rejected and has been superseded. Clipboard Manager now exposes the standard minimize command but converts it into hiding back into AiteBar; it has no Windows taskbar entry, remains owned by the main application, and restores the same live window from the panel. Clipboard history ownership remains in `ClipboardHistoryService.Instance`.

The updated focused suite passed 7/7, including a real STA/WPF lifecycle test that shows the window, minimizes it, verifies it is hidden and normalized, and restores the same instance. The Release build completed with zero warnings and errors, and the final full suite passed 645/645. Installer `1.11.1` was rebuilt; SHA-256 `2414F9DE3E580AA66540A22CBCC5508430F33FE60DBF721C7CEBC93D41A4E38A` matches the generated `SHA256SUMS.txt`. The updated publish is running as PID 25192, and `error.log` retained its pre-start timestamp and size, so startup added no exception.

The focused integration tests passed 3/3. The Release build completed with zero warnings and errors, and the full suite passed 640/640. `installer/Build-Installer.ps1` rebuilt version `1.11.1`; its SHA-256 is `46A134A3311ABD0C3E9D6470FD186BE32520ADCE5D7FE6DB06A47DA62C6BD6CB`. The updated publish restarted as PID 16356, and the production error log retained its old last-write timestamp `2026-07-15 06:05:00`, showing no new startup exception.

The 2026-08-21 reliability audit is in progress. No code has yet been changed for the audit findings; the remaining milestones above are the source of truth for the hardening work.

The reliability implementation now removes global key injection, makes history persistence opt-in, captures only the latest clipboard notification, encodes bounded images on a background task, and saves capture-triggered history on a background task. The UI exposes separate clear-unpinned and wipe-all commands, and its action buttons participate in keyboard navigation with automation names. Focused verification is complete; Release and full validation remain.

Release validation is complete: Clipboard-focused tests passed 39/39, the full suite passed 1343/1343, and the installer was rebuilt. The only remaining verification is manual interaction with a real large clipboard image and an assistive technology client.

The final persistence pass removes concurrent use of the shared temporary file and flushes the latest queued history snapshot during normal shutdown. Clipboard-focused tests now pass 40/40 and the full suite passes 1344/1344. The existing installer should be rebuilt again before distribution because this final source change occurred after its previous build.

## Context and Orientation

`AiteBar/ClipboardManagerWindow.xaml` defines the WPF window chrome. `ResizeMode` controls which standard title-bar commands Windows exposes, and `ShowInTaskbar` controls taskbar visibility. `AiteBar/ClipboardManagerUtility.cs` creates this window through the generic utility registry. `AiteBar/UtilityRegistry.cs` owns one live instance per utility and activates that instance when the panel action is invoked again. Clipboard capture itself belongs to the singleton `ClipboardHistoryService`, so minimizing or closing only the presentation window does not stop history collection.

## Plan of Work

Keep `ResizeMode="CanMinimize"`, set `ShowInTaskbar="False"`, handle `StateChanged` by normalizing and hiding, and restore the main-window owner. Add a no-op restoration seam to `UtilityBase<TWindow>` and override it for Clipboard Manager so the next panel action shows the existing hidden instance.

Extend structural integration coverage and add a real WPF lifecycle test that verifies hide and same-instance restore behavior.

## Concrete Steps

Work from `D:\01_Codebdbd\01_projects\aitebar`.

Run the focused test:

    dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release --filter "FullyQualifiedName~ClipboardManagerIntegrationTests"

Then run:

    dotnet build .\AiteBar.sln -c Release
    dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release --no-build

## Validation and Acceptance

The focused tests must prove that Clipboard Manager allows minimization, does not appear in the taskbar, retains its owner, hides after minimization, and restores the same instance through the panel action. Manual acceptance is: open Clipboard Manager, enter search text and select a filter, minimize it, confirm no Windows taskbar button appears, invoke Clipboard Manager from AiteBar, and confirm its state remains.

## Idempotence and Recovery

The changes are source-only and repeatable. If WPF build files are locked, stop only the AiteBar process running from `artifacts/publish/win-x64`, rerun validation, and restart that same publish afterward. Do not remove clipboard history or user settings.

## Artifacts and Notes

Validation evidence will be appended after implementation.

## Interfaces and Dependencies

No package or public API is added. `ClipboardManagerWindow` remains the presentation type, `ClipboardHistoryService.Instance` remains the history owner, and `IUtility.LaunchAsync` retains its signature.

Plan revision note (2026-07-15 06:10Z): created the self-contained plan after inspecting the window chrome, ownership, launcher behavior, and existing integration-test conventions.

Plan revision note (2026-07-15 06:20Z): marked implementation and automated validation complete; recorded focused/full test evidence, rebuilt artifact identity, application restart, and the remaining manual two-path restoration smoke.

Plan revision note (2026-07-15 06:26Z): replaced the rejected taskbar workflow with hide-to-AiteBar semantics, recorded the superseded decision, and added focused WPF evidence.

Plan revision note (2026-07-15 06:33Z): recorded final Release, full-suite, installer checksum, publish restart, and clean log evidence.

Plan revision note (2026-08-21): expanded this plan from minimization-only behavior to the clipboard reliability audit. Recorded the discovery and implementation of privacy, performance, interaction, and accessibility fixes; retained Release validation as the final checkpoint.

Plan revision note (2026-08-21, final): recorded focused/full test results, clean Release build, and the rebuilt installer checksum.

Plan revision note (2026-08-21, persistence closeout): recorded serialized deferred persistence, shutdown flushing, and the 40/40 focused plus 1344/1344 full test results.
