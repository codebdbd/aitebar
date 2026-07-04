# QuickNote Fixes ExecPlan

## Overview
Fix the major issues identified in the QuickNote code review:
- Nested list parsing and rendering
- Recursive block handling for clear formatting
- O(n²) offset calculation performance
- Selection collapse during clear formatting
- Link unwrapping in clear formatting

## Context
QuickNote is a note-taking utility in AiteBar using WPF FlowDocument and Markdown serialization.

## Implementation Plan

### Phase 1: Nested List Parsing
**Files to modify:** `QuickNoteMarkdown.cs`

**Changes:**
- Rewrite `LoadMarkdown` method to parse nested lists properly
- Build a tree of `FlowList` and `ListItem` instead of using Tag for indent
- Calculate indent level from leading whitespace
- Track current nested list hierarchy

**Validation:**
- Unit tests for nested lists (markdown → FlowDocument → markdown round trip)
- Manual testing with various nested list structures

### Phase 2: Recursive Block Handling
**Files to modify:** `QuickNoteWindow.xaml.cs`

**Changes:**
- Rewrite `RemoveSelectedListFormatting` to recursively traverse all blocks
- Handle lists inside `ListItem`, `Section`, etc.

**Validation:**
- Test clear formatting on nested lists
- Test clear formatting on lists inside sections

### Phase 3: Optimize Offset Calculations
**Files to modify:** `QuickNoteDocumentHelper.cs`

**Changes:**
- Implement single-pass offset calculation
- Cache offset information if needed
- Avoid creating new TextRange for every position

**Validation:**
- Performance testing with large notes
- Ensure all existing functionality still works

### Phase 4: Fix Selection Collapse
**Files to modify:** `QuickNoteWindow.xaml.cs`

**Changes:**
- Modify `ClearSelectedFormatting` to preserve original selection
- Apply marker edits first, but don't collapse selection until after formatting reset

**Validation:**
- Test clear formatting with various selection sizes

### Phase 5: Hyperlink Unwrapping
**Files to modify:** `QuickNoteWindow.xaml.cs`

**Changes:**
- Add `UnwrapHyperlinks` method to convert Hyperlink to plain text runs
- Call this method from `ResetSelectionFormatting` or `ClearSelectedFormatting`

**Validation:**
- Test clear formatting on linked text
- Verify links become plain text

### Phase 6: Minor Improvements
**Files to modify:** `QuickNoteWindow.xaml.cs`, `QuickNoteMarkdown.cs`

**Changes:**
- Unify selection preservation for all format buttons
- Document limitations (no nested inline formatting)
- Comment out or remove legacy marker code paths that aren't used

## Success Criteria
- All major issues resolved
- Build passes without warnings
- All existing tests pass
- Manual verification of key scenarios
