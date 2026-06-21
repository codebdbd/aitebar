using System;
using System.Linq;
using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace AiteBar
{
    [SupportedOSPlatform("windows6.1")]
    public partial class ClipboardManagerWindow : DarkWindow
    {
        private readonly ClipboardHistoryService _historyService;
        private readonly DispatcherTimer _refreshTimer;
        private string _lastRenderedSignature = string.Empty;

        public ClipboardManagerWindow(ClipboardHistoryService historyService)
        {
            InitializeComponent();
            _historyService = historyService;
            _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
            _refreshTimer.Tick += (_, _) =>
            {
                _refreshTimer.Stop();
                UpdateEntriesList();
            };
            UpdateSearchPlaceholder();
            UpdateEntriesList(force: true);
        }

        public void ShowNearPanel(AppSettingsService settingsService)
        {
            var settings = settingsService.Settings;
            var screens = System.Windows.Forms.Screen.AllScreens;
            var screen = settings.MonitorIndex >= 0 && settings.MonitorIndex < screens.Length
                ? screens[settings.MonitorIndex]
                : System.Windows.Forms.Screen.PrimaryScreen;
            var work = screen?.WorkingArea ?? System.Windows.Forms.Screen.PrimaryScreen?.WorkingArea ?? new System.Drawing.Rectangle(0, 0, 1280, 720);

            var (_, _, shownX, shownY) = QuickNoteLayoutHelper.GetSlideCoordinates(settings.Edge, work, Width, Height);
            Left = shownX;
            Top = shownY;
            Show();
            Activate();
            TxtSearch.Focus();
        }

        protected override void OnInitialized(EventArgs e)
        {
            base.OnInitialized(e);
            if (_historyService != null)
            {
                _historyService.HistoryChanged += OnHistoryChanged;
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            if (_historyService != null)
            {
                _historyService.HistoryChanged -= OnHistoryChanged;
            }
        }

        private void OnHistoryChanged(object? sender, EventArgs e)
        {
            _refreshTimer.Stop();
            _refreshTimer.Start();
        }

        private void UpdateEntriesList(bool force = false)
        {
            var searchText = TxtSearch?.Text.ToLowerInvariant() ?? string.Empty;
            var filteredEntries = _historyService.Entries.Where(entry => 
                entry.Text.ToLowerInvariant().Contains(searchText) || 
                (entry.IsImage && "image".ToLowerInvariant().Contains(searchText))).ToList();
            string signature = BuildEntriesSignature(searchText, filteredEntries);
            if (!force && string.Equals(signature, _lastRenderedSignature, StringComparison.Ordinal))
            {
                return;
            }

            _lastRenderedSignature = signature;
            EntriesPanel.Children.Clear();

            if (filteredEntries.Count == 0)
            {
                TxtEmptyHint.Visibility = Visibility.Visible;
            }
            else
            {
                TxtEmptyHint.Visibility = Visibility.Collapsed;

                foreach (var entry in filteredEntries)
                {
                    var entryBorder = new Border
                    {
                        Style = (Style)FindResource("EntryItemStyle"),
                        Tag = entry
                    };

                    var grid = new Grid();
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                    // Icon
                    var iconTextBlock = new TextBlock
                    {
                        Text = entry.IsImage ? "📷" : "📝",
                        FontSize = 18,
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(0, 0, 10, 0)
                    };
                    Grid.SetColumn(iconTextBlock, 0);
                    grid.Children.Add(iconTextBlock);

                    // Content
                    var contentStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                    var displayText = new TextBlock
                    {
                        Text = entry.DisplayText,
                        FontSize = 12,
                        Foreground = System.Windows.Media.Brushes.White,
                        TextWrapping = TextWrapping.Wrap,
                        MaxWidth = 240
                    };
                    var timestampText = new TextBlock
                    {
                        Text = entry.Timestamp.ToString("HH:mm"),
                        FontSize = 10,
                        Foreground = (System.Windows.Media.Brush)FindResource("MutedText"),
                        Margin = new Thickness(0, 3, 0, 0)
                    };
                    contentStack.Children.Add(displayText);
                    contentStack.Children.Add(timestampText);
                    Grid.SetColumn(contentStack, 1);
                    grid.Children.Add(contentStack);

                    // Copy button
                    var copyButton = new Button
                    {
                        Content = "✓",
                        Style = (Style)FindResource("HeaderButtonStyle"),
                        ToolTip = LocalizationService.Get("ClipboardManager_Copy"),
                        Margin = new Thickness(5, 0, 0, 0)
                    };
                    copyButton.Click += (s, e) => CopyEntry(entry);
                    Grid.SetColumn(copyButton, 2);
                    grid.Children.Add(copyButton);

                    entryBorder.Child = grid;
                    entryBorder.MouseLeftButtonDown += (s, e) => CopyEntry(entry);
                    EntriesPanel.Children.Add(entryBorder);
                }
            }

            TxtStatus.Text = filteredEntries.Count > 0 
                ? LocalizationService.Format("ClipboardManager_Status", filteredEntries.Count) 
                : "";
        }


        private static string BuildEntriesSignature(string searchText, System.Collections.Generic.IReadOnlyList<ClipboardHistoryEntry> entries)
        {
            return string.Join("|", entries.Select(entry => $"{searchText}:{entry.Timestamp.Ticks}:{entry.Text.Length}:{entry.ImageBytes?.Length ?? 0}"));
        }
        private void CopyEntry(ClipboardHistoryEntry entry)
        {
            TxtStatus.Text = _historyService.CopyEntryToClipboard(entry)
                ? LocalizationService.Get("ClipboardManager_Copied")
                : LocalizationService.Get("ClipboardManager_CopyFailed");
        }

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateSearchPlaceholder();
            UpdateEntriesList(force: true);
        }

        private void UpdateSearchPlaceholder()
        {
            TxtSearchPlaceholder.Visibility = string.IsNullOrEmpty(TxtSearch.Text) 
                ? Visibility.Visible 
                : Visibility.Collapsed;
        }

        private void BtnClearAll_Click(object sender, RoutedEventArgs e)
        {
            _historyService.ClearHistory();
            TxtStatus.Text = LocalizationService.Get("ClipboardManager_Cleared");
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Close();
                e.Handled = true;
            }
        }
    }
}
