using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using AiteBar.IptvPlayer;
using AiteBar.TelegramPostEditor.Services;
using Control = System.Windows.Controls.Control;

namespace AiteBar;

public partial class AppSettingsWindow
{
    private readonly CancellationTokenSource _listOperations = new();
    private readonly Dictionary<SettingsListKind, Action> _listUndo = [];
    private readonly Dictionary<string, string> _sourceChecks = new(StringComparer.Ordinal);
    private readonly List<string> _aiDisplayOrder = [];
    private readonly Dictionary<SettingsListKind, List<SettingsSourceEntry>> _sourceLists = [];
    private bool _savingListDraft;
    private SettingsListKind CurrentList => (SettingsListKind)Math.Max(0, SettingsListPicker.SelectedIndex);

    private void InitializeSettingsListDrafts()
    {
        _sourceLists[SettingsListKind.Rss] = [.. _settings.RssFeeds];
        _sourceLists[SettingsListKind.TelegramReading] = [.. _settings.TelegramReadingSources];
        _sourceLists[SettingsListKind.Prompts] = [.. _settings.SavedPrompts];
        _aiDisplayOrder.AddRange(_settings.AiConnectionDisplayOrder);
        Closed += (_, _) => _listOperations.Cancel();
        Closing += (_, args) => args.Cancel |= _savingListDraft;
    }

    private void SaveSettingsListDrafts(AppSettings settings)
    {
        settings.RssFeeds = [.. _sourceLists[SettingsListKind.Rss]];
        settings.TelegramReadingSources = [.. _sourceLists[SettingsListKind.TelegramReading]];
        settings.SavedPrompts = [.. _sourceLists[SettingsListKind.Prompts]];
        settings.AiConnectionDisplayOrder = [.. _aiDisplayOrder];
        settings.TelegramBotDisplayOrder = _telegramBots.Select(bot => bot.Id).ToList();
        settings.TelegramChannelDisplayOrder = _telegramChannels.Select(channel => channel.Id).ToList();
        settings.TelegramPublishingCategories = _telegramChannels.Where(channel => _telegramChannelCategories.ContainsKey(channel.Id))
            .ToDictionary(channel => channel.Id, channel => _telegramChannelCategories[channel.Id], StringComparer.Ordinal);
    }

    private static string ListLabel(SettingsListKind kind) => LocalizationService.Get(kind switch
    {
        SettingsListKind.Ai => "SettingsLists_Ai",
        SettingsListKind.Iptv => "SettingsLists_Iptv",
        SettingsListKind.TelegramBots => "TelegramSettings_BotsTitle",
        SettingsListKind.TelegramReading => "SettingsLists_TelegramReading",
        SettingsListKind.TelegramPublishing => "SettingsLists_TelegramPublishing",
        SettingsListKind.Rss => "SettingsLists_Rss",
        _ => "SettingsLists_Prompts"
    });

    private void RefreshSettingsListPicker()
    {
        SettingsListKind selected = CurrentList;
        SettingsListPicker.ItemsSource = Enum.GetValues<SettingsListKind>().Select(ListLabel).ToArray();
        SettingsListPicker.SelectedIndex = (int)selected;
    }

