# Add a local Text Tools utility to AiteBar

This ExecPlan is a living document. The sections `Progress`, `Surprises & Discoveries`, `Decision Log`, and `Outcomes & Retrospective` must be kept up to date as work proceeds.

This document must be maintained in accordance with [PLANS.md](/D:/01_Codebdbd/01_projects/aitebar/PLANS.md).

## Purpose / Big Picture

After this change, AiteBar will have a new built-in utility for fast local text transformations such as case changes, transliteration, keyboard layout fixes, encoding and decoding, cleanup, and simple text analysis. A user will open it from the primary quick-tool panel like other built-in utilities, paste text, get an immediate local result without internet access, and copy the result back to the clipboard.

This plan intentionally adapts the original specification to the actual AiteBar architecture. In this repository, a "utility" is a built-in tool registered through `UtilityRegistry`, launched from the panel, and usually rendered as its own `DarkWindow`. This plan does not introduce a new application module, a new design system, or a pure MVVM subsystem that would diverge from existing patterns.

## Progress

- [x] 2026-08-21 00:00Z Researched the current utility architecture, settings integration, localization rules, and adjacent text-focused utilities before writing this plan.
- [x] 2026-08-21 00:00Z Reframed the original generic WPF specification into an AiteBar-specific utility plan with repository-relative files and behavioral acceptance criteria.
- [ ] Implement `TextToolsUtility`, `TextToolsWindow`, and the local transformation services described below.
- [ ] Integrate the new utility into settings, localization resources, and panel launch flows without breaking existing utilities.
- [ ] Add focused tests for transformation logic, settings persistence, visual contract, and window behavior.
- [ ] Validate the utility manually in the running app and run the required build and test commands.

## Surprises & Discoveries

- Observation: `AiteBar` already contains a built-in utility named `TextProcessing`, but it is an AI-assisted editor rather than a deterministic local text toolbox.
  Evidence: `AiteBar/TextProcessingUtility.cs` registers `Id => "TextProcessing"` and creates `TextProcessingWindow`, which depends on `AiGateway`.

- Observation: Existing utilities are not implemented as a shared MVVM module tree. The common pattern is one utility class, one WPF window, and separate helper or service classes for non-UI logic.
  Evidence: `AiteBar/QuickNoteUtility.cs`, `AiteBar/ClipboardManagerUtility.cs`, and `AiteBar/TextProcessingUtility.cs`.

- Observation: Utility visibility, panel presence, and settings exposure are centralized and must be updated in several coordinated files.
  Evidence: `AiteBar/UtilityButtonCatalog.cs`, `AiteBar/Models.cs`, `AiteBar/AppSettingsService.cs`, `AiteBar/AppSettingsWindow.xaml`, and `AiteBar/AppSettingsWindow.xaml.cs`.

- Observation: Localization coverage in this repository is broader than the original specification. New user-visible strings must be added to English, Russian, Ukrainian, and German resources.
  Evidence: `AiteBar/Resources/Strings.resx`, `Strings.ru.resx`, `Strings.uk.resx`, and `Strings.de.resx`, plus the note in `docs/UTILITIES.md`.

## Decision Log

- Decision: The new utility will use the stable internal identifier `TextTools`, not `TextProcessing` and not a nested module name.
  Rationale: `TextProcessing` already exists and is AI-oriented. Reusing that identifier would create a direct conflict in `UtilityRegistry`, settings, localization, and panel button launch logic.
  Date/Author: 2026-08-21 / Codex

- Decision: The implementation will follow AiteBar's existing utility-window architecture rather than introducing a new MVVM-first module structure.
  Rationale: The repository standard is a built-in utility class plus a `DarkWindow`, with non-UI logic split into helper services only when it adds clarity or testability.
  Date/Author: 2026-08-21 / Codex

- Decision: The first release will be fully local and deterministic. It will not call `AiGateway`, external APIs, remote services, or local LLM runtimes.
  Rationale: The original specification explicitly requires offline behavior, and this also matches AiteBar's privacy expectations for local text tooling.
  Date/Author: 2026-08-21 / Codex

