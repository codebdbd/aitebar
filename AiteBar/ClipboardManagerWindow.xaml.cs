using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
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
        private readonly DispatcherTimer _copiedBadgeTimer;
        private string _lastRenderedSignature = string.Empty;
        private ClipboardManagerFilter _activeFilter = ClipboardManagerFilter.All;
        private string? _selectedEntryId;
        private string? _copiedEntryId;

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
            _copiedBadgeTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(900) };
            _copiedBadgeTimer.Tick += (_, _) =>
            {
                _copiedBadgeTimer.Stop();
                _copiedEntryId = null;
                UpdateEntriesList(force: true);
            };
            UpdateSearchPlaceholder();
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
            UpdateEntriesList(force: true);
        }

        private void OnHistoryChanged(object? sender, EventArgs e)
        {
            _refreshTimer.Stop();
            _refreshTimer.Start();
        }

        private void UpdateEntriesList(bool force = false)
        {
            ListBox entriesList = GetEntriesList();
            string searchText = TxtSearch?.Text ?? string.Empty;
            List<ClipboardHistoryEntry> filteredEntries = GetFilteredEntries(searchText);
            string signature = BuildEntriesSignature(searchText, filteredEntries);
            if (!force && string.Equals(signature, _lastRenderedSignature, StringComparison.Ordinal))
            {
                return;
            }

            _lastRenderedSignature = signature;
            List<ClipboardHistoryEntryViewModel> items = filteredEntries.Select(CreateViewModel).ToList();
            entriesList.ItemsSource = items;

            if (items.Count == 0)
            {
                TxtEmptyHint.Visibility = Visibility.Visible;
                entriesList.SelectedItem = null;
                _selectedEntryId = null;
            }
            else
            {
                TxtEmptyHint.Visibility = Visibility.Collapsed;
                ClipboardHistoryEntryViewModel? selectedItem = items.FirstOrDefault(item => item.Id == _selectedEntryId) ?? items[0];
                entriesList.SelectedItem = selectedItem;
                _selectedEntryId = selectedItem.Id;
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

        private ClipboardHistoryEntryViewModel CreateViewModel(ClipboardHistoryEntry entry)
        {
            return new ClipboardHistoryEntryViewModel
            {
                Id = entry.Id,
                Entry = entry,
                IsImage = entry.IsImage,
                IsPinned = entry.IsPinned,
                Title = entry.IsImage ? LocalizationService.Get("ClipboardManager_ImageLabel") : entry.DisplayText,
                Summary = BuildEntrySummary(entry),
                PreviewImage = TryCreateBitmap(entry.ImageBytes),
                ImageLabel = LocalizationService.Get("ClipboardManager_ImageLabel"),
                CopyLabel = LocalizationService.Get("ClipboardManager_Copy"),
                CopiedLabel = LocalizationService.Get("ClipboardManager_Copied"),
                PinLabel = entry.IsPinned
                    ? LocalizationService.Get("ClipboardManager_Unpin")
                    : LocalizationService.Get("ClipboardManager_Pin"),
                DeleteLabel = LocalizationService.Get("ClipboardManager_Delete"),
                ActionsMargin = entry.IsImage ? new Thickness(0, 10, 0, 0) : new Thickness(50, 10, 0, 0),
                IsRecentlyCopied = string.Equals(_copiedEntryId, entry.Id, StringComparison.Ordinal)
            };
        }

        private static BitmapSource? TryCreateBitmap(byte[]? imageBytes)
        {
            if (imageBytes == null || imageBytes.Length == 0)
            {
                return null;
            }

            try
            {
                using var stream = new MemoryStream(imageBytes);
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.StreamSource = stream;
                bitmap.DecodePixelWidth = 176;
                bitmap.EndInit();
                bitmap.Freeze();
                return bitmap;
            }
            catch
            {
                return null;
            }
        }

        private static bool IsInteractiveChildClick(DependencyObject? source)
        {
            DependencyObject? current = source;
            while (current != null)
            {
                if (current is Button)
                {
                    return true;
                }

                current = VisualTreeHelper.GetParent(current);
            }

            return false;
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
            if (success)
            {
                _copiedEntryId = entry.Id;
                _copiedBadgeTimer.Stop();
                _copiedBadgeTimer.Start();
                UpdateEntriesList(force: true);
            }

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
            UpdateEntriesList(force: true);
        }

        private ListBox GetEntriesList()
        {
            return (ListBox)(FindName("EntriesList") ?? throw new InvalidOperationException("EntriesList was not found."));
        }

        private static void FocusSelectedListBoxItem(ListBox listBox)
        {
            if (listBox.SelectedItem == null)
            {
                return;
            }

            listBox.UpdateLayout();
            if (listBox.ItemContainerGenerator.ContainerFromItem(listBox.SelectedItem) is ListBoxItem item)
            {
                item.Focus();
            }
        }

        private ClipboardHistoryEntry? GetSelectedEntry()
        {
            return GetEntriesList().SelectedItem is ClipboardHistoryEntryViewModel item ? item.Entry : null;
        }

        private ClipboardHistoryEntry? FindEntryById(string? entryId)
        {
            if (string.IsNullOrWhiteSpace(entryId))
            {
                return null;
            }

            return _historyService.Entries.FirstOrDefault(entry => string.Equals(entry.Id, entryId, StringComparison.Ordinal));
        }

        private static string? GetEntryIdFromSender(object sender)
        {
            return sender switch
            {
                FrameworkElement { Tag: string entryId } => entryId,
                _ => null
            };
        }

        private bool Confirm(string messageKey)
        {
            return new DarkDialog(LocalizationService.Get(messageKey), isConfirm: true)
            {
                Owner = this
            }.ShowDialog() == true;
        }

        private void FilterTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded || sender is not System.Windows.Controls.TabControl tabControl || tabControl.SelectedItem is not TabItem selectedTab)
            {
                return;
            }

            if (selectedTab.Tag is not string tag)
            {
                return;
            }

            ClipboardManagerFilter filter = tag switch
            {
                "Pinned" => ClipboardManagerFilter.Pinned,
                "Text" => ClipboardManagerFilter.Text,
                "Images" => ClipboardManagerFilter.Images,
                _ => ClipboardManagerFilter.All
            };

            if (_activeFilter != filter)
            {
                SetFilter(filter);
            }
        }

        private void EntryCard_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (IsInteractiveChildClick(e.OriginalSource as DependencyObject) || sender is not FrameworkElement { DataContext: ClipboardHistoryEntryViewModel item })
            {
                return;
            }

            GetEntriesList().SelectedItem = item;
            _selectedEntryId = item.Id;
            CopyEntry(item.Entry);
        }

        private void EntryCopyButton_Click(object sender, RoutedEventArgs e)
        {
            if (FindEntryById(GetEntryIdFromSender(sender)) is ClipboardHistoryEntry entry)
            {
                ListBox entriesList = GetEntriesList();
                entriesList.SelectedItem = entriesList.Items.OfType<ClipboardHistoryEntryViewModel>().FirstOrDefault(item => item.Id == entry.Id);
                _selectedEntryId = entry.Id;
                CopyEntry(entry);
            }
        }

        private void EntryPinButton_Click(object sender, RoutedEventArgs e)
        {
            if (FindEntryById(GetEntryIdFromSender(sender)) is ClipboardHistoryEntry entry)
            {
                _selectedEntryId = entry.Id;
                TogglePin(entry);
            }
        }

        private void EntryDeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (FindEntryById(GetEntryIdFromSender(sender)) is ClipboardHistoryEntry entry)
            {
                _selectedEntryId = null;
                DeleteEntry(entry);
            }
        }

        private void BtnClearHistory_Click(object sender, RoutedEventArgs e)
        {
            if (!Confirm("ClipboardManager_ClearHistoryConfirm"))
            {
                return;
            }

            _historyService.ClearUnpinnedHistory();
            _selectedEntryId = _historyService.Entries.FirstOrDefault(entry => entry.IsPinned)?.Id;
            TxtStatus.Text = LocalizationService.Get("ClipboardManager_Cleared");
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.F)
            {
                TxtSearch.Focus();
                TxtSearch.SelectAll();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Escape)
            {
                Close();
                e.Handled = true;
                return;
            }

            if (TxtSearch.IsKeyboardFocusWithin)
            {
                ListBox entriesList = GetEntriesList();
                if (e.Key == Key.Down && entriesList.Items.Count > 0)
                {
                    entriesList.SelectedIndex = Math.Max(0, entriesList.SelectedIndex);
                    FocusSelectedListBoxItem(entriesList);
                    e.Handled = true;
                }

                return;
            }

            ListBox listBox = GetEntriesList();
            if (listBox.Items.Count == 0)
            {
                return;
            }

            if (e.Key == Key.Down)
            {
                listBox.SelectedIndex = Math.Min(listBox.Items.Count - 1, listBox.SelectedIndex < 0 ? 0 : listBox.SelectedIndex + 1);
                listBox.ScrollIntoView(listBox.SelectedItem);
                FocusSelectedListBoxItem(listBox);
                if (GetSelectedEntry() is ClipboardHistoryEntry selectedEntry)
                {
                    _selectedEntryId = selectedEntry.Id;
                }
                e.Handled = true;
            }
            else if (e.Key == Key.Up)
            {
                listBox.SelectedIndex = listBox.SelectedIndex <= 0 ? 0 : listBox.SelectedIndex - 1;
                listBox.ScrollIntoView(listBox.SelectedItem);
                FocusSelectedListBoxItem(listBox);
                if (GetSelectedEntry() is ClipboardHistoryEntry selectedEntry)
                {
                    _selectedEntryId = selectedEntry.Id;
                }
                e.Handled = true;
            }
            else if (e.Key == Key.Enter)
            {
                if (GetSelectedEntry() is ClipboardHistoryEntry selectedEntry)
                {
                    CopyEntry(selectedEntry);
                    _selectedEntryId = selectedEntry.Id;
                    e.Handled = true;
                }
            }
            else if (e.Key == Key.Delete)
            {
                if (GetSelectedEntry() is ClipboardHistoryEntry selectedEntry)
                {
                    _selectedEntryId = null;
                    DeleteEntry(selectedEntry);
                    e.Handled = true;
                }
            }
        }
    }

    public sealed class ClipboardHistoryEntryViewModel
    {
        public required string Id { get; init; }
        public required ClipboardHistoryEntry Entry { get; init; }
        public required bool IsImage { get; init; }
        public required bool IsPinned { get; init; }
        public required string Title { get; init; }
        public required string Summary { get; init; }
        public required string ImageLabel { get; init; }
        public required string CopyLabel { get; init; }
        public required string CopiedLabel { get; init; }
        public required string PinLabel { get; init; }
        public required string DeleteLabel { get; init; }
        public required Thickness ActionsMargin { get; init; }
        public required bool IsRecentlyCopied { get; init; }
        public BitmapSource? PreviewImage { get; init; }
    }
}
