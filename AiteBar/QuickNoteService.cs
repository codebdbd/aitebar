using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
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
                ? Path.Combine(PathHelper.AppDataFolder, "QuickNote.md")
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
            string? currentHash = ComputeContentHash(NotePath);
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
            string markdown = await ReadMarkdownAsync();
            LoadMarkdown(document, markdown);
        }

        public void Load(FlowDocument document)
        {
            string markdown = ReadMarkdown();
            LoadMarkdown(document, markdown);
        }

        public string ReadMarkdown()
        {
            EnsureNoteDirectory();
            if (!File.Exists(NotePath))
            {
                RecordBaseline();
                RefreshLastConflictCopy();
                return string.Empty;
            }

            string markdown = File.ReadAllText(NotePath);
            RecordBaseline();
            RefreshLastConflictCopy();
            return markdown;
        }

        public async Task<string> ReadMarkdownAsync()
        {
            EnsureNoteDirectory();
            if (!File.Exists(NotePath))
            {
                RecordBaseline();
                RefreshLastConflictCopy();
                return string.Empty;
            }

            string markdown = await File.ReadAllTextAsync(NotePath);
            RecordBaseline();
            RefreshLastConflictCopy();
            return markdown;
        }

        public void LoadMarkdown(FlowDocument document, string markdown)
        {
            document.Blocks.Clear();
            QuickNoteMarkdown.LoadMarkdown(document, markdown);
        }

        public async Task SaveAsync(FlowDocument document)
        {
            EnsureNoteDirectory();
            string markdown = QuickNoteMarkdown.ToMarkdown(document);
            await WriteAtomicallyAsync(NotePath, markdown);
            RecordBaseline();
        }

        public async Task<string> SaveConflictCopyAsync(FlowDocument document)
        {
            EnsureNoteDirectory();
            string conflictPath = Path.Combine(
                Path.GetDirectoryName(NotePath) ?? PathHelper.AppDataFolder,
                $"QuickNote.conflict-{DateTime.Now:yyyyMMdd-HHmmss-fff}-{Guid.NewGuid():N}.md");
            string markdown = QuickNoteMarkdown.ToMarkdown(document);
            await WriteNewFileAsync(conflictPath, markdown);
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

                List<string> conflictFiles = Directory.GetFiles(directory, "QuickNote.conflict-*.md")
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
                File.WriteAllText(NotePath, string.Empty);
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
                LastConflictCopyPath = Directory.GetFiles(directory, "QuickNote.conflict-*.md")
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

        private static async Task WriteAtomicallyAsync(string path, string content)
        {
            string directory = Path.GetDirectoryName(path) ?? Directory.GetCurrentDirectory();
            string tempPath = Path.Combine(directory, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
            try
            {
                await File.WriteAllTextAsync(tempPath, content);
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

        private static async Task WriteNewFileAsync(string path, string content)
        {
            await using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            await using var writer = new StreamWriter(stream);
            await writer.WriteAsync(content);
        }
    }
}
