using System;
using System.IO;
using System.Runtime.Versioning;
using System.Threading;
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
        private readonly SemaphoreSlim _saveSemaphore = new(1, 1);
        private readonly Func<FlowDocument> _getDocument;
        private readonly Action<QuickNoteStatusKind, string?> _setStatus;
        private readonly Action _updateStatusSaved;
        private readonly Func<bool> _isLoaded;

        private bool _hasPendingChanges;
        private bool _saveAgainAfterCurrent;
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
            _saveTimer.Tick += async (_, _) => await SaveNowAsync();
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
            if (!_isLoaded() || (!_hasPendingChanges && !force))
            {
                return true;
            }

            TimeSpan waitTimeout = force
                ? QuickNoteWindow.ForcedSaveWaitTimeout
                : TimeSpan.Zero;
            if (waitTimeout == TimeSpan.Zero)
            {
                if (!await _saveSemaphore.WaitAsync(0).ConfigureAwait(true))
                {
                    _saveAgainAfterCurrent = true;
                    return true;
                }
            }
            else
            {
                if (!await _saveSemaphore.WaitAsync(waitTimeout).ConfigureAwait(true))
                {
                    _setStatus(QuickNoteStatusKind.SaveFailed, null);
                    return false;
                }
            }

            _setStatus(QuickNoteStatusKind.Saving, null);
            try
            {
                if (!_hasPendingChanges)
                {
                    _updateStatusSaved();
                    return true;
                }

                do
                {
                    if (!_hasPendingChanges && !force)
                    {
                        return true;
                    }

                    if (await _noteService.HasExternalChangesAsync().ConfigureAwait(true))
                    {
                        FlowDocument doc = _getDocument();
                        string conflictPath = await _noteService.SaveConflictCopyAsync(doc).ConfigureAwait(true);
                        _hasPendingChanges = false;
                        _saveAgainAfterCurrent = false;
                        _setStatus(QuickNoteStatusKind.ConflictCopySaved, Path.GetFileName(conflictPath));
                        return true;
                    }

                    _saveAgainAfterCurrent = false;
                    long savedVersion = _changeVersion;
                    try
                    {
                        await _noteService.SaveAsync(_getDocument()).ConfigureAwait(true);
                    }
                    catch (QuickNoteExternalChangeException)
                    {
                        FlowDocument doc = _getDocument();
                        string conflictPath = await _noteService.SaveConflictCopyAsync(doc).ConfigureAwait(true);
                        _hasPendingChanges = false;
                        _saveAgainAfterCurrent = false;
                        _setStatus(QuickNoteStatusKind.ConflictCopySaved, Path.GetFileName(conflictPath));
                        return true;
                    }

                    if (_changeVersion == savedVersion)
                    {
                        _hasPendingChanges = false;
                    }
                    else
                    {
                        _hasPendingChanges = true;
                        _saveAgainAfterCurrent = true;
                    }
                }
                while (_saveAgainAfterCurrent || (force && _hasPendingChanges));

                _updateStatusSaved();
                return true;
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
                _setStatus(QuickNoteStatusKind.SaveFailed, null);
                return false;
            }
            finally
            {
                _saveSemaphore.Release();
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _saveTimer.Stop();
            _saveSemaphore.Dispose();
        }
    }
}
