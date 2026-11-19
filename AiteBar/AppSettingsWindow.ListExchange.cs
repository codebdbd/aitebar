using System.Globalization;
using System.Text;
using AiteBar.IptvPlayer;
using AiteBar.TelegramPostEditor.Models;
using AiteBar.TelegramPostEditor.Services;
using DataGrid = System.Windows.Controls.DataGrid;
using DataGridCell = System.Windows.Controls.DataGridCell;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;

namespace AiteBar;

public partial class AppSettingsWindow
{
    private const int MaximumListFileSize = 4 * 1024 * 1024;

    private async void ImportSettingsList_Click(object sender, RoutedEventArgs e)
    {
        SettingsListKind kind = CurrentList;
        string? content = ShowListImport(kind);
        if (content is null) return;
        IReadOnlyList<ImportPreviewRow>? rows = PreviewListImport(kind, SettingsListTextFormat.Read(kind, content));
        if (rows is null || rows.Count == 0) return;
        BtnSave.IsEnabled = false;
        AiProvidersSettingsSection.IsEnabled = false;
        var failures = new List<string>();
        int added = 0;
        int updated = 0;
        int skipped = 0;
        var updatedTargets = new HashSet<object>();
        try
        {
            foreach (var row in rows)
            {
                _listOperations.Token.ThrowIfCancellationRequested();
                try
                {
                    if (row.Target is { } target)
                    {
                        if (!updatedTargets.Add(target)) { skipped++; continue; }
                        await UpdateImportedRowAsync(kind, row.Row.Values, target);
                        updated++;
                    }
                    else if (await ImportListRowAsync(kind, row.Row.Values)) added++;
                    else skipped++;
                }
                catch (Exception) when (!_listOperations.IsCancellationRequested)
                {
                    failures.Add(row.Line.ToString(CultureInfo.CurrentCulture));
                }
            }
            RefreshAdditionalRows();
            var result = LocalizationService.Format("SettingsLists_ImportResult", added, skipped, failures.Count);
            if (updated > 0) result += Environment.NewLine + LocalizationService.Format("SettingsLists_UpdatedCount", updated);
            if (failures.Count > 0)
            {
                result += Environment.NewLine + LocalizationService.Format("SettingsLists_ImportErrors", string.Join(", ", failures.Take(25)));
            }
            new DarkDialog(result) { Owner = this }.ShowDialog();
        }
        catch (OperationCanceledException) when (_listOperations.IsCancellationRequested) { }
        finally
        {
            BtnSave.IsEnabled = true;
            AiProvidersSettingsSection.IsEnabled = true;
        }
    }

    private sealed class ImportPreviewRow
    {
        public required SettingsListTextFormat.Row Row { get; set; }
        public string Status { get; set; } = "";
        public object? Target { get; set; }
        public bool Valid { get; set; }
        public int Line => Row.Line;
        public string Name => PublicLocation(Cell(Row.Values, 0));
        public bool Include { get; set; }
    }

