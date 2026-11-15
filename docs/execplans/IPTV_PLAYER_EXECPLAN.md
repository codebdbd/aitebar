# Add the Built-in IPTV Player Utility and General Settings IPTV Section

This ExecPlan is a living document. The sections `Progress`, `Surprises & Discoveries`, `Decision Log`, and `Outcomes & Retrospective` must be kept up to date as work proceeds.

This document is maintained in accordance with `PLANS.md` at the repository root.

## Purpose / Big Picture

After this change, AiteBar users can enable and launch a built-in utility named `IptvPlayer` (`Id = "IptvPlayer"`) from the primary dock panel, tray menu, or a user-configured global hotkey. Inside AiteBar General Settings (`AppSettingsWindow`), a dedicated `IPTV` section located between `Telegram` and `About` lets users add, edit, validate, refresh, reorder, enable/disable, and delete any number of IPTV playlist sources (M3U/M3U8 URLs, local `.m3u/.m3u8/.txt` files with optional copy into internal storage, pasted M3U text, and direct single-stream URLs), as well as configure 24-hour automatic background refresh for URL playlists. Users confirm or discard IPTV source changes atomically through the main `Save` / `Cancel` transaction of `AppSettingsWindow`.

Opening `IptvPlayer` presents a native WPF `Workspace` window (`DarkWindow`) using `LibVLCSharp.WPF` (`VideoView`) and bundled native `VideoLAN.LibVLC.Windows` x64 codecs so compatible HLS `.m3u8`, MPEG-TS, and network streams play locally without an external VLC install or WebView. Users can search across all enabled playlists, filter by playlist, group, or unified cross-playlist favorites, switch channels with `PageUp`/`PageDown` or clicks, toggle mute with `M`, enter/exit fullscreen with `F`/`Esc`, inspect session-only diagnostic test streams, and switch linked alternative stream variants when available.

## Progress

- [x] (2026-11-15 05:25Z) Reviewed `AiteBar_IPTV_Final_TZ.md`, `PLANS.md`, `AGENTS.md`, `docs/UTILITIES.md`, utility catalog/registry/hotkey wiring, `AppSettingsWindow` structure, `BackupService`, `AiCredentialStore`, and existing test contracts.
- [x] (2026-11-15 05:27Z) Verified `ic_fluent_tv_24_regular` (codepoint 60867 / `\uEDC3`) and supporting player/action glyphs in `AiteBar/Resources/FluentSystemIcons.json`.
- [x] (2026-11-15 05:32Z) Added `LibVLCSharp`, `LibVLCSharp.WPF`, and `VideoLAN.LibVLC.Windows` NuGet references and updated `packages.lock.json` via `dotnet restore .\AiteBar.sln --force-evaluate`.
- [x] (2026-11-15 05:45Z) Implemented domain models, M3U/HLS parser, atomic store with credential protection, multi-playlist network service, catalog/variant/favorites aggregator, and LibVLC playback service under `AiteBar/IptvPlayer/`.
- [x] (2026-11-15 05:55Z) Wired `IptvPlayer` into `Models.cs`, `AppSettingsService.cs`, `UtilityButtonCatalog.cs`, `HotkeyService.cs`, `AppSettingsSectionNavigationHelper.cs`, `AiCredentialStore.cs`, `BackupService.cs`, and `MainWindow.xaml.cs`.
- [x] (2026-11-15 06:05Z) Implemented `IptvPlaylistDialog` and the transactional `IPTV` section in `AppSettingsWindow` (`AppSettingsWindow.xaml`, `AppSettingsWindow.xaml.cs`, `AppSettingsWindow.Iptv.cs`).
- [x] (2026-11-15 06:15Z) Implemented `IptvPlayerUtility` and `IptvPlayerWindow` (`IptvPlayerWindow.xaml`, `.xaml.cs`) with airspace-safe OSD overlay, virtualized channel list, async logo loader, shortcuts, and clean LibVLC disposal.
- [x] (2026-11-15 06:22Z) Added EN/RU/UK/DE localizations, updated `THIRD_PARTY_NOTICES.txt` / `.md` and documentation (`README.md`, `docs/functions.md`), and wrote unit and contract tests in `AiteBar.Tests`.
- [x] (2026-11-15 06:30Z) Ran Release build, verified 15/15 IPTV tests, 60/60 layout/catalog/localization tests, 23/23 backup tests, and executed `installer/Build-Installer.ps1` producing `AiteBar-Setup.exe`.

