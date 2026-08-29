# Quick Note: centered checks, standalone images and native Snap


This living ExecPlan follows `PLANS.md`. Update Progress, Surprises & Discoveries, Decision Log and Outcomes & Retrospective as implementation and verification proceed.

## Purpose / Big Picture


The check mark must sit in the middle of its checkbox. New pasted, dropped and file-picked images must occupy their own line, with text before and after them rather than beside them. A pinned or unpinned note must participate in ordinary Windows Snap arrangements and shared-boundary resizing instead of overlaying its neighbor. Preserve single-note storage, image selection/copy/delete, Undo/Redo, formatting, auto-dismiss when unpinned, saved geometry, palette and Windows 11 rounded corners. Deliver a rebuilt installer.

## Progress


- [x] (2026-08-28) Inspect checkbox geometry, image insertion and native window configuration.
- [x] (2026-08-28) Centered checkbox stroke verified in light/dark themes. Separate image paragraphs passed start/middle/end/empty/replacement cases, Undo/Redo and save/reload.
- [x] (2026-08-28) Four interactive shared-boundary scenarios passed: Quick Note on either side and both ordinary-window reference cases. Report: `AiteBar.Tests/TestResults/quicknote-snap-desktop.trx`; before/after bounds: `artifacts/quicknote-validation/snap-*.txt`.
- [x] (2026-08-28) Release build passed with zero warnings/errors; full suite passed 1496 tests, with one opt-in Snap theory skipped. All four Snap cases passed separately. Installer rebuilt and SHA256 verified.

- [x] (2026-08-28, follow-up) Reproduced unpinned edge-drag closing the note when Snap transfers foreground to its shell popup. Previous desktop coverage only used pinned notes.
- [x] (2026-08-28, follow-up) The first post-fix interactive run passed both unpinned edge drags plus seven focused tests (9/9). Later runs confirmed Win+Left/Right, ordinary departure dismissal, and all four shared resize cases.
- [x] (2026-08-28 15:06 local, follow-up) Release build has zero warnings/errors; full suite passed 1504 tests, with the two opt-in desktop theories skipped. Published AiteBar.dll SHA256 matches the tested Release assembly.
- [x] (2026-08-28 15:07 local, follow-up) Replacement installer verified: nonempty, matching application version and SHA256 manifest.
- [ ] Optional final uninterrupted interactive rerun remains pending: later attempts encountered external cursor/focus changes; user was asked to leave input idle. Earlier successful checks and current limitations are reported separately.

- [x] (2026-08-28 15:50 local) Compared resize frames for Quick Note, ordinary WPF and independent WinForms/GDI controls. The gray neighbor preview occurs in all three; after release the note and controls return. Two final desktop comparison cases passed.
- [x] (2026-08-28) Full suite after capture-test additions: 1504 passed, 0 failed, 3 opt-in desktop tests skipped. Release build passed. No runtime change or replacement installer is justified by the reproduced system preview.

- [x] (2026-08-28 16:22 local, follow-up) A real click inside the note, followed by dragging its own border, kept note text visible on both sides while both windows resized. Desktop test passed 2/2; this is an interaction workaround, not automatic removal of the shell preview.
- [x] (2026-08-28 16:24 local) Full note-edge regression: 1504 passed, 0 failed, 4 opt-in tests skipped.
- [x] (2026-08-28 16:30 local) Both DWM probes completed: 4/4 geometry checks per flag, but actual note-right drag frames still show acrylic. Rejected and removed test-only selector.
- [ ] Automatic note visibility while another app initiates shared resize remains unresolved; no supported per-window fix was found.

- [x] (2026-08-28) Built a direct inactive-edge prototype. Native hit testing reaches the note at a nine-pixel inset with a sixteen-unit side band.
- [x] (2026-08-28 18:08 local) With idle input authorized, both direct-edge prototype cases passed: actual text pixels remained visible, shared edge moved 120 pixels, native activation followed WM_MOUSEACTIVATE before sizing.
- [x] Promote a smaller 12-unit side band into production XAML; preserve top/bottom 8 and caption 28. Native hit tests pass for edges/corners, first text character, toolbar buttons and scrollbar in two themes.
- [x] (2026-08-28) Production 12-unit chrome passed both direct-edge desktop cases without any test-time chrome override; text remained visible while both windows resized.
- [x] (2026-08-28) Full Release suite: 1504 passed, 0 failed, 5 opt-in tests skipped. Published assembly matches tested Release SHA256.
- [x] (2026-08-28) Installer 1.15.15 built and verified: nonempty, matching publish/project version and SHA256 manifest. Unsigned (no certificate supplied).

