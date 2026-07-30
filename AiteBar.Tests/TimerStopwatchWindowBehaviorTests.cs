using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace AiteBar.Tests;

[Collection("WpfTestCollection")]
public sealed class TimerStopwatchWindowBehaviorTests
{
    [Fact]
    public async Task CompactButtons_ToggleRunningAndRestoreFullWindow()
    {
        await RunStaAsync(() =>
        {
            var window = new TimerStopwatchWindow();
            try
            {
                Button compact = Assert.IsType<Button>(window.FindName("BtnCompact"));
                Button startPause = Assert.IsType<Button>(window.FindName("BtnCompactStartPause"));
                Button expand = Assert.IsType<Button>(window.FindName("BtnCompactExpand"));
                FrameworkElement compactRoot = Assert.IsAssignableFrom<FrameworkElement>(
                    window.FindName("CompactRootBorder"));
                FrameworkElement fullRoot = Assert.IsAssignableFrom<FrameworkElement>(
                    window.FindName("RootBorder"));

                compact.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, compact));

                Assert.Equal(TimerStopwatchLayoutHelper.CompactWindowWidth, window.Width);
                Assert.Equal(Visibility.Visible, compactRoot.Visibility);
                Assert.Equal(Visibility.Collapsed, fullRoot.Visibility);
                Assert.Equal(TimerStopwatchLayoutHelper.CompactPlayGlyph, startPause.Content);

                startPause.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, startPause));
                Assert.Equal(TimerStopwatchLayoutHelper.CompactPauseGlyph, startPause.Content);

                startPause.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, startPause));
                Assert.Equal(TimerStopwatchLayoutHelper.CompactPlayGlyph, startPause.Content);

                expand.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, expand));

                Assert.Equal(TimerStopwatchLayoutHelper.TimerWindowWidth, window.Width);
                Assert.Equal(Visibility.Collapsed, compactRoot.Visibility);
                Assert.Equal(Visibility.Visible, fullRoot.Visibility);
            }
            finally
            {
                window.Close();
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
