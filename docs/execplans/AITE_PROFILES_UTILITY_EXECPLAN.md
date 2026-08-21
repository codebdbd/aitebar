# Add Aite Profiles as an AiteBar built-in utility

This ExecPlan is a living document. The sections `Progress`, `Surprises & Discoveries`, `Decision Log`, and `Outcomes & Retrospective` must be kept up to date as work proceeds.

This document must be maintained in accordance with `PLANS.md` at the repository root.

## Purpose / Big Picture

After this change, AiteBar users can launch a new built-in utility that provides the user-facing functionality of the standalone AiteProfiles application from inside AiteBar. The utility lets the user scan local Google Chrome profiles, search and sort them, select one or many profiles, open profiles or URLs in those profiles, manage favorites, farm category membership, tags, quick links, and rotation mode, and use the same core profile workflows without running a separate tray application.

The new utility is not a pixel-perfect port of the old WinUI application. It is a WPF utility styled like the existing AiteBar tools, with a profile grid that remains similar in structure to the original AiteProfiles grid. The explicit exclusions are API integration, the local HTTP server, token and Credential Manager handling, password counting and SQLite-related complexity, the standalone tray behavior, the standalone global hotkey listener, and migration of old AiteProfiles data.

## Progress

- [x] (2026-08-13 08:40Z) Inspected the AiteBar utility contract, panel catalog, settings model, localization paths, and the standalone AiteProfiles project structure.
- [x] (2026-08-13 08:50Z) Confirmed the user requirement: preserve all user-facing functionality except API integration, password counting, standalone tray behavior, old hotkeys, and old-data migration.
- [x] (2026-08-13 09:05Z) Created this ExecPlan to guide the implementation.
- [x] (2026-08-13 10:35Z) Implemented the non-UI profile, Chrome scan, Chrome launch, quick-link, category, rotation, and persistence services inside `AiteBar/AiteProfilesUtility`.
- [x] (2026-08-13 11:05Z) Implemented the WPF Aite Profiles utility window in AiteBar style with tabs, search, profile grid, context actions, quick-link controls, remembered link, rotation, import/export, and focus-loss hiding.
- [x] (2026-08-13 11:25Z) Integrated the utility into AiteBar utility discovery, panel catalog, settings, localization, and tests.
- [x] (2026-08-13 11:45Z) Ran `dotnet build .\AiteBar.sln -c Release` successfully with 0 warnings and 0 errors.
- [x] (2026-08-13 11:50Z) Ran `dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release --filter "FullyQualifiedName~AiteProfiles"` successfully with 5 passed tests.
- [x] (2026-08-13 12:20Z) Ran full `dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release` successfully with 1332 passed tests.
- [x] (2026-08-13 12:21Z) Reworked the profile grid away from a WPF `DataGrid` into the original row structure: checkbox, avatar, profile name/email, time, and tags/info in one fixed popup-sized list.
- [x] (2026-08-13 12:39Z) Compared the port against the standalone `MainViewModel`, `TerminalViewModel`, `RefreshController`, `SnippetService`, `QuickLinkSelectionService`, and `RotationStateService`; found that the first port had simplified quick-link resolution and refresh behavior.
- [x] (2026-08-13 12:40Z) Ported original quick-link resolution order and ranking: selected suggestion, `tags:name:url|url` command, direct URLs, then ranked fallback by tags, name, and URL.
- [x] (2026-08-13 12:40Z) Updated refresh to do a fast profile pass followed by a stats pass for bookmarks and disk, while still excluding password counting.
- [x] (2026-08-13 12:41Z) Ran `dotnet build .\AiteBar.sln -c Release` successfully with 0 warnings and 0 errors.
- [x] (2026-08-13 12:42Z) Ran `dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release --filter "FullyQualifiedName~AiteProfiles"` successfully with 7 passed tests.
- [x] (2026-08-13 12:43Z) Ran full `dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release` successfully with 1334 passed tests.
- [x] (2026-08-13 12:40Z) Built the installer at `artifacts\installer\AiteBar-Setup-1.15.11.exe`.
- [x] (2026-08-21) Hardened profile metadata persistence: cancellation is propagated and atomic replacement no longer deletes the current JSON file before the replacement is ready.
- [x] (2026-08-21) Reduced local-update churn: favorite, farm, tags, and launch timestamps now replace only the affected cached profile; the visible list is synchronized rather than cleared and recreated.
- [x] (2026-08-21) Localized stateful favorite/farm context actions for all supported languages and added accessibility names for search, scan, and quick-link icon commands.
- [x] (2026-08-21) Ran focused Aite Profiles tests successfully with 9 passed tests and the full test suite successfully with 1337 passed tests.
- [x] (2026-08-21) Made favorite, farm, tags, and last-launch changes commit to memory only after their JSON write succeeds; added regression coverage for a rejected metadata write.
- [x] (2026-08-21) Replaced synchronous rotation-state file access on the UI path with asynchronous initialization and serialized background persistence; added persistence/reload coverage.
- [x] (2026-08-21) Treated user-requested scan cancellation as a normal state rather than a scan failure; added cancellation coverage for both JSON persistence and refresh.
- [x] (2026-08-21) Ran focused Aite Profiles tests successfully with 13 passed tests and the full test suite successfully with 1341 passed tests.
- [x] (2026-08-21) Made failed rotation-state writes visible to the utility, added a bounded final flush when its window closes, and covered the reported failure plus unsuccessful flush path.
- [x] (2026-08-21) Ran focused Aite Profiles tests successfully with 14 passed tests and the full test suite successfully with 1342 passed tests.
- [ ] Manually validate the utility window and AiteBar panel behavior on all four panel sides.

