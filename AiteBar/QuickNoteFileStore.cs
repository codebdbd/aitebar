using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace AiteBar;

/// <summary>Owns disk state only. Callers move these blocking operations off the UI thread.</summary>
internal sealed class QuickNoteFileStore
{
    private readonly object _gate = new();
    private byte[]? _baselineHash;
    private bool _baselineEstablished;
    private volatile bool _loadFailed;
    private volatile string? _lastConflictCopyPath;

    internal const long MaxFileSizeBytes = 50 * 1024 * 1024; // 50 MB safety limit for notes

    internal QuickNoteFileStore(string path) => NotePath = Path.GetFullPath(path);
    internal string NotePath { get; }
    // UI status reads must never wait behind disk flushes held under the storage gate.
    internal bool HasLoadFailed => _loadFailed;
    internal string? LastConflictCopyPath => _lastConflictCopyPath;

    internal void ResetConflictSession()
    {
        lock (_gate)
        {
            _baselineEstablished = false;
            _baselineHash = null;
        }
    }

    internal (byte[]? Content, bool IsLegacy) Read(bool allowLegacy)
    {
        lock (_gate)
        {
            try
            {
                byte[]? bytes = ReadIfPresent(NotePath);
                _baselineHash = bytes == null ? null : SHA256.HashData(bytes);
                _baselineEstablished = true;
                _loadFailed = false;
                RefreshConflictCopy();
                if (bytes == null && allowLegacy)
                    return (ReadIfPresent(Path.ChangeExtension(NotePath, ".rtf")), true);
                return (bytes, false);
            }
            catch
            {
                _loadFailed = true;
                throw;
            }
        }
    }

    internal void MarkLoadFailed()
    {
        lock (_gate) _loadFailed = true;
    }

    internal bool HasExternalChanges()
    {
        lock (_gate)
        {
            if (_loadFailed) return true;
            if (!_baselineEstablished) return false;
            try
            {
                byte[]? current = HashIfPresent(NotePath);
                return current == null ? _baselineHash != null
                    : _baselineHash == null || !current.AsSpan().SequenceEqual(_baselineHash);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Unreadable is never evidence that the target is safe to overwrite.
                return true;
            }
        }
    }

    internal void Save(byte[] content)
    {
        lock (_gate)
        {
            WriteAtomically(NotePath, content, overwrite: true);
            // Track the bytes we wrote, not a later reread of the target.
            _baselineHash = SHA256.HashData(content);
            _baselineEstablished = true;
        }
    }

    internal string SaveConflictCopy(byte[] content)
    {
        lock (_gate)
        {
            string path = Path.Combine(DirectoryPath,
                $"{Path.GetFileNameWithoutExtension(NotePath)}.conflict-{DateTime.Now:yyyyMMdd-HHmmss-fff}-{Guid.NewGuid():N}{Path.GetExtension(NotePath)}");
            WriteAtomically(path, content, overwrite: false);
            _lastConflictCopyPath = path;
            CleanupConflictCopies();
            return path;
        }
    }

    internal void UpdateConflictCopy(string path, byte[] content)
    {
        lock (_gate)
        {
            WriteAtomically(path, content, overwrite: true);
            _lastConflictCopyPath = path;
        }
    }

    private string DirectoryPath => Path.GetDirectoryName(NotePath)!;
    private string ConflictPattern => $"{Path.GetFileNameWithoutExtension(NotePath)}.conflict-*{Path.GetExtension(NotePath)}";

    private void WriteAtomically(string path, byte[] content, bool overwrite)
    {
        Directory.CreateDirectory(DirectoryPath);
        string temporaryPath = Path.Combine(DirectoryPath, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        string? backupPath = null;
        try
        {
            using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(content);
                stream.Flush(flushToDisk: true);
            }

            if (overwrite && HasExternalChanges()) throw new QuickNoteExternalChangeException();

            if (overwrite && File.Exists(path))
            {
                backupPath = Path.Combine(DirectoryPath, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.bak");
                File.Replace(temporaryPath, path, backupPath, ignoreMetadataErrors: true);

                // Verify if the displaced file matched our baseline. If an external write happened
                // right between check and replace, preserve the displaced version as a conflict copy.
                if (_baselineEstablished && _baselineHash != null && File.Exists(backupPath))
                {
                    byte[]? displacedHash = HashIfPresent(backupPath);
                    if (displacedHash != null && !displacedHash.AsSpan().SequenceEqual(_baselineHash))
                    {
                        string displacedConflictPath = Path.Combine(DirectoryPath,
                            $"{Path.GetFileNameWithoutExtension(NotePath)}.conflict-displaced-{DateTime.Now:yyyyMMdd-HHmmss-fff}-{Guid.NewGuid():N}{Path.GetExtension(NotePath)}");
                        try
                        {
                            File.Move(backupPath, displacedConflictPath, overwrite: false);
                            _lastConflictCopyPath = displacedConflictPath;
                            backupPath = null;
                            CleanupConflictCopies();
                        }
                        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Logger.Log(ex); }
                    }
                }
            }
            else
            {
                File.Move(temporaryPath, path, overwrite);
            }
        }
        finally
        {
            try { File.Delete(temporaryPath); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Logger.Log(ex); }

            if (backupPath != null)
            {
                try { File.Delete(backupPath); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Logger.Log(ex); }
            }
        }
    }

    private static byte[]? ReadIfPresent(string path)
    {
        try
        {
            // Deny concurrent writes/deletes while reading one coherent snapshot.
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (stream.Length > MaxFileSizeBytes)
            {
                throw new InvalidDataException($"Quick Note file exceeds maximum allowable limit of {MaxFileSizeBytes} bytes.");
            }
            using var buffer = new MemoryStream((int)stream.Length);
            stream.CopyTo(buffer);
            return buffer.ToArray();
        }
        catch (FileNotFoundException) { return null; }
        catch (DirectoryNotFoundException) { return null; }
    }

    private static byte[]? HashIfPresent(string path)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            return SHA256.HashData(stream);
        }
        catch (FileNotFoundException) { return null; }
        catch (DirectoryNotFoundException) { return null; }
    }

    private void RefreshConflictCopy()
    {
        try
        {
            _lastConflictCopyPath = Directory.Exists(DirectoryPath)
                ? Directory.EnumerateFiles(DirectoryPath, ConflictPattern).OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault()
                : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Logger.Log(ex); }
    }

    private void CleanupConflictCopies()
    {
        try
        {
            // Retain five copies per note and never delete the copy just completed.
            foreach (string path in Directory.EnumerateFiles(DirectoryPath, ConflictPattern)
                .Where(path => !string.Equals(path, _lastConflictCopyPath, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(File.GetLastWriteTimeUtc).Skip(4))
            {
                try { File.Delete(path); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Logger.Log(ex); }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Logger.Log(ex); }
    }
}