- [x] (2026-08-28) Compact scrollbar: actual 2-unit indicator/2-unit edge gap/6-unit column verified in three themes. Thumb drag, wheel scroll, short-note collapse and native grips pass.
- [x] (2026-08-28) Release build: zero warnings/errors. Final ordinary regression: 1505 passed, 0 failed, 5 opt-in desktop checks skipped.
- [x] (2026-08-28) Installer 1.15.16 built and verified: version, size and SHA256 manifest match.
- [x] (2026-08-28) Exact published DLL passed the full suite: 1505 passed, 0 failed, 5 opt-in Snap checks skipped; original test-output DLL restored.
- [x] (2026-08-29) Traced the application chooser to ordinary-click activation of automatically recognized pasted phone/e-mail text (`tel:`/`mailto:`). Normal editor clicks were intercepted before RichTextBox could place its caret.
- [x] (2026-08-29) Changed link activation to `Ctrl+click`; an unmodified click remains a normal RichTextBox edit click with the I-beam cursor. The code-block copy target remains available by ordinary click.
- [x] (2026-08-29) Removed the unnecessary custom `PART_ContentHost` ScrollViewer while retaining the six-unit local ScrollBar style. A controlled red/green check showed this template alone did not fail the new point-mapping test, so it is treated as risk removal rather than the proven chooser cause.
- [x] (2026-08-29) Added tests for phone recognition/activation gating plus rendered point-to-text mapping and caret placement after an earlier hyperlink.
- [x] (2026-08-29 12:53 local) Final 1.15.17 Release build passed with zero warnings/errors. Full suite: 1507 passed, 0 failed, 5 opt-in Snap tests skipped. Exact published DLL Quick Note suite: 226 passed, 0 failed, 5 opt-in tests skipped; original test-output DLL restored by verified hash.
- [x] (2026-08-29 12:53 local) Replacement installer 1.15.17 rebuilt from the final published output and verified as the only current installer artifact. Installer/published product versions and SHA256 manifest match.
- [x] (2026-08-29) Removed the empty-editor placeholder from Quick Note XAML, all localized resources, theme application and footer-controller plumbing. Focused empty-window/statistics/resource-parity tests pass.
- [x] (2026-08-29 13:17 local) Final 1.15.18 Release build passed with zero warnings/errors. Full suite: 1507 passed, 0 failed, 5 opt-in Snap tests skipped. Exact published DLL Quick Note suite: 226 passed, 0 failed, 5 opt-in tests skipped; the test-output DLL was restored by verified hash.
- [x] (2026-08-29 13:17 local) Installer 1.15.18 rebuilt and verified as the only current installer artifact. Installer/published versions and SHA256 manifest match.
- [x] (2026-08-29, 1.15.19 stabilization) Fully audited all requested Quick Note window, save, service, store and codec files. Eight editor mutations lacked caller-owned change groups; serialization/file-write thread separation had no violation.
- [x] (2026-08-29, 1.15.19 stabilization) Resolved all eight audit findings, removed the unused unsafe plain-text helper and added focused release regressions. Release build passed; full suite passed 1513 tests with five opt-in methods skipped.
- [x] (2026-08-29, 1.15.19 stabilization) Opt-in Windows Snap suite passed all 13 mouse, keyboard, left/right, shared-resize and content-visibility cases.
- [ ] Build and verify the synchronized 1.15.19 self-contained installer, then test the exact published assembly and close the temporary audit/checklist.

## Surprises & Discoveries


`QuickNoteDocumentFormatting.CreateTaskCheckboxTemplate` centers a Path whose geometry starts at positive coordinates, so centering its layout box does not center the visible stroke. `QuickNoteWindow.Editor.InsertImage` inserts an inline directly at the caret. Despite native caption/resize hit testing, the window still has `Topmost=True`, `ShowInTaskbar=False` and a panel owner assigned by `QuickNoteUtility`; those are overlay-window semantics, not an independent application window.

New insertion tests exposed an existing Undo defect: custom QuickNoteImage controls become empty Grid placeholders on native redo. A plain Image with a raw CachedBitmap also cannot deserialize. A native Image round-tripped through WPF XamlPackage has a package URI whose image lifetime is maintained by WPF, and passed all image Undo/Redo tests. No custom undo stack or permissive XAML parser is introduced. A desktop test process cannot force foreground focus; interactive checks instead click only their own visible test surfaces. Initial asymmetric input sequences were unreliable even for ordinary reference windows. Forming the same right-then-left pair for every case and ensuring the pointer is not moved externally produced passing shared-boundary drags on both sides.

The follow-up edge-drag test recorded WM_EXITSIZEMOVE, then WM_ACTIVATE with the shell ForegroundStaging tool popup in foreground and IsWindowArranged=true, then CLOSED. This is actual loss of focus, not a geometry or visibility-only defect. Previous four passing cases did not cover an unpinned note.

Resize investigation finding: during a successful shared resize, Windows visually substitutes a blurred gray neighbor rectangle for both WPF and WinForms/GDI windows. WindowFromPoint still reports the underlying application HWND, so that API does not identify the compositor visual owner; do not claim otherwise. The independent framework comparison and restored after-release content distinguish this from Quick Note losing or clearing its document.

