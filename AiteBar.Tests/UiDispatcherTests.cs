using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace AiteBar.Tests;

public sealed class UiDispatcherTests
{
    [Fact]
    public async Task Run_MarshalsBackgroundCallerToDispatcherThread()
    {
        var dispatcherReady = new TaskCompletionSource<(Dispatcher Dispatcher, int ThreadId)>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var actionCompleted = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var dispatcherThread = new Thread(() =>
        {
            Dispatcher dispatcher = Dispatcher.CurrentDispatcher;
            dispatcherReady.SetResult((dispatcher, Environment.CurrentManagedThreadId));
            Dispatcher.Run();
        })
        {
            IsBackground = true
        };
        dispatcherThread.SetApartmentState(ApartmentState.STA);
        dispatcherThread.Start();

        (Dispatcher dispatcher, int dispatcherThreadId) = await dispatcherReady.Task;
        try
        {
            await Task.Run(() => UiDispatcher.Run(
                dispatcher,
                () => actionCompleted.SetResult(Environment.CurrentManagedThreadId)));

            int actionThreadId = await actionCompleted.Task.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.Equal(dispatcherThreadId, actionThreadId);
        }
        finally
        {
            dispatcher.InvokeShutdown();
            Assert.True(dispatcherThread.Join(TimeSpan.FromSeconds(5)));
        }
    }
}
