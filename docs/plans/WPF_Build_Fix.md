# Исправление локальных file-lock сбоев WPF/test build

This ExecPlan is a living document. The sections `Progress`, `Surprises & Discoveries`, `Decision Log`, and `Outcomes & Retrospective` must be kept up to date as work proceeds. This document follows `PLANS.md` from the repository root.

## Purpose / Big Picture

Локальная сборка AiteBar на Windows иногда падает с `Access denied` при записи generated-файлов WPF или тестового output. После исправления у разработчика и Codex должен быть воспроизводимый безопасный путь сборки, который не требует отключать антивирус, убивать все `dotnet.exe` процессы или удалять `bin/obj` перед каждой проверкой.

Основная команда workaround:

```powershell
.\Build-Safe.ps1 -Test
```

Она строит solution последовательно (`-m:1`), отключает Roslyn compiler server (`-p:UseSharedCompilation=false`) и при `-Test` запускает уже собранную test DLL через `dotnet vstest`, не повторяя MSBuild.

## Progress

- [x] (2026-06-20) Первичная проблема описана как `CS2012 _wpftmp` file lock.
- [x] (2026-06-20) Сверены актуальные симптомы: WPF `obj` lock и test `bin` lock от `Microsoft.CodeCoverage.targets`.
- [x] (2026-06-20) Проверен безопасный workaround: `dotnet build .\AiteBar.sln -c Release -m:1 -p:UseSharedCompilation=false` проходит успешно.
- [x] (2026-06-20) Добавлен `Build-Safe.ps1` как локальная команда безопасной сборки.
- [x] (2026-06-20) Уточнен test path: после safe build тесты запускаются через dotnet vstest по собранной DLL.
- [ ] Обновить проектные инструкции/документацию сборки, где требуется.
- [ ] При следующем устойчивом повторении проблемы собрать данные о блокирующем процессе через handle tooling или Process Monitor.

## Surprises & Discoveries

- Observation: Проблема шире, чем WPF `_wpftmp`. Обычный `dotnet build .\AiteBar.sln -c Release` также падал в тестовом проекте на `Microsoft.CodeCoverage.targets` при записи `.msCoverageSourceRootsMapping_AiteBar.Tests` в `AiteBar.Tests\bin\Release\net10.0-windows`.
  Evidence: `MSB3491: не удалось записать строки в файл ... .msCoverageSourceRootsMapping_AiteBar.Tests. Access to the path ... *.tmp~ is denied.`

- Observation: В `AiteBar\obj` накоплены WPF/markup артефакты, включая `_wpftmp`, `MarkupCompile`, `*.g.cs`.
  Evidence: локальная проверка нашла 241 таких файла.

- Observation: Во время анализа были живые процессы `dotnet`, `VBCSCompiler` и установленный `AiteBar.exe`.
  Evidence: `Get-Process dotnet,VBCSCompiler,AiteBar` показал активные процессы.

- Observation: Последовательная сборка без shared compiler прошла успешно без удаления `bin/obj` и без системных Defender exclusions.
  Evidence: `dotnet build .\AiteBar.sln -c Release -m:1 -p:UseSharedCompilation=false` завершился с 0 ошибок и 0 предупреждений.

## Decision Log

- Decision: Не считать Windows Defender доказанной единственной причиной.
  Rationale: Симптомы совместимы с antivirus/file scanning, но подтвержденный сбой возникал и в WPF `obj`, и в test `bin`. Без Process Monitor/handle-снимка нельзя утверждать, что root cause именно Defender.
  Date/Author: 2026-06-20 / Codex.

- Decision: Primary workaround — сериализовать MSBuild и отключить shared compilation.
  Rationale: Это проверенно устранило текущий сбой, не требует системных настроек, не убивает чужие процессы и не влияет на исходный код.
  Date/Author: 2026-06-20 / Codex.

- Decision: Не добавлять `GenerateTemporaryTargetAssemblyParallelism=1` в `AiteBar.csproj` первым шагом.
  Rationale: Это может помочь только WPF temporary target assembly, но не покрывает подтвержденный сбой `Microsoft.CodeCoverage.targets` в тестовом проекте.
  Date/Author: 2026-06-20 / Codex.

- Decision: Не рекомендовать `Get-Process dotnet | Stop-Process -Force` как обычную процедуру.
  Rationale: Команда слишком широкая и может завершить чужие .NET-процессы. Ее можно применять только вручную, когда понятно, какие процессы действительно застряли.
  Date/Author: 2026-06-20 / Codex.

## Outcomes & Retrospective

На текущем этапе проблема подтверждена как локальный generated-output file-lock, а не только как WPF `CS2012 _wpftmp`. Добавлен безопасный script `Build-Safe.ps1`, который использует проверенную комбинацию `-m:1 -p:UseSharedCompilation=false`. Это должно стать первым fallback для локальной разработки и Codex-проверок.

