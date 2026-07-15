using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Threading;

namespace AiteBar.Tests;

[Collection("WpfTestCollection")]
public sealed class ClipboardManagerWindowBehaviorTests
{
    [Fact]
    public async Task Minimize_HidesWindow_AndRestoreShowsSameInstance()
    {
        await RunStaAsync(() =>
        {
            string tempRoot = Path.Combine(Path.GetTempPath(), "AiteBarTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempRoot);

            try
            {
                using var historyService = new ClipboardHistoryService(
                    Path.Combine(tempRoot, "clipboard_history.json"),
                    persistHistory: false);
                var window = new ClipboardManagerWindow(historyService);

                try
                {
                    window.Show();
                    window.WindowState = WindowState.Minimized;
                    Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);

                    Assert.False(window.IsVisible);
                    Assert.Equal(WindowState.Normal, window.WindowState);

                    window.RestoreFromAiteBar();

                    Assert.True(window.IsVisible);
                    Assert.Equal(WindowState.Normal, window.WindowState);
                }
                finally
                {
                    window.Close();
                }
            }
            finally
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        });
    }

    private static Task RunStaAsync(Action action)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            SynchronizationContext.SetSynchronizationContext(
                new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));

            try
            {
                action();
                completion.SetResult();
            }
            catch (Exception ex)
            {
                completion.SetException(ex);
            }
            finally
            {
                Dispatcher.CurrentDispatcher.InvokeShutdown();
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return completion.Task;
    }
}
