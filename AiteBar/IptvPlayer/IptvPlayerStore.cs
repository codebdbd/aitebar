using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AiteBar.IptvPlayer;

[SupportedOSPlatform("windows6.1")]
public sealed class IptvPlayerStore
{
    private const string ProtectedTextPrefix = "aitebar-dpapi-v1:";
    private static readonly object FileGate = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly string _baseDirectory;
    private readonly IAiCredentialStore _credentials;

    public IptvPlayerStore(string? baseDirectory = null)
        : this(baseDirectory, null)
    {
    }

    internal IptvPlayerStore(string? baseDirectory, IAiCredentialStore? credentials)
    {
        _baseDirectory = !string.IsNullOrWhiteSpace(baseDirectory)
            ? baseDirectory
            : Path.Combine(PathHelper.AppDataFolder, "IptvPlayer");

        _credentials = credentials ?? new WindowsAiCredentialStore();

        EnsureDirectories();
    }

    public string BaseDirectory => _baseDirectory;
    public string SourcesDirectory => Path.Combine(_baseDirectory, "Sources");
    public string CacheDirectory => Path.Combine(_baseDirectory, "Cache");
    public string SecretsDirectory => Path.Combine(_baseDirectory, "Secrets");
    public string FavoritesPath => Path.Combine(_baseDirectory, "favorites.json");
    public string PlayerStatePath => Path.Combine(_baseDirectory, "player-state.json");

    internal void ProtectLegacyFiles()
    {
        lock (FileGate)
        {
            foreach (string path in Directory.EnumerateFiles(CacheDirectory, "*.json*")
                .Concat(Directory.EnumerateFiles(SourcesDirectory, "*.m3u*"))
                .Concat(new[] { FavoritesPath, FavoritesPath + ".bak", FavoritesPath + ".tmp" }).ToList())
            {
                try
                {
                    if (!File.Exists(path)) continue;
                    string content = File.ReadAllText(path, Encoding.UTF8);
                    if (!content.StartsWith(ProtectedTextPrefix, StringComparison.Ordinal))
                    {
                        WriteProtectedFile(path, content);
                    }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or CryptographicException)
                {
                    Logger.Log(ex);
                }
            }
        }
    }

    private void EnsureDirectories()
    {
        try
        {
            Directory.CreateDirectory(_baseDirectory);
            Directory.CreateDirectory(SourcesDirectory);
            Directory.CreateDirectory(CacheDirectory);
            Directory.CreateDirectory(SecretsDirectory);
        }
        catch
        {
            // Игнорируем в средах без доступа к FS или перехватим при операциях записи
        }
    }

    public string GetSourceFilePath(string sourceId) => GetSourcePath(SourcesDirectory, sourceId, ".m3u");
    public string GetCacheFilePath(string sourceId) => GetSourcePath(CacheDirectory, sourceId, ".json");
    private string GetDpapiSecretPath(string sourceId) => GetSourcePath(SecretsDirectory, sourceId, ".dat");

    private static string GetSourcePath(string directory, string sourceId, string extension)
    {
        if (!IptvSettingsNormalizer.IsValidSourceId(sourceId))
        {
            throw new ArgumentException("Invalid IPTV source ID.", nameof(sourceId));
        }

        return Path.Combine(directory, sourceId + extension);
    }

    #region Favorites

    public List<IptvFavorite> LoadFavorites()
    {
        return LoadWithBackupFallback<List<IptvFavorite>>(FavoritesPath, protect: true) ?? [];
    }

    public void SaveFavorites(List<IptvFavorite> favorites)
    {
        AtomicSaveWithBackup(FavoritesPath, favorites ?? [], protect: true);
    }

    #endregion

    #region Player State

    public IptvPlayerState LoadPlayerState()
    {
        return LoadWithBackupFallback<IptvPlayerState>(PlayerStatePath) ?? new IptvPlayerState();
    }

    public void SavePlayerState(IptvPlayerState state)
    {
        AtomicSaveWithBackup(PlayerStatePath, state ?? new IptvPlayerState());
    }

    #endregion

    #region Cache

    public IptvPlaylistCache? LoadCache(string sourceId)
    {
        if (string.IsNullOrWhiteSpace(sourceId))
        {
            return null;
        }

        string path = GetCacheFilePath(sourceId);
        return LoadWithBackupFallback<IptvPlaylistCache>(path, protect: true);
    }

    public void SaveCache(IptvPlaylistCache cache)
    {
        if (cache == null || string.IsNullOrWhiteSpace(cache.PlaylistId))
        {
            return;
        }

        string path = GetCacheFilePath(cache.PlaylistId);
        AtomicSaveWithBackup(path, cache, protect: true);
    }

    public void DeleteCache(string sourceId)
    {
        if (string.IsNullOrWhiteSpace(sourceId))
        {
            return;
        }

        DeleteFiles(GetCacheFilePath(sourceId));
    }

    #endregion

    #region Local Source Files

    public string? ReadSourceFileContent(string sourceId)
    {
        return LoadWithBackupFallback(GetSourceFilePath(sourceId), static content => content, protect: true);
    }

    public void SaveSourceFileContent(string sourceId, string content)
    {
        string path = GetSourceFilePath(sourceId);
        AtomicSaveRaw(path, content ?? string.Empty, protect: true);
    }

    public void DeleteSourceFile(string sourceId)
    {
        DeleteFiles(GetSourceFilePath(sourceId));
    }

