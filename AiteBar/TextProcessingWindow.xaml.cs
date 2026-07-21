using System;
using System.ComponentModel;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace AiteBar;

[SupportedOSPlatform("windows6.1")]
public partial class TextProcessingWindow : DarkWindow
{
    private readonly TextProcessingViewModel _viewModel;
    private readonly AppSettingsService _settingsService;
    private bool _isLoadingState;
    private bool _isDirty;

    public TextProcessingWindow(TextProcessingViewModel viewModel, AppSettingsService settingsService)
    {
        _isLoadingState = true;
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));

        InitializeComponent();
        DataContext = _viewModel;

        _viewModel.ShowNotification += OnShowNotification;
        _viewModel.ConfirmAction = OnConfirmAction;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;

        BtnProcess.Content = _viewModel.MainButtonText;
        TxtModeDescription.Text = _viewModel.ModeDescription;
    }

    public void ShowNearPanel(AppSettingsService settingsService)
    {
        AppSettings settings = settingsService.Settings;
        var screens = System.Windows.Forms.Screen.AllScreens;
        var screen = settings.MonitorIndex >= 0 && settings.MonitorIndex < screens.Length
            ? screens[settings.MonitorIndex]
            : System.Windows.Forms.Screen.PrimaryScreen;
        var work = screen?.WorkingArea ?? System.Windows.Forms.Screen.PrimaryScreen?.WorkingArea ?? new System.Drawing.Rectangle(0, 0, 1280, 720);

        Width = Math.Min(1280, work.Width * 0.9);
        Height = Math.Min(840, work.Height * 0.9);
        Left = work.Left + (work.Width - Width) / 2;
        Top = work.Top + (work.Height - Height) / 2;

        RestoreWindowState(settings);
        Show();
        Activate();
        TxtEditor.Focus();
    }

    internal void RestoreFromAiteBar()
    {
        WindowState = WindowState.Normal;
        if (!IsVisible)
        {
            Show();
        }

        Activate();
        TxtEditor.Focus();
    }

    private void RestoreWindowState(AppSettings settings)
    {
        if (settings.TextProcessingWidth.HasValue && settings.TextProcessingHeight.HasValue)
        {
            double w = settings.TextProcessingWidth.Value;
            double h = settings.TextProcessingHeight.Value;
            var work = System.Windows.Forms.Screen.PrimaryScreen?.WorkingArea
                ?? new System.Drawing.Rectangle(0, 0, 1280, 720);

            if (w >= MinWidth && h >= MinHeight && w <= work.Width * 0.95 && h <= work.Height * 0.95)
            {
                Width = w;
                Height = h;
            }
        }

        if (settings.TextProcessingLeft.HasValue && settings.TextProcessingTop.HasValue)
        {
            double l = settings.TextProcessingLeft.Value;
            double t = settings.TextProcessingTop.Value;
            var work = System.Windows.Forms.Screen.PrimaryScreen?.WorkingArea
                ?? new System.Drawing.Rectangle(0, 0, 1280, 720);

            if (l >= work.Left - 100 && l <= work.Right - 100 &&
                t >= work.Top - 100 && t <= work.Bottom - 100)
            {
                Left = l;
                Top = t;
                WindowStartupLocation = WindowStartupLocation.Manual;
            }
        }

        if (string.Equals(settings.TextProcessingWindowState, "Maximized", StringComparison.Ordinal))
        {
            WindowState = WindowState.Maximized;
        }
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        _isLoadingState = true;
        try
        {
            _viewModel.RestoreMode();
            ApplyModeToUI();
            await _viewModel.LoadModelsAsync();
        }
        catch
        {
            // Model loading failed — window remains functional
        }
        finally
        {
            _isLoadingState = false;
        }
        TxtEditor.Focus();
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (_isDirty && _viewModel.HasUnsavedContent())
        {
            var result = new DarkDialog(
                LocalizationService.Get("TextProcessing_ConfirmClose"),
                isConfirm: true) { Owner = this }.ShowDialog();

            if (result != true)
            {
                e.Cancel = true;
                return;
            }
        }

        SaveWindowState();
        _viewModel.SaveMode();
    }

    private void Window_StateChanged(object? sender, EventArgs e)
    {
        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
            Hide();
        }
    }

    private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_isLoadingState) return;
        SaveWindowState();
    }

    private void Window_LocationChanged(object? sender, EventArgs e)
    {
        if (_isLoadingState) return;
        if (Left > -10000 && Top > -10000)
        {
            SaveWindowState();
        }
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Control)
        {
            e.Handled = true;
            _ = ProcessAsync();
        }
        else if (e.Key == Key.Escape && _viewModel.IsProcessing)
        {
            e.Handled = true;
            _viewModel.CancelProcessing();
        }
    }

    private void ModeSegment_Click(object sender, RoutedEventArgs e)
    {
        if (_isLoadingState || _viewModel == null || TxtModeDescription == null || BtnProcess == null) return;
        if (sender is not System.Windows.Controls.RadioButton rb || rb.Tag is not string tag) return;

        switch (tag)
        {
            case "Proofread": _viewModel.SwitchMode(TextProcessingMode.Proofread); break;
            case "Typography": _viewModel.SwitchMode(TextProcessingMode.Typography); break;
            case "Cleanup": _viewModel.SwitchMode(TextProcessingMode.Cleanup); break;
        }
        TxtModeDescription.Text = _viewModel.ModeDescription;
        BtnProcess.Content = _viewModel.MainButtonText;
    }

    private void TxtEditor_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isLoadingState || _viewModel == null || TxtPlaceholder == null || BtnCopy == null || BtnClear == null) return;
        _viewModel.InputText = TxtEditor.Text;
        TxtPlaceholder.Visibility = string.IsNullOrEmpty(TxtEditor.Text)
            ? Visibility.Visible
            : Visibility.Collapsed;
        BtnCopy.IsEnabled = !string.IsNullOrEmpty(TxtEditor.Text);
        BtnClear.IsEnabled = !string.IsNullOrEmpty(TxtEditor.Text);
        _isDirty = true;
    }

    private void CmbModels_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoadingState || _viewModel == null || CmbModels == null) return;
        if (CmbModels.SelectedItem is ModelItem item)
        {
            _viewModel.IsAutoModel = item.ModelId == null;
            _viewModel.SelectedModelId = item.ModelId;
            _viewModel.SelectedProviderId = item.ProviderId;
            _viewModel.SelectedModelDisplay = item.Display;
        }
    }

    private async void BtnProcess_Click(object sender, RoutedEventArgs e)
    {
        await ProcessAsync();
    }

    private void BtnClear_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.Clear();
        TxtEditor.Text = string.Empty;
        TxtPlaceholder.Visibility = Visibility.Visible;
        BtnCopy.IsEnabled = false;
        BtnToggleVersion.Visibility = Visibility.Collapsed;
        _isDirty = false;
    }

    private void BtnPaste_Click(object sender, RoutedEventArgs e)
    {
        if (Clipboard.ContainsText())
        {
            string text = Clipboard.GetText();
            TxtEditor.Text = text;
            TxtPlaceholder.Visibility = Visibility.Collapsed;
            BtnCopy.IsEnabled = true;
            _isDirty = true;

            _viewModel.OriginalText = string.Empty;
            _viewModel.ProcessedText = string.Empty;
            _viewModel.HasSuccessfulResult = false;
            _viewModel.IsShowingOriginal = false;
            BtnToggleVersion.Visibility = Visibility.Collapsed;
        }
    }

    private void BtnCopy_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(TxtEditor.Text))
        {
            Clipboard.SetText(TxtEditor.Text);
        }
    }

    private void BtnToggleVersion_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.ToggleVersion();
        TxtEditor.Text = _viewModel.InputText;
        TxtPlaceholder.Visibility = string.IsNullOrEmpty(TxtEditor.Text)
            ? Visibility.Visible
            : Visibility.Collapsed;
        TxtCounters.Text = $"{_viewModel.CharacterCountText} · {_viewModel.WordCountText}";
        UpdateToggleVersionLabel();
    }

    private async System.Threading.Tasks.Task ProcessAsync()
    {
        await _viewModel.ProcessAsync();

        if (!string.IsNullOrEmpty(_viewModel.ProcessedText) && !_viewModel.IsProcessing)
        {
            TxtEditor.Text = _viewModel.InputText;
            TxtPlaceholder.Visibility = Visibility.Collapsed;
            BtnToggleVersion.Visibility = _viewModel.HasSuccessfulResult
                ? Visibility.Visible
                : Visibility.Collapsed;
            UpdateToggleVersionLabel();
            TxtCounters.Text = $"{_viewModel.CharacterCountText} · {_viewModel.WordCountText}";
            _isDirty = false;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TextProcessingViewModel.IsProcessing))
        {
            BtnProcess.Content = _viewModel.MainButtonText;
            TxtEditor.IsEnabled = _viewModel.IsEditorEnabled;
            BtnModeProofread.IsEnabled = _viewModel.IsModeSwitcherEnabled;
            BtnModeTypography.IsEnabled = _viewModel.IsModeSwitcherEnabled;
            BtnModeCleanup.IsEnabled = _viewModel.IsModeSwitcherEnabled;
            CmbModels.IsEnabled = _viewModel.IsModelSelectorEnabled;
            BtnPaste.IsEnabled = _viewModel.IsPasteEnabled;
            BtnClear.IsEnabled = _viewModel.IsClearEnabled;
            BtnToggleVersion.Visibility = _viewModel.IsToggleVersionVisible
                ? Visibility.Visible
                : Visibility.Collapsed;
        }
        else if (e.PropertyName == nameof(TextProcessingViewModel.ToggleButtonText))
        {
            UpdateToggleVersionLabel();
        }
        else if (e.PropertyName == nameof(TextProcessingViewModel.CharacterCountText) ||
                 e.PropertyName == nameof(TextProcessingViewModel.WordCountText))
        {
            TxtCounters.Text = $"{_viewModel.CharacterCountText} · {_viewModel.WordCountText}";
        }
        else if (e.PropertyName == nameof(TextProcessingViewModel.IsOverLimit))
        {
            TxtCounters.Foreground = _viewModel.IsOverLimit
                ? System.Windows.Media.Brushes.OrangeRed
                : (System.Windows.Media.Brush)FindResource("MutedText");
        }
        else if (e.PropertyName == nameof(TextProcessingViewModel.InputText))
        {
            if (TxtEditor.Text != _viewModel.InputText)
            {
                TxtEditor.Text = _viewModel.InputText;
            }
        }
    }

    private void OnShowNotification(object? sender, string message)
    {
        new DarkDialog(message) { Owner = this }.ShowDialog();
    }

    private Task<bool> OnConfirmAction(string message)
    {
        var result = new DarkDialog(message, isConfirm: true) { Owner = this }.ShowDialog();
        return Task.FromResult(result == true);
    }

    private void UpdateToggleVersionLabel()
    {
        if (_viewModel.IsShowingOriginal)
        {
            ToggleVersionIcon.Text = "\uF56A";
            ToggleVersionLabel.Text = LocalizationService.Get("TextProcessing_ButtonAfterProcessing");
        }
        else
        {
            ToggleVersionIcon.Text = "\uF629";
            ToggleVersionLabel.Text = LocalizationService.Get("TextProcessing_ButtonBeforeProcessing");
        }
    }

    private void SaveWindowState()
    {
        if (WindowState == WindowState.Minimized) return;

        string state = WindowState == WindowState.Maximized ? "Maximized" : "Normal";
        _viewModel.SaveWindowState(Left, Top, ActualWidth, ActualHeight, state);
    }

    private void ApplyModeToUI()
    {
        switch (_viewModel.CurrentMode)
        {
            case TextProcessingMode.Proofread:
                BtnModeProofread.IsChecked = true;
                break;
            case TextProcessingMode.Typography:
                BtnModeTypography.IsChecked = true;
                break;
            case TextProcessingMode.Cleanup:
                BtnModeCleanup.IsChecked = true;
                break;
        }
        TxtModeDescription.Text = _viewModel.ModeDescription;
        BtnProcess.Content = _viewModel.MainButtonText;
    }
}