## Surprises & Discoveries

- Observation: AiteBar and AiteProfiles use different UI stacks.
  Evidence: `AiteBar/AiteBar.csproj` targets `net10.0-windows` with `UseWPF=true`, while `D:\01_Codebdbd\01_projects\aiteprofiles\src\AiteProfiles.csproj` targets `net8.0-windows10.0.19041.0` with `UseWinUI=true` and `Microsoft.WindowsAppSDK`. This means XAML and window lifecycle code cannot be copied directly.

- Observation: AiteProfiles already separates most business logic from UI.
  Evidence: the standalone app has `src\Domain\Chrome`, `src\Domain\Profiles`, `src\Services`, `src\ViewModels`, and `src\Views`, so much of the domain behavior can be adapted into the AiteBar utility folder.

- Observation: The standalone startup code contains responsibilities that must not become part of the utility.
  Evidence: `D:\01_Codebdbd\01_projects\aiteprofiles\src\App.xaml.cs` starts `SingleInstanceService`, `GlobalHotkeyService`, `LocalHttpServer`, and tray bindings. AiteBar already owns application single-instance, tray, and hotkey behavior.

- Observation: Password counting is the main extra dependency and privacy/performance risk in the profile scanner.
  Evidence: `D:\01_Codebdbd\01_projects\aiteprofiles\src\Domain\Chrome\ChromeProfileScanner.cs` uses `Microsoft.Data.Sqlite` and reads Chrome `Login Data` files to count saved passwords. The user explicitly chose to remove this behavior.

- Observation: Existing layout tests were manually disabling the previous fixed list of built-in utilities.
  Evidence: after adding `AiteProfiles` to `UtilityButtonCatalog.All`, `MainWindowIconConverterOrientationTests` still left the new utility visible and failed with two panel children. The helper now disables built-ins through the catalog and re-enables only Icon Converter.

- Observation: The first WPF port simplified the original quick-link terminal behavior too much.
  Evidence: standalone `TerminalViewModel` resolves selected suggestions, `tags:name:url|url` commands, direct URL groups, and ranked fallback matches; the AiteBar port initially only matched exact snippet names/URLs or direct URLs. The port now mirrors the original resolution and ranking order.

- Observation: The standalone refresh pipeline is two-phase.
  Evidence: `RefreshController` runs a fast pass without heavy stats and then a second pass with stats. The utility now follows that pattern for bookmarks and disk, while still not reading password databases.

## Decision Log

- Decision: Implement the feature as a built-in AiteBar utility under `AiteBar/AiteProfilesUtility`.
  Rationale: The user requested a separate architectural folder, and AiteBar discovers built-in utilities through `[Utility]` and `IUtility` in `AiteBar/UtilityRegistry.cs`. Keeping all new files under one folder avoids scattering the ported application across the root.
  Date/Author: 2026-08-13 / Codex

