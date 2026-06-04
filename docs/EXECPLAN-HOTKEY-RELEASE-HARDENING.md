# Harden custom-button hotkeys for the v1.7.5 release

This ExecPlan is a living document and must be maintained in accordance with `PLANS.md`.

## Purpose / Big Picture

AiteBar must distinguish the global shortcut that launches a custom button from a Hotkey action's keyboard combination sent to another application. After this work, a Hotkey action can no longer recursively trigger itself, input failures are visible, oversized settings are rejected, and the release audit can be updated with verified evidence.

## Progress

- [x] (2026-06-04) Audited the current hotkey model and confirmed the self-trigger design defect.
- [x] (2026-06-04) Added a separate activation hotkey model, migration, package mapping, and editor UI.
- [x] (2026-06-04) Hardened keyboard input execution and registration failure reporting.
- [x] (2026-06-04) Enforced settings file limits and corrected documentation drift.
- [x] (2026-06-04) Added regression tests and ran release validation.

## Surprises & Discoveries

- Observation: Before this change, `CustomElement.Ctrl`, `Alt`, `Shift`, `Win`, and `Key` served both as Hotkey action payload and as the global element shortcut.
  Evidence: `SettingsWindow.xaml.cs`, `HotkeyService.cs`, and `ActionService.cs` all consume the same fields.

- Observation: Editing, deleting, duplicating, or importing custom-button shortcuts did not consistently re-register hotkeys immediately.
  Evidence: the relevant `MainWindow` paths refreshed the panel without calling `RegisterGlobalHotkey`.

## Decision Log

- Decision: Add `CustomElement.ActivationHotkey` while keeping the existing key fields as the Hotkey action payload.
  Rationale: This preserves existing Hotkey actions and gives every action type an independent optional global shortcut.
  Date/Author: 2026-06-04 / Codex

- Decision: Migrate legacy key fields to `ActivationHotkey` only for non-Hotkey actions.
  Rationale: For Hotkey actions the old fields are the payload; for other action types they can only represent the v1.7.5 element shortcut.
  Date/Author: 2026-06-04 / Codex

## Outcomes & Retrospective

Implementation and automated validation are complete. The standard Release solution build passed with zero warnings and errors, tests passed 411/411, the NuGet vulnerability scan found no vulnerable packages, and the v1.7.5 installer was rebuilt. The manual keyboard matrix remains a release condition because it cannot be executed in this non-interactive audit environment.

## Context and Orientation

`AiteBar/Models.cs` defines persisted settings and custom buttons. `AiteBar/SettingsWindow.xaml(.cs)` edits a button. `AiteBar/HotkeyService.cs` registers system-wide shortcuts. `AiteBar/ActionService.cs` sends Hotkey action input. `AiteBar/AppSettingsService.cs` loads, normalizes, clones, and saves buttons. Panel package import and export use `AiteBar/PanelPackageManifest.cs` and `AiteBar/PanelPackageMapper.cs`.

## Plan of Work

Add an `ActivationHotkey` binding to custom buttons and panel packages. Normalize old non-Hotkey elements by moving their old key fields into the new binding, while preserving old Hotkey action payloads. Update the button editor to show an independent global shortcut section and validate it as a global hotkey.

Update `HotkeyService` to register only `ActivationHotkey` for custom elements and return readable failure details. Update `ActionService` so partial `SendInput` calls fail the action and injected modifiers are released in `finally`.

Change the settings size guard to reject oversized files before reading. Update tests, technical documentation, changelog formatting, and the release audit.

## Concrete Steps

From the repository root:

    dotnet build .\AiteBar.sln -c Release
    dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release
    .\installer\Build-Installer.ps1

## Validation and Acceptance

The release is acceptable when non-Hotkey legacy element shortcuts migrate to `ActivationHotkey`, Hotkey action payloads remain unchanged, Hotkey actions are not registered from their payload fields, input failures return a failed action result, oversized settings fall back safely, all tests pass, the Release solution builds, and the installer is produced with version `1.7.5`.

## Idempotence and Recovery

Normalization must be idempotent: once old non-Hotkey fields are moved and cleared, subsequent loads must not change the element again. Existing Hotkey actions must never be migrated automatically.

## Artifacts and Notes

The authoritative release report is `docs/release-audit.md`.

## Interfaces and Dependencies

No new external dependencies are required. `CustomElement.ActivationHotkey` and `PanelPackageElement.ActivationHotkey` are additive persisted interfaces. Existing key fields remain the Hotkey action payload for backward compatibility.
