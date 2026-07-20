# Review the current tree and prepare the next AiteBar release

This ExecPlan is a living document. The sections `Progress`, `Surprises & Discoveries`, `Decision Log`, and `Outcomes & Retrospective` must be kept up to date as work proceeds. This document is maintained in accordance with `PLANS.md` at the repository root.

## Purpose / Big Picture

The goal is to turn the current working tree into a release-ready AiteBar revision. A maintainer should be able to inspect the code-review findings, see that release metadata and release notes describe the actual changes, build and test the application in Release configuration, and produce the Windows installer using the repository's standard script. No existing user work in the dirty working tree may be discarded.

## Progress

- [x] (2026-07-13 00:00Z) Read `PLANS.md`, inspected repository status, current version, tags, and release workflow.
- [x] (2026-07-13 13:50Z) Reviewed the current diff and surrounding settings, Clipboard Manager, QR Code, localization, documentation, and release code paths.
- [x] (2026-07-13 14:05Z) Built Release successfully with zero warnings and zero errors using the isolated `ReleaseVerificationRoot` because existing test `obj`/`bin` files reject generated-file replacement.
- [x] (2026-07-13 14:15Z) Fixed silent settings-value rounding, multi-monitor index loss, QR text-copy interception, and inaccurate network/privacy wording; added focused tests.
- [x] (2026-07-13 14:25Z) Ran 618 tests successfully from the freshly built Release assembly.
- [x] (2026-07-13 14:30Z) Chose version 1.11.0 and synchronized project, assembly, installer, security support table, and changelog metadata.
- [x] (2026-07-13 15:55Z) Ran the final isolated Release build with zero warnings/errors, passed all 618 tests, built the installer, and verified version, size, signature state, and SHA-256.
- [x] (2026-07-13 15:58Z) Started the published AiteBar executable, observed that it remained alive for eight seconds, and stopped the smoke-test process.
- [ ] Complete the manual WPF checklist for all four panel sides, contexts, hotkeys, tray access, and the changed settings/Clipboard/QR windows on representative monitor layouts.
- [x] (2026-07-13 16:00Z) Recorded final automated evidence, remaining manual checks, and the release review outcome.
- [ ] Rebuild and revalidate release artifacts after changing tray/About project-support links to `https://codebdbd.github.io/`.

## Surprises & Discoveries

- Observation: The repository is on `master` with many existing modified, deleted, and untracked files, while `v1.10.0` already exists and project metadata still says `1.10.0`.
  Evidence: `git status --short --branch`, `git tag --sort=-version:refname`, and `AiteBar/AiteBar.csproj`.

- Observation: The redesigned segmented controls map continuous defaults such as panel size 80 and activation delay 150 to nearby presets, which caused unrelated saves to rewrite values to 70 and 100.
  Evidence: `Models.cs` defaults, `SelectSegment`, and the original unconditional assignments in `BtnSave_Click`.

- Observation: Replacing the monitor ComboBox with a secondary-monitor checkbox collapsed every positive `MonitorIndex` to 1.
  Evidence: `Models.cs` documents indices `0, 1, 2...`, while the reviewed save expression was `checked ? 1 : 0`.

- Observation: A preview-level Ctrl+C handler in QR Code Generator intercepted text editing controls before their normal copy command.
  Evidence: `QRCodeGeneratorWindow.xaml` attaches `PreviewKeyDown`; the handler previously marked every Ctrl+C event handled.

- Observation: Standard test output directories have an environment-specific write restriction, but an isolated output build succeeds; source-aware tests must then run from the repository path.
  Evidence: ordinary build reported `UnauthorizedAccessException` under `AiteBar.Tests/obj` and `bin`; isolated build completed with zero warnings/errors, and the copied fresh assembly passed 618 tests from the standard repository path.

## Decision Log