- Decision: The first release will keep history and favorites out of scope unless a later implementation milestone proves they fit cleanly into existing privacy and settings patterns.
  Rationale: AiteBar already treats clipboard persistence as a privacy-sensitive feature. Adding persistent text history as a default requirement would widen scope and privacy surface without being essential to the core user outcome.
  Date/Author: 2026-08-21 / Codex

- Decision: The utility will open as a separate `DarkWindow` positioned near the panel, using the same window-state persistence pattern as existing larger utilities.
  Rationale: This matches the actual behavior of `ClipboardManagerWindow` and `TextProcessingWindow` and fits the requested dense dual-editor workflow much better than trying to force the feature into `MainWindow`.
  Date/Author: 2026-08-21 / Codex

## Outcomes & Retrospective

The original pasted technical specification described a reasonable product idea, but it assumed a generic WPF application, leaned on a new module hierarchy, and did not account for AiteBar's existing `TextProcessing` utility, multi-language resources, centralized quick-tool catalog, or panel integration rules. This ExecPlan converts that broad specification into a repository-specific implementation guide that a contributor can execute end to end inside this codebase.

No code is implemented yet. The remaining work is feature delivery, not further specification.

## Context and Orientation

`AiteBar` is a Windows WPF desktop utility bar. Built-in quick tools are registered through [AiteBar/UtilityRegistry.cs](/D:/01_Codebdbd/01_projects/aitebar/AiteBar/UtilityRegistry.cs), surfaced through [AiteBar/UtilityButtonCatalog.cs](/D:/01_Codebdbd/01_projects/aitebar/AiteBar/UtilityButtonCatalog.cs), and made visible in the panel through [AiteBar/UnifiedButtonService.cs](/D:/01_Codebdbd/01_projects/aitebar/AiteBar/UnifiedButtonService.cs) and `MainWindow` launch handling.

The new feature in this plan is a built-in quick tool named `TextTools`. It is separate from the existing AI utility `TextProcessing`. `TextTools` is a deterministic local text workspace. "Deterministic" here means the same input and selected operation always yield the same output with no network access and no probabilistic model behavior.

The key existing files a contributor must understand are:

- [AiteBar/TextProcessingUtility.cs](/D:/01_Codebdbd/01_projects/aitebar/AiteBar/TextProcessingUtility.cs): shows how a larger text-oriented utility is declared and launched.
- [AiteBar/ClipboardManagerUtility.cs](/D:/01_Codebdbd/01_projects/aitebar/AiteBar/ClipboardManagerUtility.cs): shows a local non-AI utility that restores an existing window instance.
- [AiteBar/Models.cs](/D:/01_Codebdbd/01_projects/aitebar/AiteBar/Models.cs): contains `AppSettings`, which stores quick-tool visibility flags, hotkeys, and per-utility state.
- [AiteBar/AppSettingsService.cs](/D:/01_Codebdbd/01_projects/aitebar/AiteBar/AppSettingsService.cs): clones and persists settings; new `AppSettings` fields must be copied here.
- [AiteBar/AppSettingsWindow.xaml](/D:/01_Codebdbd/01_projects/aitebar/AiteBar/AppSettingsWindow.xaml) and [AiteBar/AppSettingsWindow.xaml.cs](/D:/01_Codebdbd/01_projects/aitebar/AiteBar/AppSettingsWindow.xaml.cs): expose quick-tool visibility and utility-specific options.
- [AiteBar/MainWindow.xaml.cs](/D:/01_Codebdbd/01_projects/aitebar/AiteBar/MainWindow.xaml.cs): launches utilities from button clicks and hotkeys.
- [AiteBar/Resources/Strings.resx](/D:/01_Codebdbd/01_projects/aitebar/AiteBar/Resources/Strings.resx) plus the `ru`, `uk`, and `de` variants: hold all user-visible strings.
- [AiteBar.Tests](/D:/01_Codebdbd/01_projects/aitebar/AiteBar.Tests): contains the expected style of focused unit and UI-contract tests for utility logic.

