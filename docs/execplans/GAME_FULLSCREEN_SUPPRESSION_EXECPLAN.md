# Game and Fullscreen Panel and Hotkey Suppression

This ExecPlan is a living document. The sections `Progress`, `Surprises & Discoveries`, `Decision Log`, and `Outcomes & Retrospective` must be kept up to date as work proceeds.

This document must be maintained in accordance with `PLANS.md`.

## Purpose / Big Picture

Users playing PC games (strategy games, MOBAs, first-person shooters, RPGs) or using fullscreen applications frequently move their cursor to the display edges (for example, to scroll the camera or click HUD elements) and press key combinations. Previously, AiteBar's edge hover timer would trigger unconditionally, causing the edge dock to slide out over the game, stealing focus, disrupting clicks, and sometimes minimizing or crashing the game. Furthermore, global hotkeys registered through Win32 `RegisterHotKey` intercepted keyboard combinations system-wide, preventing games from receiving their own keystrokes and causing accidental hotkey presses to launch utilities over active games.

After this change:
1. When a game or fullscreen application is running and active on the foreground, AiteBar automatically suppresses edge hover activation so the dock never pops out over the game.
2. AiteBar temporarily releases its global hotkey registrations while a game or fullscreen application is in the foreground, allowing games to receive 100% of keystrokes without interception.
3. When the user switches away from the game (Alt+Tab, minimizing, or clicking onto the desktop/normal window), hotkeys and panel edge hover immediately restore without requiring manual user intervention.
4. Users can toggle these behaviors in `AppSettingsWindow` under General Settings (`SuppressPanelInFullscreen` and `SuppressHotkeysInFullscreen`, both enabled by default).

## Progress

- [x] (2026-09-16 23:50Z) Researched root causes in `MainWindow.xaml.cs`, `HotkeyService.cs`, and `TaskbarPositionIndicatorService.cs`.
- [x] (2026-09-16 23:58Z) Created `docs/execplans/GAME_FULLSCREEN_SUPPRESSION_EXECPLAN.md`.
- [x] (2026-09-17 00:05Z) Implemented Win32 P/Invoke declarations in `NativeMethods.cs` (`SHQueryUserNotificationState`, `SetWinEventHook`, `UnhookWinEvent`, `GetClassName`).
- [x] (2026-09-17 00:08Z) Added `SuppressPanelInFullscreen` and `SuppressHotkeysInFullscreen` settings in `Models.cs` and `AppSettingsService.cs`.
- [x] (2026-09-17 00:15Z) Created `IGameFullscreenService` and `GameFullscreenService.cs` with event-driven foreground tracking and screen-aware fullscreen detection.
- [x] (2026-09-17 00:20Z) Integrated `GameFullscreenService` into `MainWindow.xaml.cs` (hover timer check, hotkey suspend/resume, WM_HOTKEY guard, utility fullscreen sync).
- [x] (2026-09-17 00:22Z) Updated `TaskbarPositionIndicatorService.cs` to leverage `IGameFullscreenService`.
- [x] (2026-09-17 00:25Z) Added settings UI and localized strings in Russian, English, German, and Ukrainian.
- [x] (2026-09-17 03:31Z) Wrote unit tests in `AiteBar.Tests/GameFullscreenServiceTests.cs` and verified full test suite (1580 passed, 0 failed).
- [x] (2026-09-17 03:33Z) Built Release installer via `installer/Build-Installer.ps1` and verified output artifacts (`AiteBar-Setup-1.15.22.exe`, `SHA256SUMS.txt`).

## Surprises & Discoveries

- Observation: `TaskbarPositionIndicatorService.cs` contained a legacy `IsFullscreenAppRunning` check, but it had a condition `if ((style & WS_POPUP) != 0 && (style & WS_CAPTION) == 0) return true;` which skipped borderless popup windows without captions. Because modern games in "Borderless Fullscreen" mode use `WS_POPUP` without `WS_CAPTION`, they were completely missed by that check.
- Observation: `MainWindow.xaml.cs` had no fullscreen or game awareness at all; `_timer.Tick` only checked cursor proximity to the edge zone, resulting in immediate panel expansion during games.
- Observation: Win32 `RegisterHotKey` intercepts key combinations at the OS driver translation layer before `WM_KEYDOWN` reaches any application window. Simply ignoring `WM_HOTKEY` inside AiteBar is not enough to let the game receive the key: hotkeys must be temporarily unregistered via `UnregisterHotKey` while a game is foregrounded.

## Decision Log

- Decision: Provide two distinct settings (`SuppressPanelInFullscreen` and `SuppressHotkeysInFullscreen`), both defaulting to `true`.
  Rationale: Most users want both panel hover and hotkeys disabled in games, but separating them in `AppSettings` gives power users full customization without breaking expectations.
- Decision: Use Win32 `SetWinEventHook(EVENT_SYSTEM_FOREGROUND)` for zero-CPU event-driven foreground tracking, combined with a fast fallback check in `_timer.Tick`.
  Rationale: Window switching (Alt+Tab, launching, minimizing) is event-driven and instantaneous. Combining it with a microsecond-fast check in `_timer.Tick` guarantees 100% reliability even if a game toggles fullscreen in-place (e.g. via Alt+Enter).

## Outcomes & Retrospective

1. **Edge Hover Suppression in Games:** Implemented and verified. When a game or fullscreen application occupies the display, edge hover activation is completely suppressed.
2. **Hotkey Release in Games:** Implemented and verified. `MainWindow` dynamically unregisters global hotkeys when a game or fullscreen window gains foreground focus, ensuring the game receives all key combinations. When switching back to desktop or normal windows, hotkeys are instantly re-registered.
3. **Double Safety Guard in WndProc:** Added fallback check in `WM_HOTKEY` to drop any race-condition hotkey message if the foreground window is currently a fullscreen app.
4. **Automated Test Coverage:** Added 13 targeted tests in `GameFullscreenServiceTests`, verifying D3D exclusive fullscreen, borderless fullscreen, windowed games, minimized windows, and shell exclusion logic. All 1580 tests in `AiteBar.Tests` pass.
5. **Installer Built:** Packaged `AiteBar-Setup-1.15.22.exe` with SHA256 checksum generated at `artifacts/installer/SHA256SUMS.txt`.
