using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AiteBar;

[System.Runtime.Versioning.SupportedOSPlatform("windows6.1")]
internal sealed class BackupService
{
    private const string SettingsEntry = "settings.json";
    private const string SecretsEntry = "private.bin";
    private const string IconsPrefix = "icons/";
    private const long MaxArchiveBytes = 100 * 1024 * 1024;
    private const int SaltSize = 16;
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private const int Iterations = 210_000;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly IAiCredentialStore _credentials;

    public BackupService(IAiCredentialStore credentials) => _credentials = credentials;

    public async Task CreateAsync(string destination, AppSettings settings, BackupCreateOptions options, CancellationToken cancellationToken = default)
    {
        if (options.IncludeSecrets && string.IsNullOrEmpty(options.Password))
            throw new InvalidOperationException("A password is required for a full backup.");

        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        await using FileStream output = File.Create(destination);
        using var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: false);
        await WriteEntryAsync(archive, SettingsEntry, JsonSerializer.Serialize(settings, JsonOptions), cancellationToken);

        if (Directory.Exists(PathHelper.IconsFolder))
        {
            foreach (string path in Directory.EnumerateFiles(PathHelper.IconsFolder, "*", SearchOption.AllDirectories))
            {
                string relative = Path.GetRelativePath(PathHelper.IconsFolder, path).Replace('\\', '/');
                archive.CreateEntryFromFile(path, $"{IconsPrefix}{relative}", CompressionLevel.Optimal);
            }
        }