DWM probe discovery (2026-08-28): DwmSetWindowAttribute returned success for TRANSITIONS_FORCEDISABLED (3) and EXCLUDED_FROM_PEEK (12), but neither preserved the note pixels during a peer-initiated shared resize. Both runs moved the common edge from x=960 to x=1080, confirming the drag was real; step-6 captures still show the same gray acrylic replacement.

Direct-edge discovery (2026-08-28): the wider test-only side band makes WindowFromPoint return the note and native hit testing return HTLEFT/HTRIGHT while the peer remains foreground. This confirms reachability, not successful resize. Two desktop attempts failed; explicit pre-click cursor checks in the second found positions 1354,1031 and 1482,1053 instead of 969,504 and 951,504. These runs cannot establish resize or visibility behavior.

Successful direct-edge finding (2026-08-28): quicknote-direct-edge-idle.trx passed both sides. Native messages were WM_MOUSEACTIVATE, WM_ACTIVATE, WM_NCLBUTTONDOWN, WM_ENTERSIZEMOVE, WM_EXITSIZEMOVE. No application activation hook was required. The initial 16-unit prototype band is wider than needed near text/scroll controls; the production proposal uses 12-unit sides and explicit control hit-test priority. Native offscreen hit tests passed for the first text character, scrollbar and toolbar buttons in both themes.

Compact-scroll finding: the old scroll host reserves more width than the visible indicator and adds an outer right margin. A local ScrollViewer template is needed to control the reserved column independently from the thumb. Initial rendered tests still measured 17 units despite the new width setting, so acceptance must inspect actual layout, not merely XAML setters.

Compact-scroll discovery: the WPF theme minimum kept the scrollbar at 17 units despite Width=6. Setting local MinWidth/MinHeight to zero removed that floor; actual layout tests now verify six units. The new thumb template also exposed an old structural test selecting the first Grid anywhere in the XAML, which was changed to select the window content Grid specifically.

Editing-regression discovery (2026-08-29): an inserted phone such as `+380631001155` is recognized as a phone link and normalized to a `tel:` URI. The editor previously treated both no modifier and Control as activation, set the hand cursor and called the Windows shell on an ordinary click; without a registered phone application Windows opens its application chooser. A controlled reintroduction of the custom ScrollViewer still passed point-to-run mapping, so that template is not recorded as the chooser cause.

Empty-editor discovery (2026-08-29): the visible placeholder was not purely XAML; `QuickNoteFooterStatsController` also owned its visibility and `ApplyTheme` recolored it. Removing only the TextBlock would leave an invalid constructor dependency and stale localization keys. The placeholder was therefore removed through the full dependency path while keeping footer statistics unchanged.

Stabilization discovery (2026-08-29): document clear, initial load/recovery, caret reset, direct formatting and theme restyling were individually correct but bypassed the editor's change-group invariant. The persistence boundary itself was already sound: the dispatcher-owned document is serialized synchronously after `VerifyAccess`, and only detached bytes reach `Task.Run` and `QuickNoteFileStore`. The first interactive Snap run also exposed a one-pixel input-harness tolerance that was narrower than this multi-monitor desktop's native SendInput rounding; actual HWND Snap geometry still passed.

## Decision Log


Use existing WPF document/serialization machinery and native Windows window management. Do not implement a custom layout manager that moves unrelated applications. Isolate new images using paragraphs while retaining the existing InlineUIContainer image payload; this preserves storage compatibility. Treat pin as keeping the note open, consistent with its existing documented contract. Any changes to taskbar/ownership must be verified with real Snap interaction rather than inferred only from hit-test codes.

Use native Image controls backed by WPF's in-memory image packages for new/restored images; retain QuickNoteImage only for legacy compatibility. The extra packaging at image creation is preferable to an unrecoverable undo entry or a bespoke image cache. Other document edits do not invoke this packaging.

Follow-up decision (2026-08-28): use a disposable QuickNoteWindowInteraction HWND message observer to recognize the native move/resize loop, plus the documented IsWindowArranged API and shell process/popup/tool-window flags to distinguish Snap Assist from ordinary applications. Do not match localized titles, hardcode shell class names, change pin preferences, add a global hook, force focus or delay auto-dismiss with a guessed timer. Capture the transition before the existing dispatcher yield.

Resize investigation decision (2026-08-28): retain native Snap and unchanged note rendering. Reproducing the same shell preview with WinForms/GDI is evidence against a Quick Note renderer fix. Added diagnostic captures and manual acceptance for restoration after release instead of inventing a custom resize implementation. No application settings were changed and no new installer is presented as a fix for this OS effect.

DWM decision (2026-08-28): reject both flags because they do not satisfy visibility acceptance. Remove experimental interop/environment selection from the test source; retain the prototype under ignored artifacts for reproducibility. Do not ship ineffective flags, global focus interception or a custom manager of unrelated windows.

