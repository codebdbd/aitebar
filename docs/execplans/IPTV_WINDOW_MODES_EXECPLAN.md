# Add viewing modes to the IPTV player

This living plan follows `PLANS.md`. Maintain Progress, Surprises & Discoveries, Decision Log, and Outcomes & Retrospective as work proceeds.

## Purpose / Big Picture

Users can watch IPTV next to the channel list, hide the list, switch to fullscreen, or float a small always-on-top window over other apps. The running channel continues during these transitions. The player remembers the floating window separately and restores the ordinary window and list on return.

## Progress

- [x] (2026-11-16) Inspected the existing playback host, fullscreen controls, and persisted state.
- [x] (2026-11-16 02:33Z) Implemented window mode transitions, controls, and persisted floating placement.
- [x] (2026-11-16 02:40Z) Passed the Release suite and checked the transitions on a playing HTTP channel.
- [x] (2026-11-16) Replaced the floating frame and transport bar with a single hover overlay and made the window 16:9.
- [x] (2026-11-16) Verified the revised overlay on Windows with and without a playing channel, and ran the Release suite.
- [x] (2026-11-16) Added the channel label, previous and next channel actions, and dedicated picture-in-picture icons.
- [x] (2026-11-16) Verified channel controls, keyboard focus, error visibility, and rapid switching on Windows.
- [x] (2026-11-16) Built Debug and Release and finished testing the final overlay. The AiteBar suite passed, and the server test failure is recorded below.

## Surprises & Discoveries

The current fullscreen code remembers only the window state and sidebar flag. Closing while fullscreen can save temporary geometry as the primary placement. The native video surface can intercept mouse movement before WPF receives it.

A native tool window was excluded from normal window discovery. A zero-width sidebar still left its search control in the WPF focus scope. The implementation collapses the sidebar with an explicit focus transfer. LibVLCSharp renders into a native child window and provides a separate foreground window for WPF content over the video.

The first Windows capture showed missing glyph boxes for the new icon characters. WPF vector paths render the same icons in both windows. The focus trace also showed that conditions in the template bound to the wrong element. The conditions now bind to the control that owns the template.

## Decision Log

Use the existing WPF window and video host for all modes. Use a short pointer-position poll in fullscreen to reveal or hide the transport controls over native video. Date/author 2026-11-16, Codex.

Remove the native frame and dedicate the entire floating window to video. Share one WPF template between the foreground window provided by LibVLCSharp and the idle black surface. The template reveals the return button on hover and handles drag from the remaining area. Retain the saved width and derive its height at 16:9 so older placements load without the space formerly occupied by the frame and controls. Date/author 2026-11-16, Codex.

Wait for 180 ms after the last PiP navigation input. Update the pending channel label immediately and then submit the final request through the existing player. Keep mouse focus from holding the overlay open after a click. Tab enables keyboard visibility and Esc restores the main player. Date/author 2026-11-16, Codex.

## Outcomes & Retrospective

Release verification passed 1817 tests with five skipped. On Windows, a running local HTTP video moved from the ordinary window to PiP, fullscreen, PiP, and back. The visible playback timestamp advanced and the log recorded only the original stream startup for that session. The fullscreen toolbar disappeared after inactivity, and video-only mode hid the channel list. The saved state contained independent PiP bounds. The fixture was a video-only stream. Multi-monitor geometry was verified by tests.

After removing the floating frame, the Release suite again passed 1817 tests with five skipped. Windows captures with the local HTTP clip and with an idle player showed a 16:9 surface and a single return arrow while hovering. The pointer test also moved the floating window and clicked the arrow to restore the preceding window bounds.

The expanded overlay displayed the current channel and the three controls over live video. Tab revealed the controls with the pointer outside, and Esc restored the main window without an additional HTTP request. Two rapid next-channel presses opened the final channel URL and skipped the intermediate one. The unavailable channel notice stayed visible with the pointer outside. The next-channel button returned to a working stream.

