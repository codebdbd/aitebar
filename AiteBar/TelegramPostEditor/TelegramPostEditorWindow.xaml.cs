using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Media;
using AiteBar.TelegramPostEditor.Models;
using AiteBar.TelegramPostEditor.Persistence;
using AiteBar.TelegramPostEditor.Services;

namespace AiteBar;

[SupportedOSPlatform("windows6.1")]
public partial class TelegramPostEditorWindow : DarkWindow
{
    private const double DefaultWidth = 1000;
    private const double DefaultHeight = 690;

    private readonly AppSettingsService _settingsService;
    private readonly MainWindow? _mainWindow;
    private readonly AiGateway _aiGateway;

    private readonly PostRepository _repository;
    private readonly PostAutosaveService _autosave;
    private readonly PostMediaStore _mediaStore;
    private readonly PostInstructionService _instructionService;
    private readonly PostAiService _aiService;
    private readonly TelegramSecretStore _secretStore;
    private readonly TelegramChannelService _channelService;
    private readonly PublicationService _publicationService;
    private readonly PublicationSyncService _syncService;
    private readonly PostBackupService _backupService;

    private readonly ObservableCollection<ChannelSelectionItem> _channelSelections = [];
    private PostDocument? _currentPost;
    private bool _isLoadingPost;
    private bool _changingDraft;
    private bool _closingAfterSave;
    private CancellationTokenSource? _aiCts;
    private PostAiSuggestion? _lastSuggestion;
    private TextRange? _aiReplacementRange;
    private long _editorVersion;
    private long _aiInputVersion;

    public TelegramPostEditorWindow(
        AppSettingsService settingsService,
        MainWindow? mainWindow,
        AiGateway aiGateway)
        : this(settingsService, mainWindow, aiGateway, new PostDbMigrator(), new TelegramSecretStore(), new TelegramBotClient())
    {
    }

    internal TelegramPostEditorWindow(
        AppSettingsService settingsService, MainWindow? mainWindow, AiGateway aiGateway,
        PostDbMigrator migrator, TelegramSecretStore secretStore, TelegramBotClient botClient,
        ITelegramAiGateway? writingGateway = null, HttpClient? schedulingClient = null)
    {
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _mainWindow = mainWindow;
        _aiGateway = aiGateway ?? throw new ArgumentNullException(nameof(aiGateway));

        _repository = new PostRepository(migrator);
        _repository.EnsureBuiltInTemplates();
        _autosave = new PostAutosaveService(_repository);
        _mediaStore = new PostMediaStore(migrator, Path.Combine(Path.GetDirectoryName(migrator.DatabasePath)!, "Media"));
        _instructionService = new PostInstructionService(migrator);
        _instructionService.EnsureBuiltInInstructions();
        _aiService = writingGateway == null
            ? new PostAiService(_aiGateway, _instructionService)
            : new PostAiService(writingGateway, _instructionService);
        _secretStore = secretStore;
        _channelService = new TelegramChannelService(migrator, botClient, _secretStore);
        _publicationService = new PublicationService(migrator, _mediaStore, botClient, _secretStore, _channelService);
        _syncService = new PublicationSyncService(
            schedulingClient ?? new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(45) },
            _secretStore, _mediaStore);
        _backupService = new PostBackupService(_repository, _mediaStore);

        InitializeComponent();

        LstChannels.ItemsSource = _channelSelections;
        TxtServerUrl.Text = _settingsService.Settings.TelegramPostEditorServerUrl ?? string.Empty;
        UpdatePairingStatus();
        DateTime defaultTime = DateTime.Now.AddHours(1);
        ScheduleDate.SelectedDate = defaultTime.Date;
        ScheduleTime.Text = defaultTime.ToString("HH:mm", CultureInfo.InvariantCulture);
        ScheduleZone.Text = LocalizationService.Format("TelegramComposer_ScheduleZone", TimeZoneInfo.Local.DisplayName);
        _autosave.Saved += (_, saved) => Dispatcher.BeginInvoke(() =>
        {
            if (ReferenceEquals(saved, _currentPost))
            {
                TxtStatusBar.Text = LocalizationService.Get("QuickNote_SavedInitial");
            }
        });
        _autosave.SaveFailed += (_, _) => Dispatcher.BeginInvoke(() =>
            TxtStatusBar.Text = LocalizationService.Get("TelegramComposer_SaveError"));
        CmbLibraryFilter.ItemsSource = new[]
        {
            LocalizationService.Get("TelegramPostEditor_AllDrafts"),
            LocalizationService.Get("TelegramPostEditor_Trash")
        };
        CmbLibraryFilter.SelectedIndex = 0;

        CmbLibrarySort.ItemsSource = new[]
        {
            LocalizationService.Get("TelegramPostEditor_SortByDate"),
            LocalizationService.Get("TelegramPostEditor_SortByTitle")
        };
        CmbLibrarySort.SelectedIndex = 0;

