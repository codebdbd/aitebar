using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace AiteBar;

public sealed class ZenEditorStore
{
    private const int MaximumBackupsPerDocument = 24;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly string _rootDirectory;
    private readonly string _documentsDirectory;
    private readonly string _backupsDirectory;
    private readonly string _indexPath;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private ZenEditorStoreIndex? _index;

    public ZenEditorStore(string? rootDirectory = null)
    {
        _rootDirectory = rootDirectory ?? Path.Combine(PathHelper.AppDataFolder, "ZenEditor");
        _documentsDirectory = Path.Combine(_rootDirectory, "Documents");
        _backupsDirectory = Path.Combine(_rootDirectory, "Backups");
        _indexPath = Path.Combine(_rootDirectory, "index.json");
    }

    public string RootDirectory => _rootDirectory;

    public async Task<ZenEditorLoadResult> InitializeAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            EnsureDirectories();
            _index = await ReadIndexCoreAsync(cancellationToken).ConfigureAwait(false) ?? new ZenEditorStoreIndex();
            ZenEditorDocument? document = null;
            bool wasRecovered = false;

            if (_index.ActiveDocumentId is Guid activeId)
            {
                (document, wasRecovered) = await LoadWithRecoveryCoreAsync(activeId, cancellationToken).ConfigureAwait(false);
            }

            if (document is null || document.IsDeleted)
            {
                foreach (Guid id in EnumerateDocumentIds())
                {
                    (ZenEditorDocument? candidate, bool recovered) =
                        await LoadWithRecoveryCoreAsync(id, cancellationToken).ConfigureAwait(false);
                    if (candidate is null || candidate.IsDeleted)
                    {
                        continue;
                    }

                    if (document is null || candidate.ModifiedUtc > document.ModifiedUtc)
                    {
                        document = candidate;
                        wasRecovered = recovered;
                    }
                }
            }

            if (document is null)
            {
                document = CreateDocument();
                await SaveDocumentCoreAsync(document, createSnapshot: false, cancellationToken).ConfigureAwait(false);
            }

