# ExecPlan: довести release quality AiteBar до практик 2026

This ExecPlan is a living document. The sections `Progress`, `Surprises & Discoveries`, `Decision Log`, and `Outcomes & Retrospective` must be kept up to date as work proceeds.

This plan follows `PLANS.md` in the repository root. It is self-contained so a new contributor can continue from this file alone.

## Purpose / Big Picture

AiteBar already has a Windows desktop application, unit tests, a publish script, and an Inno Setup installer, but the release pipeline is still mostly manual. After this work, every push and pull request can be built and tested by GitHub Actions, every version tag can produce a GitHub Release with the installer attached, production crashes can be sent to Sentry when a DSN is configured, and users can check from inside the app whether a newer GitHub Release exists.

The user-visible behavior is concrete: the tray menu and About window gain a "Check for updates" action. If GitHub has a newer `vX.Y.Z` release than the installed app version, AiteBar shows the version and opens the release page where the installer is attached. If the app is already current, it says so. Crash reporting is opt-in by environment variable, so local builds do not send telemetry unless the operator configures it.

## Progress

- [x] (2026-05-27 16:34 Europe/Kiev) Read `PLANS.md`, the existing release plan, project files, installer script, App startup, ActionService, About window, localization, and tray menu.
- [x] (2026-05-27 16:36 Europe/Kiev) Verified current package versions from NuGet search: `Sentry` 6.5.0 and `NetSparkleUpdater.UI.WPF` 3.1.0 were current on 2026-05-27.
- [x] (2026-05-27 16:38 Europe/Kiev) Chose GitHub Releases based update checking instead of a full in-process installer updater for the first production-safe increment.
- [x] (2026-05-27 16:49 Europe/Kiev) Added deterministic/package-lock build settings and GitHub Actions workflows for build, test, coverage artifact, and tag release.
- [x] (2026-05-27 16:51 Europe/Kiev) Added Sentry SDK integration behind environment configuration and capture unhandled exceptions plus action failures.
- [x] (2026-05-27 16:53 Europe/Kiev) Added GitHub Releases update checking service with unit tests, About window button, and tray menu entry.
- [x] (2026-05-27 16:54 Europe/Kiev) Updated README release documentation and third-party notices.
- [x] (2026-05-27 16:58 Europe/Kiev) Ran `dotnet restore`, `dotnet build .\AiteBar.sln -c Release`, and `dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release`.

## Surprises & Discoveries

- Observation: The checked-in plan suggested `Sentry` 4.7.0, but NuGet search on 2026-05-27 showed `Sentry` 6.5.0 as current.
  Evidence: Search result from NuGet Gallery reported `Sentry 6.5.0`, published 2026-05-05.
- Observation: The installed local SDK is .NET `8.0.421`, so the implementation keeps `net8.0-windows` and pins that SDK family in `global.json` instead of moving the project to .NET 10 during the same change.
  Evidence: `dotnet --version` returned `8.0.421`.
- Observation: The repository remote is `https://github.com/codebdbd/aitebar.git`, which gives a stable update source for GitHub Releases.
  Evidence: `git remote -v` returned the `codebdbd/aitebar` origin.
- Observation: Local build outputs were locked by stale `MSBuild.dll` and `VBCSCompiler.dll` dotnet worker processes, causing `Access to the path ... GlobalUsings.g.cs is denied`.
  Evidence: After stopping only those worker processes, `dotnet build .\AiteBar.sln -c Release` completed with 0 warnings and 0 errors.
- Observation: Sentry .NET SDK 6.5.0 no longer exposes `SentrySdk.WithScope`.
  Evidence: The first build reported `CS0117: "SentrySdk" does not contain a definition for "WithScope"`; package XML showed `SentrySdk.CaptureException(Exception, Action<Scope>)`, and switching to that API fixed compilation.

## Decision Log

- Decision: Keep the app on .NET 8 for this change and add build automation around the current target framework.
  Rationale: .NET 8 is installed locally and still compatible with the existing WPF project. A .NET 10 migration can be a separate, verifiable milestone because it changes developer and CI prerequisites.
  Date/Author: 2026-05-27 / Codex.

- Decision: Use GitHub Releases as the update source and implement a check-and-open flow instead of silent self-updating.
  Rationale: The repository already publishes an Inno Setup installer, and a safe self-update path needs code signing, installer handoff, and rollback behavior. A built-in release check gives users a visible update mechanism now and leaves room to add signed automatic installation later.
  Date/Author: 2026-05-27 / Codex.

