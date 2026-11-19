# Make Telegram editor navigation visible

This ExecPlan follows `PLANS.md`. Keep Progress, Surprises & Discoveries, Decision Log, and Outcomes & Retrospective current.

## Purpose / Big Picture

A person editing a Telegram post can find the drafts library, the scheduled publication queue, and the next publication action in one shared bar. Every secondary screen exposes a Back button in the same place. Opening the library or queue from publication setup or results and going back restores the screen the person was using.

## Progress

- [x] (2026-11-17) Traced the current draft, publication, result, and queue flows.
- [x] (2026-11-17) Implemented the shared bar, return behavior, and fixes to the formatting menu and AI controls.
- [x] (2026-11-17) Built Release and passed the automated editor navigation, publication, and retry checks.
- [x] (2026-11-17) Checked the return path, formatting menu, and AI panel in an isolated Russian-language editor window at default and minimum widths.

## Surprises & Discoveries

The window already has five mutually exclusive views. Its header holds shortcuts to the library and queue, while each view places its return action differently. The formatting menu explicitly uses a custom style that sets two colors but leaves the system template in place.

The first build exited without a diagnostic using parallel MSBuild workers. Running one worker completed the build. The first VSTest run timed out connecting to its child process in the sandbox. The same tests ran outside the sandbox.

## Decision Log

On 2026-11-17, Codex chose a shared bar with a Back button on the left and the current publication action on the right. The library and queue remember the task that opened them, including publication results. Back from the results goes to the editor, since the operation has already finished. A shared dark menu template addresses the system-colored gutter.

## Outcomes & Retrospective

The shared bar now gives the return and publication controls consistent locations. In the isolated editor, Back from scheduled posts restored publication setup, and the next Back restored the same text. The formatting menu used a dark background across its full width. The AI controls fit on one line at the default width and wrapped with the preview open at the minimum width. The window test verifies that leaving setup and results for another view preserves selected recipients and the result that can be retried. Release built with zero errors and the desktop suite reported 1,840 passed and five skipped.

## Context and Orientation

`AiteBar/TelegramPostEditor/TelegramPostEditorWindow.xaml` defines the five screens in a single WPF window. WPF is the Windows desktop framework used by AiteBar, and XAML defines its controls and layout. `TelegramPostEditorWindow.Composer.cs` switches screens and flushes draft autosave before entering the library. `TelegramPostEditorWindow.Publication.cs` controls sending, scheduling, retrying, and queue refresh. `AiteBar/App.xaml` supplies dark menu templates. `AiteBar.Tests/TelegramPostEditor/TelegramPostEditorWindowTests.cs` drives the window with an in-memory credential store and fake Telegram responses.

## Plan of Work

### Milestone 1. Establish a path through the editor

Move the publication buttons to a common action slot in the top bar and use a single return control. Track the last task screen when entering the drafts library or queue to support returning without reloading a draft or starting another submission. Keep the existing publication validation and flush on opening the drafts library. Update the user manual and exercise the return paths in the existing window test. The Release build and the test command below verify that recipients and the delivery result survive the new transitions.

### Milestone 2. Make the editor controls fit

Use the shared dark context-menu template for the formatting and draft commands. Put the AI options and their action in one wrapping row with a compact close button. Run the same Release build and inspect the menu, AI panel, and navigation at the default window width. Reduce the width to verify that the AI action wraps and stays reachable.

## Concrete Steps

From the repository root, run:

    dotnet build .\AiteBar.sln -c Release -m:1 -nr:false
    dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release --no-build -m:1 -nr:false

If the WPF build fails only on temporary generated files, run `dotnet vstest .\AiteBar.Tests\bin\Release\net10.0-windows\AiteBar.Tests.dll` against the verified Release build. Expect zero errors and passing navigation, delivery, and retry assertions.

## Validation and Acceptance

Run the Release AiteBar application and open Telegram Post Editor from the utility panel. Type a short draft, open Drafts then Queue, and use Back to return to the same text. Open publication setup, use Back to edit the text, then return to setup and verify that the button confirms the selected destination and timing. Open the formatting menu to check the dark background and open AI options at default and minimum window widths to check that fields and controls remain reachable. The automated test must also visit publication results and the queue, return to the same results, and confirm that retry sends only the failed delivery. Use the fake transport to validate sending without a live Telegram post.

## Idempotence and Recovery

The changes affect window navigation and presentation. Tests can be repeated with isolated temporary files and fake Telegram responses. No storage migration is required.

## Artifacts and Notes

Release reported `0` errors. The desktop run reported `1840` passed, `5` skipped, and `0` failed. Compilation includes the Telegram server and server test projects. The desktop test command uses a fake Telegram transport for delivery and retry.

## Interfaces and Dependencies

Reuse `ShowPage(FrameworkElement)` for display and `ChangeDraftAsync` for draft changes. In the composer partial, add a handler for the common Back button and a helper for entering the library or queue while retaining the current task. Reuse dark menu resources from `AiteBar/App.xaml` and keep all labels in the existing `.resx` localization files.

Revision on 2026-11-17 records the navigation implementation, the build and test commands that succeeded in the workspace, and the completed visual checks.