        InitializeAiControls();
        CmbInstruction.SelectionChanged += (_, _) => SyncCurrentPostFromUi(scheduleAutosave: true);
        CreateAndLoadNewPost();
        RefreshChannelsAndBots();
        Activated += (_, _) =>
        {
            if (!_publicationBusy && PublishPage.IsVisible)
                RefreshChannelsAndBots();
        };
    }

    internal AiGateway AiGateway => _aiGateway;

    public void ShowWorkspace(AppSettingsService settingsService)
    {
        RestoreWindowPlacement(settingsService.Settings);
        Show();
        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }
        Activate();
        PostEditorRichTextBox.Focus();
    }

    private void InitializeAiControls()
    {
        var actionItems = new List<PostAiActionItem>
        {
            new(PostAiAction.Improve, LocalizationService.Get("TelegramPostEditor_AiAction_Improve")),
            new(PostAiAction.FixGrammar, LocalizationService.Get("TelegramPostEditor_AiAction_FixGrammar")),
            new(PostAiAction.Shorten, LocalizationService.Get("TelegramPostEditor_AiAction_Shorten")),
            new(PostAiAction.FormatForTelegram, LocalizationService.Get("TelegramPostEditor_AiAction_FormatForTelegram")),
            new(PostAiAction.RewriteStyle, LocalizationService.Get("TelegramPostEditor_AiAction_RewriteStyle")),
            new(PostAiAction.SuggestTitles, LocalizationService.Get("TelegramPostEditor_AiAction_SuggestTitles")),
            new(PostAiAction.AdaptToChannel, LocalizationService.Get("TelegramPostEditor_AiAction_AdaptToChannel")),
            new(PostAiAction.Translate, LocalizationService.Get("TelegramPostEditor_AiAction_Translate")),
            new(PostAiAction.CreateFromBrief, LocalizationService.Get("TelegramPostEditor_AiAction_CreateFromBrief")),
            new(PostAiAction.Alternatives, LocalizationService.Get("TelegramPostEditor_AiAction_Alternatives"))
        };
        CmbAiAction.ItemsSource = actionItems;
        CmbAiAction.SelectedIndex = 0;

        var modelChoices = new List<PostAiModelChoice>
        {
            new(null, null, null, LocalizationService.Get("TelegramPostEditor_AutoModel"), IsAuto: true)
        };
        var connections = _settingsService.Settings.Ai.Connections.Where(connection => connection.IsEnabled).ToList();
        var repeatedChoices = connections.GroupBy(connection => (connection.ProviderId, connection.PreferredModelId))
            .Where(group => group.Count() > 1).Select(group => group.Key).ToHashSet();
        foreach (AiConnectionSettings conn in connections)
        {
            string provider = AiProviderCatalog.TryGet(conn.ProviderId, out AiProviderDefinition definition)
                ? definition.DisplayName : conn.ProviderId;
            string model = conn.PreferredModelId ?? LocalizationService.Get("TelegramPostEditor_AutoModel");
            string label = $"{model} · {provider}";
            if (repeatedChoices.Contains((conn.ProviderId, conn.PreferredModelId))) label += $" · {conn.DisplayName}";
            modelChoices.Add(new PostAiModelChoice(conn.Id, conn.ProviderId, conn.PreferredModelId, label));
        }
        CmbAiModel.ItemsSource = modelChoices;
        CmbAiModel.DisplayMemberPath = nameof(PostAiModelChoice.DisplayName);
        CmbAiModel.SelectedIndex = 0;

        var instructions = new List<PostInstruction>
        {
            new()
            {
                Id = string.Empty,
                Title = LocalizationService.Get("TelegramPostEditor_NoInstruction")
            }
        };
        instructions.AddRange(_instructionService.GetAll());
        CmbInstruction.ItemsSource = instructions;
        CmbInstruction.SelectedIndex = 0;
    }

    private void RefreshPostsList()
    {
        bool includeDeleted = CmbLibraryFilter?.SelectedIndex == 1;
        IReadOnlyList<PostDocument> posts = _repository.SearchPosts(new PostSearchFilter
        {
            SearchText = TxtSearchPosts.Text,
            IncludeDeleted = includeDeleted
        });
        if (CmbLibrarySort?.SelectedIndex == 1)
        {
            posts = posts.OrderBy(p => p.Title, StringComparer.CurrentCultureIgnoreCase).ToList();
        }
        LstPosts.ItemsSource = posts;
        LstPosts.SelectedItem = posts.FirstOrDefault(post => post.Id == _currentPost?.Id);
        DraftSelection_Changed(this, null);
    }

    private void RefreshChannelsAndBots()
    {
        HashSet<string> previouslySelected = _channelSelections
            .Where(c => c.IsSelected)
            .Select(c => c.Channel.Id)
            .ToHashSet(StringComparer.Ordinal);

        _channelSelections.Clear();
        IReadOnlyList<BotAccount> bots = _channelService.GetBots();
        Dictionary<string, BotAccount> botsById = bots.ToDictionary(b => b.Id, StringComparer.Ordinal);

        if (bots.Count == 0)
        {
            TxtConnectedBotsSummary.Text = LocalizationService.Get("TelegramSettings_NoBots");
            TxtConnectedBotsSummary.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xE8, 0x9B, 0x4E));
        }
        else
        {
            string botNames = string.Join(", ", bots.Select(b =>
                string.IsNullOrWhiteSpace(b.Username) ? b.DisplayName : $"@{b.Username.TrimStart('@')}"));
            TxtConnectedBotsSummary.Text = $"{string.Format(LocalizationService.Get("TelegramPostEditor_ConnectedBotsSummary"), bots.Count)} ({botNames})";
            TxtConnectedBotsSummary.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x88, 0xD4, 0xA2));
        }

        IReadOnlyList<ChannelAccount> channels = _channelService.GetChannels();
        TxtNoChannelsHint.Visibility = channels.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        QuickConnectPanel.IsExpanded |= channels.Count == 0;
        var displayOrder = _settingsService.Settings.TelegramChannelDisplayOrder;
        foreach (ChannelAccount ch in channels.OrderBy(channel =>
            displayOrder.IndexOf(channel.Id) is int index and >= 0 ? index : int.MaxValue))
        {
            bool isSelected = previouslySelected.Contains(ch.Id);
            botsById.TryGetValue(ch.BotId, out BotAccount? boundBot);
            bool hasToken = !string.IsNullOrWhiteSpace(_secretStore.ReadBotToken(ch.BotId));
            string subtitle;
            if (!hasToken)
            {
                subtitle = LocalizationService.Get("TelegramPostEditor_ChannelMissingBotToken");
            }
            else
            {
                subtitle = boundBot == null || string.IsNullOrWhiteSpace(boundBot.Username)
                    ? boundBot?.DisplayName ?? string.Empty
                    : $"@{boundBot.Username.TrimStart('@')}";
            }

            _channelSelections.Add(new ChannelSelectionItem(ch, isSelected, subtitle, hasToken));
        }
        RefreshPublishingCategories();
        UpdatePublicationControls();
    }

    private sealed record PublishingCategory(string? Key, string Label);

    private void RefreshPublishingCategories()
    {
        var categories = _settingsService.Settings.TelegramPublishingCategories;
        string? current = (CmbPublishingCategory.SelectedItem as PublishingCategory)?.Key;
        var options = new List<PublishingCategory> { new(null, LocalizationService.Get("SettingsLists_AllCategories")) };
        options.AddRange(_channelSelections
            .Select(item => categories.GetValueOrDefault(item.Channel.Id, ""))
            .Distinct(StringComparer.CurrentCultureIgnoreCase)
            .OrderBy(category => category, StringComparer.CurrentCultureIgnoreCase)
            .Select(category => new PublishingCategory(category,
                category.Length == 0 ? LocalizationService.Get("SettingsLists_NoCategory") : category)));
        CmbPublishingCategory.ItemsSource = options;
        CmbPublishingCategory.SelectedItem = options.FirstOrDefault(option => option.Key == current) ?? options[0];
        CmbPublishingCategory.Visibility = options.Count > 2 || options.Count == 2 && options[1].Key?.Length > 0
            ? Visibility.Visible : Visibility.Collapsed;
    }

    private void PublishingCategory_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (LstChannels is null || LstChannels.ItemsSource is null) return;
        string? selected = (CmbPublishingCategory.SelectedItem as PublishingCategory)?.Key;
        var categories = _settingsService.Settings.TelegramPublishingCategories;
        var view = System.Windows.Data.CollectionViewSource.GetDefaultView(_channelSelections);
        view.Filter = selected is null ? null : item => item is ChannelSelectionItem channel &&
            string.Equals(categories.GetValueOrDefault(channel.Channel.Id, ""), selected, StringComparison.CurrentCultureIgnoreCase);
    }

    private void CreateAndLoadNewPost()
    {
        var post = new PostDocument { Title = LocalizationService.Get("TelegramPostEditor_NewPost") };
        LoadPostIntoEditor(post, isNew: true);
    }

    private void LoadPostIntoEditor(PostDocument post, bool isNew = false)
    {
        _isLoadingPost = true;
        try
        {
            if (_currentPost?.Id != post.Id)
            {
                _aiCts?.Cancel();
                _lastSuggestion = null;
                AiSuggestionPanel.Visibility = Visibility.Collapsed;
                AiOptionsPanel.Visibility = Visibility.Collapsed;
                AiToggleButton.Tag = null;
                AiPromptBox.Clear();
                SendNowOption.IsChecked = true;
                _publicationError = null;
                foreach (ChannelSelectionItem item in _channelSelections)
                    item.IsSelected = false;
            }
            _currentPost = post;
            TxtPostTitle.Text = isNew ? string.Empty : post.Title;
            TxtStatusBar.Text = isNew ? string.Empty : LocalizationService.Get("QuickNote_SavedInitial");
            SetDraftManagementEnabled(!isNew);
            TxtPostTitlePlaceholder.Visibility = string.IsNullOrEmpty(TxtPostTitle.Text) ? Visibility.Visible : Visibility.Collapsed;
            PostEditorRichTextBox.Document = TelegramDocumentSerializer.ToFlowDocument(post);
            SelectInstructionInCombo(post.InstructionId);
            RefreshAttachedMediaList();
            UpdatePreviewAndValidation();
            DeleteDraftMenuItem.Header = post.DeletedUtc.HasValue
                ? LocalizationService.Get("TelegramPostEditor_Restore")
                : LocalizationService.Get("TelegramPostEditor_MoveToTrash");
        }
        finally
        {
            _isLoadingPost = false;
        }
    }

    private void SelectInstructionInCombo(string? instructionId)
    {
        if (CmbInstruction.ItemsSource is not IEnumerable<PostInstruction> items)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(instructionId))
        {
            CmbInstruction.SelectedIndex = 0;
            return;
        }

        PostInstruction? match = items.FirstOrDefault(i => string.Equals(i.Id, instructionId, StringComparison.Ordinal));
        CmbInstruction.SelectedItem = match ?? items.FirstOrDefault();
    }

    private void RefreshAttachedMediaList()
    {
        if (_currentPost == null)
        {
            LstAttachedMedia.ItemsSource = null;
            return;
        }

        var items = _currentPost.Media
            .OrderBy(m => m.SortOrder)
            .Select(m => _mediaStore.GetById(m.MediaId))
            .Where(m => m != null)
            .Cast<PostMediaItem>()
            .ToList();
        LstAttachedMedia.ItemsSource = items;
        _previewMediaDirty = true;
    }

    private void SyncCurrentPostFromUi(bool scheduleAutosave)
    {
        if (_isLoadingPost || _currentPost == null)
        {
            return;
        }

        _currentPost.Title = string.IsNullOrWhiteSpace(TxtPostTitle.Text)
            ? LocalizationService.Get("TelegramPostEditor_NewPost")
            : TxtPostTitle.Text.Trim();
        TxtPostTitlePlaceholder.Visibility = string.IsNullOrEmpty(TxtPostTitle.Text) ? Visibility.Visible : Visibility.Collapsed;
        _currentPost.Blocks = TelegramDocumentSerializer.FromFlowDocument(PostEditorRichTextBox.Document);
        if (CmbInstruction.SelectedItem is PostInstruction inst)
        {
            _currentPost.InstructionId = string.IsNullOrWhiteSpace(inst.Id) ? null : inst.Id;
        }

        UpdatePreviewAndValidation();

        if (scheduleAutosave)
        {
            TxtStatusBar.Text = LocalizationService.Get("TelegramPostEditor_StatusSaving");
            _autosave.ScheduleSave(_currentPost);
            SetDraftManagementEnabled(true);
        }
    }

    private void SetDraftManagementEnabled(bool enabled)
    {
        DuplicateDraftMenuItem.IsEnabled = enabled;
        DeleteDraftMenuItem.IsEnabled = enabled;
    }

    private async void BtnNewPost_Click(object sender, RoutedEventArgs e)
    {
        await ChangeDraftAsync(CreateAndLoadNewPost);
    }

    private async void BtnSavePost_Click(object sender, RoutedEventArgs e)
    {
        if (_currentPost is not { } post) return;
        SyncCurrentPostFromUi(scheduleAutosave: false);
        await ChangeDraftAsync(() =>
        {
            _repository.SavePost(post, createRevision: true);
            TxtStatusBar.Text = LocalizationService.Get("QuickNote_SavedInitial");
            SetDraftManagementEnabled(true);
        });
    }

    private async void BtnFromTemplate_Click(object sender, RoutedEventArgs e)
    {
        IReadOnlyList<PostTemplate> templates = _repository.GetTemplates();
        if (templates.Count == 0) return;
        await ChangeDraftAsync(() => LoadPostIntoEditor(_repository.CreateDraftFromTemplate(templates[0].Id)));
    }

    private void BtnExportJson_Click(object sender, RoutedEventArgs e)
    {
        if (_currentPost == null) return;
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "JSON (*.json)|*.json",
            FileName = $"{_currentPost.Title}.json"
        };
        if (dlg.ShowDialog(this) == true)
        {
            File.WriteAllText(dlg.FileName, _backupService.ExportPostToJson(_currentPost));
            TxtStatusBar.Text = "Пост экспортирован в JSON (без секретов).";
        }
    }

    private void TxtSearchPosts_TextChanged(object sender, TextChangedEventArgs e)
    {
        TxtSearchPlaceholder.Visibility = string.IsNullOrEmpty(TxtSearchPosts.Text)
            ? Visibility.Visible
            : Visibility.Collapsed;
        RefreshPostsList();
    }

    private void BtnToggleAi_Click(object sender, RoutedEventArgs e)
    {
        AiOptionsPanel.Visibility = AiOptionsPanel.Visibility == Visibility.Visible
            ? Visibility.Collapsed
            : Visibility.Visible;
        AiToggleButton.Tag = AiOptionsPanel.Visibility == Visibility.Visible ? "Active" : null;
        if (AiOptionsPanel.Visibility == Visibility.Visible)
        {
            UpdateAiScope();
            _ = Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input, new Action(() =>
            {
                if (AiPromptBox.IsVisible)
                    AiPromptBox.Focus();
            }));
        }
    }

    private void ChannelSelection_Changed(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.CheckBox { DataContext: ChannelSelectionItem item } checkBox)
        {
            item.IsSelected = checkBox.IsChecked == true;
            _publicationError = null;
            UpdatePublicationControls();
        }
    }
    private void CmbLibraryFilter_SelectionChanged(object sender, SelectionChangedEventArgs e) => RefreshPostsList();
    private void CmbLibrarySort_SelectionChanged(object sender, SelectionChangedEventArgs e) => RefreshPostsList();

    private async void BtnDuplicatePost_Click(object sender, RoutedEventArgs e)
    {
        if (_currentPost is { } post)
        {
            await ChangeDraftAsync(() => LoadPostIntoEditor(_repository.DuplicatePost(post.Id)));
        }
    }

    private async void BtnDeleteOrRestorePost_Click(object sender, RoutedEventArgs e)
    {
        if (_currentPost is not { } post) return;
        await ChangeDraftAsync(() =>
        {
            if (post.DeletedUtc.HasValue)
                _repository.RestorePost(post.Id);
            else
                _repository.SoftDeletePost(post.Id);

            LoadPostIntoEditor(_repository.GetPost(post.Id) ?? post);
        }, showEditor: false);
        ShowDrafts_Click(sender, e);
    }

    private void EditorMetadata_Changed(object sender, RoutedEventArgs e) => SyncCurrentPostFromUi(scheduleAutosave: true);
    private void PostEditorRichTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        _editorVersion++;
        SyncCurrentPostFromUi(scheduleAutosave: true);
        UpdateAiScope();
    }

    private void PostSelection_Changed(object sender, RoutedEventArgs e) => UpdateAiScope();

    private void UpdateAiScope()
    {
        if (AiScopeLabel == null) return;
        TextSelection selection = PostEditorRichTextBox.Selection;
        bool isExcerpt = !string.IsNullOrWhiteSpace(selection.Text);
        bool isEmpty = string.IsNullOrWhiteSpace(new TextRange(
            PostEditorRichTextBox.Document.ContentStart, PostEditorRichTextBox.Document.ContentEnd).Text);
        AiScopeLabel.Text = isEmpty ? LocalizationService.Get("TelegramComposer_AiBrief")
            : LocalizationService.Format("TelegramComposer_AiInputContext",
                LocalizationService.Get(isExcerpt ? "TelegramComposer_AiSelection" : "TelegramComposer_AiWholePost"));
        string aiLabel = LocalizationService.Get(isEmpty ? "TelegramComposer_AiStart" : "TelegramComposer_Assistant");
        AiToggleButton.ToolTip = aiLabel;
        System.Windows.Automation.AutomationProperties.SetName(AiToggleButton, aiLabel);
        AiDraftButton.Visibility = isEmpty ? Visibility.Visible : Visibility.Collapsed;
        AiShortenButton.Visibility = AiRewriteButton.Visibility = isEmpty ? Visibility.Collapsed : Visibility.Visible;
    }

    private void BtnBold_Click(object sender, RoutedEventArgs e) => EditingCommands.ToggleBold.Execute(null, PostEditorRichTextBox);
    private void BtnItalic_Click(object sender, RoutedEventArgs e) => EditingCommands.ToggleItalic.Execute(null, PostEditorRichTextBox);
    private void BtnUnderline_Click(object sender, RoutedEventArgs e) => EditingCommands.ToggleUnderline.Execute(null, PostEditorRichTextBox);

    private void BtnStrike_Click(object sender, RoutedEventArgs e)
    {
        TextSelection sel = PostEditorRichTextBox.Selection;
        if (sel != null && !sel.IsEmpty)
        {
            sel.ApplyPropertyValue(Inline.TextDecorationsProperty, TextDecorations.Strikethrough);
            SyncCurrentPostFromUi(scheduleAutosave: true);
        }
    }

    private void BtnSpoiler_Click(object sender, RoutedEventArgs e)
    {
        TextSelection sel = PostEditorRichTextBox.Selection;
        if (sel != null && !sel.IsEmpty)
        {
            sel.ApplyPropertyValue(TelegramDocumentSerializer.IsSpoilerProperty, true);
            sel.ApplyPropertyValue(TextElement.BackgroundProperty, new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x3A, 0x3A, 0x42)));
            SyncCurrentPostFromUi(scheduleAutosave: true);
        }
    }

    private void BtnInlineCode_Click(object sender, RoutedEventArgs e)
    {
        TextSelection sel = PostEditorRichTextBox.Selection;
        if (sel != null && !sel.IsEmpty)
        {
            sel.ApplyPropertyValue(TelegramDocumentSerializer.IsInlineCodeProperty, true);
            sel.ApplyPropertyValue(TextElement.FontFamilyProperty, new System.Windows.Media.FontFamily("JetBrains Mono, Consolas"));
            SyncCurrentPostFromUi(scheduleAutosave: true);
        }
    }

    private void BtnQuote_Click(object sender, RoutedEventArgs e)
    {
        Paragraph? p = PostEditorRichTextBox.CaretPosition?.Paragraph;
        if (p != null)
        {
            PostBlockType next = TelegramDocumentSerializer.GetBlockType(p) == PostBlockType.Quote
                ? PostBlockType.Paragraph
                : PostBlockType.Quote;
            TelegramDocumentSerializer.SetBlockType(p, next);
            SyncCurrentPostFromUi(scheduleAutosave: true);
        }
    }

    private void BtnAddMedia_Click(object sender, RoutedEventArgs e)
    {
        if (_currentPost == null) return;
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Multiselect = true,
            Filter = "Медиа и документы|*.jpg;*.jpeg;*.png;*.webp;*.gif;*.mp4;*.mov;*.pdf;*.txt|*.*|*.*"
        };
        if (dlg.ShowDialog(this) != true) return;

        foreach (string file in dlg.FileNames)
        {
            try
            {
                PostMediaItem item = _mediaStore.ImportFile(file);
                _currentPost.Media.Add(new PostMediaAttachment
                {
                    MediaId = item.Id,
                    SortOrder = _currentPost.Media.Count
                });
            }
            catch (Exception ex)
            {
                new DarkDialog(ex.Message) { Owner = this }.ShowDialog();
            }
        }

        RefreshAttachedMediaList();
        SyncCurrentPostFromUi(scheduleAutosave: true);
    }

    private void BtnRemoveMedia_Click(object sender, RoutedEventArgs e)
    {
        if (_currentPost == null || sender is not System.Windows.Controls.Button btn || btn.Tag is not string mediaId) return;
        _currentPost.Media.RemoveAll(m => m.MediaId == mediaId);
        RefreshAttachedMediaList();
        SyncCurrentPostFromUi(scheduleAutosave: true);
    }

    private async void QuickAi_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string tag } && Enum.TryParse(tag, out PostAiAction action))
            await RunAiAsync(action);
    }

    private async void BtnRunAi_Click(object sender, RoutedEventArgs e)
    {
        await RunAiAsync(CmbAiAction.SelectedItem is PostAiActionItem actionItem
            ? actionItem.Action : PostAiAction.Improve);
    }

    private async Task RunAiAsync(PostAiAction action)
    {
        if (_currentPost is not { } post || _aiCts != null) return;
        SyncCurrentPostFromUi(scheduleAutosave: false);

        TextSelection selection = PostEditorRichTextBox.Selection;
        bool isExcerpt = !string.IsNullOrWhiteSpace(selection.Text);
        var range = isExcerpt ? new TextRange(selection.Start, selection.End) : null;
        string source = range?.Text ?? TelegramDocumentSerializer.ExtractPlainText(post);
        string prompt = AiPromptBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(source) && (action != PostAiAction.CreateFromBrief || prompt.Length == 0))
        {
            _lastSuggestion = null;
            ApplyAiButton.IsEnabled = false;
            AiSuggestionPanel.Visibility = Visibility.Visible;
            TxtAiSuggestion.Clear();
            AiOriginalText.Visibility = Visibility.Collapsed;
            TxtAiMeta.Text = LocalizationService.Get("TelegramComposer_AiBrief");
            AiPromptBox.Focus();
            return;
        }

        PostAiModelChoice choice = CmbAiModel.SelectedItem as PostAiModelChoice
            ?? new PostAiModelChoice(null, null, null, LocalizationService.Get("TelegramPostEditor_AutoModel"), IsAuto: true);
        string? instId = CmbInstruction.SelectedItem is PostInstruction inst && !string.IsNullOrWhiteSpace(inst.Id)
            ? inst.Id
            : null;
        post.InstructionId = instId;
        var input = new PostDocument
        {
            InstructionId = instId,
            Blocks = range == null ? post.Blocks : [new PostBlock { Runs = [new PostTextRun { Text = source }] }]
        };
        long inputVersion = _editorVersion;
        using var operation = new CancellationTokenSource();
        _aiCts = operation;
        _lastSuggestion = null;

        BtnRunAi.IsEnabled = AiDraftButton.IsEnabled = AiShortenButton.IsEnabled = AiRewriteButton.IsEnabled = false;
        BtnStopAi.IsEnabled = true;
        BtnStopAi.Visibility = Visibility.Visible;
        ApplyAiButton.IsEnabled = false;
        AiSuggestionPanel.Visibility = Visibility.Visible;
        TxtAiSuggestion.Text = string.Empty;
        AiOriginalText.Text = isExcerpt ? source : string.Empty;
        AiOriginalText.Visibility = isExcerpt ? Visibility.Visible : Visibility.Collapsed;
        TxtAiMeta.Text = LocalizationService.Get("TelegramPostEditor_AiGenerating");

        var progress = new Progress<string>(text =>
        {
            if (ReferenceEquals(_aiCts, operation) && !operation.IsCancellationRequested && _currentPost?.Id == post.Id)
                TxtAiSuggestion.Text = text;
        });
        try
        {
            PostAiSuggestion suggestion = await _aiService.GenerateSuggestionAsync(
                input,
                action,
                choice,
                instId,
                userPrompt: prompt,
                streamingProgress: progress,
                cancellationToken: operation.Token,
                isExcerpt: isExcerpt);

            if (operation.IsCancellationRequested || _currentPost?.Id != post.Id) return;
            _lastSuggestion = suggestion;
            _aiReplacementRange = range;
            _aiInputVersion = inputVersion;
            TxtAiSuggestion.Text = suggestion.Content;
            TxtAiMeta.Text = LocalizationService.Get(isExcerpt ? "TelegramComposer_AiSelection" : "TelegramComposer_AiWholePost");
            ApplyAiButton.Content = LocalizationService.Get(isExcerpt ? "TelegramComposer_AiReplace" : "TelegramPostEditor_AiApply");
            ApplyAiButton.IsEnabled = !string.IsNullOrWhiteSpace(suggestion.Content);
        }
        catch (OperationCanceledException)
        {
            if (_currentPost?.Id == post.Id)
                TxtAiMeta.Text = LocalizationService.Get("Common_Cancel");
        }
        catch (Exception ex)
        {
            if (_currentPost?.Id == post.Id)
                TxtAiMeta.Text = ex.Message;
        }
        finally
        {
            _aiCts = null;
            BtnRunAi.IsEnabled = AiDraftButton.IsEnabled = AiShortenButton.IsEnabled = AiRewriteButton.IsEnabled = true;
            BtnStopAi.IsEnabled = false;
            BtnStopAi.Visibility = Visibility.Collapsed;
        }
    }

    private void BtnStopAi_Click(object sender, RoutedEventArgs e) => _aiCts?.Cancel();

    private async void BtnApplyAi_Click(object sender, RoutedEventArgs e)
    {
        if (_currentPost is not { } post || _lastSuggestion is not { } suggestion || string.IsNullOrWhiteSpace(suggestion.Content)) return;
        await ChangeDraftAsync(() =>
        {
            if (_aiInputVersion != _editorVersion)
            {
                TxtAiMeta.Text = LocalizationService.Get("TelegramComposer_AiStale");
                ApplyAiButton.IsEnabled = false;
                return;
            }

            post.AiGenerationMetadata = suggestion.Metadata;
            if (_aiReplacementRange is { } range)
            {
                range.Text = suggestion.Content;
                SyncCurrentPostFromUi(scheduleAutosave: false);
                _repository.SavePost(post, createRevision: true);
                BtnDismissAi_Click(sender, e);
                return;
            }

            List<PostBlock> parsedBlocks = TelegramDocumentSerializer.ParseMarkdownToBlocks(suggestion.Content);
            post.Blocks = parsedBlocks.Count > 0
                ? parsedBlocks
                : [new PostBlock { Runs = [new PostTextRun { Text = suggestion.Content.Trim() }] }];
            _repository.SavePost(post, createRevision: true);
            LoadPostIntoEditor(post);
            BtnDismissAi_Click(sender, e);
        });
    }

    private void BtnDismissAi_Click(object sender, RoutedEventArgs e)
    {
        _aiCts?.Cancel();
        AiSuggestionPanel.Visibility = Visibility.Collapsed;
        _lastSuggestion = null;
        _aiReplacementRange = null;
    }

    private async void BtnQuickConnectBot_Click(object sender, RoutedEventArgs e)
    {
        string token = PwdQuickBotToken.Password;
        if (string.IsNullOrWhiteSpace(token))
        {
            return;
        }

        BtnQuickConnectBot.IsEnabled = false;
        try
        {
            await _channelService.ConnectBotAsync(token);
            PwdQuickBotToken.Clear();
            RefreshChannelsAndBots();
        }
        catch (Exception ex)
        {
            new DarkDialog(ex.Message) { Owner = this }.ShowDialog();
        }
        finally
        {
            BtnQuickConnectBot.IsEnabled = true;
        }
    }

    private async void BtnQuickConnectChannel_Click(object sender, RoutedEventArgs e)
    {
        string input = TxtQuickChannelInput.Text;
        if (string.IsNullOrWhiteSpace(input))
        {
            return;
        }

        BtnQuickConnectChannel.IsEnabled = false;
        try
        {
            if (!string.IsNullOrWhiteSpace(PwdQuickBotToken.Password))
            {
                await _channelService.ConnectBotAsync(PwdQuickBotToken.Password);
                PwdQuickBotToken.Clear();
            }

            await _channelService.ConnectChannelAsync(input, ChkQuickTestChannel.IsChecked == true);
            TxtQuickChannelInput.Clear();
            RefreshChannelsAndBots();
        }
        catch (Exception ex)
        {
            new DarkDialog(ex.Message) { Owner = this }.ShowDialog();
        }
        finally
        {
            BtnQuickConnectChannel.IsEnabled = true;
        }
    }

    private async void BtnOpenTelegramSettings_Click(object sender, RoutedEventArgs e)
    {
        MainWindow? main = _mainWindow
            ?? Owner as MainWindow
            ?? System.Windows.Application.Current?.MainWindow as MainWindow;
        if (main != null)
        {
            await main.ShowAppSettingsWindow(AppSettingsSection.Telegram);
        }
    }

    private void RestoreWindowPlacement(AppSettings settings)
    {
        Rectangle work = GetTargetWorkArea(settings);
        double maxWidth = Math.Max(MinWidth, work.Width - 24);
        double maxHeight = Math.Max(MinHeight, work.Height - 24);
        Width = Math.Clamp(settings.TelegramPostEditorWidth ?? DefaultWidth, MinWidth, maxWidth);
        Height = Math.Clamp(settings.TelegramPostEditorHeight ?? DefaultHeight, MinHeight, maxHeight);

        double desiredLeft = settings.TelegramPostEditorLeft ?? (work.Left + (work.Width - Width) / 2);
        double desiredTop = settings.TelegramPostEditorTop ?? (work.Top + (work.Height - Height) / 2);
        Left = Math.Clamp(desiredLeft, work.Left, Math.Max(work.Left, work.Right - Width));
        Top = Math.Clamp(desiredTop, work.Top, Math.Max(work.Top, work.Bottom - Height));
        WindowState = string.Equals(settings.TelegramPostEditorWindowState, nameof(WindowState.Maximized), StringComparison.Ordinal)
            ? WindowState.Maximized
            : WindowState.Normal;
    }

    private static Rectangle GetTargetWorkArea(AppSettings settings)
    {
        Screen[] screens = Screen.AllScreens;
        if (screens.Length == 0) return new Rectangle(0, 0, 1360, 840);
        if (settings.TelegramPostEditorLeft.HasValue && settings.TelegramPostEditorTop.HasValue)
        {
            var pt = new System.Drawing.Point((int)Math.Round(settings.TelegramPostEditorLeft.Value + 40), (int)Math.Round(settings.TelegramPostEditorTop.Value + 40));
            return Screen.FromPoint(pt).WorkingArea;
        }
        return screens[Math.Clamp(settings.MonitorIndex, 0, screens.Length - 1)].WorkingArea;
    }

    private void PersistWindowPlacement()
    {
        if (!IsLoaded) return;
        AppSettings settings = _settingsService.Settings;
        Rect bounds = WindowState == WindowState.Normal ? new Rect(Left, Top, Width, Height) : RestoreBounds;
        if (bounds.Width >= MinWidth && bounds.Height >= MinHeight)
        {
            settings.TelegramPostEditorLeft = bounds.Left;
            settings.TelegramPostEditorTop = bounds.Top;
            settings.TelegramPostEditorWidth = bounds.Width;
            settings.TelegramPostEditorHeight = bounds.Height;
        }
        settings.TelegramPostEditorWindowState = WindowState == WindowState.Maximized ? nameof(WindowState.Maximized) : nameof(WindowState.Normal);
    }

    private async void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (_closingAfterSave) return;
        e.Cancel = true;
        if (_publicationBusy || _changingDraft) return;
        _changingDraft = true;
        _aiCts?.Cancel();
        try
        {
            await _autosave.FlushAsync();
            PersistWindowPlacement();
            await _settingsService.SaveAsync();
            _closingAfterSave = true;
            _autosave.Dispose();
            _ = Dispatcher.BeginInvoke(new Action(Close));
        }
        catch (Exception ex)
        {
            ShowWorkspaceError(ex);
        }
        finally
        {
            _changingDraft = false;
        }
    }

    public sealed record PostAiActionItem(PostAiAction Action, string DisplayName);

    public sealed class ChannelSelectionItem
    {
        private static readonly System.Windows.Media.Brush MutedBrush =
            new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x9E, 0xA9, 0xB7));
        private static readonly System.Windows.Media.Brush WarningBrush =
            new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xE8, 0x5A, 0x5A));

        public ChannelSelectionItem(ChannelAccount channel, bool isSelected, string subtitle, bool hasValidBotToken)
        {
            Channel = channel;
            IsSelected = isSelected;
            Subtitle = subtitle;
            HasValidBotToken = hasValidBotToken;
            SubtitleBrush = hasValidBotToken ? MutedBrush : WarningBrush;
        }

        public ChannelAccount Channel { get; }
        public bool HasValidBotToken { get; }
        public string DisplayName => Channel.IsTestChannel
            ? LocalizationService.Format("TelegramComposer_TestChannel", Channel.DisplayName)
            : Channel.DisplayName;
        public string DestinationLabel => string.IsNullOrWhiteSpace(Channel.Username)
            ? $"{DisplayName} · {Channel.TelegramChatId}"
            : $"{DisplayName} · @{Channel.Username.TrimStart('@')}";
        public string Subtitle { get; }
        public System.Windows.Media.Brush SubtitleBrush { get; }
        public bool IsSelected { get; set; }
    }
}

public sealed class TelegramUtcToLocalConverter : System.Windows.Data.IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is DateTime dt
            ? dt.ToLocalTime().ToString("dd.MM HH:mm", culture)
            : string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