Direct-edge decision (2026-08-28): stop repeated desktop input until an idle-input window is available. Preserve the standalone test prototype under ignored artifacts and restore the active test source to its previously verified state. Do not ship the wider band without testing native shared resize and editor hit targets.

Production decision (2026-08-28): use only WindowChrome side hit-test geometry and standard IsHitTestVisibleInChrome control priority; do not add focus stealing or global hooks. Preserve all visible margins, header size and corner behavior. Increment app/assembly/installer metadata to 1.15.15 to distinguish this delivery from the earlier 1.15.14 installer. Verify the final production XAML, not merely the prototype, before shipping.

Compact-scroll decision (2026-08-28): keep all templates local to QuickNoteWindow, preserve the standard WPF part/command contracts, and change only the editor outer right margin from four to zero. Reserve six units for the thumb hit target while drawing two units (four on hover/drag), rather than shrinking input to a difficult two-unit target. Retain WindowChrome control priority and verify the adjacent native resize hit area. Bump synchronized metadata to 1.15.16 and archive the previous generated installer.

Editing-regression decision (2026-08-29): ordinary clicks in an editable note always remain editing input; require Control to activate recognized URL/e-mail/phone text and show the hand cursor only while Control is held. Preserve ordinary-click code-block copying as a separate explicit control action. Retain local ScrollBar/Thumb templates but restore WPF's standard RichTextBox ScrollViewer because scrollbar width does not justify owning the text host. Release this correction as 1.15.17.

Empty-editor decision (2026-08-29): render no onboarding text inside an empty Quick Note. Remove the placeholder resource and API rather than hiding it with a permanent visibility flag. Keep character/line statistics and image-only document accounting in the existing footer controller. Release as 1.15.18.

Stabilization decision (2026-08-29): introduce one shared, save-suppressed editor change-group boundary and route only the audited mutation sites through it. Do not alter document format, save timing, UI design, input semantics or window behavior. Keep strict native Snap geometry assertions while allowing three pixels for SendInput coordinate rounding. Release as 1.15.19 only after the exact installer payload passes the same Quick Note regression suite.

## Outcomes & Retrospective


Checkbox and image insertion tests passed. All four desktop Snap scenarios passed after forming an identical right-then-left pair and avoiding mouse interference during the drag. For both note sides the common edge moved from x=960 to x=1080 on this desktop while outside edges remained x=0 and x=1920. Release compilation passed without warnings or errors. The full regression run passed 1496 tests with one explicitly skipped interactive theory; its four cases passed in the separate desktop run. The installer is version 1.15.14, 80,859,542 bytes, built at 2026-08-28 14:34:56 local time, and its SHA256 matches the manifest. Earlier native-hit-test checks alone were insufficient evidence of shared resizing; this plan now has real input/geometry evidence.

Latest resize outcome (2026-08-28): no automatic fix was produced. The note-edge interaction passes actual2-sided visibility/geometry tests, and the normal suite passes 1504 tests with 4 interactive methods skipped. Both DWM experiments passed their 4 geometry cases but failed visual acceptance; passing geometry is not a fix. The verified 15:07 installer remains unchanged and is not presented as solving the acrylic preview.

Current delivery outcome (2026-08-28): production direct-edge tests passed 2/2 (quicknote-direct-edge-production.trx), with screenshots independently inspected for both sides. Full regression passed 1504 with 5 opt-in tests skipped (quicknote-inner-grip-regression.trx). The publish assembly equals the tested Release assembly: SHA256 22D33C601DF9C1D1B2D90CBC2FCA6B87BE688114F9766E0145830DECC46D42EF. Release build/publish succeeded with NU1900 vulnerability-service access warnings; network escalation for a fresh locked restore was rejected by the environment. No audit setting or dependency version was weakened to hide this. Publish used existing locked assets with --no-restore, then Build-Installer.ps1 -SkipPublish packages that exact output. Installer compression is still running. This delivery improves direct note-edge dragging without a preliminary click; it does not disable previews when another app owns the operation.

Editing-regression outcome (2026-08-29): ordinary clicks on recognized URL/e-mail/phone text now remain editor clicks and place the caret; Control activates links and shows the hand cursor. The custom ScrollViewer was removed as unnecessary risk while the compact six-unit ScrollBar and two-unit indicator remain. Clean Release build: 0 warnings/errors. Full suite: 1507 passed, 0 failed, 5 opt-in desktop tests skipped. Exact published DLL Quick Note suite: 226 passed, 0 failed, 5 opt-in tests skipped. Installer `AiteBar-Setup-1.15.17.exe` is 80,853,240 bytes; SHA256 `9AF6D00D2FF5534F979D5293FBABDA136D4F4ADEED034369E4D513F94B9119B2`; unsigned because no signing certificate was supplied.

