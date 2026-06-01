# Extract Global Hotkey Service

This ExecPlan is a living document. The sections `Progress`, `Surprises & Discoveries`, `Decision Log`, and `Outcomes & Retrospective` must be kept up to date as work proceeds.

This document follows `PLANS.md` in the repository root.

## Purpose / Big Picture

AiteBar currently registers and handles global Windows hotkeys directly inside `AiteBar/MainWindow.xaml.cs`. After this change, global hotkey registration will live in a small `HotkeyService` that exposes explicit hotkey commands, maps `HotkeyBinding` values into Win32 registration data, and returns structured registration results. The visible behavior should remain the same: the configured global hotkeys still show the panel, switch panels, add a button, and launch quick tools. The improvement is demonstrated by unit tests that exercise the mapping and registration outcomes without opening WPF windows or calling the real Windows API.

## Progress

- [x] (2026-06-01 10:15Z) Created this ExecPlan and identified the current `MainWindow` registration path.
- [x] (2026-06-01 10:24Z) Added `HotkeyService`, explicit `HotkeyCommand` values, registration result types, and an injectable registrar.
- [x] (2026-06-01 10:27Z) Updated `MainWindow` to register through `HotkeyService` and dispatch `WM_HOTKEY` through `HotkeyCommand`.
- [x] (2026-06-01 10:30Z) Added xUnit tests for hotkey mapping, skipped registrations, failed registrations, command id lookup, and generated definitions.
- [x] (2026-06-01 10:47Z) Verified a clean temp copy with `dotnet build .\AiteBar.sln -c Release` and `dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release`; 303 tests passed.
- [x] (2026-06-01 11:05Z) Reviewed for regressions, duplication, and debt; consolidated hotkey command metadata and removed obsolete hotkey wrapper methods from `NativeIntegrationService`.

## Surprises & Discoveries

- Observation: `AiteBar/AssemblyInfo.cs` already grants `InternalsVisibleTo("AiteBar.Tests")`.
  Evidence: tests can cover internal helper types without making implementation-only APIs public.

- Observation: The live working tree has a local ACL/MSBuild issue in `AiteBar.Tests\obj` where `dotnet` cannot write generated files even though PowerShell can create files there. A clean copy under `%TEMP%` builds and tests successfully.
  Evidence: local standard build failed with `Access to the path 'D:\01_Codebdbd\01_projects\aitebar\AiteBar.Tests\obj\Release\net8.0-windows\AiteBar.Tests.GlobalUsings.g.cs' is denied`; the clean temp copy passed with `303` tests.

## Decision Log

- Decision: Keep command execution in `MainWindow` and move only registration, unregistration, mapping, and result aggregation into `HotkeyService`.
  Rationale: Showing/hiding the panel and opening WPF windows are UI responsibilities. Moving them into the service would make the service harder to test and increase coupling.
  Date/Author: 2026-06-01 / Codex

- Decision: Introduce a tiny `IHotkeyRegistrar` wrapper around `RegisterHotKey` and `UnregisterHotKey`.
  Rationale: The mapping and result behavior can then be tested with a fake registrar, while production still calls the existing Win32 API.
  Date/Author: 2026-06-01 / Codex

## Outcomes & Retrospective

Completed. Global hotkey registration is now centralized in `AiteBar/HotkeyService.cs`, with explicit `HotkeyCommand` values and structured registration results. `MainWindow` still owns UI behavior, but it no longer maps `HotkeyBinding` values to Win32 modifiers or hardcodes hotkey ids in its dispatch switch. Unit tests in `AiteBar.Tests/HotkeyServiceTests.cs` cover the pure mapping and fake-registration behavior without calling Win32 or constructing WPF windows.

Follow-up review cleanup consolidated the command/id/display-name metadata into one descriptor list inside `HotkeyService`, which avoids drift between command lookup and definition generation. The old `NativeIntegrationService.RegisterHotkey` and `NativeIntegrationService.UnregisterHotkey` methods were removed because they were unused after the extraction and duplicated the new registrar.

## Context and Orientation

The relevant application code is in `AiteBar/MainWindow.xaml.cs`, `AiteBar/Models.cs`, `AiteBar/HotkeyValidationHelper.cs`, `AiteBar/NativeMethods.cs`, and `AiteBar/NativeIntegrationService.cs`. A `HotkeyBinding` is a model with four modifier booleans (`Ctrl`, `Alt`, `Shift`, `Win`) and a WPF key string such as `D4` or `Space`. A global hotkey is a Windows-wide keyboard shortcut registered with the Win32 `RegisterHotKey` function. When Windows detects the shortcut, it sends `WM_HOTKEY` to the window, and `MainWindow.WndProc` decides which action to run based on the hotkey id.