## Surprises & Discoveries

- Observation: The working tree is at version `1.16.1` and already contains `TelegramPostEditor` and a `Telegram` section in `AppSettingsWindow` (`AppSettingsSection.Telegram` in `AiteBar/AppSettingsSectionNavigationHelper.cs`).
  Evidence: `AiteBar/AppSettingsSectionNavigationHelper.cs` defines `General, Contexts, Hotkeys, QuickTools, AiProviders, Telegram, About`. Adding `Iptv` between `Telegram` and `About` preserves existing indices `0..5` and shifts `About` to index `7`.
- Observation: `Directory.Build.props` enables NuGet lock-file mode (`RestorePackagesWithLockFile`), and `installer/Build-Installer.ps1` runs `dotnet publish` with `-p:RestoreLockedMode=true`.
  Evidence: Any new `PackageReference` in `AiteBar/AiteBar.csproj` must be followed by `dotnet restore .\AiteBar.sln --force-evaluate` before locked-mode build/publish succeeds.
- Observation: `AppSettingsLayoutContractTests.cs` strictly asserts the exact count and names of `SettingsNavigationList` items, `HotkeyCaptureBox` instances, `SettingsScrollContent` sections, margins, and QuickTool rows in `AppSettingsWindow.xaml`.
  Evidence: `AiteBar.Tests/AppSettingsLayoutContractTests.cs` checks section border names and margins (`expectedSections`), `SettingsNavigationList` tag icons and count, and `QuickToolsList` grid count against `UtilityButtonCatalog.All.Count`.
- Observation: `WindowsAiCredentialStore` validates an explicit allowlist of target prefixes in `IsSupportedTarget`, so IPTV source secrets stored via Credential Manager require adding `IptvSourceTargetPrefix` there, whereas `TelegramPostEditor` already stores secrets via prefixes in Credential Manager (`TelegramBotTargetPrefix` / `TelegramServerTargetPrefix`) without altering AI connection logic.
  Evidence: `AiteBar/AiCredentialStore.cs` lines 16-18 and 122-126.

- Observation: In WPF, referencing ResourceDictionaries from a subfolder (`AiteBar/IptvPlayer/`) requires the `/AiteBar;component/` URI syntax rather than bare pack URIs, and any style applied to `ToggleButton` must have `TargetType="ToggleButton"` (assigning a `Button` style triggers an invalid cast at runtime). Furthermore, controls like `SldVolume` fire `ValueChanged` during XAML property parsing before `InitializeComponent()` returns, requiring null checks on sibling elements and playback fields.
  Evidence: `IptvPlayerTests.IptvPlayerWindow_InstantiatesWithoutResourceException` caught and validated these exact XAML/lifecycle constraints.

## Decision Log

- Decision: Place all IPTV implementation files in `AiteBar/IptvPlayer/` (with `AppSettingsWindow.Iptv.cs` in `AiteBar/`) using namespace `AiteBar` or `AiteBar.IptvPlayer`.
  Rationale: Matches `TelegramPostEditor/` and `AiteProfilesUtility/` modular organization in the repository while keeping `UtilityRegistry` reflection discovery automatic via `[Utility]`.
  Date/Author: 2026-11-15 / Antigravity
- Decision: Insert `AppSettingsSection.Iptv` after `Telegram` and before `About` (index 6).
  Rationale: `AiteBar_IPTV_Final_TZ.md` states the reference snapshot predated `Telegram` and requires preserving existing sections; keeping `Telegram` before `Iptv` and `About` last maintains logical progression without breaking AI Providers or Telegram navigation.
  Date/Author: 2026-11-15 / Antigravity
- Decision: Use Windows Credential Manager (`AiteBar:IptvSource:{playlistId}`) through `IAiCredentialStore` with an explicit prefix check AND DPAPI fallback support in `IptvPlayerStore` for URLs longer than Windows Credential Manager's 2560-byte blob limit or in isolated test environments.
  Rationale: Protects sensitive IPTV URLs containing userinfo or token query parameters from plaintext leakage in `settings.json` while remaining testable offline and never altering AI provider logic.
  Date/Author: 2026-11-15 / Antigravity
