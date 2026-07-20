# Make Quick Note closing durable and isolate test logs

This ExecPlan is a living document. The sections `Progress`, `Surprises & Discoveries`, `Decision Log`, and `Outcomes & Retrospective` must be kept up to date as work proceeds.

This document must be maintained in accordance with `PLANS.md` at the repository root.

## Purpose / Big Picture

Closing Quick Note must wait until the current note and window geometry have finished saving before any synchronization object is disposed. A user should be able to type, immediately close the window, reopen it, and see the final text without an `ObjectDisposedException` in `error.log`. Automated tests must also write expected exceptions only to a temporary test directory, never to the user's `%APPDATA%\Codebdbd\Aite Bar\error.log`.

## Progress

- [x] (2026-07-15 03:24Z) Correlated the smoke-test exception with `Window_Closing`, `OnClosed`, and `_saveSemaphore.Release`.
- [x] (2026-07-15 03:24Z) Mapped Quick Note save concurrency and all test-time `PathHelper` overrides.
- [x] (2026-07-15 03:27Z) Implemented two-phase Quick Note closing and made forced saves wait for an active save.
- [x] (2026-07-15 03:29Z) Added a delayed-save WPF regression test that closes the window while a save owns the semaphore.
- [x] (2026-07-15 03:29Z) Added a process-wide temporary app-data fallback for the test assembly and verified override restoration.
- [x] (2026-07-15 03:30Z) Passed 26 focused tests, a clean Release build, and all 638 tests; the production log timestamp remained unchanged.

## Surprises & Discoveries

- Observation: cancelling the first close event is necessary but not sufficient.
  Evidence: `SaveNowAsync(force: true)` currently calls `WaitAsync(0)` and returns immediately when another save owns the semaphore, so the window could still close and dispose the semaphore while that earlier save is running.

- Observation: automated tests use the production logger unless an individual test happens to install a `PathHelper` override.
  Evidence: the user log contains `AiteBar.Tests`, `Test crash`, `missing-target`, malformed test JSON, and locked test-file exceptions at the exact times of full-suite runs.

- Observation: Quick Note's generic application pack URI did not resolve in the WPF test host.
  Evidence: the first focused run failed before window construction with `IOException: Cannot locate resource 'resources/app.ico'`. Qualifying the URI with `AiteBar;component` preserved runtime behavior and allowed the real-window lifecycle test to run.

## Decision Log

- Decision: use a two-phase close rather than merely suppressing semaphore disposal.
  Rationale: keeping the semaphore alive would hide the exception but would not guarantee that the final note and geometry are saved before the WPF document is destroyed.
  Date/Author: 2026-07-15 / Codex

- Decision: make forced saves wait for the semaphore while ordinary timer saves retain their non-blocking coalescing behavior.
  Rationale: timer ticks should remain cheap, but close and explicit-open workflows require a durability barrier.
  Date/Author: 2026-07-15 / Codex

- Decision: give the test process a fallback app-data directory below the system temporary directory.
  Rationale: individual tests can continue to install narrower overrides and clear them; clearing then falls back to the test-process directory instead of the user's production directory.
  Date/Author: 2026-07-15 / Codex

## Outcomes & Retrospective

Quick Note now cancels the first close request, stops its timers, waits behind any active save, performs a forced final note save and geometry save, and only then authorizes a second close. `OnClosed` therefore disposes the semaphore only after every save has released it. Timer-driven saves remain non-blocking and coalesce as before.

The delayed persistence test proved that the window remains visible while the first active save is blocked, remains visible while the forced close-save is blocked, and closes only after both complete. Focused tests passed 26/26, the full suite passed 638/638, and Release build completed with zero warnings and errors.

The test assembly now installs a unique temporary app-data fallback at module initialization. Existing narrower overrides still work and clearing one returns to that fallback. The production log stayed at `2026-07-15T03:05:00.2029375Z` before focused tests, after focused tests, and after the complete suite; expected test exceptions were written beneath `%TEMP%\AiteBarTests\TestProcess` instead.

## Context and Orientation

