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
        private const int MaxRetryAttempts = 4;

        private readonly IQuickNotePersistence _noteService;
        private readonly DispatcherTimer _saveTimer;
        private readonly DispatcherTimer _retryTimer;
        private TaskCompletionSource<bool>? _activeSave;
        private readonly Func<FlowDocument> _getDocument;
        private readonly Action<QuickNoteStatusKind, string?> _setStatus;
        private readonly Action _updateStatusSaved;
        private readonly Func<bool> _isLoaded;

        private bool _hasPendingChanges;
        private long _changeVersion;
        private int _retryAttempt;
        private string? _activeConflictSessionPath;
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

            _retryTimer = new DispatcherTimer();
            _retryTimer.Tick += RetryTimer_Tick;
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
            _retryTimer.Stop();
        }

        public void Schedule()
        {
            if (_disposed) return;
            _retryTimer.Stop();
            _setStatus(QuickNoteStatusKind.Saving, null);
            _saveTimer.Stop();
            _saveTimer.Start();
        }

        public async Task<bool> SaveNowAsync(bool force = false)
        {
            if (_disposed) return false;

            _saveTimer.Stop();
            _retryTimer.Stop();
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

            var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            _activeSave = completion;
            _setStatus(QuickNoteStatusKind.Saving, null);
            bool succeeded = false;

            try
            {
                Task<bool> execution = ExecuteSaveLoopAsync(force);
                succeeded = force
                    ? await execution.WaitAsync(QuickNoteWindow.ForcedSaveWaitTimeout)
                    : await execution;
                return succeeded;
            }
            catch (TimeoutException)
            {
                if (!_disposed) _setStatus(QuickNoteStatusKind.SaveFailed, null);
                return false;
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
                if (!_disposed)
                {
                    _setStatus(QuickNoteStatusKind.SaveFailed, null);
                    if (!force && IsTransientSaveException(ex) && _retryAttempt < MaxRetryAttempts)
                    {
                        ScheduleRetry();
                    }
                }
                return false;
            }
            finally
            {
                _activeSave = null;
                completion.TrySetResult(succeeded);
            }
        }

        private async Task<bool> ExecuteSaveLoopAsync(bool force)
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
                    {
                        if (_activeConflictSessionPath != null && File.Exists(_activeConflictSessionPath))
                        {
                            await _noteService.UpdateConflictCopyAsync(_activeConflictSessionPath, _getDocument());
                            conflictPath = _activeConflictSessionPath;
                        }
                        else
                        {
                            conflictPath = await _noteService.SaveConflictCopyAsync(_getDocument());
                            _activeConflictSessionPath = conflictPath;
                        }
                    }
                    else
                    {
                        await _noteService.SaveAsync(_getDocument());
                        _activeConflictSessionPath = null;
                    }
                }
                catch (QuickNoteExternalChangeException)
                {
                    if (_disposed) return false;
                    savedVersion = _changeVersion;
                    if (_activeConflictSessionPath != null && File.Exists(_activeConflictSessionPath))
                    {
                        await _noteService.UpdateConflictCopyAsync(_activeConflictSessionPath, _getDocument());
                        conflictPath = _activeConflictSessionPath;
                    }
                    else
                    {
                        conflictPath = await _noteService.SaveConflictCopyAsync(_getDocument());
                        _activeConflictSessionPath = conflictPath;
                    }
                }

                _hasPendingChanges = _changeVersion != savedVersion;
                if (!force && _hasPendingChanges)
                {
                    Schedule();
                    break;
                }
            }

            if (_disposed) return false;
            _saveTimer.Stop();
            _retryAttempt = 0;
            if (conflictPath != null)
                _setStatus(QuickNoteStatusKind.ConflictCopySaved, Path.GetFileName(conflictPath));
            else
                _updateStatusSaved();

            return true;
        }

        private void ScheduleRetry()
        {
            _retryAttempt++;
            _retryTimer.Stop();
            _retryTimer.Interval = TimeSpan.FromSeconds(Math.Min(10, Math.Pow(2, _retryAttempt)));
            _retryTimer.Start();
        }

        private static bool IsTransientSaveException(Exception ex) =>
            ex is IOException && ex is not UnauthorizedAccessException;

        private async void SaveTimer_Tick(object? sender, EventArgs e) => await SaveNowAsync();

        private async void RetryTimer_Tick(object? sender, EventArgs e)
        {
            _retryTimer.Stop();
            if (_hasPendingChanges && !_disposed)
            {
                await SaveNowAsync();
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _saveTimer.Stop();
            _saveTimer.Tick -= SaveTimer_Tick;
            _retryTimer.Stop();
            _retryTimer.Tick -= RetryTimer_Tick;
        }
    }
}
