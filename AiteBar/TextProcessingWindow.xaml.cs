using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Forms = System.Windows.Forms;

namespace AiteBar;

[SupportedOSPlatform("windows6.1")]
public partial class TextProcessingWindow : DarkWindow
{
    private const double PreferredWidth = 1280;
    private const double PreferredHeight = 840;
    private const double PreferredMinWidth = 1000;
    private const double PreferredMinHeight = 700;
    private const double WorkAreaRatio = 0.9;

    private readonly TextProcessingService _service;
    private readonly AppSettingsService _settingsService;
    private readonly AiGateway _gateway;
    private readonly ObservableCollection<ModelItem> _models = [];
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
    private bool _isModifiedManually;
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

    public TextProcessingWindow(TextProcessingService service, AppSettingsService settingsService)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _gateway = new AiGateway(settingsService);
        InitializeComponent();
        CmbModels.ItemsSource = _models;
        ApplyModeToUi();
        RefreshUiState();
    }

    public void ShowNearPanel(AppSettingsService settingsService)
    {
        ResetModeToProofread();
        RestoreWindowState(settingsService.Settings);
        Show();
        Activate();
        FocusEditor();
    }

    internal void RestoreFromAiteBar()
    {
        ResetModeToProofread();
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
        _currentMode = TextProcessingMode.Proofread;
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
        if (_isDirty && !string.IsNullOrWhiteSpace(TxtEditor.Text))
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
        _processingCts?.Dispose();
        _loadModelsCts?.Dispose();
    }

    private void Window_StateChanged(object? sender, EventArgs e)
    {
        if (WindowState == WindowState.Minimized)
        {
            Hide();
            WindowState = WindowState.Normal;
            return;
        }
        if (!_isLoadingState)
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
            if (_isShowingOriginal)
            {
                _originalText = TxtEditor.Text;
            }
            else
            {
                _processedText = TxtEditor.Text;
            }
            _isModifiedManually = true;
        }
        _isDirty = true;
        SetStatus(string.Empty);
        RefreshUiState();
    }

    private void CmbModels_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoadingState || CmbModels.SelectedItem is not ModelItem item)
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

        _isShowingOriginal = !_isShowingOriginal;
        SetEditorText(_isShowingOriginal ? _originalText : _processedText);
        RefreshUiState();
        FocusEditor();
    }

    private void BtnClear_Click(object sender, RoutedEventArgs e) => Clear();

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
            ResetResultHistory();
            SetEditorText(Clipboard.GetText());
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
                useAutoModel = true;
                providerId = null;
                modelId = null;
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

        AiChatRequest request = _service.BuildRequest(mode, input);
        ModelItem? selected = null;
        if (!useAutoModel)
        {
            selected = FindModel(providerId, modelId);
            if (selected == null)
            {
                useAutoModel = true;
                providerId = null;
                modelId = null;
                _isAutoModel = true;
                CmbModels.SelectedIndex = 0;
                _selectedProviderId = null;
                _selectedModelId = null;
                SaveModelSelection();
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
        _isProcessing = true;
        _processingCts = new CancellationTokenSource();
        RefreshUiState();
        try
        {
            AiGatewayResponse response = await _gateway.GenerateAsync(request, _processingCts.Token);
            string cleaned = _service.CleanResponse(response.Content);
            if (string.IsNullOrWhiteSpace(cleaned))
            {
                SetStatus(LocalizationService.Get("TextProcessing_ErrorEmptyResponse"));
                return;
            }
            _originalText = input;
            _processedText = cleaned;
            _hasSuccessfulResult = true;
            _isShowingOriginal = false;
            _isModifiedManually = false;
            _lastOriginalText = input;
            _lastMode = mode;
            _lastWasAutoModel = useAutoModel;
            _lastProviderId = providerId;
            _lastModelId = modelId;
            SetEditorText(cleaned);
            _isDirty = false;
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
            SetStatus(LocalizationService.Get("TextProcessing_ErrorNoModels"));
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
            _isProcessing = false;
            _processingCts?.Dispose();
            _processingCts = null;
            RefreshUiState();
            FocusEditor();
        }
    }

    private static AiChatRequest CopyRequestWithModel(AiChatRequest request, string? providerId, string? modelId) => new()
    {
        Messages = request.Messages,
        RequiredCapabilities = request.RequiredCapabilities,
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

    private void Clear()
    {
        ResetResultHistory();
        SetEditorText(string.Empty);
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
        _isModifiedManually = false;
    }

    private void SetEditorText(string text)
    {
        _isApplyingEditorText = true;
        try
        {
            TxtEditor.Text = text ?? string.Empty;
            TxtEditor.CaretIndex = TxtEditor.Text.Length;
        }
        finally
        {
            _isApplyingEditorText = false;
        }
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
        ModeProofread.IsEnabled = state.CanSelectMode;
        ModeTypography.IsEnabled = state.CanSelectMode;
        ModeCleanup.IsEnabled = state.CanSelectMode;
        CmbModels.IsEnabled = state.CanSelectModel;
        BtnPaste.IsEnabled = state.CanPaste;
        BtnCopy.IsEnabled = state.CanCopy;
        BtnClear.IsEnabled = state.CanClear;
        BtnRepeat.IsEnabled = state.CanRepeat;
        BtnRepeat.Visibility = _hasSuccessfulResult ? Visibility.Visible : Visibility.Collapsed;
        BtnToggleVersion.IsEnabled = state.CanSwitchVersion;
        BtnToggleVersion.Visibility = _hasSuccessfulResult ? Visibility.Visible : Visibility.Collapsed;
        ToggleVersionLabel.Text = LocalizationService.Get(_isShowingOriginal
            ? "TextProcessing_ButtonAfterProcessing"
            : "TextProcessing_ButtonBeforeProcessing");
        AutomationProperties.SetName(BtnToggleVersion, ToggleVersionLabel.Text);
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
        }
        else if (!_hasEligibleModel)
        {
            TxtModelState.Text = LocalizationService.Get("TextProcessing_ErrorNoModels");
            TxtModelState.Foreground = (Brush)FindResource("TextProcessingWarningBrush");
            TxtModelState.ToolTip = TxtModelState.Text;
            TxtModelState.Visibility = Visibility.Visible;
        }
        else
        {
            TxtModelState.Visibility = Visibility.Collapsed;
            TxtModelState.ToolTip = null;
        }
    }

    private void SetStatus(string message)
    {
        TxtStatusMessage.Text = message;
        AutomationProperties.SetName(StatusBorder, message);
        StatusBorder.Visibility = string.IsNullOrWhiteSpace(message) ? Visibility.Collapsed : Visibility.Visible;
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
        var discovered = new List<ModelItem>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
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
                        string identity = $"{model.ProviderId}\n{model.ModelId}";
                        if (seen.Add(identity))
                        {
                            string connectionName = NormalizeModelText(connection.DisplayName);
                            if (!HasVisibleModelText(connectionName))
                            {
                                connectionName = NormalizeModelText(model.ProviderId);
                            }
                            string displayName = NormalizeModelText(model.DisplayName);
                            if (!HasVisibleModelText(displayName))
                            {
                                displayName = NormalizeModelText(model.ModelId);
                            }
                            if (!HasVisibleModelText(displayName))
                            {
                                continue;
                            }
                            discovered.Add(new ModelItem(model.ProviderId, model.ModelId,
                                displayName, model.ContextLength)
                            {
                                FullDisplay = $"{connectionName} — {displayName}"
                            });
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
        foreach (ModelItem model in discovered.OrderBy(m => m.Display, StringComparer.CurrentCultureIgnoreCase))
        {
            _models.Add(model);
        }
        _hasEligibleModel = discovered.Count > 0;
        RestoreModelSelection();
        _isLoadingModels = false;
        RefreshUiState();
    }

    internal static bool IsEligibleModel(AiModelDescriptor model) =>
        HasVisibleModelText(model.ModelId) &&
        !model.IsDeprecated &&
        (model.Capabilities & AiCapabilities.Text) == AiCapabilities.Text &&
        (model.CostStatus is AiCostStatus.VerifiedFree or AiCostStatus.FreeTierAvailable) &&
        IsSuitableForWriting(model);

    private static bool IsSuitableForWriting(AiModelDescriptor model)
    {
        string searchable = $"{model.ModelId} {model.DisplayName}".ToLowerInvariant();
        string[] excludedTerms =
        [
            "whisper", "speech", "audio", "transcrib", "tts",
            "embedding", "rerank", "moderation", "prompt-guard", "prompt guard",
            "safety gpt"
        ];
        return !excludedTerms.Any(searchable.Contains);
    }

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

    private void ResetModeToProofread()
    {
        if (_isProcessing)
        {
            return;
        }
        _currentMode = TextProcessingMode.Proofread;
        ApplyModeToUi();
        RefreshUiState();
    }

    private void RestoreModelSelection()
    {
        AppSettings settings = _settingsService.Settings;
        _isAutoModel = settings.TextProcessingIsAutoModel;
        bool replacedUnavailableSelection = false;
        if (!_isAutoModel)
        {
            ModelItem? model = FindModel(settings.TextProcessingSelectedProviderId, settings.TextProcessingSelectedModelId);
            if (model != null)
            {
                CmbModels.SelectedItem = model;
                _selectedProviderId = model.ProviderId;
                _selectedModelId = model.ModelId;
                return;
            }
            _isAutoModel = true;
            replacedUnavailableSelection = true;
        }
        CmbModels.SelectedIndex = 0;
        _selectedProviderId = null;
        _selectedModelId = null;
        if (replacedUnavailableSelection)
        {
            SaveModelSelection();
        }
    }

    private void SaveModelSelection()
    {
        _settingsService.UpdateSettings(settings =>
        {
            settings.TextProcessingIsAutoModel = _isAutoModel;
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
        MinWidth = Math.Min(PreferredMinWidth, maxWidth);
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

public sealed record ModelItem(string? ProviderId, string? ModelId, string Display, int? ContextLength)
{
    public string FullDisplay { get; init; } = Display;
}