- Decision: Preserve user workflows, not the standalone application shell.
  Rationale: The important requirement is to keep the AiteProfiles functionality. Standalone process concerns such as tray, single-instance, old hotkeys, and API server are either excluded or already provided by AiteBar.
  Date/Author: 2026-08-13 / Codex

- Decision: Port UI to WPF in AiteBar style instead of embedding WinUI.
  Rationale: AiteBar is a WPF app. Mixing WinUI/Windows App SDK windows into the WPF utility surface would add deployment and lifecycle risk. A WPF rewrite can preserve the grid structure and workflows while fitting AiteBar's existing visual contract.
  Date/Author: 2026-08-13 / Codex

- Decision: Remove password count support completely for the initial utility.
  Rationale: It requires reading Chrome login databases through SQLite, adds a NuGet dependency, and increases privacy and performance risk. The user explicitly asked to drop password counting and everything that unnecessarily complicates the program.
  Date/Author: 2026-08-13 / Codex

- Decision: Do not migrate data from the standalone AiteProfiles app.
  Rationale: The user explicitly said old data does not need to be transferred. The new utility will create fresh AiteBar-owned data files under the AiteBar app data directory.
  Date/Author: 2026-08-13 / Codex

- Decision: Keep the first implementation folder flat under `AiteBar/AiteProfilesUtility` instead of adding nested `Domain`, `Services`, `ViewModels`, and `Views` folders immediately.
  Rationale: The first port is scoped and the current AiteBar utility pattern uses compact utility-specific files. The architectural boundary is still isolated in one folder, and nested folders can be introduced later if this utility grows.
  Date/Author: 2026-08-13 / Codex

- Decision: Implement `Create profile` by opening Chrome's profile picker.
  Rationale: Chrome owns actual profile creation. This preserves the original user workflow without adding browser automation or unsafe profile directory fabrication.
  Date/Author: 2026-08-13 / Codex

- Decision: Reconcile the visible profile collection in place after filtering and sorting.
  Rationale: Local metadata changes should retain item identity and avoid a complete `ObservableCollection` reset, which otherwise discards selection and causes unnecessary WPF layout work.
  Date/Author: 2026-08-21 / Codex

- Decision: Commit profile metadata in memory only after atomic persistence succeeds.
  Rationale: A failed disk write must leave both the current UI state and the next-start state consistent; candidate collections make this possible without a compensating write or reload.
  Date/Author: 2026-08-21 / Codex

- Decision: Persist rotation state asynchronously and serialize writes through a service-owned gate.
  Rationale: Rotation controls are bound to the UI and must never synchronously wait on filesystem I/O. Each queued save snapshots the latest in-memory state, preventing obsolete state from overwriting a newer interaction.
  Date/Author: 2026-08-21 / Codex

- Decision: Surface rotation-state persistence failures and retry once during a bounded window-close flush.
  Rationale: Background persistence must not silently lose user settings. The normal UI path remains non-blocking, while the closing path has a short bounded opportunity to commit the latest state safely.
  Date/Author: 2026-08-21 / Codex

## Outcomes & Retrospective

Implemented a first complete AiteBar-native Aite Profiles utility. It scans local Chrome profile metadata without reading password databases, stores AiteBar-owned favorites/farm/tags/snippets/rotation/cache data, launches Chrome profiles and Google actions, supports multi-selection, search, sorting, quick-link import/export/edit/selection, remembered quick links, rotation mode, context actions, and focus-loss hiding. Automated Release build and tests pass; remaining validation is manual UI exercise from the live AiteBar panel on all four panel sides.

After user validation feedback, corrected two major mismatches with the standalone program: the profile list now follows the original row structure instead of a WPF `DataGrid`, and quick-link launch behavior now follows the standalone terminal algorithm. Refresh now follows the standalone two-phase model without reintroducing password count logic.

The post-audit hardening preserves valid JSON during writes, respects cancellation, reduces profile-list churn after local updates, localizes dynamic context actions, and exposes icon-only controls to accessibility tools. Automated verification passed with 9 focused Aite Profiles tests and 1337 tests across the full suite. Manual validation on every panel side remains the only unchecked acceptance item.

The final reliability pass adds transactional metadata commits, asynchronous rotation-state persistence, normal cancellation handling, and focused failure-path coverage. The current automated result is 13 focused Aite Profiles tests and 1341 tests across the full suite. Manual validation on every panel side remains required because it exercises WPF window interaction and the live Chrome environment.

