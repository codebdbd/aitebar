# Repair the IPTV picture and channel switching

This living plan is maintained under `PLANS.md`. Keep Progress, Surprises & Discoveries, Decision Log, and Outcomes & Retrospective current.

## Purpose / Big Picture

The IPTV player should show a black screen while it is idle or opening a channel. The video rectangle should retain 16:9 at different window widths and heights. A user should be able to tune another station by clicking it once or pressing Page Up or Page Down. Scrolling and filter changes should leave the playing channel alone. Rapid tuning should settle on the last station requested.

The next iteration makes the window useful on ordinary remote playlists. The channel list uses less space, the status is visible when waiting for video, and the viewing controls offer a reversible crop. Measure local stream startup and compare it with a delayed response to establish what the player can control.

## Progress

- [x] (2026-11-16 01:46Z) Simplified the channel list and controls and added the Fill mode and visible playback states.
- [x] (2026-11-16 01:48Z) Tuned opening and captured request-to-video-output timings.
- [x] (2026-11-16 01:50Z) Verified both local HTTP streams, the Fill mode, and the full Release test suite.

- [x] (2026-11-15) Traced the white surface, the stretch layout, and the channel navigation handlers.
- [x] (2026-11-15 17:28Z) Connected a fixed-ratio viewport and upgraded both managed LibVLCSharp packages to 3.9.4.
- [x] (2026-11-15 17:29Z) Serialized playback commands and connected explicit channel tuning.
- [x] (2026-11-15 17:32Z) Passed the Release build and full test suite.
- [x] (2026-11-15 17:38Z) Verified the black background, both channel inputs, and the video proportions in a Windows desktop fixture.

## Surprises & Discoveries

The video host in `AiteBar/IptvPlayer/IptvPlayerWindow.xaml` stretches to the entire row even though its comment says 16:9. Its background brush paints a WPF layer behind the Win32 child window that actually displays video. The VideoLAN release notes identify a white-background fix in LibVLCSharp 3.9.4. The project pins 3.9.0. The last navigation change moved preview and playback apart but left Page Up and Page Down moving only the highlight.

The sidebar column's minimum width prevented the previous zero-width setting from clearing the full sidebar area. The hide and restore routines now adjust its minimum width too.

In LibVLC 3, assigning media releases the previous input. Calling Stop first also terminates output resources. This allowed us to keep the existing API and remove redundant teardown during channel switches.

## Decision Log

Use the first LibVLCSharp version containing VideoLAN's native host fix, with the two managed package references kept at the same version. Use WPF layout to fit the 16:9 viewport. Retain the source video's original proportions inside that viewport. Date/author 2026-11-15, Codex.

Keep one LibVLC player and one render target while switching channel sources. Use a serial command queue and request generations, meaning numbers identifying the newest tuning request, to skip outdated work. Start tuning on a channel click or the dedicated channel keys. Date/author 2026-11-15, Codex.

Use a text-only channel list to avoid placeholder clutter and logo fetches during browsing. Use a 750 ms first-open buffering configuration and 1500 ms for reconnects, with a 12-second startup deadline. Let LibVLC choose the decoder. Date/author 2026-11-16, Codex.

## Outcomes & Retrospective

Release verification passed 1812 tests with 5 skipped. A Windows fixture using the Release assemblies showed black before playback and after Stop. Page Down changed the picture to a local 4:3 clip and a single click changed it back to a local 16:9 clip without invoking the transport controls. The picture kept its proportions after entering and leaving fullscreen. Remote stream opening time was outside the verification run.

The second Release verification passed 1815 tests with 5 skipped. Desktop playback switched from a local 16:9 HTTP stream to a 4:3 stream whose server delayed each response by 1200 ms. The latter filled the entire 800 by 475 pixel area when Fill was selected. The available video left about 13 pixels above and below a 16:9 stream in the compact default window. Provider servers were outside the verification run.

## Context and Orientation

`AiteBar/IptvPlayer/IptvPlayerWindow.xaml` places the LibVLCSharp WPF `VideoView`, the channel list, and playback controls. `AiteBar/IptvPlayer/IptvPlayerWindow.xaml.cs` handles explicit user actions and updates the visible state. `AiteBar/IptvPlayer/IptvPlaybackService.cs` owns the single native VLC player. IPTV catalogs and playlist refresh are managed elsewhere and continue supplying channel URLs and quality variants.

## Plan of Work

Update the matching LibVLCSharp packages in `AiteBar/AiteBar.csproj` and regenerate the affected lock files. Add a WPF decorator in the IPTV directory that measures and arranges its child inside the largest centered 16:9 rectangle available. Keep the video host hidden over a black container until playback has picture output. Correct the sidebar minimum width when hidden so maximizing and fullscreen give the video the available area.

Update the channel input handlers so single click and Page Up or Page Down tune the selected channel and list selection alone remains passive. Serialize stop, tuning, and other VLC commands in the playback service. Each request must supersede older queued requests, including reconnect work. Add layout, input, and command regressions to `AiteBar.Tests/IptvPlayerTests.cs`.

## Concrete Steps

From the repository root, run:

    rtk proxy dotnet restore .\AiteBar.sln
    rtk proxy dotnet build .\AiteBar.sln -c Release -m:1 --no-restore
    rtk proxy dotnet test .\AiteBar.sln -c Release -m:1 --no-build --no-restore

The build should finish with zero errors. If the sandbox prevents VSTest from connecting to its child process, use the approved escalation flow for the test command.

## Validation and Acceptance

The fitted viewport must have a 16:9 ratio inside both tall and wide rectangles. Open the IPTV window with no playback and expect a black rectangle. Switch stations with a single row click and Page Up or Page Down. Scrolling the list and toggling favorites must leave playback intact. In fullscreen, check the same aspect ratio and channel navigation. The automated command test should prove stale requests are skipped and the newest request is applied.

For the compact layout, play a local 16:9 clip through HTTP and switch to a local 4:3 clip. Choose Fill and confirm the black side margins disappear without distorting the image. Read startup durations from the local AiteBar log. The clock begins when tuning is requested and ends when LibVLC reports both playback and video output.

## Idempotence and Recovery

Existing settings and playlist data require no migration. Rebuild with the refreshed lock files. If a stream fails, only the latest selected channel may reconnect. Stop cancels pending tuning and reconnect work.

## Artifacts and Notes

The user supplied screenshots showing the idle native child as white and a video viewport taller than 16:9.

Observed the 16:9 native picture occupy about 670 by 377 pixels in the default window and 1764 by 992 pixels fullscreen. The 4:3 clip kept black side margins. Test coverage includes wide, tall, and zero-height layout bounds and a queued switch that skips an intermediate request.

    IPTV video ready: total 290 ms, queue and media change 4 ms.
    IPTV video ready: total 3783 ms, queue and media change 30 ms.

## Interfaces and Dependencies

Continue to use LibVLCSharp and LibVLCSharp.WPF. Use a WPF `Decorator` for aspect ratio and .NET tasks for asynchronous playback work.

Revision note, 2026-11-15. Created the plan after confirming the host version and the missing aspect-ratio constraint.

Revision note, 2026-11-15. Recorded the package upgrade, the sidebar sizing dependency, and the Release and desktop verification results.

Revision note, 2026-11-16. Expanded the scope after real-playlist screenshots showed unused video space and repeated channel labels. Editing the compact workspace and measuring live startup are in progress.

Revision note, 2026-11-16. Recorded the completed compact layout, playable HTTP checks, and measurements for immediate and delayed responses.
