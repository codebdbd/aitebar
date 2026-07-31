# Privacy Policy

AiteBar is a desktop utility for Windows that operates entirely locally. This document describes what data AiteBar collects, stores, and transmits.

## Data Storage

All user data is stored locally on your computer:

| Data | Location | Purpose |
|------|----------|---------|
| Settings | `%AppData%\Codebdbd\Aite Bar\settings.json` | Application configuration, buttons, and preferences |
| Quick Note | `%AppData%\Codebdbd\Aite Bar\QuickNote.md` | User's quick note content |
| Clipboard History | `%AppData%\Codebdbd\Aite Bar\clipboard_history.json` | Clipboard history (if enabled) |
| Icons | `%AppData%\Codebdbd\Aite Bar\Icons\` | User-imported custom icons |
| Logs | `%AppData%\Codebdbd\Aite Bar\error.log` | Error logs (rotated at 1MB) |

No data is stored in the cloud or synced to external servers.

## Data Collection

### Default Behavior (No Telemetry)

By default, AiteBar does **not** collect or transmit any data. The application works entirely offline.

### Error Reporting (Optional, Disabled by Default)

AiteBar integrates Sentry SDK for error monitoring. This feature is **disabled by default** and only activates if:

1. You set the environment variable `AITEBAR_SENTRY_DSN` or `SENTRY_DSN` before launching the application, OR
2. You manually configure Sentry in `settings.json`

When enabled, Sentry collects:
- Exception stack traces (for debugging)
- Application version
- Operating system information

**Sentry does NOT collect:**
- Personal files or documents
- Clipboard contents
- Quick note content
- URLs or browsing history
- User-identifiable information (PII)

### Network Connections

AiteBar makes network connections only in these scenarios:

1. **Opening URLs** — When you click a web button, AiteBar opens the URL in your default or selected browser. This is a standard browser launch, not a direct network connection from AiteBar.

2. **Checking for updates** — When you manually trigger "Check for updates" from the tray menu or About window, AiteBar queries GitHub Releases API to check for newer versions. No personal data is sent.

3. **Sentry error reporting** — Only if explicitly enabled (see above).

## Clipboard Manager

The Clipboard Manager stores a history of text and images copied to your clipboard:

- History is stored locally in `clipboard_history.json`
- History is persisted between sessions by default (can be disabled in settings to avoid storing clipboard data on disk)
- When persistence is enabled, history survives application restarts
- History is stored as plain text and is not encrypted
- You can clear history at any time from the Clipboard Manager window
- No clipboard data is transmitted to external servers
- None of the local data files are protected with at-rest encryption. Both `clipboard_history.json` and QuickNote.md rely on the Windows profile access control for the `%AppData%\Codebdbd\Aite Bar` folder. If the machine or profile is shared with other local administrators, store sensitive notes or disable clipboard persistence and move the profile under BitLocker/VeraCrypt protection.

## Quick Note

Quick Note stores your notes as a local Markdown file:

- Notes are saved to `QuickNote.md`
- Notes are stored as plain text without encryption
- Notes are not synced to any cloud service
- You can open the note file in any text editor
- Notes rely on Windows profile ACLs for protection; see the at-rest encryption note under Clipboard Manager above.

## Logs

AiteBar logs errors to a local file:

- Log file: `%AppData%\Codebdbd\Aite Bar\error.log`
- Logs are rotated when the file reaches 1MB
- Only error messages and stack traces are logged
- No personal data is included in logs
- Logs are not transmitted to external servers

## Settings Backups

AiteBar automatically creates backups of your settings:

- Up to 5 backup versions are kept
- Backups are stored locally alongside the main settings file
- Backups are not transmitted anywhere

## Third-Party Services

### Sentry (Optional)

- Used for: Error monitoring and crash reporting
- Status: Disabled by default
- Data collected: Exception stack traces, app version, OS info
- Privacy: No PII collected by default
- Documentation: See [SENTRY_SETUP.md](SENTRY_SETUP.md)

### GitHub (Update Check)

- Used for: Checking for application updates
- Status: Only when user manually triggers update check
- Data collected: None (only reads release information)
- Privacy: No personal data is sent

### User-Configured AI Services

- Supported services: OpenRouter, Cerebras, Google Gemini, Groq, GitHub Models, and Mistral AI.
- Status: Disabled until the user adds an API connection. A saved connection does not send data by itself.
- Data transmission: Text or files are sent only after an explicit action in an AI-powered tool and only to the service selected by the routing configuration.
- Credentials: API keys are stored in Windows Credential Manager. The settings file stores only a credential reference and non-secret routing metadata.
- Logging: API keys, prompts, model responses, and Authorization headers are excluded from local logs and Sentry events.
- Provider terms: External services process submitted content under their own privacy and retention policies. Users should review those policies before connecting a service.
- Documentation: See [docs/AI_PROVIDERS.md](docs/AI_PROVIDERS.md).

## Children's Privacy

AiteBar is not directed at children under 13. We do not knowingly collect personal information from children.

## Changes to This Policy

If this privacy policy changes, the updated version will be available in the repository and included with future releases.

## Contact

For questions about this privacy policy, please open an issue on the [GitHub repository](https://github.com/codebdbd/aitebar).