The final AiteBar Release test run passed 1814 tests and skipped five. Two server tests passed. A Telegram server test failed in its cleanup while deleting a locked SQLite file and reproduced on an isolated rerun.

## Context and Orientation

`AiteBar/IptvPlayer/IptvPlayerWindow.xaml` and its code behind contain the channel list, video host, and transport. `IptvPlaybackService` owns the running stream. `IptvPlayerState` stores playback preferences through `IptvPlayerStore`, and `AppSettings` stores ordinary window placement. The PiP position is stored as offsets from the monitor work area, the portion of the screen outside the taskbar.

## Plan of Work

Create a small view mode and placement helper in `AiteBar/IptvPlayer/IptvViewMode.cs`. Add window-specific transitions in `IptvPlayerWindow.Modes.cs`. Connect PiP and video-only buttons in XAML and extend the existing keyboard shortcuts. Keep the existing video ownership, selection, volume, and crop behavior. Save a snapshot of the ordinary window when entering PiP or fullscreen. Save PiP position and size independently in `IptvPlayerState` and use the screen scaling helper to restore it.

In `AiteBar/IptvPlayer/IptvPlayerWindow.xaml`, place the hover template in the video foreground and in the idle surface behind it. In `AiteBar/IptvPlayer/IptvPlayerWindow.Modes.cs`, hide the transport, switch to borderless style, and fill the video surface while PiP is active. Restore the ordinary window placement and crop preference on return.

Add channel information and navigation to the shared template in `AiteBar/IptvPlayer/IptvPlayerWindow.xaml`. In the mode code behind, use the same filtered channel order and navigation method as the keyboard shortcuts. Display the requested channel immediately and use a short wait before opening the final channel in a series of presses. Keep the last failure visible and retain the previous and next controls on hover or keyboard focus.

## Concrete Steps

From the repository root run:

    rtk proxy dotnet build .\AiteBar.sln -c Release -m:1 --no-restore
    rtk proxy dotnet test .\AiteBar.sln -c Release -m:1 --no-build --no-restore

Expect a successful build and no failing tests. If VSTest cannot connect to its loopback child process in the sandbox, use the escalation flow for the test run.

## Validation and Acceptance

Start a channel, activate PiP, enter fullscreen, then return to PiP and the ordinary window. The picture and audio should continue and the stream should be opened once. Verify that video-only hides the list, that fullscreen controls hide after inactivity, and that the separate PiP placement is clamped when a saved monitor is unavailable. Tests cover view transitions, position calculation, and persisted state.

Open PiP before and during playback. Move the pointer in and out to see the return arrow appear and disappear. Drag away from the arrow to reposition the video. Click the arrow and check that the ordinary window and list return. In PiP, the video occupies the 16:9 window and the idle surface is black.

With a fixture containing two local streams, enter PiP, move the pointer over the video, and check both channel buttons and the current title. Use each direction once and verify the request goes to the matching local URL. Press the buttons in quick succession and verify only the final URL in the series opens. With the cursor outside, press Tab in the active PiP window and verify that focus reveals the buttons. Esc returns to the standard player.

## Idempotence and Recovery

Repeatedly entering and leaving any mode should keep the initial ordinary window placement. PiP starts with a default lower-right position if there is no saved placement. Existing player state files load with the default floating placement.

## Artifacts and Notes

Use a disposable profile and a local channel for desktop smoke testing.

## Interfaces and Dependencies

Use WPF, LibVLCSharp, the existing WPF dispatcher, and the existing AiteBar screen geometry helper. No additional package is needed.

Revision note, 2026-11-16. Created the plan for PiP and the remaining window modes.

Revision note, 2026-11-16. Recorded the frame and focus adjustments found during Windows testing and the final Release verification.

Revision note, 2026-11-16. Revised the floating presentation to meet the requested hover-only design.

Revision note, 2026-11-16. Expanded the floating overlay with controls and status for live channel navigation.
