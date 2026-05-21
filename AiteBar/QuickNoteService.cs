using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Documents;

namespace AiteBar
{
    public sealed class QuickNoteService
    {
        private DateTime _lastKnownWriteTimeUtc = DateTime.MinValue;

        public string NotePath => Path.Combine(PathHelper.AppDataFolder, "QuickNote.md");

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
            PathHelper.EnsureDirectories();
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
            PathHelper.EnsureDirectories();
            await File.WriteAllTextAsync(NotePath, QuickNoteMarkdown.ToMarkdown(document));
            _lastKnownWriteTimeUtc = File.GetLastWriteTimeUtc(NotePath);
        }

        public async Task<string> SaveConflictCopyAsync(FlowDocument document)
        {
            PathHelper.EnsureDirectories();
            string conflictPath = Path.Combine(
                PathHelper.AppDataFolder,
                $"QuickNote.conflict-{DateTime.Now:yyyyMMdd-HHmmss}.md");
            await File.WriteAllTextAsync(conflictPath, QuickNoteMarkdown.ToMarkdown(document));
            return conflictPath;
        }

        public void OpenInEditor()
        {
            PathHelper.EnsureDirectories();
            if (!File.Exists(NotePath))
            {
                File.WriteAllText(NotePath, string.Empty);
            }

            Process.Start(new ProcessStartInfo(NotePath) { UseShellExecute = true });
        }
    }
}
