# Harden asynchronous service coordination and preserve settings state

This ExecPlan is a living document. The sections `Progress`, `Surprises & Discoveries`, `Decision Log`, and `Outcomes & Retrospective` must be kept up to date as work proceeds.

This document must be maintained in accordance with `PLANS.md` at the repository root.

## Purpose / Big Picture

AiteBar should start telemetry exactly once even when initialization is requested concurrently, should never lose a queued log entry during a flush handoff, and should keep button settings isolated from accidental mutations. After this work, focused unit tests will demonstrate those concurrency and state-isolation guarantees, while the existing action execution behavior remains compatible with WPF's UI-thread requirements.

## Progress

- [x] (2026-07-15 06:24Z) Исправлено освобождение single-instance mutex не-владеющим вторым процессом и гонка асинхронного preview изображения.
- [x] (2026-07-15 06:26Z) Добавлены regression-тесты mutex ownership и version guard preview; общий focused-набор прошёл 7/7.
- [x] (2026-07-15 06:33Z) Завершены Release build без warnings/errors, полный suite 645/645, installer 1.11.1 с совпадающей checksum, чистый post-start log smoke и реальный smoke второго экземпляра.
- [x] (2026-07-15 05:33Z) Проведено предрелизное ревью текущего дерева: обнаружены потеря Quick Note при ошибке сохранения, гонка между telemetry initialization и shutdown, небезопасное игнорирование ошибки снятия mouse hook, лишний финализатор индикатора и несинхронизированная версия релиза.
- [x] (2026-07-15 05:52Z) Исправлены пять предрелизных замечаний и добавлен focused regression test для сценария отказа сохранения Quick Note.
- [x] (2026-07-15 05:55Z) Повторены Release build, полный test suite и сборка installer версии 1.11.1; новый publish запущен, production log не получил новых записей.
- [x] (2026-07-15 01:54Z) Re-read `PLANS.md`, inspected the affected services, tests, utility contract, and the pre-existing `ActionService.cs` working-tree diff.
- [x] (2026-07-15 01:54Z) Confirmed that `UpdateSettings` intentionally preserves `Elements`; the apparent redundant work cannot be removed without redesigning the public mutation contract.
- [x] (2026-07-15 02:00Z) Implemented shared-task and shutdown-safe telemetry initialization.
- [x] (2026-07-15 02:00Z) Made the logger drain-to-idle transition atomic.
- [x] (2026-07-15 02:03Z) Added focused telemetry, logger, and settings state-isolation tests.
- [x] (2026-07-15 02:04Z) Normalized the accidental indentation in the existing `ActionService.cs` diff and verified its continuation behavior against `UtilityBase<TWindow>`.
- [x] (2026-07-15 02:12Z) Isolated app-wide localization state in tests and removed an order-dependent modal utility-test hang discovered during full validation.
- [x] (2026-07-15 02:14Z) Completed Release build with zero warnings/errors and the full test suite with 625 passing tests.

## Surprises & Discoveries

- Observation: второй экземпляр создавал handle существующего named mutex без владения, а `OnExit` безусловно вызывал `ReleaseMutex`.
  Evidence: production `error.log` содержит `ApplicationException: Object synchronization method was called from an unsynchronized block of code` из `App.OnExit`; значение `createdNew` ранее не сохранялось.

- Observation: несколько вызовов `UpdatePreview()` могли завершить файловое чтение не по порядку.
  Evidence: обработчик присваивал результат каждого `await` без проверки, что путь и поколение запроса всё ещё актуальны.

- Observation: `AppSettingsService.UpdateSettings` is deliberately not a general mutation API for `Elements`.
  Evidence: `AiteBar.Tests/AppSettingsServiceTests.cs` contains `UpdateSettings_PreservesCurrentElementsWhenUpdatingStaleSettingsFields`, which clears `next.Elements` and expects the stored element to remain.

- Observation: the current `ActionService.cs` changes predate this plan and combine `ConfigureAwait(false)` additions with accidental indentation drift.
  Evidence: `git status --short` reports only `M AiteBar/ActionService.cs`, and its diff shows no prior reliability-service changes.

