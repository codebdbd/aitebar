using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace AiteBar;

[SupportedOSPlatform("windows6.1")]
public partial class ZenEditorDocumentPicker : DarkWindow
{
    private readonly List<PickerItem> _items;
    private readonly DispatcherTimer _prefixTimer;
    private readonly bool _restoreMode;
    private string _prefix = string.Empty;

    public ZenEditorDocumentPicker(
        IReadOnlyList<ZenEditorDocumentSummary> documents,
        ZenEditorTheme theme,
        bool restoreMode = false)
    {
        InitializeComponent();
        _restoreMode = restoreMode;
        string titleKey = restoreMode
            ? "ZenEditor_RecentlyDeleted"
            : "ZenEditor_OpenDocument";
        string title = LocalizationService.Get(titleKey);
        Title = title;
        PickerTitle.Text = title;
        System.Windows.Automation.AutomationProperties.SetName(DocumentList, title);
        _items = documents.Select(summary => new PickerItem(
            summary.Id,
            summary.ModifiedUtc.ToLocalTime().ToString("dd.MM.yyyy HH:mm:ss"),
            summary.Title,
            summary.IsCurrent ? "•" : string.Empty)).ToList();
        DocumentList.ItemsSource = _items;
        DocumentList.SelectedIndex = _items.Count > 0 ? 0 : -1;
        _prefixTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(900) };
        _prefixTimer.Tick += (_, _) =>
        {
            _prefixTimer.Stop();
            _prefix = string.Empty;
        };
        ApplyTheme(theme);
    }

    public Guid? SelectedDocumentId { get; private set; }
    public bool DeleteRequested { get; private set; }
    public bool RestoreRequested { get; private set; }

    private void ApplyTheme(ZenEditorTheme theme)
    {
        Brush background = BrushFrom(theme.Background);
        Brush text = BrushFrom(theme.Text);
        Brush separator = BrushFrom(theme.Separator);
        Background = background;
        Foreground = text;
        Frame.BorderBrush = separator;
        DocumentList.Foreground = text;
        FontFamily = ZenEditorWindow.CreateThemeFontFamily(theme);
        FontSize = Math.Max(13, theme.FontSize - 5);
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape:
                DialogResult = false;
                e.Handled = true;
                break;
            case Key.Enter:
                AcceptSelected();
                e.Handled = true;
                break;
            case Key.Delete:
                if (!_restoreMode && DocumentList.SelectedItem is PickerItem selected)
                {
                    SelectedDocumentId = selected.Id;
                    DeleteRequested = true;
                    DialogResult = true;
                }
                e.Handled = true;
                break;
            case Key.Home:
                DocumentList.SelectedIndex = _items.Count > 0 ? 0 : -1;
                DocumentList.ScrollIntoView(DocumentList.SelectedItem);
                e.Handled = true;
                break;
            case Key.End:
                DocumentList.SelectedIndex = _items.Count - 1;
                DocumentList.ScrollIntoView(DocumentList.SelectedItem);
                e.Handled = true;
                break;
        }
    }

    private void Window_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(e.Text) || char.IsControl(e.Text[0]))
        {
            return;
        }

        _prefix += e.Text;
        _prefixTimer.Stop();
        _prefixTimer.Start();
        int index = _items.FindIndex(item =>
            item.Title.StartsWith(_prefix, StringComparison.CurrentCultureIgnoreCase));
        if (index >= 0)
        {
            DocumentList.SelectedIndex = index;
            DocumentList.ScrollIntoView(DocumentList.SelectedItem);
        }

        e.Handled = true;
    }

    private void DocumentList_MouseDoubleClick(object sender, MouseButtonEventArgs e) => AcceptSelected();

    private void AcceptSelected()
    {
        if (DocumentList.SelectedItem is not PickerItem selected)
        {
            return;
        }

        SelectedDocumentId = selected.Id;
        RestoreRequested = _restoreMode;
        DialogResult = true;
    }

    private static SolidColorBrush BrushFrom(string color) =>
        new((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(color));

    private sealed record PickerItem(Guid Id, string Modified, string Title, string CurrentMarker);
}
