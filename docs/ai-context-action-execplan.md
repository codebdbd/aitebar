# Add AI Context Utility With Text-First Capture And Copilot Handoff

This ExecPlan is a living document. The sections `Progress`, `Surprises & Discoveries`, `Decision Log`, and `Outcomes & Retrospective` must be kept up to date as work proceeds.

This document must be maintained in accordance with `PLANS.md` from the repository root.

## Purpose / Big Picture

After this change, a user can press a new built-in `AI Context` button in AiteBar and send the current work context to Copilot without manually collecting it first. The normal path is: capture selected text from the active application; if that fails, capture a screenshot of the active window; then open Copilot and place the prepared payload where the user can paste or use it immediately. The feature is visible on the primary panel alongside the other built-in tools, can be hidden in settings, supports right-click mode selection, and must not break panel behavior on any of the four dock edges.

The result is considered working only when a human can start AiteBar, click `AI Context`, observe that the panel hides, observe that text is preferred over screenshot when possible, observe that the fallback screenshot does not include the AiteBar panel, and observe that Copilot opens with the prompt content prepared for use.

## Progress

- [x] (2026-06-27 13:54+03:00) Reviewed the proposed requirements, `PLANS.md`, the current built-in utility architecture, and the main integration points in `AiteBar/UnifiedButtonService.cs`, `AiteBar/MainWindow.xaml.cs`, `AiteBar/ActionService.cs`, `AiteBar/AppSettingsService.cs`, and `AiteBar/Models.cs`.
- [x] (2026-06-27 13:54+03:00) Identified the main mismatch with the original draft: current built-in utilities are rendered through unified buttons rather than dedicated XAML buttons, and current Copilot support only opens Copilot via `Win+C`.
- [ ] Create the new feature model types and settings normalization for `AI Context`.
- [ ] Implement and test a prototype capture pipeline that proves the app can reliably prefer selected text and fall back to a window screenshot on this codebase.
- [ ] Implement Copilot handoff in a way that is demonstrably reliable on Windows 10 and Windows 11, with a clear fallback when direct rich handoff is not possible.
- [ ] Wire the utility into the unified built-in button system, settings UI, localization resources, and context menu behavior.
- [ ] Run release build, automated tests, fallback test command if needed, and manual verification on all four dock edges.

## Surprises & Discoveries

- Observation: the repository documentation in `docs/UTILITIES.md` still describes the older explicit per-button `MainWindow.xaml` integration path, but the running panel now renders built-in utilities through `UnifiedButtonService` and `UnifiedButtonsPanel`.
  Evidence: `AiteBar/UnifiedButtonService.cs` defines the utility list and `AiteBar/MainWindow.xaml.cs` dispatches utility clicks inside `ExecuteUnifiedButtonActionAsync`.

- Observation: current Copilot support is only a launcher that sends `Win+C`; there is no existing repository mechanism for typing text into Copilot, attaching an image, or invoking a public Copilot API.
  Evidence: `AiteBar/ActionService.cs` contains `StartCopilotAsync` but nothing for text or image transfer.

## Decision Log

- Decision: treat the original text as a requirements source, not as the implementation plan.
  Rationale: the original text is a good product specification, but it does not satisfy the repository's `ExecPlan` rules and it assumes some outdated integration points.
  Date/Author: 2026-06-27 / Codex

- Decision: integrate `AI Context` as a built-in utility rendered through the existing unified button system rather than adding a dedicated hard-coded panel button in `MainWindow.xaml`.
  Rationale: this matches the current app logic, built-in ordering, detach behavior, and settings-driven visibility.
  Date/Author: 2026-06-27 / Codex

- Decision: make Copilot handoff reliable before making it ambitious. The baseline handoff is: open Copilot, copy the prepared prompt to clipboard, optionally copy the screenshot image to clipboard or preserve a temp file path, and show a localized user hint when full automatic insertion is not possible.
  Rationale: the codebase has no existing supported automation path for rich Copilot submission, and the feature must be demonstrably working rather than aspirational.
  Date/Author: 2026-06-27 / Codex

