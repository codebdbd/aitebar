using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace AiteBar;

[SupportedOSPlatform("windows6.1")]
public partial class PromptBuilderWindow : DarkWindow
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

    private readonly PromptBuilderService _service;
    private readonly AppSettingsService _settingsService;
    private readonly AiGateway _gateway;
    private readonly MainWindow? _mainWindow;
    private readonly ObservableCollection<ModelItem> _models = [];
    private readonly TextProcessingUndoHistory _operationHistory = new(10);
    private readonly DispatcherTimer _progressTimer;
    private CancellationTokenSource? _processingCts;
    private CancellationTokenSource? _loadModelsCts;
    private bool _isLoadingState = true;
    private bool _isApplyingMode;
    private bool _isLoadingModels;
    private bool _isApplyingEditorText;
    private bool _isProcessing;
    private bool _hasClipboardText;
    private bool _hasEligibleModel;
    private bool _hasAutomaticModel;
    private bool _hasSelectableModel;
    private bool _hasSuccessfulResult;
    private bool _isShowingOriginal;
    private bool _isShowingDiff;
    private string _lastUsedModelDisplay = string.Empty;
    private string _inlineInfoStatus = string.Empty;
    private PromptBuilderCategory _currentMode = PromptBuilderCategory.Programming;
    private PaintingStyle _paintingStyle = PaintingStyle.Auto;
    private AnimationStyle _animationStyle = AnimationStyle.Auto;
    private PhotoStyle _photoStyle = PhotoStyle.Auto;
    private TextPromptType _textType = TextPromptType.Auto;
    private TextPromptTone _textTone = TextPromptTone.Neutral;
    private AnalysisDirection _analysisDirection = AnalysisDirection.Auto;
    private VideoDirection _videoDirection = VideoDirection.Auto;
    private ProgrammingTaskType _programmingTaskType = ProgrammingTaskType.Auto;
    private VisualTargetModel _visualTarget = VisualTargetModel.Universal;
    private IconPlatform _iconPlatform = IconPlatform.Auto;
    private IconStyle _iconStyle = IconStyle.Auto;
    private GraphicType _graphicType = GraphicType.Auto;
    private GraphicStyle _graphicStyle = GraphicStyle.Auto;
    private string _originalText = string.Empty;
    private string _processedText = string.Empty;
    private bool _isAutoModel = true;
    private string? _selectedProviderId;
    private string? _selectedModelId;
    private string _lastOriginalText = string.Empty;
    private PromptBuilderCategory _lastMode;
    private bool _lastWasAutoModel = true;
    private string? _lastProviderId;
    private string? _lastModelId;
    private double _requiredMinWidth = PreferredMinWidth;
    private DateTimeOffset _processingStartedAt;
    private bool _isProgressStatusVisible;
    private int _infoStatusVersion;

    public PromptBuilderWindow(
        PromptBuilderService service,
        AppSettingsService settingsService,
        MainWindow? mainWindow = null)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _mainWindow = mainWindow;
        _gateway = new AiGateway(settingsService);
        _currentMode = PromptBuilderCategory.Programming;
        InitializeComponent();
        SourceInitialized += OnSourceInitialized;
        _progressTimer = new DispatcherTimer(TimeSpan.FromSeconds(1), DispatcherPriority.Background, (_, _) =>
            UpdateProcessingProgress(), Dispatcher);
        CmbModels.ItemsSource = _models;
        ApplyModeToUi();
        RefreshUiState();
        UpdateCommandButtonLayout();
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        if (PresentationSource.FromVisual(this) is HwndSource source)
        {
            IntPtr hwnd = source.Handle;
            int style = NativeMethods.GetWindowLong(hwnd, NativeMethods.GWL_STYLE);
            style &= ~NativeMethods.WS_MINIMIZEBOX;
            _ = NativeMethods.SetWindowLong(hwnd, NativeMethods.GWL_STYLE, style);
            _ = NativeMethods.SetWindowPos(
                hwnd,
                IntPtr.Zero,
                0, 0, 0, 0,
                NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOZORDER | NativeMethods.SWP_FRAMECHANGED);
        }
    }

    public void ShowNearPanel(AppSettingsService settingsService)
    {
        AppSettings settings = settingsService.Settings;
        RestoreWindowState(settings);
        RestoreEditorText(settings);
        Show();
        Activate();
        FocusEditor();
    }

    private void RestoreEditorText(AppSettings settings)
    {
        if (!settings.SavePromptBuilderDrafts)
        {
            _operationHistory.Clear();
            ResetResultHistory();
            SetEditorText(string.Empty, caretIndex: 0);
            RefreshUiState();
            return;
        }
        PromptBuilderDraft? draft = GetDraft(settings, _currentMode);
        _operationHistory.Clear();
        ResetResultHistory();
        if (draft?.HasResult == true)
        {
            _originalText = draft.Input;
            _lastOriginalText = draft.Input;
            _processedText = draft.Result;
            _hasSuccessfulResult = true;
            _isShowingOriginal = draft.ShowOriginal;
            SetEditorText(_isShowingOriginal ? _originalText : _processedText, caretIndex: 0);
        }
        else
        {
            string saved = draft?.Input
                ?? (_currentMode == (PromptBuilderCategory)settings.PromptBuilderLastMode ? settings.PromptBuilderLastText : null)
                ?? string.Empty;
            SetEditorText(saved, caretIndex: 0);
        }
        SetStatus(string.Empty);
        ClearInfoStatus();
        RefreshUiState();
    }

    private void SaveEditorText()
    {
        if (!_settingsService.Settings.SavePromptBuilderDrafts)
        {
            _settingsService.UpdateSettings(settings =>
            {
                settings.PromptBuilderDrafts = [];
                settings.PromptBuilderLastText = null;
            });
            return;
        }
        PromptBuilderDraft draft = new()
        {
            Input = _hasSuccessfulResult ? _originalText : TxtEditor.Text ?? string.Empty,
            Result = _hasSuccessfulResult ? _processedText : string.Empty,
            HasResult = _hasSuccessfulResult,
            ShowOriginal = _hasSuccessfulResult && _isShowingOriginal
        };
        _settingsService.UpdateSettings(settings =>
        {
            settings.PromptBuilderDrafts ??= [];
            settings.PromptBuilderDrafts[GetDraftKey(_currentMode)] = draft;
            settings.PromptBuilderLastText = TxtEditor.Text ?? string.Empty;
        });
    }

    private static string GetDraftKey(PromptBuilderCategory category) => ((int)category).ToString(CultureInfo.InvariantCulture);

    private static PromptBuilderDraft? GetDraft(AppSettings settings, PromptBuilderCategory category)
    {
        Dictionary<string, PromptBuilderDraft>? drafts = settings.PromptBuilderDrafts;
        if (drafts != null && drafts.TryGetValue(GetDraftKey(category), out PromptBuilderDraft? draft)) return draft;
        return category == PromptBuilderCategory.Analysis && drafts != null && drafts.TryGetValue(GetDraftKey(PromptBuilderCategory.Ideas), out PromptBuilderDraft? legacy) ? legacy : null;
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
        RestoreCurrentMode();
        RestoreEditorText(_settingsService.Settings);
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
        SaveEditorText();
        SaveCurrentMode();
        SaveWindowState();
        _processingCts?.Cancel();
        _loadModelsCts?.Cancel();
    }

    private void SaveCurrentMode()
    {
        _settingsService.UpdateSettings(settings =>
        {
            settings.PromptBuilderLastMode = (int)_currentMode;
            settings.PromptBuilderPaintingStyle = _paintingStyle;
            settings.PromptBuilderAnimationStyle = _animationStyle;
            settings.PromptBuilderPhotoStyle = _photoStyle;
            settings.PromptBuilderTextType = _textType;
            settings.PromptBuilderTextTone = _textTone;
            settings.PromptBuilderAnalysisDirection = _analysisDirection;
            settings.PromptBuilderVideoDirection = _videoDirection;
            settings.PromptBuilderProgrammingTaskType = _programmingTaskType;
            settings.PromptBuilderVisualTarget = _visualTarget;
            settings.PromptBuilderIconPlatform = _iconPlatform;
            settings.PromptBuilderIconStyle = _iconStyle;
            settings.PromptBuilderGraphicType = _graphicType;
            settings.PromptBuilderGraphicStyle = _graphicStyle;
        });
    }

    private void RestoreCurrentMode()
    {
        int storedMode = _settingsService.Settings.PromptBuilderLastMode;
        PromptBuilderCategory restoredMode = storedMode switch
        {
            (int)PromptBuilderCategory.Programming => PromptBuilderCategory.Programming,
            (int)PromptBuilderCategory.Images => PromptBuilderCategory.Images,
            (int)PromptBuilderCategory.Texts => PromptBuilderCategory.Texts,
            (int)PromptBuilderCategory.Video => PromptBuilderCategory.Video,
            (int)PromptBuilderCategory.Analysis => PromptBuilderCategory.Analysis,
            (int)PromptBuilderCategory.Music => PromptBuilderCategory.Music,
            (int)PromptBuilderCategory.Ideas => PromptBuilderCategory.Analysis,
            (int)PromptBuilderCategory.Paintings => PromptBuilderCategory.Paintings,
            (int)PromptBuilderCategory.Animation => PromptBuilderCategory.Animation,
            (int)PromptBuilderCategory.Icons => PromptBuilderCategory.Icons,
            (int)PromptBuilderCategory.Graphics => PromptBuilderCategory.Graphics,
            _ => PromptBuilderCategory.Programming
        };
        _currentMode = restoredMode;
        _paintingStyle = _settingsService.Settings.PromptBuilderPaintingStyle;
        if (!PromptBuilderService.PaintingStyles.Any(style => style.Style == _paintingStyle)) _paintingStyle = PaintingStyle.Auto;
        _animationStyle = _settingsService.Settings.PromptBuilderAnimationStyle;
        if (!PromptBuilderService.AnimationStyles.Any(style => style.Style == _animationStyle)) _animationStyle = AnimationStyle.Auto;
        _photoStyle = _settingsService.Settings.PromptBuilderPhotoStyle;
        if (!PromptBuilderService.PhotoStyles.Any(style => style.Style == _photoStyle)) _photoStyle = PhotoStyle.Auto;
        _textType = _settingsService.Settings.PromptBuilderTextType;
        if (!PromptBuilderService.TextPromptTypes.Any(style => style.Type == _textType)) _textType = TextPromptType.Auto;
        _textTone = _settingsService.Settings.PromptBuilderTextTone;
        if (!PromptBuilderService.TextPromptTones.Any(style => style.Tone == _textTone)) _textTone = TextPromptTone.Neutral;
        _analysisDirection = _settingsService.Settings.PromptBuilderAnalysisDirection;
        if (!PromptBuilderService.AnalysisDirections.Any(item => item.Direction == _analysisDirection)) _analysisDirection = AnalysisDirection.Auto;
        _videoDirection = _settingsService.Settings.PromptBuilderVideoDirection;
        if (!PromptBuilderService.VideoDirections.Any(item => item.Direction == _videoDirection)) _videoDirection = VideoDirection.Auto;
        _programmingTaskType = _settingsService.Settings.PromptBuilderProgrammingTaskType;
        if (!PromptBuilderService.ProgrammingTaskTypes.Any(item => item.Type == _programmingTaskType)) _programmingTaskType = ProgrammingTaskType.Auto;
        _visualTarget = _settingsService.Settings.PromptBuilderVisualTarget;
        if (!PromptBuilderService.VisualTargetModels.Any(item => item.Model == _visualTarget)) _visualTarget = VisualTargetModel.Universal;
        _iconPlatform = _settingsService.Settings.PromptBuilderIconPlatform;
        if (!PromptBuilderService.IconPlatforms.Any(item => item.Platform == _iconPlatform)) _iconPlatform = IconPlatform.Auto;
        _iconStyle = _settingsService.Settings.PromptBuilderIconStyle;
        if (!PromptBuilderService.IconStyles.Any(item => item.Style == _iconStyle)) _iconStyle = IconStyle.Auto;
        _graphicType = _settingsService.Settings.PromptBuilderGraphicType;
        if (!PromptBuilderService.GraphicTypes.Any(item => item.Type == _graphicType)) _graphicType = GraphicType.Auto;
        _graphicStyle = _settingsService.Settings.PromptBuilderGraphicStyle;
        if (!PromptBuilderService.GraphicStyles.Any(item => item.Style == _graphicStyle)) _graphicStyle = GraphicStyle.Auto;
    }

    private void Window_StateChanged(object? sender, EventArgs e)
    {
        if (WindowState == WindowState.Minimized)
        {
            Close();
            return;
        }
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
        ModifierKeys modifiers = Keyboard.Modifiers;
        bool processFromEditor = e.Key == Key.Enter &&
            modifiers == ModifierKeys.Shift &&
            TxtEditor.IsKeyboardFocusWithin;
        bool processFromLegacyShortcut = e.Key == Key.Enter && modifiers == ModifierKeys.Control;
        if (processFromEditor || processFromLegacyShortcut)
        {
            e.Handled = true;
            if (!_isProcessing)
            {
                await ProcessAsync(repeatLast: false);
            }
            return;
        }
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            if (_isProcessing)
            {
                CancelProcessing();
            }
            else
            {
                Close();
            }
            return;
        }
        if (e.Key == Key.Z && Keyboard.Modifiers == ModifierKeys.Control)
        {
            e.Handled = true;
            UndoEditor();
            return;
        }
        if (e.Key == Key.Y && Keyboard.Modifiers == ModifierKeys.Control)
        {
            e.Handled = true;
            RedoEditor();
            return;
        }
    }

    private void ModeTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoadingState || _isApplyingMode || _isProcessing || ModeTabs.SelectedItem is not TabItem { Tag: string tag })
        {
            return;
        }
        PromptBuilderCategory selectedMode = tag switch
        {
            "Programming" => PromptBuilderCategory.Programming,
            "Images" => PromptBuilderCategory.Images,
            "Paintings" => PromptBuilderCategory.Paintings,
            "Animation" => PromptBuilderCategory.Animation,
            "Icons" => PromptBuilderCategory.Icons,
            "Graphics" => PromptBuilderCategory.Graphics,
            "Texts" => PromptBuilderCategory.Texts,
            "Video" => PromptBuilderCategory.Video,
            "Music" => PromptBuilderCategory.Music,
            "Analytics" => PromptBuilderCategory.Analysis,
            _ => _currentMode
        };

        if (selectedMode == _currentMode)
        {
            return;
        }

        SaveEditorText();
        _currentMode = selectedMode;
        SaveCurrentMode();
        RestoreEditorText(_settingsService.Settings);
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
            }
        }
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
        UpdateSelectedModelAvailability();
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
                }
                SetStatus(string.Empty);
                ShowTransientInfoStatus(LocalizationService.Get("TextProcessing_Copied"));
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
        PromptBuilderCategory mode;
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
                    SetStatus(LocalizationService.Get("PromptBuilder_ErrorEmptyInput"));
                }
                else if (!_hasEligibleModel)
                {
                    SetStatus(LocalizationService.Get("TextProcessing_ErrorNoModels"));
                }
                return;
            }
        }

        AiChatRequest request = _service.BuildRequest(mode, input, createAlternative: repeatLast, paintingStyle: _paintingStyle, animationStyle: _animationStyle, photoStyle: _photoStyle, textType: _textType, textTone: _textTone, analysisDirection: _analysisDirection, videoDirection: _videoDirection, programmingTaskType: _programmingTaskType, visualTarget: _visualTarget, iconPlatform: _iconPlatform, iconStyle: _iconStyle, graphicType: _graphicType, graphicStyle: _graphicStyle);
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
        if (!useAutoModel)
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
        _processingCts = new CancellationTokenSource();
        StartProcessingProgress();
        RefreshUiState();
        try
        {
            AiGatewayStream response = await _gateway.GeneratePromptBuilderStreamingAsync(request, _processingCts.Token);
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
                    string preview = BuildStreamingPreview(streamedResponse.ToString());
                    SetEditorText(preview);
                    lastUiUpdate = Stopwatch.GetTimestamp();
                }
            }
            string cleaned = _service.CleanResponse(streamedResponse.ToString());
            if (string.IsNullOrWhiteSpace(cleaned))
            {
                SetStatus(LocalizationService.Get("PromptBuilder_ErrorEmptyResponse"));
                return;
            }
            _originalText = input;
            _processedText = cleaned;
            _hasSuccessfulResult = true;
            _isShowingOriginal = false;
            _isShowingDiff = false;
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
            SetStatus(GetAvailabilityError(ex));
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

    internal static string GetAvailabilityError(NoAvailableConnectionException ex) => ex.Reason switch
    {
        AiAvailabilityFailureReason.NoConnectionsConfigured => LocalizationService.Get("TextProcessing_ErrorNoModels"),
        AiAvailabilityFailureReason.RateLimited => LocalizationService.Get("TextProcessing_ErrorRateLimit"),
        AiAvailabilityFailureReason.QuotaExhausted => LocalizationService.Get("TextProcessing_ErrorQuota"),
        AiAvailabilityFailureReason.Unauthorized => LocalizationService.Get("TextProcessing_ErrorUnauthorized"),
        AiAvailabilityFailureReason.Forbidden => LocalizationService.Get("TextProcessing_ErrorForbidden"),
        AiAvailabilityFailureReason.Network => LocalizationService.Get("TextProcessing_ErrorNetwork"),
        AiAvailabilityFailureReason.Timeout => LocalizationService.Get("TextProcessing_ErrorTimeout"),
        AiAvailabilityFailureReason.TemporarilyUnavailable => LocalizationService.Get("TextProcessing_ErrorUnavailable"),
        _ => ex.InnerException switch
        {
            AiProviderHttpException providerError => GetProviderError(providerError),
            HttpRequestException => LocalizationService.Get("TextProcessing_ErrorNetwork"),
            TimeoutException => LocalizationService.Get("TextProcessing_ErrorTimeout"),
            _ => LocalizationService.Get("TextProcessing_ErrorUnavailable")
        }
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
        SetStatus(string.Empty);
        SaveEditorText();
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
            SetStatus(string.Empty);
            RefreshUiState();
            FocusEditor();
        }
    }

    internal static string BuildStreamingPreview(string rawText) =>
        PromptBuilderService.HideReasoningFromStreamingPreview(rawText ?? string.Empty);

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
            ? (Brush)FindResource("PromptBuilderWarningBrush")
            : (Brush)FindResource("MutedText");
        AutomationProperties.SetName(TxtCounters, TxtCounters.Text);
        LimitBorder.Visibility = state.IsOverLimit ? Visibility.Visible : Visibility.Collapsed;
        TxtEditor.IsEnabled = state.CanEdit;
        TxtEditor.IsReadOnly = _isShowingOriginal;
        TxtEditor.Visibility = _isShowingDiff ? Visibility.Collapsed : Visibility.Visible;
        DiffViewer.Visibility = _isShowingDiff ? Visibility.Visible : Visibility.Collapsed;
        ModeProgramming.IsEnabled = state.CanSelectMode;
        ModeImages.IsEnabled = state.CanSelectMode;
        ModePaintings.IsEnabled = state.CanSelectMode;
        ModeAnimation.IsEnabled = state.CanSelectMode;
        ModeTexts.IsEnabled = state.CanSelectMode;
        ModeVideo.IsEnabled = state.CanSelectMode;
        ModeMusic.IsEnabled = state.CanSelectMode;
        ModeAnalytics.IsEnabled = state.CanSelectMode;
        CmbModels.IsEnabled = !_isProcessing && !_isLoadingModels && _hasSelectableModel;
        BtnRefreshModels.IsEnabled = !_isProcessing && !_isLoadingModels;
        BtnPaste.IsEnabled = state.CanPaste;
        BtnCopy.IsEnabled = state.CanCopy;
        BtnClear.IsEnabled = state.CanClear;
        BtnRepeat.IsEnabled = state.CanRepeat;
        BtnToggleVersion.IsEnabled = state.CanSwitchVersion;
        ToggleVersionLabel.Text = LocalizationService.Get(_isShowingOriginal
            ? "PromptBuilder_ButtonShowResult"
            : "PromptBuilder_ButtonShowOriginal");
        AutomationProperties.SetName(BtnToggleVersion, ToggleVersionLabel.Text);
        BtnProcess.IsEnabled = state.CanCancel || state.CanProcess;
        ProcessButtonLabel.Text = _isProcessing
            ? LocalizationService.Get("TextProcessing_ButtonCancel")
            : LocalizationService.Get("PromptBuilder_ButtonProcess");
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
            TxtModelState.Foreground = (Brush)FindResource("PromptBuilderWarningBrush");
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
        Button[] buttons = [BtnPaste, BtnCopy, BtnRepeat, BtnToggleVersion, BtnClear, BtnProcess];
        double commandWidth = buttons.Max(MeasureButtonWidth);
        commandWidth = Math.Max(commandWidth, MeasureButtonWidthForLabels(BtnToggleVersion, ToggleVersionLabel,
            LocalizationService.Get("PromptBuilder_ButtonShowOriginal"), LocalizationService.Get("PromptBuilder_ButtonShowResult")));
        commandWidth = Math.Max(commandWidth, MeasureButtonWidthForLabels(BtnProcess, ProcessButtonLabel,
            LocalizationService.Get("PromptBuilder_ButtonProcess"), LocalizationService.Get("TextProcessing_ButtonCancel")));
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
        unchecked { _infoStatusVersion++; }
        _inlineInfoStatus = message ?? string.Empty;
        if (IsInitialized)
        {
            RefreshUiState();
        }
    }

    private async void ShowTransientInfoStatus(string message)
    {
        SetInfoStatus(message);
        int version = _infoStatusVersion;
        await Task.Delay(TimeSpan.FromSeconds(2));
        if (version == _infoStatusVersion)
        {
            ClearInfoStatus();
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
                run.Foreground = (Brush)FindResource("PromptBuilderDiffAddedBrush");
                run.TextDecorations = TextDecorations.Underline;
            }
            else if (segment.Kind == TextDiffKind.Removed)
            {
                run.Foreground = (Brush)FindResource("PromptBuilderDiffRemovedBrush");
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
        _isApplyingMode = true;
        try
        {
            ModeProgramming.IsSelected = _currentMode == PromptBuilderCategory.Programming;
            ModeImages.IsSelected = _currentMode == PromptBuilderCategory.Images;
            ModePaintings.IsSelected = _currentMode == PromptBuilderCategory.Paintings;
            ModeAnimation.IsSelected = _currentMode == PromptBuilderCategory.Animation;
            ModeIcons.IsSelected = _currentMode == PromptBuilderCategory.Icons;
            ModeGraphics.IsSelected = _currentMode == PromptBuilderCategory.Graphics;
            ModeTexts.IsSelected = _currentMode == PromptBuilderCategory.Texts;
            ModeVideo.IsSelected = _currentMode == PromptBuilderCategory.Video;
            ModeMusic.IsSelected = _currentMode == PromptBuilderCategory.Music;
            ModeAnalytics.IsSelected = _currentMode == PromptBuilderCategory.Analysis;
        }
        finally
        {
            _isApplyingMode = false;
        }
        TxtModeDescription.Text = _currentMode switch
        {
            PromptBuilderCategory.Programming => LocalizationService.Get("PromptBuilder_ModeProgrammingDesc"),
            PromptBuilderCategory.Images => LocalizationService.Get("PromptBuilder_ModeImagesDesc"),
            PromptBuilderCategory.Paintings => LocalizationService.Get("PromptBuilder_ModePaintingsDesc"),
            PromptBuilderCategory.Animation => LocalizationService.Get("PromptBuilder_ModeAnimationDesc"),
            PromptBuilderCategory.Icons => LocalizationService.Get("PromptBuilder_ModeIconsDesc"),
            PromptBuilderCategory.Graphics => LocalizationService.Get("PromptBuilder_ModeGraphicsDesc"),
            PromptBuilderCategory.Texts => LocalizationService.Get("PromptBuilder_ModeTextsDesc"),
            PromptBuilderCategory.Video => LocalizationService.Get("PromptBuilder_ModeVideoDesc"),
            PromptBuilderCategory.Music => LocalizationService.Get("PromptBuilder_ModeMusicDesc"),
            PromptBuilderCategory.Analysis => LocalizationService.Get("PromptBuilder_ModeAnalyticsDesc"),
            _ => string.Empty
        };
        VisualOptionsHost.Visibility = _currentMode is PromptBuilderCategory.Images or PromptBuilderCategory.Paintings or PromptBuilderCategory.Animation ? Visibility.Visible : Visibility.Collapsed;
        IconOptionsHost.Visibility = _currentMode == PromptBuilderCategory.Icons ? Visibility.Visible : Visibility.Collapsed;
        GraphicOptionsHost.Visibility = _currentMode == PromptBuilderCategory.Graphics ? Visibility.Visible : Visibility.Collapsed;
        TextOptionsHost.Visibility = _currentMode == PromptBuilderCategory.Texts ? Visibility.Visible : Visibility.Collapsed;
        AnalysisDirectionHost.Visibility = _currentMode == PromptBuilderCategory.Analysis ? Visibility.Visible : Visibility.Collapsed;
        VideoDirectionHost.Visibility = _currentMode == PromptBuilderCategory.Video ? Visibility.Visible : Visibility.Collapsed;
        ProgrammingTaskHost.Visibility = _currentMode == PromptBuilderCategory.Programming ? Visibility.Visible : Visibility.Collapsed;
        RefreshVisualStyleOptions();
        RefreshTextOptions();
        RefreshAnalysisDirections();
        RefreshVideoDirections();
        RefreshProgrammingTaskTypes();
        RefreshVisualTargets();
        RefreshIconOptions();
        RefreshGraphicOptions();
    }

    private void RefreshVisualStyleOptions()
    {
        CmbVisualStyle.SelectionChanged -= CmbVisualStyle_SelectionChanged;
        CmbVisualStyle.Items.Clear();

        switch (_currentMode)
        {
            case PromptBuilderCategory.Paintings:
                foreach (PaintingStyleDefinition style in PromptBuilderService.PaintingStyles.OrderBy(style => LocalizationService.Get(style.LocalizationKey), StringComparer.CurrentCultureIgnoreCase))
                    CmbVisualStyle.Items.Add(new ComboBoxItem { Tag = style.Style, Content = LocalizationService.Get(style.LocalizationKey) });
                CmbVisualStyle.SelectedItem = CmbVisualStyle.Items.Cast<ComboBoxItem>().First(item => (PaintingStyle)item.Tag == _paintingStyle);
                break;
            case PromptBuilderCategory.Images:
                foreach (PhotoStyleDefinition style in PromptBuilderService.PhotoStyles.OrderBy(style => LocalizationService.Get(style.LocalizationKey), StringComparer.CurrentCultureIgnoreCase))
                    CmbVisualStyle.Items.Add(new ComboBoxItem { Tag = style.Style, Content = LocalizationService.Get(style.LocalizationKey) });
                CmbVisualStyle.SelectedItem = CmbVisualStyle.Items.Cast<ComboBoxItem>().First(item => (PhotoStyle)item.Tag == _photoStyle);
                break;
            case PromptBuilderCategory.Animation:
                foreach (AnimationStyleDefinition style in PromptBuilderService.AnimationStyles.OrderBy(style => LocalizationService.Get(style.LocalizationKey), StringComparer.CurrentCultureIgnoreCase))
                    CmbVisualStyle.Items.Add(new ComboBoxItem { Tag = style.Style, Content = LocalizationService.Get(style.LocalizationKey) });
                CmbVisualStyle.SelectedItem = CmbVisualStyle.Items.Cast<ComboBoxItem>().First(item => (AnimationStyle)item.Tag == _animationStyle);
                break;
        }

        CmbVisualStyle.SelectionChanged += CmbVisualStyle_SelectionChanged;
    }

    private void RefreshTextOptions()
    {
        CmbTextType.SelectionChanged -= CmbTextType_SelectionChanged;
        CmbTextTone.SelectionChanged -= CmbTextTone_SelectionChanged;
        CmbTextType.Items.Clear();
        CmbTextTone.Items.Clear();
        foreach (TextPromptTypeDefinition item in PromptBuilderService.TextPromptTypes.OrderBy(item => LocalizationService.Get(item.LocalizationKey), StringComparer.CurrentCultureIgnoreCase))
            CmbTextType.Items.Add(new ComboBoxItem { Tag = item.Type, Content = LocalizationService.Get(item.LocalizationKey) });
        foreach (TextPromptToneDefinition item in PromptBuilderService.TextPromptTones.OrderBy(item => LocalizationService.Get(item.LocalizationKey), StringComparer.CurrentCultureIgnoreCase))
            CmbTextTone.Items.Add(new ComboBoxItem { Tag = item.Tone, Content = LocalizationService.Get(item.LocalizationKey) });
        CmbTextType.SelectedItem = CmbTextType.Items.Cast<ComboBoxItem>().First(item => (TextPromptType)item.Tag == _textType);
        CmbTextTone.SelectedItem = CmbTextTone.Items.Cast<ComboBoxItem>().First(item => (TextPromptTone)item.Tag == _textTone);
        CmbTextType.SelectionChanged += CmbTextType_SelectionChanged;
        CmbTextTone.SelectionChanged += CmbTextTone_SelectionChanged;
    }

    private void CmbTextType_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CmbTextType.SelectedItem is ComboBoxItem { Tag: TextPromptType type } && _textType != type) { _textType = type; SaveCurrentMode(); UpdateTextOptionsOutcome(); }
    }

    private void CmbTextTone_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CmbTextTone.SelectedItem is ComboBoxItem { Tag: TextPromptTone tone } && _textTone != tone) { _textTone = tone; SaveCurrentMode(); UpdateTextOptionsOutcome(); }
    }

    private void CmbVisualStyle_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CmbVisualStyle.SelectedItem is not ComboBoxItem { Tag: object style }) return;

        bool changed = style switch
        {
            PaintingStyle painting when _paintingStyle != painting => SetPaintingStyle(painting),
            PhotoStyle photo when _photoStyle != photo => SetPhotoStyle(photo),
            AnimationStyle animation when _animationStyle != animation => SetAnimationStyle(animation),
            _ => false
        };
        if (changed) SaveCurrentMode();
    }

    private bool SetPaintingStyle(PaintingStyle style) { _paintingStyle = style; return true; }
    private bool SetPhotoStyle(PhotoStyle style) { _photoStyle = style; return true; }
    private bool SetAnimationStyle(AnimationStyle style) { _animationStyle = style; return true; }

    private void RefreshAnalysisDirections()
    {
        CmbAnalysisDirection.SelectionChanged -= CmbAnalysisDirection_SelectionChanged;
        CmbAnalysisDirection.Items.Clear();
        foreach (AnalysisDirectionDefinition item in PromptBuilderService.AnalysisDirections
                     .OrderBy(item => LocalizationService.Get(item.LocalizationKey), StringComparer.CurrentCultureIgnoreCase))
        {
            CmbAnalysisDirection.Items.Add(new ComboBoxItem { Tag = item.Direction, Content = LocalizationService.Get(item.LocalizationKey) });
        }
        CmbAnalysisDirection.SelectedItem = CmbAnalysisDirection.Items.Cast<ComboBoxItem>().First(item => (AnalysisDirection)item.Tag == _analysisDirection);
        CmbAnalysisDirection.SelectionChanged += CmbAnalysisDirection_SelectionChanged;
        UpdateAnalysisDirectionOutcome();
    }

    private void CmbAnalysisDirection_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CmbAnalysisDirection.SelectedItem is ComboBoxItem { Tag: AnalysisDirection direction } && _analysisDirection != direction)
        {
            _analysisDirection = direction;
            SaveCurrentMode();
            UpdateAnalysisDirectionOutcome();
        }
    }

    private void UpdateAnalysisDirectionOutcome()
    {
        AnalysisDirectionDefinition definition = PromptBuilderService.AnalysisDirections.FirstOrDefault(item => item.Direction == _analysisDirection)
            ?? PromptBuilderService.AnalysisDirections[0];
        string outcome = $"{LocalizationService.Get("PromptBuilder_AnalysisInputHint")} {LocalizationService.Get(definition.OutcomeLocalizationKey)}";
        TxtAnalysisDirectionOutcome.Text = outcome;
        TxtAnalysisDirectionOutcome.ToolTip = outcome;
    }

    private void RefreshVideoDirections()
    {
        CmbVideoDirection.SelectionChanged -= CmbVideoDirection_SelectionChanged;
        CmbVideoDirection.Items.Clear();
        foreach (VideoDirectionDefinition item in PromptBuilderService.VideoDirections
                     .OrderBy(item => LocalizationService.Get(item.LocalizationKey), StringComparer.CurrentCultureIgnoreCase))
        {
            CmbVideoDirection.Items.Add(new ComboBoxItem { Tag = item.Direction, Content = LocalizationService.Get(item.LocalizationKey) });
        }
        CmbVideoDirection.SelectedItem = CmbVideoDirection.Items.Cast<ComboBoxItem>().First(item => (VideoDirection)item.Tag == _videoDirection);
        CmbVideoDirection.SelectionChanged += CmbVideoDirection_SelectionChanged;
        UpdateVideoDirectionOutcome();
    }

    private void CmbVideoDirection_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CmbVideoDirection.SelectedItem is ComboBoxItem { Tag: VideoDirection direction } && _videoDirection != direction)
        {
            _videoDirection = direction;
            SaveCurrentMode();
            UpdateVideoDirectionOutcome();
        }
    }

    private void RefreshProgrammingTaskTypes()
    {
        CmbProgrammingTask.SelectionChanged -= CmbProgrammingTask_SelectionChanged;
        CmbProgrammingTask.Items.Clear();
        foreach (ProgrammingTaskTypeDefinition item in PromptBuilderService.ProgrammingTaskTypes
                     .OrderBy(item => LocalizationService.Get(item.LocalizationKey), StringComparer.CurrentCultureIgnoreCase))
        {
            CmbProgrammingTask.Items.Add(new ComboBoxItem { Tag = item.Type, Content = LocalizationService.Get(item.LocalizationKey) });
        }
        CmbProgrammingTask.SelectedItem = CmbProgrammingTask.Items.Cast<ComboBoxItem>().First(item => (ProgrammingTaskType)item.Tag == _programmingTaskType);
        CmbProgrammingTask.SelectionChanged += CmbProgrammingTask_SelectionChanged;
        UpdateProgrammingTaskOutcome();
    }

    private void CmbProgrammingTask_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CmbProgrammingTask.SelectedItem is ComboBoxItem { Tag: ProgrammingTaskType type } && _programmingTaskType != type)
        {
            _programmingTaskType = type;
            SaveCurrentMode();
            UpdateProgrammingTaskOutcome();
        }
    }

    private static string FormatOptionOutcome(string resourceKey, string option) =>
        string.Format(CultureInfo.CurrentCulture, LocalizationService.Get(resourceKey), option);

    private void UpdateProgrammingTaskOutcome() =>
        TxtProgrammingTaskOutcome.Text = FormatOptionOutcome("PromptBuilder_ProgrammingOutcome", LocalizationService.Get((PromptBuilderService.ProgrammingTaskTypes.First(item => item.Type == _programmingTaskType)).LocalizationKey));

    private void UpdateVideoDirectionOutcome() =>
        TxtVideoDirectionOutcome.Text = FormatOptionOutcome("PromptBuilder_VideoOutcome", LocalizationService.Get((PromptBuilderService.VideoDirections.First(item => item.Direction == _videoDirection)).LocalizationKey));

    private void UpdateTextOptionsOutcome() =>
        TxtTextOptionsOutcome.Text = string.Format(CultureInfo.CurrentCulture, LocalizationService.Get("PromptBuilder_TextOutcome"),
            LocalizationService.Get((PromptBuilderService.TextPromptTypes.First(item => item.Type == _textType)).LocalizationKey),
            LocalizationService.Get((PromptBuilderService.TextPromptTones.First(item => item.Tone == _textTone)).LocalizationKey));

    private void RefreshVisualTargets()
    {
        CmbVisualTarget.SelectionChanged -= CmbVisualTarget_SelectionChanged;
        CmbVisualTarget.Items.Clear();
        foreach (VisualTargetModelDefinition item in PromptBuilderService.VisualTargetModels)
            CmbVisualTarget.Items.Add(new ComboBoxItem { Tag = item.Model, Content = LocalizationService.Get(item.LocalizationKey) });
        CmbVisualTarget.SelectedItem = CmbVisualTarget.Items.Cast<ComboBoxItem>().First(item => (VisualTargetModel)item.Tag == _visualTarget);
        CmbVisualTarget.SelectionChanged += CmbVisualTarget_SelectionChanged;
    }

    private void CmbVisualTarget_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CmbVisualTarget.SelectedItem is ComboBoxItem { Tag: VisualTargetModel target } && _visualTarget != target)
        {
            _visualTarget = target;
            SaveCurrentMode();
        }
    }

    private void RefreshIconOptions()
    {
        PopulateVisualTargets(CmbIconTarget);
        PopulateOptions(CmbIconPlatform, PromptBuilderService.IconPlatforms, item => item.Platform, item => item.LocalizationKey, _iconPlatform);
        PopulateOptions(CmbIconStyle, PromptBuilderService.IconStyles, item => item.Style, item => item.LocalizationKey, _iconStyle);
    }

    private void RefreshGraphicOptions()
    {
        PopulateVisualTargets(CmbGraphicTarget);
        PopulateOptions(CmbGraphicType, PromptBuilderService.GraphicTypes, item => item.Type, item => item.LocalizationKey, _graphicType);
        PopulateOptions(CmbGraphicStyle, PromptBuilderService.GraphicStyles, item => item.Style, item => item.LocalizationKey, _graphicStyle);
    }

    private void PopulateOptions<TDefinition, TValue>(ComboBox comboBox, IEnumerable<TDefinition> definitions, Func<TDefinition, TValue> value, Func<TDefinition, string> localizationKey, TValue selected)
        where TValue : struct
    {
        comboBox.SelectionChanged -= CmbIconOrGraphicOption_SelectionChanged;
        comboBox.Items.Clear();
        foreach (TDefinition item in definitions.OrderBy(localizationKey, StringComparer.CurrentCultureIgnoreCase))
            comboBox.Items.Add(new ComboBoxItem { Tag = value(item), Content = LocalizationService.Get(localizationKey(item)) });
        comboBox.SelectedItem = comboBox.Items.Cast<ComboBoxItem>().First(item => EqualityComparer<TValue>.Default.Equals((TValue)item.Tag, selected));
        comboBox.SelectionChanged += CmbIconOrGraphicOption_SelectionChanged;
    }

    private void PopulateVisualTargets(ComboBox comboBox)
    {
        comboBox.SelectionChanged -= CmbIconOrGraphicTarget_SelectionChanged;
        comboBox.Items.Clear();
        foreach (VisualTargetModelDefinition item in PromptBuilderService.VisualTargetModels)
            comboBox.Items.Add(new ComboBoxItem { Tag = item.Model, Content = LocalizationService.Get(item.LocalizationKey) });
        comboBox.SelectedItem = comboBox.Items.Cast<ComboBoxItem>().First(item => (VisualTargetModel)item.Tag == _visualTarget);
        comboBox.SelectionChanged += CmbIconOrGraphicTarget_SelectionChanged;
    }

    private void CmbIconOrGraphicTarget_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (((ComboBox)sender).SelectedItem is ComboBoxItem { Tag: VisualTargetModel target } && _visualTarget != target)
        {
            _visualTarget = target;
            SaveCurrentMode();
        }
    }

    private void CmbIconOrGraphicOption_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (((ComboBox)sender).SelectedItem is not ComboBoxItem { Tag: object value }) return;

        bool changed = value switch
        {
            IconPlatform platform when _iconPlatform != platform => SetIconPlatform(platform),
            IconStyle style when _iconStyle != style => SetIconStyle(style),
            GraphicType type when _graphicType != type => SetGraphicType(type),
            GraphicStyle style when _graphicStyle != style => SetGraphicStyle(style),
            _ => false
        };
        if (changed) SaveCurrentMode();
    }

    private bool SetIconPlatform(IconPlatform value) { _iconPlatform = value; return true; }
    private bool SetIconStyle(IconStyle value) { _iconStyle = value; return true; }
    private bool SetGraphicType(GraphicType value) { _graphicType = value; return true; }
    private bool SetGraphicStyle(GraphicStyle value) { _graphicStyle = value; return true; }


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
        _hasAutomaticModel = false;
        _hasSelectableModel = false;
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
        _hasAutomaticModel = logicalModels.Any(model =>
            model.Tier == TextProcessingModelTier.CertifiedAutomatic);
        _hasSelectableModel = logicalModels.Count > 0;
        RestoreModelSelection();
        UpdateSelectedModelAvailability();
        _isLoadingModels = false;
        RefreshUiState();
    }

    internal static bool IsEligibleModel(AiModelDescriptor model) =>
        HasVisibleModelText(model.ModelId) &&
        !model.IsDeprecated &&
        (model.Capabilities & AiCapabilities.Text) == AiCapabilities.Text &&
        (model.CostStatus is AiCostStatus.VerifiedFree or AiCostStatus.FreeTierAvailable) &&
        TextProcessingModelPolicy.Classify(model) != TextProcessingModelTier.Unsupported;

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
                Item = new ModelItem(
                    first.ProviderId,
                    first.ModelId,
                    display,
                    contextLength,
                    TextProcessingModelPolicy.Classify(first))
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
        UpdateSelectedModelAvailability();
        return true;
    }

    private void UpdateSelectedModelAvailability()
    {
        _hasEligibleModel = _isAutoModel
            ? _hasAutomaticModel
            : CmbModels.SelectedItem is ModelItem selected &&
              selected.ModelId != null &&
              selected.Tier != TextProcessingModelTier.Unsupported;
    }

    private void RestoreModelSelection()
    {
        AppSettings settings = _settingsService.Settings;
        bool hadLegacyConnectionSelection =
            !string.IsNullOrWhiteSpace(settings.PromptBuilderSelectedConnectionId);
        _isAutoModel = settings.PromptBuilderIsAutoModel;
        bool replacedUnavailableSelection = false;
        if (!_isAutoModel)
        {
            ModelItem? model = FindModel(
                settings.PromptBuilderSelectedProviderId,
                settings.PromptBuilderSelectedModelId);
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
            settings.PromptBuilderIsAutoModel = _isAutoModel;
            settings.PromptBuilderSelectedConnectionId = null;
            settings.PromptBuilderSelectedProviderId = _isAutoModel ? null : _selectedProviderId;
            settings.PromptBuilderSelectedModelId = _isAutoModel ? null : _selectedModelId;
        });
    }

    private void RestoreWindowState(AppSettings settings)
    {
        bool useOwnPlacement = settings.PromptBuilderWindowPlacementInitialized;
        double? savedLeft = useOwnPlacement ? settings.PromptBuilderLeft : settings.TextProcessingLeft;
        double? savedTop = useOwnPlacement ? settings.PromptBuilderTop : settings.TextProcessingTop;
        double? savedWidth = useOwnPlacement ? settings.PromptBuilderWidth : settings.TextProcessingWidth;
        double? savedHeight = useOwnPlacement ? settings.PromptBuilderHeight : settings.TextProcessingHeight;
        string? savedWindowState = useOwnPlacement ? settings.PromptBuilderWindowState : settings.TextProcessingWindowState;

        Forms.Screen screen = GetTargetScreen(savedLeft, savedTop);
        System.Drawing.Rectangle work = screen.WorkingArea;
        double maxWidth = Math.Max(640, work.Width * WorkAreaRatio);
        double maxHeight = Math.Max(560, work.Height * WorkAreaRatio);
        MinWidth = Math.Min(_requiredMinWidth, maxWidth);
        MinHeight = Math.Min(PreferredMinHeight, maxHeight);
        Width = Math.Clamp(savedWidth ?? PreferredWidth, MinWidth, Math.Min(MaxWidth, maxWidth));
        Height = Math.Clamp(savedHeight ?? PreferredHeight, MinHeight, Math.Min(MaxHeight, maxHeight));
        double desiredLeft = savedLeft ?? work.Left + (work.Width - Width) / 2;
        double desiredTop = savedTop ?? work.Top + (work.Height - Height) / 2;
        Left = Math.Clamp(desiredLeft, work.Left, Math.Max(work.Left, work.Right - Width));
        Top = Math.Clamp(desiredTop, work.Top, Math.Max(work.Top, work.Bottom - Height));
        WindowStartupLocation = WindowStartupLocation.Manual;
        WindowState = string.Equals(savedWindowState, "Maximized", StringComparison.Ordinal)
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
            settings.PromptBuilderLeft = bounds.Left;
            settings.PromptBuilderTop = bounds.Top;
            settings.PromptBuilderWidth = bounds.Width;
            settings.PromptBuilderHeight = bounds.Height;
            settings.PromptBuilderWindowState = state;
            settings.PromptBuilderWindowPlacementInitialized = true;
        });
    }
}
