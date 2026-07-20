# Keep the panel indicator visible at every display scale

This ExecPlan is a living document and must be maintained in accordance with `PLANS.md`. The sections `Progress`, `Surprises & Discoveries`, `Decision Log`, and `Outcomes & Retrospective` reflect the current implementation state.

## Purpose / Big Picture

The small panel-position indicator must remain visible when Windows display scaling changes, including 125%, and the user must be able to drag it to a convenient location. Its saved position must survive restarts and scaling changes without moving outside the selected monitor.

## Progress

- [x] (2026-07-17 16:15 +0300) Reproduced the design flaw: the indicator uses absolute taskbar coordinates and has no user-controlled, scale-independent position.
- [x] (2026-07-17 16:22 +0300) Added scale-independent normalized position fields and a pure clamp/normalization helper.
- [x] (2026-07-17 16:28 +0300) Added captured pointer dragging that preserves a normal click for opening the panel and prevents the z-order timer from snapping the indicator back during a drag.
- [x] (2026-07-17 16:34 +0300) Made the process PerMonitorV2 aware through `ApplicationHighDpiMode` and sized the native indicator window in physical pixels.
- [x] (2026-07-17 16:39 +0300) Added focused tests; Release build completed with zero warnings/errors and all 779 tests passed.

## Surprises & Discoveries

- Observation: The error dialog originally shown is owned by `D:\BackgroundRemover\BackgroundRemover.Tests\...\testhost.exe`, not AiteBar. It is outside this change.
- Observation: `TaskbarPositionIndicatorService.UpdatePosition` passes a fixed 28-pixel size directly to `SetWindowPos`, while the WPF window declares 28 device-independent pixels. These differ at 125% scaling.
- Observation: The 250 ms z-order refresh also called full position recalculation, which would compete with pointer dragging.
  Evidence: `BringIndicatorToTop` calls `UpdatePosition`; the service now preserves z-order without moving or resizing while `_isDragging` is true.

## Decision Log

- Decision: Persist horizontal and vertical position as values from 0 to 1 relative to the usable placement range of the selected monitor.
  Rationale: Normalized coordinates survive resolution and DPI changes and can always be clamped to a visible position.
  Date/Author: 2026-07-17 / Codex
- Decision: Keep the existing taskbar-derived location as the default until the user drags the indicator.
  Rationale: Existing users retain current behavior, while dragging opts into a custom position.
  Date/Author: 2026-07-17 / Codex
- Decision: Make AiteBar PerMonitorV2 aware and use physical coordinates only at the Win32 boundary.
  Rationale: `GetCursorPos`, monitor rectangles, taskbar rectangles, and `SetWindowPos` must share one coordinate system.
  Date/Author: 2026-07-17 / Codex

## Outcomes & Retrospective

The indicator now supports dragging, stores normalized coordinates, clamps its complete physical rectangle to the selected monitor, and recalculates its size for per-monitor DPI. Release compilation completed with zero warnings and errors, all 779 automated tests passed, and `installer/Build-Installer.ps1` produced the updated installer. A human visual check at 100%, 125%, and 150% remains appropriate because this environment cannot directly judge the on-screen feel of pointer dragging.

## Context and Orientation

`AiteBar/TaskbarPositionIndicatorWindow.xaml.cs` owns pointer input for the 28-DIP indicator. `AiteBar/TaskbarPositionIndicatorService.cs` creates that window and positions it with Win32 `SetWindowPos`. `AiteBar/TaskbarGeometryHelper.cs` obtains taskbar and monitor geometry. `AiteBar/Models.cs` and `AiteBar/AppSettingsService.cs` define and clone persistent settings.

## Plan of Work

Add nullable normalized X/Y settings so old configuration files continue to use the default taskbar location. Add a pure `IndicatorPositionHelper` that converts between normalized and physical coordinates while clamping the full indicator rectangle inside the monitor. Change the indicator window so a short left click still toggles the panel, while movement beyond a small threshold starts a captured drag. The service will apply drag positions immediately and save the normalized values on release. Add a PerMonitorV2 application manifest and scale the 28-DIP native window size using the target monitor DPI.

## Concrete Steps

Work from `D:\01_Codebdbd\01_projects\aitebar`. Edit the files named above, then run:

    dotnet build .\AiteBar.sln -c Release
    dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release

If `dotnet test` encounters the known WPF temporary-project issue, run:

    dotnet vstest .\AiteBar.Tests\bin\Release\net10.0-windows\AiteBar.Tests.dll

## Validation and Acceptance

At 100%, 125%, and 150% display scale, start AiteBar and confirm the indicator is fully visible. Drag it to every edge and corner; it must stop before any part leaves the selected monitor. Restart AiteBar and confirm the relative location is preserved. Change scaling and restart; the indicator must remain at the same approximate visual location. A short click without dragging must still open the panel.

## Idempotence and Recovery

The new settings are nullable and additive, so existing JSON files remain valid. Invalid, missing, infinite, or out-of-range values are clamped by the helper. Removing the two JSON properties returns the indicator to its default taskbar-derived location.

## Artifacts and Notes

Validation evidence will be recorded after implementation.

    Build succeeded. 0 warnings, 0 errors.
    Passed: 779, Failed: 0, Skipped: 0.
    Installer: artifacts/installer/AiteBar-Setup.exe
    SHA-256: 82BFB28DA9188A41590ACC626FE8C7B844E240711ED0DE153784DCF6C583451B

## Interfaces and Dependencies

`IndicatorPositionHelper` will expose pure methods to clamp a physical top-left point, calculate a physical top-left point from normalized coordinates, and normalize a physical top-left point. No external package is required. `TaskbarPositionIndicatorWindow` will expose drag-move and drag-completed events containing a requested physical top-left point.

Change note: created this plan to cover the DPI-safe draggable-indicator implementation requested after the 125% scaling defect was isolated. Updated after implementation to record the z-order interaction, completed steps, and automated validation evidence.
