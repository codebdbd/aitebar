using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Runtime.Versioning;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace AiteBar;

[SupportedOSPlatform("windows6.1")]
public partial class TextProcessingWindow : DarkWindow
{
    private const double PreferredWidth = 1280;
    private const double PreferredHeight = 840;
    private const double PreferredMinWidth = 1000;
    private const double PreferredMinHeight = 700;
    private const double EditorWidth = 738;
    private const double CommandGap = 16;
    private const double HorizontalWindowInsets = 80;
    private const double WorkAreaRatio = 0.9;
    private static readonly TimeSpan StreamingUiUpdateInterval = TimeSpan.FromMilliseconds(50);

    private readonly TextProcessingService _service;
    private readonly AppSettingsService _settingsService;
    private readonly AiGateway _gateway;
    private readonly MainWindow? _mainWindow;
    private readonly ObservableCollection<ModelItem> _models = [];
    private readonly TextProcessingUndoHistory _operationHistory = new(10);
    private readonly DispatcherTimer _progressTimer;
    private CancellationTokenSource? _processingCts;
    private CancellationTokenSource? _loadModelsCts;
    private bool _isLoadingState = true;
    private bool _isLoadingModels;
    private bool _isApplyingEditorText;
    private bool _isDirty;
    private bool _isProcessing;
    private bool _hasClipboardText;
    private bool _hasEligibleModel;
    private bool _hasSuccessfulResult;
    private bool _isShowingOriginal;
    private bool _isShowingDiff;
    private bool _isModifiedManually;
    private bool _hasCopiedResult;
    private string _lastUsedModelDisplay = string.Empty;
    private string _inlineInfoStatus = string.Empty;
    private TextProcessingMode _currentMode = TextProcessingMode.Proofread;
    private string _originalText = string.Empty;
    private string _processedText = string.Empty;
    private bool _isAutoModel = true;
    private string? _selectedProviderId;
    private string? _selectedModelId;
    private string _lastOriginalText = string.Empty;
    private TextProcessingMode _lastMode;
    private bool _lastWasAutoModel = true;
    private string? _lastProviderId;
    private string? _lastModelId;
    private double _requiredMinWidth = PreferredMinWidth;
    private DateTimeOffset _processingStartedAt;
    private bool _isProgressStatusVisible;