    private IReadOnlyList<ImportPreviewRow>? PreviewListImport(SettingsListKind kind, IReadOnlyList<SettingsListTextFormat.Row> rows)
    {
        var dialog = new SettingsListDialog { Owner = this, Title = Locale("SettingsLists_Preview"), Width = 600, Height = 410, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        var panel = new DockPanel { Margin = new Thickness(16), LastChildFill = true };
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = System.Windows.HorizontalAlignment.Right, Margin = new Thickness(0, 8, 0, 0) };
        DockPanel.SetDock(actions, Dock.Bottom);
        var cancel = new Button { IsCancel = true, MinWidth = 90, Content = Locale("Common_Cancel"), Margin = new Thickness(0, 0, 6, 0) };
        var import = new Button { IsDefault = true, MinWidth = 90, Content = Locale("SettingsLists_Import") };
        var edit = new Button { MinWidth = 90, Content = Locale("SettingsLists_Edit"), IsEnabled = false, Margin = new Thickness(0, 0, 6, 0) };
        actions.Children.Add(edit);
        actions.Children.Add(cancel);
        actions.Children.Add(import);
        panel.Children.Add(actions);
        var defaultBot = new ComboBox { Margin = new Thickness(0, 0, 0, 8) };
        var botOptions = _telegramBots.Select(bot => new KeyValuePair<string, string>(bot.Id, bot.DisplayName)).ToList();
        if (kind == SettingsListKind.TelegramPublishing && botOptions.Count > 0)
        {
            DockPanel.SetDock(defaultBot, Dock.Top);
            botOptions.Insert(0, new KeyValuePair<string, string>("", Locale("SettingsLists_UseDefaultBot")));
            defaultBot.ItemsSource = botOptions;
            defaultBot.DisplayMemberPath = "Value";
            defaultBot.SelectedValuePath = "Key";
            defaultBot.SelectedIndex = botOptions.Count == 2 ? 1 : 0;
            panel.Children.Add(defaultBot);
        }
        var grid = new DataGrid
        {
            AutoGenerateColumns = false, CanUserAddRows = false, CanUserDeleteRows = false,
            EnableRowVirtualization = true, EnableColumnVirtualization = true, HeadersVisibility = DataGridHeadersVisibility.Column,
            GridLinesVisibility = DataGridGridLinesVisibility.Horizontal, RowHeight = 30
        };
        grid.SetResourceReference(BackgroundProperty, "FormInputBackground");
        grid.SetResourceReference(ForegroundProperty, "PrimaryText");
        grid.SetResourceReference(BorderBrushProperty, "FormControlBorderBrush");
        grid.SetResourceReference(DataGrid.HorizontalGridLinesBrushProperty, "FormControlBorderBrush");
        var headers = new Style(typeof(System.Windows.Controls.Primitives.DataGridColumnHeader));
        headers.Setters.Add(new Setter(BackgroundProperty, new DynamicResourceExtension("PanelBackground")));
        headers.Setters.Add(new Setter(ForegroundProperty, new DynamicResourceExtension("PrimaryText")));
        headers.Setters.Add(new Setter(BorderBrushProperty, new DynamicResourceExtension("FormControlBorderBrush")));
        headers.Setters.Add(new Setter(BorderThicknessProperty, new Thickness(0, 0, 1, 1)));
        headers.Setters.Add(new Setter(PaddingProperty, new Thickness(6, 5, 6, 5)));
        grid.ColumnHeaderStyle = headers;
        var rowsStyle = new Style(typeof(DataGridRow));
        rowsStyle.Setters.Add(new Setter(BackgroundProperty, new DynamicResourceExtension("FormInputBackground")));
        rowsStyle.Setters.Add(new Setter(ForegroundProperty, new DynamicResourceExtension("PrimaryText")));
        grid.RowStyle = rowsStyle;
        var cells = new Style(typeof(DataGridCell));
        cells.Setters.Add(new Setter(ForegroundProperty, new DynamicResourceExtension("PrimaryText")));
        var selectedCell = new Trigger { Property = DataGridCell.IsSelectedProperty, Value = true };
        selectedCell.Setters.Add(new Setter(BackgroundProperty, new DynamicResourceExtension("AccentColor")));
        selectedCell.Setters.Add(new Setter(BorderBrushProperty, new DynamicResourceExtension("AccentColor")));
        cells.Triggers.Add(selectedCell);
        grid.CellStyle = cells;
        grid.Columns.Add(new DataGridCheckBoxColumn { Header = Locale("SettingsLists_SelectEntry"), Binding = new System.Windows.Data.Binding(nameof(ImportPreviewRow.Include)) { UpdateSourceTrigger = System.Windows.Data.UpdateSourceTrigger.PropertyChanged } });
        grid.Columns.Add(new DataGridTextColumn { Header = Locale("SettingsLists_Line"), Binding = new System.Windows.Data.Binding(nameof(ImportPreviewRow.Line)), IsReadOnly = true });
        grid.Columns.Add(new DataGridTextColumn { Header = Locale("SettingsLists_Name"), Binding = new System.Windows.Data.Binding(nameof(ImportPreviewRow.Name)), IsReadOnly = true, Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        grid.Columns.Add(new DataGridTextColumn { Header = Locale("SettingsLists_Status"), Binding = new System.Windows.Data.Binding(nameof(ImportPreviewRow.Status)), IsReadOnly = true });
        string[] ValuesWithDefaultBot(string[] values, object? target)
        {
            if (kind != SettingsListKind.TelegramPublishing || target is not null || Cell(values, 2).Length > 0 ||
                defaultBot.SelectedValue is not string id || id.Length == 0) return values;
            return [Cell(values, 0), Cell(values, 1), id, Cell(values, 3)];
        }
        void Rate(ImportPreviewRow item)
        {
            item.Target = FindImportTarget(kind, item.Row.Values);
            item.Valid = IsValidImport(kind, ValuesWithDefaultBot(item.Row.Values, item.Target), item.Target);
            item.Status = Locale(!item.Valid ? "SettingsLists_Error" :
                item.Target is null ? "SettingsLists_New" : "SettingsLists_SelectToUpdate");
        }
        var preview = rows.Select(row =>
        {
            var item = new ImportPreviewRow { Row = row };
            Rate(item);
            item.Include = item.Valid && item.Target is null;
            return item;
        }).ToList();
        grid.ItemsSource = preview;
        grid.SelectionChanged += (_, _) => edit.IsEnabled = grid.SelectedItem is ImportPreviewRow;
        defaultBot.SelectionChanged += (_, _) =>
        {
            grid.CommitEdit();
            grid.CommitEdit();
            foreach (ImportPreviewRow item in preview) Rate(item);
            grid.Items.Refresh();
        };
        edit.Click += (_, _) =>
        {
            grid.CommitEdit();
            grid.CommitEdit();
            if (grid.SelectedItem is not ImportPreviewRow item) return;
            string[]? values = EditImportedValues(dialog, kind, item.Row.Values);
            if (values is null) return;
            item.Row = new SettingsListTextFormat.Row(item.Line, values);
            Rate(item);
            item.Include = item.Valid && item.Target is null;
            grid.Items.Refresh();
        };
        IReadOnlyList<ImportPreviewRow>? selected = null;
        import.Click += (_, _) =>
        {
            grid.CommitEdit(DataGridEditingUnit.Cell, true);
            grid.CommitEdit(DataGridEditingUnit.Row, true);
            var requested = preview.Where(item => item.Include).ToList();
            if (requested.Any(item => !item.Valid))
            {
                new DarkDialog(Locale("SettingsLists_InvalidEntry")) { Owner = dialog }.ShowDialog();
                return;
            }
            foreach (ImportPreviewRow item in requested)
                item.Row = new SettingsListTextFormat.Row(item.Line, ValuesWithDefaultBot(item.Row.Values, item.Target));
            selected = requested;
            dialog.DialogResult = true;
        };
        panel.Children.Add(grid);
        dialog.Content = panel;
        return dialog.ShowDialog() == true ? selected : null;
    }

    internal object? FindImportTarget(SettingsListKind kind, string[] values)
    {
        string name = Cell(values, 0);
        string value = Cell(values, 1);
        string address = IsTelegramAddress(value) ? TelegramChannelService.NormalizeChannelIdentifier(value) : "";
        return kind switch
        {
            SettingsListKind.Ai => SingleMatch(_aiConnections, item => item.DisplayName.Equals(name, StringComparison.OrdinalIgnoreCase)),
            SettingsListKind.Iptv => TryIptvKind(Cell(values, 2), out var sourceKind) && value.Length > 0
                ? SingleMatch(_iptvDraft.Sources, item => item.Kind == sourceKind &&
                    (item.Location == value || _pendingIptvSecrets.GetValueOrDefault(item.Id) == value ||
                        IptvPlaylistParser.IsSensitiveUrl(value) &&
                        item.Location == IptvPlaylistParser.MaskSensitiveUrl(value) &&
                        _iptvStore?.ReadSecretLocation(item.SourceStorageId) == value))
                : null,
            SettingsListKind.TelegramBots => value.Length > 0
                ? SingleMatch(_telegramBots, item => item.TelegramBotId?.ToString(CultureInfo.InvariantCulture) == value) : null,
            SettingsListKind.TelegramPublishing => address.Length > 0 ? SingleMatch(_telegramChannels, item =>
                ChannelAddress(item).Equals(address, StringComparison.OrdinalIgnoreCase) ||
                ("@" + item.Username).Equals(address, StringComparison.OrdinalIgnoreCase)) : null,
            SettingsListKind.TelegramReading => SingleMatch(_sourceLists[kind], item => item.Value.Equals(address, StringComparison.OrdinalIgnoreCase)),
            SettingsListKind.Rss => SingleMatch(_sourceLists[kind], item => item.Value == value),
            SettingsListKind.Prompts => SingleMatch(_sourceLists[kind], item => item.Name.Equals(name, StringComparison.OrdinalIgnoreCase)),
            _ => null
        };
    }

    private static T? SingleMatch<T>(IEnumerable<T> entries, Func<T, bool> matches) where T : class
    {
        var results = entries.Where(matches).Take(2).ToArray();
        return results.Length == 1 ? results[0] : null;
    }

    internal bool IsValidImport(SettingsListKind kind, string[] values, object? target)
    {
        string name = Cell(values, 0);
        string value = Cell(values, 1);
        return kind switch
        {
            SettingsListKind.Ai => ValidateImportedAi(name, value, values, target as AiConnectionSettings),
            SettingsListKind.Iptv => name.Length > 0 && TryIptvKind(Cell(values, 2), out var sourceKind) &&
                (target is not null || ValidIptvLocation(value, sourceKind)),
            SettingsListKind.Prompts => name.Length > 0 && value.Length > 0,
            SettingsListKind.TelegramBots => (value.Length == 0 || long.TryParse(value, CultureInfo.InvariantCulture, out long id) && id > 0) &&
                (Cell(values, 2).Length > 0 || target is BotAccount && name.Length > 0),
            SettingsListKind.TelegramReading => IsTelegramAddress(value),
            SettingsListKind.TelegramPublishing => IsTelegramAddress(value) &&
                FindImportBot(Cell(values, 2), (target as ChannelAccount)?.BotId) is not null,
            SettingsListKind.Rss => TryWebUri(value, out _),
            _ => false
        };
    }

    private static string AiImportEndpoint(string[] values, AiConnectionSettings? existing) =>
        Cell(values, 2) is { Length: > 0 } endpoint ? endpoint : existing?.CustomBaseUrl ?? "";

    private static bool ValidateImportedAi(string name, string providerId, string[] values, AiConnectionSettings? existing)
    {
        if (name.Length == 0 || !AiProviderCatalog.TryGet(providerId, out var provider)) return false;
        string? endpoint = null;
        if (provider.RequiresCustomBaseUrl)
        {
            if (!AiProviderCatalog.TryNormalizeCustomBaseUrl(AiImportEndpoint(values, existing), out var normalized)) return false;
            endpoint = normalized;
        }
        return Cell(values, 3).Length > 0 ||
            existing is not null && provider.Id == existing.ProviderId && endpoint == existing.CustomBaseUrl;
    }

    private BotAccount? FindImportBot(string value, string? existingId) => value.Length == 0
        ? existingId is null ? SingleMatch(_telegramBots, _ => true) : _telegramBots.FirstOrDefault(bot => bot.Id == existingId)
        : SingleMatch(_telegramBots, bot => bot.Id == value || bot.TelegramBotId?.ToString(CultureInfo.InvariantCulture) == value);

    private string[]? EditImportedValues(Window owner, SettingsListKind kind, string[] values)
    {
        ListField Field(int index, string label, bool secret = false, bool multiline = false) =>
            new(label, index < values.Length ? values[index] : "", Secret: secret, Multiline: multiline);
        ListField Enabled(int index) => new("SettingsLists_Enabled", Cell(values, index).ToLowerInvariant(), Options: ["", "true", "false"]);
        var botOptions = new Dictionary<string, string> { [Locale("SettingsLists_DefaultOrCurrentBot")] = "" };
        string selectedBot = botOptions.Keys.First();
        if (kind == SettingsListKind.TelegramPublishing)
        {
            BotAccount? current = Cell(values, 2).Length == 0 ? null : FindImportBot(Cell(values, 2), null);
            foreach (BotAccount bot in _telegramBots)
            {
                string id = bot.TelegramBotId?.ToString(CultureInfo.InvariantCulture) ?? bot.Id;
                string label = $"{bot.DisplayName} · {id}";
                botOptions[label] = id;
                if (bot == current) selectedBot = label;
            }
        }
        ListField[] fields = kind switch
        {
            SettingsListKind.Ai => [Field(1, "SettingsLists_Provider"), Field(2, "AiSettings_BaseUrl",
                IptvPlaylistParser.IsSensitiveUrl(Cell(values, 2))), Field(3, "SettingsLists_Token", true), Enabled(4)],
            SettingsListKind.Iptv => [Field(1, "Iptv_Location", IptvPlaylistParser.IsSensitiveUrl(Cell(values, 1))),
                Field(2, "SettingsLists_PlaylistType"), Enabled(3)],
            SettingsListKind.TelegramBots => [Field(1, "SettingsLists_TelegramId"), Field(2, "SettingsLists_Token", true)],
            SettingsListKind.TelegramPublishing => [Field(1, "SettingsLists_ChannelAddress"),
                new("SettingsLists_BotId", selectedBot, Options: [.. botOptions.Keys]), Field(3, "SettingsLists_Category")],
            SettingsListKind.TelegramReading => [Field(1, "SettingsLists_ChannelAddress"), Field(2, "SettingsLists_Category")],
            SettingsListKind.Rss => [Field(1, "SettingsLists_RssUrl", IptvPlaylistParser.IsSensitiveUrl(Cell(values, 1))),
                Field(2, "SettingsLists_Category"), Enabled(3)],
            _ => [Field(1, "SettingsLists_PromptText", multiline: true), Field(2, "SettingsLists_Category")]
        };
        var result = ShowListEditor(owner, Locale("SettingsLists_Edit"),
            [Field(0, "SettingsLists_Name", IptvPlaylistParser.IsSensitiveUrl(Cell(values, 0))), .. fields]);
        if (result is not null && kind == SettingsListKind.TelegramPublishing) result[2] = botOptions.GetValueOrDefault(result[2], "");
        return result;
    }

    internal async Task UpdateImportedRowAsync(SettingsListKind kind, string[] values, object target)
    {
        string name = Cell(values, 0);
        string value = Cell(values, 1);
        switch (target)
        {
            case AiConnectionSettings ai:
                StageAiConnection(name, value, AiImportEndpoint(values, ai), Cell(values, 3), ReadEnabled(Cell(values, 4), ai.IsEnabled), ai);
                break;
            case IptvPlaylistSource iptv:
                iptv.Name = name;
                iptv.IsEnabled = ReadEnabled(Cell(values, 3), iptv.IsEnabled);
                RefreshIptvSourcesList();
                break;
            case BotAccount bot:
                await StageBotAsync(name, value, Cell(values, 2), bot);
                break;
            case ChannelAccount channel:
                string botId = FindImportBot(Cell(values, 2), channel.BotId)?.Id ?? throw new InvalidOperationException();
                if (botId != channel.BotId)
                    await StageChannelAsync(name, value, botId, Cell(values, 3), channel);
                else
                {
                    if (name.Length > 0) channel.DisplayName = name;
                    _telegramChannelCategories[channel.Id] = Cell(values, 3);
                    _changedChannels.Add(channel.Id);
                    RefreshTelegramSettingsLists();
                }
                break;
            case SettingsSourceEntry source when TryCreateSource(kind, values, source, out var entry):
                _sourceLists[kind][_sourceLists[kind].IndexOf(source)] = entry!;
                _sourceChecks.Remove(source.Id);
                break;
            default:
                throw new InvalidOperationException();
        }
    }

    private string? ShowListImport(SettingsListKind kind)
    {
        var dialog = new SettingsListDialog
        {
            Owner = this, Title = Locale("SettingsLists_Import"), Width = 600,
            SizeToContent = SizeToContent.Height, ResizeMode = ResizeMode.NoResize, WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        var panel = new StackPanel { Margin = new Thickness(18) };
        var sources = new WrapPanel { Margin = new Thickness(0, 0, 0, 8) };
        var open = new Button { Content = Locale("SettingsLists_OpenFile"), Margin = new Thickness(0, 0, 6, 4) };
        var paste = new Button { Content = Locale("SettingsLists_PasteImport"), Margin = new Thickness(0, 0, 6, 4) };
        var edit = new Button { Content = Locale("SettingsLists_EnterImport"), Margin = new Thickness(0, 0, 0, 4) };
        sources.Children.Add(open);
        sources.Children.Add(paste);
        sources.Children.Add(edit);
        panel.Children.Add(sources);
        panel.Children.Add(new TextBlock
        {
            Text = Locale(kind == SettingsListKind.Iptv ? "SettingsLists_IptvImportFormat" : "SettingsLists_ImportFormat"),
            TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 4)
        });
        panel.Children.Add(new TextBlock
        {
            Text = string.Join(" · ", ImportColumnLabels(kind).Select(Locale)), TextWrapping = TextWrapping.Wrap
        });
        var text = new TextBox
        {
            AcceptsReturn = true, AcceptsTab = true, Height = 164, Margin = new Thickness(0, 10, 0, 0),
            VerticalContentAlignment = VerticalAlignment.Stretch, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, Visibility = Visibility.Collapsed
        };
        System.Windows.Automation.AutomationProperties.SetName(text, Locale("SettingsLists_ImportText"));
        panel.Children.Add(text);
        var status = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0), Visibility = Visibility.Collapsed };
        System.Windows.Automation.AutomationProperties.SetLiveSetting(status, System.Windows.Automation.AutomationLiveSetting.Polite);
        panel.Children.Add(status);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = System.Windows.HorizontalAlignment.Right, Margin = new Thickness(0, 10, 0, 0) };
        var cancel = new Button { Content = Locale("Common_Cancel"), IsCancel = true, MinWidth = 90, Margin = new Thickness(0, 0, 8, 0) };
        var import = new Button { Content = Locale("SettingsLists_Preview"), IsDefault = true, IsEnabled = false, MinWidth = 90 };
        buttons.Children.Add(cancel);
        buttons.Children.Add(import);
        panel.Children.Add(buttons);