`AiteBar/QuickNoteWindow.xaml.cs` handles WPF's synchronous `Closing` event with an `async void` method. WPF continues closing when that handler reaches its first incomplete await. `OnClosed` immediately calls `Dispose`, which disposes `_saveSemaphore`; the pending save later reaches its `finally` block and calls `Release`, producing the smoke-test exception.

`AiteBar/QuickNoteService.cs` performs Markdown persistence. Introducing a narrow internal interface for the operations used by the window will allow a focused WPF test to pause saves deterministically without slowing or corrupting real files.

`AiteBar/PathHelper.cs` resolves `error.log` beneath the user's roaming app-data directory unless a mutable test override is installed. A test-assembly module initializer will install a distinct process fallback before tests run. Existing per-test overrides remain higher priority.

## Plan of Work

Add an internal Quick Note persistence interface implemented by `QuickNoteService`, and allow `QuickNoteWindow` to receive that interface through an internal constructor while retaining its existing public constructor. This is a test seam only; production construction remains unchanged.

Change `SaveNowAsync` so `force: true` asynchronously waits for `_saveSemaphore`, while timer saves continue using `WaitAsync(0)` and coalescing. Add close-state flags. On the first `Closing`, cancel the event, stop save timers, wait for the forced note save and geometry save, then mark closing as authorized and call `Close` again. The second `Closing` must proceed synchronously. `OnClosed` may then dispose the semaphore safely. Make disposal idempotent.

Add a WPF test with a fake persistence service whose first save remains incomplete. Start that save, request close, and prove the window stays open. Complete the first save, prove the forced close-save begins and the window still stays open, then complete it and prove `Closed` fires without an exception.

Add a fallback override to `PathHelper` and a module initializer in `AiteBar.Tests` that points it at a unique temporary directory. Add a test showing a normal override can be cleared back to the test fallback. Record the production log's last-write timestamp before and after the focused and full test runs.

## Concrete Steps

Work from `D:\01_Codebdbd\01_projects\aitebar`.

Run focused tests:

    dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release --filter "FullyQualifiedName~QuickNoteWindowCloseTests|FullyQualifiedName~QuickNoteServiceTests|FullyQualifiedName~LoggerTests|FullyQualifiedName~PathHelperTests"

Then run:

    dotnet build .\AiteBar.sln -c Release
    dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release

## Validation and Acceptance

The delayed-save test must prove that `Closed` is not raised while either the active save or the forced close-save is incomplete, and that it is raised after both complete. The test must finish without an `ObjectDisposedException`.

The production `%APPDATA%\Codebdbd\Aite Bar\error.log` last-write timestamp must remain unchanged across focused and full test runs. Expected test exceptions must appear, if inspected, only beneath the unique temporary test root.

The Release build must report zero warnings and errors, and the full suite must pass. No Quick Note formatting, pin, conflict-copy, geometry-clamping, or Markdown behavior may change.

## Idempotence and Recovery

All changes are source-only. The test process uses a unique temporary directory and may safely leave it behind if process-exit cleanup cannot delete a file still being flushed. Do not delete or truncate the user's production log. Preserve all prior uncommitted changes.

## Artifacts and Notes

Smoke evidence:

    [2026-07-15 06:05:00] System.ObjectDisposedException: Cannot access a disposed object.
    Object name: 'System.Threading.SemaphoreSlim'.
    at AiteBar.QuickNoteWindow.SaveNowAsync(Boolean force) ... line 271
    at AiteBar.QuickNoteWindow.Window_Closing(...) ... line 121

## Interfaces and Dependencies

No package is added. `QuickNoteService` remains the production persistence implementation. The internal interface must expose only the members already consumed by `QuickNoteWindow`: external-change detection, note/conflict save and load, editor opening, conflict-copy opening, and the last conflict path.

`PathHelper.AppDataFolder` resolution order after this change is: an explicit override, then the test-process fallback override, then the normal roaming app-data path. The fallback setter remains internal and is available to `AiteBar.Tests` through the existing `InternalsVisibleTo` declaration.

Plan revision note (2026-07-15 03:24Z): created the initial self-contained plan from the smoke log and current save/test-path implementations.

Plan revision note (2026-07-15 03:30Z): recorded the completed two-phase lifecycle, WPF resource discovery, test-log isolation, and final validation evidence.
