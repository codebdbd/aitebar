# AiteBar v1.7.5 Pre-Release Audit

**Audit date:** 2026-06-04
**Base commit:** `73c330a` on `master`
**Candidate version:** `1.7.5`
**Verdict:** **READY WITH CONDITIONS**

## Executive Summary

The release-blocking keyboard defect found during the audit has been fixed in the current working tree. A custom button now has a separate `ActivationHotkey` for its optional global shortcut, while `Ctrl`, `Alt`, `Shift`, `Win`, and `Key` remain the payload sent only by a Hotkey action. `HotkeyService` no longer registers a Hotkey action payload, so the previous self-trigger path is removed.

The same hardening change also makes `SendInput` failures visible to callers, releases injected modifiers during cleanup, includes registration failure reasons in warnings, immediately re-registers custom-button shortcuts after relevant changes, rejects oversized settings files, and corrects release-document drift.

Release build, tests, dependency vulnerability scan, publish, and installer creation passed. The remaining release condition is a manual keyboard matrix on a Windows desktop. It cannot be claimed as passed from this non-interactive audit environment.

## Finding Summary

| Severity | Open | Closed in candidate | Release blocking |
| --- | ---: | ---: | --- |
| CRITICAL | 0 | 0 | No |
| HIGH | 0 | 1 | No |
| MEDIUM | 2 | 5 | Conditional |
| LOW | 3 | 2 | No |

## Findings

| ID | Severity | Status | Area | Release blocking |
| --- | --- | --- | --- | --- |
| HIGH-1 | HIGH | Closed | Keyboard architecture | No |
| MED-1 | MEDIUM | Closed | Hotkey action reliability | No |
| MED-2 | MEDIUM | Closed | Hotkey action cleanup | No |
| MED-3 | MEDIUM | Closed | Registration diagnostics | No |
| MED-4 | MEDIUM | Open | Keyboard integration coverage | Conditional |
| MED-5 | MEDIUM | Closed | Settings-file reliability | No |
| MED-6 | MEDIUM | Open | Test isolation | No |
| LOW-1 | LOW | Open | `MainWindow` architecture | No |
| LOW-2 | LOW | Closed | Key catalog duplication | No |
| LOW-3 | LOW | Open | Installer signing | No |
| LOW-4 | LOW | Open | Dependency freshness | No |

### HIGH-1: Hotkey actions could trigger themselves

**Status:** Closed
**Impact:** A Hotkey button could register the same combination that it injected with `SendInput`, creating a repeated execution path.
**Previous reproduction:** Create a Hotkey action with `Ctrl+K`, save it as a custom-button shortcut, then click it or press `Ctrl+K`.

**Evidence of fix**

- `AiteBar/Models.cs:105` adds `CustomElement.ActivationHotkey`.
- `AiteBar/HotkeyService.cs:128-133` creates element definitions only from `ActivationHotkey`.
- `AiteBar/SettingsWindow.xaml.cs:644-665` validates the separate activation binding.
- `AiteBar/SettingsWindow.xaml.cs:751-759` stores Hotkey action payload and activation shortcut separately.
- `AiteBar/AppSettingsService.cs:350-386` migrates legacy non-Hotkey shortcuts without changing Hotkey action payloads.
- `AiteBar.Tests/HotkeyServiceTests.cs:220` proves a Hotkey action payload is not registered.
- `AiteBar.Tests/AppSettingsServiceTests.cs:494` and `:521` cover migration and payload preservation.

**Recommendation:** Keep the two persisted concepts separate in all future import, export, editor, and registration changes.

### MED-1: Hotkey action input failures were reported as success

**Status:** Closed
**Impact:** Callers could not distinguish successful input delivery from a short or failed `SendInput` call.

**Evidence of fix**

- `AiteBar/ActionService.cs:102` returns the Hotkey action result.
- `AiteBar/ActionService.cs:135-186` converts input failures into a failed `ActionExecutionResult`.
- `AiteBar.Tests/ActionServiceTests.cs:372` verifies failed input is reported.

**Recommendation:** Keep short `SendInput` results as action failures and preserve the regression test.