The requested feature should follow the same visual language as existing windows: dark background, subdued contrast, blue accent, compact spacing, existing button and input styles, and no new standalone visual system. The panel itself must not change. The utility is opened from the panel; it is not embedded into `MainWindow`.

## Plan of Work

Implement the feature as a new built-in utility named `TextTools` with a dedicated window and a set of local helper services. The core user experience is two large text areas, one for source text and one for result text, with category selection, operation selection, clipboard actions, and lightweight statistics. The source text remains editable at all times. The result area is read-only except where a deliberate "swap" action turns it back into source text.

Create the following new files in `AiteBar`:

- `AiteBar/TextToolsUtility.cs`
- `AiteBar/TextToolsWindow.xaml`
- `AiteBar/TextToolsWindow.xaml.cs`
- `AiteBar/TextToolsModels.cs`
- `AiteBar/TextToolsService.cs`
- `AiteBar/TextToolsStatisticsService.cs`

If implementation becomes cleaner by splitting deterministic transformations into separate helpers, add small file-scoped services such as `TextToolsCaseService.cs`, `TextToolsTransliterationService.cs`, `TextToolsKeyboardLayoutService.cs`, `TextToolsEncodingService.cs`, and `TextToolsCleanupService.cs`. Do not create a deep folder tree unless the code volume justifies it. This repository favors flat utility-related files in `AiteBar/`.

In `TextToolsUtility.cs`, declare `[Utility]` and inherit from `UtilityBase<TextToolsWindow>`. Use `Id => "TextTools"`, a new localization key `Tool_TextTools`, a Fluent glyph chosen to match the existing quick-tool set, and `UtilityIconColors.TextWorkspace` so the button sits with other text-focused tools. `ShowWindow` should call a window method that positions the utility near the panel and restores saved size and location from `AppSettings`.

In `TextToolsWindow.xaml` and code-behind, build a compact two-pane workspace with the established `DarkWindow` styling. The header must present the utility title and one-line description. Below that, render category tabs or segmented buttons for: case conversion, transliteration, keyboard layout, encoding, cleanup, text operations, and analysis. Avoid adding the entire original proposal's "quick actions", persistent history, or favorites in the first implementation pass. Those are secondary features and should not crowd the first release.

Use existing button and input styles from the repository. Do not invent custom chrome. The preferred layout is a single window with a left-aligned content column containing source editor, operation controls, and result editor, plus a compact side or footer area for statistics and actions. Keep the visual density close to `TextProcessingWindow` and `ClipboardManagerWindow`: large enough for serious work, but still compact and restrained.

The initial operation set for the first shipping milestone should be:

Case conversion: lowercase, uppercase, title case, sentence case, invert case.

Transliteration: Russian to Latin and Latin back to Russian with a deterministic longest-match reverse parser.

Keyboard layout repair: English to Russian and Russian to English using standard physical-key mappings for Windows keyboard layout positions.

Encoding and decoding: Base64, URL, HTML, Hex, Binary, and Unicode escape.

Cleanup: collapse repeated spaces, trim each line, remove empty lines, strip punctuation, keep only letters, keep only digits, keep only letters and digits, and remove HTML tags.

Text operations: reverse text, reverse lines, sort lines, unique lines, reverse word order.

Analysis: counts for characters, non-whitespace characters, words, and lines, with room in the model for later extensions.

Keep all transformation logic out of the window class. The window should ask a deterministic service layer to transform the current source text based on the selected category and operation. The service layer must return a result object that can represent either a transformed string or a friendly validation error for malformed decode inputs. Do not throw user-facing exceptions for invalid Base64, invalid Hex, invalid Binary, or invalid Unicode escape text; return a readable error state and display it in the status area.

Define a simple internal data model in `TextToolsModels.cs`. The exact type names can vary, but the final design must include:

