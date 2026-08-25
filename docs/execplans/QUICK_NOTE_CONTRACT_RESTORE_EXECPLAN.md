# Restore Quick Note to the agreed note utility contract

This ExecPlan is a living document. The sections `Progress`, `Surprises & Discoveries`, `Decision Log`, and `Outcomes & Retrospective` must be kept up to date as work proceeds.

This document follows `PLANS.md` from the repository root.

## Purpose / Big Picture

The Quick Note window must behave like a fast temporary note editor: formatting is directly available from the top toolbar, pinning is a separate window control, and secondary note commands live in the editor context menu next to copy and paste. After this work, a user can open Quick Note and immediately see eleven direct formatting buttons, no three-dot menu in the toolbar, and a code block that keeps a stable code appearance with a copy glyph that does not hide code text.

## Progress

- [x] (2026-08-23 00:00Z) Captured the target contract in `QUICK_NOTE_SPEC.md`.
- [x] (2026-08-23 00:00Z) Inspected the current implementation and found dropdown toolbar controls, a top toolbar overflow menu, and a fragile floating code copy glyph that conflict with the target contract.
- [x] (2026-08-23 00:00Z) Restore the toolbar and editor context menu in `AiteBar/QuickNoteWindow.xaml`.
- [x] (2026-08-23 00:00Z) Remove unused dropdown and overflow-menu handlers from `AiteBar/QuickNoteWindow.xaml.cs`.
- [x] (2026-08-23 00:00Z) Simplify code block formatting in `AiteBar/QuickNoteDocumentFormatting.cs` and related style application.
- [x] (2026-08-23 00:00Z) Restore window-level note content persistence in `AiteBar/QuickNotePersistence.cs`.
- [x] (2026-08-23 00:00Z) Update focused tests to enforce the agreed UI contract.
- [x] (2026-08-23 00:00Z) Run focused Quick Note tests: 40 passed, 0 failed.
- [x] (2026-08-23 00:00Z) Run full tests: 1347 passed, 0 failed.
- [x] (2026-08-23 00:00Z) Run Release build: succeeded with 0 errors and NU1900 vulnerability-feed warnings only.
- [x] (2026-08-23 00:00Z) Build installer: `artifacts\installer\AiteBar-Setup-1.15.14.exe`.
- [x] (2026-08-23 00:30Z) Replace internal `QuickNote.aite-note` persistence with user-facing `QuickNote.rtf` persistence and RTF code-block adaptation.
- [x] (2026-08-23 14:17Z) Run focused Quick Note tests after RTF conversion: 40 passed, 0 failed.
- [x] (2026-08-23 14:18Z) Run full tests after RTF conversion: 1347 passed, 0 failed.
- [x] (2026-08-23 14:18Z) Run Release build after RTF conversion: succeeded with 0 errors and NU1900 vulnerability-feed warnings only.
- [x] (2026-08-23 14:17Z) Build fresh installer after RTF conversion: `artifacts\installer\AiteBar-Setup-1.15.14.exe`.

## Surprises & Discoveries

- Observation: The current quick note toolbar contains `CmbHeading` and `CmbList`, so four one-click controls are hidden behind dropdowns.
  Evidence: `AiteBar/QuickNoteWindow.xaml` contains `ComboBox x:Name="CmbHeading"` and `ComboBox x:Name="CmbList"`.
- Observation: The editor uses the shared `TextEditingContextMenu`, so the requested note commands are not attached to the editor context menu in the Quick Note XAML.
  Evidence: `TxtNote` has `ContextMenu="{DynamicResource TextEditingContextMenu}"`.
- Observation: Focused Quick Note validation passes after removing dropdowns, the toolbar overflow menu, the floating `Figure` copy glyph, and window note persistence.
  Evidence: `dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release --filter "QuickNoteFormattingControlsTests|QuickNoteDocumentFormattingTests|QuickNoteWindowFormattingTests|QuickNoteServiceTests"` reported 40 passed, 0 failed.
- Observation: The final source scan for the Quick Note implementation finds no forbidden dropdown, overflow-menu, conflict-copy menu, or old `Figure` code block placement symbols.
  Evidence: `rg -n "Figure|FormatComboStyle|CmbHeading|CmbList|BtnMenu_Click|MenuOpenConflictCopy|QuickNote_MoreFormatting" AiteBar/QuickNoteWindow.xaml AiteBar/QuickNoteWindow.xaml.cs AiteBar/QuickNoteDocumentFormatting.cs` returned no matches.
- Observation: RTF cannot persist WPF `BlockUIContainer` button controls as functional editor UI.
  Evidence: `AiteBar/QuickNoteRtfAdapter.cs` exports app code blocks as readable fenced RTF paragraphs and restores them into visual code-block sections on load.

## Decision Log

- Decision: Keep the implementation scoped to Quick Note files and focused tests.
  Rationale: The working tree contains many unrelated changes; changing shared app menus or unrelated utilities would increase risk.
  Date/Author: 2026-08-23 / Codex.
- Decision: Put the three custom note commands in a Quick Note-specific editor context menu, not in the shared `TextEditingContextMenu` resource.
  Rationale: The shared resource is used by many text boxes across the app. The user's contract is specifically for the note editor.
  Date/Author: 2026-08-23 / Codex.
