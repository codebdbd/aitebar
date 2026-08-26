using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;

namespace AiteBar
{
    internal sealed class QuickNoteExternalChangeException : IOException
    {
        public QuickNoteExternalChangeException()
            : base("Quick Note changed externally while saving.")
        {
        }
    }

    internal interface IQuickNoteProcessStartDispatcher
    {
        void Start(ProcessStartInfo startInfo);
    }

    internal sealed class QuickNoteProcessStartDispatcher : IQuickNoteProcessStartDispatcher
    {
        public void Start(ProcessStartInfo startInfo)
        {
            Process.Start(startInfo);
        }
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows6.1")]
    public sealed class QuickNoteService
    {
        private readonly string _notePath;
        private readonly IQuickNoteProcessStartDispatcher _processStartDispatcher;
        private bool _baselineEstablished;
        private bool _lastKnownExists;
        private DateTime _lastKnownWriteTimeUtc = DateTime.MinValue;
        private long _lastKnownLength;
        private string? _lastKnownContentHash;

        private readonly record struct FileSnapshot(bool Exists, DateTime LastWriteTimeUtc, long Length, string? ContentHash);

        public QuickNoteService(string? notePath = null)
            : this(notePath, new QuickNoteProcessStartDispatcher())
        {
        }

        internal QuickNoteService(string? notePath, IQuickNoteProcessStartDispatcher processStartDispatcher)
        {
            _notePath = string.IsNullOrWhiteSpace(notePath)
                ? Path.Combine(PathHelper.AppDataFolder, "QuickNote.aite-note")
                : notePath;
            _processStartDispatcher = processStartDispatcher;
        }

        public string NotePath => _notePath;
        public string? LastConflictCopyPath { get; private set; }

