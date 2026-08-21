# Harden Panel Settings, Geometry, and Links

This ExecPlan is a living document. The sections `Progress`, `Surprises & Discoveries`, `Decision Log`, and `Outcomes & Retrospective` must be kept up to date as work proceeds. It follows `PLANS.md` in the repository root.

## Purpose / Big Picture

After this work, a user can remove the final custom panel link without it returning after restart, create links to public, local, and intranet HTTP services consistently, and use the panel on monitors with different display scaling without incorrect size calculations. Settings consumers receive isolated snapshots rather than mutable service state. The behavior is demonstrated by focused unit tests and the normal Release build/test commands.

## Progress

- [x] (2026-08-21 12:00Z) Reviewed the panel settings, geometry, and custom-link paths and recorded four confirmed defects.
- [x] (2026-08-21 12:15Z) Added the persistence, URL, snapshot, and DPI fixes with focused regression tests.
- [x] (2026-08-21 12:35Z) Added a `File.Replace` fallback, stable monitor device names, safe unknown-action handling, and shared drop URL normalization with regression tests.
- [ ] Build Release and run the complete test suite (blocked by externally locked WPF intermediate files and failing `File.Replace` calls in the pre-existing test DLL).

## Surprises & Discoveries

- Observation: `WriteSettingsWithBackupAsync` intentionally returns before replacing settings when the element count changes from nonzero to zero.
  Evidence: `AiteBar/AppSettingsService.cs` compares old and new element counts at lines 613-626.
- Observation: the project already has monitor-handle enumeration through `TaskbarGeometryHelper.GetMonitorFromIndex`, but no target-monitor DPI helper.
  Evidence: `AiteBar/TaskbarGeometryHelper.cs` enumerates `EnumDisplayMonitors`.
- Observation: the normal Release build exits unsuccessfully without compiler diagnostics, and the pre-existing `vstest` DLL fails six persistence tests at `File.Replace` with `UnauthorizedAccessException` in temporary test directories.
  Evidence: 2026-08-21 validation output; the fallback DLL did not contain the newly added tests because the current source could not be rebuilt.
- Observation: a direct `%TEMP%` probe shows `File.Replace` fails with `UnauthorizedAccessException` while `File.Move(temp, target, overwrite: true)` succeeds.
  Evidence: 2026-08-21 `aitebar_replace_probe` and `aitebar_move_probe` commands.

## Decision Log

- Decision: Preserve atomic file replacement and backups while allowing an intentional empty element list.
  Rationale: the temporary-file write plus `File.Replace` protects against interrupted writes; rejecting empty data prevents a valid user action from persisting.
  Date/Author: 2026-08-21 / Codex
- Decision: Use `GetDpiForMonitor` when available and fall back to the current WPF DPI when Windows cannot supply it.
  Rationale: Windows 8.1+ provides monitor-specific DPI; fallback keeps the existing Windows 7-compatible behavior instead of failing placement.
  Date/Author: 2026-08-21 / Codex
- Decision: Return deep copies from the public `Elements` snapshot and mutate stored elements only through service methods.
  Rationale: callers must not bypass locking, change notification, or persistence.
  Date/Author: 2026-08-21 / Codex
- Decision: Persist a monitor device name alongside the legacy monitor index.
  Rationale: Windows display indices can change after docking or disconnecting a monitor; the device name identifies the intended display, while the index keeps old settings compatible.
  Date/Author: 2026-08-21 / Codex
- Decision: Only migrate the known legacy action type `Exe`; preserve unknown values and reject them at execution.
  Rationale: corrupt or future action types must not be converted into an executable shell target.
  Date/Author: 2026-08-21 / Codex

## Outcomes & Retrospective

The implementation removes the empty-list write rejection, adds a resilient `File.Replace` fallback, shares HTTP host validation for all custom links and drops, returns deep element snapshots, validates web URLs again at execution, migrates only known legacy action types, persists monitor device names, and uses target-monitor DPI when Windows exposes it. Full compilation remains blocked by environmental file locks; manual verification on mixed-DPI monitors remains required after a clean build.

## Context and Orientation

`AiteBar/AppSettingsService.cs` owns the in-memory `AppSettings` and custom `CustomElement` links, then writes `settings.json` through a temporary file and `File.Replace`. `AiteBar/SettingsWindow.xaml.cs` validates a user-created link through `AiteBar/ActionTargetHelper.cs`. `AiteBar/MainWindow.xaml.cs` converts physical monitor bounds from Windows Forms `Screen` objects to WPF device-independent pixels (DIPs), which are logical WPF coordinates. `TaskbarGeometryHelper` can obtain a Windows monitor handle by monitor index.

## Plan of Work

First, remove the early return that blocks a valid empty custom-link list and add a restart round-trip test for deleting the only element. Keep temporary-file cleanup and backup rotation unchanged.

Second, centralize acceptance of HTTP and HTTPS hosts in `ActionTargetHelper` using `Uri.CheckHostName`; this accepts DNS names, `localhost`, and IP addresses while still rejecting invalid hosts and non-web schemes. Update existing tests and add local/intranet cases.

Third, make `AppSettingsService.Elements` build deep `CustomElement` copies. Change the web action's rotation update to use `UpdateElementAsync` so it does not rely on a mutable snapshot.

Fourth, add a target-monitor DPI helper using the existing monitor handle lookup and `GetDpiForMonitor`. Main window calculations for available size and monitor work area will use that DPI. Add pure helper tests for DPI conversion and retain a fallback path for unavailable APIs.

## Concrete Steps

Work from `D:\01_Codebdbd\01_projects\aitebar`.

1. Edit the four implementation areas named above and add focused xUnit tests under `AiteBar.Tests`.
2. Run `dotnet build .\AiteBar.sln -c Release`.
3. Run `dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release`.
4. If WPF reports an externally locked `obj`/`wpftmp` file, run `dotnet vstest .\AiteBar.Tests\bin\Release\net10.0-windows\AiteBar.Tests.dll` and record the limitation.

## Validation and Acceptance

The new deletion test must create a settings file with one custom element, delete it, reload a new service, and observe zero elements. URL tests must accept `localhost`, an IP address, and a single-label intranet host while rejecting `ftp` and malformed inputs. Snapshot tests must prove mutating an obtained element cannot alter service state. DPI conversion tests must prove that a 150% target monitor converts pixels using `1.5`, not the source window DPI. Release build and tests must pass with no errors.

## Idempotence and Recovery

The code changes are safe to reapply. The settings writer keeps backups and writes through a unique temporary file; a failed write leaves the previous settings file intact. Tests use isolated temporary directories and clean them in `finally` blocks.

## Artifacts and Notes

The primary proof will be the focused test names for last-element persistence, link host normalization, element snapshot isolation, and target-monitor DPI conversion. Any WPF build lock is environmental and must be reported separately from code failures.

## Interfaces and Dependencies

`NativeMethods` will expose a safe internal monitor DPI call returning a scale factor. `TaskbarGeometryHelper` will provide target-monitor DPI conversion for the selected index. `ActionTargetHelper.TryNormalizeWebUrl` remains the single URL validation interface for custom panel links. `AppSettingsService.Elements` remains `IReadOnlyList<CustomElement>` but returns deep copies.

Revision note (2026-08-21): created to guide the reliability fixes identified by the panel core review.

Revision note (2026-08-21): recorded the completed implementation and the environmental build/test blockers.

Revision note (2026-08-21): added follow-up fixes from the repeat release audit, including persistence fallback and stable monitor identity.