- a category identifier enum or similar stable value type,
- an operation descriptor containing internal id, category, display resource keys, and execution mode,
- an execution mode indicating automatic or manual processing,
- a result model containing output text, optional error message, and optional statistics,
- a statistics model with at least character, non-whitespace, word, and line counts.

The window should allow both automatic and manual execution, but this must remain simple. For lightweight deterministic transforms, automatic mode should update the result when source text or operation changes. For decoders or larger texts, the UI may force manual processing or apply a small debounce. Use the repository's pragmatic style: a direct code-behind timer or dispatch-based debounce is acceptable here, because the project already uses code-behind heavily for utility windows.

Persist only the settings that fit naturally into `AppSettings`: visibility toggle, last selected category, last selected operation, automatic versus manual mode, saved window geometry, and optionally the last source draft if this is implemented conservatively. Do not persist a full cross-session history in the first release. If a draft is persisted, store only the last source text and make sure the feature can be disabled by a boolean setting exactly like similar persisted utility data in the repo.

Update integration points across the existing application:

- In `AiteBar/UtilityButtonCatalog.cs`, add the new `TextTools` quick-tool definition and include it in `All`.
- In `AiteBar/Models.cs`, add `ShowPresetTextTools` and the per-window state fields needed for category, operation, mode, geometry, and optional draft text.
- In `AiteBar/AppSettingsService.cs`, update the clone logic for every new `AppSettings` field.
- In `AiteBar/AppSettingsWindow.xaml` and `.xaml.cs`, add the visibility switch and any optional utility-specific settings in the existing compact settings style.
- In `AiteBar/MainWindow.xaml.cs`, add the launch case for `"TextTools"`.
- In all `Strings*.resx` files, add the title, tooltip, description, category labels, operation labels, status messages, and validation errors.

Do not add a global hotkey in the first milestone unless the implementation is already complete and low-risk. A hotkey touches `Models.cs`, `HotkeyService.cs`, `AppSettingsWindow`, tests, and `MainWindow`, and it is not essential to prove the utility itself works.

## Concrete Steps

Work from the repository root `D:\01_Codebdbd\01_projects\aitebar`.

First create the core utility files and wire the utility into discovery and panel launch:

    dotnet build .\AiteBar.sln -c Release

The project should still build after the utility registration and settings wiring are in place, even before every operation is implemented.

Next add the deterministic transformation helpers and tests for each family of operations. After that, add the window behavior, localization strings, and settings persistence. Re-run:

    dotnet build .\AiteBar.sln -c Release
    dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release

If `dotnet test` fails because of transient WPF build-temp issues, run the repository's documented fallback:

    dotnet vstest .\AiteBar.Tests\bin\Release\net10.0-windows\AiteBar.Tests.dll

During manual validation, launch the application, ensure the new quick tool is enabled in app settings, open `TextTools` from the primary context, and exercise each operation family with representative text.

## Validation and Acceptance

The feature is accepted when a human can verify all of the following behaviors:

Opening the app with `TextTools` visibility enabled shows a new text-focused quick-tool button in the primary context, using the existing quick-tool styling rather than a custom panel treatment.

Clicking that button opens a dedicated dark utility window near the panel. Reopening the tool restores the existing window instance or brings it to front in the same way comparable utilities already do.

Pasting `HELLO world` into the source box and choosing lowercase produces `hello world`. Choosing uppercase produces `HELLO WORLD`. Choosing title case produces `Hello World`.

Pasting `Привет, мир!` and choosing transliteration to Latin produces `Privet, mir!`. Pasting `ghbdtn` and choosing keyboard layout English to Russian produces `привет`.

Pasting `SGVsbG8=` and choosing Base64 decode produces `Hello`. Pasting malformed Base64 does not crash the app and instead shows a friendly validation message in the window.

Pasting text with repeated spaces and blank lines into cleanup operations changes only the requested aspects and preserves the rest of the text deterministically.

