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
    internal const long MaxArchiveBytes = 100 * 1024 * 1024;
    internal const long MaxTotalUncompressedBytes = 100 * 1024 * 1024;
    internal const long MaxSingleEntryBytes = 10 * 1024 * 1024;
    internal const int MaxArchiveEntries = 1000;
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
        if (!File.Exists(source))
        {
            throw new FileNotFoundException("Backup file not found.", source);
        }

        if (new FileInfo(source).Length > MaxArchiveBytes)
        {
            throw new InvalidDataException("Backup is too large.");
        }

        long totalUncompressedBytes = 0;
        void TrackUncompressedBytes(long bytes)
        {
            checked
            {
                totalUncompressedBytes += bytes;
            }

            if (totalUncompressedBytes > MaxTotalUncompressedBytes)
            {
                throw new InvalidDataException("Backup uncompressed data exceeds maximum allowed size.");
            }
        }

        try
        {
            await using FileStream input = File.OpenRead(source);
            using var archive = new ZipArchive(input, ZipArchiveMode.Read, leaveOpen: false);

            if (archive.Entries.Count > MaxArchiveEntries)
            {
                throw new InvalidDataException("Backup contains too many entries.");
            }

            ZipArchiveEntry settingsEntry = archive.GetEntry(SettingsEntry)
                ?? throw new InvalidDataException("Backup does not contain settings.");

            byte[] settingsBytes = await ReadBoundedEntryBytesAsync(settingsEntry, MaxSingleEntryBytes, TrackUncompressedBytes, cancellationToken);
            ReadOnlySpan<byte> settingsSpan = settingsBytes;
            if (settingsSpan.StartsWith([(byte)0xEF, (byte)0xBB, (byte)0xBF]))
            {
                settingsSpan = settingsSpan[3..];
            }
            AppSettings settings = JsonSerializer.Deserialize<AppSettings>(settingsSpan, JsonOptions)
                ?? throw new InvalidDataException("Backup settings are invalid.");

            var icons = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
            foreach (ZipArchiveEntry entry in archive.Entries.Where(entry => entry.FullName.StartsWith(IconsPrefix, StringComparison.Ordinal)))
            {
                string relativePath = entry.FullName[IconsPrefix.Length..];
                if (!IsSafeRelativePath(relativePath))
                {
                    throw new InvalidDataException("Backup contains an invalid icon path.");
                }

                byte[] iconBytes = await ReadBoundedEntryBytesAsync(entry, MaxSingleEntryBytes, TrackUncompressedBytes, cancellationToken);
                icons[relativePath] = iconBytes;
            }

            BackupSecretPayload? secrets = null;
            ZipArchiveEntry? secretsEntry = archive.GetEntry(SecretsEntry);
            if (secretsEntry is not null)
            {
                if (string.IsNullOrEmpty(options.Password))
                {
                    throw new InvalidOperationException("A password is required for this backup.");
                }

                byte[] encryptedBytes = await ReadBoundedEntryBytesAsync(secretsEntry, MaxSingleEntryBytes, TrackUncompressedBytes, cancellationToken);
                byte[] plaintext = Decrypt(encryptedBytes, options.Password);
                try
                {
                    TrackUncompressedBytes(plaintext.Length);
                    secrets = JsonSerializer.Deserialize<BackupSecretPayload>(plaintext, JsonOptions)
                        ?? throw new InvalidDataException("Backup private data is invalid.");
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(plaintext);
                }
            }

            return new BackupReadResult(settings, secrets, icons);
        }
        catch (InvalidDataException)
        {
            throw;
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new InvalidDataException("Backup file is corrupt or invalid.", ex);
        }
    }

    public async Task RestoreAsync(string source, BackupRestoreOptions options, AppSettingsService settingsService, CancellationToken cancellationToken = default)
    {
        BackupReadResult backup = await ReadAsync(source, options, cancellationToken);
        string root = Path.GetFullPath(PathHelper.IconsFolder + Path.DirectorySeparatorChar);

        foreach (var (relativePath, content) in backup.Icons)
        {
            if (!IsSafeRelativePath(relativePath))
            {
                throw new InvalidDataException("Backup contains an invalid icon path.");
            }

            string destination = Path.GetFullPath(Path.Combine(PathHelper.IconsFolder, relativePath));
            if (!destination.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("Backup contains an invalid icon path.");
            }

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

    private static async Task<byte[]> ReadBoundedEntryBytesAsync(
        ZipArchiveEntry entry,
        long maxEntryBytes,
        Action<long>? onBytesRead,
        CancellationToken cancellationToken)
    {
        if (entry.Length > maxEntryBytes)
        {
            throw new InvalidDataException($"Backup entry '{entry.FullName}' exceeds maximum allowed size.");
        }

        await using Stream stream = entry.Open();
        using var memory = new MemoryStream();
        byte[] buffer = new byte[8192];
        long totalRead = 0;
        int read;

        while ((read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)) > 0)
        {
            totalRead += read;
            if (totalRead > maxEntryBytes)
            {
                throw new InvalidDataException($"Backup entry '{entry.FullName}' exceeds maximum allowed size.");
            }

            onBytesRead?.Invoke(read);
            await memory.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }

        return memory.ToArray();
    }

    internal static bool IsSafeRelativePath(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath))
        {
            return false;
        }

        string normalized = relativePath.Replace('\\', '/');
        string[] parts = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return false;
        }

        return parts.All(part => !string.Equals(part, "..", StringComparison.Ordinal) && !string.Equals(part, ".", StringComparison.Ordinal));
    }

    private static async Task WriteEntryAsync(ZipArchive archive, string name, string content, CancellationToken token)
    {
        ZipArchiveEntry entry = archive.CreateEntry(name, CompressionLevel.Optimal);
        await using StreamWriter writer = new(entry.Open(), new UTF8Encoding(false));
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