### MED-2: Injected modifiers were not guaranteed to be released

**Status:** Closed
**Impact:** An exception after a modifier key-down could leave an injected modifier logically pressed.

**Evidence of fix**

- `AiteBar/ActionService.cs:139-193` tracks injected keys and performs cleanup in `finally`.
- `AiteBar/ActionService.cs:206` releases modifiers in reverse order.
- `AiteBar.Tests/ActionServiceTests.cs:372-390` verifies cleanup after a failed main-key send.

**Recommendation:** Continue tracking only keys injected by AiteBar and release them in `finally`.

### MED-3: Registration warnings discarded failure reasons

**Status:** Closed
**Impact:** Users could not tell whether a shortcut was unsupported, reserved, conflicting, or rejected by Windows.

**Evidence of fix**

- `AiteBar/HotkeyService.cs:207-212` includes `FailureReason` in displayed failures.
- `AiteBar/SettingsWindow.xaml.cs:763` shows failures immediately after saving a button.
- `AiteBar/MainWindow.xaml.cs:1101` reports failures after panel import.

**Recommendation:** Preserve actionable failure reasons in every future hotkey-editing workflow.

### MED-4: Critical keyboard integration paths lack automated coverage

**Status:** Open
**Release blocking:** Conditional until the manual matrix passes.

**Description and impact**

Service-level tests cover registration, mapping, conflicts, migration, and input cleanup, but WPF focus behavior and end-to-end `WM_HOTKEY` dispatch remain tied to `MainWindow`. Regressions in Tab order, arrow navigation, Enter, Escape, owned-window filtering, or panel orientation can pass CI.

**Evidence**

- `AiteBar/MainWindow.xaml.cs:822-870` owns registration and `WM_HOTKEY` dispatch.
- `AiteBar/MainWindow.xaml.cs:1982-2134` owns keyboard focus and navigation.
- No UI automation project exists in the solution.

**Recommendation**

Run the manual keyboard matrix before release. Longer term, extract navigation and dispatch decisions into testable helpers and add Windows UI automation.

### MED-5: Settings file-size limit was not enforced

**Status:** Closed
**Impact:** A corrupted or maliciously large settings file could cause excessive startup memory use.

**Evidence of fix**

- `AiteBar/AppSettingsService.cs:54`, `:74`, and `:171` check primary, legacy, and backup files before loading.
- `AiteBar/AppSettingsService.cs:288-294` rejects files larger than the configured limit.
- `AiteBar.Tests/AppSettingsServiceTests.cs:134` verifies oversized settings fall back to defaults.

**Recommendation:** Keep size checks before every settings-file read and retain the regression test.

### MED-6: Some tests share real user AppData

**Status:** Open
**Release blocking:** No

**Description and impact**

The first standard `dotnet test` run failed one `LoggerTests` case because the installed `AiteBar.exe` briefly held the real `%APPDATA%\Codebdbd\Aite Bar\error.log`. A retry passed `411/411`. Other tests also use real browser-profile and AppData locations, which makes local results sensitive to running applications and permissions.

**Evidence**

- `AiteBar.Tests/LoggerTests.cs:77-83` manipulates the application log artifact.
- `AiteBar.Tests/BrowserHelperTests.cs:143`, `:238`, and `:266` create browser profile directories.
- `AiteBar.Tests/TelemetryServiceTests.cs:222-250` manipulates the settings file.

**Recommendation**

Inject path providers or isolate `APPDATA` and `LOCALAPPDATA` per test process. Treat persistent CI failures as release blocking.

### LOW-1: `MainWindow` remains an oversized system coordinator

**Status:** Open
**Evidence:** `AiteBar/MainWindow.xaml.cs` contains 2,544 lines and owns hotkeys, dispatch, focus, navigation, animation, tray behavior, contexts, drag-and-drop, and execution wiring.
**Impact:** Keyboard changes have a broad regression surface and are difficult to test without WPF.
**Recommendation:** Move hotkey orchestration and navigation decisions into focused collaborators while keeping WPF event wiring in `MainWindow`.

