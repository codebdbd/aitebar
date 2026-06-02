# Cover ActionService Async Paths With Real Tests

This ExecPlan is a living document. The sections `Progress`, `Surprises & Discoveries`, `Decision Log`, and `Outcomes & Retrospective` must be kept up to date as work proceeds.

This document is maintained in accordance with [PLANS.md](D:/01_Codebdbd/01_projects/aitebar/PLANS.md).

## Purpose / Big Picture

After this change, the async execution paths inside `AiteBar/ActionService.cs` will be covered by real unit tests instead of being hidden behind class-level aggregate coverage. A contributor will be able to run the Release test suite with coverage collection and see non-zero coverage for `ExecuteHotkeyAsync`, `ExecuteWebActionAsync`, `StartCalculatorAsync`, `StartColorPickerAsync`, `StartQuickNoteAsync`, `StartTimerStopwatchAsync`, and `TryEnterFullscreenAsync`.

## Progress

- [x] (2026-06-02 03:45Z) Re-read `PLANS.md`, re-checked the latest Cobertura report, and confirmed the async state machine classes for `ActionService` are still at `0%`.
- [x] (2026-06-02 03:52Z) Introduced narrow runtime and window abstractions in `AiteBar/ActionService.cs` and wired production implementations through the existing public constructor.
- [x] (2026-06-02 03:52Z) Updated `QuickNoteWindow`, `TimerStopwatchWindow`, and `ScreenColorPickerWindow` to satisfy the new internal ActionService interfaces without changing user-visible behavior.
- [x] (2026-06-02 03:53Z) Added focused tests in `AiteBar.Tests/ActionServiceTests.cs` for hotkey execution, web launch plus fullscreen, calculator, color picker, quick note reuse, timer reuse, screenshot, recording, explorer, and downloads.
- [x] (2026-06-02 03:53Z) Ran `dotnet build .\AiteBar.sln -c Release` successfully.
- [x] (2026-06-02 03:53Z) Ran `dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release` successfully with `368` passed tests.
- [x] (2026-06-02 03:53Z) Ran `dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release --collect:"XPlat Code Coverage" --results-directory .\TestResults` successfully and recorded the updated async-method coverage evidence below.

## Surprises & Discoveries

- Observation: The class-level `ActionService` line-rate was high enough to look healthy while every important async state machine stayed at `0%`.
  Evidence: `coverage.cobertura.xml` currently reports `AiteBar.ActionService|82.6%`, but `AiteBar.ActionService/<ExecuteHotkeyAsync>d__8|0|0` and the other async state machines are also `0|0`.

- Observation: A parallel coverage run produced a valid test pass with an empty Cobertura file (`lines-valid="0"`), so coverage collection needed a standalone rerun.
  Evidence: `TestResults\c02bfe99-4bbd-46c7-9475-3bd4543db1d2\coverage.cobertura.xml` contains only `<packages />`, while the standalone rerun in `37c3c7dd-d2e1-4bfd-aa27-1d9bee2e81c5` contains the expected class entries.

## Decision Log

- Decision: Fix `ActionService` coverage by introducing minimal injectable seams instead of using reflection-only probes or synthetic wrapper tests.
  Rationale: The user explicitly asked for real coverage. The async methods need to run end-to-end in tests, which requires controlling process launch, delay, Win32 keyboard input, and window creation.
  Date/Author: 2026-06-02 / Codex

- Decision: Reuse the existing WPF window classes by making them implement narrow internal interfaces rather than creating separate adapter classes.
  Rationale: The WPF windows already expose the needed behavior (`ShowSliding`, `ShowNearPanel`, `ShowDialog`, `IsVisible`, `Closed`, `Activate`). Letting them implement the interfaces kept the refactor small and preserved production behavior.
  Date/Author: 2026-06-02 / Codex

- Decision: Verify coverage using the compiler-generated async state machine class names, even when ordinal suffixes changed after refactoring.
  Rationale: Refactoring changed `ExecuteHotkeyAsync` from `<...>d__8` to `<...>d__10`, so method-name matching had to stay semantic rather than relying on the old generated-number suffix.
  Date/Author: 2026-06-02 / Codex

## Outcomes & Retrospective