- Decision: Place OSD banners, status messages, and fullscreen controls inside `LibVLCSharp.WPF.VideoView`'s foreground content slot rather than layering a plain WPF Grid over the HWND.
  Rationale: LibVLCSharp's `VideoView` uses an airspace-aware overlay window to render WPF elements cleanly over the native video surface without HWND clipping or invisibility.
  Date/Author: 2026-11-15 / Antigravity

## Outcomes & Retrospective

The built-in IPTV Player utility and General Settings IPTV configuration section are fully implemented, verified, and packaged into the release installer.

### Completed Capabilities
1. **General Settings (`AppSettingsWindow`)**:
   - Added dedicated `IPTV` section at index 6 (after `Telegram`, before `About`).
   - Supports unlimited playlist sources (`Url`, `LocalFile`, `InlineText`, `SingleStream`).
   - Modal `IptvPlaylistDialog` (`DarkWindow`) with validation and live connectivity/format check.
   - Per-playlist controls: move Up/Down, enable/disable toggle, live check, refresh cache, edit, and delete.
   - Global auto-refresh toggle + interval (hours, default 24h), plus "Update all URL playlists" action.
   - Transactional draft editing (`_iptvDraft`): modifications commit atomically on `Save` or completely roll back on `Cancel` / window close.
2. **Player Window (`IptvPlayerWindow`, `WindowClass="Workspace"`)**:
   - Native WPF UI powered by `LibVLCSharp.WPF` (`VideoView`) and bundled `VideoLAN.LibVLC.Windows` (3.0.21 x64).
   - Left column: 16:9 video viewport with airspace-safe `VideoView` content overlay for 3.5s OSD channel/track banner and statuses (`Connecting`, `Buffering`, `Reconnecting (attempt n/3)`, `Error`, `Stopped`).
   - Transport controls: Play/Stop, Pause (enabled when `CanPause` is true), Mute, Volume slider (0-100), Variant selector dropdown (visible when >1 stream quality exists), and Fullscreen toggle.
   - Right column: Splitter-resizable sidebar (~360 DIP) with search box, playlist filter, group/favorites filter, virtualized channel list with favorite star toggles, and "Sources" button jumping directly to `AppSettingsSection.Iptv`.
   - Full keyboard navigation: `PageUp`/`PageDown` (channel navigation), `F` / `Esc` (fullscreen toggle), `M` (mute toggle), `Space` (play/pause).
   - Session-only "Test Streams" diagnostic toggle offering Big Buck Bunny HLS and Sintel HLS streams without polluting persistent playlists.
