using System;
using System.IO;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using System.Windows.Documents;
using System.Windows.Threading;

namespace AiteBar
{
    [SupportedOSPlatform("windows6.1")]
    internal sealed class QuickNoteSaveController : IDisposable
    {
        private static readonly TimeSpan SaveDebounceInterval = TimeSpan.FromMilliseconds(700);

        private readonly IQuickNotePersistence _noteService;
        private readonly DispatcherTimer _saveTimer;
        private TaskCompletionSource<bool>? _activeSave;
        private readonly Func<FlowDocument> _getDocument;
        private readonly Action<QuickNoteStatusKind, string?> _setStatus;
        private readonly Action _updateStatusSaved;
        private readonly Func<bool> _isLoaded;

        private bool _hasPendingChanges;
        private long _changeVersion;
        private bool _disposed;

        public QuickNoteSaveController(
            IQuickNotePersistence noteService,
            Func<FlowDocument> getDocument,
            Action<QuickNoteStatusKind, string?> setStatus,
            Action updateStatusSaved,
            Func<bool> isLoaded)
        {
            _noteService = noteService ?? throw new ArgumentNullException(nameof(noteService));
            _getDocument = getDocument ?? throw new ArgumentNullException(nameof(getDocument));
            _setStatus = setStatus ?? throw new ArgumentNullException(nameof(setStatus));
            _updateStatusSaved = updateStatusSaved ?? throw new ArgumentNullException(nameof(updateStatusSaved));
            _isLoaded = isLoaded ?? throw new ArgumentNullException(nameof(isLoaded));

            _saveTimer = new DispatcherTimer { Interval = SaveDebounceInterval };
            _saveTimer.Tick += SaveTimer_Tick;
        }

        public long ChangeVersion => _changeVersion;
        public bool HasPendingChanges => _hasPendingChanges;

        public void MarkChangedAndSchedule()
        {
            if (!_isLoaded() || _disposed)
            {
                return;
            }

            _changeVersion++;
            _hasPendingChanges = true;
            Schedule();
        }

        public void Stop()
        {
            if (_disposed) return;
            _saveTimer.Stop();
        }

        public void Schedule()
        {
            if (_disposed) return;
            _setStatus(QuickNoteStatusKind.Saving, null);
            _saveTimer.Stop();
            _saveTimer.Start();
        }

        public async Task<bool> SaveNowAsync(bool force = false)
        {
            if (_disposed) return false;

            _saveTimer.Stop();
            if (!_isLoaded())
            {
                return true;
            }

            if (_activeSave != null)
            {
                if (!force)
                {
                    return true;
                }
                try
                {
                    bool saved = await _activeSave.Task.WaitAsync(QuickNoteWindow.ForcedSaveWaitTimeout);
                    if (!saved || _disposed) return false;
                    // Dispatcher input may run between completion and this continuation.
                    return !_hasPendingChanges || await SaveNowAsync(force: true);
                }
                catch (TimeoutException)
                {
                    if (!_disposed) _setStatus(QuickNoteStatusKind.SaveFailed, null);
                    return false;
                }
            }

            if (!_hasPendingChanges) return true;

            // Installed before calling persistence, which may complete synchronously or re-enter.
            var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            _activeSave = completion;
            _setStatus(QuickNoteStatusKind.Saving, null);
            bool succeeded = false;
            try
            {
                string? conflictPath = null;
                while (_hasPendingChanges && !_disposed)
                {
                    bool useConflictCopy = await _noteService.HasExternalChangesAsync();
                    if (_disposed) return false;
                    long savedVersion = _changeVersion;
                    try
                    {
                        if (useConflictCopy)
                            conflictPath = await _noteService.SaveConflictCopyAsync(_getDocument());
                        else
                            await _noteService.SaveAsync(_getDocument());
                    }
                    catch (QuickNoteExternalChangeException)
                    {
                        if (_disposed) return false;
                        savedVersion = _changeVersion;
                        conflictPath = await _noteService.SaveConflictCopyAsync(_getDocument());
                    }

                    _hasPendingChanges = _changeVersion != savedVersion;
                }

                if (_disposed) return false;
                _saveTimer.Stop();
                if (conflictPath != null)
                    _setStatus(QuickNoteStatusKind.ConflictCopySaved, Path.GetFileName(conflictPath));
                else
                    _updateStatusSaved();
                succeeded = true;
                return succeeded;
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
                if (!_disposed) _setStatus(QuickNoteStatusKind.SaveFailed, null);
                return false;
            }
            finally
            {
                _activeSave = null;
                completion.TrySetResult(succeeded);
            }
        }

        private async void SaveTimer_Tick(object? sender, EventArgs e) => await SaveNowAsync();

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _saveTimer.Stop();
            _saveTimer.Tick -= SaveTimer_Tick;
        }
    }
}
