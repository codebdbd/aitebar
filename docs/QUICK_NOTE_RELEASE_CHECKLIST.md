# Quick Note: Pre-Release Checklist

Use this checklist before publishing a release that changes Quick Note, its persistence, formatting, themes, links, images, or window behavior. Quick Note is a single rich-text note stored by default as `QuickNote.aite-note` in the application-data folder. Its service also supports legacy RTF loading/writing; the current window does not expose an RTF export command.

Do not release when a blocking check fails. Record the version, commit, Windows version, theme used, command outputs, and any manually observed result in the release ticket or pull request.

## Release Gates

All gates must pass before manual testing begins.

1. Build Release from the repository root:

       dotnet build .\AiteBar.sln -c Release

   Expected: `Build succeeded` with zero warnings and errors. If the known environment anomaly reports `Build FAILED` with zero diagnostics, use a serial solution build (`-m:1`) or build the test project separately, record the anomaly, and do not treat it as evidence of a clean solution build:

       dotnet build .\AiteBar.Tests\AiteBar.Tests.csproj -c Release --no-restore

2. Run all Quick Note tests:

       dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release --no-build --filter "FullyQualifiedName~QuickNote"

   Expected: zero failed tests. The set must cover persistence, formatting, code blocks, quotes, dividers, task lists, links, images, save/close behavior, layout, and themes.

3. Run the remaining tests:

       dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release --no-build --filter "FullyQualifiedName!~QuickNote"

   If `dotnet test` exceeds the terminal timeout or hits an MSBuild WPF temporary-file issue, run the built DLL instead:

       dotnet vstest .\AiteBar.Tests\bin\Release\net10.0-windows\AiteBar.Tests.dll

   Expected: zero failed tests.

4. Check the diff for whitespace errors:

       git diff --check

   Expected: no whitespace errors. Line-ending notices must be reviewed separately and must not hide a real diff error.

5. If this is a publishable release, build the installer:

       .\installer\Build-Installer.ps1

   Verify that exactly one non-empty installer exists in `artifacts\installer` and that its version matches `AiteBar\AiteBar.csproj`, assembly metadata, and the release tag.

## Manual Smoke Test

Perform this against the Release build or the built installer, not only a Debug build. Begin with a temporary or backed-up note so recovery behavior can be assessed safely.

1. Open Quick Note from its panel button and tray path. Confirm the window appears quickly, receives keyboard focus, and shows the last note.

2. Enter plain text, multiple paragraphs, a heading, bold, italic, underline, strikethrough, inline code, a hyperlink, a quote, a divider, a bulleted list, a numbered list, a code block, an image, an unchecked task, and a checked task.

3. Create a task containing an embedded image. Close and reopen Quick Note. Confirm that task state and text remain, the image remains available, and the original note did not become corrupted or lose its checkbox.

4. Apply strikethrough to a hyperlink. Change the Quick Note theme, close the note, then reopen it. Confirm the link remains clickable, remains underlined, and still has its user-applied strikethrough.

5. Select only the middle of a formatted hyperlink and use Clear Formatting. Confirm the selected fragment becomes plain text while both unselected link fragments retain their URL, font family, size, style, weight, stretch, background, and other visible formatting.

6. In a light theme and a dark theme, create a code block and then reopen the note. Confirm the code header, `code` label, and Copy glyph use code-theme colors rather than the ordinary window text color. Click Copy and confirm the code body reaches the clipboard.

7. In each available theme, verify normal text, muted status text, links, inline code, code blocks, quotes, dividers, completed tasks, and toolbar icons. Check that a theme switch updates existing content without turning links or quote children into stale colors.

8. Test Markdown-compatible behavior: URLs open with an ordinary click, unsafe URLs are rejected, link highlighting has a sensible fallback on long notes, and underline survives the relevant save/reload path.

9. Test editing behavior: `Ctrl+Z`, `Ctrl+Y`, Enter in the middle and at the end of a task, Enter on an empty task, Backspace task-prefix conversion, list conversion, and Clear Formatting across text, links, code blocks, and quotes.

10. Confirm checklists appear only in the formatting toolbar, never in the context menu. Check visible keyboard focus and all formatting buttons at 460×320 and 580×430 in dark and light themes.