### LOW-2: Key catalogs were duplicated across settings UIs

**Status:** Closed
**Evidence:** `AiteBar/HotkeyKeyCatalog.cs` centralizes global-shortcut and Hotkey-action key options; both settings windows consume it.
**Recommendation:** Keep Win32 mapping and validation tests synchronized with this catalog.

### LOW-3: Installer is not signed

**Status:** Open
**Evidence:** `artifacts/installer/AiteBar-Setup.exe` has signature status `NotSigned`; installer build reports that signing was skipped.
**Impact:** Windows SmartScreen and reputation warnings may reduce user trust.
**Recommendation:** Require a valid signature in release CI when a code-signing certificate is available.

### LOW-4: Some dependencies have newer versions

**Status:** Open
**Evidence:** `dotnet list .\AiteBar.sln package --outdated --include-transitive` reports `Sentry 6.5.0 -> 6.6.0` and newer transitive test dependencies.
**Impact:** No current security issue was found, but maintenance drift will grow.
**Recommendation:** Review updates after v1.7.5 rather than combining them with keyboard hardening.

## Keyboard Subsystem

### Model and storage

- `AppSettings` stores application command shortcuts as `HotkeyBinding`.
- `CustomElement.ActivationHotkey` stores the optional global shortcut that launches a custom button.
- `CustomElement.Ctrl`, `Alt`, `Shift`, `Win`, and `Key` store only the payload sent by a Hotkey action.
- Legacy non-Hotkey element bindings migrate to `ActivationHotkey`; legacy Hotkey actions preserve their payload.
- Panel package import and export preserve the separate activation binding.

### Settings UI and validation

- `HotkeyKeyCatalog` supplies key options by purpose.
- Application command hotkeys and button activation hotkeys require a modifier.
- Reserved Windows combinations are rejected before saving.
- Runtime registration still detects command-command, command-element, element-element, unsupported-key, and Windows registration failures.
- Button save and panel import show registration failures with reasons.

### Registration and dispatch

- `HotkeyService` allocates command IDs separately from dynamic custom-element IDs.
- Commands register before elements and therefore retain priority.
- Re-registration occurs after button save, delete, duplicate, and panel import.
- `MainWindow.WndProc` dispatches `WM_HOTKEY`; owned-window filtering remains a manual integration concern.

### Panel navigation

- `MainWindow` handles keyboard mode, focus, Tab/Shift+Tab, arrows, Enter, and Escape.
- Orientation-aware behavior exists for Top, Bottom, Left, and Right edges.
- These paths require manual verification because they are not covered by UI automation.

### Input sending

- `ActionService` sends Hotkey action payloads through `SendInput`.
- Already pressed modifiers are not injected again.
- Successfully injected modifiers are released in reverse order.
- Short `SendInput` results fail the action and trigger cleanup.
- Fullscreen `F11` input now also fails when `SendInput` is incomplete.
- Normal Win32 limitations remain: foreground-window rules, UIPI/integrity boundaries, reserved shortcuts, and behavior owned by the target application.

## Architecture And Duplication

The principal keyboard architecture defect is closed by separating activation from action payload. The remaining architectural risk is concentration of system behavior in `MainWindow`. Centralized key descriptors reduce UI duplication, but Win32 key mapping, validation rules, and documentation must still be kept aligned through tests and review.

## Security

- NuGet vulnerability scan found no vulnerable direct or transitive packages.
- Oversized settings files are rejected before `ReadAllText`.
- Panel package import retains path, size, entry-count, and manifest validation.
- Script and command execution still require explicit confirmation.
- Installer signing remains unavailable in the current local build.

## Reliability And Performance

No independent performance blocker was confirmed. The previous repeated hotkey self-trigger risk is removed. Settings-size enforcement reduces startup memory and latency risk from malformed files. There is no benchmark suite or startup-time measurement, so performance conclusions remain static-analysis based.

## Tests And Coverage

The candidate has **411 tests**. New regression tests cover:

- Hotkey action payloads not being globally registered.
- Legacy non-Hotkey shortcut migration.
- Hotkey action payload preservation.
- Panel package mapping of activation shortcuts.
- `SendInput` failure reporting and modifier cleanup.
- Oversized settings rejection.