Empty-editor outcome (2026-08-29): Quick Note has no placeholder element, visibility branch or `QuickNote_Placeholder` localization key. Empty notes render as a clean editor while text selection, character/line statistics and image-only statistics continue to pass. Clean Release build: 0 warnings/errors. Full suite: 1507 passed, 0 failed, 5 opt-in desktop tests skipped. Exact published DLL Quick Note suite: 226 passed, 0 failed, 5 opt-in tests skipped. Installer `AiteBar-Setup-1.15.18.exe` is 80,851,178 bytes; SHA256 `14B87F77A54A4CA1DCC51205883050C2A8DA4FC035AA01D0F1DEAE71E4F34FF1`; unsigned because no signing certificate was supplied.

## Context and Orientation


Repository root is `D:\01_Codebdbd\01_projects\aitebar`. `AiteBar/QuickNoteDocumentFormatting.cs` creates task controls; `AiteBar/QuickNoteWindow.Editor.cs` handles every image insertion route. `QuickNoteImageHelper.cs` and `QuickNoteDocumentCodec.cs` serialize image payloads. `QuickNoteWindow.xaml` and `QuickNoteUtility.cs` configure the native window and its owner. `QuickNoteWindowCloseTests.cs` contains actual HWND tests and an opt-in desktop capture; HWND means the native Windows handle. `QuickNoteWindowFormattingTests.cs` contains STA-thread editor tests, where STA is WPF's required threading mode. `QuickNoteDocumentFormattingTests.cs` tests document formatting. The installer script is `installer/Build-Installer.ps1`.

## Plan of Work


First normalize the check geometry and explicitly size/center its visible stroke, testing its rendered bounds. Next split the insertion paragraph through WPF text pointers inside one undo group, preserving text formatting on both sides, and insert the existing image inline in its own paragraph. Place the caret after the image and test selection replacement, empty notes, styled text and save/reload. Finally use independent ordinary window semantics for Quick Note and validate Snap beside a second synthetic window. Keep all native interaction in Windows; do not write shell settings or manipulate real user documents.

## Concrete Steps


From the repository root run `dotnet build .\AiteBar.sln -c Release -m:1`, then `dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release --no-build --no-restore -m:1`. During development use focused QuickNote filters. Interactive desktop tests require permission, show only disposable synthetic notes/windows, and restore cursor/focus where possible. Inspect captured images, not only RenderTargetBitmap output, for native window behavior. After passing checks, verify the resolved publish directory is inside the repository and run `.\installer\Build-Installer.ps1`.

Milestone 1 is the centered check. Set `AITEBAR_QUICKNOTE_RENDER_DIR` to `artifacts/quicknote-validation` and run the test command with `--filter FullyQualifiedName~TaskCheckmark_VisibleStrokeIsCenteredInsideBox`. Both theme cases must pass; inspect the light and dark checkmark PNGs to confirm the stroke is centered rather than merely its layout box.

Milestone 2 is separate image insertion with reliable native history. Run with `--filter FullyQualifiedName~InsertImage_IsolatesImageAndPreservesTextAcrossUndoAndReload`; all five cases must pass. The middle-of-text capture must show text above and below a picture occupying its own line. Tests verify the same paragraph structure after Undo/Redo and native document save/reload. Native Image is WPF's built-in image control; XamlPackage is WPF's document archive format, which packages image bytes together with document markup.

Milestone 3 is ordinary native window behavior. In an approved interactive desktop session set `$env:AITEBAR_QUICKNOTE_TEST_SNAP='1'` and run `dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release --no-build --no-restore -m:1 --filter FullyQualifiedName~QuickNoteSnapIntegrationTests --logger 'trx;LogFileName=quicknote-snap-desktop.trx'`. Do not move the mouse during its approximately 20-second run. All four cases must pass, with common boundaries moving and external edges stationary. Clear that environment variable before the ordinary full regression run. This check requires Windows Snap and shared resizing to be enabled in the user's existing desktop settings; the test does not change those settings.

## Validation and Acceptance


Visible stroke bounds should be centered within the checkbox with subpixel tolerance. New images should have no adjacent text at insertion, after Undo/Redo and save/reload; prior embedded-image documents must still load. Real Snap testing must show the two synthetic windows changing width together when their shared boundary is dragged, with their outer edges stationary. Test both note sides, verify native caption/buttons and pin auto-dismiss behavior. Build and tests must pass. The nonempty `artifacts/installer/AiteBar-Setup-1.15.14.exe` must have a matching version and SHA256 entry in `SHA256SUMS.txt`.

## Idempotence and Recovery


Do not touch the user's stored note, unrelated dirty files, desktop layouts or registry settings. Tests use unique temporary storage and close only their own windows. Retry failed tests after inspecting evidence; do not mark a skipped interactive check as passed. Installer builds replace only generated artifacts under the verified repository paths. No package updates or data migrations are planned.

## Artifacts and Notes