    public TextProcessingWindow(
        TextProcessingService service,
        AppSettingsService settingsService,
        MainWindow? mainWindow = null)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _mainWindow = mainWindow;
        _gateway = new AiGateway(settingsService);
        _currentMode = ParseSavedMode(settingsService.Settings.TextProcessingLastMode);
        InitializeComponent();
        _progressTimer = new DispatcherTimer(TimeSpan.FromSeconds(1), DispatcherPriority.Background, (_, _) =>
            UpdateProcessingProgress(), Dispatcher);
        CmbModels.ItemsSource = _models;
        ApplyModeToUi();
        RefreshUiState();
        UpdateCommandButtonLayout();
    }

    public void ShowNearPanel(AppSettingsService settingsService)
    {
        RestoreWindowState(settingsService.Settings);
        Show();
        Activate();
        FocusEditor();
    }

    internal void RestoreFromAiteBar()
    {
        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }
        if (!IsVisible)
        {
            Show();
        }
        Activate();
        FocusEditor();
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        _isLoadingState = true;
        ApplyModeToUi();
        RefreshClipboardAvailability(showError: false);
        _loadModelsCts = new CancellationTokenSource();
        try
        {
            await LoadModelsAsync(_loadModelsCts.Token);
        }
        catch (OperationCanceledException)
        {
            // Closing while the model catalogue loads is expected.
        }
        catch (Exception ex)
        {
            Logger.Log(ex);
            SetStatus(LocalizationService.Get("TextProcessing_ErrorNoModels"));
        }
        finally
        {
            _isLoadingModels = false;
            _isLoadingState = false;
            _loadModelsCts?.Dispose();
            _loadModelsCts = null;
            RefreshUiState();
            FocusEditor();
        }
    }

    private void Window_Activated(object? sender, EventArgs e)
    {
        RefreshClipboardAvailability(showError: false);
        RefreshUiState();
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        bool hasContent = !string.IsNullOrWhiteSpace(TxtEditor.Text);
        bool needsWarning = hasContent && (_isDirty || (_hasSuccessfulResult && !_hasCopiedResult));
        if (needsWarning)
        {
            bool close = new DarkDialog(LocalizationService.Get("TextProcessing_ConfirmClose"), isConfirm: true)
            {
                Owner = this
            }.ShowDialog() == true;
            if (!close)
            {
                e.Cancel = true;
                return;
            }
        }
        SaveWindowState();
        _processingCts?.Cancel();
        _loadModelsCts?.Cancel();
    }

    private void Window_StateChanged(object? sender, EventArgs e)
    {
        if (!_isLoadingState && WindowState != WindowState.Minimized)
        {
            SaveWindowState();
        }
    }

    private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (!_isLoadingState && WindowState == WindowState.Normal)
        {
            SaveWindowState();
        }
    }

    private void Window_LocationChanged(object? sender, EventArgs e)
    {
        if (!_isLoadingState && WindowState == WindowState.Normal && Left > -10000 && Top > -10000)
        {
            SaveWindowState();
        }
    }

    private async void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Control)
        {
            e.Handled = true;
            if (!_isProcessing)
            {
                await ProcessAsync(repeatLast: false);
            }
        }
        else if (e.Key == Key.Escape && _isProcessing)
        {
            e.Handled = true;
            CancelProcessing();
        }
        else if (e.Key == Key.Z && Keyboard.Modifiers == ModifierKeys.Control)
        {
            e.Handled = true;
            UndoEditor();
        }
        else if (e.Key == Key.Y && Keyboard.Modifiers == ModifierKeys.Control)
        {
            e.Handled = true;
            RedoEditor();
        }
    }

    private void ModeTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoadingState || _isProcessing || ModeTabs.SelectedItem is not TabItem { Tag: string tag })
        {
            return;
        }
        _currentMode = tag switch
        {
            "Proofread" => TextProcessingMode.Proofread,
            "Typography" => TextProcessingMode.Typography,
            "Cleanup" => TextProcessingMode.Cleanup,
            _ => _currentMode
        };
        SaveModeSelection();
        ApplyModeToUi();
        RefreshUiState();
    }

    private void TxtEditor_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isApplyingEditorText)
        {
            return;
        }
        if (_hasSuccessfulResult)
        {
            if (!_isShowingOriginal)
            {
                _processedText = TxtEditor.Text;
                _isModifiedManually = true;
            }
        }
        _isDirty = true;
        SetStatus(string.Empty);
        ClearInfoStatus();
        RefreshUiState();
    }

    private void CmbModels_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoadingState || _isLoadingModels || CmbModels.SelectedItem is not ModelItem item)
        {
            return;
        }
        _isAutoModel = item.ModelId == null;
        _selectedProviderId = item.ProviderId;
        _selectedModelId = item.ModelId;
        SaveModelSelection();
        SetStatus(string.Empty);
        RefreshUiState();
    }

    private void CmbModels_DropDownOpened(object sender, EventArgs e)
    {
        if (CmbModels.Template.FindName("DropDownBorder", CmbModels) is FrameworkElement dropDown)
        {
            dropDown.MinWidth = 0;
            dropDown.Width = CmbModels.ActualWidth;
            dropDown.MaxWidth = CmbModels.ActualWidth;
        }
    }

    private async void BtnProcess_Click(object sender, RoutedEventArgs e)
    {
        if (_isProcessing)
        {
            CancelProcessing();
            return;
        }
        await ProcessAsync(repeatLast: false);
    }

    private async void BtnRepeat_Click(object sender, RoutedEventArgs e) =>
        await ProcessAsync(repeatLast: true);

    private void BtnToggleVersion_Click(object sender, RoutedEventArgs e)
    {
        if (_isProcessing || !_hasSuccessfulResult)
        {
            return;
        }

        _isShowingOriginal = _isShowingDiff || !_isShowingOriginal;
        _isShowingDiff = false;
        SetEditorText(_isShowingOriginal ? _originalText : _processedText);
        RefreshUiState();
        FocusEditor();
    }

    private void BtnClear_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(TxtEditor.Text))
        {
            bool confirmed = new DarkDialog(LocalizationService.Get("TextProcessing_ConfirmClear"), isConfirm: true)
            {
                Owner = this
            }.ShowDialog() == true;
            if (!confirmed)
            {
                return;
            }
        }
        Clear();
    }

    private void BtnPaste_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!Clipboard.ContainsText())
            {
                RefreshClipboardAvailability(showError: false);
                RefreshUiState();
                return;
            }
            string clipboardText = Clipboard.GetText();
            (string updatedText, int caretIndex) = InsertAtSelection(
                TxtEditor.Text,
                TxtEditor.SelectionStart,
                TxtEditor.SelectionLength,
                clipboardText);
            ResetResultHistory();
            SetEditorText(updatedText, caretIndex, recordUndo: true);
            _isDirty = true;
            SetStatus(string.Empty);
            RefreshClipboardAvailability(showError: false);
            RefreshUiState();
            FocusEditor();
        }
        catch (Exception ex)
        {
            Logger.Log(ex);
            SetStatus(LocalizationService.Get("TextProcessing_ErrorClipboard"));
            RefreshClipboardAvailability(showError: false);
            RefreshUiState();
        }
    }

    private void BtnCopy_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(TxtEditor.Text))
            {
                Clipboard.SetText(TxtEditor.Text);
                if (_hasSuccessfulResult)
                {
                    _hasCopiedResult = true;
                }
                SetStatus(LocalizationService.Get("TextProcessing_Copied"));
            }
            RefreshClipboardAvailability(showError: false);
            RefreshUiState();
        }
        catch (Exception ex)
        {
            Logger.Log(ex);
            SetStatus(LocalizationService.Get("TextProcessing_ErrorClipboard"));
        }
    }

    private void BtnOpenSettings_Click(object sender, RoutedEventArgs e)
    {
        if (_mainWindow != null)
        {
            _ = _mainWindow.ShowAppSettingsWindow(AppSettingsSection.AiProviders);
        }
        else
        {
            SetStatus(LocalizationService.Get("TextProcessing_ErrorNoModels"));
        }
    }

    private async void BtnRefreshModels_Click(object sender, RoutedEventArgs e)
    {
        if (_isLoadingModels || _isProcessing)
        {
            return;
        }
        _loadModelsCts?.Cancel();
        _loadModelsCts?.Dispose();
        var refreshCts = new CancellationTokenSource();
        _loadModelsCts = refreshCts;
        try
        {
            foreach (AiConnectionSettings connection in
                     (_settingsService.Settings.Ai?.Connections ?? []).Where(connection => connection.IsEnabled))
            {
                _gateway.InvalidateModelCache(connection.Id);
            }
            await LoadModelsAsync(refreshCts.Token);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Logger.Log(ex);
            SetStatus(LocalizationService.Get("TextProcessing_ErrorNoModels"));
        }
        finally
        {
            if (ReferenceEquals(_loadModelsCts, refreshCts))
            {
                _isLoadingModels = false;
                refreshCts.Dispose();
                _loadModelsCts = null;
                RefreshUiState();
            }
        }
    }

    private void BtnShowDiff_Click(object sender, RoutedEventArgs e)
    {
        if (_isProcessing || !_hasSuccessfulResult)
        {
            return;
        }

        _isShowingDiff = !_isShowingDiff;
        _isShowingOriginal = false;
        if (_isShowingDiff)
        {
            RenderDiff();
        }
        else
        {
            SetEditorText(_processedText);
        }
        RefreshUiState();
    }

    private async Task ProcessAsync(bool repeatLast)
    {
        if (_isProcessing)
        {
            return;
        }

        string input;
        TextProcessingMode mode;
        bool useAutoModel;
        string? providerId;
        string? modelId;
        if (repeatLast)
        {
            if (!_hasSuccessfulResult || string.IsNullOrWhiteSpace(_lastOriginalText))
            {
                return;
            }
            input = _lastOriginalText;
            mode = _lastMode;
            useAutoModel = _lastWasAutoModel;
            providerId = _lastProviderId;
            modelId = _lastModelId;
            if (!useAutoModel && !TrySelectModel(providerId, modelId))
            {
                SetStatus(LocalizationService.Get("TextProcessing_ModelUnavailable"));
                return;
            }
            _currentMode = mode;
            _isAutoModel = useAutoModel;
            if (useAutoModel)
            {
                CmbModels.SelectedIndex = 0;
                _selectedProviderId = null;
                _selectedModelId = null;
            }
            ApplyModeToUi();
        }
        else
        {
            input = TxtEditor.Text;
            mode = _currentMode;
            useAutoModel = _isAutoModel;
            providerId = useAutoModel ? null : _selectedProviderId;
            modelId = useAutoModel ? null : _selectedModelId;
            if (!GetUiState().CanProcess)
            {
                if (string.IsNullOrWhiteSpace(input))
                {
                    SetStatus(LocalizationService.Get("TextProcessing_ErrorEmptyInput"));
                }
                else if (!_hasEligibleModel)
                {
                    SetStatus(LocalizationService.Get("TextProcessing_ErrorNoModels"));
                }
                return;
            }
            if (_hasSuccessfulResult && !_isShowingOriginal && _isModifiedManually)
            {
                bool replaceEditedResult = new DarkDialog(LocalizationService.Get("TextProcessing_ConfirmRepeat"), isConfirm: true)
                {
                    Owner = this
                }.ShowDialog() == true;
                if (!replaceEditedResult)
                {
                    return;
                }
            }
        }

        ProtectedText protectedInput = _service.ProtectTechnicalFragments(input);
        AiChatRequest request = _service.BuildRequest(mode, protectedInput.Text);
        ModelItem? selected = null;
        if (!useAutoModel)
        {
            selected = FindModel(providerId, modelId);
            if (selected == null)
            {
                SetStatus(LocalizationService.Get("TextProcessing_ModelUnavailable"));
                return;
            }
        }
        if (useAutoModel)
        {
            if (!_models.Any(model =>
                    model.ModelId != null &&
                    (!model.ContextLength.HasValue || request.RequiredContextTokens <= model.ContextLength.Value)))
            {
                SetStatus(LocalizationService.Get("TextProcessing_ErrorContextOverflow"));
                return;
            }
        }
        else
        {
            if (selected!.ContextLength.HasValue && request.RequiredContextTokens > selected.ContextLength.Value)
            {
                SetStatus(LocalizationService.Get("TextProcessing_ErrorContextOverflow"));
                return;
            }
            request = CopyRequestWithModel(request, providerId, modelId);
        }

        string textShownBeforeRequest = TxtEditor.Text;
        SetStatus(string.Empty);
        ClearInfoStatus();
        _isShowingOriginal = false;
        _isShowingDiff = false;
        _isProcessing = true;
        _isModifiedManually = false;
        _hasCopiedResult = false;
        _processingCts = new CancellationTokenSource();
        StartProcessingProgress();
        RefreshUiState();
        try
        {
            AiGatewayStream response = await _gateway.GenerateTextProcessingStreamingAsync(request, _processingCts.Token);
            var streamedResponse = new StringBuilder();
            bool receivedContent = false;
            long lastUiUpdate = 0;
            await foreach (string chunk in response.Chunks)
            {
                streamedResponse.Append(chunk);
                if (!receivedContent)
                {
                    receivedContent = true;
                }
                if (lastUiUpdate == 0 ||
                    Stopwatch.GetElapsedTime(lastUiUpdate) >= StreamingUiUpdateInterval)
                {
                    string preview = BuildStreamingPreview(streamedResponse.ToString(), protectedInput);
                    SetEditorText(preview);
                    lastUiUpdate = Stopwatch.GetTimestamp();
                }
            }
            string cleanedProtected = _service.CleanResponse(
                streamedResponse.ToString(),
                protectedInput.Text);
            string cleaned = TextProcessingService.RestoreTechnicalFragments(
                cleanedProtected,
                protectedInput,
                requireAllMarkers: true);
            if (string.IsNullOrWhiteSpace(cleaned))
            {
                SetStatus(LocalizationService.Get("TextProcessing_ErrorEmptyResponse"));
                return;
            }
            _originalText = input;
            _processedText = cleaned;
            _hasSuccessfulResult = true;
            _isShowingOriginal = false;
            _isShowingDiff = false;
            _isModifiedManually = false;
            _lastOriginalText = input;
            _lastMode = mode;
            _lastWasAutoModel = useAutoModel;
            _lastProviderId = providerId;
            _lastModelId = modelId;
            ModelItem? usedModel = FindModel(response.ProviderId, response.ModelId);
            _lastUsedModelDisplay = usedModel?.Display ?? response.ModelId;
            if (!string.Equals(textShownBeforeRequest, cleaned, StringComparison.Ordinal))
            {
                _operationHistory.Record(textShownBeforeRequest);
            }
            SetEditorText(cleaned);
            _isDirty = false;
            StopProcessingProgress();
            if (!string.IsNullOrEmpty(_lastUsedModelDisplay))
            {
                SetInfoStatus(LocalizationService.Format("TextProcessing_ModelUsed", _lastUsedModelDisplay));
            }
        }
        catch (OperationCanceledException) when (_processingCts?.IsCancellationRequested == true)
        {
            SetEditorText(textShownBeforeRequest);
            SetStatus(LocalizationService.Get("TextProcessing_ErrorCancellation"));
        }
        catch (OperationCanceledException ex)
        {
            Logger.Log(ex);
            SetEditorText(textShownBeforeRequest);
            SetStatus(LocalizationService.Get("TextProcessing_ErrorTimeout"));
        }
        catch (NoAvailableConnectionException ex)
        {
            Logger.Log(ex);
            SetEditorText(textShownBeforeRequest);
            SetStatus(ex.InnerException switch
            {
                AiProviderHttpException providerError => GetProviderError(providerError),
                HttpRequestException => LocalizationService.Get("TextProcessing_ErrorNetwork"),
                TimeoutException => LocalizationService.Get("TextProcessing_ErrorTimeout"),
                _ => LocalizationService.Get("TextProcessing_ErrorNoModels")
            });
        }
        catch (AiProviderHttpException ex)
        {
            Logger.Log(ex);
            SetEditorText(textShownBeforeRequest);
            SetStatus(GetProviderError(ex));
        }
        catch (TimeoutException ex)
        {
            Logger.Log(ex);
            SetEditorText(textShownBeforeRequest);
            SetStatus(LocalizationService.Get("TextProcessing_ErrorTimeout"));
        }
        catch (HttpRequestException ex)
        {
            Logger.Log(ex);
            SetEditorText(textShownBeforeRequest);
            SetStatus(LocalizationService.Get("TextProcessing_ErrorNetwork"));
        }
        catch (Exception ex)
        {
            Logger.Log(ex);
            SetEditorText(textShownBeforeRequest);
            SetStatus(LocalizationService.Get("TextProcessing_ErrorGeneric"));
        }
        finally
        {
            StopProcessingProgress();
            _isProcessing = false;
            _processingCts?.Dispose();
            _processingCts = null;
            RefreshUiState();
            FocusEditor();
        }
    }

    private static AiChatRequest CopyRequestWithModel(
        AiChatRequest request,
        string? providerId,
        string? modelId) => new()
    {
        Messages = request.Messages,
        RequiredCapabilities = request.RequiredCapabilities,
        RequireFreeModel = request.RequireFreeModel,
        RequireWritingModel = request.RequireWritingModel,
        RequireExactModel = true,
        PreferredProviderId = providerId,
        PreferredModelId = modelId,
        RequiredContextTokens = request.RequiredContextTokens,
        MaxOutputTokens = request.MaxOutputTokens,
        Temperature = request.Temperature
    };

    private static string GetProviderError(AiProviderHttpException ex) => ex.StatusCode switch
    {
        System.Net.HttpStatusCode.Unauthorized => LocalizationService.Get("TextProcessing_ErrorUnauthorized"),
        System.Net.HttpStatusCode.Forbidden => LocalizationService.Get("TextProcessing_ErrorForbidden"),
        System.Net.HttpStatusCode.PaymentRequired => LocalizationService.Get("TextProcessing_ErrorQuota"),
        System.Net.HttpStatusCode.TooManyRequests => LocalizationService.Get("TextProcessing_ErrorRateLimit"),
        _ when (int)ex.StatusCode >= 500 => LocalizationService.Get("TextProcessing_ErrorUnavailable"),
        _ => LocalizationService.Get("TextProcessing_ErrorGeneric")
    };

    private void CancelProcessing() => _processingCts?.Cancel();

    private void StartProcessingProgress()
    {
        _processingStartedAt = DateTimeOffset.Now;
        _isProgressStatusVisible = true;
        UpdateProcessingProgress();
        _progressTimer.Start();
    }

    private void UpdateProcessingProgress()
    {
        if (!_isProcessing || !_isProgressStatusVisible)
        {
            return;
        }
        int seconds = Math.Max(0, (int)(DateTimeOffset.Now - _processingStartedAt).TotalSeconds);
        SetInfoStatus(LocalizationService.Format("TextProcessing_Progress", seconds));
    }

    private void StopProcessingProgress()
    {
        _progressTimer.Stop();
        if (_isProgressStatusVisible)
        {
            _isProgressStatusVisible = false;
            ClearInfoStatus();
        }
    }

    private void Clear()
    {
        ResetResultHistory();
        SetEditorText(string.Empty);
        _operationHistory.Clear();
        _isDirty = false;
        SetStatus(string.Empty);
        RefreshUiState();
        FocusEditor();
    }

    private void ResetResultHistory()
    {
        _originalText = string.Empty;
        _processedText = string.Empty;
        _lastOriginalText = string.Empty;
        _hasSuccessfulResult = false;
        _isShowingOriginal = false;
        _isShowingDiff = false;
        _isModifiedManually = false;
        _hasCopiedResult = false;
        ClearInfoStatus();
    }

    private void SetEditorText(string text, int? caretIndex = null, bool recordUndo = false)
    {
        text ??= string.Empty;
        if (recordUndo && !string.Equals(TxtEditor.Text, text, StringComparison.Ordinal))
        {
            _operationHistory.Record(TxtEditor.Text);
        }

        _isApplyingEditorText = true;
        try
        {
            bool restoreUndo = TxtEditor.IsUndoEnabled;
            TxtEditor.IsUndoEnabled = false;
            TxtEditor.Text = text;
            TxtEditor.IsUndoEnabled = restoreUndo;
            TxtEditor.CaretIndex = Math.Clamp(caretIndex ?? TxtEditor.Text.Length, 0, TxtEditor.Text.Length);
        }
        finally
        {
            _isApplyingEditorText = false;
        }
    }

    private void UndoEditor()
    {
        if (_isProcessing || _isShowingOriginal)
        {
            return;
        }
        if (TxtEditor.CanUndo)
        {
            TxtEditor.Undo();
            return;
        }
        if (_operationHistory.TryUndo(TxtEditor.Text, out string previous))
        {
            ResetResultHistory();
            SetEditorText(previous);
            _isDirty = true;
            SetStatus(string.Empty);
            RefreshUiState();
            FocusEditor();
        }
    }

    private void RedoEditor()
    {
        if (_isProcessing || _isShowingOriginal)
        {
            return;
        }
        if (TxtEditor.CanRedo)
        {
            TxtEditor.Redo();
            return;
        }
        if (_operationHistory.TryRedo(TxtEditor.Text, out string next))
        {
            ResetResultHistory();
            SetEditorText(next);
            _isDirty = true;
            SetStatus(string.Empty);
            RefreshUiState();
            FocusEditor();
        }
    }

    internal static string BuildStreamingPreview(string rawText, ProtectedText protectedText)
    {
        string visibleText = TextProcessingService.HideReasoningFromStreamingPreview(
            rawText ?? string.Empty);
        int partialMarkerLength = 0;
        foreach (string marker in protectedText.Fragments.Keys)
        {
            int limit = Math.Min(visibleText.Length, marker.Length - 1);
            for (int length = limit; length >= 2; length--)
            {
                if (marker.StartsWith(visibleText[^length..], StringComparison.Ordinal))
                {
                    partialMarkerLength = Math.Max(partialMarkerLength, length);
                    break;
                }
            }
        }
        if (partialMarkerLength > 0)
        {
            visibleText = visibleText[..^partialMarkerLength];
        }
        return TextProcessingService.RestoreTechnicalFragments(visibleText, protectedText);
    }

    internal static (string Text, int CaretIndex) InsertAtSelection(
        string source,
        int selectionStart,
        int selectionLength,
        string insertion)
    {
        source ??= string.Empty;
        insertion ??= string.Empty;
        int start = Math.Clamp(selectionStart, 0, source.Length);
        int length = Math.Clamp(selectionLength, 0, source.Length - start);
        string result = source.Remove(start, length).Insert(start, insertion);
        return (result, start + insertion.Length);
    }

    private TextProcessingUiState GetUiState() => TextProcessingUiState.Create(new TextProcessingUiStateInput(
        TxtEditor.Text,
        _isProcessing,
        _isLoadingModels,
        _hasEligibleModel,
        _hasClipboardText,
        _hasSuccessfulResult));

    private void RefreshUiState()
    {
        if (!IsInitialized)
        {
            return;
        }
        TextProcessingUiState state = GetUiState();
        TxtPlaceholder.Visibility = state.CharacterCount == 0 ? Visibility.Visible : Visibility.Collapsed;
        TxtCounters.Text = $"{LocalizationService.Format("TextProcessing_Characters", state.CharacterCount)} · {LocalizationService.Format("TextProcessing_Words", state.WordCount)}";
        TxtCounters.Foreground = state.IsOverLimit
            ? (Brush)FindResource("TextProcessingWarningBrush")
            : (Brush)FindResource("MutedText");
        AutomationProperties.SetName(TxtCounters, TxtCounters.Text);
        LimitBorder.Visibility = state.IsOverLimit ? Visibility.Visible : Visibility.Collapsed;
        TxtEditor.IsEnabled = state.CanEdit;
        TxtEditor.IsReadOnly = _isShowingOriginal;
        TxtEditor.Visibility = _isShowingDiff ? Visibility.Collapsed : Visibility.Visible;
        DiffViewer.Visibility = _isShowingDiff ? Visibility.Visible : Visibility.Collapsed;
        ModeProofread.IsEnabled = state.CanSelectMode;
        ModeTypography.IsEnabled = state.CanSelectMode;
        ModeCleanup.IsEnabled = state.CanSelectMode;
        CmbModels.IsEnabled = state.CanSelectModel;
        BtnRefreshModels.IsEnabled = !_isProcessing && !_isLoadingModels;
        BtnPaste.IsEnabled = state.CanPaste;
        BtnCopy.IsEnabled = state.CanCopy;
        BtnClear.IsEnabled = state.CanClear;
        BtnRepeat.IsEnabled = state.CanRepeat;
        BtnToggleVersion.IsEnabled = state.CanSwitchVersion;
        BtnShowDiff.IsEnabled = state.CanSwitchVersion;
        ToggleVersionLabel.Text = LocalizationService.Get(_isShowingOriginal
            ? "TextProcessing_ButtonShowResult"
            : "TextProcessing_ButtonShowOriginal");
        AutomationProperties.SetName(BtnToggleVersion, ToggleVersionLabel.Text);
        ShowDiffLabel.Text = LocalizationService.Get(_isShowingDiff
            ? "TextProcessing_ButtonHideDiff"
            : "TextProcessing_ButtonShowDiff");
        BtnShowDiff.ToolTip = ShowDiffLabel.Text;
        AutomationProperties.SetName(BtnShowDiff, ShowDiffLabel.Text);
        BtnProcess.IsEnabled = state.CanCancel || state.CanProcess;
        ProcessButtonLabel.Text = _isProcessing
            ? LocalizationService.Get("TextProcessing_ButtonCancel")
            : LocalizationService.Get("TextProcessing_ButtonProcess");
        AutomationProperties.SetName(BtnProcess, ProcessButtonLabel.Text);

        if (_isLoadingModels)
        {
            TxtModelState.Text = LocalizationService.Get("TextProcessing_ModelLoading");
            TxtModelState.Foreground = (Brush)FindResource("MutedText");
            TxtModelState.ToolTip = TxtModelState.Text;
            TxtModelState.Visibility = Visibility.Visible;
            BtnOpenSettings.Visibility = Visibility.Collapsed;
        }
        else if (!_hasEligibleModel)
        {
            TxtModelState.Text = LocalizationService.Get("TextProcessing_ErrorNoModels");
            TxtModelState.Foreground = (Brush)FindResource("TextProcessingWarningBrush");
            TxtModelState.ToolTip = TxtModelState.Text;
            TxtModelState.Visibility = Visibility.Visible;
            BtnOpenSettings.Visibility = Visibility.Visible;
        }
        else if (!string.IsNullOrWhiteSpace(_inlineInfoStatus))
        {
            TxtModelState.Text = _inlineInfoStatus;
            TxtModelState.Foreground = (Brush)FindResource("MutedText");
            TxtModelState.ToolTip = _inlineInfoStatus;
            TxtModelState.Visibility = Visibility.Visible;
            BtnOpenSettings.Visibility = Visibility.Collapsed;
        }
        else
        {
            TxtModelState.Visibility = Visibility.Collapsed;
            TxtModelState.ToolTip = null;
            BtnOpenSettings.Visibility = Visibility.Collapsed;
        }
    }

    private void UpdateCommandButtonLayout()
    {
        Button[] buttons = [BtnPaste, BtnCopy, BtnRepeat, BtnToggleVersion, BtnShowDiff, BtnClear, BtnProcess];
        double commandWidth = buttons.Max(MeasureButtonWidth);
        commandWidth = Math.Max(commandWidth, MeasureButtonWidthForLabels(
            BtnToggleVersion,
            ToggleVersionLabel,
            LocalizationService.Get("TextProcessing_ButtonShowOriginal"),
            LocalizationService.Get("TextProcessing_ButtonShowResult")));
        commandWidth = Math.Max(commandWidth, MeasureButtonWidthForLabels(
            BtnProcess,
            ProcessButtonLabel,
            LocalizationService.Get("TextProcessing_ButtonProcess"),
            LocalizationService.Get("TextProcessing_ButtonCancel")));
        commandWidth = Math.Max(commandWidth, MeasureButtonWidthForLabels(
            BtnShowDiff,
            ShowDiffLabel,
            LocalizationService.Get("TextProcessing_ButtonShowDiff"),
            LocalizationService.Get("TextProcessing_ButtonHideDiff")));
        commandWidth = Math.Ceiling(commandWidth);

        foreach (Button button in buttons)
        {
            button.Width = commandWidth;
        }
        FooterCommandColumn.Width = new GridLength(commandWidth);
        RailCommandColumn.Width = new GridLength(commandWidth);
        ContentHost.Width = EditorWidth + CommandGap + commandWidth;
        _requiredMinWidth = Math.Max(PreferredMinWidth, ContentHost.Width + HorizontalWindowInsets);
        MinWidth = _requiredMinWidth;
    }

    private static double MeasureButtonWidthForLabels(
        Button button,
        TextBlock label,
        params string[] labels)
    {
        string originalText = label.Text;
        double width = 0;
        foreach (string text in labels)
        {
            label.Text = text;
            label.InvalidateMeasure();
            label.Measure(new System.Windows.Size(double.PositiveInfinity, double.PositiveInfinity));
            if (button.Content is FrameworkElement content)
            {
                content.InvalidateMeasure();
            }
            width = Math.Max(width, MeasureButtonWidth(button));
        }
        label.Text = originalText;
        label.InvalidateMeasure();
        return width;
    }

    private static double MeasureButtonWidth(Button button)
    {
        if (button.Content is not FrameworkElement content)
        {
            return button.MinWidth;
        }
        content.Measure(new System.Windows.Size(double.PositiveInfinity, double.PositiveInfinity));
        return Math.Max(
            button.MinWidth,
            content.DesiredSize.Width +
            button.Padding.Left +
            button.Padding.Right +
            button.BorderThickness.Left +
            button.BorderThickness.Right);
    }

    private void SetStatus(string message)
    {
        TxtStatusMessage.Text = message;
        AutomationProperties.SetName(StatusBorder, message);
        StatusBorder.Visibility = string.IsNullOrWhiteSpace(message) ? Visibility.Collapsed : Visibility.Visible;
        if (!string.IsNullOrWhiteSpace(message))
        {
            _inlineInfoStatus = string.Empty;
            TxtModelState.Visibility = Visibility.Collapsed;
        }
    }

    private void SetInfoStatus(string message)
    {
        _inlineInfoStatus = message ?? string.Empty;
        if (IsInitialized)
        {
            RefreshUiState();
        }
    }

    private void ClearInfoStatus() => SetInfoStatus(string.Empty);

    private void RenderDiff()
    {
        var paragraph = new Paragraph { Margin = new Thickness(0) };
        foreach (TextDiffSegment segment in TextDiff.Create(_originalText, _processedText))
        {
            var run = new Run(segment.Text);
            if (segment.Kind == TextDiffKind.Added)
            {
                run.Foreground = (Brush)FindResource("TextProcessingDiffAddedBrush");
                run.TextDecorations = TextDecorations.Underline;
            }
            else if (segment.Kind == TextDiffKind.Removed)
            {
                run.Foreground = (Brush)FindResource("TextProcessingDiffRemovedBrush");
                run.TextDecorations = TextDecorations.Strikethrough;
            }
            paragraph.Inlines.Add(run);
        }
        DiffViewer.Document = new FlowDocument(paragraph)
        {
            PagePadding = new Thickness(0),
            FontFamily = TxtEditor.FontFamily,
            FontSize = TxtEditor.FontSize,
            Foreground = TxtEditor.Foreground
        };
    }

    private void ApplyModeToUi()
    {
        ModeProofread.IsSelected = _currentMode == TextProcessingMode.Proofread;
        ModeTypography.IsSelected = _currentMode == TextProcessingMode.Typography;
        ModeCleanup.IsSelected = _currentMode == TextProcessingMode.Cleanup;
        TxtModeDescription.Text = _currentMode switch
        {
            TextProcessingMode.Proofread => LocalizationService.Get("TextProcessing_ModeProofreadDesc"),
            TextProcessingMode.Typography => LocalizationService.Get("TextProcessing_ModeTypographyDesc"),
            TextProcessingMode.Cleanup => LocalizationService.Get("TextProcessing_ModeCleanupDesc"),
            _ => string.Empty
        };
    }

    protected override void OnLocalizationChanged()
    {
        base.OnLocalizationChanged();
        ApplyModeToUi();
        RefreshUiState();
        UpdateCommandButtonLayout();
    }

    private void RefreshClipboardAvailability(bool showError)
    {
        try
        {
            _hasClipboardText = Clipboard.ContainsText();
        }
        catch (Exception ex)
        {
            _hasClipboardText = false;
            Logger.Log(ex);
            if (showError)
            {
                SetStatus(LocalizationService.Get("TextProcessing_ErrorClipboard"));
            }
        }
    }

    private void FocusEditor()
    {
        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (TxtEditor.IsEnabled)
            {
                TxtEditor.Focus();
                Keyboard.Focus(TxtEditor);
            }
        }));
    }

    private async Task LoadModelsAsync(CancellationToken cancellationToken)
    {
        _isLoadingModels = true;
        _hasEligibleModel = false;
        _models.Clear();
        string automaticLabel = LocalizationService.Get("TextProcessing_ModelAuto");
        _models.Add(new ModelItem(null, null, automaticLabel, null) { FullDisplay = automaticLabel });
        CmbModels.SelectedIndex = 0;
        RefreshUiState();
        var availableModels = new List<AiModelDescriptor>();
        var preferredIdentities = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AiSettings? aiSettings = _settingsService.Settings.Ai;
        if (aiSettings?.Connections != null)
        {
            foreach (AiConnectionSettings connection in aiSettings.Connections.Where(c => c.IsEnabled))
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    IReadOnlyList<AiModelDescriptor> models = await _gateway.GetModelsAsync(connection, cancellationToken);
                    foreach (AiModelDescriptor model in models.Where(IsEligibleModel))
                    {
                        availableModels.Add(model);
                        if (string.Equals(
                                model.ModelId,
                                connection.PreferredModelId,
                                StringComparison.OrdinalIgnoreCase))
                        {
                            preferredIdentities.Add(CreateModelIdentity(model.ProviderId, model.ModelId));
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    Logger.Log(ex);
                }
            }
        }
        IReadOnlyList<ModelItem> logicalModels = BuildLogicalModelItems(availableModels, preferredIdentities);
        foreach (ModelItem model in logicalModels)
        {
            _models.Add(model);
        }
        _hasEligibleModel = logicalModels.Count > 0;
        RestoreModelSelection();
        _isLoadingModels = false;
        RefreshUiState();
    }

    internal static bool IsEligibleModel(AiModelDescriptor model) =>
        HasVisibleModelText(model.ModelId) &&
        !model.IsDeprecated &&
        (model.Capabilities & AiCapabilities.Text) == AiCapabilities.Text &&
        (model.CostStatus is AiCostStatus.VerifiedFree or AiCostStatus.FreeTierAvailable) &&
        TextProcessingService.IsSuitableForWritingModel(model);

    private static bool HasVisibleModelText(string? value) =>
        !string.IsNullOrEmpty(value) && value.Any(character =>
            !char.IsWhiteSpace(character) &&
            !char.IsControl(character) &&
            CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.Format);

    private static string NormalizeModelText(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }
        return new string(value.Where(character =>
            !char.IsControl(character) &&
            CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.Format).ToArray()).Trim();
    }

    internal static IReadOnlyList<ModelItem> BuildLogicalModelItems(
        IEnumerable<AiModelDescriptor> models,
        IReadOnlySet<string>? preferredIdentities = null)
    {
        ArgumentNullException.ThrowIfNull(models);
        var grouped = new Dictionary<string, List<AiModelDescriptor>>(StringComparer.OrdinalIgnoreCase);
        foreach (AiModelDescriptor model in models.Where(IsEligibleModel))
        {
            string identity = CreateModelIdentity(model.ProviderId, model.ModelId);
            if (!grouped.TryGetValue(identity, out List<AiModelDescriptor>? routes))
            {
                routes = [];
                grouped.Add(identity, routes);
            }
            routes.Add(model);
        }

        var items = grouped.Select(pair =>
        {
            AiModelDescriptor first = pair.Value[0];
            string display = pair.Value
                .Select(model => NormalizeModelText(model.DisplayName))
                .FirstOrDefault(HasVisibleModelText) ?? NormalizeModelText(first.ModelId);
            int? contextLength = pair.Value.Any(model => !model.ContextLength.HasValue)
                ? null
                : pair.Value.Max(model => model.ContextLength);
            return new
            {
                Identity = pair.Key,
                Item = new ModelItem(first.ProviderId, first.ModelId, display, contextLength)
            };
        }).ToList();

        var duplicateDisplays = items
            .GroupBy(entry => entry.Item.Display, StringComparer.CurrentCultureIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToHashSet(StringComparer.CurrentCultureIgnoreCase);

        return items
            .Select(entry =>
            {
                string fullDisplay = duplicateDisplays.Contains(entry.Item.Display)
                    ? $"{entry.Item.Display} — {GetProviderDisplayName(entry.Item.ProviderId)}"
                    : entry.Item.Display;
                return new
                {
                    entry.Identity,
                    IsPreferred = preferredIdentities?.Contains(entry.Identity) == true,
                    Item = entry.Item with { FullDisplay = fullDisplay }
                };
            })
            .OrderByDescending(entry => entry.IsPreferred)
            .ThenBy(entry => entry.Item.FullDisplay, StringComparer.CurrentCultureIgnoreCase)
            .Select(entry => entry.Item)
            .ToArray();
    }

    private static string CreateModelIdentity(string? providerId, string? modelId) =>
        $"{providerId?.Trim()}\n{modelId?.Trim()}";

    private static string GetProviderDisplayName(string? providerId) =>
        providerId != null && AiProviderCatalog.TryGet(providerId, out AiProviderDefinition definition)
            ? definition.DisplayName
            : NormalizeModelText(providerId);

    private ModelItem? FindModel(string? providerId, string? modelId) =>
        _models.FirstOrDefault(m =>
            string.Equals(m.ProviderId, providerId, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(m.ModelId, modelId, StringComparison.OrdinalIgnoreCase));

    private bool TrySelectModel(string? providerId, string? modelId)
    {
        ModelItem? model = FindModel(providerId, modelId);
        if (model == null)
        {
            return false;
        }
        CmbModels.SelectedItem = model;
        _selectedProviderId = model.ProviderId;
        _selectedModelId = model.ModelId;
        return true;
    }

    private static TextProcessingMode ParseSavedMode(int value) =>
        Enum.IsDefined(typeof(TextProcessingMode), value)
            ? (TextProcessingMode)value
            : TextProcessingMode.Proofread;

    private void SaveModeSelection()
    {
        _settingsService.UpdateSettings(settings =>
            settings.TextProcessingLastMode = (int)_currentMode);
    }

    private void RestoreModelSelection()
    {
        AppSettings settings = _settingsService.Settings;
        bool hadLegacyConnectionSelection =
            !string.IsNullOrWhiteSpace(settings.TextProcessingSelectedConnectionId);
        _isAutoModel = settings.TextProcessingIsAutoModel;
        bool replacedUnavailableSelection = false;
        if (!_isAutoModel)
        {
            ModelItem? model = FindModel(
                settings.TextProcessingSelectedProviderId,
                settings.TextProcessingSelectedModelId);
            if (model != null)
            {
                CmbModels.SelectedItem = model;
                _selectedProviderId = model.ProviderId;
                _selectedModelId = model.ModelId;
                SaveModelSelection();
                return;
            }
            _isAutoModel = true;
            replacedUnavailableSelection = true;
        }
        CmbModels.SelectedIndex = 0;
        _selectedProviderId = null;
        _selectedModelId = null;
        if (replacedUnavailableSelection || hadLegacyConnectionSelection)
        {
            SaveModelSelection();
        }
        if (replacedUnavailableSelection)
        {
            SetStatus(LocalizationService.Get("TextProcessing_ModelUnavailable"));
        }
    }

    private void SaveModelSelection()
    {
        _settingsService.UpdateSettings(settings =>
        {
            settings.TextProcessingIsAutoModel = _isAutoModel;
            settings.TextProcessingSelectedConnectionId = null;
            settings.TextProcessingSelectedProviderId = _isAutoModel ? null : _selectedProviderId;
            settings.TextProcessingSelectedModelId = _isAutoModel ? null : _selectedModelId;
        });
    }

    private void RestoreWindowState(AppSettings settings)
    {
        Forms.Screen screen = GetTargetScreen(settings.TextProcessingLeft, settings.TextProcessingTop);
        System.Drawing.Rectangle work = screen.WorkingArea;
        double maxWidth = Math.Max(640, work.Width * WorkAreaRatio);
        double maxHeight = Math.Max(560, work.Height * WorkAreaRatio);
        MinWidth = Math.Min(_requiredMinWidth, maxWidth);
        MinHeight = Math.Min(PreferredMinHeight, maxHeight);
        Width = Math.Clamp(settings.TextProcessingWidth ?? PreferredWidth, MinWidth, Math.Min(MaxWidth, maxWidth));
        Height = Math.Clamp(settings.TextProcessingHeight ?? PreferredHeight, MinHeight, Math.Min(MaxHeight, maxHeight));
        double desiredLeft = settings.TextProcessingLeft ?? work.Left + (work.Width - Width) / 2;
        double desiredTop = settings.TextProcessingTop ?? work.Top + (work.Height - Height) / 2;
        Left = Math.Clamp(desiredLeft, work.Left, Math.Max(work.Left, work.Right - Width));
        Top = Math.Clamp(desiredTop, work.Top, Math.Max(work.Top, work.Bottom - Height));
        WindowStartupLocation = WindowStartupLocation.Manual;
        WindowState = string.Equals(settings.TextProcessingWindowState, "Maximized", StringComparison.Ordinal)
            ? WindowState.Maximized
            : WindowState.Normal;
    }

    private static Forms.Screen GetTargetScreen(double? left, double? top)
    {
        if (left.HasValue && top.HasValue && double.IsFinite(left.Value) && double.IsFinite(top.Value))
        {
            return Forms.Screen.FromPoint(new System.Drawing.Point((int)left.Value, (int)top.Value));
        }
        return Forms.Screen.PrimaryScreen ?? throw new InvalidOperationException("No Windows display is available.");
    }

    private void SaveWindowState()
    {
        if (_isLoadingState || WindowState == WindowState.Minimized)
        {
            return;
        }
        Rect bounds = WindowState == WindowState.Normal
            ? new Rect(Left, Top, ActualWidth, ActualHeight)
            : RestoreBounds;
        if (bounds.IsEmpty || !double.IsFinite(bounds.Width) || !double.IsFinite(bounds.Height))
        {
            return;
        }
        string state = WindowState == WindowState.Maximized ? "Maximized" : "Normal";
        _settingsService.UpdateSettings(settings =>
        {
            settings.TextProcessingLeft = bounds.Left;
            settings.TextProcessingTop = bounds.Top;
            settings.TextProcessingWidth = bounds.Width;
            settings.TextProcessingHeight = bounds.Height;
            settings.TextProcessingWindowState = state;
        });
    }
}

public sealed record ModelItem(
    string? ProviderId,
    string? ModelId,
    string Display,
    int? ContextLength)
{
    public string FullDisplay { get; init; } = Display;
}
