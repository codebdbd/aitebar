# Audit every built-in utility and prepare AiteBar 1.13.0

This ExecPlan is a living document. The sections `Progress`, `Surprises & Discoveries`, `Decision Log`, and `Outcomes & Retrospective` must be kept up to date as work proceeds. This document must be maintained in accordance with `PLANS.md` at the repository root.

## Purpose / Big Picture

This work prepares a release candidate by reviewing every built-in utility in the exact order exposed by `UtilityButtonCatalog.All`, fixing release-blocking defects, proving existing user data and settings remain safe, and producing synchronized version metadata, release notes, tests, publish output, checksums, and installer. A user should be able to install version 1.13.0 and launch each visible utility or system action without a crash, missing localization, broken primary workflow, or regression in the recent File Sorter and Text Processing changes.

## Progress

- [x] (2026-08-02 01:25Z) Captured the dirty working tree, current version 1.12.2, release pipeline, test inventory, and the authoritative 18-item utility catalog.
- [x] (2026-08-02 01:35Z) Reviewed system actions 1–6: Search, Screenshot, Record, Calculator, Explorer, and Downloads; fixed silent launch failures and added regression tests.
- [x] (2026-08-02 02:02Z) Reviewed window utilities 7–12: File Sorter, Icon Converter, Timer/Stopwatch, Color Picker, Quick Note, and QR Code Generator; fixed Color Picker localization and contained Timer close-time save failures.
- [x] (2026-08-02 02:12Z) Reviewed utilities and system actions 13–18: Clipboard Manager, Show Desktop, Apps Folder, Copilot, Text Processing, and Zen Editor; fixed silent shell launch failures and added Copilot input-release coverage.
- [x] (2026-08-02 02:12Z) Fixed every confirmed release-blocking defect and added focused regression tests where logic could be isolated.
- [x] (2026-08-02 02:19Z) Synchronized version 1.13.0 in project, assembly, installer fallback, changelog, security support table, and release workflow.
- [x] (2026-08-02 02:27Z) Ran release-equivalent isolated WPF and non-WPF tests, built and published, built the installer, verified version/checksum, and smoke-started the published executable.

## Surprises & Discoveries

- Observation: The working tree contains a coherent but uncommitted release candidate spanning File Sorter, AI routing, Text Processing stability, professional prompts, and Literary Editing.
  Evidence: `git status --short` reports modified source, tests, resources, and documentation plus new helper, policy, and ExecPlan files. These edits belong to the user and must be preserved as one release scope rather than reset or partially discarded.

- Observation: The built-in catalog has 18 entries, but only 9 are `IUtility` window implementations.
  Evidence: `UtilityButtonCatalog.All` contains Search, Screenshot, Record, Calculator, Explorer, Downloads, FileSorter, IconConverter, TimerStopwatch, ColorPicker, QuickNote, QRCodeGenerator, ClipboardManager, ShowDesktop, AppsFolder, Copilot, TextProcessing, and ZenEditor. The system actions are executed directly by `MainWindow` or `ActionService`; the remaining entries create managed utility windows.

- Observation: The current version is 1.12.2 and `CHANGELOG.md` has an empty Unreleased section.
  Evidence: `AiteBar/AiteBar.csproj`, `AiteBar/AssemblyInfo.cs`, and `installer/AiteBar.iss` all contain 1.12.2. The accumulated work adds a fourth Text Processing mode and materially redesigns File Sorter, which qualifies as a minor release under the repository's stated semantic-versioning policy.

- Observation: The release workflow did not isolate the new WPF `FileSorterWindowBehaviorTests`, so it could run in the shared non-WPF test host and recreate the file-lock/hang failure seen locally.
  Evidence: The class declares `WpfTestCollection`, but both class arrays in `.github/workflows/release.yml` omitted it. The workflow now includes it in both arrays and all ten WPF classes pass in separate hosts.

- Observation: Shell protocol actions consistently need a null process-handle check even when Windows does not throw.
  Evidence: The fake runtime reproduced null launches for Screenshot, Record, Calculator, Explorer, Downloads, Show Desktop, and Apps Folder. All now raise the localized `Action_LaunchFailed` path and the theory covers all eight launch variants including Search.

## Decision Log

- Decision: Prepare version 1.13.0.
  Rationale: The release adds user-visible capabilities rather than only backward-compatible bug fixes: File Sorter becomes a one-screen multi-folder workflow and Text Processing gains Literary Editing. Existing settings and public behavior remain backward compatible, so a minor rather than major version is appropriate.
  Date/Author: 2026-08-02 / Codex