The targeted async paths in `ActionService` are now covered with real tests that execute the methods themselves through controllable fakes. The originally disputed methods now report non-zero coverage, and most of them are fully covered at line level. The remaining `0%` async path inside `ActionService` is `StartScriptFileAsync`, which was not one of the user-reported gaps and still depends on modal dialog confirmation plus real process launch.

## Context and Orientation

The target file is `AiteBar/ActionService.cs`. It currently mixes business decisions with hard dependencies on `Process.Start`, `Task.Delay`, `NativeMethods.SendInput`, `NativeMethods.SetForegroundWindow`, `GetAsyncKeyState`, and direct construction of `QuickNoteWindow`, `TimerStopwatchWindow`, and `ScreenColorPickerWindow`. This makes the public methods easy to call but hard to verify because the interesting work happens inside private async methods that immediately reach out to Windows.

The existing test file `AiteBar.Tests/ActionServiceTests.cs` already covers argument-building helpers such as `BuildWebActionProcessStartInfo`, script launch setup, and a few safe helper paths. Those tests do not execute the async methods that the Cobertura report tracks as nested compiler-generated classes named like `<ExecuteHotkeyAsync>d__8`.

The practical way to cover those methods is to keep `ActionService` as the orchestrator but move system interaction behind narrow internal interfaces that have a production implementation in the same file or a nearby file. Tests can then inject fakes, call the real async methods, and assert which inputs were sent, which windows were shown, and which delays were requested.

## Plan of Work

First, define a small runtime abstraction for `ActionService`. It must own the side effects that currently block testing: starting a process from `ProcessStartInfo`, starting a process from a file name, waiting with `Task.Delay`, checking whether a key is already pressed, sending keyboard input arrays, bringing a window to the foreground, and creating the three UI windows used by the quick tools. Keep the abstraction internal and default it from the existing `ActionService(AppSettingsService settingsService)` constructor so production callers do not change.

Next, add lightweight interfaces for the windows and launched process handles that `ActionService` actually needs. For a launched process, only `MainWindowHandle`, `Refresh()`, and `Dispose()` are required. For quick-note and timer windows, the service only needs `IsVisible`, `Activate()`, an event for `Closed`, and the specific show method (`ShowSliding` or `ShowNearPanel`). For the color picker, the service only needs `ShowDialog()`. Use adapters around the existing WPF window classes so their runtime behavior stays unchanged.

Then update `ActionService` to call the runtime abstraction everywhere the async methods currently hit Windows directly. Keep helper logic and public method names intact. Do not broaden the refactor beyond what is required for testability.

Finally, extend `AiteBar.Tests/ActionServiceTests.cs` with fakes for the runtime, process handle, quick note window, timer window, and color picker window. Add tests that call the real async methods and verify observable effects: hotkey inputs and delays are sent, web launch persists profile and optionally enters fullscreen, calculator launch requests `calc.exe`, color picker waits then shows, quick note and timer windows are created once and re-activated on repeated calls, and fullscreen handling polls until a handle appears then sends `F11`.

## Concrete Steps

Work from `D:\01_Codebdbd\01_projects\aitebar`.

1. Edit `AiteBar/ActionService.cs` to add the internal interfaces, production adapters, and constructor injection.
2. Edit `AiteBar.Tests/ActionServiceTests.cs` to add fake implementations and direct tests for the async methods.
3. Run:

       dotnet build .\AiteBar.sln -c Release

4. Run:

       dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release --collect:"XPlat Code Coverage" --results-directory .\TestResults

5. Inspect the latest Cobertura file and record the relevant `AiteBar.ActionService/<...>` entries in this plan.

## Validation and Acceptance

Acceptance is reached when the Release build succeeds, the Release test suite passes, and the latest Cobertura report shows non-zero coverage for these compiler-generated classes:

- `AiteBar.ActionService/<ExecuteHotkeyAsync>d__8`
- `AiteBar.ActionService/<ExecuteWebActionAsync>d__11`
- `AiteBar.ActionService/<StartCalculatorAsync>d__18`
- `AiteBar.ActionService/<StartColorPickerAsync>d__21`
- `AiteBar.ActionService/<StartQuickNoteAsync>d__22`
- `AiteBar.ActionService/<StartTimerStopwatchAsync>d__23`
- `AiteBar.ActionService/<TryEnterFullscreenAsync>d__28`