- Decision: add a prototyping milestone for foreground capture and screenshot capture before committing to full UI wiring.
  Rationale: clipboard-based text capture and active-window screenshotting are the risk-heavy parts and need proof on this repository before the rest of the feature is layered on top.
  Date/Author: 2026-06-27 / Codex

## Outcomes & Retrospective

No implementation work has been performed yet. The main outcome of planning is that the feature is feasible in this application, but only if the plan aligns with the current unified utility architecture and only if Copilot handoff is defined conservatively enough to be testable.

## Context and Orientation

`AiteBar` is a Windows desktop utility built with WPF on `.NET 10`. The main window is the edge panel. Built-in tools such as `Quick Note`, `Clipboard Manager`, and `Copilot` are not defined as fixed visible buttons anymore. Instead, the app creates a single mixed list of user buttons and built-in utility buttons through `AiteBar/UnifiedButtonService.cs`, then renders that list into `UnifiedButtonsPanel` inside `AiteBar/MainWindow.xaml`.

The files that matter for this feature are:

`AiteBar/Models.cs` holds `AppSettings` and shared enums and models. New settings and lightweight feature models must be added here unless a new dedicated file is more readable.

`AiteBar/AppSettingsService.cs` loads settings, saves settings, normalizes older settings files, and maps utility visibility keys through `GetUtilityVisibility` and `SetUtilityVisibility`. Any new `ShowPreset...` property must be included here so old settings files remain safe.

`AiteBar/UtilityRegistry.cs` defines `IUtility`, `UtilityBase<TWindow>`, the `[Utility]` attribute, and automatic utility registration. If the feature does not need a persistent WPF window, it may implement `IUtility` directly. If it does present a configuration or preview window, it should still use the existing utility contract.

`AiteBar/ActionService.cs` launches built-in behaviors. It already contains `LaunchUtilityAsync` for utility execution and `StartCopilotAsync` for opening Copilot with `Win+C`.

`AiteBar/UnifiedButtonService.cs` is the source of truth for which built-in utility buttons exist, which icon they use, which visibility setting controls them, and in what order they appear before user buttons in the primary context.

`AiteBar/MainWindow.xaml.cs` builds the runtime button list, dispatches utility clicks in `ExecuteUnifiedButtonActionAsync`, builds right-click menus for unified buttons, and owns the panel hide/show logic that must remain smooth on `Top`, `Bottom`, `Left`, and `Right`.

`AiteBar/AppSettingsWindow.xaml` and `AiteBar/AppSettingsWindow.xaml.cs` contain the general program settings UI, including the `Quick tools` tab where utility visibility and several utility hotkeys are already managed.

`AiteBar/NativeMethods.cs` is the Win32 interop file. It already contains `GetForegroundWindow`, `GetWindowRect`, `SetForegroundWindow`, and `SendInput`. Additional P/Invoke entries needed for this feature must be added here, not scattered elsewhere.

`AiteBar/Resources/Strings.resx`, `AiteBar/Resources/Strings.ru.resx`, `AiteBar/Resources/Strings.uk.resx`, and `AiteBar/Resources/Strings.de.resx` hold localization keys. The test suite expects all four files to contain the same keys and format placeholders.

`AiteBar.Tests` already contains logic-oriented tests for settings and helpers. This feature should add tests only where behavior is deterministic and non-UI: mode selection, prompt construction, settings normalization, and any standalone capture decision logic. Native integration itself should be wrapped behind testable abstractions where practical.

For this plan, "handoff" means the sequence that makes the captured context ready for AI use. A direct rich handoff would mean Copilot opens with text and image already inserted. A conservative handoff means Copilot opens and the prompt or image is already on the clipboard, with the UI telling the user what happened. The second path is acceptable for the first shipping version because it is observable and reliable.

## Plan of Work