The current documented coverage baseline remains `34.62%` line and `27.36%` branch from `docs/COVERAGE-REPORT-2026-06-02.md`. It was not re-collected during this hardening run. CI enforces only a `19%` line threshold, so WPF keyboard integration remains weakly protected.

## CI/CD And Packaging

- Version `1.7.5` is synchronized across `AiteBar.csproj`, `AssemblyInfo.cs`, installer configuration, and `CHANGELOG.md`.
- Both projects contain lock files.
- Build CI restores in locked mode, builds, tests, collects coverage, and uploads artifacts.
- CodeQL runs on pushes, pull requests, and a schedule.
- Release CI validates the version, builds, tests, creates the installer, verifies installer version, creates checksums, and publishes artifacts.
- The local installer was rebuilt at `artifacts/installer/AiteBar-Setup.exe`, ProductVersion `1.7.5`.

## Documentation Drift

The following drift was corrected:

- `docs/release-audit.md` no longer describes v1.6.1 or the pre-fix hotkey model.
- `docs/technical-reference.md` no longer lists absent `Context1Hotkey`-`Context4Hotkey` fields.
- `CHANGELOG.md` v1.7.5 Markdown formatting is corrected.
- `docs/RELEASE-2026-SUMMARY.md` and `docs/RELEASE-HARDENING-CHANGE-PLAN.md` now exist.
- User, function, architecture, and technical-reference documents distinguish button activation shortcuts from Hotkey action payloads.

## Confirmed Checks

| Command or check | Result |
| --- | --- |
| `dotnet build .\AiteBar.sln -c Release` | Passed, 0 warnings, 0 errors |
| `dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release` | Passed, 411 / 411; an earlier run had one transient real-AppData log lock |
| `dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release --no-build` | Passed, 411 / 411 |
| `dotnet vstest .\AiteBar.Tests\bin\Release\net8.0-windows\AiteBar.Tests.dll` | Passed, 411 / 411 |
| `dotnet list .\AiteBar.sln package --vulnerable --include-transitive` | No vulnerable packages |
| `dotnet list .\AiteBar.sln package --outdated --include-transitive` | Newer non-security updates available |
| `.\installer\Build-Installer.ps1` | Passed; publish and installer created |
| Installer version | `1.7.5` |
| Installer signature | `NotSigned` |

## Manual Keyboard Matrix

The following scenarios are **not verified** by this audit environment and must be completed before release:

- Show panel hotkey from hidden state and repeated press while visible.
- Tab and Shift+Tab focus cycling.
- Arrow navigation, Enter, and Escape on Top, Bottom, Left, and Right edges.
- Short and long contexts, including context switching.
- Command-command, command-element, and element-element conflicts.
- Windows registration rejection and reserved combinations.
- Custom-button global shortcut for non-Hotkey and Hotkey actions.
- Hotkey action input delivery to another foreground application.
- Owned-window filtering for Quick Note, Timer/Stopwatch, and other windows.
- Post-fix confirmation that a Hotkey action cannot trigger itself.

## Remediation Plan

### Before release

1. Complete and record the manual keyboard matrix.
2. Run the release workflow dry-run from the final commit.
3. Verify installer checksum and expected unsigned status, or sign the installer if certificate infrastructure is available.

### Immediately after release

1. Isolate tests from real AppData and browser profile directories.
2. Add automated coverage for WPF focus, `WM_HOTKEY` dispatch, and owned-window filtering.
3. Review the available Sentry and test dependency updates.

### Longer-term technical debt

1. Extract hotkey orchestration and keyboard navigation decisions from `MainWindow`.
2. Raise the CI coverage threshold as non-UI keyboard behavior becomes testable.
3. Make installer signing mandatory when certificate infrastructure is available.

## Final Verdict

**READY WITH CONDITIONS**

No confirmed code-level release blocker remains in the current candidate. AiteBar v1.7.5 can proceed to release only after the manual keyboard matrix passes and the final release workflow dry-run succeeds.