The final persistence closeout makes a failed rotation write visible in the utility and retries the latest state during a bounded window-close flush. Automated verification now passes 14 focused Aite Profiles tests and 1342 tests across the full suite. Manual validation remains required for the live Chrome and WPF interaction matrix.

## Context and Orientation

AiteBar is a Windows desktop app built with WPF on .NET 10. The solution file is `AiteBar.sln`. The main project is `AiteBar/AiteBar.csproj`, and tests live in `AiteBar.Tests/AiteBar.Tests.csproj`.

AiteBar represents each built-in tool as an `IUtility`. The interface and base class live in `AiteBar/UtilityRegistry.cs`. `UtilityBase<TWindow>` keeps one utility window instance, restores an existing window on repeated launch when implemented, and catches launch errors. `AiteBar/App.xaml.cs` calls `UtilityRegistry.RegisterAllFromAssembly` at startup, so a new class marked with `[Utility]` can be discovered automatically. AiteBar's panel button catalog lives in `AiteBar/UtilityButtonCatalog.cs`, while user-visible settings are stored in `AiteBar/Models.cs` as `AppSettings` and loaded/saved through `AiteBar/AppSettingsService.cs`.

The standalone source program is outside this repository at `D:\01_Codebdbd\01_projects\aiteprofiles`. It is a WinUI/Windows App SDK application. Important source areas are `src\Domain\Chrome`, which scans and launches Chrome profiles; `src\Domain\Profiles`, which stores profile state such as favorites, farm category, tags, and cache; `src\Services`, which includes quick-link and rotation services; `src\ViewModels`, which contains the main workflow commands; and `src\Views`, which contains the current WinUI layout. The implementation should read those files during the port and adapt behavior, but final source files must live in the AiteBar repository.

"Quick link" means a named group of one or more URLs with tags. In the original app, a user can type or pick a quick link, then launch its URLs in selected Chrome profiles. "Rotation mode" means repeated launch chooses the next visible profile in a saved order instead of always launching the current selection. "Farm" is a user-managed category separate from favorites.

## Plan of Work

First, create the folder structure `AiteBar/AiteProfilesUtility`. Under it create `Domain/Chrome`, `Domain/Profiles`, `Services`, `ViewModels`, and `Views`. Keep namespaces under `AiteBar.AiteProfilesUtility` or nested namespaces below it. The implementation should not add code to the AiteBar project root except for integration points that are already centralized there.

Second, port the data models and pure helpers. Define profile records equivalent to the standalone `Profile`, `ProfileScanRow`, `CacheProfileEntry`, and quick-link `Snippet`, but remove password count fields from the user-facing contract. Keep bookmark count, disk size, last launch time, display name, email, folder, path, avatar/image URI, favorites, farm category, tags, and search key if they remain cheap enough to compute without SQLite. Add tests for profile keys, tag normalization, quick-link parsing, URL normalization, sort order, and rotation sequence.

Third, implement Chrome scanning without password counting. The scanner should read Chrome user data from `%LOCALAPPDATA%\Google\Chrome\User Data`, read `Local State`, profile `Preferences` or `Secure Preferences`, profile picture files, bookmark files, and file timestamps using shared-read file access so Chrome may remain open. It must skip system and guest profile folders. Expensive directory size calculation should either run only during explicit refresh or be cached so the UI does not freeze. If disk-size calculation proves too slow, keep the column but show an unknown value until a background refresh completes. Do not read `Login Data`, do not add `Microsoft.Data.Sqlite`, and do not store password metadata.

Fourth, port Chrome launching. Define a launch service that finds `chrome.exe` in Program Files, Program Files (x86), or LocalAppData, then uses `ProcessStartInfo.ArgumentList` to pass `--profile-directory=<folder>`, `--incognito`, `--start-maximized`, and URLs safely. Support opening a profile, selected profiles, selected profile folder, profile picker, Gmail, Gmail compose, Google Drive, Gemini, Google account settings, and a quick link's URLs in the selected profile or profiles.