The work starts by adding small, testable feature models. In `AiteBar/Models.cs`, add `AiContextMode` and `AiContextType`, plus new `AppSettings` properties for visibility, default mode, prompt template, clipboard restore, active-window-only screenshot preference, clipboard delay, and minimum text length. Keep defaults modest and safe: show the utility by default, prefer `Auto`, restore the clipboard by default, default to active-window capture, default to a short clipboard delay near the app's existing interaction timings, and clamp numeric settings in `AppSettingsService.NormalizeAppState()`.

Create a dedicated feature file for the non-settings models, for example `AiteBar/AiContextModels.cs`. Define `AiContextRequest`, `CapturedAiContext`, and a compact result type for handoff such as `AiHandoffResult`. These types must be plain data containers so they can be tested without WPF windows.

Create a capture service such as `AiteBar/AiContextCaptureService.cs`. This service should not depend directly on `MainWindow`; it should accept an `AiContextRequest` and return a `CapturedAiContext`. Internally it must record the foreground window handle before AiteBar hides, hide the panel through `onBeforeExecute`, try to restore focus to the previously active window, send `Ctrl+C` with `SendInput`, wait for the configured delay, and compare clipboard state before and after the copy attempt. If new valid text is present, the service returns a text context. If not, it attempts a screenshot of the active window rectangle and falls back to the relevant monitor work area when the active window is unavailable, minimized, or unsafe to capture.

Because clipboard access is fragile, create a small helper dedicated to snapshotting and restoring clipboard state. Keep the scope intentionally narrow for the first version: restore plain text when possible, preserve the pre-existing clipboard when it was plain text, and do not crash when the clipboard contains a format the app cannot round-trip. Log only metadata such as text length and context type. Never log captured user text.

For screenshotting, prefer a service such as `AiteBar/AiContextScreenshotService.cs` or keep the implementation inside the capture service if the code remains short. Add any missing Win32 methods to `AiteBar/NativeMethods.cs`, including `GetWindowText` and `PrintWindow` only if they are actually used. The plan does not require `PrintWindow` if `CopyFromScreen` against a validated rectangle proves more reliable in testing. Save screenshots into a temp directory under the system temp path, add a cleanup routine that removes stale files created by this feature, and ensure the panel is hidden before capture so AiteBar does not appear in the image.

Create `AiteBar/AiPromptBuilder.cs` to transform a `CapturedAiContext` plus the user's template into the actual prompt text. The builder must handle an empty template, `{context}`, `{text}`, and screenshot-only scenarios without leaking implementation details into the UI layer. If a screenshot exists without text, the default prompt should describe that the screenshot is the context rather than inserting a fake placeholder body.

Create `AiteBar/AiCopilotLauncher.cs` to encapsulate handoff. For the first implementation, this class should call `ActionService.StartCopilotAsync` or reuse the same `Win+C` behavior indirectly, then copy the prompt to clipboard, and when a screenshot exists, attempt to put the bitmap onto the clipboard. If image clipboard population is not reliable enough across the supported environments, the fallback is to copy the prompt text and keep the screenshot temp file path available in a localized dialog. The launcher must return a result object so the caller can show accurate messages rather than guessing.

Create `AiteBar/AiContextUtility.cs` and implement `IUtility` directly unless a dedicated window is introduced. Its `LaunchAsync` implementation should build the request from `AppSettings`, capture context, build the prompt, perform the handoff, and show localized `DarkDialog` messages only for actionable failures or degraded fallback states. Use the real utility signature from `IUtility`: `Task LaunchAsync(AppSettingsService settingsService, Window? owner, Func<Task>? onBeforeExecute = null)`.

Once the feature works in isolation, wire it into the unified built-in tool list. In `AiteBar/UnifiedButtonService.cs`, add a new `UtilityButtonDef` for `AiContext` with a stable ID, glyph, color, visibility setting key, and tooltip key. In `AiteBar/MainWindow.xaml.cs`, extend `ExecuteUnifiedButtonActionAsync` so `AiContext` launches through `_actionService.LaunchUtilityAsync("AiContext", HideDock)`. Update right-click handling in `BuildUnifiedButtonContextMenu` so `AiContext` gets a richer menu than the generic "detach" menu: add entries for `Auto`, `Send selected text`, `Send screenshot`, the mode submenu, and `Configure prompt`. The generic detach option should remain available.