- Observation: WPF build verification is currently blocked by access failures for generated `obj` and `*_wpftmp.csproj` files.
  Evidence: both the initial normal build and an isolated intermediate-output build failed with `UnauthorizedAccessException`. Running the required build outside the filesystem sandbox succeeded with zero warnings and errors.

- Observation: tests that changed `CultureInfo.CurrentCulture` no longer controlled application localization after global culture mutation was removed from `LocalizationService`.
  Evidence: filtered tests expected `Панель 1` but observed `Leiste 1`, left in the static `LocalizationService.ResolvedCulture` by a parallel test. A non-parallel localization-state collection and explicit apply/restore behavior made the full suite deterministic.

- Observation: the full suite could hang after a WPF `Application` had been created because `UtilityBase_LaunchAsync_HandlesExceptions` entered the production modal error dialog.
  Evidence: the run stopped after `UtilityRegistryTests`; adding an overridable error-presentation method let the crashing test suppress only the dialog while retaining production behavior. The next full run completed all 625 tests in eight seconds.

## Decision Log

- Decision: хранить факт владения mutex отдельно от handle и освобождать mutex только владельцем, но всегда вызывать `Dispose`.
  Rationale: второй процесс должен закрыть собственный handle без попытки освободить синхронизационный объект первого процесса.
  Date/Author: 2026-07-15 / Codex

- Decision: защищать preview монотонной версией запроса и снимком пути.
  Rationale: чтение файла остаётся асинхронным, а поздно завершившийся старый запрос больше не может перезаписать текущий UI.
  Date/Author: 2026-07-15 / Codex

- Decision: выпустить накопленные после `v1.11.0` исправления как patch-версию `1.11.1`.
  Rationale: локальный тег `v1.11.0` уже существует, поэтому повторная сборка новых исходников с тем же номером создаст неоднозначные installer и Sentry release identifiers.
  Date/Author: 2026-07-15 / Codex

- Decision: при ошибке сохранения Quick Note отменять закрытие окна, но не блокировать закрытие только из-за ошибки сохранения геометрии.
  Rationale: содержимое заметки является пользовательскими данными и не должно теряться; координаты окна являются восстанавливаемой настройкой и не должны запирать пользователя в окне.
  Date/Author: 2026-07-15 / Codex

- Decision: перед созданием Sentry handle проверять не только cancellation token, но и принадлежность initialization source текущему поколению.
  Rationale: `Shutdown` очищает текущее поколение под lock; identity check закрывает промежуток до фактического вызова `Cancel` без выполнения пользовательских cancellation callbacks под lock.
  Date/Author: 2026-07-15 / Codex

- Decision: do not remove the second `CloneAppSettings` call or allow `UpdateSettings` delegates to replace `Elements` in this change.
  Rationale: the second clone prevents a caller from retaining the mutable object passed to the callback and changing live state later. The existing preservation test defines the current contract. A future optimization requires a typed settings mutation API rather than deleting isolation defensively.
  Date/Author: 2026-07-15 / Codex

- Decision: coordinate telemetry initialization with one shared `Task` and a cancellation token owned by the current initialization generation.
  Rationale: every concurrent caller must observe completion of the same work, while synchronous `Shutdown` must prevent delayed initialization from enabling Sentry afterward.
  Date/Author: 2026-07-15 / Codex

- Decision: keep the logger's queue-and-worker architecture but make the empty-queue check and `_isFlushing` transition under one lock.
  Rationale: this is the smallest change that closes the lost-wakeup window without introducing a lifetime-managed background channel into application shutdown.
  Date/Author: 2026-07-15 / Codex

- Decision: serialize tests that mutate `LocalizationService.ResolvedCulture` in a collection marked `DisableParallelization = true` and make each test restore the prior resolved culture.
  Rationale: application culture is intentionally process-wide. Changing only the thread's `CultureInfo` is no longer a valid way to control it, and parallel mutation makes otherwise unrelated assertions order-dependent.
  Date/Author: 2026-07-15 / Codex

- Decision: add `UtilityBase<TWindow>.ShowUnavailableMessageAsync` as an overridable presentation seam.
  Rationale: production still shows the same modal error dialog, while tests of exception containment can replace presentation and finish without user interaction after WPF initialization.
  Date/Author: 2026-07-15 / Codex

## Outcomes & Retrospective

