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

                var expectations = new Dictionary<DockEdge, (Orientation Orientation, Dock AppSettingsDock, Dock DragHandleDock, PlacementMode ToolTipPlacement, double HorizontalOffset, double VerticalOffset)>
                {
                    [DockEdge.Top] = (Orientation.Horizontal, Dock.Right, Dock.Left, PlacementMode.Bottom, 0d, 4d),
                    [DockEdge.Bottom] = (Orientation.Horizontal, Dock.Right, Dock.Left, PlacementMode.Top, 0d, -4d),
                    [DockEdge.Left] = (Orientation.Vertical, Dock.Bottom, Dock.Top, PlacementMode.Right, 4d, 4d),
                    [DockEdge.Right] = (Orientation.Vertical, Dock.Bottom, Dock.Top, PlacementMode.Left, -4d, 4d)
                };

                foreach ((DockEdge edge, var expected) in expectations)
                {
                    var settings = window.GetAppSettings();
                    settings.Edge = edge;
                    window.GetSettingsService().Settings = settings;
                    window.RefreshPanel();
                    LayoutWindow(window);

                    var root = Assert.IsAssignableFrom<FrameworkElement>(window.FindName("RootBorder"));
                    var unifiedPanel = Assert.IsAssignableFrom<OverflowWrapPanel>(window.FindName("UnifiedButtonsPanel"));
                    var appSettingsBlock = Assert.IsAssignableFrom<FrameworkElement>(window.FindName("AppSettingsBlock"));
                    var dragHandle = Assert.IsAssignableFrom<FrameworkElement>(window.FindName("DragHandle"));

                    Assert.Single(unifiedPanel.Children);
                    var iconButton = Assert.IsType<Button>(unifiedPanel.Children[0]);
                    Assert.Equal(Visibility.Visible, iconButton.Visibility);
                    Assert.Equal(expected.Orientation, unifiedPanel.Orientation);
                    Assert.Equal(expected.AppSettingsDock, DockPanel.GetDock(appSettingsBlock));
                    Assert.Equal(expected.DragHandleDock, DockPanel.GetDock(dragHandle));
                    AssertPanelToolTipPlacement(iconButton, expected.ToolTipPlacement, expected.HorizontalOffset, expected.VerticalOffset);
                    AssertPanelToolTipPlacement(
                        Assert.IsType<Button>(window.FindName("BtnAdd")),
                        expected.ToolTipPlacement,
                        expected.HorizontalOffset,
                        expected.VerticalOffset);
                    AssertPanelToolTipPlacement(
                        Assert.IsType<Button>(window.FindName("BtnAppSettings")),
                        expected.ToolTipPlacement,
                        expected.HorizontalOffset,
                        expected.VerticalOffset);
                    AssertWithinRoot(root, iconButton, $"IconConverter ({edge})");
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
                window.GetSettingsService().Settings = settings;

                window.RefreshPanel();
                LayoutWindow(window);

                var unifiedPanel = Assert.IsAssignableFrom<OverflowWrapPanel>(window.FindName("UnifiedButtonsPanel"));
                Assert.Empty(unifiedPanel.Children);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public async Task VerticalPanel_KeepsCurrentContextColumnWidth_WhenAnotherContextIsTaller()
    {
        await RunStaAsync(() =>
        {
            EnsureApplicationResources();

            var window = new MainWindow();
            try
            {
                ConfigureSettingsForIconConverterOnly(window);
                AppSettings settings = window.GetAppSettings();
                settings.Edge = DockEdge.Left;
                settings.PanelSizePercent = 50;
                settings.Contexts[1].IsEnabled = true;
                settings.ActiveContextId = settings.Contexts[0].Id;
                settings.Elements = CreateContextElements(settings.Contexts[1].Id, 20);
                window.GetSettingsService().Settings = settings;

                window.RefreshPanel();
                LayoutWindow(window);

                var root = Assert.IsAssignableFrom<FrameworkElement>(window.FindName("RootBorder"));
                var unifiedPanel = Assert.IsAssignableFrom<OverflowWrapPanel>(window.FindName("UnifiedButtonsPanel"));

                Assert.Single(unifiedPanel.Children);
                Assert.Equal(44, unifiedPanel.Width);
                Assert.Equal(52, root.MinWidth);
                Assert.Equal(root.MinWidth, root.MaxWidth);
                Assert.True(root.MinHeight > 52 + 18, $"Expected vertical primary size to be reserved from the taller context, got {root.MinHeight}.");
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public async Task HorizontalPanel_KeepsCurrentContextRowHeight_WhenAnotherContextIsWider()
    {
        await RunStaAsync(() =>
        {
            EnsureApplicationResources();

            var window = new MainWindow();
            try
            {
                ConfigureSettingsForIconConverterOnly(window);
                AppSettings settings = window.GetAppSettings();
                settings.Edge = DockEdge.Top;
                settings.PanelSizePercent = 100;
                settings.Contexts[1].IsEnabled = true;
                settings.ActiveContextId = settings.Contexts[0].Id;
                settings.Elements = CreateContextElements(settings.Contexts[1].Id, 20);
                window.GetSettingsService().Settings = settings;

                window.RefreshPanel();
                LayoutWindow(window);

                var root = Assert.IsAssignableFrom<FrameworkElement>(window.FindName("RootBorder"));
                var unifiedPanel = Assert.IsAssignableFrom<OverflowWrapPanel>(window.FindName("UnifiedButtonsPanel"));

                Assert.Single(unifiedPanel.Children);
                Assert.Equal(44, unifiedPanel.Height);
                Assert.Equal(52, root.MinHeight);
                Assert.Equal(root.MinHeight, root.MaxHeight);
                Assert.True(root.MinWidth > 220, $"Expected horizontal primary size to be reserved from the wider context, got {root.MinWidth}.");
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public async Task OrientationRoundTrip_KeepsStablePrimarySizeFromLargestContext()
    {
        await RunStaAsync(() =>
        {
            EnsureApplicationResources();

            var window = new MainWindow();
            try
            {
                ConfigureSettingsForIconConverterOnly(window);
                AppSettings settings = window.GetAppSettings();
                settings.Edge = DockEdge.Top;
                settings.PanelSizePercent = 50;
                settings.Contexts[1].IsEnabled = true;
                settings.ActiveContextId = settings.Contexts[0].Id;
                settings.Elements = CreateContextElements(settings.Contexts[1].Id, 20);
                window.GetSettingsService().Settings = settings;

                window.RefreshPanel();
                LayoutWindow(window);

                var root = Assert.IsAssignableFrom<FrameworkElement>(window.FindName("RootBorder"));
                double initialHorizontalWidth = root.MinWidth;

                ApplyEdge(window, settings, DockEdge.Right);
                double rightHeight = root.MinHeight;

                ApplyEdge(window, settings, DockEdge.Left);
                Assert.Equal(rightHeight, root.MinHeight);

                ApplyEdge(window, settings, DockEdge.Bottom);
                Assert.Equal(initialHorizontalWidth, root.MinWidth);

                settings.ActiveContextId = settings.Contexts[1].Id;
                window.GetSettingsService().Settings = settings;
                window.RefreshPanel();
                LayoutWindow(window);
                Assert.Equal(initialHorizontalWidth, root.MinWidth);

                ApplyEdge(window, settings, DockEdge.Top);
                Assert.Equal(initialHorizontalWidth, root.MinWidth);
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
        settings.ShowPresetQRCodeGenerator = false;
        settings.ShowPresetShowDesktop = false;
        settings.ShowPresetAppsFolder = false;
        settings.ShowPresetCopilot = false;
        settings.ShowPresetTextProcessing = false;
        settings.ShowPresetZenEditor = false;
        settings.ShowPresetIconConverter = true;
        window.GetSettingsService().Settings = settings;
    }

    private static List<CustomElement> CreateContextElements(string contextId, int count)
    {
        var elements = new List<CustomElement>();
        for (int i = 0; i < count; i++)
        {
            elements.Add(new CustomElement
            {
                Id = $"test-element-{i}",
                Name = $"Test {i}",
                ContextId = contextId,
                ActionType = nameof(ActionType.Web),
                ActionValue = "https://example.com"
            });
        }

        return elements;
    }

    private static void ApplyEdge(MainWindow window, AppSettings settings, DockEdge edge)
    {
        settings.Edge = edge;
        window.GetSettingsService().Settings = settings;
        window.UpdateOrientation(reposition: false);
        LayoutWindow(window);
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

    private static void AssertPanelToolTipPlacement(Button button, PlacementMode placement, double horizontalOffset, double verticalOffset)
    {
        Assert.Equal(0, ToolTipService.GetInitialShowDelay(button));
        Assert.Equal(placement, ToolTipService.GetPlacement(button));
        Assert.Equal(horizontalOffset, ToolTipService.GetHorizontalOffset(button));
        Assert.Equal(verticalOffset, ToolTipService.GetVerticalOffset(button));
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