    private void SettingsListPicker_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SettingsListPicker.SelectedIndex < 0 || AdditionalListBody is null) return;
        SettingsListKind selected = CurrentList;
        AiListBody.Visibility = VisibleWhen(selected == SettingsListKind.Ai);
        AiListOptions.Visibility = AiListBody.Visibility;
        IptvListBody.Visibility = VisibleWhen(selected == SettingsListKind.Iptv);
        IptvListOptions.Visibility = IptvListBody.Visibility;
        TelegramBotListBody.Visibility = VisibleWhen(selected == SettingsListKind.TelegramBots);
        TelegramChannelListBody.Visibility = VisibleWhen(selected == SettingsListKind.TelegramPublishing);
        AdditionalListBody.Visibility = VisibleWhen(_sourceLists.ContainsKey(selected));
        RefreshAdditionalRows();
        UpdateListUndo();
    }

    private static Visibility VisibleWhen(bool visible) => visible ? Visibility.Visible : Visibility.Collapsed;

    private async void AddSettingsListEntry_Click(object sender, RoutedEventArgs e)
    {
        switch (CurrentList)
        {
            case SettingsListKind.Ai: BtnAiAddConnection_Click(sender, e); break;
            case SettingsListKind.Iptv: BtnAddIptvPlaylist_Click(sender, e); break;
            case SettingsListKind.TelegramBots: await EditTelegramBotAsync(null); break;
            case SettingsListKind.TelegramPublishing: await EditTelegramChannelAsync(null); break;
            default: EditSource(CurrentList, null); break;
        }
    }

    private Grid BuildSettingsListRow(string name, string details, bool? enabled, Action<bool>? toggle,
        int index, int count, Action<int> move, Func<Task> check, Action edit, Action remove)
    {
        var row = new Grid { Margin = new Thickness(0, 4, 0, 4), MinHeight = 44 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var surface = new Border { CornerRadius = new CornerRadius(6), BorderThickness = new Thickness(1) };
        surface.SetResourceReference(Border.BackgroundProperty, "SettingsListRowBackground");
        Grid.SetColumnSpan(surface, 3);
        row.Children.Add(surface);
        if (enabled.HasValue)
        {
            var control = new CheckBox
            {
                IsChecked = enabled,
                Style = (Style)FindResource("ModernSwitchStyle"),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(10, 0, 2, 0)
            };
            AutomationProperties.SetName(control, $"{Locale("SettingsLists_Enabled")} · {name}");
            control.Click += (_, _) => toggle?.Invoke(control.IsChecked == true);
            row.Children.Add(control);
        }
        var info = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 8, 0) };
        info.Children.Add(new TextBlock { Text = name, ToolTip = name, TextTrimming = TextTrimming.CharacterEllipsis, Style = (Style)FindResource("ModernSettingTitleStyle") });
        info.Children.Add(new TextBlock { Text = details, ToolTip = details, TextTrimming = TextTrimming.CharacterEllipsis, Style = (Style)FindResource("ModernSettingDescriptionStyle") });
        Grid.SetColumn(info, 1);
        row.Children.Add(info);

        var actions = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };
        actions.Children.Add(Icon("M 4,10 L 8,6 12,10", "SettingsLists_Up", () => move(-1), index > 0));
        actions.Children.Add(Icon("M 4,6 L 8,10 12,6", "SettingsLists_Down", () => move(1), index + 1 < count));
        Button checkButton = Icon("M 8,2 A 6,6 0 1 0 8,14 A 6,6 0 1 0 8,2 M 5.3,8 L 7.3,10 10.8,6.6", "AiSettings_Test", () => { });
        checkButton.Click += async (_, _) =>
        {
            checkButton.IsEnabled = false;
            try { await check(); }
            catch (OperationCanceledException) when (_listOperations.IsCancellationRequested) { }
            catch (Exception) { if (!_listOperations.IsCancellationRequested) ShowListMessage("SettingsLists_CheckFailed"); }
            finally { checkButton.IsEnabled = true; }
        };
        actions.Children.Add(checkButton);
        actions.Children.Add(Icon("M 2.5,13.5 L 3.5,10 11.3,2.2 Q 12,1.5 12.7,2.2 L 13.8,3.3 Q 14.5,4 13.8,4.7 L 6,12.5 Z M 10,3.5 L 12.5,6", "SettingsLists_Edit", edit));
        actions.Children.Add(Icon("M 2.5,4.5 H 13.5 M 6,4.5 V 2.5 H 10 V 4.5 M 4,4.5 L 4.8,13.5 H 11.2 L 12,4.5 M 6.7,7 V 11 M 9.3,7 V 11", "Common_Delete", remove, danger: true));
        Grid.SetColumn(actions, 2);
        row.Children.Add(actions);
        return row;

        Button Icon(string geometry, string key, Action action, bool available = true, bool danger = false)
        {
            string label = Locale(key);
            var icon = new System.Windows.Shapes.Path
            {
                Data = Geometry.Parse(geometry), StrokeThickness = 1.15,
                StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round, StrokeLineJoin = PenLineJoin.Round
            };
            var canvas = new Canvas { Width = 16, Height = 16, IsHitTestVisible = false };
            canvas.Children.Add(icon);
            var button = new Button
            {
                Content = canvas, Style = (Style)FindResource("IconButtonStyle"), ToolTip = label,
                IsEnabled = available, Margin = new Thickness(2, 0, 2, 0),
                BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(64, 64, 68))
            };
            if (danger) icon.Stroke = new SolidColorBrush(System.Windows.Media.Color.FromRgb(231, 153, 153));
            else icon.SetBinding(System.Windows.Shapes.Shape.StrokeProperty, new System.Windows.Data.Binding(nameof(Foreground)) { Source = button });
            button.Click += (_, _) => action();
            AutomationProperties.SetName(button, label);
            return button;
        }
    }

    private void UpdateListUndo() => UndoListRemoval.Visibility = VisibleWhen(_listUndo.ContainsKey(CurrentList));

    private void RegisterListUndo(SettingsListKind kind, Action undo)
    {
        _listUndo[kind] = undo;
        UpdateListUndo();
    }

    private void UndoListRemoval_Click(object sender, RoutedEventArgs e)
    {
        if (_listUndo.Remove(CurrentList, out Action? undo)) undo();
        UpdateListUndo();
    }

    private static void MoveListItem<T>(List<T> entries, int index, int delta)
    {
        int next = index + delta;
        if (next < 0 || next >= entries.Count) return;
        (entries[index], entries[next]) = (entries[next], entries[index]);
    }

    private List<T> OrderList<T>(IEnumerable<T> entries, IReadOnlyList<string> ids, Func<T, string> getId)
    {
        var ranks = ids.Distinct(StringComparer.Ordinal).Select((id, index) => (id, index))
            .ToDictionary(pair => pair.id, pair => pair.index, StringComparer.Ordinal);
        return entries.OrderBy(entry => ranks.GetValueOrDefault(getId(entry), int.MaxValue)).ToList();
    }

    private void RefreshAdditionalRows()
    {
        AdditionalListBody.Children.Clear();
        SettingsListKind kind = CurrentList;
        if (!_sourceLists.TryGetValue(kind, out var entries)) return;
        if (entries.Count == 0)
        {
            AdditionalListBody.Children.Add(new TextBlock { Text = Locale("SettingsLists_Empty"), Style = (Style)FindResource("ModernSettingDescriptionStyle") });
        }
        for (int index = 0; index < entries.Count; index++)
        {
            SettingsSourceEntry entry = entries[index];
            int position = index;
            string summary = kind == SettingsListKind.Prompts
                ? entry.Value.Split('\n')[0]
                : kind == SettingsListKind.Rss && Uri.TryCreate(entry.Value, UriKind.Absolute, out Uri? uri)
                    ? uri.GetLeftPart(UriPartial.Path)
                    : entry.Value;
            string details = string.Join(" · ", new[] { entry.Category, summary, _sourceChecks.GetValueOrDefault(entry.Id, "") }.Where(value => !string.IsNullOrWhiteSpace(value)));
            AdditionalListBody.Children.Add(BuildSettingsListRow(entry.Name, details,
                kind == SettingsListKind.Rss ? entry.IsEnabled : null,
                enabled =>
                {
                    entries[position] = entry with { IsEnabled = enabled };
                    RefreshAdditionalRows();
                },
                index, entries.Count, offset => { MoveListItem(entries, position, offset); RefreshAdditionalRows(); },
                async () =>
                {
                    bool valid = kind == SettingsListKind.Rss
                        ? await CheckFeedAsync(entry.Value, _listOperations.Token)
                        : kind == SettingsListKind.Prompts ? !string.IsNullOrWhiteSpace(entry.Value) : IsTelegramAddress(entry.Value);
                    if (_listOperations.IsCancellationRequested) return;
                    _sourceChecks[entry.Id] = Locale(!valid ? "SettingsLists_CheckFailed" :
                        kind == SettingsListKind.TelegramReading ? "SettingsLists_AddressValid" : "SettingsLists_Checked");
                    RefreshAdditionalRows();
                }, () => EditSource(kind, entry), () =>
                {
                    entries.RemoveAt(position);
                    RegisterListUndo(kind, () =>
                    {
                        entries.Insert(Math.Min(position, entries.Count), entry);
                        RefreshAdditionalRows();
                    });
                    RefreshAdditionalRows();
                }));
        }
    }

    private void EditSource(SettingsListKind kind, SettingsSourceEntry? existing)
    {
        string valueKey = kind switch
        {
            SettingsListKind.Rss => "SettingsLists_RssUrl",
            SettingsListKind.TelegramReading => "SettingsLists_ChannelAddress",
            _ => "SettingsLists_PromptText"
        };
        var values = ShowListEditor(ListLabel(kind),
            new("SettingsLists_Name", existing?.Name ?? ""),
            new(valueKey, existing?.Value ?? "", Multiline: kind == SettingsListKind.Prompts),
            new("SettingsLists_Category", existing?.Category ?? ""));
        if (values is null) return;
        if (!TryCreateSource(kind, values, existing, out SettingsSourceEntry? entry))
        {
            ShowListMessage("SettingsLists_InvalidEntry");
            return;
        }
        List<SettingsSourceEntry> entries = _sourceLists[kind];
        if (existing is null) entries.Add(entry!);
        else entries[entries.IndexOf(existing)] = entry!;
        _sourceChecks.Remove(entry!.Id);
        RefreshAdditionalRows();
    }

    private bool TryCreateSource(SettingsListKind kind, string[] values, SettingsSourceEntry? existing, out SettingsSourceEntry? entry)
    {
        string name = Cell(values, 0);
        string value = kind == SettingsListKind.Prompts && values.Length > 1 ? values[1] : Cell(values, 1);
        entry = null;
        if (kind == SettingsListKind.TelegramReading && !IsTelegramAddress(value) ||
            kind == SettingsListKind.Rss && !TryWebUri(value, out _) ||
            kind == SettingsListKind.Prompts && (name.Length == 0 || string.IsNullOrWhiteSpace(value))) return false;
        if (kind == SettingsListKind.TelegramReading) value = TelegramChannelService.NormalizeChannelIdentifier(value);
        string key = kind == SettingsListKind.Prompts ? name : value;
        if (_sourceLists[kind].Any(item => item.Id != existing?.Id &&
                string.Equals(kind == SettingsListKind.Prompts ? item.Name : item.Value, key,
                    kind == SettingsListKind.Rss ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase))) return false;
        entry = (existing ?? new SettingsSourceEntry()) with
        {
            Name = name.Length == 0 ? value : name, Value = value, Category = Cell(values, 2),
            IsEnabled = ReadEnabled(Cell(values, 3), existing?.IsEnabled ?? true)
        };
        return true;
    }

    private static bool TryWebUri(string value, out Uri? uri) =>
        Uri.TryCreate(value, UriKind.Absolute, out uri) && uri.Scheme is "https" or "http" && uri.UserInfo.Length == 0;

    private static bool IsTelegramAddress(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        string normalized = TelegramChannelService.NormalizeChannelIdentifier(value);
        return long.TryParse(normalized, CultureInfo.InvariantCulture, out _) ||
            normalized.Length > 1 && normalized[0] == '@' &&
            normalized.Skip(1).All(character => char.IsAsciiLetterOrDigit(character) || character == '_');
    }

    internal static async Task<bool> CheckFeedAsync(string address, CancellationToken cancellationToken) =>
        await RssFeedReader.ReadAsync(address, cancellationToken) is not null;

    internal static bool IsFeedXml(Stream stream) => RssFeedReader.Read(stream) is not null;

    private sealed record ListField(string Label, string Value = "", bool Secret = false, bool Multiline = false, IReadOnlyList<string>? Options = null);
    private sealed class SettingsListDialog : DarkWindow
    {
        public SettingsListDialog()
        {
            SetResourceReference(BackgroundProperty, "WindowBackground");
            SetResourceReference(ForegroundProperty, "PrimaryText");
        }
    }

    private string[]? ShowListEditor(string title, params ListField[] fields) => ShowListEditor(this, title, fields);

    private static string[]? ShowListEditor(Window owner, string title, params ListField[] fields)
    {
        var dialog = new SettingsListDialog { Owner = owner, Title = title, Width = 500, SizeToContent = SizeToContent.Height, WindowStartupLocation = WindowStartupLocation.CenterOwner, ResizeMode = ResizeMode.NoResize };
        var content = new StackPanel { Margin = new Thickness(18) };
        var inputs = new List<Func<string>>();
        foreach (ListField field in fields)
        {
            string label = Locale(field.Label);
            content.Children.Add(new TextBlock { Text = label, Margin = new Thickness(0, 0, 0, 4) });
            Control control;
            if (field.Options is not null)
            {
                var picker = new ComboBox { ItemsSource = field.Options, SelectedItem = field.Value, MinHeight = 32 };
                if (picker.SelectedIndex < 0 && field.Options.Count == 1) picker.SelectedIndex = 0;
                control = picker;
                inputs.Add(() => picker.SelectedItem as string ?? "");
            }
            else if (field.Secret)
            {
                var password = new PasswordBox { MinHeight = 32, Password = field.Value };
                control = password;
                inputs.Add(() => password.Password);
            }
            else
            {
                var box = new TextBox { Text = field.Value, AcceptsReturn = field.Multiline, TextWrapping = field.Multiline ? TextWrapping.Wrap : TextWrapping.NoWrap, Height = double.NaN, MinHeight = field.Multiline ? 100 : 32, MaxHeight = field.Multiline ? 160 : 32 };
                if (field.Multiline) box.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
                if (field.Multiline) box.VerticalContentAlignment = VerticalAlignment.Stretch;
                control = box;
                inputs.Add(() => box.Text);
            }
            AutomationProperties.SetName(control, label);
            control.Margin = new Thickness(0, 0, 0, 10);
            content.Children.Add(control);
        }
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = System.Windows.HorizontalAlignment.Right };
        var cancel = new Button { IsCancel = true, Content = Locale("Common_Cancel"), MinWidth = 90, Margin = new Thickness(0, 0, 8, 0) };
        var apply = new Button { IsDefault = true, Content = Locale("Common_Save"), MinWidth = 90 };
        apply.Click += (_, _) => dialog.DialogResult = true;
        actions.Children.Add(cancel);
        actions.Children.Add(apply);
        content.Children.Add(actions);
        dialog.Content = content;
        return dialog.ShowDialog() == true ? inputs.Select(read => read()).ToArray() : null;
    }

    private static string Cell(string[] values, int index) => index < values.Length ? values[index].Trim() : "";
    private static bool ReadEnabled(string value, bool fallback = true) => bool.TryParse(value, out bool parsed) ? parsed : fallback;
    private static string Locale(string key) => LocalizationService.Get(key);
    private void ShowListMessage(string key) => new DarkDialog(Locale(key)) { Owner = this }.ShowDialog();
}