- Decision: Treat every pre-existing working-tree change as user-owned and review it in place without resetting or restoring files.
  Rationale: The repository instructions require preserving unrelated or overlapping user work, and these changes appear to be the intended next-release scope.
  Date/Author: 2026-07-13 / Codex

- Decision: Defer the exact next version until the diff and changelog are understood, but require it to be newer than the existing `v1.10.0` tag.
  Rationale: Semantic-version impact depends on whether the current tree contains fixes only, backward-compatible features, or breaking changes.
  Date/Author: 2026-07-13 / Codex

- Decision: Release as 1.11.0.
  Rationale: The accumulated scope adds backward-compatible Quick Note formatting and substantial Clipboard Manager, QR Code, settings, panel, and documentation enhancements; semantic versioning therefore calls for a minor release.
  Date/Author: 2026-07-13 / Codex

- Decision: Preserve continuous settings values unless the user explicitly clicks a segmented preset, and preserve any existing positive monitor index while the secondary-monitor option remains enabled.
  Rationale: A compact UI redesign must not silently mutate valid existing configuration during an unrelated save.
  Date/Author: 2026-07-13 / Codex

## Outcomes & Retrospective

The review and automated release preparation are complete. Three release-risk regressions were corrected with focused tests, version 1.11.0 metadata and release notes are synchronized, the isolated Release build has zero warnings/errors, and all 618 tests pass. The standard installer script produced one 77,357,070-byte unsigned `AiteBar-Setup.exe` with ProductVersion 1.11.0 and SHA-256 `6F8D3141864F015AAE86C59F655DA7222E4D22108017DD7BF36797A62C940570`. The published executable remained alive during an eight-second startup smoke test. Only the explicitly manual WPF interaction checklist remains; it cannot be claimed from non-interactive automation.

## Context and Orientation

`AiteBar/AiteBar.csproj` and `AiteBar/AssemblyInfo.cs` contain the application version. `installer/AiteBar.iss` and `installer/Build-Installer.ps1` define installer metadata and create output under `artifacts/installer`. `.github/workflows/release.yml` validates that a tag named `vX.Y.Z` matches the project version, builds and tests the solution, produces exactly one installer, generates `SHA256SUMS.txt`, extracts release notes from `CHANGELOG.md`, and publishes a GitHub Release for tag builds.

The current working tree includes application, test, resource, and documentation changes centered on Clipboard Manager, QR Code, and settings. A code review means reading both the diff and the surrounding implementations, then reporting concrete defects by severity and fixing release-blocking issues because the user also requested release preparation. A release-ready state means metadata is synchronized, automated validation passes, the installer is generated successfully, and manual-only WPF behavior is explicitly listed for verification.

## Plan of Work

First, inspect the complete diff, recent commit history since `v1.10.0`, all release metadata, and the code paths touched by the current changes. Review data handling and UI behavior with special attention to Clipboard Manager privacy and limits, QR encoding and validation, localization completeness, WPF event lifetimes, and the compact fixed-height UI contract.

Second, run a Release build and tests to establish objective failures. For each confirmed defect, make the smallest project-style fix and add a focused unit or source-level integration test where practical. Keep this plan current after each stopping point, recording unexpected behavior and decisions.

Third, classify the reviewed scope under semantic versioning. Update `AiteBar/AiteBar.csproj`, `AiteBar/AssemblyInfo.cs`, `installer/AiteBar.iss` if it embeds a version, and `CHANGELOG.md` so the new section exists and accurately summarizes the release. Update other documentation only where it exposes the old current version or contradicts the shipped behavior.

Finally, run `dotnet build`, `dotnet test`, version-consistency checks, and `installer/Build-Installer.ps1`. Confirm there is exactly one non-empty installer in `artifacts/installer`, that its product version matches the application, and that a SHA-256 hash can be generated. WPF UI behavior that cannot be safely automated will be documented as a manual release checklist rather than claimed as verified.

## Concrete Steps