    #endregion

    #region Secrets & Credentials

    public string? ReadSecretLocation(string sourceId)
    {
        if (string.IsNullOrWhiteSpace(sourceId)) return null;

        lock (FileGate)
        {
            string dpapiFile = GetDpapiSecretPath(sourceId);
            if (File.Exists(dpapiFile))
            {
                try
                {
                    byte[] decrypted = ProtectedData.Unprotect(File.ReadAllBytes(dpapiFile), null, DataProtectionScope.CurrentUser);
                    return Encoding.UTF8.GetString(decrypted);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or CryptographicException)
                {
                    // Try the legacy Credential Manager entry if the protected file cannot be opened.
                }
            }

            try
            {
                return _credentials.Read($"{WindowsAiCredentialStore.IptvSourceTargetPrefix}{sourceId}");
            }
            catch
            {
                return null;
            }
        }
    }

    public void SaveSecretLocation(string sourceId, string secretLocation)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secretLocation);
        string dpapiFile = GetDpapiSecretPath(sourceId);
        string target = $"{WindowsAiCredentialStore.IptvSourceTargetPrefix}{sourceId}";
        byte[] bytes = Encoding.UTF8.GetBytes(secretLocation);

        EnsureDirectories();
        lock (FileGate)
        {
            if (bytes.Length <= WindowsAiCredentialStore.MaxCredentialBlobBytes)
            {
                try
                {
                    _credentials.Write(target, secretLocation);
                    File.Delete(dpapiFile);
                    return;
                }
                catch
                {
                    // Use DPAPI when Credential Manager is unavailable.
                }
            }

            string tempPath = dpapiFile + ".tmp";
            byte[] encrypted = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
            File.WriteAllBytes(tempPath, encrypted);
            File.Move(tempPath, dpapiFile, overwrite: true);
        }
    }

    public void DeleteSecret(string sourceId)
    {
        if (string.IsNullOrWhiteSpace(sourceId))
        {
            return;
        }

        string target = $"{WindowsAiCredentialStore.IptvSourceTargetPrefix}{sourceId}";
        try
        {
            _credentials.Delete(target);
        }
        catch
        {
            // ignore
        }

        DeleteFiles(GetDpapiSecretPath(sourceId));
    }

    #endregion

    #region Atomic Helpers

    private T? LoadWithBackupFallback<T>(string filePath, bool protect = false) where T : class =>
        LoadWithBackupFallback(filePath, content => JsonSerializer.Deserialize<T>(content, JsonOptions), protect);

    private static T? LoadWithBackupFallback<T>(string filePath, Func<string, T?> deserialize, bool protect) where T : class
    {
        lock (FileGate)
        {
            T? result = null;
            foreach (string candidate in new[] { filePath, filePath + ".bak" })
            {
                if (!File.Exists(candidate)) continue;

                try
                {
                    string stored = File.ReadAllText(candidate, Encoding.UTF8);
                    bool encrypted = stored.StartsWith(ProtectedTextPrefix, StringComparison.Ordinal);
                    string content = encrypted
                        ? Encoding.UTF8.GetString(ProtectedData.Unprotect(
                            Convert.FromBase64String(stored[ProtectedTextPrefix.Length..]), null, DataProtectionScope.CurrentUser))
                        : stored;
                    result ??= deserialize(content);

                    if (protect && !encrypted)
                    {
                        WriteProtectedFile(candidate, content);
                    }
                    if (!protect && result is not null) break;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or FormatException or CryptographicException)
                {
                    // Try the other copy if this file cannot be read.
                }
            }

            return result;
        }
    }

    private void AtomicSaveWithBackup<T>(string filePath, T data, bool protect = false)
    {
        EnsureDirectories();
        string json = JsonSerializer.Serialize(data, JsonOptions);
        AtomicSaveRaw(filePath, json, protect);
    }

    private void AtomicSaveRaw(string filePath, string content, bool protect)
    {
        EnsureDirectories();
        lock (FileGate)
        {
            string stored = protect ? ProtectText(content) : content;
            string bakPath = filePath + ".bak";
            if (File.Exists(filePath))
            {
                string previous = File.ReadAllText(filePath, Encoding.UTF8);
                if (protect && !previous.StartsWith(ProtectedTextPrefix, StringComparison.Ordinal))
                {
                    previous = ProtectText(previous);
                }
                WriteAtomicFile(bakPath, previous);
            }

            WriteAtomicFile(filePath, stored);
        }
    }

    private static void WriteProtectedFile(string path, string content) => WriteAtomicFile(path, ProtectText(content));

    private static string ProtectText(string content) => ProtectedTextPrefix + Convert.ToBase64String(
        ProtectedData.Protect(Encoding.UTF8.GetBytes(content), null, DataProtectionScope.CurrentUser));

    private static void WriteAtomicFile(string path, string content)
    {
        string tempPath = path + ".tmp";
        File.WriteAllText(tempPath, content, Encoding.UTF8);
        File.Move(tempPath, path, true);
    }

    private static void DeleteFiles(string mainPath)
    {
        lock (FileGate)
        {
            string[] candidates = [mainPath, mainPath + ".tmp", mainPath + ".bak", mainPath + ".bak.tmp"];
            foreach (string file in candidates)
            {
                try { File.Delete(file); } catch { }
            }
        }
    }

    #endregion
}
