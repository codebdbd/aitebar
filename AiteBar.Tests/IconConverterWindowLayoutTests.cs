using System;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Xml.Linq;
using AiteBar;

namespace AiteBar.Tests;

[Collection("WpfTestCollection")]
public sealed class IconConverterWindowLayoutTests : IDisposable
{
    private readonly string _tempDir;

    public IconConverterWindowLayoutTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "AiteBarTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_tempDir, recursive: true);
        }
        catch
        {
        }
    }

    [Fact]
    public void SizeCheckboxes_AreWiredToRefreshPreview()
    {
        string xamlPath = Path.Combine(FindRepoRoot(), "AiteBar", "IconConverterWindow.xaml");
        XDocument document = XDocument.Load(xamlPath);
        string[] checkboxNames = ["Chk16", "Chk20", "Chk24", "Chk32", "Chk40", "Chk48", "Chk64", "Chk128", "Chk256"];

        foreach (string checkboxName in checkboxNames)
        {
            XElement checkbox = Assert.Single(document.Descendants(),
                element => string.Equals(element.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value, checkboxName, StringComparison.Ordinal));

            Assert.Equal("Options_Changed", checkbox.Attribute("Click")?.Value);
        }
    }

    [Fact]
    public async Task Window_MinimumSize_DoesNotClipCriticalControlsInRussian()
    {
        await RunStaAsync(() =>
        {
            EnsureApplicationResources();
            LocalizationService.ApplyCulture("ru");

            var settingsService = new AppSettingsService(
                Path.Combine(_tempDir, "elements.json"),
                Path.Combine(_tempDir, "settings.json"));
            var window = new IconConverterWindow(settingsService)
            {
                Width = 640,
                Height = 500
            };

            try
            {
                window.Measure(new Size(window.Width, window.Height));
                window.Arrange(new Rect(0, 0, window.Width, window.Height));
                window.UpdateLayout();

                var root = Assert.IsAssignableFrom<FrameworkElement>(window.Content);
                AssertWithinRoot(window, root, "BtnSave");
                AssertWithinRoot(window, root, "TxtStatus");
                AssertWithinRoot(window, root, "Chk256");
                AssertWithinRoot(window, root, "RbTransparent");
                AssertWithinRoot(window, root, "RbSolid");
                AssertWithinRoot(window, root, "TxtBackgroundColor");
                Assert.False(Assert.IsType<TextBox>(window.FindName("TxtBackgroundColor")).IsEnabled);

                Assert.IsType<RadioButton>(window.FindName("RbSolid")).IsChecked = true;
                window.UpdateLayout();
                Assert.True(Assert.IsType<TextBox>(window.FindName("TxtBackgroundColor")).IsEnabled);

                AssertButtonsHaveEnoughSpace(window);
            }
            finally
            {
                window.Close();
                LocalizationService.ApplyCulture(CultureInfo.CurrentUICulture.Name);
            }
        });
    }

    private static void AssertWithinRoot(Window window, FrameworkElement root, string elementName)
    {
        var element = Assert.IsAssignableFrom<FrameworkElement>(window.FindName(elementName));
        Rect bounds = element.TransformToAncestor(root)
            .TransformBounds(new Rect(0, 0, element.ActualWidth, element.ActualHeight));

        Assert.True(bounds.Left >= -0.5, $"{elementName} left is outside the content root: {bounds.Left}");
        Assert.True(bounds.Top >= -0.5, $"{elementName} top is outside the content root: {bounds.Top}");
        Assert.True(bounds.Right <= root.ActualWidth + 0.5, $"{elementName} right is outside the content root: {bounds.Right} > {root.ActualWidth}");
        Assert.True(bounds.Bottom <= root.ActualHeight + 0.5, $"{elementName} bottom is outside the content root: {bounds.Bottom} > {root.ActualHeight}");
    }

    private static void AssertButtonsHaveEnoughSpace(DependencyObject root)
    {
        foreach (Button button in FindVisualChildren<Button>(root))
        {
            if (button.ActualWidth <= 0 || button.ActualHeight <= 0)
            {
                continue;
            }

            button.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Assert.True(button.DesiredSize.Width <= button.ActualWidth + 0.5,
                $"Button '{button.Content}' is clipped horizontally: desired {button.DesiredSize.Width}, actual {button.ActualWidth}.");
            Assert.True(button.DesiredSize.Height <= button.ActualHeight + 0.5,
                $"Button '{button.Content}' is clipped vertically: desired {button.DesiredSize.Height}, actual {button.ActualHeight}.");
        }
    }

    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject root)
        where T : DependencyObject
    {
        int count = System.Windows.Media.VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < count; i++)
        {
            DependencyObject child = System.Windows.Media.VisualTreeHelper.GetChild(root, i);
            if (child is T match)
            {
                yield return match;
            }

            foreach (T descendant in FindVisualChildren<T>(child))
            {
                yield return descendant;
            }
        }
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

    private static string FindRepoRoot()
    {
        string? current = AppContext.BaseDirectory;

        while (!string.IsNullOrEmpty(current))
        {
            if (File.Exists(Path.Combine(current, "AiteBar.sln")))
            {
                return current;
            }

            current = Directory.GetParent(current)?.FullName;
        }

        throw new DirectoryNotFoundException("Repository root with AiteBar.sln was not found.");
    }
}
