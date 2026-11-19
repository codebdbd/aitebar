namespace AiteBar;

internal sealed record BackupCreateOptions(bool IncludeSecrets, bool IncludeClipboard, string? Password);

internal sealed record BackupRestoreOptions(string? Password);

internal sealed record BackupSecretPayload(
    Dictionary<string, string> Credentials,
    IReadOnlyList<ClipboardHistoryEntry>? ClipboardEntries,
    Dictionary<string, string>? IptvLocations = null,
    Dictionary<string, string>? IptvSourceContents = null);

internal sealed record BackupReadResult(
    AppSettings Settings,
    BackupSecretPayload? Secrets,
    IReadOnlyDictionary<string, byte[]> Icons,
    IReadOnlyDictionary<string, byte[]>? TelegramFiles = null,
    IReadOnlyDictionary<string, byte[]>? IptvFiles = null);
