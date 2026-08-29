# Quick Note 1.15.19 stabilization audit

Audit date: 2026-08-29. Scope was read in full:

- `AiteBar/QuickNoteWindow.xaml.cs`
- `AiteBar/QuickNoteWindow.Editor.cs`
- `AiteBar/QuickNoteWindow.Presentation.cs`
- `AiteBar/QuickNoteSaveController.cs`
- `AiteBar/QuickNoteService.cs`
- `AiteBar/QuickNoteFileStore.cs`
- `AiteBar/QuickNoteDocumentCodec.cs`

Release invariants:

1. Mutations of the editor-owned `FlowDocument` must occur inside `TxtNote.BeginChange()` / `TxtNote.EndChange()`.
2. Document serialization must occur on its dispatcher thread; physical file writes must occur outside the UI thread.

## Findings

| ID | Status | Location | Finding |
|---|---|---|---|
| QN-A01 | RESOLVED | `QuickNoteWindow.xaml.cs`, `BtnClear_Click` | Clear/repopulate now executes through the shared editor change-group boundary. |
| QN-A02 | RESOLVED | `QuickNoteWindow.xaml.cs`, `ResetCaretFormatting` | Caret formatting now executes through the shared editor change-group boundary. |
| QN-A03 | RESOLVED | `QuickNoteWindow.xaml.cs`, `EnsureDocumentLoadedForFirstPaint` | Load, normalization and recovery fallback now share one caller-owned change group while Undo remains disabled. |
| QN-A04 | RESOLVED | `QuickNoteWindow.Editor.cs`, `ToggleFormatting` | Bold/italic now execute as one suppressed change group followed by one save schedule. |
| QN-A05 | RESOLVED | `QuickNoteWindow.Editor.cs`, `ChangeSelectionFontSize` | Font-size mutation now executes as one suppressed change group. |
| QN-A06 | RESOLVED | `QuickNoteWindow.Editor.cs`, `ToggleTextDecoration` | Underline/strikethrough mutation now executes as one suppressed change group. |
| QN-A07 | RESOLVED | `QuickNoteWindow.Editor.cs`, `SetEditorPlainText` | Unused unsafe helper removed. |
| QN-A08 | RESOLVED | `QuickNoteWindow.Presentation.cs`, `ApplyDocumentStyles` | Theme/style normalization now executes through the shared editor change-group boundary without scheduling persistence. |

## Serialization and I/O audit

No current violation of invariant 2 was found:

- `QuickNoteDocumentCodec.Serialize` calls `document.VerifyAccess()` before reading the dispatcher-owned document.
- `QuickNoteService.SaveAsync` and `SaveConflictCopyAsync` create the byte snapshot synchronously, then pass only detached `byte[]` data into `Task.Run`.
- `QuickNoteFileStore` owns only byte/file state; atomic `FileStream`, flush, move, hash and cleanup operations are reached from those background tasks.
- `QuickNoteSaveController` is driven by a dispatcher timer and preserves the WPF synchronization context across awaits; it calls persistence with the UI-owned document only after returning from the background external-change check.
- `QuickNoteDocumentCodec.Deserialize` calls `document.VerifyAccess()`. Its mutations need the caller-owned editor change group recorded in QN-A03.

Synchronous initial file reading in `QuickNoteService.Load` is outside the stated write-thread invariant. It is recorded as a performance consideration, not as a violation of either release rule, and is not changed during stabilization.