The settings UI should stay compact. In `AiteBar/AppSettingsWindow.xaml`, add a `Show AI Context` checkbox to the quick tools tab and a small `AI Context` settings block on the same tab or a suitable existing settings area. Include only the controls needed for the first version: default mode, restore clipboard, and maybe active-window-only capture. Do not turn `AppSettingsWindow` into a wizard. If editing the prompt template in-line makes the tab too dense, the first version may expose only the default mode and clipboard behavior in settings and defer full template editing to the right-click menu or a lightweight dialog.

Localization must be added in all four resource files. Include the tool name, tooltip, mode names, error messages, clipboard restore warning, prompt copied fallback message, and any labels needed by the settings UI and AI context menu. Keep format placeholders identical across languages.

Testing work should focus on deterministic logic. Add tests for mode routing, prompt building, settings normalization, screenshot fallback selection logic where abstracted, and any utility visibility plumbing affected by the new setting. If clipboard and screenshot code are wrapped behind small runtime interfaces, add tests for decision-making around changed versus unchanged clipboard content rather than trying to automate Windows clipboard behavior in unit tests.

## Milestones

### Milestone 1: Prove The Capture Pipeline

At the end of this milestone, the repository contains testable services that can decide whether to use text or screenshot and can produce a `CapturedAiContext` without touching the full UI wiring yet. Run the unit tests for the new decision logic and a manual local run of AiteBar. The proof is a simple manual scenario: select text in Notepad, launch the prototype path, and observe that text is captured; repeat with no selection and observe that a screenshot file is created.

The concrete work is to add the new models, the request object, the capture service, the clipboard snapshot helper, the temp screenshot cleanup routine, and the tests for the text-versus-screenshot choice. If a capture step proves unreliable in practice, record that in `Surprises & Discoveries` before proceeding.

### Milestone 2: Prove Reliable Copilot Handoff

At the end of this milestone, the repository can open Copilot and leave the prompt ready for use in a way that is demonstrably observable on supported Windows systems. Run the app locally, trigger the utility from a temporary test entry point or direct service call, and observe the resulting clipboard state and Copilot launch.

The concrete work is to create `AiPromptBuilder`, `AiCopilotLauncher`, and the result types that distinguish full success from fallback success. If true automatic insertion into Copilot is not reliable, freeze the first shipping behavior at "Copilot opens and prompt is copied" and update the plan to state that clearly. Do not pretend richer behavior exists unless it has been manually verified.

### Milestone 3: Ship The Utility Through The Existing Panel Architecture

At the end of this milestone, `AI Context` appears in the primary panel with the built-in tools, can be hidden in settings, follows built-in ordering, supports right-click mode selection, and runs through the utility registry like the rest of the app. Run the full build and test commands, then launch the app and verify the panel on `Top`, `Bottom`, `Left`, and `Right`.

The concrete work is to add `AiContextUtility`, add the unified utility definition, extend the runtime click dispatcher, extend the AI-specific context menu, add settings UI and localization, and verify that detaching, ordering, keyboard focus traversal, and context switching still behave as expected.

## Concrete Steps

Work from the repository root:

    D:\01_Codebdbd\01_projects\aitebar

Create the new files and update the existing ones listed in this plan. After each milestone, run:

    dotnet build .\AiteBar.sln -c Release

Run the main automated test command:

    dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release

If the WPF temporary-project problem appears, run the fallback test command:

    dotnet vstest .\AiteBar.Tests\bin\Release\net10.0-windows\AiteBar.Tests.dll

Run the application for manual verification:

    dotnet run --project .\AiteBar\AiteBar.csproj

Expected successful build transcript:

    Build succeeded.
        0 Warning(s)
        0 Error(s)

