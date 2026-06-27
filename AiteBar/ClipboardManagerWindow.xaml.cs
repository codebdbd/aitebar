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
        private const string GlyphCopy = "\uE8C8";
        private const string GlyphPin = "\uE718";
        private const string GlyphDelete = "\uE74D";

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

            var layout = new Grid();
            layout.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            if (entry.IsImage)
            {
                Border preview = CreateImagePreview(entry);
                Grid.SetColumn(preview, 0);
                layout.Children.Add(preview);
            }

            var content = new Grid();
            content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Grid.SetColumn(content, 1);
            layout.Children.Add(content);

            var header = new Grid();
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            Border typeBadge = entry.IsImage
                ? CreateTypeBadge("IMG", "#1F2731", "#344150")
                : CreateTypeBadge("TXT", "#1C2734", "#314559");
            Grid.SetColumn(typeBadge, 0);
            header.Children.Add(typeBadge);

            var textStack = new StackPanel();
            var title = new TextBlock
            {
                Text = entry.IsImage ? LocalizationService.Get("ClipboardManager_ImageLabel") : entry.DisplayText,
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White,
                TextWrapping = TextWrapping.Wrap,
                MaxHeight = 36
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
                    Style = (Style)FindResource("ClipboardPinnedBadgeStyle"),
                    Margin = new Thickness(8, 1, 0, 0),
                    VerticalAlignment = VerticalAlignment.Top,
                    ToolTip = LocalizationService.Get("ClipboardManager_PinnedBadge"),
                    Child = new TextBlock
                    {
                        Text = LocalizationService.Get("ClipboardManager_PinnedBadge"),
                        FontSize = 10,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#F2D57B"))
                    }
                };
                Grid.SetColumn(pinned, 2);
                header.Children.Add(pinned);
            }

            Grid.SetRow(header, 0);
            content.Children.Add(header);

            var actions = new StackPanel
            {
                Margin = new Thickness(entry.IsImage ? 0 : 50, 10, 0, 0),
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Right
            };
            actions.Children.Add(CreateActionButton(GlyphCopy, LocalizationService.Get("ClipboardManager_Copy"), () => CopyEntry(entry)));
            actions.Children.Add(CreateActionButton(
                GlyphPin,
                entry.IsPinned
                    ? LocalizationService.Get("ClipboardManager_Unpin")
                    : LocalizationService.Get("ClipboardManager_Pin"),
                () => TogglePin(entry)));
            actions.Children.Add(CreateActionButton(GlyphDelete, LocalizationService.Get("ClipboardManager_Delete"), () => DeleteEntry(entry)));

            Grid.SetRow(actions, 1);
            content.Children.Add(actions);

            entryBorder.Child = layout;
            entryBorder.MouseLeftButtonDown += (_, args) =>
            {
                if (IsInteractiveChildClick(args.OriginalSource as DependencyObject))
                {
                    return;
                }

                CopyEntry(entry);
            };
            return entryBorder;
        }

        private Border CreateImagePreview(ClipboardHistoryEntry entry)
        {
            var frame = new Border
            {
                Style = (Style)FindResource("PreviewFrameStyle")
            };

            if (TryCreateBitmap(entry.ImageBytes) is BitmapSource bitmap)
            {
                frame.Child = new Image
                {
                    Source = bitmap,
                    Stretch = Stretch.UniformToFill,
                    SnapsToDevicePixels = true
                };
                frame.ClipToBounds = true;
            }
            else
            {
                frame.Child = new TextBlock
                {
                    Text = LocalizationService.Get("ClipboardManager_ImageLabel"),
                    Foreground = (Brush)FindResource("MutedText"),
                    FontSize = 11,
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextAlignment = TextAlignment.Center
                };
            }

            return frame;
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

        private Button CreateActionButton(string glyph, string toolTip, Action onClick)
        {
            var button = new Button
            {
                Content = glyph,
                ToolTip = toolTip,
                Style = (Style)FindResource("ClipboardActionButtonStyle")
            };
            button.Click += (_, _) => onClick();
            return button;
        }

        private Border CreateTypeBadge(string label, string backgroundHex, string borderHex)
        {
            return new Border
            {
                Style = (Style)FindResource("ClipboardTypeBadgeStyle"),
                Background = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(backgroundHex)),
                BorderBrush = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(borderHex)),
                Child = new TextBlock
                {
                    Text = label,
                    FontSize = 10,
                    FontWeight = FontWeights.Bold,
                    Foreground = Brushes.White,
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextAlignment = TextAlignment.Center
                }
            };
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
            button.Opacity = 1.0;
            button.FontWeight = FontWeights.SemiBold;
            button.Background = active
                ? new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#21496F"))
                : new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#141C27"));
            button.BorderBrush = active
                ? new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#3B78B6"))
                : new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#26384A"));
            button.Foreground = active
                ? Brushes.White
                : new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#AEB9C8"));
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
