# Test Coverage Report

**Date**: 2026-06-02  
**Generated at**: 2026-06-02 04:25Z  
**Build**: `dotnet build .\AiteBar.sln -c Release`  
**Test Run**: `dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release --collect:"XPlat Code Coverage" --results-directory .\TestResults`  
**Coverage Artifact**: [TestResults/50aac6e4-bf9e-44e5-823a-e6ae172fd010/coverage.cobertura.xml](D:/01_Codebdbd/01_projects/aitebar/TestResults/50aac6e4-bf9e-44e5-823a-e6ae172fd010/coverage.cobertura.xml)

## Executive Summary

Current test coverage remains split between well-covered business logic and mostly uncovered WPF UI. The overall Cobertura line coverage is **33.4%** because the report includes large UI surfaces and many generated classes. The important business-logic and helper classes are in much better shape, with the main service layer largely above 80%.

The previously disputed `ActionService` async paths are no longer at `0%`. The methods for hotkeys, web launch, calculator, color picker, quick note, timer/stopwatch, fullscreen entry, screenshot, recording, explorer, and downloads all now have direct coverage.

## Raw Cobertura Summary

These numbers are taken directly from the Cobertura root node and include compiler-generated async state machines, XAML partials, and generated support classes.

- **Line Coverage**: `33.4%` (`2089 / 6261`)
- **Branch Coverage**: `27.2%` (`931 / 3418`)
- **Class Entries**: `197`
- **Files**: `61`
- **Tests Passed**: `382 / 382`

## Interpreting the Numbers

Cobertura counts many things that are not useful to read as “real classes” in a WPF application:

- compiler-generated async state machines such as `AiteBar.ActionService/<StartQuickNoteAsync>d__22`
- XAML-generated partial classes
- generated helper types under `obj/`
- small record-like transport objects that naturally reach `100%`

For that reason, the normalized “top-level project classes” view below excludes:

- generated classes under `obj/*`
- compiler-generated nested async/state-machine classes whose names contain `/<`
- `Sentry.*` and `XamlGeneratedNamespace.*`

That normalized view is better for discussing the actual codebase.

## Top-Level Project Class Summary

Normalized top-level project classes counted: **94**

### Excellent Coverage (>= 90%)

| Class | Line | Branch |
|---|---:|---:|
| ActionExecutionResult | 100.0% | 100.0% |
| AppSettings | 100.0% | 100.0% |
| BrowserProfileInfo | 100.0% | 100.0% |
| CustomElement | 100.0% | 100.0% |
| EasingHelper | 100.0% | 100.0% |
| HotkeyBinding | 100.0% | 100.0% |
| HotkeyDefinition | 100.0% | 100.0% |
| HotkeyRegistrationData | 100.0% | 100.0% |
| HotkeyService/HotkeyDescriptor | 100.0% | 100.0% |
| PanelContext | 100.0% | 100.0% |
| PanelExportResult | 100.0% | 100.0% |
| PanelImportPreview | 100.0% | 100.0% |
| PanelImportResult | 100.0% | 100.0% |
| PanelLayoutHelper/FixedLayout | 100.0% | 100.0% |
| PanelLayoutHelper/UserLayout | 100.0% | 100.0% |
| PanelPackageAppInfo | 100.0% | 100.0% |
| PanelPackageElement | 100.0% | 100.0% |
| PanelPackageImageInfo | 100.0% | 100.0% |
| PanelPackageManifest | 100.0% | 100.0% |
| PanelPackagePanelInfo | 100.0% | 100.0% |
| PathHelper | 100.0% | 50.0% |
| QuickNoteLayoutHelper | 100.0% | 100.0% |
| QuickNoteTextEdit | 100.0% | 100.0% |
| QuickNoteTextOperation | 100.0% | 100.0% |
| QuickNoteTheme | 100.0% | 100.0% |
| QuickNoteThemeCatalog | 100.0% | 100.0% |
| SentrySettings | 100.0% | 100.0% |
| TimerStopwatchLayoutHelper | 100.0% | 95.0% |
| TimerStopwatchProgressTickMetrics | 100.0% | 100.0% |
| TimerStopwatchWindowMetrics | 100.0% | 100.0% |
| PanelLayoutHelper | 98.8% | 86.2% |
| PanelPackageMapper | 98.7% | 66.7% |
| HotkeyValidationHelper | 97.3% | 77.3% |
| ActivationZoneHelper | 96.6% | 95.6% |
| HotkeyService | 96.6% | 93.8% |
| QuickNoteDocumentHelper | 96.3% | 60.7% |
| QuickNoteMarkdown | 93.3% | 87.8% |
| AppSettingsService | 92.7% | 67.4% |
| TelemetryService | 91.9% | 82.1% |

### Good Coverage (80% to < 90%)

| Class | Line | Branch |
|---|---:|---:|
| BrowserHelper | 89.8% | 70.3% |
| UpdateCheckService | 98.1% | 84.6% |
| QuickNoteService | 100.0% | 70.0% |
| ProfileRotationHelper | 87.5% | 57.1% |
| ActionService | 87.1% | 85.0% |
| IconHelper | 83.3% | 91.7% |
| PanelPackageService | 82.8% | 61.4% |
| ActionTargetHelper | 82.5% | 68.4% |
| ContextStateHelper | 82.5% | 80.0% |
| Logger | 82.6% | 75.0% |
| LocalizationService | 80.0% | 75.0% |
| TimerStopwatchFormatter | 80.0% | 68.2% |

