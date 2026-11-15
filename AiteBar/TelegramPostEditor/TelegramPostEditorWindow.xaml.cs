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
    private const double DefaultWidth = 1360;
    private const double DefaultHeight = 840;

    private readonly AppSettingsService _settingsService;
    private readonly MainWindow? _mainWindow;
    private readonly AiGateway _aiGateway;

    private readonly PostDbMigrator _migrator;
    private readonly PostRepository _repository;
    private readonly PostAutosaveService _autosave;
    private readonly PostMediaStore _mediaStore;
    private readonly PostInstructionService _instructionService;
    private readonly PostAiService _aiService;
    private readonly TelegramSecretStore _secretStore;
    private readonly TelegramBotClient _botClient;
    private readonly TelegramChannelService _channelService;
    private readonly PublicationService _publicationService;
    private readonly PublicationSyncService _syncService;
    private readonly PostBackupService _backupService;

    private readonly ObservableCollection<ChannelSelectionItem> _channelSelections = [];
    private PostDocument? _currentPost;
    private bool _isLoadingPost;
    private bool _isRestoringPlacement;
    private CancellationTokenSource? _aiCts;
    private PostAiSuggestion? _lastSuggestion;
    private string? _pairedDeviceId;

    public TelegramPostEditorWindow(
        AppSettingsService settingsService,
        MainWindow? mainWindow,
        AiGateway aiGateway)
    {
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _mainWindow = mainWindow;
        _aiGateway = aiGateway ?? throw new ArgumentNullException(nameof(aiGateway));

        _migrator = new PostDbMigrator();
        _repository = new PostRepository(_migrator);
        _repository.EnsureBuiltInTemplates();
        _autosave = new PostAutosaveService(_repository);
        _mediaStore = new PostMediaStore(_migrator);
        _instructionService = new PostInstructionService(_migrator);
        _instructionService.EnsureBuiltInInstructions();
        _aiService = new PostAiService(_aiGateway, _instructionService);
        _secretStore = new TelegramSecretStore();
        _botClient = new TelegramBotClient();
        _channelService = new TelegramChannelService(_migrator, _botClient, _secretStore);
        _publicationService = new PublicationService(_migrator, _mediaStore, _botClient, _secretStore, _channelService);
        _syncService = new PublicationSyncService(new HttpClient { Timeout = TimeSpan.FromSeconds(45) }, _secretStore, _mediaStore);
        _backupService = new PostBackupService(_repository, _mediaStore);

        InitializeComponent();

        LstChannels.ItemsSource = _channelSelections;
        TxtScheduleLocal.Text = DateTime.Now.AddHours(1).ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        _autosave.Saved += (_, _) => Dispatcher.BeginInvoke(() =>
        {
            TxtStatusBar.Text = string.Format(
                LocalizationService.Get("TelegramPostEditor_StatusSavedAt"),
                DateTime.Now.ToString("HH:mm:ss", CultureInfo.CurrentCulture));
        });
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
        RefreshAllLists();
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
        foreach (AiConnectionSettings conn in _settingsService.Settings.Ai.Connections.Where(c => c.IsEnabled))
        {
            modelChoices.Add(new PostAiModelChoice(conn.Id, conn.ProviderId, conn.PreferredModelId, conn.DisplayName));
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

    private void RefreshAllLists()
    {
        RefreshPostsList();
        RefreshChannelsAndBots();
        RefreshEvents();
        Activated += (_, _) => RefreshChannelsAndBots();
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
        if (_currentPost == null && posts.Count > 0)
        {
            LoadPostIntoEditor(posts[0]);
            LstPosts.SelectedItem = posts[0];
        }
        else if (_currentPost == null)
        {
            CreateAndLoadNewPost();
        }
    }

    private void RefreshChannelsAndBots()
    {
        HashSet<string> previouslySelected = _channelSelections
            .Where(c => c.IsSelected)
            .Select(c => c.Channel.Id)
            .ToHashSet(StringComparer.Ordinal);
        bool hadSelections = _channelSelections.Count > 0;

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
        foreach (ChannelAccount ch in channels)
        {
            bool isSelected = hadSelections ? previouslySelected.Contains(ch.Id) : !ch.IsTestChannel;
            botsById.TryGetValue(ch.BotId, out BotAccount? boundBot);
            bool hasToken = !string.IsNullOrWhiteSpace(_secretStore.ReadBotToken(ch.BotId));
            string subtitle;
            if (!hasToken)
            {
                subtitle = LocalizationService.Get("TelegramPostEditor_ChannelMissingBotToken");
            }
            else
            {
                string chatPart = string.IsNullOrWhiteSpace(ch.Username)
                    ? $"ID {ch.TelegramChatId}"
                    : $"@{ch.Username.TrimStart('@')}";
                string botPart = boundBot == null
                    ? string.Empty
                    : string.IsNullOrWhiteSpace(boundBot.Username)
                        ? $" · {boundBot.DisplayName}"
                        : $" · @{boundBot.Username.TrimStart('@')}";
                subtitle = $"{chatPart}{botPart}";
            }

            _channelSelections.Add(new ChannelSelectionItem(ch, isSelected, subtitle, hasToken));
        }
        UpdatePreviewAndValidation();
    }

    private void RefreshEvents()
    {
        LstEvents.ItemsSource = _publicationService.GetEvents(limit: 100);
    }

    private void CreateAndLoadNewPost()
    {
        var post = new PostDocument { Title = LocalizationService.Get("TelegramPostEditor_NewPost") };
        _repository.SavePost(post);
        LoadPostIntoEditor(post);
        RefreshPostsList();
    }

    private void LoadPostIntoEditor(PostDocument post)
    {
        _isLoadingPost = true;
        try
        {
            _currentPost = post;
            TxtPostTitle.Text = post.Title;
            TxtPostTitlePlaceholder.Visibility = string.IsNullOrEmpty(TxtPostTitle.Text) ? Visibility.Visible : Visibility.Collapsed;
            PostEditorRichTextBox.Document = TelegramDocumentSerializer.ToFlowDocument(post);
            SelectInstructionInCombo(post.InstructionId);
            RefreshAttachedMediaList();
            UpdatePreviewAndValidation();
            TxtDeleteOrRestoreLabel.Text = post.DeletedUtc.HasValue
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
        }
    }

    private void UpdatePreviewAndValidation()
    {
        if (_currentPost == null)
        {
            return;
        }

        List<PostMediaItem> resolvedMedia = _currentPost.Media
            .OrderBy(a => a.SortOrder)
            .Select(a => _mediaStore.GetById(a.MediaId))
            .Where(m => m != null)
            .Cast<PostMediaItem>()
            .ToList();

        int selectedCount = _channelSelections.Count(c => c.IsSelected);
        TxtSelectedChannelsSummary.Text = string.Format(
            LocalizationService.Get("TelegramPostEditor_SelectedChannelsSummary"),
            selectedCount);

        ChannelAccount? previewChannel = _channelSelections.FirstOrDefault(c => c.IsSelected)?.Channel
            ?? _channelSelections.FirstOrDefault()?.Channel;

        TelegramPreviewModel preview = TelegramPreviewService.BuildPreview(_currentPost, resolvedMedia, previewChannel);
        TxtPreviewChannel.Text = previewChannel?.DisplayName
            ?? LocalizationService.Get("TelegramPostEditor_PreviewDefaultChannel");
        TxtPreviewSubtitle.Text = previewChannel != null && !string.IsNullOrWhiteSpace(previewChannel.Username)
            ? $"@{previewChannel.Username.TrimStart('@')}"
            : TxtSelectedChannelsSummary.Text;

        PreviewMediaBadge.Visibility = resolvedMedia.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        TxtPreviewMediaCount.Text = resolvedMedia.Count > 0
            ? string.Format(LocalizationService.Get("TelegramPostEditor_SelectedCountSuffix"), resolvedMedia.Count)
            : string.Empty;

        bool isEmpty = string.IsNullOrWhiteSpace(preview.Content.Text) && resolvedMedia.Count == 0;
        TxtEditorPlaceholder.Visibility = string.IsNullOrWhiteSpace(preview.Content.Text)
            ? Visibility.Visible
            : Visibility.Collapsed;

        int limit = resolvedMedia.Count > 0 ? TelegramValidationService.MaxCaptionLength : TelegramValidationService.MaxTextMessageLength;
        TxtCharCounter.Text = $"{preview.Content.Text.Length} / {limit}";

        if (isEmpty)
        {
            TxtPreviewBody.Foreground = (System.Windows.Media.Brush)FindResource("EditorMutedTextBrush");
            TxtPreviewBody.Text = LocalizationService.Get("TelegramPostEditor_PreviewEmpty");
            TxtValidationStatus.Foreground = (System.Windows.Media.Brush)FindResource("EditorMutedTextBrush");
            TxtValidationStatus.Text = LocalizationService.Get("TelegramPostEditor_EmptyPostHint");
            BtnPublishNow.IsEnabled = false;
            BtnTestPublish.IsEnabled = false;
            return;
        }

        TxtPreviewBody.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xF4, 0xF7, 0xFA));
        TxtPreviewBody.Text = preview.Content.Text;

        if (preview.Validation.IsValid)
        {
            TxtValidationStatus.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x67, 0xC5, 0x87));
            TxtValidationStatus.Text = preview.Validation.Warnings.Count > 0
                ? $"{LocalizationService.Get("TelegramPostEditor_ReadyToPublish")} ({string.Join("; ", preview.Validation.Warnings)})"
                : LocalizationService.Get("TelegramPostEditor_ReadyToPublish");
            BtnPublishNow.IsEnabled = true;
            BtnTestPublish.IsEnabled = true;
        }
        else
        {
            TxtValidationStatus.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xE8, 0x5A, 0x5A));
            TxtValidationStatus.Text = string.Join("\n", preview.Validation.Errors);
            BtnPublishNow.IsEnabled = false;
            BtnTestPublish.IsEnabled = false;
        }
    }

    private void BtnNewPost_Click(object sender, RoutedEventArgs e) => CreateAndLoadNewPost();

    private async void BtnSavePost_Click(object sender, RoutedEventArgs e)
    {
        if (_currentPost == null) return;
        SyncCurrentPostFromUi(scheduleAutosave: false);
        await _autosave.FlushAsync();
        _repository.SavePost(_currentPost, createRevision: true);
        RefreshPostsList();
        TxtStatusBar.Text = string.Format(
            LocalizationService.Get("TelegramPostEditor_StatusSavedAt"),
            DateTime.Now.ToString("HH:mm:ss", CultureInfo.CurrentCulture));
    }

    private void BtnFromTemplate_Click(object sender, RoutedEventArgs e)
    {
        IReadOnlyList<PostTemplate> templates = _repository.GetTemplates();
        if (templates.Count == 0) return;
        PostDocument draft = _repository.CreateDraftFromTemplate(templates[0].Id);
        LoadPostIntoEditor(draft);
        RefreshPostsList();
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

    private void BtnChooseChannels_Click(object sender, RoutedEventArgs e)
    {
        RightTabs.SelectedItem = TabChannelsItem;
    }

    private void BtnSchedulePost_Click(object sender, RoutedEventArgs e)
    {
        RightTabs.SelectedItem = TabQueueItem;
    }

    private void ChannelSelection_Changed(object sender, RoutedEventArgs e) => UpdatePreviewAndValidation();
    private void CmbLibraryFilter_SelectionChanged(object sender, SelectionChangedEventArgs e) => RefreshPostsList();
    private void CmbLibrarySort_SelectionChanged(object sender, SelectionChangedEventArgs e) => RefreshPostsList();

    private void LstPosts_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (LstPosts.SelectedItem is PostDocument selected && selected.Id != _currentPost?.Id)
        {
            LoadPostIntoEditor(selected);
        }
    }

    private void BtnDuplicatePost_Click(object sender, RoutedEventArgs e)
    {
        if (_currentPost == null) return;
        PostDocument copy = _repository.DuplicatePost(_currentPost.Id);
        RefreshPostsList();
        LoadPostIntoEditor(copy);
    }

    private void BtnDeleteOrRestorePost_Click(object sender, RoutedEventArgs e)
    {
        if (_currentPost == null) return;
        if (_currentPost.DeletedUtc.HasValue)
        {
            _repository.RestorePost(_currentPost.Id);
        }
        else
        {
            _repository.SoftDeletePost(_currentPost.Id);
        }
        _currentPost = null;
        RefreshPostsList();
    }

    private void EditorMetadata_Changed(object sender, RoutedEventArgs e) => SyncCurrentPostFromUi(scheduleAutosave: true);
    private void PostEditorRichTextBox_TextChanged(object sender, TextChangedEventArgs e) => SyncCurrentPostFromUi(scheduleAutosave: true);

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

    private async void BtnRunAi_Click(object sender, RoutedEventArgs e)
    {
        if (_currentPost == null) return;
        SyncCurrentPostFromUi(scheduleAutosave: false);

        PostAiAction action = CmbAiAction.SelectedItem is PostAiActionItem actionItem
            ? actionItem.Action
            : PostAiAction.Improve;
        string plainText = TelegramDocumentSerializer.ExtractPlainText(_currentPost);
        if (string.IsNullOrWhiteSpace(plainText) && action != PostAiAction.CreateFromBrief)
        {
            string emptyMsg = LocalizationService.Get("TelegramPostEditor_AiEmptyInput");
            TxtStatusBar.Text = emptyMsg;
            new DarkDialog(emptyMsg) { Owner = this }.ShowDialog();
            return;
        }

        PostAiModelChoice choice = CmbAiModel.SelectedItem as PostAiModelChoice
            ?? new PostAiModelChoice(null, null, null, LocalizationService.Get("TelegramPostEditor_AutoModel"), IsAuto: true);
        string? instId = CmbInstruction.SelectedItem is PostInstruction inst && !string.IsNullOrWhiteSpace(inst.Id)
            ? inst.Id
            : null;
        _currentPost.InstructionId = instId;

        _aiCts?.Cancel();
        _aiCts?.Dispose();
        _aiCts = new CancellationTokenSource();

        BtnRunAi.IsEnabled = false;
        BtnStopAi.IsEnabled = true;
        BtnStopAi.Visibility = Visibility.Visible;
        AiSuggestionPanel.Visibility = Visibility.Visible;
        TxtAiSuggestion.Text = string.Empty;
        TxtAiMeta.Text = LocalizationService.Get("TelegramPostEditor_AiGenerating");

        var progress = new Progress<string>(text => TxtAiSuggestion.Text = text);
        try
        {
            _lastSuggestion = await _aiService.GenerateSuggestionAsync(
                _currentPost,
                action,
                choice,
                instId,
                userPrompt: null,
                streamingProgress: progress,
                cancellationToken: _aiCts.Token);

            TxtAiSuggestion.Text = _lastSuggestion.Content;
            TxtAiMeta.Text = $"{_lastSuggestion.Metadata.ProviderId}/{_lastSuggestion.Metadata.ModelId}";
        }
        catch (OperationCanceledException)
        {
            TxtAiMeta.Text = LocalizationService.Get("Common_Cancel");
        }
        catch (Exception ex)
        {
            TxtAiMeta.Text = ex.Message;
        }
        finally
        {
            BtnRunAi.IsEnabled = true;
            BtnStopAi.IsEnabled = false;
            BtnStopAi.Visibility = Visibility.Collapsed;
        }
    }

    private void BtnStopAi_Click(object sender, RoutedEventArgs e) => _aiCts?.Cancel();

    private void BtnApplyAi_Click(object sender, RoutedEventArgs e)
    {
        if (_currentPost == null || string.IsNullOrWhiteSpace(TxtAiSuggestion.Text)) return;

        List<PostBlock> parsedBlocks = TelegramDocumentSerializer.ParseMarkdownToBlocks(TxtAiSuggestion.Text);
        _currentPost.Blocks = parsedBlocks.Count > 0
            ? parsedBlocks
            : [
                new PostBlock
                {
                    Type = PostBlockType.Paragraph,
                    Runs = [new PostTextRun { Text = TxtAiSuggestion.Text.Trim() }]
                }
            ];

        if (_lastSuggestion != null)
        {
            _currentPost.AiGenerationMetadata = _lastSuggestion.Metadata;
        }
        _repository.SavePost(_currentPost, createRevision: true);
        LoadPostIntoEditor(_currentPost);
        RefreshPostsList();
        AiSuggestionPanel.Visibility = Visibility.Collapsed;
    }

    private void BtnDismissAi_Click(object sender, RoutedEventArgs e)
    {
        AiSuggestionPanel.Visibility = Visibility.Collapsed;
        _lastSuggestion = null;
    }

    private void PwdQuickBotToken_PasswordChanged(object sender, RoutedEventArgs e)
    {
        TxtQuickBotTokenPlaceholder.Visibility = string.IsNullOrEmpty(PwdQuickBotToken.Password)
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void TxtQuickChannelInput_TextChanged(object sender, TextChangedEventArgs e)
    {
        TxtQuickChannelPlaceholder.Visibility = string.IsNullOrEmpty(TxtQuickChannelInput.Text)
            ? Visibility.Visible
            : Visibility.Collapsed;
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

    private void BtnInlineDeleteChannel_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button { Tag: string channelId })
        {
            _channelService.DeleteChannel(channelId);
            RefreshChannelsAndBots();
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

    private async void BtnTestPublish_Click(object sender, RoutedEventArgs e)
    {
        if (_currentPost == null) return;
        SyncCurrentPostFromUi(scheduleAutosave: false);

        List<ChannelAccount> testChannels = _channelSelections.Select(c => c.Channel).Where(c => c.IsTestChannel).ToList();
        if (testChannels.Count == 0)
        {
            new DarkDialog(LocalizationService.Get("TelegramPostEditor_NoTestChannelWarning")) { Owner = this }.ShowDialog();
            return;
        }

        BtnTestPublish.IsEnabled = false;
        try
        {
            _repository.SavePost(_currentPost, createRevision: false);
            IReadOnlyList<PublicationTarget> res = await _publicationService.PublishNowAsync(_currentPost, testChannels, isTestRun: true);
            RefreshEvents();
            ReportPublishResults(res);
        }
        finally
        {
            UpdatePreviewAndValidation();
        }
    }

    private async void BtnPublishNow_Click(object sender, RoutedEventArgs e)
    {
        if (_currentPost == null) return;
        SyncCurrentPostFromUi(scheduleAutosave: false);

        List<ChannelAccount> selected = _channelSelections.Where(c => c.IsSelected).Select(c => c.Channel).ToList();
        if (selected.Count == 0)
        {
            new DarkDialog(LocalizationService.Get("TelegramPostEditor_SelectChannelWarning")) { Owner = this }.ShowDialog();
            RightTabs.SelectedItem = TabChannelsItem;
            return;
        }

        BtnPublishNow.IsEnabled = false;
        try
        {
            _repository.SavePost(_currentPost, createRevision: true);
            IReadOnlyList<PublicationTarget> res = await _publicationService.PublishNowAsync(_currentPost, selected, isTestRun: false);
            RefreshEvents();
            ReportPublishResults(res);
        }
        finally
        {
            UpdatePreviewAndValidation();
        }
    }

    private void ReportPublishResults(IReadOnlyList<PublicationTarget> results)
    {
        int sentCount = results.Count(r => r.Status == WorkflowStatus.SENT);
        TxtStatusBar.Text = $"{sentCount}/{results.Count} SENT";

        List<string> errors = results
            .Where(r => r.Status != WorkflowStatus.SENT)
            .Select(r => string.IsNullOrWhiteSpace(r.LastErrorDetails) ? r.LastErrorCode ?? r.Status.ToString() : r.LastErrorDetails!)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (errors.Count > 0)
        {
            string message = string.Join("\n", errors);
            TxtValidationStatus.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xE8, 0x5A, 0x5A));
            TxtValidationStatus.Text = message;
            new DarkDialog($"{LocalizationService.Get("TelegramPostEditor_PublishFailedTitle")}\n\n{message}") { Owner = this }.ShowDialog();
        }
    }

    private async void BtnPairServer_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            ServerPairingResult pair = await _syncService.PairAsync(TxtServerUrl.Text, TxtPairingCode.Text, Environment.MachineName);
            _pairedDeviceId = pair.DeviceId;
            TxtPairingCode.Clear();
            TxtModeBadge.Text = $"Сервер подключён ({pair.DeviceId[..6]})";
        }
        catch (Exception ex)
        {
            new DarkDialog(ex.Message) { Owner = this }.ShowDialog();
        }
    }

    private async void BtnQueueServer_Click(object sender, RoutedEventArgs e)
    {
        if (_currentPost == null || string.IsNullOrWhiteSpace(_pairedDeviceId))
        {
            new DarkDialog("Сначала привяжите сервер планирования.") { Owner = this }.ShowDialog();
            return;
        }

        if (!DateTime.TryParse(TxtScheduleLocal.Text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out DateTime scheduledLocal))
        {
            new DarkDialog("Неверный формат даты/времени.") { Owner = this }.ShowDialog();
            return;
        }

        List<ChannelAccount> selected = _channelSelections.Where(c => c.IsSelected).Select(c => c.Channel).ToList();
        if (selected.Count == 0) return;

        try
        {
            _repository.SavePost(_currentPost, createRevision: true);
            List<(ChannelAccount, DateTime)> schedule = selected.Select(c => (c, scheduledLocal.ToUniversalTime())).ToList();
            string serverPubId = await _syncService.QueuePostOnServerAsync(TxtServerUrl.Text, _pairedDeviceId, _currentPost, schedule);
            TxtStatusBar.Text = $"Принято сервером (QUEUED_SERVER): {serverPubId}";
        }
        catch (Exception ex)
        {
            new DarkDialog($"Сервер не подтвердил очередь: {ex.Message}") { Owner = this }.ShowDialog();
        }
    }

    private void RestoreWindowPlacement(AppSettings settings)
    {
        _isRestoringPlacement = true;
        try
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
        finally
        {
            _isRestoringPlacement = false;
        }
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
        if (_isRestoringPlacement) return;
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

    private void Window_LocationChanged(object? sender, EventArgs e) { if (IsLoaded) PersistWindowPlacement(); }
    private void Window_SizeChanged(object? sender, SizeChangedEventArgs e) { if (IsLoaded) PersistWindowPlacement(); }
    private void Window_StateChanged(object? sender, EventArgs e) { if (IsLoaded) PersistWindowPlacement(); }

    private async void Window_Closing(object? sender, CancelEventArgs e)
    {
        _aiCts?.Cancel();
        _aiCts?.Dispose();
        try
        {
            await _autosave.FlushAsync();
        }
        catch
        {
            // Ignore flush errors on close.
        }
        _autosave.Dispose();
        PersistWindowPlacement();
        await _settingsService.SaveAsync();
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
            SubtitleBrush = hasValidBotToken ? MutedBrush : WarningBrush;
        }

        public ChannelAccount Channel { get; }
        public string DisplayName => Channel.IsTestChannel ? $"[TEST] {Channel.DisplayName}" : Channel.DisplayName;
        public string Subtitle { get; }
        public System.Windows.Media.Brush SubtitleBrush { get; }
        public bool IsSelected { get; set; }
    }
}

public sealed class TelegramWorkflowStatusConverter : System.Windows.Data.IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is WorkflowStatus status
            ? status switch
            {
                WorkflowStatus.DRAFT => LocalizationService.Get("TelegramPostEditor_StatusDraft"),
                WorkflowStatus.QUEUED_LOCAL or WorkflowStatus.QUEUED_SERVER => LocalizationService.Get("TelegramPostEditor_StatusScheduled"),
                WorkflowStatus.SENT => LocalizationService.Get("TelegramPostEditor_StatusPublished"),
                WorkflowStatus.FAILED => LocalizationService.Get("TelegramPostEditor_StatusFailed"),
                _ => status.ToString()
            }
            : string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
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
