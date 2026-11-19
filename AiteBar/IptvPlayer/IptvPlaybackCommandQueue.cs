using System;
using System.Threading;
using System.Threading.Tasks;

namespace AiteBar.IptvPlayer;

internal sealed class IptvPlaybackCommandQueue
{
    private readonly object _gate = new();
    private Task _tail = Task.CompletedTask;

    public Task Enqueue(Action command, Func<bool>? isCurrent = null)
    {
        lock (_gate)
        {
            _tail = _tail.ContinueWith(_ =>
            {
                if (isCurrent?.Invoke() != false) command();
            }, CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default);
            return _tail;
        }
    }
}