Expected successful test transcript should end with all tests passing. The exact count may change over time; the important signal is that the new AI Context tests pass and there are zero failed tests.

During manual verification, prepare at least these scenarios:

    1. Open Notepad, type text, select part of it, click `AI Context`, and confirm that text is used instead of screenshot.
    2. Open an application window with no selected text, click `AI Context`, and confirm that screenshot fallback is used.
    3. Right-click the `AI Context` built-in button, choose `Force Text`, and confirm that missing text shows the localized error instead of taking a screenshot.
    4. Right-click the `AI Context` built-in button, choose `Force Screenshot`, and confirm that text capture is skipped.
    5. Repeat the launch on all four dock edges and confirm the panel does not appear inside the screenshot.
    6. Switch to a non-primary context and confirm the built-in utility placement behavior still matches current app rules.

## Validation and Acceptance

Acceptance is behavior-based.

The feature is accepted when all of the following are true:

The `AI Context` built-in tool is visible in the primary panel when `ShowPresetAiContext` is enabled, and hidden when it is disabled. The tool participates in the same ordering and detach behavior as the other built-in tools.

Clicking the tool hides the panel and tries selected-text capture first in `Auto` and `PreferText` modes. A non-empty clipboard result that changed after the synthetic `Ctrl+C` action counts as captured text. Empty text, whitespace-only text, or unchanged clipboard content does not.

When text capture fails and the active mode permits fallback, the feature captures a screenshot of the active window or a safe screen fallback and stores it in a temp location that is cleaned up later. The screenshot must not contain the AiteBar panel in the normal path.

`ForceText` never falls back to screenshot. `ForceScreenshot` never attempts text capture. `PreferScreenshot` tries screenshot first and only uses text if screenshot capture fails.

Copilot opens through the existing launcher behavior. The prompt content is made available in a reliable way that the user can observe immediately. If direct insertion is not implemented, the UI clearly tells the user that the prompt was copied to clipboard and how the screenshot was preserved.

All new localization keys exist in `Strings.resx`, `Strings.ru.resx`, `Strings.uk.resx`, and `Strings.de.resx` with matching placeholders. The localization consistency test passes.

`dotnet build .\AiteBar.sln -c Release` succeeds. `dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release` succeeds, or the fallback `dotnet vstest` command succeeds if the known WPF temporary-project issue blocks the main test command.

Manual verification confirms that panel show/hide, tray access, context switching, and the four dock edges still behave correctly after the change.

## Idempotence and Recovery

All code changes in this plan are additive and can be re-applied safely if the working tree is clean. Settings normalization must be written so that opening an older `settings.json` more than once does not keep mutating values after the first normalization pass.

Temporary screenshot files are the only new external artifact. The implementation must keep them under a dedicated temp naming pattern so cleanup can be repeated safely without touching unrelated files.

If Copilot handoff automation proves unreliable during implementation, do not leave half-working behavior in place. Roll back to the conservative fallback defined in this plan: open Copilot, copy the prompt, preserve image context in the safest verified form, and report that behavior in the UI and in this plan's `Decision Log`.

If clipboard restoration cannot be performed because the clipboard is locked or contains unsupported data, log the failure, do not crash, and leave the newly captured text available rather than clearing the clipboard.

## Artifacts and Notes

Important implementation notes for the contributor:

    The current built-in utility system is centered on `AiteBar/UnifiedButtonService.cs` and `AiteBar/MainWindow.xaml.cs`, not on explicit hard-coded utility buttons in XAML.

    `IUtility.LaunchAsync` currently uses:
        Task LaunchAsync(AppSettingsService settingsService, Window? owner, Func<Task>? onBeforeExecute = null);

    `AiteBar/ActionService.cs` already provides:
        LaunchUtilityAsync(string utilityId, Func<Task>? onBeforeExecute = null)
        StartCopilotAsync(Func<Task>? onBeforeExecute = null)

    `AiteBar/MainWindow.xaml` currently renders the mixed button list through:
        <local:OverflowWrapPanel x:Name="UnifiedButtonsPanel" ... />

    `AiteBar/MainWindow.xaml.cs` currently routes built-in utility clicks in:
        ExecuteUnifiedButtonActionAsync(UnifiedButton item)

    `AiteBar/AppSettingsService.cs` currently centralizes visibility settings through:
        GetUtilityVisibility(string key)
        SetUtilityVisibility(string key, bool visible)