        string input = "";
        bool editing = false;
        bool loadingFile = false;
        string ReadInput() => editing ? text.Text : input;
        void RefreshActions()
        {
            sources.IsEnabled = text.IsEnabled = !loadingFile;
            import.IsEnabled = !loadingFile && !string.IsNullOrWhiteSpace(ReadInput());
        }
        void ShowStatus(string? key)
        {
            status.Text = key is null ? "" : Locale(key);
            status.Visibility = VisibleWhen(key is not null);
        }
        void SetEditing(bool visible)
        {
            if (editing && !visible) input = text.Text;
            editing = visible;
            text.Visibility = VisibleWhen(visible);
            text.Text = visible ? input : "";
            edit.Content = Locale(visible ? "SettingsLists_HideImport" :
                string.IsNullOrWhiteSpace(input) ? "SettingsLists_EnterImport" : "SettingsLists_EditImport");
            ShowStatus(!visible && !string.IsNullOrWhiteSpace(input) ? "SettingsLists_ImportReady" : null);
            RefreshActions();
            if (visible) text.Focus();
        }
        void SetInput(string candidate, string emptyMessage)
        {
            if (string.IsNullOrWhiteSpace(candidate))
            {
                ShowStatus(emptyMessage);
                return;
            }
            if (Encoding.UTF8.GetByteCount(candidate) > MaximumListFileSize)
            {
                ShowStatus("SettingsLists_FileTooLarge");
                return;
            }
            editing = false;
            input = candidate;
            SetEditing(false);
        }
        text.TextChanged += (_, _) =>
        {
            if (editing) ShowStatus(null);
            RefreshActions();
        };
        edit.Click += (_, _) => SetEditing(!editing);
        paste.Click += (_, _) =>
        {
            try { SetInput(Clipboard.GetText(), "SettingsLists_ClipboardEmpty"); }
            catch (System.Runtime.InteropServices.ExternalException) { ShowStatus("SettingsLists_ClipboardUnavailable"); }
        };
        import.Click += (_, _) =>
        {
            if (Encoding.UTF8.GetByteCount(ReadInput()) > MaximumListFileSize)
            {
                ShowStatus("SettingsLists_FileTooLarge");
                return;
            }
            dialog.DialogResult = true;
        };
        open.Click += async (_, _) =>
        {
            var picker = new OpenFileDialog { Filter = "TXT / Markdown|*.txt;*.md", CheckFileExists = true };
            if (picker.ShowDialog(dialog) != true) return;
            loadingFile = true;
            RefreshActions();
            try
            {
                if (new FileInfo(picker.FileName).Length > MaximumListFileSize)
                {
                    ShowStatus("SettingsLists_FileTooLarge");
                    return;
                }
                string contents = await File.ReadAllTextAsync(picker.FileName, _listOperations.Token);
                if (!dialog.IsVisible) return;
                SetInput(contents, "SettingsLists_EmptyImportFile");
            }
            catch (Exception)
            {
                if (dialog.IsVisible) ShowStatus("SettingsLists_InvalidEntry");
            }
            finally
            {
                loadingFile = false;
                RefreshActions();
            }
        };
        dialog.Content = panel;
        return dialog.ShowDialog() == true ? ReadInput() : null;
    }

    private static string[] ImportColumnLabels(SettingsListKind kind) => kind switch
    {
        SettingsListKind.Ai => ["SettingsLists_Name", "SettingsLists_Provider", "AiSettings_BaseUrl", "SettingsLists_Token", "SettingsLists_Enabled"],
        SettingsListKind.Iptv => ["SettingsLists_Name", "Iptv_Location", "SettingsLists_PlaylistType", "SettingsLists_Enabled"],
        SettingsListKind.TelegramBots => ["SettingsLists_Name", "SettingsLists_TelegramId", "SettingsLists_Token"],
        SettingsListKind.TelegramReading => ["SettingsLists_Name", "SettingsLists_ChannelAddress", "SettingsLists_Category"],
        SettingsListKind.TelegramPublishing => ["SettingsLists_Name", "SettingsLists_ChannelAddress", "SettingsLists_BotId", "SettingsLists_Category"],
        SettingsListKind.Rss => ["SettingsLists_Name", "SettingsLists_RssUrl", "SettingsLists_Category", "SettingsLists_Enabled"],
        _ => ["SettingsLists_Name", "SettingsLists_PromptText", "SettingsLists_Category"]
    };

    internal async Task<bool> ImportListRowAsync(SettingsListKind kind, string[] values)
    {
        if (FindImportTarget(kind, values) is not null) return false;
        string name = Cell(values, 0);
        string value = Cell(values, 1);
        switch (kind)
        {
            case SettingsListKind.Ai:
                if (_aiConnections.Any(item => item.DisplayName.Equals(name, StringComparison.OrdinalIgnoreCase))) return false;
                StageAiConnection(name, value, Cell(values, 2), Cell(values, 3), ReadEnabled(Cell(values, 4)));
                return true;
            case SettingsListKind.Iptv:
                return StageIptvSource(name, value, Cell(values, 2), ReadEnabled(Cell(values, 3)));
            case SettingsListKind.TelegramBots:
                if (_telegramBots.Any(bot => bot.TelegramBotId?.ToString(CultureInfo.InvariantCulture) == value ||
                    value.Length == 0 && name.Length > 0 && bot.DisplayName.Equals(name, StringComparison.OrdinalIgnoreCase))) return false;
                await StageBotAsync(name, value, Cell(values, 2), null);
                return true;
            case SettingsListKind.TelegramPublishing:
                if (!IsTelegramAddress(value)) throw new InvalidOperationException();
                string address = TelegramChannelService.NormalizeChannelIdentifier(value);
                if (_telegramChannels.Any(channel => ChannelAddress(channel) == address ||
                    ("@" + channel.Username).Equals(address, StringComparison.OrdinalIgnoreCase))) return false;
                string botId = FindImportBot(Cell(values, 2), null)?.Id ?? throw new InvalidOperationException();
                await StageChannelAsync(name, value, botId, Cell(values, 3), null);
                return true;
            default:
                if (name.Length == 0 && value.Length == 0) throw new InvalidOperationException();
                if (kind == SettingsListKind.TelegramReading && !IsTelegramAddress(value) ||
                    kind == SettingsListKind.Rss && !TryWebUri(value, out _) ||
                    kind == SettingsListKind.Prompts && (name.Length == 0 || value.Length == 0)) throw new InvalidOperationException();
                if (!TryCreateSource(kind, values, null, out SettingsSourceEntry? entry)) return false;
                _sourceLists[kind].Add(entry!);
                return true;
        }
    }

    private bool StageIptvSource(string name, string location, string kind, bool enabled)
    {
        if (name.Length == 0 || location.Length == 0 || _iptvStore is null) throw new InvalidOperationException();
        if (!TryIptvKind(kind, out var sourceKind) || !ValidIptvLocation(location, sourceKind)) throw new InvalidOperationException();
        if (_iptvDraft.Sources.Any(source => source.Kind == sourceKind &&
            string.Equals(_pendingIptvSecrets.GetValueOrDefault(source.Id) ?? source.Location, location, StringComparison.Ordinal))) return false;
        var entry = new IptvPlaylistSource { Name = name, Kind = sourceKind, Location = location, IsEnabled = enabled, SortOrder = _iptvDraft.Sources.Count };
        if (sourceKind == IptvSourceKind.InlineText)
        {
            if (new FileInfo(location).Length > MaximumListFileSize) throw new InvalidOperationException();
            entry.InlineContent = File.ReadAllText(location);
            entry.Location = "";
        }
        else if (sourceKind is IptvSourceKind.Url or IptvSourceKind.SingleStream && IptvPlaylistParser.IsSensitiveUrl(location))
        {
            _pendingIptvSecrets[entry.Id] = location;
            entry.Location = IptvPlaylistParser.MaskSensitiveUrl(location);
        }
        _iptvDraft.Sources.Add(entry);
        _changedIptvSourceIds.Add(entry.Id);
        RefreshIptvSourcesList();
        return true;
    }

    private static bool TryIptvKind(string kind, out IptvSourceKind sourceKind) =>
        Enum.TryParse(kind.Length == 0 ? nameof(IptvSourceKind.Url) : kind, true, out sourceKind) && Enum.IsDefined(sourceKind);

    private static bool ValidIptvLocation(string location, IptvSourceKind kind) =>
        kind is IptvSourceKind.Url or IptvSourceKind.SingleStream
            ? Uri.TryCreate(location, UriKind.Absolute, out Uri? uri) && uri.Scheme is "http" or "https"
            : File.Exists(location);

    private async void ExportSettingsList_Click(object sender, RoutedEventArgs e)
    {
        SettingsListKind kind = CurrentList;
        var dialog = new SaveFileDialog { Filter = "Markdown (*.md)|*.md|TXT (*.txt)|*.txt", DefaultExt = ".md", AddExtension = true, FileName = kind.ToString() };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            bool markdown = !Path.GetExtension(dialog.FileName).Equals(".txt", StringComparison.OrdinalIgnoreCase);
            await File.WriteAllTextAsync(dialog.FileName, SettingsListTextFormat.Write(kind, ExportListRows(kind), markdown), _listOperations.Token);
        }
        catch (Exception) when (!_listOperations.IsCancellationRequested) { ShowListMessage("SettingsLists_InvalidEntry"); }
        catch (OperationCanceledException) when (_listOperations.IsCancellationRequested) { }
    }

    private IEnumerable<string[]> ExportListRows(SettingsListKind kind)
    {
        return kind switch
        {
            SettingsListKind.Ai => OrderList(_aiConnections, _aiDisplayOrder, item => item.Id)
                .Select(item => new[] { item.DisplayName, item.ProviderId, PublicLocation(item.CustomBaseUrl), "", item.IsEnabled.ToString() }),
            SettingsListKind.Iptv => _iptvDraft.Sources.OrderBy(item => item.SortOrder)
                .Select(item => new[] { item.Name, item.Kind == IptvSourceKind.InlineText ? "" : PublicLocation(item.Location), item.Kind.ToString(), item.IsEnabled.ToString() }),
            SettingsListKind.TelegramBots => _telegramBots.Select(bot => new[] { bot.DisplayName, bot.TelegramBotId?.ToString(CultureInfo.InvariantCulture) ?? "", "" }),
            SettingsListKind.TelegramPublishing => _telegramChannels.Select(channel => new[] { channel.DisplayName, ChannelAddress(channel),
                _telegramBots.FirstOrDefault(bot => bot.Id == channel.BotId)?.TelegramBotId?.ToString(CultureInfo.InvariantCulture) ?? "",
                _telegramChannelCategories.GetValueOrDefault(channel.Id, "") }),
            SettingsListKind.Rss => _sourceLists[kind].Select(item => new[] { PublicLocation(item.Name), PublicLocation(item.Value), item.Category, item.IsEnabled.ToString() }),
            _ => _sourceLists[kind].Select(item => new[] { item.Name, item.Value, item.Category })
        };
    }

    private static string PublicLocation(string? value) => IptvPlaylistParser.IsSensitiveUrl(value) ? "" : value ?? "";
}