Fifth, implement local persistence under the existing AiteBar app data root, in a subfolder such as `%APPDATA%\Codebdbd\Aite Bar\AiteProfiles`. Store favorites, farm category membership, tags, snippets, rotation enabled state, last rotation profile key, rotation order, profile cache, and utility window geometry. Use AiteBar or .NET atomic-write helpers where available. Do not read or migrate `%APPDATA%\Codebdbdb\AiteProfiles` or any standalone AiteProfiles data directory.

Sixth, port the main view model behavior to WPF. The WPF view model should expose the filtered profile list, search text, active category, selected profiles, context profile, quick-link input and suggestions, busy status, and commands. Preserve the original user workflows: refresh/scan, filter, sort by profile/time/email where applicable, select all, multi-select, double-click open, Enter launch, Escape clear search or focus the table, context menu actions, favorite toggle, farm toggle, tag edit, quick-link add/edit/import/export, lock remembered quick link, rotation toggle, and post-launch quick-link input behavior.

Seventh, build the WPF UI. Create `AiteProfilesWindow.xaml` as a normal utility window, not a tray popup. Use AiteBar's dark window resources and compact utility styling. The main layout should keep the original structure: tabs and search/scan controls at the top, a large profile grid in the center, and quick-link/action controls at the bottom. Use a WPF `ListView` or `DataGrid` depending on which better matches the AiteBar style and testability. The grid should include selection, avatar, profile name and email, last activity, tags and info, and action/context affordances. Use AiteBar-style context menus, buttons, typography, and colors.

Eighth, implement utility window lifecycle. The new utility class should be `AiteProfilesUtility : UtilityBase<AiteProfilesWindow>` with stable ID `AiteProfiles`. Launching it from the AiteBar panel should hide the panel through the standard `onBeforeExecute` callback and show the utility. Re-launching should activate the existing window instead of creating duplicates. The utility window should hide or close itself on focus loss like the original popup behavior, but it must not hide while an owned dialog, context menu, import/export file dialog, or tag editor is active.

Ninth, integrate the utility into AiteBar. Add `ShowPresetAiteProfiles` to `AppSettings` in `AiteBar/Models.cs`, clone and normalize it in `AiteBar/AppSettingsService.cs`, add an `AiteProfiles` entry to `AiteBar/UtilityButtonCatalog.cs`, ensure the panel execution path can launch it through `UtilityRegistry`, add localized strings to `AiteBar/Resources/Strings.resx`, `Strings.ru.resx`, `Strings.uk.resx`, and `Strings.de.resx`, and expose the visibility switch in `AiteBar/AppSettingsWindow.xaml` and code-behind using the current settings-window pattern.

Tenth, update tests and documentation. Add focused tests under `AiteBar.Tests` for non-UI services, quick-link parsing, profile-key handling, rotation, persistence, and any layout helper created for the grid/window. Add UI contract tests only where existing AiteBar test patterns support them. Update `README.md`, `docs/functions.md`, `docs/UTILITIES.md`, and `docs/USER_MANUAL.md` to describe the new utility and its privacy-relevant behavior. Note explicitly that it reads local Chrome profile metadata and does not read password databases.

## Concrete Steps

All commands run from `D:\01_Codebdbd\01_projects\aitebar`.

Before editing, inspect the current state:

    git status --short
    rg --files AiteBar AiteBar.Tests

Read the source app areas that define the behavior to preserve:

    Get-Content D:\01_Codebdbd\01_projects\aiteprofiles\src\ViewModels\MainViewModel.cs
    Get-Content D:\01_Codebdbd\01_projects\aiteprofiles\src\Domain\Chrome\ChromeProfileScanner.cs
    Get-Content D:\01_Codebdbd\01_projects\aiteprofiles\src\Domain\Chrome\ChromeLauncher.cs
    Get-Content D:\01_Codebdbd\01_projects\aiteprofiles\src\Domain\Profiles\ProfilesStore.cs
    Get-Content D:\01_Codebdbd\01_projects\aiteprofiles\src\Services\SnippetService.cs
    Get-Content D:\01_Codebdbd\01_projects\aiteprofiles\src\Services\QuickLinkSelectionService.cs
    Get-Content D:\01_Codebdbd\01_projects\aiteprofiles\src\Services\RotationStateService.cs

Create the new implementation folder and files, then run focused tests as each non-UI part becomes available. Example filters should be updated to match the final test class names:

    dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release --filter "FullyQualifiedName~AiteProfiles"