        if (!options.IncludeSecrets) return;
        var secrets = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (AiConnectionSettings connection in settings.Ai.Connections)
        {
            if (!string.IsNullOrWhiteSpace(connection.CredentialTarget) && _credentials.Read(connection.CredentialTarget) is string value)
                secrets[connection.CredentialTarget] = value;
        }
        var payload = new BackupSecretPayload(secrets, options.IncludeClipboard ? ClipboardHistoryService.Instance.Entries : null);
        await WriteEntryAsync(archive, SecretsEntry, Encrypt(JsonSerializer.SerializeToUtf8Bytes(payload, JsonOptions), options.Password!), cancellationToken);
    }

    public async Task<BackupReadResult> ReadAsync(string source, BackupRestoreOptions options, CancellationToken cancellationToken = default)
    {
        if (new FileInfo(source).Length > MaxArchiveBytes) throw new InvalidDataException("Backup is too large.");
        await using FileStream input = File.OpenRead(source);
        using var archive = new ZipArchive(input, ZipArchiveMode.Read, leaveOpen: false);
        ZipArchiveEntry settingsEntry = archive.GetEntry(SettingsEntry) ?? throw new InvalidDataException("Backup does not contain settings.");
        await using Stream settingsStream = settingsEntry.Open();
        AppSettings settings = await JsonSerializer.DeserializeAsync<AppSettings>(settingsStream, JsonOptions, cancellationToken)
            ?? throw new InvalidDataException("Backup settings are invalid.");

        var icons = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        foreach (ZipArchiveEntry entry in archive.Entries.Where(entry => entry.FullName.StartsWith(IconsPrefix, StringComparison.Ordinal)))
        {
            string relativePath = entry.FullName[IconsPrefix.Length..];
            if (string.IsNullOrWhiteSpace(relativePath) || relativePath.Contains("..", StringComparison.Ordinal) || Path.IsPathRooted(relativePath))
                throw new InvalidDataException("Backup contains an invalid icon path.");
            await using Stream stream = entry.Open();
            using var memory = new MemoryStream();
            await stream.CopyToAsync(memory, cancellationToken);
            icons[relativePath] = memory.ToArray();
        }

        BackupSecretPayload? secrets = null;
        ZipArchiveEntry? secretsEntry = archive.GetEntry(SecretsEntry);
        if (secretsEntry is not null)
        {
            if (string.IsNullOrEmpty(options.Password)) throw new InvalidOperationException("A password is required for this backup.");
            await using Stream secretStream = secretsEntry.Open();
            using var memory = new MemoryStream();
            await secretStream.CopyToAsync(memory, cancellationToken);
            byte[] plaintext = Decrypt(memory.ToArray(), options.Password);
            try
            {
                secrets = JsonSerializer.Deserialize<BackupSecretPayload>(plaintext, JsonOptions)
                    ?? throw new InvalidDataException("Backup private data is invalid.");
            }
            finally { CryptographicOperations.ZeroMemory(plaintext); }
        }

        return new BackupReadResult(settings, secrets, icons);
    }

    public async Task RestoreAsync(string source, BackupRestoreOptions options, AppSettingsService settingsService, CancellationToken cancellationToken = default)
    {
        BackupReadResult backup = await ReadAsync(source, options, cancellationToken);
        foreach (var (relativePath, content) in backup.Icons)
        {
            string destination = Path.GetFullPath(Path.Combine(PathHelper.IconsFolder, relativePath));
            string root = Path.GetFullPath(PathHelper.IconsFolder + Path.DirectorySeparatorChar);
            if (!destination.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Backup contains an invalid icon path.");
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            await File.WriteAllBytesAsync(destination, content, cancellationToken);
        }

        if (backup.Secrets is not null)
        {
            foreach (var (target, secret) in backup.Secrets.Credentials)
            {
                _credentials.Write(target, secret);
            }
            if (backup.Secrets.ClipboardEntries is not null)
            {
                ClipboardHistoryService.Instance.ReplaceEntries(backup.Secrets.ClipboardEntries);
            }
        }

        settingsService.Settings = backup.Settings;
        settingsService.NormalizeAppState();
        await settingsService.SaveAsync();
    }

    private static async Task WriteEntryAsync(ZipArchive archive, string name, string content, CancellationToken token)
    {
        ZipArchiveEntry entry = archive.CreateEntry(name, CompressionLevel.Optimal);
        await using StreamWriter writer = new(entry.Open(), Encoding.UTF8);
        await writer.WriteAsync(content.AsMemory(), token);
    }

    private static async Task WriteEntryAsync(ZipArchive archive, string name, byte[] content, CancellationToken token)
    {
        ZipArchiveEntry entry = archive.CreateEntry(name, CompressionLevel.Optimal);
        await using Stream stream = entry.Open();
        await stream.WriteAsync(content, token);
    }

    private static byte[] Encrypt(byte[] plainText, string password)
    {
        byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
        byte[] nonce = RandomNumberGenerator.GetBytes(NonceSize);
        byte[] cipher = new byte[plainText.Length];
        byte[] tag = new byte[TagSize];
        byte[] key = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, 32);
        using var aes = new AesGcm(key, TagSize);
        aes.Encrypt(nonce, plainText, cipher, tag);
        CryptographicOperations.ZeroMemory(key);
        return [.. salt, .. nonce, .. tag, .. cipher];
    }

    private static byte[] Decrypt(byte[] encrypted, string password)
    {
        if (encrypted.Length <= SaltSize + NonceSize + TagSize) throw new InvalidDataException("Backup private data is invalid.");
        byte[] salt = encrypted[..SaltSize];
        byte[] nonce = encrypted[SaltSize..(SaltSize + NonceSize)];
        byte[] tag = encrypted[(SaltSize + NonceSize)..(SaltSize + NonceSize + TagSize)];
        byte[] cipher = encrypted[(SaltSize + NonceSize + TagSize)..];
        byte[] plain = new byte[cipher.Length];
        byte[] key = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, 32);
        try { using var aes = new AesGcm(key, TagSize); aes.Decrypt(nonce, cipher, tag, plain); return plain; }
        catch (CryptographicException ex) { CryptographicOperations.ZeroMemory(plain); throw new InvalidDataException("Backup password is incorrect or data is damaged.", ex); }
        finally { CryptographicOperations.ZeroMemory(key); }
    }
}
