using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Documents;

namespace AiteBar
{
    internal sealed class QuickNoteExternalChangeException : IOException
    {
        public QuickNoteExternalChangeException() : base("Quick Note changed externally or could not be loaded safely.") { }
    }

    internal interface IQuickNoteProcessStartDispatcher
    {
        void Start(ProcessStartInfo startInfo);
    }

    internal sealed class QuickNoteProcessStartDispatcher : IQuickNoteProcessStartDispatcher
    {
        public void Start(ProcessStartInfo startInfo) => Process.Start(startInfo);
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows6.1")]
    public sealed class QuickNoteService
    {
        private readonly QuickNoteFileStore _store;
        private readonly IQuickNoteProcessStartDispatcher _processStartDispatcher;

        public QuickNoteService(string? notePath = null) : this(notePath, new QuickNoteProcessStartDispatcher()) { }

        internal QuickNoteService(string? notePath, IQuickNoteProcessStartDispatcher processStartDispatcher)
        {
            _store = new QuickNoteFileStore(string.IsNullOrWhiteSpace(notePath)
                ? Path.Combine(PathHelper.AppDataFolder, "QuickNote.aite-note") : notePath);
            _processStartDispatcher = processStartDispatcher ?? throw new ArgumentNullException(nameof(processStartDispatcher));
        }

        public string NotePath => _store.NotePath;
        public string? LastConflictCopyPath => _store.LastConflictCopyPath;
        public bool HasLoadFailed => _store.HasLoadFailed;
        private bool IsPackage => string.Equals(Path.GetExtension(NotePath), ".aite-note", StringComparison.OrdinalIgnoreCase);

        public bool HasExternalChanges() => _store.HasExternalChanges();
        public Task<bool> HasExternalChangesAsync() => Task.Run(_store.HasExternalChanges);

        public void Load(FlowDocument document)
        {
            document.VerifyAccess();
            var snapshot = _store.Read(IsPackage);
            if (LoadSnapshot(document, snapshot) && snapshot.IsLegacy)
                SaveAsync(document).GetAwaiter().GetResult();
        }

        public async Task LoadAsync(FlowDocument document)
        {
            document.VerifyAccess();
            var snapshot = await Task.Run(() => _store.Read(IsPackage));
            if (LoadSnapshot(document, snapshot) && snapshot.IsLegacy)
                await SaveAsync(document);
        }

        private bool LoadSnapshot(FlowDocument document, (byte[]? Content, bool IsLegacy) snapshot)
        {
            if (snapshot.Content == null)
            {
                QuickNoteDocumentCodec.LoadEmpty(document);
                return false;
            }
            try
            {
                QuickNoteDocumentCodec.Deserialize(snapshot.Content, document, IsPackage && !snapshot.IsLegacy);
                return true;
            }
            catch (Exception ex)
            {
                // Preserve original bytes. New local edits may only become a conflict copy.
                _store.MarkLoadFailed();
                Logger.Log(ex);
                QuickNoteDocumentCodec.LoadEmpty(document);
                return false;
            }
        }

        public Task SaveAsync(FlowDocument document)
        {
            byte[] snapshot = QuickNoteDocumentCodec.Serialize(document, IsPackage);
            return Task.Run(() => _store.Save(snapshot));
        }

        public Task<string> SaveConflictCopyAsync(FlowDocument document)
        {
            byte[] snapshot = QuickNoteDocumentCodec.Serialize(document, IsPackage);
            return Task.Run(() => _store.SaveConflictCopy(snapshot));
        }

        public void OpenConflictCopy()
        {
            string? path = LastConflictCopyPath;
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                throw new FileNotFoundException("No Quick Note conflict copy is available.", path);
            _processStartDispatcher.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }

        public void RevealConflictCopy()
        {
            string? path = LastConflictCopyPath;
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                throw new FileNotFoundException("No Quick Note conflict copy is available.", path);
            // The native package need not have a shell file association. Explorer can always reveal it.
            _processStartDispatcher.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true });
        }
    }
}