            _index.ActiveDocumentId = document.Id;
            _index.ThemeId = ZenEditorThemeCatalog.Get(_index.ThemeId).Id;
            UpdateDocumentSummary(document);
            await EnsureDocumentSummariesCoreAsync(cancellationToken).ConfigureAwait(false);
            await WriteJsonAtomicAsync(_indexPath, _index, cancellationToken).ConfigureAwait(false);
            return new ZenEditorLoadResult(document.Clone(), CloneIndex(_index), wasRecovered);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyList<ZenEditorDocumentSummary>> ListAsync(
        string untitled,
        CancellationToken cancellationToken = default)
        => await ListCoreAsync(untitled, deleted: false, cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyList<ZenEditorDocumentSummary>> ListDeletedAsync(
        string untitled,
        CancellationToken cancellationToken = default)
        => await ListCoreAsync(untitled, deleted: true, cancellationToken).ConfigureAwait(false);

    private async Task<IReadOnlyList<ZenEditorDocumentSummary>> ListCoreAsync(
        string untitled,
        bool deleted,
        CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            EnsureDirectories();
            _index ??= await ReadIndexCoreAsync(cancellationToken).ConfigureAwait(false) ?? new ZenEditorStoreIndex();
            await EnsureDocumentSummariesCoreAsync(cancellationToken).ConfigureAwait(false);
            return _index.Documents
                .Where(metadata => metadata.IsDeleted == deleted)
                .Select(metadata => new ZenEditorDocumentSummary(
                    metadata.Id,
                    string.IsNullOrEmpty(metadata.Title) ? untitled : metadata.Title,
                    metadata.ModifiedUtc,
                    metadata.Id == _index.ActiveDocumentId))
                .OrderByDescending(summary => summary.ModifiedUtc)
                .ToArray();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<ZenEditorDocument> LoadAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            EnsureDirectories();
            (ZenEditorDocument? document, _) =
                await LoadWithRecoveryCoreAsync(id, cancellationToken).ConfigureAwait(false);
            if (document is null || document.IsDeleted)
            {
                throw new FileNotFoundException("Zen Editor document was not found.", GetDocumentPath(id));
            }

            return document.Clone();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SaveAsync(
        ZenEditorDocument document,
        bool createSnapshot,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            EnsureDirectories();
            _index ??= await ReadIndexCoreAsync(cancellationToken).ConfigureAwait(false) ?? new ZenEditorStoreIndex();
            await SaveDocumentCoreAsync(document, createSnapshot, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SaveSnapshotAsync(
        ZenEditorDocument document,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            EnsureDirectories();
            await CreateSnapshotCoreAsync(document, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<ZenEditorDocument> CreateAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            EnsureDirectories();
            _index ??= await ReadIndexCoreAsync(cancellationToken).ConfigureAwait(false) ?? new ZenEditorStoreIndex();
            var document = CreateDocument();
            await SaveDocumentCoreAsync(document, createSnapshot: false, cancellationToken).ConfigureAwait(false);
            _index.ActiveDocumentId = document.Id;
            await WriteJsonAtomicAsync(_indexPath, _index, cancellationToken).ConfigureAwait(false);
            return document.Clone();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            EnsureDirectories();
            (ZenEditorDocument? document, _) =
                await LoadWithRecoveryCoreAsync(id, cancellationToken).ConfigureAwait(false);
            if (document is null || document.IsDeleted)
            {
                return;
            }

            await CreateSnapshotCoreAsync(document, cancellationToken).ConfigureAwait(false);
            document.IsDeleted = true;
            document.ModifiedUtc = DateTime.UtcNow;
            await SaveDocumentCoreAsync(document, createSnapshot: false, cancellationToken).ConfigureAwait(false);
            _index ??= await ReadIndexCoreAsync(cancellationToken).ConfigureAwait(false) ?? new ZenEditorStoreIndex();
            await WriteJsonAtomicAsync(_indexPath, _index, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<ZenEditorDocument> RestoreAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            EnsureDirectories();
            (ZenEditorDocument? document, _) =
                await LoadWithRecoveryCoreAsync(id, cancellationToken).ConfigureAwait(false);
            if (document is null)
            {
                throw new FileNotFoundException(
                    "Deleted Zen Editor document was not found.",
                    GetDocumentPath(id));
            }

            if (document.IsDeleted)
            {
                document.IsDeleted = false;
                document.ModifiedUtc = DateTime.UtcNow;
                await SaveDocumentCoreAsync(
                    document,
                    createSnapshot: true,
                    cancellationToken).ConfigureAwait(false);
            }

            _index ??= await ReadIndexCoreAsync(cancellationToken).ConfigureAwait(false)
                ?? new ZenEditorStoreIndex();
            _index.ActiveDocumentId = document.Id;
            await WriteJsonAtomicAsync(_indexPath, _index, cancellationToken).ConfigureAwait(false);
            return document.Clone();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SaveIndexAsync(ZenEditorStoreIndex index, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(index);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            EnsureDirectories();
            index.ThemeId = ZenEditorThemeCatalog.Get(index.ThemeId).Id;
            List<ZenEditorDocumentMetadata> documents = _index?.Documents
                .Select(CloneMetadata)
                .ToList()
                ?? index.Documents.Select(CloneMetadata).ToList();
            _index = CloneIndex(index);
            _index.Documents = documents;
            await WriteJsonAtomicAsync(_indexPath, _index, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task SaveDocumentCoreAsync(
        ZenEditorDocument document,
        bool createSnapshot,
        CancellationToken cancellationToken)
    {
        if (createSnapshot && File.Exists(GetDocumentPath(document.Id)))
        {
            (ZenEditorDocument? existing, _) =
                await LoadWithRecoveryCoreAsync(document.Id, cancellationToken).ConfigureAwait(false);
            if (existing is not null)
            {
                await CreateSnapshotCoreAsync(existing, cancellationToken).ConfigureAwait(false);
            }
        }

        ZenEditorDocument copy = document.Clone();
        copy.Text ??= string.Empty;
        copy.Styles ??= [];
        copy.HasEverContainedText |= copy.Text.Length > 0;
        (copy.CaretIndex, copy.SelectionStart, copy.SelectionLength) =
            ZenEditorTextHelper.ClampSelection(
                copy.Text.Length,
                copy.CaretIndex,
                copy.SelectionStart,
                copy.SelectionLength);
        copy.ScrollOffset = Math.Max(0, copy.ScrollOffset);
        copy.Styles = NormalizeStyles(copy.Styles, copy.Text.Length);
        copy.Checksum = await Task.Run(
            () => ComputeChecksum(copy),
            cancellationToken).ConfigureAwait(false);
        await WriteJsonAtomicAsync(GetDocumentPath(copy.Id), copy, cancellationToken).ConfigureAwait(false);
        UpdateDocumentSummary(copy);

        document.Text = copy.Text;
        document.CaretIndex = copy.CaretIndex;
        document.SelectionStart = copy.SelectionStart;
        document.SelectionLength = copy.SelectionLength;
        document.ScrollOffset = copy.ScrollOffset;
        document.HasEverContainedText = copy.HasEverContainedText;
        document.Checksum = copy.Checksum;
        document.Styles = copy.Styles.Select(style => style with { }).ToList();
    }

    private async Task<(ZenEditorDocument? Document, bool WasRecovered)> LoadWithRecoveryCoreAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        ZenEditorDocument? current = await ReadDocumentCoreAsync(GetDocumentPath(id), cancellationToken).ConfigureAwait(false);
        if (current is not null && IsValid(current))
        {
            return (current, false);
        }

        string backupDirectory = GetBackupDirectory(id);
        if (!Directory.Exists(backupDirectory))
        {
            return (null, false);
        }

        foreach (string backupPath in Directory.EnumerateFiles(backupDirectory, "*.json")
                     .OrderByDescending(File.GetLastWriteTimeUtc))
        {
            ZenEditorDocument? backup = await ReadDocumentCoreAsync(backupPath, cancellationToken).ConfigureAwait(false);
            if (backup is null || !IsValid(backup))
            {
                continue;
            }

            PreserveCorruptDocument(GetDocumentPath(id));
            await WriteJsonAtomicAsync(GetDocumentPath(id), backup, cancellationToken).ConfigureAwait(false);
            return (backup, true);
        }

        return (null, false);
    }

    private async Task<ZenEditorDocument?> ReadDocumentCoreAsync(
        string path,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!File.Exists(path))
            {
                return null;
            }

            await using FileStream stream = new(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                64 * 1024,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            return await JsonSerializer.DeserializeAsync<ZenEditorDocument>(
                stream,
                JsonOptions,
                cancellationToken).ConfigureAwait(false);
        }
        catch (JsonException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    private async Task<ZenEditorStoreIndex?> ReadIndexCoreAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (!File.Exists(_indexPath))
            {
                return null;
            }

            await using FileStream stream = new(
                _indexPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                16 * 1024,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            return await JsonSerializer.DeserializeAsync<ZenEditorStoreIndex>(
                stream,
                JsonOptions,
                cancellationToken).ConfigureAwait(false);
        }
        catch (JsonException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    private async Task CreateSnapshotCoreAsync(
        ZenEditorDocument document,
        CancellationToken cancellationToken)
    {
        string directory = GetBackupDirectory(document.Id);
        Directory.CreateDirectory(directory);
        string path = Path.Combine(
            directory,
            $"{DateTime.UtcNow:yyyyMMdd-HHmmss-fffffff}-{Guid.NewGuid():N}.json");
        ZenEditorDocument snapshot = document.Clone();
        snapshot.Checksum = await Task.Run(
            () => ComputeChecksum(snapshot),
            cancellationToken).ConfigureAwait(false);
        await WriteJsonAtomicAsync(path, snapshot, cancellationToken).ConfigureAwait(false);

        foreach (string obsolete in Directory.EnumerateFiles(directory, "*.json")
                     .OrderByDescending(File.GetLastWriteTimeUtc)
                     .Skip(MaximumBackupsPerDocument))
        {
            try
            {
                File.Delete(obsolete);
            }
            catch (IOException)
            {
                // Backup retention must not prevent the current document from being saved.
            }
            catch (UnauthorizedAccessException)
            {
                // A locked or protected old backup can be cleaned up on a later save.
            }
        }
    }

    private static void PreserveCorruptDocument(string documentPath)
    {
        if (!File.Exists(documentPath))
        {
            return;
        }

        string corruptPath = documentPath + ".corrupt-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff");
        try
        {
            File.Copy(documentPath, corruptPath, overwrite: false);
        }
        catch (IOException)
        {
            // Recovery remains more valuable than retaining a diagnostic copy.
        }
        catch (UnauthorizedAccessException)
        {
            // Recovery remains more valuable than retaining a diagnostic copy.
        }
    }

    private static async Task WriteJsonAtomicAsync<T>(
        string destinationPath,
        T value,
        CancellationToken cancellationToken)
    {
        string? directory = Path.GetDirectoryName(destinationPath);
        if (string.IsNullOrEmpty(directory))
        {
            throw new InvalidOperationException("Atomic writes require a destination directory.");
        }

        Directory.CreateDirectory(directory);
        string temporaryPath = Path.Combine(directory, $".{Path.GetFileName(destinationPath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            byte[] payload = await Task.Run(
                () => JsonSerializer.SerializeToUtf8Bytes(value, JsonOptions),
                cancellationToken).ConfigureAwait(false);
            await using (var stream = new FileStream(
                             temporaryPath,
                             FileMode.CreateNew,
                             FileAccess.Write,
                             FileShare.None,
                             64 * 1024,
                             FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await stream.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                stream.Flush(flushToDisk: true);
            }

            File.Move(temporaryPath, destinationPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private IEnumerable<Guid> EnumerateDocumentIds()
    {
        if (!Directory.Exists(_documentsDirectory))
        {
            return [];
        }

        return Directory.EnumerateFiles(_documentsDirectory, "*.json")
            .Select(Path.GetFileNameWithoutExtension)
            .Select(name => Guid.TryParse(name, out Guid id) ? id : Guid.Empty)
            .Where(id => id != Guid.Empty)
            .ToArray();
    }

    private static ZenEditorDocument CreateDocument()
    {
        DateTime now = DateTime.UtcNow;
        return new ZenEditorDocument
        {
            Id = Guid.NewGuid(),
            CreatedUtc = now,
            ModifiedUtc = now
        };
    }

    private static bool IsValid(ZenEditorDocument document)
    {
        if (document.Id == Guid.Empty || string.IsNullOrEmpty(document.Checksum))
        {
            return false;
        }

        byte[] storedChecksum = Encoding.ASCII.GetBytes(document.Checksum);
        if (CryptographicOperations.FixedTimeEquals(
                storedChecksum,
                Encoding.ASCII.GetBytes(ComputeChecksum(document))))
        {
            return true;
        }

        return (document.Styles is null || document.Styles.Count == 0)
            && CryptographicOperations.FixedTimeEquals(
                storedChecksum,
                Encoding.ASCII.GetBytes(ComputeChecksum(document, includeStyles: false)));
    }

    internal static string ComputeChecksum(ZenEditorDocument document)
        => ComputeChecksum(document, includeStyles: true);

    private static string ComputeChecksum(
        ZenEditorDocument document,
        bool includeStyles)
    {
        var fields = new List<string>
        {
            document.Id.ToString("D"),
            document.CreatedUtc.ToUniversalTime().Ticks.ToString(System.Globalization.CultureInfo.InvariantCulture),
            document.ModifiedUtc.ToUniversalTime().Ticks.ToString(System.Globalization.CultureInfo.InvariantCulture),
            document.CaretIndex.ToString(System.Globalization.CultureInfo.InvariantCulture),
            document.SelectionStart.ToString(System.Globalization.CultureInfo.InvariantCulture),
            document.SelectionLength.ToString(System.Globalization.CultureInfo.InvariantCulture),
            document.ScrollOffset.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
            document.IsDeleted.ToString(),
            document.HasEverContainedText.ToString()
        };
        if (includeStyles)
        {
            fields.Add(SerializeStyles(document.Styles));
        }

        fields.Add(document.Text ?? string.Empty);
        string canonical = string.Join("\n", fields);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    private static List<ZenEditorTextStyle> NormalizeStyles(
        IEnumerable<ZenEditorTextStyle> styles,
        int textLength)
    {
        int length = Math.Max(0, textLength);
        return styles
            .Where(style => style.Length > 0 && (style.Bold || style.Italic || style.Underline))
            .Select(style =>
            {
                int start = Math.Clamp(style.Start, 0, length);
                int rangeLength = Math.Clamp(style.Length, 0, length - start);
                return style with { Start = start, Length = rangeLength };
            })
            .Where(style => style.Length > 0)
            .OrderBy(style => style.Start)
            .ThenBy(style => style.Length)
            .ToList();
    }

    private static string SerializeStyles(IEnumerable<ZenEditorTextStyle>? styles) =>
        string.Join(
            ";",
            (styles ?? [])
                .OrderBy(style => style.Start)
                .ThenBy(style => style.Length)
                .Select(style => FormattableString.Invariant(
                    $"{style.Start}:{style.Length}:{style.Bold}:{style.Italic}:{style.Underline}")));

    private static ZenEditorStoreIndex CloneIndex(ZenEditorStoreIndex index) => new()
    {
        ActiveDocumentId = index.ActiveDocumentId,
        ThemeId = index.ThemeId,
        LastMonitorDeviceName = index.LastMonitorDeviceName,
        LastExportDirectory = index.LastExportDirectory,
        Documents = index.Documents.Select(CloneMetadata).ToList()
    };

    private async Task EnsureDocumentSummariesCoreAsync(CancellationToken cancellationToken)
    {
        _index ??= new ZenEditorStoreIndex();
        Guid[] documentIds = EnumerateDocumentIds().ToArray();
        HashSet<Guid> indexedIds = _index.Documents.Select(document => document.Id).ToHashSet();
        if (documentIds.Length == indexedIds.Count && documentIds.All(indexedIds.Contains))
        {
            return;
        }

        _index.Documents.Clear();
        foreach (Guid id in documentIds)
        {
            (ZenEditorDocument? document, _) =
                await LoadWithRecoveryCoreAsync(id, cancellationToken).ConfigureAwait(false);
            if (document is not null)
            {
                UpdateDocumentSummary(document);
            }
        }

        await WriteJsonAtomicAsync(_indexPath, _index, cancellationToken).ConfigureAwait(false);
    }

    private void UpdateDocumentSummary(ZenEditorDocument document)
    {
        _index ??= new ZenEditorStoreIndex();
        _index.Documents.RemoveAll(metadata => metadata.Id == document.Id);
        _index.Documents.Add(new ZenEditorDocumentMetadata(
            document.Id,
            ZenEditorTextHelper.GetDisplayTitle(document.Text, string.Empty),
            document.ModifiedUtc,
            document.IsDeleted));
    }

    private static ZenEditorDocumentMetadata CloneMetadata(ZenEditorDocumentMetadata metadata) =>
        new(metadata.Id, metadata.Title, metadata.ModifiedUtc, metadata.IsDeleted);

    private string GetDocumentPath(Guid id) => Path.Combine(_documentsDirectory, $"{id:D}.json");
    private string GetBackupDirectory(Guid id) => Path.Combine(_backupsDirectory, id.ToString("D"));

    private void EnsureDirectories()
    {
        Directory.CreateDirectory(_rootDirectory);
        Directory.CreateDirectory(_documentsDirectory);
        Directory.CreateDirectory(_backupsDirectory);
    }
}