        public bool HasExternalChanges()
        {
            if (!_baselineEstablished)
            {
                return false;
            }

            var file = new FileInfo(NotePath);
            if (file.Exists != _lastKnownExists)
            {
                return true;
            }

            if (!file.Exists)
            {
                return false;
            }

            if (file.LastWriteTimeUtc == _lastKnownWriteTimeUtc && file.Length == _lastKnownLength)
            {
                // Probe to check if the file is locked or inaccessible by another process.
                // This is required to detect exclusive locks (e.g. during active external editing/saving)
                // and is expected by unit tests to return true.
                try
                {
                    using (new FileStream(NotePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    {
                    }
                }
                catch (IOException ex)
                {
                    Logger.Log(ex);
                    return true;
                }

                return false;
            }

            string? currentHash;
            try
            {
                currentHash = ComputeContentHash(NotePath);
            }
            catch (IOException ex)
            {
                Logger.Log(ex);
                return true;
            }
            catch (UnauthorizedAccessException ex)
            {
                Logger.Log(ex);
                return true;
            }

            if (!string.Equals(currentHash, _lastKnownContentHash, StringComparison.Ordinal))
            {
                return true;
            }

            _lastKnownWriteTimeUtc = file.LastWriteTimeUtc;
            _lastKnownLength = file.Length;

            return false;
        }

        public Task<bool> HasExternalChangesAsync() => Task.Run(HasExternalChanges);

        public async Task LoadAsync(FlowDocument document)
        {
            EnsureNoteDirectory();
            FileSnapshot baseline = await Task.Run(() => CaptureSnapshot(NotePath));
            if (baseline.Exists)
            {
                await using var stream = new FileStream(NotePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                LoadDocument(stream, document);
            }
            else if (TryLoadLegacyDocument(document))
            {
                await SaveAsync(document);
                RefreshLastConflictCopy();
                return;
            }
            else
            {
                LoadEmptyDocument(document);
            }

            RecordBaseline(baseline);
            RefreshLastConflictCopy();
        }

        public void Load(FlowDocument document)
        {
            EnsureNoteDirectory();
            FileSnapshot baseline = CaptureSnapshot(NotePath);
            if (baseline.Exists)
            {
                using var stream = new FileStream(NotePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                LoadDocument(stream, document);
            }
            else if (TryLoadLegacyDocument(document))
            {
                SaveAsync(document).GetAwaiter().GetResult();
                RefreshLastConflictCopy();
                return;
            }
            else
            {
                LoadEmptyDocument(document);
            }

            RecordBaseline(baseline);
            RefreshLastConflictCopy();
        }

        public async Task SaveAsync(FlowDocument document)
        {
            EnsureNoteDirectory();
            FileSnapshot baseline = await WriteAtomicallyAsync(NotePath, document);
            RecordBaseline(baseline);
        }

        public async Task<string> SaveConflictCopyAsync(FlowDocument document)
        {
            EnsureNoteDirectory();
            string conflictPath = Path.Combine(
                Path.GetDirectoryName(NotePath) ?? PathHelper.AppDataFolder,
                $"QuickNote.conflict-{DateTime.Now:yyyyMMdd-HHmmss-fff}-{Guid.NewGuid():N}{Path.GetExtension(NotePath)}");
            await WriteNewFileAsync(conflictPath, document);
            LastConflictCopyPath = conflictPath;
            CleanupOldConflictCopies();
            return conflictPath;
        }

        private void CleanupOldConflictCopies()
        {
            try
            {
                string directory = Path.GetDirectoryName(NotePath) ?? PathHelper.AppDataFolder;
                Directory.CreateDirectory(directory);

                List<string> conflictFiles = Directory.GetFiles(directory, $"QuickNote.conflict-*{Path.GetExtension(NotePath)}")
                    .OrderByDescending(File.GetLastWriteTimeUtc)
                    .ToList();

                // Keep last 5 conflict copies
                const int maxConflictCopies = 5;
                if (conflictFiles.Count > maxConflictCopies)
                {
                    foreach (string file in conflictFiles.Skip(maxConflictCopies))
                    {
                        try
                        {
                            File.Delete(file);
                        }
                        catch (Exception ex)
                        {
                            Logger.Log(ex);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
        }

        public void OpenInEditor()
        {
            EnsureNoteDirectory();
            if (!File.Exists(NotePath))
            {
                using (var stream = new FileStream(NotePath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
                {
                    var document = new FlowDocument();
                    SaveDocument(stream, document);
                }

                RecordBaseline();
            }

            if (!IsPackagePath)
            {
                _processStartDispatcher.Start(new ProcessStartInfo(NotePath) { UseShellExecute = true });
                return;
            }

            string exportPath = Path.ChangeExtension(NotePath, ".rtf");
            var documentToExport = new FlowDocument();
            using (var input = new FileStream(NotePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            {
                LoadPackage(input, documentToExport);
            }

            using (var output = new FileStream(exportPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
            {
                SaveRtf(output, documentToExport);
            }

            _processStartDispatcher.Start(new ProcessStartInfo(exportPath) { UseShellExecute = true });
        }

        public void OpenConflictCopy()
        {
            if (string.IsNullOrWhiteSpace(LastConflictCopyPath) || !File.Exists(LastConflictCopyPath))
            {
                throw new FileNotFoundException("No Quick Note conflict copy is available.", LastConflictCopyPath);
            }

            _processStartDispatcher.Start(new ProcessStartInfo(LastConflictCopyPath) { UseShellExecute = true });
        }

        private void EnsureNoteDirectory()
        {
            string? directory = Path.GetDirectoryName(NotePath);
            if (string.IsNullOrWhiteSpace(directory))
            {
                PathHelper.EnsureDirectories();
                return;
            }

            Directory.CreateDirectory(directory);
        }

        private void RecordBaseline()
        {
            RecordBaseline(CaptureSnapshot(NotePath));
        }

        private void RecordBaseline(FileSnapshot snapshot)
        {
            _baselineEstablished = true;
            _lastKnownExists = snapshot.Exists;
            _lastKnownWriteTimeUtc = snapshot.LastWriteTimeUtc;
            _lastKnownLength = snapshot.Length;
            _lastKnownContentHash = snapshot.ContentHash;
        }

        private static FileSnapshot CaptureSnapshot(string path)
        {
            var file = new FileInfo(path);
            return file.Exists
                ? new FileSnapshot(true, file.LastWriteTimeUtc, file.Length, ComputeContentHash(path))
                : new FileSnapshot(false, DateTime.MinValue, 0, null);
        }

        private void RefreshLastConflictCopy()
        {
            try
            {
                string directory = Path.GetDirectoryName(NotePath) ?? PathHelper.AppDataFolder;
                LastConflictCopyPath = Directory.GetFiles(directory, $"QuickNote.conflict-*{Path.GetExtension(NotePath)}")
                    .OrderByDescending(File.GetLastWriteTimeUtc)
                    .FirstOrDefault();
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
        }

        private static string ComputeContentHash(string path)
        {
            using FileStream stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            return Convert.ToHexString(SHA256.HashData(stream));
        }

        private static void LoadEmptyDocument(FlowDocument document)
        {
            document.Blocks.Clear();
            document.Blocks.Add(new Paragraph(new Run(string.Empty)));
        }

        private async Task<FileSnapshot> WriteAtomicallyAsync(string path, FlowDocument document)
        {
            string directory = Path.GetDirectoryName(path) ?? Directory.GetCurrentDirectory();
            string tempPath = Path.Combine(directory, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
            try
            {
                await using (var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
                {
                    SaveDocument(stream, document);
                }

                FileSnapshot snapshot = await Task.Run(() => CaptureSnapshot(tempPath));

                // Do not truncate the target when another process blocks replacement. Reporting the
                // failure lets the window retain the unsaved document or create a conflict copy.
                if (HasExternalChanges())
                {
                    throw new QuickNoteExternalChangeException();
                }

                File.Move(tempPath, path, overwrite: true);
                return snapshot;
            }
            finally
            {
                if (File.Exists(tempPath))
                {
                    try
                    {
                        File.Delete(tempPath);
                    }
                    catch
                    {
                    }
                }
            }
        }

        private async Task WriteNewFileAsync(string path, FlowDocument document)
        {
            await using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
            SaveDocument(stream, document);
        }

        private static void SaveRtf(Stream stream, FlowDocument document)
        {
            FlowDocument exportDocument = QuickNoteRtfAdapter.CreateExportDocument(document);
            new TextRange(exportDocument.ContentStart, exportDocument.ContentEnd)
                .Save(stream, DataFormats.Rtf, preserveTextElements: true);
        }

        private bool IsPackagePath => string.Equals(Path.GetExtension(NotePath), ".aite-note", StringComparison.OrdinalIgnoreCase);

        private void SaveDocument(Stream stream, FlowDocument document)
        {
            if (IsPackagePath)
            {
                new TextRange(document.ContentStart, document.ContentEnd)
                    .Save(stream, DataFormats.XamlPackage, preserveTextElements: true);
                return;
            }

            SaveRtf(stream, document);
        }

        private void LoadDocument(Stream stream, FlowDocument document)
        {
            if (IsPackagePath)
            {
                LoadPackage(stream, document);
                return;
            }

            LoadRtf(stream, document);
        }

        private static void LoadPackage(Stream stream, FlowDocument document)
        {
            try
            {
                new TextRange(document.ContentStart, document.ContentEnd).Load(stream, DataFormats.XamlPackage);
                QuickNoteRtfAdapter.RestoreCodeBlocksFromFences(document);
                QuickNoteRtfAdapter.NormalizeCodeBlocks(document);
                QuickNoteRtfAdapter.RestoreEmbeddedImages(document);
                QuickNoteRtfAdapter.RestoreTaskItems(document);
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or ArgumentException or FormatException or System.Windows.Markup.XamlParseException)
            {
                Logger.Log(ex);
                LoadEmptyDocument(document);
            }
        }

        private static void LoadRtf(Stream stream, FlowDocument document)
        {
            try
            {
                EnsureRtfStream(stream);
                new TextRange(document.ContentStart, document.ContentEnd).Load(stream, DataFormats.Rtf);
                QuickNoteRtfAdapter.RestoreCodeBlocksFromFences(document);
                QuickNoteRtfAdapter.RestoreEmbeddedImages(document);
                QuickNoteRtfAdapter.RestoreTaskItems(document);
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or ArgumentException or FormatException or System.Windows.Markup.XamlParseException)
            {
                Logger.Log(ex);
                LoadEmptyDocument(document);
            }
        }

        private static void EnsureRtfStream(Stream stream)
        {
            if (!stream.CanSeek)
            {
                return;
            }

            long originalPosition = stream.Position;
            Span<byte> buffer = stackalloc byte[5];
            int bytesRead = stream.Read(buffer);
            stream.Position = originalPosition;

            if (bytesRead < 5 ||
                buffer[0] != (byte)'{' ||
                buffer[1] != (byte)'\\' ||
                buffer[2] != (byte)'r' ||
                buffer[3] != (byte)'t' ||
                buffer[4] != (byte)'f')
            {
                throw new InvalidDataException("Quick Note file is not a valid RTF document.");
            }
        }

        private bool TryLoadLegacyDocument(FlowDocument document)
        {
            if (!IsPackagePath)
            {
                return false;
            }

            string legacyPath = Path.ChangeExtension(NotePath, ".rtf");
            if (!File.Exists(legacyPath))
            {
                return false;
            }

            try
            {
                using var stream = new FileStream(legacyPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                LoadRtf(stream, document);
                return true;
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or ArgumentException or FormatException or System.Windows.Markup.XamlParseException)
            {
                Logger.Log(ex);
                LoadEmptyDocument(document);
                return false;
            }
        }
    }
}