- Decision: Configure Sentry only through environment variables and do not hardcode a DSN in source.
  Rationale: The DSN is deployment configuration, not application logic. Keeping it out of source prevents accidental telemetry from local/test builds and makes CI secrets optional.
  Date/Author: 2026-05-27 / Codex.

- Decision: Stop stale MSBuild and C# compiler worker processes during validation, but leave OmniSharp running.
  Rationale: Generated files under `obj` were locked and prevented the required Release build/test from running. The stopped processes were build workers, not source files or application state.
  Date/Author: 2026-05-27 / Codex.

## Outcomes & Retrospective

The main implementation is complete. The repository now has automated build and release workflows, package lock files, an opt-in Sentry crash reporting path, a GitHub Releases update check reachable from the tray and About window, tests for the update comparison logic, README release instructions, and updated third-party notices.

Validation passed locally after clearing stale build worker locks:

    dotnet build .\AiteBar.sln -c Release
    Build succeeded with 0 warnings and 0 errors.

    dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release
    Passed: 90, Failed: 0, Skipped: 0.

## Context and Orientation

The solution root is `D:\01_Codebdbd\01_projects\aitebar`. `AiteBar.sln` contains the WPF application project `AiteBar\AiteBar.csproj` and the xUnit test project `AiteBar.Tests\AiteBar.Tests.csproj`. The app is a Windows-only desktop utility targeting `net8.0-windows`. It uses `MainWindow.xaml.cs` for tray integration and panel behavior, `App.xaml.cs` for startup and single-instance mutex logic, and `AboutWindow.xaml(.cs)` for version and resource links.

The installer is built by `installer\Build-Installer.ps1`, which publishes `AiteBar\AiteBar.csproj` to `artifacts\publish\win-x64` and then runs Inno Setup using `installer\AiteBar.iss`. `ReleaseVersionTests.cs` verifies that `AiteBar.csproj`, `AssemblyInfo.cs`, and `AiteBar.iss` agree on version `1.6.1`.

Crash reporting means capturing application exceptions and sending them to an external error dashboard. In this implementation the external dashboard is Sentry. Sentry is inactive unless `AITEBAR_SENTRY_DSN` or `SENTRY_DSN` exists in the process environment.

Update checking means the app calls the GitHub Releases API endpoint for `codebdbd/aitebar`, compares the latest release tag such as `v1.7.0` against the current assembly version, and reports whether a newer installer is available. It does not replace files by itself.

## Plan of Work

First, add repository-level build determinism files: `global.json` to pin the SDK family available locally, `Directory.Build.props` to enable package lock files and CI deterministic build properties, and GitHub Actions workflow files under `.github\workflows`. The build workflow must run on Windows, restore locked packages, build Release, run tests with coverage collection, and upload coverage as an artifact. The release workflow must run only on version tags, verify the tag matches the project version, run build and tests, run the installer script, and upload `artifacts\installer\*.exe` to a GitHub Release.

Second, add Sentry. Add `Sentry` package version 6.5.0 to `AiteBar\AiteBar.csproj`. Create `AiteBar\TelemetryService.cs` as a small wrapper around Sentry so most code does not depend directly on the SDK. Update `App.xaml.cs` to initialize telemetry early, capture unhandled AppDomain and Dispatcher exceptions, flush telemetry on exit, and keep the existing single-instance mutex behavior. Update `ActionService.cs` so caught action failures include action type context in telemetry before returning the existing failed result.

Third, add update checking. Create `AiteBar\UpdateCheckService.cs` with a testable `TryParseReleaseVersion` and `CompareReleaseVersions` path plus an async GitHub API call. Add tests in `AiteBar.Tests\UpdateCheckServiceTests.cs` for `v` prefixes, equal versions, newer versions, older versions, prerelease tags, and invalid tags. Add localized strings in the neutral resource file and use fallback for other languages. Add a "Check for updates" link to `AboutWindow.xaml`, implement its click handler in `AboutWindow.xaml.cs`, and add the same action to the tray menu in `MainWindow.xaml.cs`.

Fourth, update `README.md` so developers know that pushes and PRs are CI-gated, releases are cut by matching `vX.Y.Z` tags, Sentry requires `AITEBAR_SENTRY_DSN` or `SENTRY_DSN`, and users can check updates from the app.