The tests must be real behavior tests. They must execute the actual async methods and assert the effects that matter to the service, not only helper methods or reflection probes.

## Idempotence and Recovery

The refactor is safe to repeat because the production constructor remains the default entry point. The tests will use only in-memory fakes and temporary files. If a test run leaves a `TestResults` directory behind, rerunning the same commands is fine because the toolchain writes a new GUID-named subdirectory each time.

## Artifacts and Notes

Initial evidence before the refactor:

    AiteBar.ActionService/<ExecuteHotkeyAsync>d__8|0|0
    AiteBar.ActionService/<ExecuteWebActionAsync>d__11|0|0
    AiteBar.ActionService/<StartCalculatorAsync>d__18|0|0
    AiteBar.ActionService/<StartColorPickerAsync>d__21|0|0
    AiteBar.ActionService/<StartQuickNoteAsync>d__22|0|0
    AiteBar.ActionService/<StartTimerStopwatchAsync>d__23|0|0
    AiteBar.ActionService/<TryEnterFullscreenAsync>d__28|0|0

Final evidence after the refactor and new tests:

    AiteBar.ActionService/<ExecuteHotkeyAsync>d__10|82|76.9
    AiteBar.ActionService/<ExecuteWebActionAsync>d__11|100|83.3
    AiteBar.ActionService/<StartCalculatorAsync>d__18|100|100
    AiteBar.ActionService/<StartColorPickerAsync>d__21|100|50
    AiteBar.ActionService/<StartQuickNoteAsync>d__22|87.5|83.3
    AiteBar.ActionService/<StartTimerStopwatchAsync>d__23|87.5|83.3
    AiteBar.ActionService/<TryEnterFullscreenAsync>d__28|100|100
    AiteBar.ActionService/<StartScreenshotAsync>d__16|100|50
    AiteBar.ActionService/<StartRecordVideoAsync>d__17|100|50
    AiteBar.ActionService/<StartExplorerAsync>d__19|100|50
    AiteBar.ActionService/<StartDownloadsAsync>d__20|100|50

Validation transcripts:

    dotnet build .\AiteBar.sln -c Release
    Build succeeded. 0 warnings, 0 errors.

    dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release
    Passed!  Failed: 0, Passed: 368, Skipped: 0, Total: 368

## Interfaces and Dependencies

In `AiteBar/ActionService.cs`, define narrow internal interfaces and adapters along these lines:

    internal interface IActionServiceRuntime
    {
        Task DelayAsync(int milliseconds);
        bool IsKeyPressed(byte virtualKey);
        uint SendInput(NativeMethods.INPUT[] inputs);
        bool SetForegroundWindow(IntPtr handle);
        IActionProcessHandle? StartProcess(ProcessStartInfo startInfo);
        IActionProcessHandle? StartProcess(string fileName);
        IColorPickerDialog CreateColorPickerDialog(Window? owner);
        IQuickNoteToolWindow CreateQuickNoteWindow(AppSettingsService settingsService, Window? owner);
        ITimerStopwatchToolWindow CreateTimerStopwatchWindow(Window? owner);
        Window? GetMainWindow();
    }

    internal interface IActionProcessHandle : IDisposable
    {
        IntPtr MainWindowHandle { get; }
        void Refresh();
    }

    internal interface IColorPickerDialog
    {
        bool? ShowDialog();
    }

    internal interface IQuickNoteToolWindow
    {
        bool IsVisible { get; }
        event EventHandler? Closed;
        void Activate();
        void ShowSliding(AppSettings settings);
    }

    internal interface ITimerStopwatchToolWindow
    {
        bool IsVisible { get; }
        event EventHandler? Closed;
        void Activate();
        void ShowNearPanel(AppSettingsService settingsService);
    }

The production implementation must delegate to the current WPF windows and `System.Diagnostics.Process` without changing the user-visible behavior.

Revision note: created this ExecPlan after verifying that `ActionService` async state machines were still at `0%` despite the class-level aggregate appearing healthy. The plan exists to drive a narrow refactor and real async coverage work.