Run repository quality gates:

    dotnet build .\AiteBar.sln -c Release
    dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release

If `dotnet test` fails because WPF/MSBuild generated temporary files or dispatcher shutdown issues interfere with the run, first ensure the Release build succeeded, then run the repository fallback:

    dotnet vstest .\AiteBar.Tests\bin\Release\net10.0-windows\AiteBar.Tests.dll

The expected result is a successful Release build with zero errors and a test run with zero failed tests. Exact test counts must be recorded in `Progress` after implementation rather than predicted in advance.

## Validation and Acceptance

The utility is accepted only when all preserved AiteProfiles workflows work from inside AiteBar. A user enables the built-in Aite Profiles utility in AiteBar settings, sees its button on the panel, launches it, and receives one AiteBar-styled utility window. Re-clicking the panel button activates the existing window and does not create a second one.

On first launch, the utility scans local Chrome profiles and shows them in a grid. The grid must remain responsive while scanning. It must show profile folder, display name, email if available, avatar/image if available, last activity, bookmarks/disk/tag information where supported without password counting, favorite state, farm state, and selection state. If Chrome is missing or profile data is inaccessible, the utility shows a clear local error and does not crash AiteBar.

Search filters by profile folder, display name, email, tags, and path. Tabs switch between all profiles, favorites, and farm category. Sorting by profile and time works predictably. Multi-selection, select-all, context-click, double-click, Enter, and Escape preserve the original behavior in WPF form.

Profile actions must work: open current profile, open selected profiles, open incognito, open profile folder, open profile picker, copy email, open Gemini, Gmail, Drive, Google account settings, and Gmail compose. Quick-link actions must work: create, edit, import, export, choose suggestions, lock a remembered link, launch one or more URLs into selected profiles, and update post-launch input state. Rotation mode must choose the next visible profile in stable order and persist its enabled state, order, and last launched profile.

Focus-loss behavior must match the requirement for an AiteBar utility: the window hides or closes itself when focus moves outside the utility, but stays open while the user interacts with its context menus, quick-link menus, tag editor, or file dialogs. The utility must not create its own tray icon and must not register its own global hotkey. AiteBar's normal built-in utility hotkey assignment remains the way to launch it by keyboard.

Privacy acceptance: no code reads Chrome `Login Data` files, no password count appears in UI or persisted data, no local HTTP endpoint starts, no API token is generated, and no old standalone data is read or migrated. Documentation must state that the utility reads local Chrome profile metadata and stores only AiteBar-owned local favorites, farm category, tags, quick links, rotation, and cache data.

AiteBar regression acceptance: panel show, hide, positioning, contexts, hotkeys, tray access, and all four panel sides `Top`, `Bottom`, `Left`, and `Right` still work after adding the utility. Existing utilities such as Quick Note, Clipboard Manager, Text Processing, Prompt Builder, and Zen Editor still launch.

## Idempotence and Recovery

The implementation is additive. If work stops halfway, keep this plan's `Progress` section accurate and continue from the first unchecked item. Do not reset or revert unrelated user changes in the working tree.

The new utility's persistence root must be isolated under AiteBar's app data directory. Re-running initialization must not duplicate profiles, snippets, or categories. Atomic writes should replace a complete file only after the new payload is fully written. If a refresh fails because Chrome files are locked or changing, keep the last valid cache and report the refresh failure without deleting user metadata.

Tests must use temporary directories or injected paths and may delete only those exact temporary directories. Manual validation may create AiteBar utility data, but it must never delete the standalone AiteProfiles data directory.

## Artifacts and Notes

Expected new source layout:

    AiteBar/AiteProfilesUtility/
      AiteProfilesUtility.cs
      AiteProfilesWindow.xaml
      AiteProfilesWindow.xaml.cs
      Domain/
        Chrome/
        Profiles/
      Services/
      ViewModels/
      Views/

Expected new local data shape:

    %APPDATA%\Codebdbd\Aite Bar\AiteProfiles\
      favorites.json
      farm.json
      tags.json
      snippets.json
      rotation.json
      profiles_cache.json
      window.json

The exact file names can change during implementation if a cleaner model emerges, but the data must remain local, AiteBar-owned, and separate from the standalone app's data.

