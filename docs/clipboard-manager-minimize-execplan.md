# Add recoverable Clipboard Manager minimization

This ExecPlan is a living document. The sections `Progress`, `Surprises & Discoveries`, `Decision Log`, and `Outcomes & Retrospective` must be kept up to date as work proceeds.

This document must be maintained in accordance with `PLANS.md` at the repository root.

## Purpose / Big Picture

The Clipboard Manager must be dismissible without closing or losing its current search, filter, and selection state. The title-bar minimize command must hide the window back into AiteBar, not create a Windows taskbar entry. Pressing Clipboard Manager on the AiteBar panel again must restore the same live window, and the owned window must not outlive AiteBar.

## Progress

- [x] (2026-07-15 06:10Z) Inspected `ClipboardManagerWindow`, `ClipboardManagerUtility`, the shared utility launcher, and existing integration tests.
- [x] (2026-07-15 06:15Z) Enabled standard Windows minimization and taskbar access for Clipboard Manager.
- [x] (2026-07-15 06:15Z) Restored an already visible minimized utility when its AiteBar panel action is invoked again.
- [x] (2026-07-15 06:20Z) Added focused structural coverage, completed Release validation, rebuilt the installer, and restarted the updated publish.
- [x] (2026-07-15 06:21Z) Reopened the plan after user acceptance rejected the Windows taskbar destination.
- [x] (2026-07-15 06:24Z) Replaced taskbar minimization with hide-to-AiteBar behavior, restored window ownership, and implemented same-instance restoration.
- [x] (2026-07-15 06:26Z) Added a real WPF lifecycle test and updated structural coverage; the combined focused suite passed 7/7.
- [x] (2026-07-15 06:33Z) Completed Release build, 645-test full suite, installer rebuild with verified checksum, publish restart, and post-start log smoke.

## Surprises & Discoveries

- Observation: Clipboard Manager currently combines `ResizeMode="NoResize"` with `ShowInTaskbar="False"`.
  Evidence: these attributes are declared on the root element in `AiteBar/ClipboardManagerWindow.xaml`, so Windows exposes no useful recoverable minimize workflow.

- Observation: the generic utility launcher treats a minimized window as visible but only calls `Activate()`.
  Evidence: `UtilityBase<TWindow>.LaunchAsync` returns immediately for `_window is { IsVisible: true }` without restoring `WindowState`.

- Observation: the first implementation was technically recoverable but violated the intended AiteBar interaction model.
  Evidence: `ShowInTaskbar="True"` sent the window to the Windows taskbar, which the user explicitly rejected during acceptance testing.

- Observation: hiding the window without changing the launcher would create a second Clipboard Manager instance on the next panel action.
  Evidence: the shared launcher reused `_window` only while `IsVisible` was true; a targeted restoration hook is required before that branch.

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

## Outcomes & Retrospective

The earlier taskbar outcome was rejected and has been superseded. Clipboard Manager now exposes the standard minimize command but converts it into hiding back into AiteBar; it has no Windows taskbar entry, remains owned by the main application, and restores the same live window from the panel. Clipboard history ownership remains in `ClipboardHistoryService.Instance`.

The updated focused suite passed 7/7, including a real STA/WPF lifecycle test that shows the window, minimizes it, verifies it is hidden and normalized, and restores the same instance. The Release build completed with zero warnings and errors, and the final full suite passed 645/645. Installer `1.11.1` was rebuilt; SHA-256 `2414F9DE3E580AA66540A22CBCC5508430F33FE60DBF721C7CEBC93D41A4E38A` matches the generated `SHA256SUMS.txt`. The updated publish is running as PID 25192, and `error.log` retained its pre-start timestamp and size, so startup added no exception.

The focused integration tests passed 3/3. The Release build completed with zero warnings and errors, and the full suite passed 640/640. `installer/Build-Installer.ps1` rebuilt version `1.11.1`; its SHA-256 is `46A134A3311ABD0C3E9D6470FD186BE32520ADCE5D7FE6DB06A47DA62C6BD6CB`. The updated publish restarted as PID 16356, and the production error log retained its old last-write timestamp `2026-07-15 06:05:00`, showing no new startup exception.

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
