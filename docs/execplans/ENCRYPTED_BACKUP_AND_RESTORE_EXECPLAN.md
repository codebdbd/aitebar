# Add encrypted full backup and restore

This ExecPlan is a living document. The sections `Progress`, `Surprises & Discoveries`, `Decision Log`, and `Outcomes & Retrospective` must be kept current as work proceeds. It follows `PLANS.md` in the repository root.

## Purpose / Big Picture

Users will be able to create and restore AiteBar backups from Program Settings. A standard backup carries settings, panel buttons, and local icons. A full backup can additionally carry API keys and a one-time clipboard-history snapshot, but only inside a password-encrypted payload. Restore first creates a local safety backup and then refreshes the live panel configuration.

## Progress

- [x] (2026-08-08 09:10Z) Located settings persistence, AI Credential Manager integration, and clipboard history service.
- [ ] Define the backup archive and encrypted secret payload service with focused tests.
- [ ] Add the Settings UI and restore confirmation flow.
- [ ] Restore settings, icons, AI credentials, and optional clipboard snapshot safely.
- [ ] Build, test, and document the completed behavior.

## Surprises & Discoveries

- Observation: general settings already retain five rotating backups named `settings.json.backup.0` through `.4`.
  Evidence: `AppSettingsService.MaxBackupCount` is 5.
- Observation: API keys are not in settings JSON; they are stored by `WindowsAiCredentialStore` in Credential Manager.
  Evidence: `AiCredentialStore.cs` reads and writes targets using the `AiteBar/AI/` prefix.

## Decision Log

- Decision: Full backups require a user-provided password and use authenticated encryption.
  Rationale: API keys and clipboard content may be sensitive; a ZIP archive alone offers no confidentiality or tamper detection.
  Date/Author: 2026-08-08 / Codex
- Decision: Clipboard history is opt-in per backup and remains non-persistent during ordinary application use.
  Rationale: A one-time user-authorized backup does not change the product's runtime privacy model.
  Date/Author: 2026-08-08 / Codex

## Outcomes & Retrospective

Work in progress.

## Context and Orientation

`AppSettingsService` atomically writes the main `settings.json` and rotates local backups. `WindowsAiCredentialStore` reads and writes API keys separately in Windows Credential Manager. `ClipboardHistoryService` owns in-memory history and optional persistence. `AppSettingsWindow` hosts the Program Settings sections.

## Plan of Work

Create a `BackupService` that writes a versioned `.aitebarbackup` ZIP. The ordinary entries contain JSON settings and packaged icon files. For a full backup, serialize secrets and the requested clipboard snapshot into one AES-GCM encrypted entry. Derive the AES key from the password with PBKDF2 and a random salt; the archive stores only salt, nonce, ciphertext, and authentication tag. On restore, validate entry paths, extract to a temporary directory, create a safety backup, atomically replace settings and icons, restore Credential Manager entries only after password decryption succeeds, and refresh AiteBar.

Add a Settings section with create, restore, include-secrets, include-current-clipboard, and open-backup-folder controls. Secrets and clipboard controls require a password and visible privacy warning. Keep credential strings out of logs and error dialogs.

## Concrete Steps

Run from `D:\01_Codebdbd\01_projects\aitebar`:

    dotnet build .\AiteBar.sln -c Release
    dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release

## Validation and Acceptance

A standard backup restores panels and icons. A full backup cannot expose keys or clipboard data without its password, rejects an incorrect password, and restores keys into Credential Manager only after authentication succeeds. The application remains usable if restore fails midway because settings are backed up before replacement.

## Idempotence and Recovery

Archive creation uses a new timestamped path. Restore validates every archive entry before writing user data. If restore fails, the pre-restore `settings.json.backup.0` remains available.

## Interfaces and Dependencies

Use built-in `System.IO.Compression` for ZIP, `System.Security.Cryptography.AesGcm` for authenticated encryption, and `Rfc2898DeriveBytes.Pbkdf2` with SHA-256 for password key derivation. Do not add a cryptography package.

Plan created 2026-08-08 for user-requested ordinary and encrypted full backup/restore in Program Settings.