These code points are the stable integration surface for the feature.

## Interfaces and Dependencies

At the end of the work, the following repository-level interfaces and types must exist in stable form.

In `AiteBar/Models.cs`, add:

    public enum AiContextMode
    {
        Auto,
        PreferText,
        PreferScreenshot,
        ForceText,
        ForceScreenshot
    }

and the `AppSettings` properties:

    public bool ShowPresetAiContext { get; set; } = true;
    public AiContextMode AiContextMode { get; set; } = AiContextMode.Auto;
    public string AiContextPromptTemplate { get; set; } = "";
    public bool AiContextRestoreClipboard { get; set; } = true;
    public bool AiContextCaptureActiveWindowOnly { get; set; } = true;
    public int AiContextClipboardDelayMs { get; set; } = 150;
    public int AiContextMinTextLength { get; set; } = 1;

In `AiteBar/AiContextModels.cs`, define:

    public enum AiContextType
    {
        None,
        Text,
        Screenshot
    }

    public sealed class AiContextRequest
    {
        public AiContextMode Mode { get; init; }
        public bool RestoreClipboard { get; init; }
        public bool CaptureActiveWindowOnly { get; init; }
        public int ClipboardDelayMs { get; init; }
        public int MinTextLength { get; init; }
        public string PromptTemplate { get; init; } = "";
        public IntPtr? PreferredWindowHandle { get; init; }
    }

    public sealed class CapturedAiContext
    {
        public AiContextType Type { get; init; }
        public string? Text { get; init; }
        public string? ScreenshotPath { get; init; }
        public string SourceWindowTitle { get; init; } = "";
        public IntPtr SourceWindowHandle { get; init; }
        public DateTime CapturedAt { get; init; } = DateTime.Now;
    }

    public sealed class AiHandoffResult
    {
        public bool Success { get; init; }
        public bool PromptCopiedToClipboard { get; init; }
        public bool ScreenshotCopiedToClipboard { get; init; }
        public string? ScreenshotPath { get; init; }
        public string? MessageKey { get; init; }
    }

In `AiteBar/AiContextCaptureService.cs`, define a capture service with a testable runtime wrapper if needed:

    public sealed class AiContextCaptureService
    {
        public Task<CapturedAiContext> CaptureAsync(AiContextRequest request);
        internal Task<string?> TryGetSelectedTextAsync(AiContextRequest request);
        internal Task<string?> TryCaptureActiveWindowScreenshotAsync(AiContextRequest request);
    }

In `AiteBar/AiPromptBuilder.cs`, define:

    public sealed class AiPromptBuilder
    {
        public string BuildPrompt(CapturedAiContext context, string template);
    }

In `AiteBar/AiCopilotLauncher.cs`, define:

    public sealed class AiCopilotLauncher
    {
        public Task<AiHandoffResult> LaunchAsync(CapturedAiContext context, string prompt, Func<Task>? onBeforeExecute = null);
    }

In `AiteBar/AiContextUtility.cs`, implement:

    [Utility]
    public sealed class AiContextUtility : IUtility

with the real utility contract from `UtilityRegistry.cs`.

The implementation may use standard .NET and WPF clipboard APIs plus the Win32 interop already present in `AiteBar/NativeMethods.cs`. Do not add a new external dependency unless the prototype milestone proves a hard blocker that the platform APIs cannot solve cleanly.

Revision note: this plan was created to replace a requirements-style draft that did not follow `PLANS.md` and did not match the current unified built-in utility architecture. It also narrows Copilot handoff to a demonstrably testable path instead of assuming unsupported direct automation.
