# Repair Telegram scheduling and IPTV source handling

This is a living ExecPlan maintained under `PLANS.md`. Update Progress, Surprises & Discoveries, Decision Log, and Outcomes & Retrospective after each milestone.

## Purpose / Big Picture

Pair a Telegram scheduling server once, queue a post from the editor, and safely repeat the request after a lost response. Each paired device must control only its own channels, bot credentials, and uploads. In IPTV, saving a source should make its channels available, testing should use the URL shown in the form, and automatic refresh should continue while AiteBar is open. An export without secrets must omit stream credentials.

## Progress

- [x] (2026-11-15) Traced the eight reported defects through the desktop, backup, and server entry points.
- [x] (2026-11-15 11:15Z) Repaired Telegram pairing, queue requests, and server ownership.
- [x] (2026-11-15 11:15Z) Repaired IPTV source loading, credential storage, backups, and scheduled refresh.
- [x] (2026-11-15 11:17Z) Added five regression tests and passed Release verification.
- [ ] Complete the manual desktop smoke check.

## Surprises & Discoveries

The Telegram client already has a method to register a channel and share its bot token, but the editor never calls it. Desktop saves can advance a post revision even when the previous queue request reached the server. IPTV uses the same dialog builder for a live test and the settings representation, which masks passwords before the live test. The original server upload path accepts a relative path into a sibling directory whose name begins with the upload directory name.

`AppSettingsService.Settings` returns a clone. The original background refresh changed the cloned IPTV settings and then called save, which could not persist the refresh timestamps. The normal application starts with preloaded settings, which bypassed the original deferred refresh call.

## Decision Log

Device-scoped IDs preserve ownership on a shared Telegram server while allowing the same desktop data to be paired again. A stable key derived from the post and effective delivery payload identifies retries across editor restarts and revision-only changes. Exact repeated deliveries share a queue job. Date/author 2026-11-15, Codex.

Use Windows Data Protection API (DPAPI), which binds encrypted local data to the current Windows user, to protect cached IPTV URLs. In portable backups, put private data in the existing password-encrypted payload. Date/author 2026-11-15, Codex.

Persist a content revision on a changed IPTV source so a cache from an older definition cannot appear as the edited source. Keep source locations and playlist text in password-protected exports because providers can place credentials anywhere in a URL. Rebuild the derived catalog when it is needed after restore. Date/author 2026-11-15, Codex.

## Outcomes & Retrospective

Release verification passed 1806 tests with 5 skipped. The new HTTP test exercised the actual server with two paired devices and local uploads. No test sent a message to Telegram. The desktop smoke check is pending.

## Context and Orientation

`AiteBar/TelegramPostEditor/TelegramPostEditorWindow.xaml.cs` handles pairing and scheduling. `AiteBar/TelegramPostEditor/Services/PublicationSyncService.cs` sends requests to `TelegramPostEditor.Server/Program.cs`. `TelegramPostEditor.Server/Workers/PublicationWorker.cs` sends accepted jobs through Telegram. SQLite holds jobs, channels and upload metadata. AiteBar settings use JSON, while bot and device tokens use Windows Credential Manager.

`AiteBar/AppSettingsWindow.Iptv.cs` owns the source draft, `AiteBar/IptvPlayer/IptvPlaylistService.cs` loads sources, `AiteBar/IptvPlayer/IptvPlayerStore.cs` persists them, and `AiteBar/BackupService.cs` builds portable ZIP backups. The player reads the catalog from the playlist cache. `AiteBar/MainWindow.xaml.cs` owns application startup and shutdown.

## Plan of Work

First persist the normalized server address and device ID together, disclose token sharing in the queue UI, register selected bots and channels, and make retry identity stable. On the server validate ownership in writes and worker reads. Keep legacy queued jobs readable, validate upload IDs, and use paths chosen by the server. Add regression tests that exercise the HTTP endpoints with independent paired devices and test retry behavior.

Then share the IPTV fetch-and-parse path between check and refresh. Consume the current dialog URL for draft operations and write secrets only on settings save. Refresh new or changed sources after saving and keep automatic URL refresh running until application shutdown. Protect local cache data and include only safe player data in a backup without secrets. Test the import, refresh, and backup behavior through their normal entry points.

## Concrete Steps

From the repository root run:

    rtk proxy dotnet build .\AiteBar.sln -c Release -m:1
    rtk proxy dotnet test .\AiteBar.sln -c Release -m:1 --no-build --no-restore

Expect a successful Release build and no failed desktop or server tests. If VSTest cannot connect to its test process inside the sandbox, run the same command through the approved escalation flow.

## Validation and Acceptance

Pair a fresh editor with a local server, select an existing Telegram channel, and schedule a post. The server should accept the job and return the same job ID after an identical resubmission. Reopening the editor should recover the server connection. Automated coverage should reject cross-device updates and upload names containing path components, using disposable application data.

Save an inline M3U and a direct stream, then open the player and check that each appears. For authenticated playlists, verify the fetch receives the URL typed into the form. A fixture with a token in a stream URL should produce a portable backup without the token when secret export is off. The background refresh should continue after startup.

## Idempotence and Recovery

Use existing IDs for stored jobs and legacy reads. Repeat a failed queue request with the same delivery values. The server will reuse a successfully accepted job. Reading legacy IPTV data should remain possible, and backup export must protect existing plaintext data as well as new data. Use temporary directories in tests.

## Artifacts and Notes

The local path check reproduced the original bug with `media-other` as a sibling of `media`. No production server or live credentials were used.

The Release test run reported 1803 passing desktop tests, 3 passing server tests, and 5 skipped desktop tests.

## Interfaces and Dependencies

Reuse `PublicationSyncService`, `TelegramSecretStore`, Microsoft.Data.Sqlite, `IptvPlaylistService`, `IptvPlayerStore`, and the existing `BackupSecretPayload`. Use .NET cryptography and Windows DPAPI already present in the app.

Revision note, 2026-11-15. Created the plan to track the implemented repairs and their verification.

Revision note, 2026-11-15. Recorded the restored startup path, protected source revisions, and passing end-to-end HTTP and desktop regression tests.
