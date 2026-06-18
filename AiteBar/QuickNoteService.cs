using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
        private DateTime _lastKnownWriteTimeUtc = DateTime.MinValue;

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
            if (!File.Exists(NotePath))
            {
                return _lastKnownWriteTimeUtc != DateTime.MinValue;
            }

            DateTime currentWriteTimeUtc = File.GetLastWriteTimeUtc(NotePath);
            return _lastKnownWriteTimeUtc != DateTime.MinValue && currentWriteTimeUtc != _lastKnownWriteTimeUtc;
        }

        public async Task LoadAsync(FlowDocument document)
        {
            string markdown = await ReadMarkdownAsync();
            LoadMarkdown(document, markdown);
        }

        public async Task<string> ReadMarkdownAsync()
        {
            EnsureNoteDirectory();
            if (!File.Exists(NotePath))
            {
                _lastKnownWriteTimeUtc = DateTime.MinValue;
                return string.Empty;
            }

            string markdown = await File.ReadAllTextAsync(NotePath);
            _lastKnownWriteTimeUtc = File.GetLastWriteTimeUtc(NotePath);
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
            await File.WriteAllTextAsync(NotePath, QuickNoteMarkdown.ToMarkdown(document));
            _lastKnownWriteTimeUtc = File.GetLastWriteTimeUtc(NotePath);
        }

        public async Task<string> SaveConflictCopyAsync(FlowDocument document)
        {
            EnsureNoteDirectory();
            string conflictPath = Path.Combine(
                Path.GetDirectoryName(NotePath) ?? PathHelper.AppDataFolder,
                $"QuickNote.conflict-{DateTime.Now:yyyyMMdd-HHmmss}.md");
            await File.WriteAllTextAsync(conflictPath, QuickNoteMarkdown.ToMarkdown(document));
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
    }
}
