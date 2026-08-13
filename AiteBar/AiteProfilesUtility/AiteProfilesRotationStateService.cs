using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace AiteBar.AiteProfilesUtility;

internal sealed class AiteProfilesRotationStateService
{
    private readonly object _sync = new();
    private readonly string _statePath;
    private bool _enabled;
    private string _lastProfileKey = string.Empty;
    private List<string> _rotationOrder = [];

    public AiteProfilesRotationStateService(string rootDirectory)
    {
        _statePath = Path.Combine(rootDirectory, "rotation_state.json");
        Restore();
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
            Persist();
        }
    }

    public void SetLastProfileKey(string profileKey)
    {
        lock (_sync)
        {
            _lastProfileKey = (profileKey ?? string.Empty).Trim();
            Persist();
        }
    }

    public void SetRotationOrder(IEnumerable<string>? rotationOrder)
    {
        lock (_sync)
        {
            _rotationOrder = NormalizeRotationOrder(rotationOrder);
            Persist();
        }
    }

    private void Restore()
    {
        lock (_sync)
        {
            try
            {
                var state = AiteProfilesJsonStore.ReadAsync<RotationStateRecord>(_statePath, CancellationToken.None)
                    .GetAwaiter()
                    .GetResult();
                if (state is null)
                {
                    return;
                }

                _enabled = state.Enabled;
                _lastProfileKey = state.LastProfileKey ?? string.Empty;
                _rotationOrder = NormalizeRotationOrder(state.RotationOrder);
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
                _enabled = false;
                _lastProfileKey = string.Empty;
                _rotationOrder = [];
            }
        }
    }

    private void Persist()
    {
        try
        {
            AiteProfilesJsonStore.WriteAsync(_statePath, new RotationStateRecord
            {
                Enabled = _enabled,
                LastProfileKey = _lastProfileKey,
                RotationOrder = _rotationOrder
            }).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            Logger.Log(ex);
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