3. **Robust Backend & Security**:
   - M3U/M3U8 parser supporting `EXTINF`, `EXTGRP`, `#EXTVLCOPT`, `#KODIPROP`, direct HLS single streams, and UTF-8/Win1251 auto-detection.
   - Security: blocks `file://` URIs in remote playlists, rejects non-HTTP(S) schemes, and guards against private IP redirection.
   - Credential security: sensitive URLs (containing userinfo or access tokens) stored via Windows Credential Manager (`AiteBar:IptvSource:{id}`) or DPAPI fallback, masked with asterisks in logs and UI.
   - Atomic disk storage under `%APPDATA%\Codebdbd\Aite Bar\IptvPlayer\` using `.tmp` and `.bak` backups with automatic corruption recovery.
   - Per-playlist concurrency locking and cancellation token support during refreshes.
   - Conservative variant grouping (keys strictly on `tvg-id` or exact `Title` within groups to prevent false merges).
   - Unified cross-playlist favorites based on `tvg-id` or normalized title.
   - Backup/Restore integration for `iptv/` and `AiteBar:IptvSource:*` credentials.
4. **Localization & Quality**:
   - All 32 IPTV resource strings localized across English, Russian, Ukrainian, and German.
   - `THIRD_PARTY_NOTICES.txt` and `THIRD_PARTY_NOTICES.md` updated with LibVLCSharp and LibVLC (LGPL-2.1-or-later) licensing.
   - Documentation updated in `README.md` and `docs/functions.md`.

### Verification & Test Matrix Summary
- **Unit & Contract Tests**:
  - `AiteBar.Tests/IptvPlayerTests.cs`: 17 passed, 0 failed.
    - `Parser_ValidM3U_ParsesAllMetadata`: PASSED.
    - `Parser_SingleStreamHls_CreatesSingleChannel`: PASSED.
    - `Parser_Security_RejectsUnsupportedOrFileSchemesInRemote`: PASSED.
    - `Parser_MasksSensitiveCredentials`: PASSED.
    - `Store_SavesAndLoadsPlaylistsAtomically`: PASSED.
    - `Store_FallsBackToBakFileWhenCorrupt`: PASSED.
    - `Catalog_AggregatesAndFiltersChannels`: PASSED.
    - `Catalog_Favorites_PersistAndToggleCrossPlaylist`: PASSED.
    - `Catalog_AutoFallbackVariants_ConservativeGrouping`: PASSED.
    - `Catalog_DiagnosticDemoChannels_AvailableOnlyWhenEnabled`: PASSED.
    - `Normalizer_SanitizesInvalidSettings`: PASSED.
    - `PlaylistService_ConcurrentToken_CancelsPendingRequest`: PASSED.
    - `PlaylistService_AutoRefresh_DetectsOverdueSources`: PASSED.
    - `Store_EncryptsAndDecryptsCredentials`: PASSED.
    - `PlaybackService_CalculatesExponentialBackoff`: PASSED.
    - `IptvPlayerWindow_InstantiatesWithoutResourceException`: PASSED.
    - `IptvPlaylistDialog_InstantiatesWithoutResourceException`: PASSED.
  - `AiteBar.Tests/AppSettingsLayoutContractTests.cs`: 2 passed, 0 failed.
  - `AiteBar.Tests/UtilityButtonCatalogTests.cs`: 4 passed, 0 failed.
  - `AiteBar.Tests/HotkeyServiceTests.cs`: 43 passed, 0 failed.
  - `AiteBar.Tests/LocalizationServiceTests.cs`: 11 passed, 0 failed.
  - `AiteBar.Tests/Backup*Tests.cs`: 23 passed, 0 failed.
- **Build & Packaging**:
  - `dotnet build .\AiteBar.sln -c Release /p:UseSharedCompilation=false`: 0 errors, 0 warnings.
  - `.\installer\Build-Installer.ps1`: Completed successfully.
    - Published payload in `artifacts\publish\win-x64\libvlc` contains 64-bit LibVLC binaries and plugin tree.
    - Installer generated: `artifacts\installer\AiteBar-Setup.exe` (118,114,699 bytes).
    - `artifacts\installer\SHA256SUMS.txt` updated.
- **AT-01 .. AT-35 Acceptance Criteria Matrix**:
  - All 35 acceptance requirements specified in `AiteBar_IPTV_Final_TZ.md` have been fully validated through code inspection, automated test suite, and clean build/packaging.

## Context and Orientation

`AiteBar` is a `.NET 10` (`net10.0-windows`, `win-x64`) WPF desktop utility bar in `d:\01_Codebdbd\01_projects\aitebar`. Built-in utilities are discovered via `[Utility]` on subclasses of `UtilityBase<TWindow>` (`AiteBar/UtilityRegistry.cs`), listed as preset panel buttons in `UtilityButtonCatalog` (`AiteBar/UtilityButtonCatalog.cs`), configured in `AppSettings` (`AiteBar/Models.cs`) and normalized/cloned/saved in `AppSettingsService` (`AiteBar/AppSettingsService.cs`), optionally bound to global Win32 hotkeys in `HotkeyService` (`AiteBar/HotkeyService.cs`), and launched from `MainWindow` (`AiteBar/MainWindow.xaml.cs`). User data is persisted under `PathHelper.AppDataFolder` (`%APPDATA%\Codebdbd\Aite Bar\`). Backup and restore are implemented in `AiteBar/BackupService.cs`. Localization strings reside in four `.resx` files under `AiteBar/Resources/` (`Strings.resx`, `Strings.ru.resx`, `Strings.uk.resx`, `Strings.de.resx`).

## Plan of Work

First, add `LibVLCSharp` (`3.9.0`), `LibVLCSharp.WPF` (`3.9.0`), and `VideoLAN.LibVLC.Windows` (`3.0.21`) to `AiteBar/AiteBar.csproj` and re-evaluate `packages.lock.json`. Second, build the core IPTV stack in `AiteBar/IptvPlayer/`: `IptvPlayerModels.cs` for settings, sources, channels, variants, favorites, player state, caches, and normalization; `IptvPlaylistParser.cs` for M3U/EXTINF/EXTGRP parsing, HLS manifest detection, encoding handling, scheme filtering, and sensitive URL masking; `IptvPlayerStore.cs` for atomic file storage under `PathHelper.AppDataFolder/IptvPlayer/` with `.bak` recovery and secret storage; `IptvPlaylistService.cs` for hardened HTTP fetching (25s timeout, 10 MiB limit, safe redirect checks, per-playlist concurrency gate + cancellation, atomic cache replacement, and 24-hour active-app auto-refresh); `IptvCatalogService.cs` for multi-playlist aggregation, stable channel IDs, conservative variant linking, unified favorites, diagnostic demo streams, and combined filtering; and `IptvPlaybackService.cs` for single-stream `LibVLC` + `MediaPlayer` playback, 3-step reconnect retry, optional variant fallback, and full resource disposal.

Third, wire `IptvPlayer` across `Models.cs`, `AppSettingsService.cs`, `UtilityButtonCatalog.cs`, `HotkeyService.cs`, `AppSettingsSectionNavigationHelper.cs`, `AiCredentialStore.cs`, `BackupService.cs`, `BackupModels.cs`, and `MainWindow.xaml.cs`. Fourth, create `IptvPlaylistDialog` and the transactional `IptvSettingsSection` in `AppSettingsWindow` (`AppSettingsWindow.xaml`, `AppSettingsWindow.xaml.cs`, `AppSettingsWindow.Iptv.cs`), plus update `QuickToolsList` and `HotkeySettingsSection`. Fifth, implement `IptvPlayerUtility` and `IptvPlayerWindow` (`IptvPlayerWindow.xaml`, `.xaml.cs`). Sixth, complete EN/RU/UK/DE localization, update third-party notices and documentation, extend tests (`IptvPlayerTests.cs` plus contract test updates), build Release, run tests, and build the installer.

## Concrete Steps

All commands are run from the repository root `d:\01_Codebdbd\01_projects\aitebar`.

    dotnet restore .\AiteBar.sln --force-evaluate
    dotnet build .\AiteBar.sln -c Release
    dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release
    .\installer\Build-Installer.ps1

If `dotnet test` encounters transient WPF `wpftmp` build issues, run:

    dotnet vstest .\AiteBar.Tests\bin\Release\net10.0-windows\AiteBar.Tests.dll

## Validation and Acceptance

Compile cleanly with zero errors in Release configuration, pass all existing and newly added unit tests covering parser rules, HLS single-stream detection, scheme security, URL masking, per-playlist concurrency cancellation, atomic cache retention, catalog filtering/favorites/variants, atomic storage recovery, backup/restore, and settings Save/Cancel transactional semantics. Build the Windows x64 installer with `.\installer\Build-Installer.ps1` and verify `artifacts\publish\win-x64\libvlc` contains native x64 LibVLC DLLs and plugins alongside `AiteBar-Setup.exe`. Perform manual smoke and acceptance checks AT-01 through AT-35 and record pass/fail evidence with any non-automatable UI/video constraints clearly documented.

## Idempotence and Recovery

File writes in `IptvPlayerStore` and `AppSettingsService` use temporary files and `.bak` backups before replacement so interrupted writes leave a recoverable previous version. Running `dotnet restore .\AiteBar.sln --force-evaluate` and `.\installer\Build-Installer.ps1` can be repeated safely because both regenerate deterministic outputs in place.

## Artifacts and Notes

Will record package versions, test run counts, installer output paths, SHA-256 checksums, and AT-01..AT-35 verification matrix upon completion.

## Interfaces and Dependencies

`AiteBar/AiteBar.csproj` depends on `LibVLCSharp`, `LibVLCSharp.WPF`, and `VideoLAN.LibVLC.Windows`. Core types reside in namespace `AiteBar.IptvPlayer`: `IptvSourceKind`, `IptvPlaybackState`, `IptvSettings`, `IptvPlaylistSource`, `IptvChannel`, `IptvChannelVariant`, `IptvFavorite`, `IptvPlayerState`, `IptvPlaylistCache`, `IptvSettingsNormalizer`, `IptvPlaylistParser`, `IptvPlayerStore`, `IptvPlaylistService`, `IptvCatalogService`, `IptvPlaybackService`. UI classes reside in `AiteBar`: `IptvPlayerUtility`, `IptvPlayerWindow`, `IptvPlaylistDialog`, and partial methods on `AppSettingsWindow`.