Important standalone code to preserve behavior from:

    D:\01_Codebdbd\01_projects\aiteprofiles\src\ViewModels\MainViewModel.cs
    D:\01_Codebdbd\01_projects\aiteprofiles\src\Domain\Chrome\ChromeProfileScanner.cs
    D:\01_Codebdbd\01_projects\aiteprofiles\src\Domain\Chrome\ChromeLauncher.cs
    D:\01_Codebdbd\01_projects\aiteprofiles\src\Domain\Profiles\ProfilesStore.cs
    D:\01_Codebdbd\01_projects\aiteprofiles\src\Services\SnippetService.cs
    D:\01_Codebdbd\01_projects\aiteprofiles\src\Services\QuickLinkSelectionService.cs
    D:\01_Codebdbd\01_projects\aiteprofiles\src\Services\RotationStateService.cs

Important standalone code to exclude:

    D:\01_Codebdbd\01_projects\aiteprofiles\src\Api\
    D:\01_Codebdbd\01_projects\aiteprofiles\src\Runtime\CredentialTokenService.cs
    D:\01_Codebdbd\01_projects\aiteprofiles\src\Runtime\ITokenService.cs
    D:\01_Codebdbd\01_projects\aiteprofiles\src\Runtime\TrayIconService.cs
    D:\01_Codebdbd\01_projects\aiteprofiles\src\Runtime\GlobalHotkeyService.cs
    D:\01_Codebdbd\01_projects\aiteprofiles\src\Runtime\SingleInstanceService.cs

## Interfaces and Dependencies

`AiteBar/AiteProfilesUtility/AiteProfilesUtility.cs` must define:

    [Utility]
    public sealed class AiteProfilesUtility : UtilityBase<AiteProfilesWindow>

The utility must use ID `AiteProfiles`. The display name key should be `Tool_AiteProfiles`. The panel tooltip key should be `Main_AiteProfilesTooltip`. The panel glyph should use an existing Fluent glyph that visually communicates browser profiles or people; choose it during implementation and keep `AiteProfilesUtility` and `UtilityButtonCatalog` synchronized.

The profile scanner should expose an interface equivalent to:

    public interface IAiteProfilesScanner
    {
        Task<IReadOnlyList<AiteProfileScanRow>> ScanAsync(
            IReadOnlyDictionary<string, AiteProfileCacheEntry>? cache,
            bool includeExpensiveStats,
            CancellationToken cancellationToken = default);
    }

The launch service should expose methods equivalent to:

    void OpenProfile(string folder);
    void OpenProfiles(IEnumerable<string> folders);
    void OpenProfileIncognito(string folder);
    void OpenUrlsInProfile(string folder, IReadOnlyList<string> urls);
    void OpenProfilePicker();
    void OpenFolder(string path);
    void OpenGemini(string folder);
    void OpenGmail(string folder);
    void OpenGoogleDrive(string folder);
    void OpenGoogleAccountSettings(string folder);
    void OpenGmailCompose(string folder);

The store should expose testable operations equivalent to:

    Task<IReadOnlyList<AiteProfile>> SnapshotProfilesAsync(CancellationToken cancellationToken = default);
    Task RefreshAsync(bool includeExpensiveStats, CancellationToken cancellationToken = default);
    Task MarkFavoriteAsync(string folder, string path, bool value, CancellationToken cancellationToken = default);
    Task MarkFarmAsync(string folder, string path, bool value, CancellationToken cancellationToken = default);
    Task SetTagsAsync(string folder, string path, string tagsText, CancellationToken cancellationToken = default);

Quick-link storage and parsing should preserve the standalone behavior of command format `tag:name:url|url`, URL normalization to HTTP/HTTPS, import from lines, text export, and JSON export. The exact types may differ, but all operations must be covered by tests.

No `Microsoft.WindowsAppSDK`, `CommunityToolkit.WinUI`, or `Microsoft.Data.Sqlite` package should be added to AiteBar for this utility. Use WPF, .NET file and process APIs, `System.Text.Json`, and existing AiteBar helpers. If an implementation step appears to require one of the excluded packages, update this ExecPlan's `Surprises & Discoveries` and `Decision Log` before changing dependencies.

Plan revision note: 2026-08-13, initial ExecPlan created after the user clarified that preserving all non-excluded functionality is the top priority, while design should follow AiteBar style and the standalone application shell should be removed.
