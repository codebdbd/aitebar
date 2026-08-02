# Security Policy

## Supported Versions

| Version | Supported          |
| ------- | ------------------ |
| 1.13.x  | :white_check_mark: |
| 1.12.x  | :white_check_mark: |
| 1.11.x  | :white_check_mark: |
| < 1.11  | :x:                |

## Reporting a Vulnerability

If you discover a security vulnerability, report it via
[GitHub Security Advisories](https://github.com/codebdbd/aitebar/security/advisories/new).

We acknowledge receipt within 48 hours and provide a fix timeline.

## Security Recommendations

### Commands and Scripts

- Only run commands and scripts you trust.
- AiteBar shows a confirmation dialog before executing commands and scripts.
- For potentially destructive commands (e.g., `del`, `format`, `shutdown`), AiteBar displays an additional warning.
- Scripts requiring administrator privileges are not made safer by AiteBar — verify scripts manually before execution.

### Imported Panels

- Review imported panels before use — they may contain buttons that launch commands or scripts.
- Only import panels from trusted sources.

### Clipboard Manager

- Clipboard history is stored locally and is not encrypted.
- Disable "Persist history between sessions" in settings if you don't want clipboard data stored on disk.
- Clipboard history is cleared when the application exits (unless persistence is enabled).

### Quick Note

- Quick notes are stored as plain text files without encryption.
- Notes are saved locally and are not synced to any cloud service.

### Network Access

- AiteBar makes network connections only when:
  - Opening URLs in browsers (user-initiated)
  - Checking for updates (enabled by default and also available from the tray menu or About window; it can be disabled in settings)
  - Sending error reports to Sentry (only if explicitly enabled via environment variable or settings)
- Error telemetry and collection of personally identifiable information are disabled by default.

### Settings and Data Storage

- All data is stored locally in `%AppData%\Codebdbd\Aite Bar\`.
- Settings are backed up automatically (up to 5 versions).
- Clipboard history, Quick Note content, settings, and panel data stay local unless the user explicitly exports or shares them.