Если обычный `dotnet build` продолжит падать, это не обязательно означает ошибку кода. Сначала нужно повторить через `Build-Safe.ps1`. Если безопасная сборка тоже начнет падать, следующий шаг — диагностика конкретного блокирующего процесса, а не расширение workaround вслепую.

## Context and Orientation

Проект — WPF desktop-приложение на `.NET 10`:

- Solution: `AiteBar.sln`
- App project: `AiteBar/AiteBar.csproj`
- Test project: `AiteBar.Tests/AiteBar.Tests.csproj`
- App uses `<UseWPF>true</UseWPF>` and `<UseWindowsForms>true</UseWindowsForms>`.
- Test project also uses `<UseWPF>true</UseWPF>` and references app project.
- Test project includes `Microsoft.NET.Test.Sdk`, `xunit`, `xunit.runner.visualstudio`, `coverlet.collector`.

WPF build generates intermediate files in `obj`, including markup compile output and temporary target assemblies. Test/build tooling also writes source-root mapping files into test `bin` output.

## Plan of Work

1. Keep the standard CI commands unchanged unless CI reproduces the issue.
2. Use `Build-Safe.ps1` for local/Codex fallback when normal `dotnet build` or `dotnet test` fails with `Access denied` in generated output.
3. Document the safe fallback in `AGENTS.md` and technical docs if these commands should be visible to future agents/developers.
4. If fallback fails, collect evidence of the locking process before changing project files:
   - Sysinternals `handle.exe` for the denied path.
   - Process Monitor filtered by repository path and `ACCESS DENIED`.
   - Active `dotnet`, `VBCSCompiler`, antivirus/indexer processes.
5. Only after evidence points to antivirus/indexing, add targeted exclusions for generated folders.

## Concrete Steps

### Safe build

```powershell
.\Build-Safe.ps1
```

Equivalent raw command:

```powershell
dotnet build .\AiteBar.sln -c Release -m:1 -p:UseSharedCompilation=false
```

### Safe build plus tests

```powershell
.\Build-Safe.ps1 -Test
```

Equivalent raw commands:

```powershell
dotnet build .\AiteBar.sln -c Release -m:1 -p:UseSharedCompilation=false
dotnet vstest .\AiteBar.Tests\bin\Release\net10.0-windows\AiteBar.Tests.dll
```

### Optional targeted cleanup

Use only when generated-output locks persist after processes have exited:

```powershell
dotnet clean .\AiteBar.sln -c Release
Remove-Item -LiteralPath .\AiteBar\obj -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath .\AiteBar\bin -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath .\AiteBar.Tests\obj -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath .\AiteBar.Tests\bin -Recurse -Force -ErrorAction SilentlyContinue
```

### Optional targeted Defender exclusions

Use only if Process Monitor/handle evidence shows antivirus/indexing locks generated files. Prefer generated folders, not the whole repository:

- `D:\01_Codebdbd\01_projects\aitebar\AiteBar\obj`
- `D:\01_Codebdbd\01_projects\aitebar\AiteBar\bin`
- `D:\01_Codebdbd\01_projects\aitebar\AiteBar.Tests\obj`
- `D:\01_Codebdbd\01_projects\aitebar\AiteBar.Tests\bin`

## Validation and Acceptance

1. Normal test command may still pass or fail depending on local locks:

```powershell
dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release
```

2. Safe build must pass:

```powershell
.\Build-Safe.ps1
```

3. Safe build plus tests must pass:

```powershell
.\Build-Safe.ps1 -Test
```

4. Success criteria:
   - Build exits with code 0.
   - Tests exit with code 0.
   - No code changes are required to recover from transient generated-output locks.

## Idempotence and Recovery

`Build-Safe.ps1` is idempotent: it does not delete files, kill processes, alter Defender settings, or mutate project files. It only changes MSBuild execution mode for that invocation.

If it fails, the failure is likely a stronger external lock or a real build error. In that case, inspect the exact denied path and collect locking-process evidence before applying cleanup or exclusions.

## Artifacts and Notes

- `Build-Safe.ps1` — safe local build/test wrapper.
- `AGENTS.md` — should mention the safe fallback so Codex can use it after Access denied failures.
- CI can remain on the normal commands because the issue is local Windows generated-output locking unless CI reproduces it.

## Interfaces and Dependencies

No new NuGet packages are required. The workaround uses existing .NET/MSBuild switches:

- `-m:1` — serialize MSBuild project execution.
- `-p:UseSharedCompilation=false` — disable the shared Roslyn compiler server for the build invocation.
- `dotnet vstest` — run the already-built test assembly so test execution does not repeat the generated-output build step.