Reports are `AiteBar.Tests/TestResults/quicknote-input-snap-full.trx` and `quicknote-snap-desktop.trx`. Screenshots are `artifacts/quicknote-validation/quicknote-checkmark-lemon.png`, `quicknote-checkmark-dark.png` and `quicknote-image-block.png`. The offscreen image test explicitly flushes the existing debounced footer update before capture, because it has no dispatcher loop. Installer: `artifacts/installer/AiteBar-Setup-1.15.14.exe`; SHA256: `4FB6CA562BB5A2C1B90E7384FD5227FB77E6C885020FC1062FF108CC15B81158`. The installer is unsigned because no certificate was supplied.

## Interfaces and Dependencies


Use WPF TextPointer paragraph splitting, Path geometry and WindowChrome, plus existing Win32/DWM interop. Retain IQuickNotePersistence and the native XamlPackage format. Native input/capture helpers belong in tests only and must be explicitly enabled. No new runtime dependencies are needed.

Created on 2026-08-28 to cover the three reported interaction defects and make shared Snap verification explicit.

Updated on 2026-08-28 with the tested image/checkbox fixes and the discovered WPF Undo serialization constraint; Snap desktop validation remains in progress.

Updated later on 2026-08-28: Snap desktop validation completed, including reference windows. Tests now explicitly skip interactive checks unless opted in, and save native coordinate evidence for failures as well as successes.

Final update on 2026-08-28: recorded completed regression, real desktop evidence and verified installer; synchronized user documentation with standalone image insertion and ordinary window behavior.

Follow-up opened 2026-08-28 after the user reported disappearance while snapping. Earlier installer/test results above describe the previous delivery; this follow-up requires fresh evidence and a replacement installer. Add unit coverage for the native move loop and shell surface classification, run UnpinnedNote_SnapKeepsVisible_ThenOrdinaryDepartureDismisses with the existing opt-in environment variables, verify ordinary departure still dismisses, then rebuild and hash the installer.

Follow-up evidence: quicknote-unpinned-snap-before.trx reproduces CLOSED after shell Snap activation. quicknote-unpinned-snap-after.trx passed both edge drags and seven focused checks (9/9). quicknote-snap-desktop-fixed.trx later passed 7/8 cases, including keyboard Snap, left drag with ordinary departure, and every shared resize case. The right-drag attempt was interrupted before entering arranged state; a subsequent run explicitly detected the cursor 31 pixels away from its commanded position and another lost foreground to an unrelated window. The final rerun must not be reported as all passed. Test diagnostics now reject an obstructed caption and external cursor movement, use native SendInput motion and avoid changing system settings. Current source changes are limited to the note's auto-dismiss decision; pin preference and native layout are unchanged. Full suite and installer verification are running.

Follow-up automated verification completed: AiteBar.Tests/TestResults/quicknote-snap-dismiss-regression.trx reports 1504 passed, 0 failed, 2 skipped. The skipped theories require real desktop input and their separate results/limitations are recorded above. No mouse or keyboard blocking was introduced to force a pass.

Follow-up installer verified on 2026-08-28T15:07:08: artifacts/installer/AiteBar-Setup-1.15.14.exe, 80849166 bytes, version 1.15.14, SHA256 51C5EFAAE9060E1BCF23A12A22F42E2691BACB04824D1251E373BEAD4BDFFC29. This replaces the earlier 14:34 installer; both publish and installer contain the fix. Signing was skipped because no certificate was supplied. Automated acceptance is complete; no claim is made that the final repeated desktop run passed without interference.

## Resize rendering investigation (2026-08-28)


The user reports that geometry changes correctly but the note becomes gray while the shared boundary is held. The previous tests only compared bounds after release. Extended QuickNoteSnapIntegrationTests to capture actual desktop pixels before, during and after the drag, and record the native window/process under each surface. A comparison with ordinary WPF windows already reproduced the same blurred gray replacement on the neighboring window. Added a separate WinForms/GDI baseline to distinguish shell behavior from WPF rendering. This investigation has not changed production code. Capture failures caused by a missed drag or external pointer movement are not accepted as rendering evidence. The next milestone is identifying the gray surface owner and comparing both UI frameworks; only then decide whether a runtime fix is justified. Re-run Release and the full test suite before delivery.

Resize investigation result: AiteBar.Tests/TestResults/quicknote-resize-native-comparison.trx passed 2/2 final desktop cases. Actual captures are artifacts/quicknote-validation/resize-True-False-step-6.png and resize-True-False-after.png for Quick Note, and resize-False-True-native-step-6.png plus resize-False-True-native-after.png for WinForms. Both show the gray neighbor preview only during the operation. No production code changed in this investigation. The existing 15:07 installer remains the current application build. Full regression after test-only changes is running; user confirmation is still useful if their gray surface persists after release rather than only during the drag.

