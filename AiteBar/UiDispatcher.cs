using System;
using System.Windows.Threading;

namespace AiteBar;

internal static class UiDispatcher
{
    public static void Run(Dispatcher dispatcher, Action action)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(action);

        if (dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished)
        {
            return;
        }

        if (dispatcher.CheckAccess())
        {
            action();
            return;
        }

        _ = dispatcher.InvokeAsync(action, DispatcherPriority.Normal);
    }
}