Today, `MainWindow.RegisterGlobalHotkey` creates one binding for showing the panel, registers it plus six configured tool/context bindings, and returns localized names for failed registrations. `MainWindow.WndProc` maps numeric ids such as `9000` and `9004` to behaviors. `AppSettingsWindow` already validates missing modifiers and duplicate configured global hotkeys before saving settings.

## Plan of Work

Create `AiteBar/HotkeyService.cs`. Define an enum `HotkeyCommand` with explicit values for `ShowPanel`, `NextContext`, `PreviousContext`, `AddButton`, `QuickNote`, `ColorPicker`, and `TimerStopwatch`. Define a `HotkeyDefinition` record that carries a command, Win32 id, display name, and `HotkeyBinding`. Define a `HotkeyRegistrationData` record for mapped modifier and virtual-key values. Define `HotkeyRegistrationResult` to carry the command, display name, success flag, and an optional reason.

Add `IHotkeyRegistrar` with `RegisterHotkey(IntPtr hwnd, int id, uint modifiers, uint virtualKey)` and `UnregisterHotkey(IntPtr hwnd, int id)`. Add `Win32HotkeyRegistrar` that delegates to `NativeMethods.RegisterHotKey` and `NativeMethods.UnregisterHotKey`. Implement `HotkeyService.CreateDefinitions(AppSettings, Func<string,string>)`, `TryMapBinding`, `RegisterAll`, `UnregisterAll`, and `TryGetCommand`.

Update `MainWindow.xaml.cs` so the hotkey id constants and static mapping move out of the window. `MainWindow` should hold a `HotkeyService` field, call `_hotkeyService.RegisterAll(hwnd, definitions)` when hotkeys need registration, call `_hotkeyService.UnregisterAll(hwnd)` when unregistering, and use `_hotkeyService.TryGetCommand(wParam.ToInt32(), out var command)` in `WndProc`.

Add `AiteBar.Tests/HotkeyServiceTests.cs`. Use a fake registrar that records calls and can fail selected ids. Test that unassigned bindings are skipped as successful, assigned bindings without modifiers fail before registration, invalid key names fail before registration, valid bindings map to expected modifiers and virtual key, duplicate command definitions aggregate failure names correctly, and `TryGetCommand` maps the production ids.

## Concrete Steps

Work from `D:\01_Codebdbd\01_projects\aitebar`.

Run:

    dotnet build .\AiteBar.sln -c Release
    dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release

If `dotnet test` fails due to WPF temporary generated files, run:

    dotnet vstest .\AiteBar.Tests\bin\Release\net8.0-windows\AiteBar.Tests.dll

## Validation and Acceptance

The change is accepted when `dotnet build .\AiteBar.sln -c Release` succeeds and `dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release` succeeds. The new tests must prove that the hotkey mapping and registration result aggregation work without constructing `MainWindow` or calling Win32.

Manual behavior should remain unchanged: configured hotkeys still trigger the same actions because `MainWindow` still dispatches commands in `WndProc`.

## Idempotence and Recovery

The edits are additive except for replacing hotkey registration code in `MainWindow`. Re-running tests and build is safe. If the service extraction causes a behavior regression, restore the old registration path from git diff and keep the tests to guide a smaller extraction.

## Artifacts and Notes

Important files expected after completion:

- `AiteBar/HotkeyService.cs`
- `AiteBar.Tests/HotkeyServiceTests.cs`
- Updated `AiteBar/MainWindow.xaml.cs`

Validation transcript from clean temp copy:

    Сборка успешно завершена.
        Предупреждений: 0
        Ошибок: 0

    Пройден!   : не пройдено     0, пройдено   303, пропущено     0, всего   303, длительность 2 s. - AiteBar.Tests.dll (net8.0)

Validation transcript after review cleanup:

    Сборка успешно завершена.
        Предупреждений: 0
        Ошибок: 0

    Пройден!   : не пройдено     0, пройдено   303, пропущено     0, всего   303, длительность 2 s. - AiteBar.Tests.dll (net8.0)

## Interfaces and Dependencies

`HotkeyService` depends only on `AppSettings`, `HotkeyBinding`, `HotkeyValidationHelper`, WPF `KeyInterop`, and the injected `IHotkeyRegistrar`. Production registration depends on `NativeMethods`. Tests use a fake registrar and do not need a WPF window handle.
