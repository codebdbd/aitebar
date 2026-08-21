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

        public QuickNoteService(string? notePath = null)
            : this(notePath, new QuickNoteProcessStartDispatcher())
        {
        }

        internal QuickNoteService(string? notePath, IQuickNoteProcessStartDispatcher processStartDispatcher)
        {
            _notePath = string.IsNullOrWhiteSpace(notePath)
                ? Path.Combine(PathHelper.AppDataFolder, "QuickNote.rtf")
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

            // Compute and compare hash first to satisfy test cases that simulate content changes
            // with spoofed/identical file timestamps.
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

            // Content is identical.
            // If the metadata on disk differs from our cached values (due to a delayed OS flush after saving),
            // update our baseline metadata so that future checks stay in sync.
            if (file.LastWriteTimeUtc != _lastKnownWriteTimeUtc || file.Length != _lastKnownLength)
            {
                _lastKnownWriteTimeUtc = file.LastWriteTimeUtc;
                _lastKnownLength = file.Length;
            }

            return false;
        }

        public async Task LoadAsync(FlowDocument document)
        {
            EnsureNoteDirectory();
            if (File.Exists(NotePath))
            {
                await using var stream = new FileStream(NotePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                new TextRange(document.ContentStart, document.ContentEnd).Load(stream, DataFormats.Rtf);
            }
            else
            {
                LoadEmptyDocument(document);
            }

            RecordBaseline();
            RefreshLastConflictCopy();
        }

        public void Load(FlowDocument document)
        {
            EnsureNoteDirectory();
            if (File.Exists(NotePath))
            {
                using var stream = new FileStream(NotePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                new TextRange(document.ContentStart, document.ContentEnd).Load(stream, DataFormats.Rtf);
            }
            else
            {
                LoadEmptyDocument(document);
            }

            RecordBaseline();
            RefreshLastConflictCopy();
        }

        public async Task SaveAsync(FlowDocument document)
        {
            EnsureNoteDirectory();
            await WriteAtomicallyAsync(NotePath, document);
            RecordBaseline();
        }

        public async Task<string> SaveConflictCopyAsync(FlowDocument document)
        {
            EnsureNoteDirectory();
            string conflictPath = Path.Combine(
                Path.GetDirectoryName(NotePath) ?? PathHelper.AppDataFolder,
                $"QuickNote.conflict-{DateTime.Now:yyyyMMdd-HHmmss-fff}-{Guid.NewGuid():N}.rtf");
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

                List<string> conflictFiles = Directory.GetFiles(directory, "QuickNote.conflict-*.rtf")
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
                using (var stream = new FileStream(NotePath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    var document = new FlowDocument();
                    new TextRange(document.ContentStart, document.ContentEnd).Save(stream, DataFormats.Rtf);
                }

                RecordBaseline();
            }

            _processStartDispatcher.Start(new ProcessStartInfo(NotePath) { UseShellExecute = true });
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
            var file = new FileInfo(NotePath);
            _baselineEstablished = true;
            _lastKnownExists = file.Exists;
            _lastKnownWriteTimeUtc = file.Exists ? file.LastWriteTimeUtc : DateTime.MinValue;
            _lastKnownLength = file.Exists ? file.Length : 0;
            _lastKnownContentHash = file.Exists ? ComputeContentHash(NotePath) : null;
        }

        private void RefreshLastConflictCopy()
        {
            try
            {
                string directory = Path.GetDirectoryName(NotePath) ?? PathHelper.AppDataFolder;
                LastConflictCopyPath = Directory.GetFiles(directory, "QuickNote.conflict-*.rtf")
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

        private static async Task WriteAtomicallyAsync(string path, FlowDocument document)
        {
            string directory = Path.GetDirectoryName(path) ?? Directory.GetCurrentDirectory();
            string tempPath = Path.Combine(directory, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
            try
            {
                await using (var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    new TextRange(document.ContentStart, document.ContentEnd).Save(stream, DataFormats.Rtf);
                }

                File.Move(tempPath, path, overwrite: true);
            }
            finally
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }
        }

        private static async Task WriteNewFileAsync(string path, FlowDocument document)
        {
            await using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            new TextRange(document.ContentStart, document.ContentEnd).Save(stream, DataFormats.Rtf);
        }
    }
}
