using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace AiteBar.Tests;

[Collection("WpfTestCollection")]
public sealed class TextProcessingWindowLayoutTests
{
    [Fact]
    public async Task Window_LoadsReleaseXamlAndKeepsEditorWidthWhileHeightExpands()
    {
        await RunStaAsync(() =>
        {
            string tempRoot = Path.Combine(Path.GetTempPath(), $"aitebar-text-ui-{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempRoot);
            TextProcessingWindow? window = null;
            try
            {
                var settings = new AppSettingsService(
                    Path.Combine(tempRoot, "legacy.json"),
                    Path.Combine(tempRoot, "settings.json"));
                window = new TextProcessingWindow(new TextProcessingService(), settings);
                var root = Assert.IsAssignableFrom<FrameworkElement>(window.Content);
                var editor = Assert.IsType<TextBox>(window.FindName("TxtEditor"));
                var editorCard = Assert.IsAssignableFrom<FrameworkElement>(window.FindName("EditorCard"));
                var contentHost = Assert.IsAssignableFrom<FrameworkElement>(window.FindName("ContentHost"));
                var process = Assert.IsType<Button>(window.FindName("BtnProcess"));
                var repeat = Assert.IsType<Button>(window.FindName("BtnRepeat"));
                Button[] commandButtons =
                [
                    Assert.IsType<Button>(window.FindName("BtnPaste")),
                    Assert.IsType<Button>(window.FindName("BtnCopy")),
                    repeat,
                    Assert.IsType<Button>(window.FindName("BtnToggleVersion")),
                    Assert.IsType<Button>(window.FindName("BtnClear")),
                    process
                ];
                repeat.Visibility = Visibility.Visible;
                commandButtons[3].Visibility = Visibility.Visible;

                root.Measure(new Size(1400, 700));
                root.Arrange(new Rect(0, 0, 1400, 700));
                root.UpdateLayout();
                double compactEditorHeight = editor.ActualHeight;

                root.Measure(new Size(1800, 1000));
                root.Arrange(new Rect(0, 0, 1800, 1000));
                root.UpdateLayout();

                Assert.Equal(974, contentHost.ActualWidth, precision: 1);
                Assert.Equal(738, editorCard.ActualWidth, precision: 1);
                Assert.True(editor.ActualHeight > compactEditorHeight + 250);
                Assert.All(commandButtons, button =>
                {
                    Assert.Equal(220, button.ActualWidth, precision: 1);
                    Assert.Equal(44, button.ActualHeight, precision: 1);
                });
                Assert.False(process.IsEnabled);
                Assert.False(repeat.IsEnabled);
            }
            finally
            {
                window?.Close();
                Directory.Delete(tempRoot, recursive: true);
            }
        });
    }

    private static Task RunStaAsync(Action action)
    {
        var completion = new TaskCompletionSource();
        var thread = new Thread(() =>
        {
            SynchronizationContext.SetSynchronizationContext(
                new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
            Dispatcher.CurrentDispatcher.Invoke(() =>
            {
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
                    Dispatcher.CurrentDispatcher.BeginInvokeShutdown(DispatcherPriority.Background);
                }
            });
            Dispatcher.Run();
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return completion.Task;
    }
}
