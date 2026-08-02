using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace AiteBar.Tests;

[Collection("WpfTestCollection")]
public sealed class FileSorterWindowBehaviorTests
{
    [Fact]
    public async Task Window_BuildsSingleScreenRowsWithPerFolderActions()
    {
        EnsureApplicationResources();
        await RunStaAsync(() =>
        {
            string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            var settingsService = new AppSettingsService(
                Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".config.json"),
                Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".settings.json"))
            {
                Settings = new AppSettings
                {
                    LastMultiFileSortOperation = new MultiFileSortUndoState
                    {
                        PerFolder = [CreateUndoState(desktopPath)]
                    }
                }
            };

            var window = new FileSorterWindow(settingsService);
            try
            {
                Assert.Equal(520, window.Width);
                StackPanel folderList = Assert.IsType<StackPanel>(window.FindName("FolderListPanel"));
                Grid desktopRow = Assert.IsType<Grid>(folderList.Children[0]);
                Grid downloadsRow = Assert.IsType<Grid>(folderList.Children[2]);

                Assert.Equal(4, desktopRow.ColumnDefinitions.Count);
                Assert.Equal(4, downloadsRow.ColumnDefinitions.Count);

                Button[] desktopActions = FindRowActions(desktopRow);
                Button[] downloadsActions = FindRowActions(downloadsRow);
                Assert.Equal(2, desktopActions.Length);
                Assert.Equal(2, downloadsActions.Length);
                Assert.True(desktopActions[0].IsEnabled);
                Assert.False(downloadsActions[0].IsEnabled);
                Assert.True(desktopActions[1].IsEnabled);
                Assert.Equal("\uE7A7", desktopActions[0].Content);
                Assert.Equal("\uE838", desktopActions[1].Content);
                Assert.Equal("Segoe MDL2 Assets", desktopActions[0].FontFamily.Source);

                Assert.NotNull(window.FindName("TxtSelectionCount"));
                Assert.NotNull(window.FindName("TxtOverallStatus"));
                var addButton = Assert.IsType<Button>(window.FindName("BtnAddFolder"));
                var sortButton = Assert.IsType<Button>(window.FindName("BtnSort"));
                Assert.Equal(addButton.Height, sortButton.Height);
                Assert.Equal(addButton.MinWidth, sortButton.MinWidth);
                Assert.Equal(addButton.Padding, sortButton.Padding);
                Assert.Equal(addButton.FontSize, sortButton.FontSize);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public async Task BusyState_BlocksInputWithoutApplyingDisabledVisuals()
    {
        EnsureApplicationResources();
        await RunStaAsync(() =>
        {
            string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            var settingsService = new AppSettingsService(
                Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".config.json"),
                Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".settings.json"))
            {
                Settings = new AppSettings
                {
                    LastMultiFileSortOperation = new MultiFileSortUndoState
                    {
                        PerFolder = [CreateUndoState(desktopPath)]
                    }
                }
            };

            var window = new FileSorterWindow(settingsService);
            MethodInfo? setBusy = typeof(FileSorterWindow).GetMethod(
                "SetBusy",
                BindingFlags.Instance | BindingFlags.NonPublic);
            try
            {
                Assert.NotNull(setBusy);
                var sortButton = Assert.IsType<Button>(window.FindName("BtnSort"));
                var addButton = Assert.IsType<Button>(window.FindName("BtnAddFolder"));
                StackPanel folderList = Assert.IsType<StackPanel>(window.FindName("FolderListPanel"));
                Grid desktopRow = Assert.IsType<Grid>(folderList.Children[0]);
                CheckBox desktopSwitch = Assert.Single(desktopRow.Children.OfType<CheckBox>());
                Button undoButton = FindRowActions(desktopRow)[0];

                setBusy.Invoke(window, [true]);

                Assert.True(sortButton.IsEnabled);
                Assert.True(addButton.IsEnabled);
                Assert.True(desktopSwitch.IsEnabled);
                Assert.True(undoButton.IsEnabled);
                Assert.False(sortButton.IsHitTestVisible);
                Assert.False(addButton.IsHitTestVisible);
                Assert.False(desktopSwitch.IsHitTestVisible);
                Assert.False(undoButton.IsHitTestVisible);

                setBusy.Invoke(window, [false]);
                Assert.True(sortButton.IsHitTestVisible);
                Assert.True(desktopSwitch.IsHitTestVisible);
            }
            finally
            {
                setBusy?.Invoke(window, [false]);
                window.Close();
            }
        });
    }

    private static Button[] FindRowActions(Grid row) =>
        row.Children
            .OfType<StackPanel>()
            .Where(panel => Grid.GetColumn(panel) == 3)
            .SelectMany(panel => panel.Children.OfType<Button>())
            .ToArray();

    private static FileSortUndoState CreateUndoState(string rootPath) =>
        new()
        {
            RootPath = rootPath,
            Entries =
            [
                new FileSortOperationEntry
                {
                    SourcePath = Path.Combine(rootPath, "photo.jpg"),
                    DestinationPath = Path.Combine(rootPath, "Images", "photo.jpg")
                }
            ]
        };

    private static void EnsureApplicationResources()
    {
        if (Application.ResourceAssembly == null)
        {
            Application.ResourceAssembly = typeof(App).Assembly;
        }

        if (Application.Current == null)
        {
            var app = new App();
            app.InitializeComponent();
        }
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