- Decision: Implement the code block with a Telegram-style header strip, decorative `code` label on the left, and copy glyph on the right.
  Rationale: The final user-provided visual reference explicitly shows this layout. A `Table` header inside the `Section` gives stable left/right placement without overlaying code text.
  Date/Author: 2026-08-23 / Codex.
- Decision: Persist Quick Note to `QuickNote.rtf` and use an adapter for visual code blocks.
  Rationale: RTF is a normal user-facing rich text file that the Open file command can open externally. The adapter keeps the app's richer visual code container without writing opaque WPF-only document packages as the user's note file.
  Date/Author: 2026-08-23 / Codex.

## Outcomes & Retrospective

Completed. The Quick Note toolbar now exposes the agreed direct formatting commands and no longer contains dropdown formatting controls or a three-dot overflow menu. The editor owns a Quick Note-specific context menu with standard text commands plus choose color, open file, and clear note. Code blocks use a Telegram-style dark container with a fixed-height gray header strip, decorative `code` label on the left, real copy button on the right, JetBrains Mono code text, and visible copy feedback. Clear formatting applied to a code block converts it into ordinary note text. The Quick Note window loads and saves note contents between launches.

Validation passed with focused Quick Note tests, the full test suite, Release build, and installer build. The installer artifact is `artifacts\installer\AiteBar-Setup-1.15.14.exe` with SHA256 `A3D12186A36735CA691B00DBF657194188341A09C970147AC47BE099DC08983B`.

## Context and Orientation

`AiteBar/QuickNoteWindow.xaml` defines the Quick Note window layout. The top toolbar and `TxtNote` editor live in this file. `AiteBar/QuickNoteWindow.xaml.cs` contains event handlers for formatting, pinning, menu commands, theme application, and mouse handling. `AiteBar/QuickNoteDocumentFormatting.cs` creates code block document elements and exposes helpers used by tests and persistence. Focused tests for the toolbar and code blocks live in `AiteBar.Tests/QuickNoteFormattingControlsTests.cs` and `AiteBar.Tests/QuickNoteDocumentFormattingTests.cs`.

The target user behavior is documented in `QUICK_NOTE_SPEC.md`. This implementation must match that contract.

## Plan of Work

Edit `AiteBar/QuickNoteWindow.xaml` so the top toolbar contains direct buttons for font size, lists, bold, italic, underline, strikethrough, link, code, and clear formatting. Remove the top toolbar three-dot button and its context menu. Define a Quick Note-specific context menu directly on `TxtNote` that includes undo, redo, cut, copy, paste, select all, and then the custom commands choose color, open file, and clear note.

Edit `AiteBar/QuickNoteWindow.xaml.cs` to remove handlers that only served deleted controls: `BtnMenu_Click`, `CmbHeading_SelectionChanged`, `CmbList_SelectionChanged`, and `ResetFormatCombo`. Remove theme updates for deleted combo boxes. Style code blocks so the header strip, copy glyph, and code text are restored after theme changes and clear formatting.

Edit `AiteBar/QuickNoteDocumentFormatting.cs` so `CreateCodeBlockElement` creates a native `Section` with a `Table` header strip and code paragraphs. Keep `GetCodeBlockText` returning only code content, excluding the `code` label and copy glyph.

Update focused tests so they fail if dropdowns, a toolbar overflow menu, the old floating glyph placement, broken clear formatting, or missing JetBrains Mono returns.

## Concrete Steps

Run these commands from `D:\01_Codebdbd\01_projects\aitebar` after implementation:

    dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release --filter "QuickNoteFormattingControlsTests|QuickNoteDocumentFormattingTests|QuickNoteWindowFormattingTests|QuickNoteServiceTests"
    dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release
    dotnet build .\AiteBar.sln -c Release -m:1
    .\installer\Build-Installer.ps1

## Validation and Acceptance

The focused tests must prove that the toolbar has exactly the agreed direct command buttons, no formatting dropdowns, and no three-dot overflow menu. Code block tests must prove that a code block is an editable `Section`, contains only code paragraphs, uses fixed code styling, and returns the exact code text when copied.

The full test suite and Release build must pass. The installer script must produce a fresh installer under `artifacts\installer`.

## Idempotence and Recovery

The changes are normal source edits and can be repeated by reapplying the same desired contract. If tests fail, inspect the focused Quick Note tests first because they encode the intended behavior.

## Artifacts and Notes

Artifacts will be recorded after validation.

Final validation artifacts:

    Focused Quick Note tests: 40 passed, 0 failed.
    Full tests: 1347 passed, 0 failed.
    Release build: succeeded, 0 errors.
    Installer: D:\01_Codebdbd\01_projects\aitebar\artifacts\installer\AiteBar-Setup-1.15.14.exe
    SHA256: A3D12186A36735CA691B00DBF657194188341A09C970147AC47BE099DC08983B

## Interfaces and Dependencies

No new runtime dependency is introduced by this plan. The existing JetBrains Mono font resources remain the code font source through `QuickNoteFonts.Code`.