### Moderate Coverage (50% to < 80%)

| Class | Line | Branch |
|---|---:|---:|
| PanelLayoutHelper/PanelLayoutMetrics | 78.6% | 100.0% |
| HotkeyRegistrationResult | 66.7% | 100.0% |

## ActionService Async Coverage

These entries come directly from the generated async state machine classes in the Cobertura report.

| Async Method | Line | Branch |
|---|---:|---:|
| `ExecuteHotkeyAsync` (`<ExecuteHotkeyAsync>d__10`) | 82.0% | 76.9% |
| `ExecuteWebActionAsync` (`<ExecuteWebActionAsync>d__11`) | 100.0% | 83.3% |
| `StartSearchAsync` (`<StartSearchAsync>d__15`) | 100.0% | 66.7% |
| `StartScreenshotAsync` (`<StartScreenshotAsync>d__16`) | 100.0% | 50.0% |
| `StartRecordVideoAsync` (`<StartRecordVideoAsync>d__17`) | 100.0% | 50.0% |
| `StartCalculatorAsync` (`<StartCalculatorAsync>d__18`) | 100.0% | 100.0% |
| `StartExplorerAsync` (`<StartExplorerAsync>d__19`) | 100.0% | 50.0% |
| `StartDownloadsAsync` (`<StartDownloadsAsync>d__20`) | 100.0% | 50.0% |
| `StartColorPickerAsync` (`<StartColorPickerAsync>d__21`) | 100.0% | 50.0% |
| `StartQuickNoteAsync` (`<StartQuickNoteAsync>d__22`) | 87.5% | 83.3% |
| `StartTimerStopwatchAsync` (`<StartTimerStopwatchAsync>d__23`) | 87.5% | 83.3% |
| `StartScriptFileAsync` (`<StartScriptFileAsync>d__25`) | 100.0% | 75.0% |
| `TryEnterFullscreenAsync` (`<TryEnterFullscreenAsync>d__28`) | 100.0% | 100.0% |

## Zero-Coverage Files

The following source files currently average `0%` line coverage in the latest report:

- `AboutWindow.xaml`
- `AboutWindow.xaml.cs`
- `App.xaml`
- `App.xaml.cs`
- `AppSettingsWindow.xaml`
- `AppSettingsWindow.xaml.cs`
- `DarkDialog.xaml`
- `DarkDialog.xaml.cs`
- `DarkWindow.cs`
- `FontHelper.cs`
- `IconPickerWindow.xaml`
- `IconPickerWindow.xaml.cs`
- `MainWindow.xaml`
- `NativeIntegrationService.cs`
- `NativeMethods.cs`
- `OverflowWrapPanel.cs`
- `QuickNoteWindow.xaml`
- `QuickNoteWindow.xaml.cs`
- `RotationProfileSelectionWindow.xaml`
- `RotationProfileSelectionWindow.xaml.cs`
- `ScreenColorPickerWindow.cs`
- `SettingsWindow.xaml`
- `SettingsWindow.xaml.cs`
- `TextPromptDialog.xaml`
- `TextPromptDialog.xaml.cs`
- `TimerStopwatchWindow.xaml`
- `TimerStopwatchWindow.xaml.cs`
- `UpdateCheckUi.cs`

## Notes on Zero-Coverage Classes

Not every zero-coverage class is equally important.

- **Expected zeroes from WPF UI**: `MainWindow`, `SettingsWindow`, `AppSettingsWindow`, `QuickNoteWindow`, `TimerStopwatchWindow`, `AboutWindow`, `TextPromptDialog`, `DarkDialog`, `OverflowWrapPanel`, `ScreenColorPickerWindow`, `IconPickerWindow`, and related XAML classes.
- **Interop-bound zeroes**: `NativeMethods`, `NativeIntegrationService`.
- **New internal adapters introduced for ActionService testability**: `ActionServiceRuntime`, `ActionProcessHandle`. These exist to let `ActionService` itself be tested and are currently not tested directly.
- **Small secondary zeroes** still worth future attention: `FontHelper`, `UpdateCheckUi`.

## Risk and Next Steps

The largest remaining uncovered surface is WPF UI. The previously uncovered `ActionService.StartScriptFileAsync`, `QuickNoteService`, and `UpdateCheckService` paths are now covered.

If the next goal is higher overall coverage rather than stronger service coverage, the most useful directions are:

1. Extract more non-UI logic from `MainWindow` and `SettingsWindow`.
2. Add focused tests for `UpdateCheckService`, `QuickNoteService`, and `FontHelper`.
3. Decide whether critical WPF flows should be covered with UI automation rather than more extraction.

## Validation Evidence

- `dotnet build .\AiteBar.sln -c Release` succeeded.
- `dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release` passed with `382 / 382`.
- `dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release --collect:"XPlat Code Coverage" --results-directory .\TestResults` produced the coverage artifact linked above.
