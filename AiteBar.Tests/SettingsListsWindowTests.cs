using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace AiteBar.Tests;

[Collection("WpfTestCollection")]
public sealed class SettingsListsWindowTests
{
    [Fact]
    public Task ImportSkipsExistingFeedUntilExplicitUpdateAndKeepsItsIdAndEnabledState()
    {
        return QuickNoteWindowCloseTests.RunStaAsync(async () =>
        {
            string directory = Path.Combine(Path.GetTempPath(), "AiteBar-SettingsLists-" + Guid.NewGuid().ToString("N"));
            PathHelper.SetAppDataFolderOverride(directory);
            var original = new SettingsSourceEntry { Name = "Updates", Value = "https://example.org/rss", IsEnabled = false };
            try
            {
                var service = new AppSettingsService
                {
                    Settings = new AppSettings { ClipboardManagerPersistHistory = false, RssFeeds = [original] }
                };
                var mainWindow = new MainWindow(service);
                try
                {
                    var settings = new AppSettingsWindow(mainWindow, AppSettingsSection.RssFeeds);
                    try
                    {
                        settings.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
                        var picker = Assert.IsType<ComboBox>(settings.FindName("SettingsListPicker"));
                        Assert.Equal((int)SettingsListKind.Rss, picker.SelectedIndex);
                        string[] values = ["New title", original.Value, "IT"];
                        var existing = Assert.IsType<SettingsSourceEntry>(settings.FindImportTarget(SettingsListKind.Rss, values));
                        Assert.True(settings.IsValidImport(SettingsListKind.Rss, values, existing));
                        Assert.False(await settings.ImportListRowAsync(SettingsListKind.Rss, values));
                        await settings.UpdateImportedRowAsync(SettingsListKind.Rss, values, existing);
                        var updated = Assert.IsType<SettingsSourceEntry>(settings.FindImportTarget(SettingsListKind.Rss, values));
                        Assert.Equal(original.Id, updated.Id);
                        Assert.Equal("New title", updated.Name);
                        Assert.Equal("IT", updated.Category);
                        Assert.False(updated.IsEnabled);
                        Assert.Equal(original, Assert.Single(service.Settings.RssFeeds));
                    }
                    finally { settings.Close(); }
                }
                finally { mainWindow.Close(); }
            }
            finally
            {
                PathHelper.ClearAppDataFolderOverride();
                Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
                if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
            }
        });
    }

    [Fact]
    public Task PickerSwitchesBetweenAllSevenListsAndPreservesTheIptvDeepLink()
    {
        return QuickNoteWindowCloseTests.RunStaAsync(() =>
        {
            string directory = Path.Combine(Path.GetTempPath(), "AiteBar-SettingsLists-" + Guid.NewGuid().ToString("N"));
            PathHelper.SetAppDataFolderOverride(directory);
            try
            {
                var service = new AppSettingsService
                {
                    Settings = new AppSettings
                    {
                        ClipboardManagerPersistHistory = false,
                        RssFeeds = [new() { Name = "News", Value = "https://example.org/rss" }]
                    }
                };
                var mainWindow = new MainWindow(service);
                try
                {
                    var settings = new AppSettingsWindow(mainWindow, AppSettingsSection.Iptv);
                    try
                    {
                        settings.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
                        var picker = Assert.IsType<ComboBox>(settings.FindName("SettingsListPicker"));
                        Assert.Equal(7, picker.Items.Count);
                        Assert.Equal((int)SettingsListKind.Iptv, picker.SelectedIndex);
                        Assert.Equal(Visibility.Visible, Assert.IsType<StackPanel>(settings.FindName("IptvListBody")).Visibility);
                        picker.SelectedIndex = (int)SettingsListKind.Rss;
                        var list = Assert.IsType<StackPanel>(settings.FindName("AdditionalListBody"));
                        Assert.Equal(Visibility.Visible, list.Visibility);
                        Assert.Single(list.Children);
                        Assert.Equal(Visibility.Collapsed, Assert.IsType<StackPanel>(settings.FindName("IptvListBody")).Visibility);
                        var row = Assert.IsType<Grid>(list.Children[0]);
                        CheckBox toggle = Assert.Single(row.Children.OfType<CheckBox>());
                        toggle.IsChecked = false;
                        toggle.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                        var updatedRow = Assert.IsType<Grid>(list.Children[0]);
                        Assert.NotSame(row, updatedRow);
                        Assert.False(Assert.Single(updatedRow.Children.OfType<CheckBox>()).IsChecked);

                        var navigation = Assert.IsType<ListBox>(settings.FindName("SettingsNavigationList"));
                        var general = Assert.IsType<Border>(settings.FindName("GeneralSettingsSection"));
                        var resources = Assert.IsType<Border>(settings.FindName("AiProvidersSettingsSection"));
                        var about = Assert.IsType<Border>(settings.FindName("AboutSettingsSection"));
                        Assert.Equal(Visibility.Visible, resources.Visibility);
                        Assert.Equal(Visibility.Collapsed, about.Visibility);
                        navigation.SelectedIndex = 0;
                        Assert.Equal(Visibility.Visible, general.Visibility);
                        Assert.Equal(Visibility.Collapsed, resources.Visibility);
                        navigation.SelectedIndex = 4;
                        Assert.Equal((int)SettingsListKind.Rss, picker.SelectedIndex);
                        Assert.False(Assert.Single(updatedRow.Children.OfType<CheckBox>()).IsChecked);
                        settings.NavigateToSection(AppSettingsSection.SavedPrompts);
                        Assert.Equal((int)SettingsListKind.Prompts, picker.SelectedIndex);
                        settings.NavigateToSection(AppSettingsSection.About);
                        Assert.Equal(Visibility.Collapsed, resources.Visibility);
                        Assert.Equal(Visibility.Visible, about.Visibility);
                    }
                    finally
                    {
                        settings.Close();
                    }
                }
                finally
                {
                    mainWindow.Close();
                }
            }
            finally
            {
                PathHelper.ClearAppDataFolderOverride();
                Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
                if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
            }
            return Task.CompletedTask;
        });
    }
}
