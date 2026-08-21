using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace AiteBar.Tests;

[Collection("WpfTestCollection")]
public sealed class TextProcessingWindowLayoutTests
{
    [Fact]
    public void ClosingDuringStreaming_PersistsOriginalInputInsteadOfPreview()
    {
        string code = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "AiteBar", "TextProcessingWindow.xaml.cs"));

        Assert.Contains("private string? _processingSourceText;", code);
        Assert.Contains("_processingSourceText = input;", code);
        Assert.Contains("_isProcessing ? _processingSourceText ?? TxtEditor.Text ?? string.Empty", code);
        Assert.Contains("_processingCts?.Cancel();\n        _loadModelsCts?.Cancel();\n        SaveEditorText();", code.ReplaceLineEndings("\n"));
    }

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
                var counters = Assert.IsType<TextBlock>(window.FindName("TxtCounters"));
                var modelState = Assert.IsType<TextBlock>(window.FindName("TxtModelState"));
                var contentHost = Assert.IsAssignableFrom<FrameworkElement>(window.FindName("ContentHost"));
                var viewport = Assert.IsType<ScrollViewer>(window.FindName("LayoutViewport"));
                var process = Assert.IsType<Button>(window.FindName("BtnProcess"));
                var refreshModels = Assert.IsType<Button>(window.FindName("BtnRefreshModels"));
                var repeat = Assert.IsType<Button>(window.FindName("BtnRepeat"));
                var showDiff = Assert.IsType<Button>(window.FindName("BtnShowDiff"));
                var modeTabs = Assert.IsType<TabControl>(window.FindName("ModeTabs"));
                var proofreadMode = Assert.IsType<TabItem>(window.FindName("ModeProofread"));
                var literaryEditMode = Assert.IsType<TabItem>(window.FindName("ModeLiteraryEdit"));
                var naturalStyleMode = Assert.IsType<TabItem>(window.FindName("ModeNaturalStyle"));
                var toggleLabel = Assert.IsType<TextBlock>(window.FindName("ToggleVersionLabel"));
                var showDiffLabel = Assert.IsType<TextBlock>(window.FindName("ShowDiffLabel"));
                var processLabel = Assert.IsType<TextBlock>(window.FindName("ProcessButtonLabel"));
                Button[] commandButtons =
                [
                    Assert.IsType<Button>(window.FindName("BtnPaste")),
                    Assert.IsType<Button>(window.FindName("BtnCopy")),
                    repeat,
                    Assert.IsType<Button>(window.FindName("BtnToggleVersion")),
                    showDiff,
                    Assert.IsType<Button>(window.FindName("BtnClear")),
                    process
                ];
                Button[] railButtons = commandButtons[..^1];
                toggleLabel.Text = LocalizationService.Get("TextProcessing_ButtonShowResult");
                processLabel.Text = LocalizationService.Get("TextProcessing_ButtonCancel");

                typeof(TextProcessingWindow)
                    .GetField("_isShowingDiff", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .SetValue(window, true);
                typeof(TextProcessingWindow)
                    .GetMethod("RefreshUiState", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(window, null);
                Assert.Equal(
                    LocalizationService.Get("TextProcessing_ButtonHideDiff"),
                    showDiffLabel.Text);
                Assert.Equal(showDiffLabel.Text, showDiff.ToolTip);
                Assert.Equal(
                    showDiffLabel.Text,
                    System.Windows.Automation.AutomationProperties.GetName(showDiff));
                typeof(TextProcessingWindow)
                    .GetField("_isShowingDiff", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .SetValue(window, false);
                typeof(TextProcessingWindow)
                    .GetMethod("RefreshUiState", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(window, null);
                Assert.Equal(
                    LocalizationService.Get("TextProcessing_ButtonShowDiff"),
                    showDiffLabel.Text);

                double minimumClientWidth = window.MinWidth - 16;
                root.Measure(new Size(minimumClientWidth, 700));
                root.Arrange(new Rect(0, 0, minimumClientWidth, 700));
                root.UpdateLayout();
                Assert.True(
                    contentHost.ActualWidth + root.Margin.Left + root.Margin.Right <= minimumClientWidth);
                Point railBottom = railButtons[^1].TranslatePoint(
                    new Point(0, railButtons[^1].ActualHeight),
                    editorCard);
                Assert.True(
                    railBottom.Y <= editorCard.ActualHeight,
                    $"Command rail requires {railBottom.Y:F2}px, but editor area is {editorCard.ActualHeight:F2}px high.");

                root.Measure(new Size(1400, 700));
                root.Arrange(new Rect(0, 0, 1400, 700));
                root.UpdateLayout();
                double compactEditorHeight = editor.ActualHeight;

                typeof(TextProcessingWindow)
                    .GetMethod("SetStatus", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(window, ["Ошибка подключения"]);
                root.Measure(new Size(1400, 700));
                root.Arrange(new Rect(0, 0, 1400, 700));
                root.UpdateLayout();
                Assert.Equal(compactEditorHeight, editor.ActualHeight, precision: 1);
                typeof(TextProcessingWindow)
                    .GetMethod("SetStatus", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(window, [string.Empty]);

                typeof(TextProcessingWindow)
                    .GetField("_hasEligibleModel", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .SetValue(window, true);
                typeof(TextProcessingWindow)
                    .GetMethod("SetInfoStatus", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(window, ["Использована: Writer"]);
                root.Measure(new Size(1400, 700));
                root.Arrange(new Rect(0, 0, 1400, 700));
                root.UpdateLayout();
                Assert.Equal(compactEditorHeight, editor.ActualHeight, precision: 1);
                Assert.Equal(Visibility.Visible, modelState.Visibility);
                Assert.Equal("Использована: Writer", modelState.Text);
                Point countersBottom = counters.TranslatePoint(
                    new Point(0, counters.ActualHeight),
                    editorCard);
                Assert.True(countersBottom.Y <= editorCard.ActualHeight);

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
                Assert.True(proofreadMode.IsSelected);
                Assert.False(literaryEditMode.IsSelected);
                Assert.False(naturalStyleMode.IsSelected);
                Assert.Equal(5, modeTabs.Items.Count);
                Assert.Equal(
                    LocalizationService.Get("TextProcessing_ModeLiteraryEdit"),
                    literaryEditMode.Header);
                Assert.Equal(
                    LocalizationService.Get("TextProcessing_ModeNaturalStyle"),
                    naturalStyleMode.Header);
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
