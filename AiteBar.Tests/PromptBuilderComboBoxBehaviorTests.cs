using System.Collections;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace AiteBar.Tests;

[Collection("WpfTestCollection")]
public sealed class PromptBuilderComboBoxBehaviorTests
{
    [Fact]
    public async Task GeneratorComboBox_DisplaysEverySelectedModel()
    {
        await RunStaAsync(() =>
        {
            string tempRoot = Path.Combine(Path.GetTempPath(), $"aitebar-prompt-model-ui-{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempRoot);
            PromptBuilderWindow? window = null;
            try
            {
                var settings = new AppSettingsService(
                    Path.Combine(tempRoot, "legacy.json"),
                    Path.Combine(tempRoot, "settings.json"));
                window = new PromptBuilderWindow(new PromptBuilderService(), settings);
                var comboBox = Assert.IsType<ComboBox>(window.FindName("CmbModels"));
                var models = Assert.IsAssignableFrom<IList>(comboBox.ItemsSource);
                var first = new ModelItem("openai", "first", "First", null)
                {
                    FullDisplay = "Первый генератор"
                };
                var second = new ModelItem("google", "second", "Second", null)
                {
                    FullDisplay = "Второй генератор с длинным названием"
                };
                models.Add(first);
                models.Add(second);

                FrameworkElement root = Assert.IsAssignableFrom<FrameworkElement>(window.Content);
                root.Measure(new Size(1280, 840));
                root.Arrange(new Rect(0, 0, 1280, 840));
                root.UpdateLayout();
                comboBox.ApplyTemplate();
                var selectedText = Assert.IsType<MarqueeTextBlock>(
                    comboBox.Template.FindName("ContentSite", comboBox));

                comboBox.SelectedItem = first;
                root.UpdateLayout();
                Assert.Equal(first.FullDisplay, selectedText.Text);

                comboBox.SelectedItem = second;
                root.UpdateLayout();
                Assert.Equal(second.FullDisplay, selectedText.Text);

                comboBox.SelectedIndex = 0;
                root.UpdateLayout();
                Assert.Equal(
                    ((ModelItem)comboBox.SelectedItem).FullDisplay,
                    selectedText.Text);
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
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
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
