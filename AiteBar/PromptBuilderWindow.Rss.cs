using System.Windows.Automation;
using System.Windows.Controls;

namespace AiteBar;

public partial class PromptBuilderWindow
{
    private sealed record RssCategory(string? Value, string Label);
    private sealed class RssSourcesDialog : DarkWindow { }

    private void BtnRssSources_Click(object sender, RoutedEventArgs e)
    {
        if (TxtEditor.IsReadOnly || TxtEditor.Visibility != Visibility.Visible) return;
        var feeds = _settingsService.Settings.RssFeeds.Where(feed => feed.IsEnabled).ToArray();
        var dialog = new RssSourcesDialog
        {
            Title = LocalizationService.Get("SettingsLists_Rss"), Owner = this,
            Width = 620, Height = 380, MinWidth = 490, MinHeight = 260,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        dialog.SetResourceReference(BackgroundProperty, "WindowBackground");
        dialog.SetResourceReference(ForegroundProperty, "PrimaryText");
        var panel = new DockPanel { Margin = new Thickness(16) };
        var categories = new ComboBox { DisplayMemberPath = nameof(RssCategory.Label), MinWidth = 145, MaxWidth = 185, Margin = new Thickness(0, 0, 6, 0) };
        AutomationProperties.SetName(categories, LocalizationService.Get("SettingsLists_Category"));
        categories.ItemsSource = new[] { new RssCategory(null, LocalizationService.Get("SettingsLists_AllCategories")) }
            .Concat(feeds.Select(feed => feed.Category).Distinct(StringComparer.CurrentCultureIgnoreCase)
                .Select(category => new RssCategory(category, category.Length == 0
                    ? LocalizationService.Get("SettingsLists_NoCategory") : category))).ToArray();
        var feedPicker = new ComboBox { DisplayMemberPath = nameof(SettingsSourceEntry.Name) };
        AutomationProperties.SetName(feedPicker, LocalizationService.Get("SettingsLists_Rss"));
        var load = new Button { Content = LocalizationService.Get("SettingsLists_LoadRss"), MinWidth = 90, Margin = new Thickness(6, 0, 0, 0) };
        var filters = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };
        DockPanel.SetDock(filters, Dock.Top);
        DockPanel.SetDock(categories, Dock.Left);
        DockPanel.SetDock(load, Dock.Right);
        filters.Children.Add(categories);
        filters.Children.Add(load);
        filters.Children.Add(feedPicker);
        panel.Children.Add(filters);
        var actions = new DockPanel { Margin = new Thickness(0, 8, 0, 0) };
        DockPanel.SetDock(actions, Dock.Bottom);
        var cancel = new Button { Content = LocalizationService.Get("Common_Cancel"), IsCancel = true, MinWidth = 90 };
        var insert = new Button { Content = LocalizationService.Get("SettingsLists_InsertRss"), MinWidth = 90, IsEnabled = false, Margin = new Thickness(6, 0, 0, 0) };
        DockPanel.SetDock(insert, Dock.Right);
        DockPanel.SetDock(cancel, Dock.Right);
        actions.Children.Add(insert);
        actions.Children.Add(cancel);
        if (_mainWindow is { } mainWindow)
        {
            var manage = new Button { Content = LocalizationService.Get("SettingsLists_ManageRss"), HorizontalAlignment = System.Windows.HorizontalAlignment.Left };
            manage.Click += async (_, _) =>
            {
                dialog.Close();
                await mainWindow.ShowAppSettingsWindow(AppSettingsSection.RssFeeds);
            };
            actions.Children.Add(manage);
        }
        var status = new TextBlock { Margin = new Thickness(0, 4, 0, 0), TextWrapping = TextWrapping.Wrap };
        DockPanel.SetDock(status, Dock.Bottom);
        AutomationProperties.SetLiveSetting(status, AutomationLiveSetting.Polite);
        var articles = new System.Windows.Controls.ListBox
        {
            DisplayMemberPath = nameof(RssFeedReader.Entry.Title), SelectionMode = System.Windows.Controls.SelectionMode.Extended
        };
        articles.SetResourceReference(BackgroundProperty, "FormInputBackground");
        articles.SetResourceReference(ForegroundProperty, "PrimaryText");
        articles.SetResourceReference(BorderBrushProperty, "FormControlBorderBrush");
        AutomationProperties.SetName(articles, LocalizationService.Get("SettingsLists_Rss"));
        panel.Children.Add(actions);
        panel.Children.Add(status);
        panel.Children.Add(articles);
        dialog.Content = panel;
        string? text = null;
        CancellationTokenSource? reading = null;

        void ResetItems()
        {
            reading?.Cancel();
            reading = null;
            articles.ItemsSource = null;
            load.IsEnabled = feedPicker.SelectedItem is SettingsSourceEntry;
            status.Text = LocalizationService.Get(load.IsEnabled ? "SettingsLists_RssReady" : "SettingsLists_NoFeeds");
        }
        categories.SelectionChanged += (_, _) =>
        {
            var category = ((RssCategory?)categories.SelectedItem)?.Value;
            feedPicker.ItemsSource = feeds.Where(feed => category is null ||
                feed.Category.Equals(category, StringComparison.CurrentCultureIgnoreCase)).ToArray();
            feedPicker.SelectedIndex = feedPicker.Items.Count > 0 ? 0 : -1;
            ResetItems();
        };
        feedPicker.SelectionChanged += (_, _) => ResetItems();
        categories.SelectedIndex = 0;
        dialog.Closed += (_, _) => reading?.Cancel();
        articles.SelectionChanged += (_, _) => insert.IsEnabled = articles.SelectedItems.Count > 0;
        insert.Click += (_, _) =>
        {
            var selected = articles.SelectedItems.Cast<RssFeedReader.Entry>().ToHashSet(ReferenceEqualityComparer.Instance);
            text = string.Join(Environment.NewLine + Environment.NewLine, articles.Items.Cast<RssFeedReader.Entry>()
                .Where(selected.Contains).Select(entry => entry.PromptText));
            dialog.DialogResult = true;
        };
        load.Click += async (_, _) =>
        {
            if (feedPicker.SelectedItem is not SettingsSourceEntry feed) return;
            ResetItems();
            using var operation = new CancellationTokenSource();
            reading = operation;
            load.IsEnabled = false;
            status.Text = LocalizationService.Get("SettingsLists_RssLoading");
            try
            {
                IReadOnlyList<RssFeedReader.Entry>? items = await RssFeedReader.ReadAsync(feed.Value, operation.Token);
                if (operation.IsCancellationRequested) return;
                articles.ItemsSource = items;
                status.Text = items switch
                {
                    null => LocalizationService.Get("SettingsLists_CheckFailed"),
                    { Count: 0 } => LocalizationService.Get("SettingsLists_RssNoItems"),
                    _ => ""
                };
            }
            catch (Exception ex) when (ex is System.Net.Http.HttpRequestException or IOException or OperationCanceledException)
            {
                if (!operation.IsCancellationRequested) status.Text = LocalizationService.Get("SettingsLists_CheckFailed");
            }
            finally
            {
                if (ReferenceEquals(reading, operation))
                {
                    reading = null;
                    if (dialog.IsVisible) load.IsEnabled = true;
                }
            }
        };
        if (dialog.ShowDialog() == true && !string.IsNullOrEmpty(text)) InsertSourceText(text);
    }
}
