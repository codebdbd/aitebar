using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace AiteBar;

public sealed class TextProcessingViewModel : INotifyPropertyChanged
{
    private readonly TextProcessingService _service;
    private readonly AiGateway _gateway;
    private readonly AppSettingsService _settingsService;
    private CancellationTokenSource? _cts;
    private bool _isProcessing;
    private string _inputText = string.Empty;
    private string _originalText = string.Empty;
    private string _processedText = string.Empty;
    private bool _isShowingOriginal;
    private bool _hasSuccessfulResult;
    private bool _isModifiedManually;
    private TextProcessingMode _currentMode = TextProcessingMode.Proofread;
    private bool _isAutoModel = true;
    private string? _selectedModelId;
    private string? _selectedProviderId;
    private string? _selectedModelDisplay;
    private int _characterCount;
    private int _wordCount;
    private bool _isModelAvailable;
    private string _statusMessage = string.Empty;
    private bool _isEditorEnabled = true;
    private bool _isModeSwitcherEnabled = true;
    private bool _isModelSelectorEnabled = true;

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler<string>? ShowNotification;
    public Func<string, Task<bool>>? ConfirmAction { get; set; }

    public TextProcessingViewModel(
        TextProcessingService service,
        AiGateway gateway,
        AppSettingsService settingsService)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
    }

    public TextProcessingMode CurrentMode
    {
        get => _currentMode;
        set { _currentMode = value; OnPropertyChanged(); OnPropertyChanged(nameof(ModeButtonText)); OnPropertyChanged(nameof(ModeDescription)); }
    }

    public string InputText
    {
        get => _inputText;
        set
        {
            if (_inputText == value) return;
            _inputText = value;
            OnPropertyChanged();
            UpdateCounts();
            if (!string.IsNullOrEmpty(_originalText) && _hasSuccessfulResult && value != _processedText)
            {
                _isModifiedManually = true;
            }
        }
    }

    public string OriginalText
    {
        get => _originalText;
        set { _originalText = value; OnPropertyChanged(); }
    }

    public string ProcessedText
    {
        get => _processedText;
        set { _processedText = value; OnPropertyChanged(); }
    }

    public bool IsShowingOriginal
    {
        get => _isShowingOriginal;
        set { _isShowingOriginal = value; OnPropertyChanged(); OnPropertyChanged(nameof(ToggleButtonText)); OnPropertyChanged(nameof(IsToggleVersionVisible)); }
    }

    public bool HasSuccessfulResult
    {
        get => _hasSuccessfulResult;
        set { _hasSuccessfulResult = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsToggleVersionVisible)); }
    }

    public bool IsModifiedManually
    {
        get => _isModifiedManually;
        set { _isModifiedManually = value; OnPropertyChanged(); }
    }

    public bool IsProcessing
    {
        get => _isProcessing;
        set
        {
            _isProcessing = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsEditorEnabled));
            OnPropertyChanged(nameof(IsModeSwitcherEnabled));
            OnPropertyChanged(nameof(IsModelSelectorEnabled));
            OnPropertyChanged(nameof(IsPasteEnabled));
            OnPropertyChanged(nameof(IsClearEnabled));
            OnPropertyChanged(nameof(IsToggleVersionVisible));
            OnPropertyChanged(nameof(IsMainButtonEnabled));
            OnPropertyChanged(nameof(MainButtonText));
        }
    }

    public bool IsAutoModel
    {
        get => _isAutoModel;
        set { _isAutoModel = value; OnPropertyChanged(); OnPropertyChanged(nameof(SelectedModelDisplay)); }
    }

    public string? SelectedModelId
    {
        get => _selectedModelId;
        set { _selectedModelId = value; OnPropertyChanged(); }
    }

    public string? SelectedProviderId
    {
        get => _selectedProviderId;
        set { _selectedProviderId = value; OnPropertyChanged(); }
    }

    public string? SelectedModelDisplay
    {
        get => _isAutoModel ? LocalizationService.Get("TextProcessing_ModelAuto") : _selectedModelDisplay;
        set { _selectedModelDisplay = value; OnPropertyChanged(); }
    }

    public int CharacterCount
    {
        get => _characterCount;
        set { _characterCount = value; OnPropertyChanged(); OnPropertyChanged(nameof(CharacterCountText)); OnPropertyChanged(nameof(IsOverLimit)); }
    }

    public int WordCount
    {
        get => _wordCount;
        set { _wordCount = value; OnPropertyChanged(); OnPropertyChanged(nameof(WordCountText)); }
    }

    public bool IsModelAvailable
    {
        get => _isModelAvailable;
        set { _isModelAvailable = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsMainButtonEnabled)); OnPropertyChanged(nameof(EmptyModelsMessage)); }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set { _statusMessage = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasStatusMessage)); }
    }

    public bool HasStatusMessage => !string.IsNullOrEmpty(_statusMessage);

    public bool IsEditorEnabled
    {
        get => !_isProcessing && _isEditorEnabled;
        set
        {
            if (_isEditorEnabled == value) return;
            _isEditorEnabled = value;
            OnPropertyChanged();
        }
    }

    public bool IsModeSwitcherEnabled
    {
        get => !_isProcessing && _isModeSwitcherEnabled;
        set
        {
            if (_isModeSwitcherEnabled == value) return;
            _isModeSwitcherEnabled = value;
            OnPropertyChanged();
        }
    }

    public bool IsModelSelectorEnabled
    {
        get => !_isProcessing && _isModelSelectorEnabled;
        set
        {
            if (_isModelSelectorEnabled == value) return;
            _isModelSelectorEnabled = value;
            OnPropertyChanged();
        }
    }
    public bool IsPasteEnabled => !_isProcessing;
    public bool IsClearEnabled => !_isProcessing && CharacterCount > 0;
    public bool IsToggleVersionVisible => _hasSuccessfulResult && !_isProcessing;
    public bool IsMainButtonEnabled => !_isProcessing && CharacterCount > 0 && (IsAutoModel || IsModelAvailable) && !IsOverLimit;
    public bool IsOverLimit => CharacterCount > TextProcessingService.MaxInputLength;
    public string ModeButtonText => CurrentMode switch
    {
        TextProcessingMode.Proofread => LocalizationService.Get("TextProcessing_ButtonProofread"),
        TextProcessingMode.Typography => LocalizationService.Get("TextProcessing_ButtonFormat"),
        TextProcessingMode.Cleanup => LocalizationService.Get("TextProcessing_ButtonCleanup"),
        _ => string.Empty
    };

    public string ModeDescription => CurrentMode switch
    {
        TextProcessingMode.Proofread => LocalizationService.Get("TextProcessing_ModeProofreadDesc"),
        TextProcessingMode.Typography => LocalizationService.Get("TextProcessing_ModeTypographyDesc"),
        TextProcessingMode.Cleanup => LocalizationService.Get("TextProcessing_ModeCleanupDesc"),
        _ => string.Empty
    };

    public string MainButtonText => _isProcessing
        ? LocalizationService.Get("TextProcessing_ButtonCancel")
        : ModeButtonText;

    public string ToggleButtonText => _isShowingOriginal
        ? LocalizationService.Get("TextProcessing_ButtonShowResult")
        : LocalizationService.Get("TextProcessing_ButtonShowOriginal");

    public string CharacterCountText => LocalizationService.Format("TextProcessing_Characters", CharacterCount);
    public string WordCountText => LocalizationService.Format("TextProcessing_Words", WordCount);
    public string EmptyModelsMessage => LocalizationService.Get("TextProcessing_ErrorNoModels");

    public ObservableCollection<ModelItem> Models { get; } = [];

    public async Task LoadModelsAsync()
    {
        var allModels = new List<ModelItem>
        {
            new(null, null, LocalizationService.Get("TextProcessing_ModelAuto"), true)
        };

        AppSettings settings = _settingsService.Settings;
        AiSettings? aiSettings = settings.Ai;
        if (aiSettings?.Connections != null)
        {
            foreach (AiConnectionSettings connection in aiSettings.Connections.Where(c => c.IsEnabled))
            {
                try
                {
                    IReadOnlyList<AiModelDescriptor> models = await _gateway.GetModelsAsync(connection, CancellationToken.None).ConfigureAwait(false);
                    var eligibleModels = models.Where(m =>
                        !m.IsDeprecated &&
                        (m.Capabilities & AiCapabilities.Text) == AiCapabilities.Text &&
                        m.CostStatus is AiCostStatus.VerifiedFree or AiCostStatus.FreeTierAvailable).ToList();

                    // Add preferred model first if present and eligible
                    if (!string.IsNullOrWhiteSpace(connection.PreferredModelId))
                    {
                        var preferredModel = eligibleModels.FirstOrDefault(m =>
                            string.Equals(m.ModelId, connection.PreferredModelId, StringComparison.OrdinalIgnoreCase));
                        if (preferredModel != null)
                        {
                            string display = $"{connection.DisplayName} — {preferredModel.DisplayName}";
                            allModels.Add(new ModelItem(connection.ProviderId, preferredModel.ModelId, display, true));
                            eligibleModels.Remove(preferredModel);
                        }
                    }

                    // Add remaining eligible models
                    foreach (AiModelDescriptor model in eligibleModels)
                    {
                        string display = $"{connection.DisplayName} — {model.DisplayName}";
                        allModels.Add(new ModelItem(connection.ProviderId, model.ModelId, display, true));
                    }
                }
                catch
                {
                    // Connection unavailable, skip
                }
            }
        }

        Models.Clear();
        // Keep auto model first, then sort the rest but keep preferred models in their order relative to connections
        var autoModel = allModels[0];
        var otherModels = allModels.Skip(1).ToList();
        otherModels.Sort((a, b) => string.Compare(a.Display, b.Display, StringComparison.CurrentCultureIgnoreCase));
        Models.Add(autoModel);
        foreach (var model in otherModels)
        {
            Models.Add(model);
        }

        IsModelAvailable = Models.Count > 1;
        RestoreModelSelection();
    }

    public async Task ProcessAsync()
    {
        if (IsProcessing) return;
        if (string.IsNullOrWhiteSpace(InputText)) return;
        if (CharacterCount > TextProcessingService.MaxInputLength) return;
        if (!IsAutoModel && !IsModelAvailable) return;

        StatusMessage = string.Empty;

        string textToShow = _isShowingOriginal ? _originalText : InputText;
        if (_hasSuccessfulResult && !_isShowingOriginal && _isModifiedManually)
        {
            if (ConfirmAction != null)
            {
                bool confirmed = await ConfirmAction(LocalizationService.Get("TextProcessing_ConfirmRepeat")).ConfigureAwait(false);
                if (!confirmed) return;
            }
        }

        OriginalText = textToShow;
        _isModifiedManually = false;

        IsProcessing = true;
        _cts = new CancellationTokenSource();

        try
        {
            AiGatewayResponse response = await _service.ProcessAsync(
                _gateway,
                CurrentMode,
                OriginalText,
                IsAutoModel ? null : SelectedProviderId,
                IsAutoModel ? null : SelectedModelId,
                _cts.Token).ConfigureAwait(false);

            string cleaned = _service.CleanResponse(response.Content);

            if (string.IsNullOrWhiteSpace(cleaned))
            {
                StatusMessage = LocalizationService.Get("TextProcessing_ErrorEmptyResponse");
                return;
            }

            ProcessedText = cleaned;
            InputText = ProcessedText;
            HasSuccessfulResult = true;
            IsShowingOriginal = false;
            UpdateCounts();
            SaveModelSelection(response.ProviderId, response.ModelId);
        }
        catch (OperationCanceledException)
        {
            StatusMessage = LocalizationService.Get("TextProcessing_ErrorCancellation");
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("No enabled AI connections"))
        {
            StatusMessage = LocalizationService.Get("TextProcessing_ErrorNoModels");
        }
        catch (AiProviderHttpException ex)
        {
            StatusMessage = ex.StatusCode switch
            {
                System.Net.HttpStatusCode.Unauthorized => LocalizationService.Get("TextProcessing_ErrorUnauthorized"),
                System.Net.HttpStatusCode.Forbidden => LocalizationService.Get("TextProcessing_ErrorForbidden"),
                System.Net.HttpStatusCode.PaymentRequired => LocalizationService.Get("TextProcessing_ErrorQuota"),
                System.Net.HttpStatusCode.TooManyRequests => LocalizationService.Get("TextProcessing_ErrorRateLimit"),
                _ when (int)ex.StatusCode >= 500 => LocalizationService.Get("TextProcessing_ErrorUnavailable"),
                _ => LocalizationService.Get("TextProcessing_ErrorGeneric")
            };
        }
        catch (Exception)
        {
            StatusMessage = LocalizationService.Get("TextProcessing_ErrorGeneric");
        }
        finally
        {
            IsProcessing = false;
            _cts?.Dispose();
            _cts = null;
        }
    }

    public void CancelProcessing()
    {
        _cts?.Cancel();
    }

    public void SwitchMode(TextProcessingMode mode)
    {
        if (IsProcessing) return;
        CurrentMode = mode;
    }

    public void ToggleVersion()
    {
        if (!_hasSuccessfulResult) return;

        if (_isShowingOriginal)
        {
            InputText = _processedText;
            IsShowingOriginal = false;
        }
        else
        {
            InputText = _originalText;
            IsShowingOriginal = true;
        }

        UpdateCounts();
    }

    public void Clear()
    {
        InputText = string.Empty;
        OriginalText = string.Empty;
        ProcessedText = string.Empty;
        HasSuccessfulResult = false;
        IsShowingOriginal = false;
        _isModifiedManually = false;
        StatusMessage = string.Empty;
        UpdateCounts();
    }

    public bool HasUnsavedContent()
    {
        return !string.IsNullOrWhiteSpace(InputText);
    }

    public void SaveWindowState(double left, double top, double width, double height, string windowState)
    {
        _settingsService.UpdateSettings(s =>
        {
            s.TextProcessingLeft = left;
            s.TextProcessingTop = top;
            s.TextProcessingWidth = width;
            s.TextProcessingHeight = height;
            s.TextProcessingWindowState = windowState;
        });
    }

    public void SaveMode()
    {
        _settingsService.UpdateSettings(s =>
        {
            s.TextProcessingLastMode = (int)CurrentMode;
        });
    }

    public void RestoreMode()
    {
        AppSettings settings = _settingsService.Settings;
        int mode = settings.TextProcessingLastMode;
        if (Enum.IsDefined(typeof(TextProcessingMode), mode))
        {
            CurrentMode = (TextProcessingMode)mode;
        }
    }

    private void UpdateCounts()
    {
        string text = _isShowingOriginal ? _originalText : _inputText;
        CharacterCount = text?.Length ?? 0;
        WordCount = string.IsNullOrWhiteSpace(text) ? 0 : text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
    }

    private void RestoreModelSelection()
    {
        AppSettings settings = _settingsService.Settings;
        IsAutoModel = settings.TextProcessingIsAutoModel;

        if (!IsAutoModel && settings.TextProcessingSelectedModelId != null)
        {
            ModelItem? match = Models.FirstOrDefault(m =>
                m.ProviderId == settings.TextProcessingSelectedProviderId &&
                m.ModelId == settings.TextProcessingSelectedModelId);

            if (match != null)
            {
                SelectedModelId = match.ModelId;
                SelectedProviderId = match.ProviderId;
                SelectedModelDisplay = match.Display;
            }
            else
            {
                IsAutoModel = true;
                ShowNotification?.Invoke(this, LocalizationService.Get("TextProcessing_ModelUnavailable"));
            }
        }
    }

    private void SaveModelSelection(string providerId, string modelId)
    {
        _settingsService.UpdateSettings(s =>
        {
            s.TextProcessingSelectedProviderId = providerId;
            s.TextProcessingSelectedModelId = modelId;
            s.TextProcessingIsAutoModel = IsAutoModel;
        });
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public sealed record ModelItem(string? ProviderId, string? ModelId, string Display, bool IsEnabled);