All commands run from `D:\01_Codebdbd\01_projects\aitebar` in PowerShell.

Inspect changes and history:

    git status --short --branch
    git diff --stat
    git diff --check
    git log --oneline v1.10.0..HEAD
    git diff -- AiteBar AiteBar.Tests installer .github CHANGELOG.md README.md docs

Validate application code:

    dotnet build .\AiteBar.sln -c Release
    dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release

If and only if the test command fails because generated WPF files under `wpftmp` or `obj` are temporarily inconsistent, run:

    dotnet vstest .\AiteBar.Tests\bin\Release\net10.0-windows\AiteBar.Tests.dll

Build the installer after metadata is synchronized:

    .\installer\Build-Installer.ps1

Then inspect `artifacts\installer` and compute SHA-256 with `Get-FileHash -Algorithm SHA256` for the single installer executable.

## Validation and Acceptance

Acceptance requires a clean exit from the Release build and the primary test command, with no failing tests. Focused tests covering any corrected non-UI defect must fail against the faulty behavior and pass after the fix. The project version, assembly version, changelog release heading, installer metadata, and proposed tag must agree. The installer script must leave exactly one non-empty `.exe` under `artifacts/installer`; its Windows product version must equal the chosen release version.

The final review must list findings in severity order with exact file and line references. If no unresolved blocking findings remain, it must say so explicitly. Manual acceptance must cover opening the changed Clipboard Manager, QR Code, SettingsWindow, and AppSettingsWindow interfaces; exercising localization; confirming Clipboard Manager capture, transform, copy-back, search, image handling, and clear-history behavior; and checking that compact windows fit without whole-window vertical scrolling. These steps are not considered executed unless the application is actually launched and observed.

## Idempotence and Recovery

Inspection, build, test, and hash commands are safe to repeat. `installer/Build-Installer.ps1` intentionally replaces publish output and expects one installer artifact; rerunning it is the supported recovery path after correcting a failure. Never use `git reset --hard`, `git checkout --`, or deletion to clean the user-owned working tree. If generated `obj` state causes the documented WPF failure, prefer the repository's `dotnet vstest` fallback and record the limitation instead of deleting source or user files.

## Artifacts and Notes

Initial evidence:

    branch: master...origin/master
    project version: 1.10.0
    newest tag: v1.10.0
    working tree: modified application/tests/docs, deleted old review plans, untracked policy/license files

Final automated evidence:

    Release build: succeeded, 0 warnings, 0 errors
    Tests: 618 passed, 0 failed, 0 skipped
    Version: project, assembly informational version, installer script, changelog, and installer ProductVersion are 1.11.0
    Installer: artifacts/installer/AiteBar-Setup.exe, 77,357,070 bytes, unsigned
    SHA-256: 6F8D3141864F015AAE86C59F655DA7222E4D22108017DD7BF36797A62C940570
    Startup smoke: process started and remained alive for 8 seconds

## Interfaces and Dependencies

No new production dependency is expected. Existing dependencies and lock files must remain unchanged unless a reviewed defect requires a deliberate update. Application code remains on `.NET 10` and `net10.0-windows`; tests use xUnit. Release generation continues through `installer/Build-Installer.ps1` and Inno Setup 6. Any helper added for testable logic should be internal to the `AiteBar` project and exposed to `AiteBar.Tests` through the existing `InternalsVisibleTo` declaration.

Plan revision note (2026-07-13): Created the self-contained release-review plan after reading `PLANS.md` and establishing the dirty-tree and version baseline.

Plan revision note (2026-07-13): Updated progress, discoveries, decisions, validation evidence, and the 1.11.0 outcome after completing code review, fixes, Release build, and the 618-test run.

Plan revision note (2026-07-13): Recorded successful installer generation, artifact/version/hash checks, startup smoke evidence, and the remaining manual WPF checklist.

Plan revision note (2026-07-13): Reopened automated validation because the project-support destination and About label changed before publication.