Заключительный проход устранил production-исключение второго экземпляра: `OnExit` освобождает mutex только при подтверждённом владении и всегда закрывает локальный handle. Асинхронное preview теперь использует generation guard. Дополнительно локальная сборка installer больше не оставляет устаревший `SHA256SUMS.txt`: checksum вычисляется после подписи и записывается через временный файл.

Финальная Release-сборка завершилась без предупреждений и ошибок; полный suite прошёл 645/645. Installer версии `1.11.1` имеет SHA-256 `2414F9DE3E580AA66540A22CBCC5508430F33FE60DBF721C7CEBC93D41A4E38A`, совпадающий с manifest. Publish запущен как PID 25192. Реальный второй экземпляр PID 18092 показал и закрыл системный диалог, штатно завершился, основной процесс остался жив, а `error.log` не изменился и сохранил старую последнюю запись от 09:15:34.

Предрелизное продолжение плана завершено. Quick Note теперь сохраняет окно открытым после неудачной финальной записи и позволяет повторить закрытие; отдельный WPF-тест доказывает этот сценарий. Telemetry commit проверяет принадлежность текущему initialization generation, Win32 unhook сохраняет delegate при ошибке, а бесполезный финализатор индикатора удалён. Версия приложения, assembly metadata, installer и changelog синхронизированы на `1.11.1`.

Финальная Release-сборка завершилась без предупреждений и ошибок, focused-набор прошёл 21 тест, полный suite прошёл 639 тестов. `installer/Build-Installer.ps1` создал `artifacts/installer/AiteBar-Setup.exe` с ProductVersion `1.11.1`; SHA-256 полученного файла равен `93395F173958979B5EE246CB9FFD8A6F3E8A19F30BD08478DBEA6AFA6CCE73C5`. Новый publish был запущен после сборки, а production `error.log` сохранил прежнее время последней записи `2026-07-15 06:05:00`, то есть запуск не добавил исключений.

The reliability hardening is complete. Concurrent telemetry callers now await the same initialization task, shutdown cancels pending initialization before Sentry can be enabled, and logger flush handoff cannot strand an entry. `UpdateSettings` now restores authoritative elements after the callback and retains its second clone for reference isolation. The pre-existing `ActionService` continuation changes compile cleanly and its accidental indentation was normalized.

Validation also repaired two test-infrastructure defects exposed by the work: process-wide localization is now explicitly isolated, and the utility exception test no longer opens a modal dialog. Release build completed with zero warnings and errors; all 625 tests passed. No application UI geometry or behavior was changed, so the four-edge manual panel checklist was not required for this service-only change.

## Context and Orientation

`AiteBar/TelemetryService.cs` is a static wrapper around Sentry. It currently sets a Boolean initialized flag before asynchronous settings-file reads. A second caller therefore returns without waiting, and `Shutdown` can reset state while the first caller later enables Sentry.

`AiteBar/Logger.cs` puts formatted log entries in a `ConcurrentQueue<string>` and starts a background flush task. A lost wakeup is possible when the worker sees an empty queue just before a producer enqueues while `_isFlushing` is still true; that producer declines to start a worker, and the old worker then marks itself idle.

`AiteBar/AppSettingsService.cs` exposes cloned settings and keeps a separate `_elements` list. `UpdateSettings` applies a callback to a clone, restores the authoritative elements, and clones again before publishing state. Although expensive, this protects state isolation and matches existing tests.

`AiteBar/ActionService.cs` contains pre-existing uncommitted edits. `ConfigureAwait(false)` is safe for process launching, delays, file persistence, and injected keyboard input, but WPF utility windows themselves must continue to be created on the Dispatcher thread. `UtilityBase<TWindow>.LaunchAsync` awaits its hide callback without suppressing the captured WPF context, so the outer `StartUtilityAsync` await configuration does not move window creation off the UI thread.

## Plan of Work

Refactor `TelemetryService.InitializeAsync` into a small synchronized entry point plus an asynchronous core. The synchronized entry point returns the existing initialization task when present. The core reads configuration with a cancellation token, computes options, then enters the service lock before installing the Sentry handle. `Shutdown` cancels the active generation, clears its task, and disposes the handle under the same lock. Cancellation caused by shutdown is treated as successful shutdown rather than an application error.

