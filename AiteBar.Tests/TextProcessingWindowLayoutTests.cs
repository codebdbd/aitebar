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
                settings.Settings = new AppSettings
                {
                    TextProcessingLastMode = (int)TextProcessingMode.Typography
                };
                window = new TextProcessingWindow(new TextProcessingService(), settings);
                Assert.True(window.MinWidth >= 1000);
                var root = Assert.IsAssignableFrom<FrameworkElement>(window.Content);
                var editor = Assert.IsType<TextBox>(window.FindName("TxtEditor"));
                var editorCard = Assert.IsAssignableFrom<FrameworkElement>(window.FindName("EditorCard"));
                var contentHost = Assert.IsAssignableFrom<FrameworkElement>(window.FindName("ContentHost"));
                var viewport = Assert.IsType<ScrollViewer>(window.FindName("LayoutViewport"));
                var process = Assert.IsType<Button>(window.FindName("BtnProcess"));
                var refreshModels = Assert.IsType<Button>(window.FindName("BtnRefreshModels"));
                var repeat = Assert.IsType<Button>(window.FindName("BtnRepeat"));
                var typographyMode = Assert.IsType<TabItem>(window.FindName("ModeTypography"));
                var toggleLabel = Assert.IsType<TextBlock>(window.FindName("ToggleVersionLabel"));
                var processLabel = Assert.IsType<TextBlock>(window.FindName("ProcessButtonLabel"));
                Button[] commandButtons =
                [
                    Assert.IsType<Button>(window.FindName("BtnPaste")),
                    Assert.IsType<Button>(window.FindName("BtnCopy")),
                    repeat,
                    Assert.IsType<Button>(window.FindName("BtnToggleVersion")),
                    Assert.IsType<Button>(window.FindName("BtnClear")),
                    process
                ];
                toggleLabel.Text = LocalizationService.Get("TextProcessing_ButtonShowResult");
                processLabel.Text = LocalizationService.Get("TextProcessing_ButtonCancel");

                double minimumClientWidth = window.MinWidth - 16;
                root.Measure(new Size(minimumClientWidth, 700));
                root.Arrange(new Rect(0, 0, minimumClientWidth, 700));
                root.UpdateLayout();
                Assert.True(
                    contentHost.ActualWidth + root.Margin.Left + root.Margin.Right <= minimumClientWidth);

                root.Measure(new Size(1400, 700));
                root.Arrange(new Rect(0, 0, 1400, 700));
                root.UpdateLayout();
                double compactEditorHeight = editor.ActualHeight;

                root.Measure(new Size(1800, 1000));
                root.Arrange(new Rect(0, 0, 1800, 1000));
                root.UpdateLayout();

                double commandWidth = commandButtons.Max(button => button.ActualWidth);
                Assert.True(contentHost.ActualWidth > commandWidth);
                Assert.Equal(738, editorCard.ActualWidth, precision: 1);
                Assert.Equal(738 + 16 + commandWidth, contentHost.ActualWidth, precision: 1);
                Assert.True(editor.ActualHeight > compactEditorHeight + 250);
                Assert.All(commandButtons, button =>
                {
                    Assert.Equal(commandWidth, button.ActualWidth, precision: 1);
                    Assert.True(commandWidth >= button.MinWidth);
                    Assert.Equal(44, button.ActualHeight, precision: 1);
                    var content = Assert.IsAssignableFrom<FrameworkElement>(button.Content);
                    content.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                    double availableContentWidth =
                        button.ActualWidth -
                        button.Padding.Left -
                        button.Padding.Right -
                        button.BorderThickness.Left -
                        button.BorderThickness.Right;
                    Assert.True(
                        content.DesiredSize.Width <= availableContentWidth,
                        $"Button content requires {content.DesiredSize.Width:F2}px, but only {availableContentWidth:F2}px is available.");
                });
                Assert.False(process.IsEnabled);
                Assert.False(repeat.IsEnabled);
                Assert.True(typographyMode.IsSelected);
                Assert.Equal(36, refreshModels.ActualWidth, precision: 1);
                Assert.Equal(36, refreshModels.ActualHeight, precision: 1);

                root.Measure(new Size(760, 700));
                root.Arrange(new Rect(0, 0, 760, 700));
                root.UpdateLayout();
                Assert.True(viewport.ScrollableWidth > 0);
                Assert.Equal(738, editorCard.ActualWidth, precision: 1);
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
