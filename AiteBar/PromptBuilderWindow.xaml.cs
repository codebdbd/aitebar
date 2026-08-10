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
    private PaintingStyleSection _paintingSection = PaintingStyleSection.All;
    private PaintingArtist _paintingArtist = PaintingArtist.Auto;
    private AnimationStyle _animationStyle = AnimationStyle.Auto;
    private AnimationStyleSection _animationSection = AnimationStyleSection.All;
    private PhotoSection _photoSection = PhotoSection.All;
    private PhotoStyle _photoStyle = PhotoStyle.Auto;
    private ThemeSection _themeSection = ThemeSection.All;
    private ThemeStyle _themeStyle = ThemeStyle.Auto;
    private TextPromptType _textType = TextPromptType.Auto;
    private TextPromptTone _textTone = TextPromptTone.Neutral;
    private AnalysisDirection _analysisDirection = AnalysisDirection.Auto;
    private VideoDirection _videoDirection = VideoDirection.Auto;
    private ProgrammingProjectType _programmingProjectType = ProgrammingProjectType.Auto;
    private ProgrammingPromptStyle _programmingStyle = ProgrammingPromptStyle.Auto;
    private VisualTargetModel _visualTarget = VisualTargetModel.Universal;
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
        MainWindow? mainWindow = null,
        AiGateway? gateway = null)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _mainWindow = mainWindow;
        _gateway = gateway ?? new AiGateway(settingsService);
        _currentMode = PromptBuilderCategory.Programming;
        InitializeComponent();
        SourceInitialized += OnSourceInitialized;
        _progressTimer = new DispatcherTimer(TimeSpan.FromSeconds(1), DispatcherPriority.Background, (_, _) =>
            UpdateProcessingProgress(), Dispatcher);
        CmbModels.ItemsSource = _models;
        AddAutomaticModelOption();
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

    private void RestoreEditorText(AppSettings settings, bool allowLastTextFallback = true)
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
                ?? (allowLastTextFallback && _currentMode == (PromptBuilderCategory)settings.PromptBuilderLastMode ? settings.PromptBuilderLastText : null)
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
        if (category == PromptBuilderCategory.Graphics && drafts != null && drafts.TryGetValue(GetDraftKey(PromptBuilderCategory.Icons), out PromptBuilderDraft? iconDraft)) return iconDraft;
        return null;
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
        RestoreEditorText(_settingsService.Settings, allowLastTextFallback: false);
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
            settings.PromptBuilderPaintingSection = _paintingSection;
            settings.PromptBuilderPaintingArtist = _paintingArtist;
            settings.PromptBuilderAnimationStyle = _animationStyle;
            settings.PromptBuilderAnimationSection = _animationSection;
            settings.PromptBuilderPhotoSection = _photoSection;
            settings.PromptBuilderPhotoStyle = _photoStyle;
            settings.PromptBuilderThemeSection = _themeSection;
            settings.PromptBuilderThemeStyle = _themeStyle;
            settings.PromptBuilderTextType = _textType;
            settings.PromptBuilderTextTone = _textTone;
            settings.PromptBuilderAnalysisDirection = _analysisDirection;
            settings.PromptBuilderVideoDirection = _videoDirection;
            settings.PromptBuilderProgrammingProjectType = _programmingProjectType;
            settings.PromptBuilderProgrammingStyle = _programmingStyle;
            settings.PromptBuilderVisualTarget = _visualTarget;
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
            (int)PromptBuilderCategory.Ideas => PromptBuilderCategory.Ideas,
            (int)PromptBuilderCategory.Paintings => PromptBuilderCategory.Paintings,
            (int)PromptBuilderCategory.Animation => PromptBuilderCategory.Animation,
            (int)PromptBuilderCategory.Icons => PromptBuilderCategory.Graphics,
            (int)PromptBuilderCategory.Graphics => PromptBuilderCategory.Graphics,
            _ => PromptBuilderCategory.Programming
        };
        _currentMode = restoredMode;
        _paintingStyle = _settingsService.Settings.PromptBuilderPaintingStyle;
        if (!PromptBuilderService.PaintingStyles.Any(style => style.Style == _paintingStyle)) _paintingStyle = PaintingStyle.Auto;
        _paintingSection = _settingsService.Settings.PromptBuilderPaintingSection;
        if (!PromptBuilderService.PaintingStyleSections.Any(section => section.Section == _paintingSection)) _paintingSection = PaintingStyleSection.All;
        _paintingArtist = _settingsService.Settings.PromptBuilderPaintingArtist;
        if (!PromptBuilderService.PaintingArtists.Any(artist => artist.Artist == _paintingArtist)) _paintingArtist = PaintingArtist.Auto;
        if (!PromptBuilderService.GetPaintingStyles(_paintingSection).Any(style => style.Style == _paintingStyle)) _paintingStyle = PaintingStyle.Auto;
        _animationStyle = _settingsService.Settings.PromptBuilderAnimationStyle;
        if (!PromptBuilderService.AnimationStyles.Any(style => style.Style == _animationStyle)) _animationStyle = AnimationStyle.Auto;
        _animationSection = _settingsService.Settings.PromptBuilderAnimationSection;
        if (!PromptBuilderService.AnimationStyleSections.Any(section => section.Section == _animationSection)) _animationSection = AnimationStyleSection.All;
        if (!PromptBuilderService.GetAnimationStyles(_animationSection).Any(style => style.Style == _animationStyle)) _animationStyle = AnimationStyle.Auto;
        _photoSection = _settingsService.Settings.PromptBuilderPhotoSection;
        if (!PromptBuilderService.PhotoSections.Any(section => section.Section == _photoSection)) _photoSection = PhotoSection.All;
        _photoStyle = _settingsService.Settings.PromptBuilderPhotoStyle;
        if (!PromptBuilderService.GetPhotoStyles(_photoSection).Any(style => style.Style == _photoStyle)) _photoStyle = PhotoStyle.Auto;
        _themeSection = _settingsService.Settings.PromptBuilderThemeSection;
        if (!PromptBuilderService.ThemeSections.Any(section => section.Section == _themeSection)) _themeSection = ThemeSection.All;
        _themeStyle = _settingsService.Settings.PromptBuilderThemeStyle;
        if (!PromptBuilderService.GetThemeStyles(_themeSection).Any(style => style.Style == _themeStyle)) _themeStyle = ThemeStyle.Auto;
        _textType = _settingsService.Settings.PromptBuilderTextType;
        if (!PromptBuilderService.TextPromptTypes.Any(style => style.Type == _textType)) _textType = TextPromptType.Auto;
        _textTone = _settingsService.Settings.PromptBuilderTextTone;
        if (!PromptBuilderService.TextPromptTones.Any(style => style.Tone == _textTone)) _textTone = TextPromptTone.Neutral;
        _analysisDirection = _settingsService.Settings.PromptBuilderAnalysisDirection;
        if (!PromptBuilderService.AnalysisDirections.Any(item => item.Direction == _analysisDirection)) _analysisDirection = AnalysisDirection.Auto;
        _videoDirection = _settingsService.Settings.PromptBuilderVideoDirection;
        if (!PromptBuilderService.VideoDirections.Any(item => item.Direction == _videoDirection)) _videoDirection = VideoDirection.Auto;
        _programmingProjectType = _settingsService.Settings.PromptBuilderProgrammingProjectType;
        if (!PromptBuilderService.ProgrammingProjectTypes.Any(item => item.Type == _programmingProjectType)) _programmingProjectType = ProgrammingProjectType.Auto;
        _programmingStyle = _settingsService.Settings.PromptBuilderProgrammingStyle;
        if (!PromptBuilderService.GetProgrammingStyles(_programmingProjectType).Any(item => item.Style == _programmingStyle)) _programmingStyle = ProgrammingPromptStyle.Auto;
        _visualTarget = _settingsService.Settings.PromptBuilderVisualTarget;
        if (!PromptBuilderService.VisualTargetModels.Any(item => item.Model == _visualTarget)) _visualTarget = VisualTargetModel.Universal;
        _iconStyle = _settingsService.Settings.PromptBuilderIconStyle;
        if (!PromptBuilderService.IconStyles.Any(item => item.Style == _iconStyle)) _iconStyle = IconStyle.Auto;
        _graphicType = _settingsService.Settings.PromptBuilderGraphicType;
        if (!PromptBuilderService.GraphicTypes.Any(item => item.Type == _graphicType)) _graphicType = GraphicType.Auto;
        _graphicStyle = _settingsService.Settings.PromptBuilderGraphicStyle;
        if (!PromptBuilderService.GetGraphicStyles(_graphicType).Any(item => item.Style == _graphicStyle)) _graphicStyle = GraphicStyle.Auto;
        if (storedMode == (int)PromptBuilderCategory.Icons && _graphicType == GraphicType.Auto)
        {
            _graphicType = GraphicType.Icon;
        }
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
            "Ideas" => PromptBuilderCategory.Ideas,
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
        RestoreEditorText(_settingsService.Settings, allowLastTextFallback: false);
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
            // A manual edit starts a new brief. Keep the previous source only for
            // the explicit repeat action, never as an implicit replacement.
            ResetResultHistory();
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
            input = GetEffectiveProcessInputText();
            mode = _currentMode;
            useAutoModel = _isAutoModel;
            providerId = useAutoModel ? null : _selectedProviderId;
            modelId = useAutoModel ? null : _selectedModelId;
            if (!GetProcessUiState().CanProcess)
            {
                if (string.IsNullOrWhiteSpace(input))
                {
                    SetStatus(LocalizationService.Get("PromptBuilder_ErrorEmptyInput"));
                }
                else if (input.Length > TextProcessingService.MaxInputLength)
                {
                    SetStatus(LocalizationService.Get("TextProcessing_ErrorInputTooLarge"));
                }
                else if (!_hasEligibleModel)
                {
                    SetStatus(LocalizationService.Get("TextProcessing_ErrorNoModels"));
                }
                return;
            }
        }

        AiChatRequest request = _service.BuildRequest(mode, input, createAlternative: repeatLast, photoSection: _photoSection, paintingStyle: _paintingStyle, paintingArtist: _paintingArtist, animationStyle: _animationStyle, photoStyle: _photoStyle, textType: _textType, textTone: _textTone, analysisDirection: _analysisDirection, videoDirection: _videoDirection, programmingProjectType: _programmingProjectType, programmingStyle: _programmingStyle, visualTarget: _visualTarget, themeSection: _themeSection, themeStyle: _themeStyle, iconStyle: _iconStyle, graphicType: _graphicType, graphicStyle: _graphicStyle, animationSection: _animationSection, paintingSection: _paintingSection);
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

    internal static string GetEffectiveProcessInputText(
        string? editorText,
        bool hasSuccessfulResult,
        bool isShowingOriginal,
        string? originalText,
        string? processedText)
    {
        string visibleText = editorText ?? string.Empty;
        if (hasSuccessfulResult &&
            !isShowingOriginal &&
            string.Equals(visibleText, processedText ?? string.Empty, StringComparison.Ordinal))
        {
            return originalText ?? string.Empty;
        }

        return visibleText;
    }

    private string GetEffectiveProcessInputText() => GetEffectiveProcessInputText(
        TxtEditor.Text,
        _hasSuccessfulResult,
        _isShowingOriginal,
        _originalText,
        _processedText);

    private TextProcessingUiState GetUiState(string text) => TextProcessingUiState.Create(new TextProcessingUiStateInput(
        text,
        _isProcessing,
        _isLoadingModels,
        _hasEligibleModel,
        _hasClipboardText,
        _hasSuccessfulResult));

    private TextProcessingUiState GetVisibleUiState() => GetUiState(TxtEditor.Text);

    private TextProcessingUiState GetProcessUiState() => GetUiState(GetEffectiveProcessInputText());

    private void RefreshUiState()
    {
        if (!IsInitialized)
        {
            return;
        }
        TextProcessingUiState visibleState = GetVisibleUiState();
        TextProcessingUiState processState = GetProcessUiState();
        TxtPlaceholder.Visibility = visibleState.CharacterCount == 0 ? Visibility.Visible : Visibility.Collapsed;
        TxtCounters.Text = $"{LocalizationService.Format("TextProcessing_Characters", visibleState.CharacterCount)} · {LocalizationService.Format("TextProcessing_Words", visibleState.WordCount)}";
        TxtCounters.Foreground = processState.IsOverLimit
            ? (Brush)FindResource("PromptBuilderWarningBrush")
            : (Brush)FindResource("MutedText");
        AutomationProperties.SetName(TxtCounters, TxtCounters.Text);
        LimitBorder.Visibility = processState.IsOverLimit ? Visibility.Visible : Visibility.Collapsed;
        TxtEditor.IsEnabled = visibleState.CanEdit;
        TxtEditor.IsReadOnly = _isShowingOriginal;
        TxtEditor.Visibility = _isShowingDiff ? Visibility.Collapsed : Visibility.Visible;
        DiffViewer.Visibility = _isShowingDiff ? Visibility.Visible : Visibility.Collapsed;
        ModeProgramming.IsEnabled = visibleState.CanSelectMode;
        ModeImages.IsEnabled = visibleState.CanSelectMode;
        ModePaintings.IsEnabled = visibleState.CanSelectMode;
        ModeAnimation.IsEnabled = visibleState.CanSelectMode;
        ModeIdeas.IsEnabled = visibleState.CanSelectMode;
        ModeGraphics.IsEnabled = visibleState.CanSelectMode;
        ModeTexts.IsEnabled = visibleState.CanSelectMode;
        ModeVideo.IsEnabled = visibleState.CanSelectMode;
        ModeMusic.IsEnabled = visibleState.CanSelectMode;
        ModeAnalytics.IsEnabled = visibleState.CanSelectMode;
        CmbModels.IsEnabled = !_isProcessing && !_isLoadingModels && _hasSelectableModel;
        BtnRefreshModels.IsEnabled = !_isProcessing && !_isLoadingModels;
        BtnPaste.IsEnabled = visibleState.CanPaste;
        BtnCopy.IsEnabled = visibleState.CanCopy;
        BtnClear.IsEnabled = visibleState.CanClear;
        BtnRepeat.IsEnabled = processState.CanRepeat;
        BtnToggleVersion.IsEnabled = visibleState.CanSwitchVersion;
        ToggleVersionLabel.Text = LocalizationService.Get(_isShowingOriginal
            ? "PromptBuilder_ButtonShowResult"
            : "PromptBuilder_ButtonShowOriginal");
        AutomationProperties.SetName(BtnToggleVersion, ToggleVersionLabel.Text);
        BtnProcess.IsEnabled = processState.CanCancel || processState.CanProcess;
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
            ModeIdeas.IsSelected = _currentMode == PromptBuilderCategory.Ideas;
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
            PromptBuilderCategory.Ideas => LocalizationService.Get("PromptBuilder_ModeIdeasDesc"),
            PromptBuilderCategory.Graphics => LocalizationService.Get("PromptBuilder_ModeGraphicsDesc"),
            PromptBuilderCategory.Texts => LocalizationService.Get("PromptBuilder_ModeTextsDesc"),
            PromptBuilderCategory.Video => LocalizationService.Get("PromptBuilder_ModeVideoDesc"),
            PromptBuilderCategory.Music => LocalizationService.Get("PromptBuilder_ModeMusicDesc"),
            PromptBuilderCategory.Analysis => LocalizationService.Get("PromptBuilder_ModeAnalyticsDesc"),
            _ => string.Empty
        };
        VisualOptionsHost.Visibility = _currentMode is PromptBuilderCategory.Images or PromptBuilderCategory.Paintings or PromptBuilderCategory.Animation or PromptBuilderCategory.Ideas ? Visibility.Visible : Visibility.Collapsed;
        GraphicOptionsHost.Visibility = _currentMode == PromptBuilderCategory.Graphics ? Visibility.Visible : Visibility.Collapsed;
        TextOptionsHost.Visibility = _currentMode == PromptBuilderCategory.Texts ? Visibility.Visible : Visibility.Collapsed;
        AnalysisDirectionHost.Visibility = _currentMode == PromptBuilderCategory.Analysis ? Visibility.Visible : Visibility.Collapsed;
        VideoDirectionHost.Visibility = _currentMode == PromptBuilderCategory.Video ? Visibility.Visible : Visibility.Collapsed;
        ProgrammingTaskHost.Visibility = _currentMode == PromptBuilderCategory.Programming ? Visibility.Visible : Visibility.Collapsed;
        RefreshVisualStyleOptions();
        RefreshTextOptions();
        RefreshAnalysisDirections();
        RefreshVideoDirections();
        RefreshProgrammingOptions();
        RefreshVisualTargets();
        RefreshGraphicOptions();
    }

    private void RefreshVisualStyleOptions()
    {
        ConfigureAnimationSectionFilter();
        CmbVisualStyle.SelectionChanged -= CmbVisualStyle_SelectionChanged;
        CmbVisualStyle.Items.Clear();

        switch (_currentMode)
        {
            case PromptBuilderCategory.Paintings:
                RefreshPaintingSections();
                if (_paintingSection == PaintingStyleSection.Artists)
                {
                    foreach (PaintingArtistDefinition artist in OrderAutoFirst(PromptBuilderService.PaintingArtists, item => item.Artist == PaintingArtist.Auto, item => item.LocalizationKey))
                        CmbVisualStyle.Items.Add(new ComboBoxItem { Tag = artist.Artist, Content = LocalizationService.Get(artist.LocalizationKey) });
                    CmbVisualStyle.SelectedItem = CmbVisualStyle.Items.Cast<ComboBoxItem>().First(item => (PaintingArtist)item.Tag == _paintingArtist);
                }
                else
                {
                    foreach (PaintingStyleDefinition style in OrderAutoFirst(PromptBuilderService.GetPaintingStyles(_paintingSection), style => style.Style == PaintingStyle.Auto, style => style.LocalizationKey))
                        CmbVisualStyle.Items.Add(new ComboBoxItem { Tag = style.Style, Content = LocalizationService.Get(style.LocalizationKey) });
                    CmbVisualStyle.SelectedItem = CmbVisualStyle.Items.Cast<ComboBoxItem>().First(item => (PaintingStyle)item.Tag == _paintingStyle);
                }
                break;
            case PromptBuilderCategory.Images:
                RefreshPhotoSections();
                foreach (PhotoStyleDefinition style in OrderAutoFirst(PromptBuilderService.GetPhotoStyles(_photoSection), style => style.Style == PhotoStyle.Auto, style => style.LocalizationKey))
                    CmbVisualStyle.Items.Add(new ComboBoxItem { Tag = style.Style, Content = LocalizationService.Get(style.LocalizationKey) });
                CmbVisualStyle.SelectedItem = CmbVisualStyle.Items.Cast<ComboBoxItem>().First(item => (PhotoStyle)item.Tag == _photoStyle);
                break;
            case PromptBuilderCategory.Animation:
                RefreshAnimationSections();
                foreach (AnimationStyleDefinition style in OrderAutoFirst(PromptBuilderService.GetAnimationStyles(_animationSection), style => style.Style == AnimationStyle.Auto, style => style.LocalizationKey))
                    CmbVisualStyle.Items.Add(new ComboBoxItem { Tag = style.Style, Content = LocalizationService.Get(style.LocalizationKey) });
                CmbVisualStyle.SelectedItem = CmbVisualStyle.Items.Cast<ComboBoxItem>().First(item => (AnimationStyle)item.Tag == _animationStyle);
                break;
            case PromptBuilderCategory.Ideas:
                RefreshThemeSections();
                foreach (ThemeStyleDefinition style in OrderAutoFirst(PromptBuilderService.GetThemeStyles(_themeSection), style => style.Style == ThemeStyle.Auto, style => style.LocalizationKey))
                    CmbVisualStyle.Items.Add(new ComboBoxItem { Tag = style.Style, Content = LocalizationService.Get(style.LocalizationKey) });
                CmbVisualStyle.SelectedItem = CmbVisualStyle.Items.Cast<ComboBoxItem>().First(item => (ThemeStyle)item.Tag == _themeStyle);
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
        foreach (TextPromptTypeDefinition item in OrderAutoFirst(PromptBuilderService.TextPromptTypes, item => item.Type == TextPromptType.Auto, item => item.LocalizationKey))
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
            PaintingArtist artist when _paintingArtist != artist => SetPaintingArtist(artist),
            PhotoStyle photo when _photoStyle != photo => SetPhotoStyle(photo),
            AnimationStyle animation when _animationStyle != animation => SetAnimationStyle(animation),
            ThemeStyle theme when _themeStyle != theme => SetThemeStyle(theme),
            _ => false
        };
        if (changed) SaveCurrentMode();
    }

    private bool SetPaintingStyle(PaintingStyle style) { _paintingStyle = style; return true; }
    private bool SetPaintingArtist(PaintingArtist artist) { _paintingArtist = artist; return true; }
    private bool SetPhotoStyle(PhotoStyle style) { _photoStyle = style; return true; }
    private bool SetAnimationStyle(AnimationStyle style) { _animationStyle = style; return true; }
    private bool SetThemeStyle(ThemeStyle style) { _themeStyle = style; return true; }

    private void ConfigureAnimationSectionFilter()
    {
        bool isImages = _currentMode == PromptBuilderCategory.Images;
        bool isAnimation = _currentMode == PromptBuilderCategory.Animation;
        bool isPaintings = _currentMode == PromptBuilderCategory.Paintings;
        bool isIdeas = _currentMode == PromptBuilderCategory.Ideas;
        TxtPhotoSectionLabel.Visibility = isImages ? Visibility.Visible : Visibility.Collapsed;
        CmbPhotoSection.Visibility = isImages ? Visibility.Visible : Visibility.Collapsed;
        TxtAnimationSectionLabel.Visibility = isAnimation ? Visibility.Visible : Visibility.Collapsed;
        CmbAnimationSection.Visibility = isAnimation ? Visibility.Visible : Visibility.Collapsed;
        TxtPaintingSectionLabel.Visibility = isPaintings ? Visibility.Visible : Visibility.Collapsed;
        CmbPaintingSection.Visibility = isPaintings ? Visibility.Visible : Visibility.Collapsed;
        TxtThemeSectionLabel.Visibility = isIdeas ? Visibility.Visible : Visibility.Collapsed;
        CmbThemeSection.Visibility = isIdeas ? Visibility.Visible : Visibility.Collapsed;
        bool hasSectionFilter = isImages || isAnimation || isPaintings || isIdeas;
        AnimationSectionLabelColumn.Width = hasSectionFilter ? GridLength.Auto : new GridLength(0);
        AnimationSectionLeadingGapColumn.Width = new GridLength(hasSectionFilter ? 10 : 0);
        AnimationSectionColumn.Width = new GridLength(hasSectionFilter ? 180 : 0);
        AnimationSectionTrailingGapColumn.Width = new GridLength(hasSectionFilter ? 20 : 0);
        VisualStyleColumn.Width = new GridLength(1, GridUnitType.Star);
        TxtVisualStyleLabel.Text = LocalizationService.Get("PromptBuilder_StyleLabel");
    }

    private void RefreshPhotoSections()
    {
        CmbPhotoSection.SelectionChanged -= CmbPhotoSection_SelectionChanged;
        CmbPhotoSection.Items.Clear();
        foreach (PhotoSectionDefinition section in PromptBuilderService.PhotoSections)
            CmbPhotoSection.Items.Add(new ComboBoxItem { Tag = section.Section, Content = LocalizationService.Get(section.LocalizationKey) });
        CmbPhotoSection.SelectedItem = CmbPhotoSection.Items.Cast<ComboBoxItem>().First(item => (PhotoSection)item.Tag == _photoSection);
        CmbPhotoSection.SelectionChanged += CmbPhotoSection_SelectionChanged;
    }

    private void CmbPhotoSection_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CmbPhotoSection.SelectedItem is not ComboBoxItem { Tag: PhotoSection section } || _photoSection == section) return;
        _photoSection = section;
        if (!PromptBuilderService.GetPhotoStyles(section).Any(style => style.Style == _photoStyle)) _photoStyle = PhotoStyle.Auto;
        RefreshVisualStyleOptions();
        SaveCurrentMode();
    }

    private void RefreshAnimationSections()
    {
        CmbAnimationSection.SelectionChanged -= CmbAnimationSection_SelectionChanged;
        CmbAnimationSection.Items.Clear();
        foreach (AnimationStyleSectionDefinition section in PromptBuilderService.AnimationStyleSections)
            CmbAnimationSection.Items.Add(new ComboBoxItem { Tag = section.Section, Content = LocalizationService.Get(section.LocalizationKey) });
        CmbAnimationSection.SelectedItem = CmbAnimationSection.Items.Cast<ComboBoxItem>().First(item => (AnimationStyleSection)item.Tag == _animationSection);
        CmbAnimationSection.SelectionChanged += CmbAnimationSection_SelectionChanged;
    }

    private void CmbAnimationSection_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CmbAnimationSection.SelectedItem is not ComboBoxItem { Tag: AnimationStyleSection section } || _animationSection == section) return;

        _animationSection = section;
        if (!PromptBuilderService.GetAnimationStyles(section).Any(style => style.Style == _animationStyle)) _animationStyle = AnimationStyle.Auto;
        RefreshVisualStyleOptions();
        SaveCurrentMode();
    }

    private void RefreshThemeSections()
    {
        CmbThemeSection.SelectionChanged -= CmbThemeSection_SelectionChanged;
        CmbThemeSection.Items.Clear();
        foreach (ThemeSectionDefinition section in PromptBuilderService.ThemeSections)
            CmbThemeSection.Items.Add(new ComboBoxItem { Tag = section.Section, Content = LocalizationService.Get(section.LocalizationKey) });
        CmbThemeSection.SelectedItem = CmbThemeSection.Items.Cast<ComboBoxItem>().First(item => (ThemeSection)item.Tag == _themeSection);
        CmbThemeSection.SelectionChanged += CmbThemeSection_SelectionChanged;
    }

    private void CmbThemeSection_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CmbThemeSection.SelectedItem is not ComboBoxItem { Tag: ThemeSection section } || _themeSection == section) return;
        _themeSection = section;
        if (!PromptBuilderService.GetThemeStyles(section).Any(style => style.Style == _themeStyle)) _themeStyle = ThemeStyle.Auto;
        RefreshVisualStyleOptions();
        SaveCurrentMode();
    }

    private void RefreshPaintingSections()
    {
        CmbPaintingSection.SelectionChanged -= CmbPaintingSection_SelectionChanged;
        CmbPaintingSection.Items.Clear();
        foreach (PaintingStyleSectionDefinition section in PromptBuilderService.PaintingStyleSections)
            CmbPaintingSection.Items.Add(new ComboBoxItem { Tag = section.Section, Content = LocalizationService.Get(section.LocalizationKey) });
        CmbPaintingSection.SelectedItem = CmbPaintingSection.Items.Cast<ComboBoxItem>().First(item => (PaintingStyleSection)item.Tag == _paintingSection);
        CmbPaintingSection.SelectionChanged += CmbPaintingSection_SelectionChanged;
    }

    private void CmbPaintingSection_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CmbPaintingSection.SelectedItem is not ComboBoxItem { Tag: PaintingStyleSection section } || _paintingSection == section) return;
        _paintingSection = section;
        if (section == PaintingStyleSection.Artists)
        {
            _paintingStyle = PaintingStyle.Auto;
        }
        else
        {
            if (!PromptBuilderService.GetPaintingStyles(section).Any(style => style.Style == _paintingStyle)) _paintingStyle = PaintingStyle.Auto;
        }
        RefreshVisualStyleOptions();
        SaveCurrentMode();
    }

    private void RefreshAnalysisDirections()
    {
        CmbAnalysisDirection.SelectionChanged -= CmbAnalysisDirection_SelectionChanged;
        CmbAnalysisDirection.Items.Clear();
        foreach (AnalysisDirectionDefinition item in OrderAutoFirst(PromptBuilderService.AnalysisDirections, item => item.Direction == AnalysisDirection.Auto, item => item.LocalizationKey))
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
        foreach (VideoDirectionDefinition item in OrderAutoFirst(PromptBuilderService.VideoDirections, item => item.Direction == VideoDirection.Auto, item => item.LocalizationKey))
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

    private void RefreshProgrammingOptions()
    {
        CmbProgrammingProjectType.SelectionChanged -= CmbProgrammingProjectType_SelectionChanged;
        CmbProgrammingStyle.SelectionChanged -= CmbProgrammingStyle_SelectionChanged;
        CmbProgrammingProjectType.Items.Clear();
        CmbProgrammingStyle.Items.Clear();
        foreach (ProgrammingProjectTypeDefinition item in OrderAutoFirst(PromptBuilderService.ProgrammingProjectTypes, item => item.Type == ProgrammingProjectType.Auto, item => item.LocalizationKey))
        {
            CmbProgrammingProjectType.Items.Add(new ComboBoxItem { Tag = item.Type, Content = LocalizationService.Get(item.LocalizationKey) });
        }
        foreach (ProgrammingPromptStyleDefinition item in OrderAutoFirst(PromptBuilderService.GetProgrammingStyles(_programmingProjectType), item => item.Style == ProgrammingPromptStyle.Auto, item => item.LocalizationKey))
        {
            CmbProgrammingStyle.Items.Add(new ComboBoxItem { Tag = item.Style, Content = LocalizationService.Get(item.LocalizationKey) });
        }
        CmbProgrammingProjectType.SelectedItem = CmbProgrammingProjectType.Items.Cast<ComboBoxItem>().First(item => (ProgrammingProjectType)item.Tag == _programmingProjectType);
        CmbProgrammingStyle.SelectedItem = CmbProgrammingStyle.Items.Cast<ComboBoxItem>().First(item => (ProgrammingPromptStyle)item.Tag == _programmingStyle);
        CmbProgrammingProjectType.SelectionChanged += CmbProgrammingProjectType_SelectionChanged;
        CmbProgrammingStyle.SelectionChanged += CmbProgrammingStyle_SelectionChanged;
    }

    private void CmbProgrammingProjectType_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CmbProgrammingProjectType.SelectedItem is ComboBoxItem { Tag: ProgrammingProjectType type } && _programmingProjectType != type)
        {
            _programmingProjectType = type;
            if (!PromptBuilderService.GetProgrammingStyles(type).Any(item => item.Style == _programmingStyle)) _programmingStyle = ProgrammingPromptStyle.Auto;
            RefreshProgrammingOptions();
            SaveCurrentMode();
        }
    }

    private void CmbProgrammingStyle_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CmbProgrammingStyle.SelectedItem is ComboBoxItem { Tag: ProgrammingPromptStyle style } && _programmingStyle != style)
        {
            _programmingStyle = style;
            SaveCurrentMode();
        }
    }

    private static string FormatOptionOutcome(string resourceKey, string option) =>
        string.Format(CultureInfo.CurrentCulture, LocalizationService.Get(resourceKey), option);

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

    private void RefreshGraphicOptions()
    {
        PopulateVisualTargets(CmbGraphicTarget);
        PopulateOptions(CmbGraphicType, PromptBuilderService.GraphicTypes, item => item.Type, item => item.LocalizationKey, _graphicType);
        if (_graphicType == GraphicType.Icon)
        {
            PopulateOptions(CmbGraphicStyle, PromptBuilderService.IconStyles, item => item.Style, item => item.LocalizationKey, _iconStyle);
        }
        else
        {
            if (!PromptBuilderService.GetGraphicStyles(_graphicType).Any(item => item.Style == _graphicStyle)) _graphicStyle = GraphicStyle.Auto;
            PopulateOptions(CmbGraphicStyle, PromptBuilderService.GetGraphicStyles(_graphicType), item => item.Style, item => item.LocalizationKey, _graphicStyle);
        }
    }

    private void PopulateOptions<TDefinition, TValue>(ComboBox comboBox, IEnumerable<TDefinition> definitions, Func<TDefinition, TValue> value, Func<TDefinition, string> localizationKey, TValue selected)
        where TValue : struct
    {
        comboBox.SelectionChanged -= CmbIconOrGraphicOption_SelectionChanged;
        comboBox.Items.Clear();
        foreach (TDefinition item in OrderAutoFirst(definitions, item => string.Equals(value(item).ToString(), "Auto", StringComparison.Ordinal), localizationKey))
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

    private static IOrderedEnumerable<TDefinition> OrderAutoFirst<TDefinition>(
        IEnumerable<TDefinition> definitions,
        Func<TDefinition, bool> isAuto,
        Func<TDefinition, string> localizationKey) =>
        definitions
            .OrderBy(item => isAuto(item) ? 0 : 1)
            .ThenBy(item => LocalizationService.Get(localizationKey(item)), StringComparer.CurrentCultureIgnoreCase);

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
            IconStyle style when _iconStyle != style => SetIconStyle(style),
            GraphicType type when _graphicType != type => SetGraphicType(type),
            GraphicStyle style when _graphicStyle != style => SetGraphicStyle(style),
            _ => false
        };
        if (changed) SaveCurrentMode();
    }

    private bool SetIconStyle(IconStyle value) { _iconStyle = value; return true; }
    private bool SetGraphicType(GraphicType value)
    {
        _graphicType = value;
        RefreshGraphicOptions();
        return true;
    }
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

    private void AddAutomaticModelOption()
    {
        string automaticLabel = LocalizationService.Get("TextProcessing_ModelAuto");
        _models.Add(new ModelItem(null, null, automaticLabel, null) { FullDisplay = automaticLabel });
        CmbModels.SelectedIndex = 0;
    }

    private async Task LoadModelsAsync(CancellationToken cancellationToken)
    {
        _isLoadingModels = true;
        _hasEligibleModel = false;
        _hasAutomaticModel = false;
        _hasSelectableModel = false;
        _models.Clear();
        AddAutomaticModelOption();
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
