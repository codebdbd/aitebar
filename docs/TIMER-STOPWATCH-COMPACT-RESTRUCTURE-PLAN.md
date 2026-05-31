# Rebuild Timer/Stopwatch Compact Layout

This ExecPlan is a living document. It follows `PLANS.md` in the repository root.

## Purpose / Big Picture

The timer/stopwatch window currently tries to create compact mode by hiding rows inside the full window layout. That caused repeated visual regressions: full-mode tabs appearing in compact mode, content disappearing, and invalid alignment. After this change the full window and compact window are separate root views that share the same timer state. A user can switch to compact mode and see only a small row containing the current time, a start/pause icon, and an expand icon.

## Progress

- [x] (2026-05-31) Identified that compact mode must not live inside the full layout grid.
- [x] (2026-05-31) Replace the current in-grid compact view with a separate root-level compact border.
- [x] (2026-05-31) Simplify `UpdateMode` so it switches between root views instead of collapsing full-layout rows.
- [x] (2026-05-31) Build, test, smoke-check, and rebuild the installer.

## Surprises & Discoveries

- Observation: `MinWidth` and `MinHeight` can prevent WPF windows from actually using smaller compact dimensions.
  Evidence: compact mode stayed large until `MinWidth` and `MinHeight` were updated alongside `Width` and `Height`.
- Observation: an overlay inside the full grid is still affected by full-grid rows and sibling controls.
  Evidence: compact screenshots showed timer/stopwatch tabs even after the compact view was visible.

## Decision Log

- Decision: Implement compact mode as a root-level sibling of the full view.
  Rationale: This removes layout coupling. Full rows, tabs, presets, and buttons cannot affect compact mode if the full root is collapsed.
  Date/Author: 2026-05-31 / Codex.

## Outcomes & Retrospective

Implemented. The compact and full timer/stopwatch views are now independent root-level views. Compact mode no longer reuses or collapses rows from the full layout, so full-mode tabs and controls cannot bleed into compact mode. Validation passed with `dotnet test`, `dotnet build`, a smoke launch of `AiteBar.exe`, and installer regeneration.

## Context and Orientation

The relevant files are `AiteBar/TimerStopwatchWindow.xaml` and `AiteBar/TimerStopwatchWindow.xaml.cs`. The window is a WPF `DarkWindow`. Full mode contains tabs, timer/stopwatch displays, timer presets, input, sound, start/pause, and reset. Compact mode should contain only current time, start/pause, and expand.

## Plan of Work

Edit `AiteBar/TimerStopwatchWindow.xaml` so the root `Grid` contains two sibling borders. The existing full UI remains in `RootBorder`. A new `CompactRootBorder` contains only the compact row. Remove the old `CompactView` from inside the full grid.

Edit `AiteBar/TimerStopwatchWindow.xaml.cs` so `UpdateMode` toggles `RootBorder.Visibility` and `CompactRootBorder.Visibility`, updates window dimensions and minimum dimensions, and only switches timer versus stopwatch inside the full view when not compact. Keep the same timer state and the same start/pause handler for compact.

## Concrete Steps

Run these commands from `D:\01_Codebdbd\01_projects\aitebar`:

    dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release
    dotnet build .\AiteBar.sln -c Release
    .\installer\Build-Installer.ps1

## Validation and Acceptance

In compact mode the window should be about `232x54` and show only one row: time, start/pause icon, expand icon. No tabs, presets, reset button, input field, or empty full-mode rows should be visible. Tests should pass and the installer should be rebuilt in `artifacts\installer`.

## Idempotence and Recovery

The changes are local to the timer/stopwatch window and resources already used by it. Re-running build, tests, and installer generation is safe. If a XAML compile error occurs, restore the previous XAML section from git diff and retry the smaller step.

## Artifacts and Notes

Validation completed with these outcomes:

    dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release
    Passed: 269, Failed: 0, Skipped: 0

    dotnet build .\AiteBar.sln -c Release
    Build succeeded with 0 warnings and 0 errors.

    .\installer\Build-Installer.ps1
    Installer created in D:\01_Codebdbd\01_projects\aitebar\artifacts\installer

## Interfaces and Dependencies

No new external dependency is introduced. Compact and full views both use existing WPF controls and existing event handlers: `BtnStartPause_Click`, `BtnCompact_Click`, and `BtnClose_Click`.