Final resize-investigation verification: quicknote-resize-investigation-regression.trx reports 1504 passed, 0 failed, 3 skipped; the separate final desktop comparison reports 2 passed. Existing installer checksum is still 51C5EFAAE9060E1BCF23A12A22F42E2691BACB04824D1251E373BEAD4BDFFC29. The application binary was not modified for this investigation; only tests and documentation changed.

## Keep note readable during shared resize (2026-08-28)


The user clarified that explaining the gray shell preview does not solve the usability requirement: the text must remain visible during resizing. Investigate a supported interaction that preserves native Snap; do not invent a global setting change or a custom window manager. Microsoft Windows Insider Build 22543 release notes explicitly document the acrylic replacement for other snapped windows during resize. Added an opt-in NoteEdge_KeepsNoteContentVisibleDuringSharedResize test which grabs the border from inside the note, verifies foreground text pixels while held, and still requires both windows to resize. Initial trials did not move the common boundary, so they are not accepted as proof of a workaround. Removing an unnecessary test activation step avoids temporarily making an already snapped window topmost. No runtime code has changed during this investigation.

Follow-up outcome: clicking inside Quick Note before grabbing its border works without changing topmost, native Snap, or application behavior. Both note-left and note-right cases passed, including foreground text-pixel checks during two intermediate drag frames and shared-boundary geometry checks after release. Evidence: AiteBar.Tests/TestResults/quicknote-note-edge-visible.trx, artifacts/quicknote-validation/note-edge/resize-True-False-step-6.png (note visible on right) and resize-True-True-step-6.png (note visible on left). Previous attempts that only shifted the border hit point without first activating the note did not consistently retain the note. The requirement to keep it visible while a different app initiates resize is not implemented and must not be described as fixed. No runtime code or installer changes are part of this follow-up; documentation now states the tested workaround explicitly.

DWM prototyping milestone (2026-08-28): temporarily add a test-only AITEBAR_QUICKNOTE_DWM_EXPERIMENT selector restricted to attributes 3 and 12. These documented attributes respectively suppress DWM transitions and exclude a window from fading during Peek; neither is documented as a Snap control. Run SharedBoundary_ResizesBothWindows with each attribute in an approved desktop session and separate artifacts/quicknote-validation/dwm-3 and dwm-12 output directories. Compare actual held-button frames with the unchanged reference windows; geometry-only passing tests do not prove content visibility. Promote a flag only if it keeps the note readable without breaking shared resizing. Otherwise remove this experimental selector, preserve diagnostic evidence, and keep the unresolved requirement explicit. No production changes or global Windows settings are part of this experiment.

Final DWM experiment update (2026-08-28): reports quicknote-dwm-3.trx and quicknote-dwm-12.trx each contain four successful geometry cases; artifacts/quicknote-validation/dwm-3/resize-True-False-step-6.png and dwm-12/resize-True-False-step-6.png demonstrate failed visibility acceptance. The test prototype is preserved as artifacts/quicknote-validation/QuickNoteSnapIntegrationTests.dwm-prototype.cs.txt, not compiled into the project. Active source was restored after removing the ineffective experiment. No runtime or installer changes were made. Remaining behavior requires a supported Windows mechanism not found here; the user requirement is explicitly still open.

## Direct edge capture milestone (2026-08-28)


The next bounded experiment addresses the extra preliminary click, not the shell preview when a different app owns the drag. Extend the existing interactive test helper with directEdge mode: keep the peer foreground, use a point nine pixels inside the visible note boundary, verify that the HWND under the pointer is the note and its native hit code is HTLEFT/HTRIGHT, then drag and assert readable pixels and shared geometry. Temporarily widen only horizontal WindowChrome resize bands from eight to sixteen units in the synthetic note; top/caption geometry remains unchanged. This may expose the note's own border beyond the peer's invisible resize frame. If direct native input does not preserve note visibility, reject the prototype. If it passes, apply a minimal production change, cover affected hit testing and editor controls, run all tests, and rebuild/verify the installer. No hover activation, global hooks, foreign-window manipulation or Windows settings changes are allowed.

Direct-edge milestone update: prototype saved at artifacts/quicknote-validation/QuickNoteSnapIntegrationTests.direct-edge-prototype.cs.txt; evidence reports are quicknote-direct-edge-prototype.trx and quicknote-direct-edge-trace.trx. All four attempted cases failed, with the latter two explicitly detecting cursor displacement before button-down. The native hit zone is reachable, but no claim of an automatic fix is made. To resume, restore the isolated prototype into the test file after checking it against current source, build Release, and run InactiveNoteEdge_ResizesWithVisibleTextWithoutPreclick with AITEBAR_QUICKNOTE_TEST_SNAP=1 during an approved 15-second interval without user mouse input. If it succeeds, promote only the justified chrome change and run the complete verification/installer steps. Runtime code and installer remain unchanged for this milestone.

