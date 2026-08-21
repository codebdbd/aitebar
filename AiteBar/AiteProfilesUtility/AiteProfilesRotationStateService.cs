using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AiteBar.AiteProfilesUtility;

internal sealed class AiteProfilesRotationStateService
{
    private readonly object _sync = new();
    private readonly SemaphoreSlim _persistGate = new(1, 1);
    private readonly string _statePath;
    private bool _enabled;
    private string _lastProfileKey = string.Empty;
    private List<string> _rotationOrder = [];
    private bool _hasLocalChanges;

    public event Action<Exception>? PersistenceFailed;

    public AiteProfilesRotationStateService(string rootDirectory)
    {
        _statePath = Path.Combine(rootDirectory, "rotation_state.json");
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            RotationStateRecord? state = await AiteProfilesJsonStore.ReadAsync<RotationStateRecord>(_statePath, cancellationToken).ConfigureAwait(false);
            if (state is null)
            {
                return;
            }

            lock (_sync)
            {
                if (_hasLocalChanges)
                {
                    return;
                }

                _enabled = state.Enabled;
                _lastProfileKey = state.LastProfileKey ?? string.Empty;
                _rotationOrder = NormalizeRotationOrder(state.RotationOrder);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Logger.Log(ex);
        }
    }

    public bool GetEnabled()
    {
        lock (_sync)
        {
            return _enabled;
        }
    }

    public string GetLastProfileKey()
    {
        lock (_sync)
        {
            return _lastProfileKey;
        }
    }

    public IReadOnlyList<string> GetRotationOrder()
    {
        lock (_sync)
        {
            return _rotationOrder.ToArray();
        }
    }

    public void SetEnabled(bool enabled)
    {
        lock (_sync)
        {
            _enabled = enabled;
            _hasLocalChanges = true;
        }

        QueuePersist();
    }

    public void SetLastProfileKey(string profileKey)
    {
        lock (_sync)
        {
            _lastProfileKey = (profileKey ?? string.Empty).Trim();
            _hasLocalChanges = true;
        }

        QueuePersist();
    }

    public void SetRotationOrder(IEnumerable<string>? rotationOrder)
    {
        lock (_sync)
        {
            _rotationOrder = NormalizeRotationOrder(rotationOrder);
            _hasLocalChanges = true;
        }

        QueuePersist();
    }

    internal async Task<bool> FlushAsync(TimeSpan timeout)
    {
        using var cancellationSource = new CancellationTokenSource(timeout);
        try
        {
            await PersistLatestAsync(cancellationSource.Token, suppressFailure: false).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException) when (cancellationSource.IsCancellationRequested)
        {
            return false;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private void QueuePersist() => _ = PersistLatestAsync(CancellationToken.None, suppressFailure: true);

    private async Task PersistLatestAsync(CancellationToken cancellationToken, bool suppressFailure)
    {
        try
        {
            await _persistGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                RotationStateRecord state;
                lock (_sync)
                {
                    state = new RotationStateRecord
                    {
                        Enabled = _enabled,
                        LastProfileKey = _lastProfileKey,
                        RotationOrder = [.. _rotationOrder]
                    };
                }

                await AiteProfilesJsonStore.WriteAsync(_statePath, state, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _persistGate.Release();
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Logger.Log(ex);
            PersistenceFailed?.Invoke(ex);
            if (!suppressFailure)
            {
                throw;
            }
        }
    }

    private static List<string> NormalizeRotationOrder(IEnumerable<string>? rotationOrder) =>
        rotationOrder is null
            ? []
            : rotationOrder
                .Select(static key => (key ?? string.Empty).Trim())
                .Where(static key => !string.IsNullOrWhiteSpace(key))
                .Distinct(StringComparer.Ordinal)
                .ToList();

    private sealed class RotationStateRecord
    {
        public bool Enabled { get; set; }
        public string LastProfileKey { get; set; } = string.Empty;
        public List<string> RotationOrder { get; set; } = [];
    }
}
