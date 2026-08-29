# Quick Note 1.15.19 release QA checklist

QA date: 2026-08-29.

Baseline gates:

- `dotnet build .\AiteBar.sln -c Release`: PASSED, 0 errors.
- `dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release`: PASSED, 1513 passed, 0 failed, 5 opt-in Snap test methods skipped by the ordinary run.
- Opt-in Windows Snap run (`AITEBAR_QUICKNOTE_TEST_SNAP=1`): PASSED, 13 passed, 0 failed, 0 skipped.

| # | Status | Verification evidence |
|---|---|---|
| 1 | PASSED | `Formatting_IsOneUndoUnitAndPreservesTheSelectedTextAndCaretRange` verifies a loaded WPF editor retains the exact selection and performs formatting as one Undo/Redo unit. `EditorHitTesting_AfterInsertedPlainText_MapsToTheTextInsteadOfAnEarlierLink` verifies caret hit testing. |
| 2 | PASSED | `ReleaseInputContracts_KeepNativeTextPasteThreeImageRoutesShortcutsAndNoPlaceholder` verifies the paste handler cancels only its image branch, leaving WPF's native text paste path intact. The full WPF suite passed. |
| 3 | PASSED | The release input contract verifies Ctrl+V, dialog and Drag&Drop are wired to the tested common image insertion paths. `InsertImage_IsolatesImageAndPreservesTextAcrossUndoAndReload` verifies block insertion and no text wrapping. |
| 4 | PASSED | `Limits_EnforceEightMegabytesSixteenMegapixelsAndTwentyFourMegabytesPerDocument`, `TryGetMarker_RejectsOversizedBase64Payload_WithoutCachePollution`, `Window_SelectedImageSupportsCopyAndCutCommands` and deletion tests verify 8 MiB, 16 MP, 24 MiB, selection, copy, cut and delete. |
| 5 | PASSED | Formatting single-unit test, `ListFormatting_KeepsLinePositionsAndUsesCompactIndent`, `TaskCheckbox_DeleteUndoRedo_RestoresTemplateAndClickBehavior` and `InsertImage_IsolatesImageAndPreservesTextAcrossUndoAndReload` verify Undo/Redo for formatting, lists/tasks and images. |
| 6 | PASSED | `MarkChangedAndSchedule_SavesAfterSevenHundredMillisecondDebounce` verifies no save at 500 ms and one save after the 700 ms dispatcher timer. `MarkChangedAndSchedule_IncrementsChangeVersionAndSavesOnSaveNowAsync` and source shortcut contract verify immediate Ctrl+S. |
| 7 | PASSED | Package round-trip tests, including nested task formatting, inline images, native code blocks and repeated reloads, verify the .aite-note document loads without structural or formatting loss. |
| 8 | PASSED | `Package_MigratesExistingRtfOnFirstLoad` and legacy RTF load tests verify migration to the portable .aite-note package. |
| 9 | PASSED | `TaskCheckbox_MouseRouteIsNotConsumedAsImageSelection`, centered-checkmark test and delete/Undo/Redo click-behavior test verify task clicks remain interactive and do not corrupt editor selection/caret behavior. |
| 10 | PASSED | List layout, quote/code native block and theme tests pass. `CodePalette_IsIdenticalAndDarkForEveryTheme` verifies #25213B, #302B49, #E3DFF2 and #433C61 exactly. |
| 11 | PASSED | Release input contract verifies Ctrl+B/I/U, Ctrl+K, Ctrl+Alt+C, Ctrl+1/2/3/0 and Escape routing; formatting, headings, links, code and clear-format behavior are covered by WPF tests. |
| 12 | PASSED | `LinkActivation_RequiresControlSoPlainClickCanPlaceTheCaret` verifies normal click remains caret-only and Ctrl is required. Safe URL tests reject tel:/mailto: activation while recognizing them as text link types. |
| 13 | PASSED | Both XAML contract and loaded-window test verify no `TxtPlaceholder` exists. |
| 14 | PASSED | `Palette_ContainsSevenDistinctStickyNoteColors` verifies lemon, sage, rose, lavender, sky, stone and dark; theme rendering/style tests pass across the palette. |
| 15 | PASSED | `NativeChrome_ExposesAllResizeEdgesAndCaptionButKeepsButtonsInteractive` verifies ShowInTaskbar, non-Topmost, ownerless HWND, WindowChrome, native resize and DWM corners. Layout helper tests verify monitor clamp; source/settings tests verify Left/Top/Width/Height/ThemeId/Pinned persistence. All 13 actual Snap cases pass. |
| 16 | PASSED | Interactive `UnpinnedNote_SnapKeepsVisible_ThenOrdinaryDepartureDismisses` covers mouse/keyboard, left/right Snap, arranging suppression and later unpinned dismissal. Pinned shared-boundary tests remain visible; `NativeMoveLoop_SuppressesDismissOnlyUntilItEnds` verifies resize lifetime. |
| 17 | PASSED | Same-length external-change tests verify SHA-256 detection. Save-controller tests verify conflict routing. `SaveConflictCopyAsync_RetainsOnlyFiveNewestPortableCopies` verifies five-copy retention and .aite-note names. |
| 18 | PASSED | Release input contract verifies the Escape branch; image interaction tests verify selected-image state can be cleared without deleting content, while window close tests verify the normal close pipeline. |
| 19 | PASSED | `LargePackage_RoundTripsWithoutChangingSource_RecordsSerializationCost` now verifies exactly 1000 paragraphs and 20 images, source identity, reload content and a bounded 256 MiB serialization allocation ceiling. |
| 20 | FAILED | Version 1.15.19 self-contained win-x64 publish and installer have not yet been produced in this stabilization run. |
