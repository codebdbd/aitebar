namespace AiteBar;

internal sealed record BackupCreateOptions(bool IncludeSecrets, bool IncludeClipboard, string? Password);

internal sealed record BackupRestoreOptions(string? Password);

internal sealed record BackupSecretPayload(
    Dictionary<string, string> Credentials,
    IReadOnlyList<ClipboardHistoryEntry>? ClipboardEntries);

internal sealed record BackupReadResult(AppSettings Settings, BackupSecretPayload? Secrets, IReadOnlyDictionary<string, byte[]> Icons);