- Decision: Review utilities in `UtilityButtonCatalog.All` order and record evidence per item.
  Rationale: The catalog is the only authoritative UI order and includes both window utilities and direct system actions. Following it prevents visually less prominent utilities from being skipped.
  Date/Author: 2026-08-02 / Codex

- Decision: Treat crashes, data loss, destructive ambiguity, broken launch paths, missing localized UI, incorrect state persistence, unusable fixed layout, and failing primary workflows as release blockers.
  Rationale: Cosmetic preferences and speculative refactors should not destabilize a release candidate. Fixes must be evidence-driven and proportional to user risk.
  Date/Author: 2026-08-02 / Codex

## Outcomes & Retrospective

The audit is complete. All 18 catalog entries have an itemized result and no known release blocker remains. After the final panel accessibility and hover-setting fixes, AiteBar 1.13.0 builds with zero warnings and errors; 1071 non-WPF and 73 isolated WPF tests pass (1144 total). The published executable remained alive during the eight-second smoke interval. `artifacts/installer/AiteBar-Setup.exe` is 79,456,114 bytes, reports ProductVersion 1.13.0, and matches `SHA256SUMS.txt` at `FABECE024C254FB7F6BB9CB9BA601385C2DB7856D9FEFAD7C3767D0AC41A9CD8`. The local artifact is unsigned, as expected because no signing certificate was supplied.

## Context and Orientation

`AiteBar/UtilityButtonCatalog.cs` defines the 18 built-in buttons and their display order. `AiteBar/UtilityRegistry.cs` manages reusable window utility instances. `AiteBar/MainWindow.xaml.cs` and `AiteBar/ActionService.cs` execute direct system actions. Each full utility has a `*Utility.cs` launcher, a `*Window.xaml`/code-behind pair when it has UI, and usually a service or helper that holds testable logic.

The nine managed window utilities are FileSorter, IconConverter, TimerStopwatch, QuickNote, QRCodeGenerator, ClipboardManager, TextProcessing, and ZenEditor; ColorPicker is a specialized `IUtility` that owns its picker lifecycle without deriving from `UtilityBase<TWindow>`. Search, Screenshot, Record, Calculator, Explorer, Downloads, ShowDesktop, AppsFolder, and Copilot are direct system integrations.

Release metadata lives in `AiteBar/AiteBar.csproj`, `AiteBar/AssemblyInfo.cs`, and the fallback constant in `installer/AiteBar.iss`. `installer/Build-Installer.ps1` reads the project version and passes it to Inno Setup. `CHANGELOG.md` supplies GitHub release notes, and `.github/workflows/release.yml` requires a changelog section matching the tag or requested release version.

## Plan of Work

For every direct system action, inspect argument construction, executable or URI validation, exception handling, and tests. For every window utility, inspect the launcher lifecycle, close/minimize restoration, cancellation and asynchronous exception paths, persistence boundaries, destructive operations, localization keys, accessibility-critical names, fixed geometry, and focused tests. Run each utility's existing focused tests immediately after its review; add a regression test before or with any confirmed fix.

Review in catalog order. Record each item as Passed, Fixed, or Blocked in `Artifacts and Notes`, with the source files and test evidence. Do not rewrite stable utilities merely for stylistic consistency. Preserve user files, settings, clipboard history privacy behavior, API credentials, and the dirty working tree.

After all items pass, change the version to 1.13.0 in all synchronized sources and replace the empty Unreleased section with bilingual Added, Changed, and Fixed entries covering the actual release scope. Run `ReleaseVersionTests` to prove metadata alignment. Execute the release workflow's split test strategy locally: one non-WPF batch and each WPF class in an isolated host. Then publish, build the installer, verify its ProductVersion and SHA-256, and start the published executable briefly to prove it initializes without immediate termination.

## Concrete Steps

Run commands from `D:\01_Codebdbd\01_projects\aitebar`.

    dotnet build .\AiteBar.sln -c Release
    dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release --no-build
    .\installer\Build-Installer.ps1

For release-equivalent tests, copy the class lists from `.github/workflows/release.yml`: exclude all WPF classes in one non-WPF run, then run every excluded WPF class separately. The exact command transcript and counts must be recorded after completion.

## Validation and Acceptance

