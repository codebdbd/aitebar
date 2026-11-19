namespace AiteBar;

public partial class PromptBuilderWindow
{
    private void BtnSavedPrompts_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button) return;
        var menu = new ContextMenu { PlacementTarget = button, Placement = PlacementMode.Bottom };
        foreach (var group in _settingsService.Settings.SavedPrompts.GroupBy(entry => entry.Category, StringComparer.CurrentCultureIgnoreCase))
        {
            var category = new MenuItem
            {
                Header = string.IsNullOrWhiteSpace(group.Key) ? LocalizationService.Get("SettingsLists_NoCategory") : group.Key
            };
            foreach (SettingsSourceEntry prompt in group)
            {
                var entry = new MenuItem { Header = prompt.Name };
                entry.Click += (_, _) => InsertSourceText(prompt.Value);
                category.Items.Add(entry);
            }
            menu.Items.Add(category);
        }
        if (_mainWindow is { } mainWindow)
        {
            if (menu.Items.Count > 0) menu.Items.Add(new Separator());
            var manage = new MenuItem { Header = LocalizationService.Get("SettingsLists_ManagePrompts") };
            manage.Click += async (_, _) => await mainWindow.ShowAppSettingsWindow(AppSettingsSection.SavedPrompts);
            menu.Items.Add(manage);
        }
        if (menu.Items.Count == 0) return;
        menu.IsOpen = true;
    }

    private void InsertSourceText(string text)
    {
        if (TxtEditor.IsReadOnly || TxtEditor.Visibility != Visibility.Visible) return;
        TxtEditor.Focus();
        int start = TxtEditor.SelectionStart;
        TxtEditor.SelectedText = text;
        TxtEditor.CaretIndex = start + text.Length;
    }
}
