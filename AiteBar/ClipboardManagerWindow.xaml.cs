using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace AiteBar
{
    public enum ClipboardManagerFilter
    {
        All,
        Pinned,
        Text,
        Images
    }

    [SupportedOSPlatform("windows6.1")]
    public partial class ClipboardManagerWindow : DarkWindow
    {
        private readonly ClipboardHistoryService _historyService;
        private readonly DispatcherTimer _refreshTimer;
        private string _lastRenderedSignature = string.Empty;
        private ClipboardManagerFilter _activeFilter = ClipboardManagerFilter.All;

        public ClipboardManagerWindow(ClipboardHistoryService historyService)
        {
            _historyService = historyService ?? throw new ArgumentNullException(nameof(historyService));
            InitializeComponent();
            _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
            _refreshTimer.Tick += (_, _) =>
            {
                _refreshTimer.Stop();
                UpdateEntriesList();
            };
            UpdateSearchPlaceholder();
            UpdateFilterButtons();
            UpdateEntriesList(force: true);
        }

        public void ShowNearPanel(AppSettingsService settingsService)
        {
            AppSettings settings = settingsService.Settings;
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
            _historyService.HistoryChanged += OnHistoryChanged;
        }

        protected override void OnClosed(EventArgs e)
        {
            _historyService.HistoryChanged -= OnHistoryChanged;
            base.OnClosed(e);
        }

        protected override void OnLocalizationChanged()
        {
            _lastRenderedSignature = string.Empty;
            UpdateFilterButtons();
            UpdateEntriesList(force: true);
        }

        private void OnHistoryChanged(object? sender, EventArgs e)
        {
            _refreshTimer.Stop();
            _refreshTimer.Start();
        }

        private void UpdateEntriesList(bool force = false)
        {
            string searchText = TxtSearch?.Text ?? string.Empty;
            List<ClipboardHistoryEntry> filteredEntries = GetFilteredEntries(searchText);
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
                EntriesPanel.Children.Add(TxtEmptyHint);
            }
            else
            {
                TxtEmptyHint.Visibility = Visibility.Collapsed;
                foreach (ClipboardHistoryEntry entry in filteredEntries)
                {
                    EntriesPanel.Children.Add(CreateEntryCard(entry));
                }
            }

            TxtStatus.Text = LocalizationService.Format(
                "ClipboardManager_StatusDetailed",
                filteredEntries.Count,
                _historyService.Entries.Count,
                _historyService.Entries.Count(entry => entry.IsPinned),
                _historyService.PersistHistory
                    ? LocalizationService.Get("ClipboardManager_StatusPersistent")
                    : LocalizationService.Get("ClipboardManager_StatusSessionOnly"));
        }

        private List<ClipboardHistoryEntry> GetFilteredEntries(string searchText)
        {
            string query = searchText.Trim();

            return _historyService.Entries
                .Where(MatchesActiveFilter)
                .Where(entry => MatchesSearch(entry, query))
                .ToList();
        }

        private bool MatchesActiveFilter(ClipboardHistoryEntry entry)
        {
            return _activeFilter switch
            {
                ClipboardManagerFilter.Pinned => entry.IsPinned,
                ClipboardManagerFilter.Text => !entry.IsImage,
                ClipboardManagerFilter.Images => entry.IsImage,
                _ => true
            };
        }

        private static bool MatchesSearch(ClipboardHistoryEntry entry, string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return true;
            }

            return entry.Text.Contains(query, StringComparison.OrdinalIgnoreCase)
                || (entry.IsImage && LocalizationService.Get("ClipboardManager_ImageLabel").Contains(query, StringComparison.OrdinalIgnoreCase))
                || (entry.IsPinned && LocalizationService.Get("ClipboardManager_PinnedBadge").Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        private Border CreateEntryCard(ClipboardHistoryEntry entry)
        {
            var entryBorder = new Border
            {
                Style = (Style)FindResource("EntryItemStyle"),
                BorderBrush = entry.IsPinned
                    ? new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#D4A72C"))
                    : (Brush)FindResource("FormControlBorderBrush"),
                Tag = entry
            };

            var root = new StackPanel();

            var header = new Grid();
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var icon = new TextBlock
            {
                Text = entry.IsImage ? "\uD83D\uDDBC" : "\uD83D\uDCDD",
                FontSize = 18,
                Margin = new Thickness(0, 0, 10, 0),
                VerticalAlignment = VerticalAlignment.Top
            };
            Grid.SetColumn(icon, 0);
            header.Children.Add(icon);

            var textStack = new StackPanel();
            var title = new TextBlock
            {
                Text = entry.IsImage ? LocalizationService.Get("ClipboardManager_ImageLabel") : entry.DisplayText,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White,
                TextWrapping = TextWrapping.Wrap
            };
            var summary = new TextBlock
            {
                Text = BuildEntrySummary(entry),
                FontSize = 10,
                Foreground = (Brush)FindResource("MutedText"),
                Margin = new Thickness(0, 4, 0, 0)
            };
            textStack.Children.Add(title);
            textStack.Children.Add(summary);
            Grid.SetColumn(textStack, 1);
            header.Children.Add(textStack);

            if (entry.IsPinned)
            {
                var pinned = new Border
                {
                    Background = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#6A5312")),
                    BorderBrush = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#D4A72C")),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(10),
                    Padding = new Thickness(8, 2, 8, 2),
                    Child = new TextBlock
                    {
                        Text = LocalizationService.Get("ClipboardManager_PinnedBadge"),
                        FontSize = 10,
                        Foreground = Brushes.White
                    }
                };
                Grid.SetColumn(pinned, 2);
                header.Children.Add(pinned);
            }

            root.Children.Add(header);

            var actions = new WrapPanel
            {
                Margin = new Thickness(0, 10, 0, 0)
            };
            actions.Children.Add(CreateActionButton(LocalizationService.Get("ClipboardManager_Copy"), () => CopyEntry(entry)));
            if (!entry.IsImage)
            {
                actions.Children.Add(CreateActionButton(LocalizationService.Get("ClipboardManager_CopySingleLine"), () => CopyEntry(entry, ClipboardCopyMode.SingleLine)));
            }

            actions.Children.Add(CreateActionButton(
                entry.IsPinned
                    ? LocalizationService.Get("ClipboardManager_Unpin")
                    : LocalizationService.Get("ClipboardManager_Pin"),
                () => TogglePin(entry)));
            actions.Children.Add(CreateActionButton(LocalizationService.Get("ClipboardManager_Delete"), () => DeleteEntry(entry)));

            root.Children.Add(actions);

            entryBorder.Child = root;
            entryBorder.MouseLeftButtonDown += (_, _) => CopyEntry(entry);
            return entryBorder;
        }

        private Button CreateActionButton(string text, Action onClick)
        {
            var button = new Button
            {
                Content = text,
                Style = (Style)FindResource("ClipboardActionButtonStyle")
            };
            button.Click += (_, _) => onClick();
            return button;
        }

        private string BuildEntrySummary(ClipboardHistoryEntry entry)
        {
            if (entry.IsImage)
            {
                int kilobytes = Math.Max(1, (entry.ImageBytes?.Length ?? 0) / 1024);
                return LocalizationService.Format(
                    "ClipboardManager_ImageSummary",
                    kilobytes,
                    entry.Timestamp.ToString("g", CultureInfo.CurrentCulture));
            }

            return LocalizationService.Format(
                "ClipboardManager_TextSummary",
                entry.Text.Length,
                ClipboardTextTransforms.CountLines(entry.Text),
                entry.Timestamp.ToString("g", CultureInfo.CurrentCulture));
        }

        private string BuildEntriesSignature(string searchText, IReadOnlyList<ClipboardHistoryEntry> entries)
        {
            return string.Join("|", entries.Select(entry =>
                $"{_activeFilter}:{searchText}:{entry.Id}:{entry.IsPinned}:{entry.Timestamp.Ticks}:{entry.Text.Length}:{entry.ImageBytes?.Length ?? 0}:{_historyService.PersistHistory}"));
        }

        private void CopyEntry(ClipboardHistoryEntry entry, ClipboardCopyMode mode = ClipboardCopyMode.Original)
        {
            bool success = _historyService.CopyEntryToClipboard(entry, mode);
            TxtStatus.Text = success
                ? mode == ClipboardCopyMode.SingleLine
                    ? LocalizationService.Get("ClipboardManager_CopiedSingleLine")
                    : LocalizationService.Get("ClipboardManager_Copied")
                : LocalizationService.Get("ClipboardManager_CopyFailed");
        }

        private void TogglePin(ClipboardHistoryEntry entry)
        {
            if (_historyService.TogglePin(entry.Id))
            {
                TxtStatus.Text = entry.IsPinned
                    ? LocalizationService.Get("ClipboardManager_Unpinned")
                    : LocalizationService.Get("ClipboardManager_Pinned");
            }
        }

        private void DeleteEntry(ClipboardHistoryEntry entry)
        {
            if (_historyService.DeleteEntry(entry.Id))
            {
                TxtStatus.Text = LocalizationService.Get("ClipboardManager_Deleted");
            }
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

        private void SetFilter(ClipboardManagerFilter filter)
        {
            _activeFilter = filter;
            UpdateFilterButtons();
            UpdateEntriesList(force: true);
        }

        private void UpdateFilterButtons()
        {
            ApplyFilterState(BtnFilterAll, _activeFilter == ClipboardManagerFilter.All);
            ApplyFilterState(BtnFilterPinned, _activeFilter == ClipboardManagerFilter.Pinned);
            ApplyFilterState(BtnFilterText, _activeFilter == ClipboardManagerFilter.Text);
            ApplyFilterState(BtnFilterImages, _activeFilter == ClipboardManagerFilter.Images);
        }

        private static void ApplyFilterState(Button button, bool active)
        {
            button.Opacity = active ? 1.0 : 0.72;
            button.FontWeight = active ? FontWeights.SemiBold : FontWeights.Normal;
        }

        private bool Confirm(string messageKey)
        {
            return new DarkDialog(LocalizationService.Get(messageKey), isConfirm: true)
            {
                Owner = this
            }.ShowDialog() == true;
        }

        private void BtnFilterAll_Click(object sender, RoutedEventArgs e) => SetFilter(ClipboardManagerFilter.All);
        private void BtnFilterPinned_Click(object sender, RoutedEventArgs e) => SetFilter(ClipboardManagerFilter.Pinned);
        private void BtnFilterText_Click(object sender, RoutedEventArgs e) => SetFilter(ClipboardManagerFilter.Text);
        private void BtnFilterImages_Click(object sender, RoutedEventArgs e) => SetFilter(ClipboardManagerFilter.Images);

        private void BtnClearHistory_Click(object sender, RoutedEventArgs e)
        {
            if (!Confirm("ClipboardManager_ClearHistoryConfirm"))
            {
                return;
            }

            _historyService.ClearUnpinnedHistory();
            TxtStatus.Text = LocalizationService.Get("ClipboardManager_Cleared");
        }

        private void BtnWipeAll_Click(object sender, RoutedEventArgs e)
        {
            if (!Confirm("ClipboardManager_WipeAllConfirm"))
            {
                return;
            }

            _historyService.ClearAllHistory();
            TxtStatus.Text = LocalizationService.Get("ClipboardManager_WipedAll");
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