Each of the 18 catalog entries must have a recorded review result. Window utilities must construct under WPF tests, fit their current minimum layout, and preserve documented minimize/close behavior. Destructive actions such as File Sorter undo and Clipboard Manager deletion must remain explicit and scoped. AI Text Processing must retain Proofread startup, four independent modes, task-specific model routing, empty-stream fallback, and content-preservation checks. Quick Note and Zen Editor must retain their documented persistence and recovery guarantees.

Release acceptance requires synchronized 1.13.0 metadata, a matching changelog section, zero build warnings/errors, zero failed tests, one non-empty installer artifact whose ProductVersion is 1.13.0, an updated `SHA256SUMS.txt`, and a published AiteBar process that remains alive during the smoke interval.

## Idempotence and Recovery

Reviews and tests are read-only. Source fixes use `apply_patch` and preserve unrelated changes. Build and installer commands can be rerun. The installer script safely replaces publish output and its own temporary files; it must not be run concurrently with another publish. Smoke testing may stop only the executable started from `artifacts/publish/win-x64`, never an installed user instance unless explicitly authorized.

## Artifacts and Notes

Audit ledger, updated sequentially:

    1. Search — Passed: empty input is ignored, query is URI-escaped, Chrome/Edge/default-browser fallback is explicit, and null launch already raises localized Action_SearchFailed.
    2. Screenshot — Fixed: null Windows protocol launch now raises localized Action_LaunchFailed.
    3. Record — Fixed: null Windows protocol launch now raises localized Action_LaunchFailed.
    4. Calculator — Fixed: null process launch now raises localized Action_LaunchFailed.
    5. Explorer — Fixed: null shell launch now raises localized Action_LaunchFailed.
    6. Downloads — Fixed: null shell launch now raises localized Action_LaunchFailed.
    7. File Sorter — Passed: one-screen folder rows, scoped undo state, busy-state close guard, persistent custom folders, localized errors, and focused service/layout/behavior tests reviewed; its WPF behavior tests are now isolated in release CI.
    8. Icon Converter — Passed: stale preview cancellation, guarded conversion/save, overwrite confirmation, and service/integration/layout tests reviewed.
    9. Timer/Stopwatch — Fixed: close-time settings persistence can no longer escape an async-void close handler; compact/full behavior and formatting/layout tests pass.
    10. Color Picker — Fixed: crash fallback now uses Utility_Unavailable localization and owns its dialog.
    11. Quick Note — Passed: final-save close guard, pinned deactivation behavior, Markdown round-trip, links, conflicts, layout, and WPF formatting/close tests reviewed.
    12. QR Code Generator — Passed: validation, cancellation, copy/save exception paths, lifecycle cleanup, shortcuts, and PNG/SVG service tests reviewed.
    13. Clipboard Manager — Passed: runtime subscription cleanup, explicit destructive confirmation, copy suppression, 50-entry/10-KiB limits, optional persistence behavior, integration, and isolated WPF behavior test reviewed.
    14. Show Desktop — Fixed: null Windows shell launch now raises localized Action_LaunchFailed.
    15. Apps Folder — Fixed: null Windows shell launch now raises localized Action_LaunchFailed.
    16. Copilot — Passed: Win+C injection order and unconditional Win-key release now have success and failure regression tests.
    17. Text Processing — Passed: Proofread startup, four independent prompts, task-specific model eligibility, original-input retry, empty-stream fallback, language/content protection, and focused service/UI/model tests reviewed.
    18. Zen Editor — Passed: close-time save guard, autosave, snapshots/backups, recovery, undo history, export, minimize/restore lifecycle, and isolated WPF behavior tests reviewed.

## Interfaces and Dependencies

No new dependency is planned. Release fixes should use existing services and helper patterns. Version sources must end as `Version=1.13.0`, `AssemblyVersion=1.13.0.0`, `FileVersion=1.13.0.0`, matching assembly attributes, and Inno fallback `AppVersion "1.13.0"`. The output installer remains `artifacts/installer/AiteBar-Setup.exe`.

Plan revision note (2026-08-02 01:25Z): Created the release-wide audit plan after inventorying the authoritative utility catalog, dirty working tree, current 1.12.2 metadata, tests, installer script, and GitHub release workflow.

Plan revision note (2026-08-02 02:27Z): Completed the 18-item ledger, recorded confirmed fixes and the WPF workflow discovery, synchronized 1.13.0, and added final build, test, installer, checksum, and smoke evidence.

Plan revision note (2026-08-02 03:47Z): Refreshed final release evidence after adding the context tooltip, keyboard focus outline, and optional mouse-hover activation setting.