Resumed direct-edge update (2026-08-28): user supplied an idle-input interval. Prototype passed 2/2 desktop cases; captures are artifacts/quicknote-validation/direct-edge-idle/resize-True-False-step-6.png and resize-True-True-step-6.png. These show the note readable during native shared resize. The production change now uses a narrower 12-unit side band to avoid covering text, with toolbar and scrollbar priority; its native hit-test checks passed 2/2. Final production desktop validation, full regression and a new 1.15.15 installer remain required. Broad visibility when another app initiates the operation remains a separate unresolved Windows behavior, not part of this fix.

Final production verification update (2026-08-28): direct-edge-production captures show readable note text during the held-button drag on either side, with the common boundary moving from x=960 to x=1080 and outer edges stable. This is the actual production 12-unit chrome, not the initial 16-unit test prototype. All changes are limited to window hit-test geometry, control priority, focused tests, documentation and synchronized release metadata; no focus hooks, foreign-window layout code or global settings were introduced. The prior 1.15.14 installer is preserved under artifacts/installer-archive/1.15.14-20260828-150708. Await current installer completion and verify its version/size/hash before delivery.

Delivered installer verification: artifacts/installer/AiteBar-Setup-1.15.15.exe, version 1.15.15, 80844688 bytes, built 2026-08-28T19:34:43.3616286+03:00, SHA256 B539AB2289AB3E520A30453DE02978DEF5D3DCE0D5D4D2F25D808CF2970D25FD. Manifest matches; app/publish/installer versions agree. Compression finished successfully. No certificate was supplied, so the installer is unsigned. All required direct-edge behavior and automated checks for this scoped change passed; the separate broader requirement to suppress the shell preview when another application initiates resizing remains unresolved.

## Compact scrollbar milestone (2026-08-28)


The user supplied a Windows Sticky Notes reference and requested a thinner scrollbar closer to the right edge without wasting editor space. Scope is QuickNoteWindow.xaml only, not global App.xaml scrollbar styles or other utilities. Use local ScrollViewer and ScrollBar templates with the standard PART names, routed scroll commands and ScrollContentPresenter; reserve six device-independent units for pointer input and draw a two-unit thumb two units from the window edge. Keep the existing editor left margin, theme colors and native window chrome. Expand the thumb inside its fixed track on hover/drag, without changing layout. Extend NativeChrome_ExposesAllResizeEdgesAndCaptionButKeepsButtonsInteractive to measure actual indicator bounds, viewport reserve, scrollbar dragging and the adjacent resize grip in lemon, lavender and dark themes; inspect the saved client renders. Then run Release/full tests and package a version-synchronized new installer. No data format, clipboard, persistence, localization command or utility-registration change is involved.

Compact-scroll verification update: initial focused tests passed in lemon, lavender and dark. The final focused assertions also pass thumb-drag, mouse-wheel scrolling and complete release of the column for short content. Client renders in artifacts/quicknote-validation/compact-scroll were inspected and match the requested thin edge placement; final-version renders are under compact-scroll-final. First full regression had only two failures: the old explicit right-margin expectation and a brittle first-Grid selector. These assertions were updated to the intended new margin and actual root content, without removing coverage. Final full regression is running; installer 1.15.16 is being compressed from the published output. Build succeeded without warnings/errors after a successful NuGet restore; no package versions were changed.

Compact-scroll delivery evidence: quicknote-compact-scroll-final.trx reports 1505 passed, 0 failed and 5 explicitly skipped interactive Snap checks. No mouse-driven Snap retest is claimed for this cosmetic scrollbar change; native hit tests cover the nearby grip. Installer artifacts/installer/AiteBar-Setup-1.15.16.exe is 80858830 bytes, built 2026-08-28T20:52:43.7455976+03:00, SHA256 BC00024D7BED4CF33A67177CE04A861C8BD72B20AFA724261DDAFF1DE06440B9. Manifest and app/publish/installer versions match. Unsigned because no certificate was supplied. The previous installer is preserved in artifacts/installer-archive/1.15.15-20260828-193443.

Published-binary verification: after rebuilding the ordinary app while correcting two test expectations, the ordinary DLL hash differs from publish. Rather than claim byte identity, temporarily replace only the generated test-output AiteBar.dll with the exact published DLL (SHA256 BC2D4723F8F7483AB7EBA9004E6B6983ACB2CCC44C5DFC9F2DF05AFAD07A86C9), run the unchanged full suite with --no-build --no-restore, and restore the original generated DLL in finally. This does not change source or installer payload. Report quicknote-compact-scroll-published.trx is running; do not report it passed yet.

Final compact-scroll outcome: quicknote-compact-scroll-published.trx passed all 1505 noninteractive tests with five explicitly skipped desktop checks. Published-client lavender render was visually inspected; the scrollbar is thin and close to the right edge, with original header, content left inset and footer retained. The original generated test assembly was restored and its hash checked. Deliver the verified 1.15.16 installer; no remaining work for the requested scrollbar change. Earlier broader Snap-preview limitations remain documented separately.
