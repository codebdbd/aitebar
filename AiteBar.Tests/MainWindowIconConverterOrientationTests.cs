using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using AiteBar;

namespace AiteBar.Tests;

[Collection("WpfTestCollection")]
public sealed class MainWindowIconConverterOrientationTests
{
    [Fact]
    public async Task IconConverterButton_IsVisibleAndPlacedCorrectly_OnAllPanelEdges()
    {
        await RunStaAsync(() =>
        {
            EnsureApplicationResources();

            var window = new MainWindow();
            try
            {
                ConfigureSettingsForIconConverterOnly(window);

                var expectations = new Dictionary<DockEdge, (Orientation Orientation, Dock AppSettingsDock, Dock DragHandleDock, PlacementMode ToolTipPlacement)>
                {
                    [DockEdge.Top] = (Orientation.Horizontal, Dock.Right, Dock.Left, PlacementMode.Bottom),
                    [DockEdge.Bottom] = (Orientation.Horizontal, Dock.Right, Dock.Left, PlacementMode.Top),
                    [DockEdge.Left] = (Orientation.Vertical, Dock.Bottom, Dock.Top, PlacementMode.Right),
                    [DockEdge.Right] = (Orientation.Vertical, Dock.Bottom, Dock.Top, PlacementMode.Left)
                };

                foreach ((DockEdge edge, var expected) in expectations)
                {
                    window.GetAppSettings().Edge = edge;
                    window.RefreshPanel();
                    LayoutWindow(window);

                    var root = Assert.IsAssignableFrom<FrameworkElement>(window.FindName("RootBorder"));
                    var iconButton = Assert.IsType<Button>(window.FindName("BtnIconConverter"));
                    var systemUtilsPanel = Assert.IsAssignableFrom<Panel>(window.FindName("SystemUtilsPanel"));
                    var appSettingsBlock = Assert.IsAssignableFrom<FrameworkElement>(window.FindName("AppSettingsBlock"));
                    var dragHandle = Assert.IsAssignableFrom<FrameworkElement>(window.FindName("DragHandle"));

                    Assert.Equal(Visibility.Visible, iconButton.Visibility);
                    Assert.Equal(Visibility.Visible, systemUtilsPanel.Visibility);
                    Assert.Equal(expected.Orientation, ((OverflowWrapPanel)systemUtilsPanel).Orientation);
                    Assert.Equal(expected.AppSettingsDock, DockPanel.GetDock(appSettingsBlock));
                    Assert.Equal(expected.DragHandleDock, DockPanel.GetDock(dragHandle));
                    Assert.Equal(expected.ToolTipPlacement, ToolTipService.GetPlacement(iconButton));
                    AssertWithinRoot(root, iconButton, $"BtnIconConverter ({edge})");
                }
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public async Task IconConverterButton_HidesOutsidePrimaryContext()
    {
        await RunStaAsync(() =>
        {
            EnsureApplicationResources();

            var window = new MainWindow();
            try
            {
                ConfigureSettingsForIconConverterOnly(window);
                AppSettings settings = window.GetAppSettings();
                settings.Contexts[1].IsEnabled = true;
                settings.ActiveContextId = settings.Contexts[1].Id;

                window.RefreshPanel();
                LayoutWindow(window);

                var iconButton = Assert.IsType<Button>(window.FindName("BtnIconConverter"));
                var systemUtilsPanel = Assert.IsAssignableFrom<FrameworkElement>(window.FindName("SystemUtilsPanel"));

                Assert.Equal(Visibility.Collapsed, iconButton.Visibility);
                Assert.Equal(Visibility.Collapsed, systemUtilsPanel.Visibility);
            }
            finally
            {
                window.Close();
            }
        });
    }

    private static void ConfigureSettingsForIconConverterOnly(MainWindow window)
    {
        AppSettings settings = window.GetAppSettings();
        settings.Contexts = ContextStateHelper.NormalizeContexts(settings.Contexts);
        settings.ActiveContextId = settings.Contexts[0].Id;
        settings.ShowPresetSearch = false;
        settings.ShowPresetScreenshot = false;
        settings.ShowPresetVideo = false;
        settings.ShowPresetCalc = false;
        settings.ShowPresetExplorer = false;
        settings.ShowPresetDownloads = false;
        settings.ShowPresetFileSorter = false;
        settings.ShowPresetTimerStopwatch = false;
        settings.ShowPresetColorPicker = false;
        settings.ShowPresetQuickNote = false;
        settings.ShowPresetIconConverter = true;
    }

    private static void LayoutWindow(Window window)
    {
        window.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Size size = window.DesiredSize;
        window.Arrange(new Rect(0, 0, Math.Max(size.Width, 150), Math.Max(size.Height, 150)));
        window.UpdateLayout();
    }

    private static void AssertWithinRoot(FrameworkElement root, FrameworkElement element, string elementName)
    {
        Rect bounds = element.TransformToAncestor(root)
            .TransformBounds(new Rect(0, 0, element.ActualWidth, element.ActualHeight));

        Assert.True(bounds.Left >= -0.5, $"{elementName} left is outside the root: {bounds.Left}");
        Assert.True(bounds.Top >= -0.5, $"{elementName} top is outside the root: {bounds.Top}");
        Assert.True(bounds.Right <= root.ActualWidth + 0.5, $"{elementName} right is outside the root: {bounds.Right} > {root.ActualWidth}");
        Assert.True(bounds.Bottom <= root.ActualHeight + 0.5, $"{elementName} bottom is outside the root: {bounds.Bottom} > {root.ActualHeight}");
    }

    private static void EnsureApplicationResources()
    {
        if (Application.ResourceAssembly == null)
        {
            Application.ResourceAssembly = typeof(App).Assembly;
        }

        if (Application.Current is App)
        {
            return;
        }

        if (Application.Current == null)
        {
            var app = new App();
            app.InitializeComponent();
        }
    }

    private static Task RunStaAsync(Action action)
    {
        var completion = new TaskCompletionSource();
        var thread = new Thread(() =>
        {
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
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