Change the logger worker so that after writing each drained batch it locks `_flushLock`. If the queue is empty, it marks the worker idle and completes waiters while still holding that lock; otherwise it leaves `_isFlushing` true and drains again. Producers enqueue before acquiring the same lock in `FlushQueue`, which makes the handoff race-free.

Add telemetry tests that lock the settings file to keep initialization pending. One test proves that two callers share pending completion; another calls `Shutdown` during the pending read and proves telemetry stays disabled after the read is released. Add a logger concurrent-burst test that waits for quiescence and verifies every unique entry was persisted. Strengthen the settings test to prove that retaining the callback object cannot mutate published settings or elements after `UpdateSettings` returns.

Normalize only the malformed indentation in `ActionService.cs`; preserve the user's `ConfigureAwait(false)` edits after confirming the utility path captures the WPF context internally. Do not change utility behavior in this plan.

## Concrete Steps

Work from `D:\01_Codebdbd\01_projects\aitebar`.

Edit `AiteBar/TelemetryService.cs`, `AiteBar/Logger.cs`, and their focused test files using small patches. Update this plan after each milestone.

Run focused tests first:

    dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release --filter "FullyQualifiedName~TelemetryServiceTests|FullyQualifiedName~LoggerTests|FullyQualifiedName~AppSettingsServiceTests"

Then run the repository checks:

    dotnet build .\AiteBar.sln -c Release
    dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release

If WPF/MSBuild fails on generated temporary files but a current test assembly exists, run:

    dotnet vstest .\AiteBar.Tests\bin\Release\net10.0-windows\AiteBar.Tests.dll

## Validation and Acceptance

The telemetry concurrency test must observe that a second `InitializeAsync` task remains incomplete while the first is blocked on the same settings read, then both complete after the file lock is released. The shutdown test must leave `TelemetryService.IsEnabled` false even when a pending configuration contains a valid DSN.

The logger burst test must write every unique marker and `WaitForFlushAsync` must not return while an entry remains queued. Existing rotation and newline-normalization tests must continue to pass.

The settings isolation tests must show that changing the object retained from an `UpdateSettings` callback after the method returns does not change `service.Settings` or `service.Elements`.

The full Release build and test suite should finish with zero errors. If environmental WPF file locking prevents that, record the exact failure and ensure no generated or unrelated files are included in the working-tree diff.

## Idempotence and Recovery

All edits are source-only and repeatable. Tests use unique temporary directories and clear telemetry environment variables and path overrides during disposal. Do not delete or reset the pre-existing `ActionService.cs` changes. If a build leaves generated temporary files locked, stop the active AiteBar/MSBuild process before retrying; do not remove user files or reset the worktree.

## Artifacts and Notes

The initial working tree contains:

    M AiteBar/ActionService.cs

No other pre-existing edits were observed. Final evidence will be appended here.

Final validation evidence:

    dotnet build .\AiteBar.sln -c Release
    Build succeeded. 0 Warning(s), 0 Error(s).

    dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release
    Passed: 625, Failed: 0, Skipped: 0.

## Interfaces and Dependencies

No new package is required. `TelemetryService.InitializeAsync` remains `public static Task InitializeAsync()`, and `Shutdown` remains synchronous. `Logger.Log`, `Logger.LogAsync`, and `Logger.WaitForFlushAsync` retain their signatures. `AppSettingsService.UpdateSettings(Action<AppSettings>)` retains its current elements-preservation contract.

Plan revision note (2026-07-15 01:54Z): created the initial self-contained plan after code and test inspection; recorded the settings-contract discovery so a future contributor does not remove required isolation as a micro-optimization.

Plan revision note (2026-07-15 02:14Z): marked implementation and validation complete; added the localization contamination and modal test-hang discoveries, their design decisions, and final Release evidence.

Plan revision note (2026-07-15 05:33Z): reopened the plan for pre-release hardening after code review; recorded the five findings, the 1.11.1 version decision, and the required validation steps.

Plan revision note (2026-07-15 05:55Z): marked all pre-release findings resolved and recorded focused/full test results, synchronized artifact versions, installer hash, application restart, and clean post-start production log evidence.

Plan revision note (2026-07-15 06:33Z): added the final mutex, preview-generation, and checksum fixes with 645-test Release and clean startup evidence.