11. Test images: paste, drag/drop, select, delete, copy, save/reopen, and verify the image size limits reject invalid or excessive payloads without corrupting other note content.

12. On Windows 11, confirm a floating note has system-rounded corners and only controls in the top strip, no visible utility title. Drag the empty strip; double-click to maximize/restore; right-click for the system menu. Windows should remove rounding when snapped/maximized and restore it when floating. Resize from all four edges and all four corners, and verify pin, Undo/Redo, palette and Close still respond to clicks. A WPF RenderTargetBitmap shows only client content, not the DWM contour; it is not evidence that the desktop window has rounded corners.

13. On Windows with Snap enabled, pin the note so it remains open, snap it beside another resizable window using Snap Assist, and drag their shared boundary in both directions. Both windows should resize together, subject to their minimum widths. Repeat with the note on the other side, then detach and re-snap it. Native HWND hit-test tests verify resize/caption routing, but do not replace this shell-level scenario or change the user's multitasking settings.

For an opt-in desktop corner check in an interactive Windows 11 session, set `AITEBAR_QUICKNOTE_RENDER_DIR` to a writable output folder and `AITEBAR_QUICKNOTE_CAPTURE_DESKTOP=1`, then run the test filter `FullyQualifiedName~NativeChrome_ExposesAllResizeEdgesAndCaptionButKeepsButtonsInteractive`. It briefly shows synthetic notes over a solid test backdrop, saves `quicknote-desktop-rounded-<theme>.png`, checks that the backdrop is visible through all four corners, and closes both test windows. It does not load or modify the user's note. The normal test run stays offscreen and verifies DWM corner preference, absence of a custom window region, native resize/caption hit tests, and button interaction. Windows VM/remote-session policies may suppress DWM rounding; do not claim a desktop visual pass in such an environment without the screenshots. See [Microsoft's corner policy](https://learn.microsoft.com/en-us/windows/apps/desktop/modernize/ui/apply-rounded-corners).

## Persistence And Recovery

1. Make the document above, wait for the saved status, close Quick Note, restart AiteBar, and reopen it. Confirm all user-visible formatting and task state survive the physical `.aite-note` save/load cycle.

2. Test legacy RTF migration using a copied `QuickNote.rtf` with no package present. Confirm successful load creates the package without deleting the RTF. The window has no Open in Editor command; do not infer one from service-level RTF tests.

3. Simulate an external change by editing or replacing the note file while Quick Note is open. Make a local edit and confirm the application neither silently overwrites the external file nor discards the local document. Verify the conflict-copy status and open the created copy.

4. Verify recovery behavior with a copied malformed RTF and malformed package file. Quick Note must show a load-failure status and preserve the original byte-for-byte. New edits must be saved to a separate conflict copy, not over the damaged source.

5. Check that at most five conflict copies remain after repeated conflict saves and that the latest conflict copy can be revealed in Explorer using the footer button. Test Ctrl+S retry after a transient write failure and confirm close waits for edits made during both ordinary and conflict saves.

## Window And Interaction

1. With pin off, click outside Quick Note and confirm it closes after transient dialogs and menus are dismissed.

2. With pin on, click outside and confirm it remains open. Toggle pin off again and confirm auto-dismiss returns.

3. Resize and move the window near every edge of the current monitor, close it, and reopen it. Confirm saved bounds are restored and clamped to the usable monitor work area.

4. Repeat the bounds check on a second monitor if available, including disconnecting that monitor before reopening. The window must remain visible on an available display.

5. Open theme, link, context, and image menus. Verify they do not cause auto-dismiss, focus loss, accidental saves, or stuck keyboard input.

## Release Evidence

Record the following before approving the release:

- Application version, commit SHA, Windows version, and monitor configuration.
- Result and test count for the Quick Note test filter.
- Result and test count for the remaining test suite or fallback `vstest` run.
- Release build result, including any zero-diagnostic solution-build anomaly.
- Installer filename, size, version, and SHA-256 when an installer is built.
- Manual checklist result, tester, and any waived scenario with a linked issue.

## Blocking Criteria

Block the release for any lost text, lost user formatting, task-state change, broken save/reopen cycle, unhandled exception, invisible/off-screen window, unsafe URL launch, silently overwritten external change, failed automated test, or uninvestigated build diagnostic. Do not waive a data-loss scenario; fix it and add an automated regression test before release.