## Concrete Steps

Run all commands from `D:\01_Codebdbd\01_projects\aitebar`.

After editing package references, run:

    dotnet restore .\AiteBar.sln

After implementation, run:

    dotnet build .\AiteBar.sln -c Release
    dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release

If `dotnet test` fails with WPF temporary generated-file issues, run:

    dotnet vstest .\AiteBar.Tests\bin\Release\net8.0-windows\AiteBar.Tests.dll

The release workflow itself cannot be fully executed locally without GitHub Actions and Inno Setup, but its local equivalent remains:

    .\installer\Build-Installer.ps1

## Validation and Acceptance

Build acceptance: `dotnet build .\AiteBar.sln -c Release` exits with code 0. Test acceptance: `dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release` exits with code 0 and includes the new update service tests.

Crash reporting acceptance: starting the app without `AITEBAR_SENTRY_DSN` or `SENTRY_DSN` works normally and does not require network. Starting with one of those variables initializes Sentry with the app release version and captures unhandled exceptions and action execution failures.

Update checking acceptance: the tray menu includes "Check for updates" and the About window includes the same action. If the GitHub API reports a release tag newer than the installed version, the app shows the latest version and asks whether to open the release page. If the latest tag matches or is older, the app shows that the current version is up to date. If network or API parsing fails, the app shows a controlled failure message instead of crashing.

CI/CD acceptance: `.github\workflows\build-test.yml` runs on pushes and pull requests to `main`, uses Windows, restores locked packages, builds Release, runs tests with coverage, and uploads coverage. `.github\workflows\release.yml` runs on `v*` tags, rejects a tag that does not match `AiteBar\AiteBar.csproj` version, builds and tests, runs `installer\Build-Installer.ps1`, and attaches the installer to the GitHub Release.

## Idempotence and Recovery

All added commands are safe to repeat. `dotnet restore` updates lock files based on declared package references. `dotnet build` and `dotnet test` write only build outputs. `installer\Build-Installer.ps1` already deletes and recreates publish output, so use it only when installer validation is needed. If network restore is unavailable, the code edits remain valid but package lock generation and compilation must be retried when NuGet access is available.

No existing user changes should be reverted. At the start of this work, `AGENTS.md` and several `docs\` audit files were already modified or untracked. They are treated as user work unless edited intentionally by this plan.

## Artifacts and Notes

Key evidence collected before implementation:

    dotnet --version
    8.0.421

    git remote -v
    origin  https://github.com/codebdbd/aitebar.git (fetch)
    origin  https://github.com/codebdbd/aitebar.git (push)

## Interfaces and Dependencies

`AiteBar\TelemetryService.cs` must expose static methods:

    Initialize()
    CaptureException(Exception exception, string? operation = null, IReadOnlyDictionary<string, string?>? context = null)
    CaptureMessage(string message)
    Flush(TimeSpan timeout)
    Shutdown()

`AiteBar\UpdateCheckService.cs` must expose:

    public sealed record UpdateCheckResult(bool IsUpdateAvailable, Version CurrentVersion, Version? LatestVersion, string? ReleasePageUrl, string? InstallerUrl, string? ErrorMessage)
    public sealed class UpdateCheckService
    public Task<UpdateCheckResult> CheckLatestReleaseAsync(CancellationToken cancellationToken = default)
    internal static bool TryParseReleaseVersion(string? tagName, out Version version)
    internal static bool IsNewerVersion(Version latest, Version current)

The Sentry dependency is `Sentry` version 6.5.0, selected because NuGet showed it as the current stable version on 2026-05-27. The update check uses `System.Net.Http` and `System.Text.Json`, already available in .NET 8, to avoid adding update framework risk before code signing and unattended installer handoff exist.

## Revision Notes

2026-05-27 / Codex: Rewrote the previous narrow CI/Sentry plan into a full 2026 release-quality plan that includes CI/CD, crash reporting, and built-in update checking. The plan now records current package versions, local SDK constraints, and the decision to use GitHub Releases for the first update mechanism.

2026-05-27 / Codex: Updated progress, discoveries, decisions, and outcomes after implementation and validation. Added the Sentry 6.5.0 API discovery and the local MSBuild worker lock recovery because both affect future maintenance.