The analysis area updates to correct counts for a short multi-line input, including characters, non-whitespace characters, words, and lines.

Closing and reopening the window restores any explicitly persisted state such as selected category, selected operation, execution mode, and window bounds.

Running:

    dotnet build .\AiteBar.sln -c Release
    dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release

must complete successfully, or the documented `vstest` fallback must pass if the failure is only the known WPF temp-build issue.

## Idempotence and Recovery

All edits in this plan are additive and can be applied incrementally. If the new utility window compiles before every operation exists, keep the unimplemented operations out of the UI until their services and tests are present. That prevents half-wired options from appearing to users.

If settings fields are added and the UI is not ready yet, default them to safe values that keep the quick tool hidden until the feature is complete. If localization keys are missing in one language file, fix that before considering the feature complete because the repository expects the resource sets to remain aligned.

If a transformation algorithm proves incorrect, prefer tightening the tests and replacing the helper service implementation rather than adding special cases into `TextToolsWindow.xaml.cs`. Recovery should keep the window thin and the deterministic logic testable.

## Artifacts and Notes

Representative examples that should eventually appear in tests:

    Input: HELLO WORLD
    Operation: lowercase
    Output: hello world

    Input: Привет, мир!
    Operation: transliterate ru->latin
    Output: Privet, mir!

    Input: ghbdtn
    Operation: keyboard layout en->ru
    Output: привет

    Input: SGVsbG8=
    Operation: Base64 decode
    Output: Hello

    Input: 48 65 6C 6C 6F
    Operation: Hex decode
    Output: Hello

    Input: Hello    world

    Operation: collapse spaces
    Output: Hello world

Suggested initial localization keys:

    Tool_TextTools
    Main_TextToolsTooltip
    QuickTool_TextTools_Title
    QuickTool_TextTools_Description
    TextTools_Title
    TextTools_Subtitle
    TextTools_CategoryCase
    TextTools_CategoryTransliteration
    TextTools_CategoryKeyboardLayout
    TextTools_CategoryEncoding
    TextTools_CategoryCleanup
    TextTools_CategoryText
    TextTools_CategoryAnalysis
    TextTools_StatusInvalidBase64
    TextTools_StatusInvalidHex
    TextTools_StatusInvalidBinary
    TextTools_StatusInvalidUnicodeEscape
    TextTools_ActionPaste
    TextTools_ActionCopy
    TextTools_ActionClear
    TextTools_ActionSwap
    TextTools_ActionConvert

## Interfaces and Dependencies

Use only the existing .NET and repository dependencies already present in `AiteBar`. No new network dependency, AI dependency, or large third-party text library is required for the first milestone.

`TextToolsUtility` must be a regular `[Utility]` class implementing the repository's utility contract through `UtilityBase<TextToolsWindow>`.

`TextToolsWindow` must remain a `DarkWindow` with code-behind orchestration only. It may own selection state, timers, and bindings to WPF controls, but it must not embed the transformation algorithms directly.

`TextToolsService` must expose a stable entry point for deterministic execution. One acceptable shape is:

    internal sealed class TextToolsService
    {
        public TextToolsResult Execute(TextToolsOperationId operation, string input);
    }

If analysis is split out, `TextToolsStatisticsService` should provide a single entry point such as:

    internal static class TextToolsStatisticsService
    {
        public static TextToolsStatistics Calculate(string text);
    }

If category-specific helpers are introduced, they should be stateless and easy to test. For example:

    internal static class TextToolsTransliterationService
    {
        public static string ToLatin(string input);
        public static string ToCyrillic(string input);
    }

The tests should follow the existing repository style: focused unit tests for transformation logic, focused behavior tests for state restoration and UI contract, and no attempt to add a brittle end-to-end UI automation suite for every button.

Revision note: 2026-08-21. This file was created to replace a generic pasted WPF specification with an AiteBar-specific execution plan that matches the repository's actual architecture, integration points, privacy expectations, and localization requirements.
