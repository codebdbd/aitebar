# Prepare Prompt Builder for release

This ExecPlan is a living document. The sections `Progress`, `Surprises & Discoveries`, `Decision Log`, and `Outcomes & Retrospective` must be kept up to date as work proceeds. It follows `PLANS.md` from the repository root.

## Purpose / Big Picture

This work makes the Prompt Builder releasable. A user should receive the same complete filter catalog in Russian, Ukrainian, German, and English, and the release build must pass its automated checks before an installer is considered ready.

## Progress

- [x] (2026-08-09 12:00Z) Reviewed the Prompt Builder source, tests, settings persistence, and installer state.
- [x] (2026-08-09 12:00Z) Synchronized Ukrainian and German resource keys with the active Photo and Code catalogs.
- [x] (2026-08-09 12:00Z) Ran the resource-parity and Prompt Builder test groups: 104 tests passed.
- [x] (2026-08-09 12:00Z) Built and verified a new installer.
- [x] (2026-08-09 12:00Z) Replaced synchronous dispatcher marshaling during culture refresh with asynchronous dispatch.
- [x] (2026-08-09 12:00Z) Ran the full Release test suite: 1241 tests passed.
- [x] (2026-08-09 12:00Z) Rebuilt the installer from the final verified source tree and verified its SHA-256 manifest.

## Surprises & Discoveries

- Observation: the application compiles, but `LocalizationServiceTests.ResourceFiles_HaveSameKeysAndFormatPlaceholders` fails.
  Evidence: Ukrainian and German resources lack the expanded Photo section/style and Programming type/style keys present in the neutral and Russian resources.
- Observation: a full unfiltered test run hangs in the test runner after starting, even after stale runner processes are removed.
  Evidence: two separate `dotnet test --no-build` runs exceeded the 120-second execution limit; the focused resource and Prompt Builder group completed successfully.
- Resolution: a window subscribed to `LocalizationService.CultureChanged` could synchronously wait on a non-pumping WPF dispatcher when the event was raised by a test thread.
  Evidence: the run stalled at `LocalizationServiceTests.ApplyCulture_SameCulture_DoesNotRaiseCultureChangedTwice`; replacing `Dispatcher.Invoke` with `Dispatcher.BeginInvoke` in `DarkWindow` and `MainWindow` made that test and the complete 1241-test suite finish.

## Decision Log

- Decision: treat localization key parity as a release blocker rather than weakening the test.
  Rationale: a missing key degrades a visible filter label for users of that language; hiding this mismatch would ship a broken catalog.
  Date/Author: 2026-08-09 / Codex
- Decision: queue culture-refresh work onto a foreign window dispatcher instead of blocking the calling thread.
  Rationale: localization refresh is UI work and need not synchronously complete before `ApplyCulture` returns; asynchronous dispatch avoids a deadlock when the dispatcher is not pumping.
  Date/Author: 2026-08-09 / Codex

## Outcomes & Retrospective

The active Prompt Builder lists, settings, and localized labels are release-ready. The Release build has no warnings, the complete automated suite passes (`1241/1241`), and the final installer plus SHA-256 manifest were produced from that source tree.

## Context and Orientation

`AiteBar/PromptBuilderService.cs` contains the active lists. `AiteBar/Resources/Strings.resx` is the neutral source of resource keys, while `Strings.ru.resx`, `Strings.uk.resx`, and `Strings.de.resx` must each contain the same keys. `AiteBar.Tests/LocalizationServiceTests.cs` enforces the parity rule. `installer/Build-Installer.ps1` publishes and packages the Windows installer.

## Plan of Work

Add translations for every missing active Photo and Programming key to `Strings.uk.resx` and `Strings.de.resx`; do not replace them with English fallbacks. Keep values in the older resource files that are no longer selected by active lists, because removing old keys is not necessary for correctness and can make upgrades harder to diagnose.

Run the full Release build and test suite. If a previous `testhost` process locks the test DLL, verify its executable path, terminate only that process, and retry. Once the suite passes, run `installer/Build-Installer.ps1` and verify `artifacts/installer/AiteBar-Setup.exe` and `SHA256SUMS.txt` exist.

## Concrete Steps

From `D:\01_Codebdbd\01_projects\aitebar`, run:

    dotnet build .\AiteBar.sln -c Release
    dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release --no-build
    .\installer\Build-Installer.ps1

## Validation and Acceptance

All resource cultures have identical keys and format placeholders. The full test suite passes. The installer directory contains one current `AiteBar-Setup.exe` and a SHA-256 manifest.

## Idempotence and Recovery

Resource additions are additive and safe to rerun only once because resource keys must remain unique. Builds and tests can be repeated. If a test host holds a DLL, stop only the verified process and retry without deleting workspace content.

## Artifacts and Notes

The installer is unsigned unless the build receives a code-signing certificate through the existing script options.

## Interfaces and Dependencies

No new runtime dependencies are required. Localization uses standard .NET `.resx` files; every localized file must provide the keys in the neutral resource file.

Revision 2026-08-09: localization parity fixed; removed the culture-change dispatcher deadlock; passed the complete test suite; rebuilt and verified the final installer.
