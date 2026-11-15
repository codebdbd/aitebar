using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AiteBar.IptvPlayer;

[SupportedOSPlatform("windows6.1")]
public sealed class IptvPlayerStore
{
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

    public string GetSourceFilePath(string sourceId) => Path.Combine(SourcesDirectory, $"{sourceId}.m3u");
    public string GetCacheFilePath(string sourceId) => Path.Combine(CacheDirectory, $"{sourceId}.json");
    private string GetDpapiSecretPath(string sourceId) => Path.Combine(SecretsDirectory, $"{sourceId}.dat");

    #region Favorites

    public List<IptvFavorite> LoadFavorites()
    {
        return LoadWithBackupFallback<List<IptvFavorite>>(FavoritesPath) ?? [];
    }

    public void SaveFavorites(List<IptvFavorite> favorites)
    {
        AtomicSaveWithBackup(FavoritesPath, favorites ?? []);
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
        return LoadWithBackupFallback<IptvPlaylistCache>(path);
    }

    public void SaveCache(IptvPlaylistCache cache)
    {
        if (cache == null || string.IsNullOrWhiteSpace(cache.PlaylistId))
        {
            return;
        }

        string path = GetCacheFilePath(cache.PlaylistId);
        AtomicSaveWithBackup(path, cache);
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
        string path = GetSourceFilePath(sourceId);
        if (File.Exists(path))
        {
            return File.ReadAllText(path, Encoding.UTF8);
        }

        return null;
    }

    public void SaveSourceFileContent(string sourceId, string content)
    {
        string path = GetSourceFilePath(sourceId);
        AtomicSaveRaw(path, content ?? string.Empty);
    }

    public void DeleteSourceFile(string sourceId)
    {
        DeleteFiles(GetSourceFilePath(sourceId));
    }

    #endregion

    #region Secrets & Credentials

    public string? ReadSecretLocation(string sourceId)
    {
        if (string.IsNullOrWhiteSpace(sourceId))
        {
            return null;
        }

        string target = $"{WindowsAiCredentialStore.IptvSourceTargetPrefix}{sourceId}";

        // Сначала пробуем Windows Credential Manager
        try
        {
            string? secret = _credentials.Read(target);
            if (!string.IsNullOrWhiteSpace(secret))
            {
                return secret;
            }
        }
        catch
        {
            // fallback к DPAPI
        }

        // Если не найдено в Credential Manager, проверяем DPAPI секрет
        string dpapiFile = GetDpapiSecretPath(sourceId);
        if (File.Exists(dpapiFile))
        {
            try
            {
                byte[] encrypted = File.ReadAllBytes(dpapiFile);
                if (encrypted.Length > 0)
                {
                    byte[] decrypted = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
                    return Encoding.UTF8.GetString(decrypted);
                }
            }
            catch
            {
                // DPAPI failed
            }
        }

        return null;
    }

    public void SaveSecretLocation(string sourceId, string secretLocation)
    {
        if (string.IsNullOrWhiteSpace(sourceId) || string.IsNullOrWhiteSpace(secretLocation))
        {
            return;
        }

        string target = $"{WindowsAiCredentialStore.IptvSourceTargetPrefix}{sourceId}";
        byte[] bytes = Encoding.UTF8.GetBytes(secretLocation);

        if (bytes.Length <= WindowsAiCredentialStore.MaxCredentialBlobBytes)
        {
            try
            {
                _credentials.Write(target, secretLocation);
                // Удаляем DPAPI файл, если оставался
                string dpapi = GetDpapiSecretPath(sourceId);
                if (File.Exists(dpapi))
                {
                    File.Delete(dpapi);
                }
                return;
            }
            catch
            {
                // Если Credential Manager недоступен (например в тесте без UI/сессии), fallback к DPAPI
            }
        }

        // DPAPI сохранение
        try
        {
            byte[] encrypted = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
            File.WriteAllBytes(GetDpapiSecretPath(sourceId), encrypted);
        }
        catch
        {
            // ignore
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

        string dpapi = GetDpapiSecretPath(sourceId);
        if (File.Exists(dpapi))
        {
            try { File.Delete(dpapi); } catch { }
        }
    }

    #endregion

    #region Atomic Helpers

    private T? LoadWithBackupFallback<T>(string filePath) where T : class
    {
        if (File.Exists(filePath))
        {
            try
            {
                string json = File.ReadAllText(filePath, Encoding.UTF8);
                return JsonSerializer.Deserialize<T>(json, JsonOptions);
            }
            catch
            {
                // Файл поврежден — пробуем .bak
            }
        }

        string bakPath = filePath + ".bak";
        if (File.Exists(bakPath))
        {
            try
            {
                string json = File.ReadAllText(bakPath, Encoding.UTF8);
                return JsonSerializer.Deserialize<T>(json, JsonOptions);
            }
            catch
            {
                // .bak тоже поврежден
            }
        }

        return null;
    }

    private void AtomicSaveWithBackup<T>(string filePath, T data)
    {
        EnsureDirectories();
        string json = JsonSerializer.Serialize(data, JsonOptions);
        AtomicSaveRaw(filePath, json);
    }

    private void AtomicSaveRaw(string filePath, string content)
    {
        EnsureDirectories();
        string tempPath = filePath + ".tmp";
        string bakPath = filePath + ".bak";

        File.WriteAllText(tempPath, content, Encoding.UTF8);

        if (File.Exists(filePath))
        {
            try
            {
                File.Copy(filePath, bakPath, true);
            }
            catch
            {
                // ignore
            }
        }

        File.Move(tempPath, filePath, true);
    }

    private static void DeleteFiles(string mainPath)
    {
        string[] candidates = [mainPath, mainPath + ".tmp", mainPath + ".bak"];
        foreach (string file in candidates)
        {
            if (File.Exists(file))
            {
                try { File.Delete(file); } catch { }
            }
        }
    }

    #endregion
}
