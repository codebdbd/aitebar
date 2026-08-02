using System;
using System.Diagnostics;
using System.Media;
using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace AiteBar;

[SupportedOSPlatform("windows6.1")]
public partial class TimerStopwatchWindow : DarkWindow
{
    private const int TimerIntervalMs = 40;

    private readonly DispatcherTimer _tickTimer = new() { Interval = TimeSpan.FromMilliseconds(TimerIntervalMs) };
    private TimeSpan _timerDuration = TimeSpan.FromMinutes(5);
    private TimeSpan _timerRemaining = TimeSpan.FromMinutes(5);
    private long _lastTickTimestamp;
    private TimeSpan _stopwatchElapsed;
    private bool _isRunning;
    private bool _isStopwatchMode;
    private bool _isCompactMode;
    private bool _isUpdatingInput;
    private bool _isUpdatingMode;
    private bool _soundEnabled = true;
    private bool _completionFeedbackActive;
    private bool _topmostBeforeCompact;
    private int _progressSegmentCount;
    private double _fullLeft;
    private double _fullTop;
    private bool _hasFullPosition;
    private AppSettingsService? _settingsService;

    public TimerStopwatchWindow()
    {
        InitializeComponent();
        DataContext = this;
        _tickTimer.Tick += TickTimer_Tick;
        Closed += TimerStopwatchWindow_Closed;
    }

    private async void TimerStopwatchWindow_Closed(object? sender, EventArgs e)
    {
        StopRunning();
        _tickTimer.Tick -= TickTimer_Tick;
        Closed -= TimerStopwatchWindow_Closed;

        if (_settingsService != null)
        {
            try
            {
                SaveSettings(_settingsService);
                await _settingsService.SaveAsync();
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
                TelemetryService.CaptureException(ex, "timer_settings_save_failed");
            }
        }
    }

    public void ShowNearPanel(AppSettingsService settingsService)
    {
        _settingsService = settingsService;
        var settings = settingsService.Settings;

        _soundEnabled = settings.TimerSoundEnabled;
        _isStopwatchMode = settings.TimerIsStopwatchMode;
        _timerDuration = settings.TimerDuration;
        _timerRemaining = settings.TimerDuration;

        UpdateMode();
        ApplyTimerDuration(_timerDuration);
        UpdateDisplay();

        var screens = Forms.Screen.AllScreens;
        var screen = settings.MonitorIndex >= 0 && settings.MonitorIndex < screens.Length
            ? screens[settings.MonitorIndex]
            : Forms.Screen.PrimaryScreen;
        var work = screen?.WorkingArea ?? Forms.Screen.PrimaryScreen?.WorkingArea ?? new System.Drawing.Rectangle(0, 0, 1280, 720);

        double width = Width;
        double height = Height;
        var (shownX, shownY) = UtilityWindowLayoutHelper.GetCenteredCoordinates(settings.Edge, work, width, height);
        Left = shownX;
        Top = shownY;

        Show();
        Activate();
    }

    private void SaveSettings(AppSettingsService settingsService)
    {
        settingsService.Settings.TimerSoundEnabled = _soundEnabled;
        settingsService.Settings.TimerIsStopwatchMode = _isStopwatchMode;
        settingsService.Settings.TimerDuration = _timerDuration;
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        UpdateDisplay();
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_isCompactMode && e.ClickCount == 1 && e.ButtonState == MouseButtonState.Pressed)
        {
            e.Handled = true;
            DragMove();
        }
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!_isCompactMode ||
            e.ClickCount != 1 ||
            e.ButtonState != MouseButtonState.Pressed ||
            IsInteractiveDragSource(e.OriginalSource))
        {
            return;
        }

        e.Handled = true;
        DragMove();
    }

    private static bool IsInteractiveDragSource(object source)
    {
        if (source is not DependencyObject current)
        {
            return false;
        }

        while (current != null)
        {
            if (current is System.Windows.Controls.Primitives.ButtonBase || current is System.Windows.Controls.TextBox)
            {
                return true;
            }

            current = System.Windows.Media.VisualTreeHelper.GetParent(current);
        }

        return false;
    }

    private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
            return;
        }

        if ((e.Key == Key.Enter || e.Key == Key.Space) && !IsInteractiveKeyboardSource(e.OriginalSource))
        {
            ToggleRunning();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.R)
        {
            ResetCurrentMode();
            e.Handled = true;
        }
    }

    private static bool IsInteractiveKeyboardSource(object source)
    {
        if (source is not DependencyObject current)
        {
            return false;
        }

        while (current != null)
        {
            if (current is System.Windows.Controls.Primitives.ButtonBase || current is System.Windows.Controls.TextBox)
            {
                return true;
            }

            current = System.Windows.Media.VisualTreeHelper.GetParent(current);
        }

        return false;
    }

    private void Mode_Checked(object sender, RoutedEventArgs e)
    {
        if (!IsInitialized || _isUpdatingMode)
        {
            return;
        }

        _isStopwatchMode = RbStopwatch.IsChecked == true;
        StopRunning();
        UpdateMode();
        UpdateDisplay();
    }

    private void BtnStartPause_Click(object sender, RoutedEventArgs e)
    {
        ToggleRunning();
    }

    private void BtnCompactStartPause_Click(object sender, RoutedEventArgs e)
    {
        ToggleRunning();
        e.Handled = true;
    }

    private void BtnReset_Click(object sender, RoutedEventArgs e)
    {
        ResetCurrentMode();
    }

    private void ToggleRunning()
    {
        if (_isRunning)
        {
            StopRunning();
            UpdateDisplay();
            return;
        }

        if (_isStopwatchMode)
        {
            StartRunning(stopwatchMode: true);
        }
        else
        {
            StartTimer();
        }
    }

    private void StartTimer()
    {
        ReadTimerDuration();
        if (_timerRemaining <= TimeSpan.Zero)
        {
            _timerRemaining = _timerDuration;
        }

        if (_timerRemaining <= TimeSpan.Zero)
        {
            return;
        }

        StartRunning(stopwatchMode: false);
    }

    private void ResetCurrentMode()
    {
        StopCompletionFeedback();
        if (_isStopwatchMode)
        {
            StopRunning();
            _stopwatchElapsed = TimeSpan.Zero;
        }
        else
        {
            StopRunning();
            ReadTimerDuration();
            _timerRemaining = _timerDuration;
        }

        UpdateDisplay();
    }

    private void BtnTimerStart_Click(object sender, RoutedEventArgs e)
    {
        StartTimer();
    }

    private void BtnTimerStop_Click(object sender, RoutedEventArgs e)
    {
        StopRunning();
        UpdateDisplay();
    }

    private void BtnStopwatchStart_Click(object sender, RoutedEventArgs e)
    {
        StartRunning(stopwatchMode: true);
    }

    private void BtnStopwatchStop_Click(object sender, RoutedEventArgs e)
    {
        StopRunning();
        UpdateDisplay();
    }

    private void BtnTimerReset_Click(object sender, RoutedEventArgs e)
    {
        StopRunning();
        ReadTimerDuration();
        _timerRemaining = _timerDuration;
        UpdateDisplay();
    }

    private void BtnStopwatchReset_Click(object sender, RoutedEventArgs e)
    {
        StopRunning();
        _stopwatchElapsed = TimeSpan.Zero;
        UpdateDisplay();
    }

    private void BtnSound_Click(object sender, RoutedEventArgs e)
    {
        _soundEnabled = !_soundEnabled;
        if (_soundEnabled)
        {
            SystemSounds.Asterisk.Play();
        }

        UpdateDisplay();
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void BtnCompact_Click(object sender, RoutedEventArgs e)
    {
        SetCompactMode(!_isCompactMode);
        e.Handled = true;
    }

    private void BtnCompactExpand_Click(object sender, RoutedEventArgs e)
    {
        SetCompactMode(false);
        Activate();
        e.Handled = true;
    }

    private void SetCompactMode(bool isCompactMode)
    {
        if (_isCompactMode == isCompactMode)
        {
            return;
        }

        if (!isCompactMode)
        {
            _isCompactMode = false;
            Topmost = _topmostBeforeCompact;
            if (_hasFullPosition)
            {
                Left = _fullLeft;
                Top = _fullTop;
            }
        }
        else
        {
            _fullLeft = Left;
            _fullTop = Top;
            _topmostBeforeCompact = Topmost;
            _hasFullPosition = true;
            _isCompactMode = true;
        }

        UpdateMode();
        UpdateDisplay();
    }

    private void Preset1_Click(object sender, RoutedEventArgs e) => ApplyTimerDuration(TimeSpan.FromMinutes(1));
    private void Preset3_Click(object sender, RoutedEventArgs e) => ApplyTimerDuration(TimeSpan.FromMinutes(3));
    private void Preset5_Click(object sender, RoutedEventArgs e) => ApplyTimerDuration(TimeSpan.FromMinutes(5));
    private void Preset10_Click(object sender, RoutedEventArgs e) => ApplyTimerDuration(TimeSpan.FromMinutes(10));
    private void Preset15_Click(object sender, RoutedEventArgs e) => ApplyTimerDuration(TimeSpan.FromMinutes(15));
    private void Preset30_Click(object sender, RoutedEventArgs e) => ApplyTimerDuration(TimeSpan.FromMinutes(30));
    private void Preset45_Click(object sender, RoutedEventArgs e) => ApplyTimerDuration(TimeSpan.FromMinutes(45));
    private void Preset60_Click(object sender, RoutedEventArgs e) => ApplyTimerDuration(TimeSpan.FromMinutes(60));
    private void Preset90_Click(object sender, RoutedEventArgs e) => ApplyTimerDuration(TimeSpan.FromMinutes(90));
    private void Preset120_Click(object sender, RoutedEventArgs e) => ApplyTimerDuration(TimeSpan.FromMinutes(120));

    private void StartRunning(bool stopwatchMode)
    {
        StopCompletionFeedback();
        _isStopwatchMode = stopwatchMode;
        _lastTickTimestamp = Stopwatch.GetTimestamp();
        _isRunning = true;
        _tickTimer.Start();
        UpdateMode();
        UpdateDisplay();
    }

    private void TickTimer_Tick(object? sender, EventArgs e)
    {
        long now = Stopwatch.GetTimestamp();
        var delta = Stopwatch.GetElapsedTime(_lastTickTimestamp, now);
        _lastTickTimestamp = now;

        if (_isStopwatchMode)
        {
            _stopwatchElapsed += delta;
        }
        else
        {
            _timerRemaining -= delta;
            if (_timerRemaining <= TimeSpan.Zero)
            {
                _timerRemaining = TimeSpan.Zero;
                StopRunning();
                StartCompletionFeedback();
                if (_soundEnabled)
                {
                    SystemSounds.Exclamation.Play();
                }
            }
        }

        UpdateDisplay();
    }

    private void TimeInput_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        // Only allow digits and colons
        e.Handled = !e.Text.All(ch => char.IsDigit(ch) || ch == ':');

        if (!e.Handled && e.Text == ":" && TxtTimerInput.Text.Count(c => c == ':') >= 2)
        {
            // Limit to 2 colons max
            e.Handled = true;
        }
    }

    private void TimerInput_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (_isUpdatingInput ||
            _isRunning ||
            _isStopwatchMode ||
            TxtTimerInput == null ||
            TxtTimerDisplay == null ||
            TxtStopwatchDisplay == null ||
            BtnStartPause == null ||
            TimerProgressTicks == null)
        {
            return;
        }

        ReadTimerDuration();
        _timerRemaining = _timerDuration;
        UpdateDisplay();
    }

    private void TimerProgressTicks_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        _progressSegmentCount = 0;
        UpdateDisplay();
    }

    private void ReadTimerDuration()
    {
        _timerDuration = TimerStopwatchFormatter.ParseTimerInput(TxtTimerInput.Text);
    }

    private void ApplyTimerDuration(TimeSpan duration)
    {
        StopCompletionFeedback();
        StopRunning();
        _timerDuration = duration;
        _timerRemaining = duration;
        _isUpdatingInput = true;
        TxtTimerInput.Text = $"{(int)duration.TotalHours:00}:{duration.Minutes:00}:{duration.Seconds:00}";
        _isUpdatingInput = false;
        UpdateDisplay();
    }

    private void StopRunning()
    {
        _isRunning = false;
        _tickTimer.Stop();
    }

    private void UpdateMode()
    {
        if (_isStopwatchMode)
        {
            StopCompletionFeedback();
        }

        var metrics = TimerStopwatchLayoutHelper.GetWindowMetrics(_isCompactMode, _isStopwatchMode);
        MinWidth = metrics.MinWidth;
        MinHeight = metrics.MinHeight;
        Width = metrics.Width;
        Height = metrics.Height;

        _isUpdatingMode = true;
        try
        {
            RootBorder.Visibility = _isCompactMode ? Visibility.Collapsed : Visibility.Visible;
            CompactRootBorder.Visibility = _isCompactMode ? Visibility.Visible : Visibility.Collapsed;
            FullHeaderView.Visibility = Visibility.Visible;
            FullActionView.Visibility = Visibility.Visible;
            TimerView.Visibility = !_isStopwatchMode ? Visibility.Visible : Visibility.Collapsed;
            StopwatchView.Visibility = _isStopwatchMode ? Visibility.Visible : Visibility.Collapsed;
            RbTimer.IsChecked = !_isStopwatchMode;
            RbStopwatch.IsChecked = _isStopwatchMode;
            HeaderRow.Height = new GridLength(36);
            DisplayRow.Height = new GridLength(126);
            PresetRows.Height = new GridLength(84);
            TimerInputRow.Height = new GridLength(44);
            ActionSpacerRow.Height = new GridLength(20);
            ActionRow.Height = new GridLength(44);
            BtnCompact.Content = TimerStopwatchLayoutHelper.CompactToggleGlyph;
            RootBorder.Margin = new Thickness(8);
            RootBorder.CornerRadius = new CornerRadius(8);
            InnerBorder.CornerRadius = new CornerRadius(7);
            ContentGrid.Margin = new Thickness(20);
        }
        finally
        {
            _isUpdatingMode = false;
        }
    }

    private void StartCompletionFeedback()
    {
        if (_isStopwatchMode)
        {
            return;
        }

        _completionFeedbackActive = true;
        BeginStoryboard("TimerDisplayCompletionFlashStoryboard");
        BeginStoryboard("TimerCompletionFlashStoryboard");
    }

    private void StopCompletionFeedback()
    {
        if (!_completionFeedbackActive)
        {
            return;
        }

        _completionFeedbackActive = false;
        StopStoryboard("TimerDisplayCompletionFlashStoryboard");
        StopStoryboard("TimerCompletionFlashStoryboard");
        TxtTimerDisplay.Opacity = 1;
        TxtCompactDisplay.Opacity = 1;
        CompactCompletionFlash.Opacity = 0;
    }

    private void BeginStoryboard(string resourceKey)
    {
        if (TryFindResource(resourceKey) is Storyboard storyboard)
        {
            storyboard.Begin(this, true);
        }
    }

    private void StopStoryboard(string resourceKey)
    {
        if (TryFindResource(resourceKey) is Storyboard storyboard)
        {
            storyboard.Stop(this);
        }
    }

    private void UpdateDisplay()
    {
        TxtTimerDisplay.Text = TimerStopwatchFormatter.FormatTimer(_timerRemaining);
        TxtStopwatchDisplay.Text = TimerStopwatchFormatter.FormatStopwatch(_stopwatchElapsed);
        TxtCompactDisplay.Text = _isStopwatchMode
            ? TimerStopwatchFormatter.FormatStopwatch(_stopwatchElapsed)
            : TimerStopwatchFormatter.FormatTimer(_timerRemaining);
        TxtCompactModeIcon.Text = TimerStopwatchLayoutHelper.GetModeGlyph(_isStopwatchMode);
        BtnSound.Content = _soundEnabled ? "\uEB42" : "\uEB4F";
        BtnSound.ToolTip = LocalizationService.Get(_soundEnabled
            ? "TimerStopwatch_SoundDefault"
            : "TimerStopwatch_SoundOff");
        TxtStartPauseIcon.Text = _isRunning ? "\uE769" : "\uE768";
        TxtStartPauseLabel.Text = LocalizationService.Get(_isRunning
            ? "TimerStopwatch_Pause"
            : "TimerStopwatch_Start");
        BtnCompactStartPause.Content = _isRunning
            ? TimerStopwatchLayoutHelper.CompactPauseGlyph
            : TimerStopwatchLayoutHelper.CompactPlayGlyph;
        BtnCompactStartPause.ToolTip = LocalizationService.Get(_isRunning
            ? "TimerStopwatch_Pause"
            : "TimerStopwatch_Start");

        double progress = _timerDuration > TimeSpan.Zero
            ? Math.Clamp(_timerRemaining.TotalMilliseconds / _timerDuration.TotalMilliseconds, 0, 1)
            : 0;
        UpdateTimerProgressTicks(progress);
        UpdateSelectedPreset();
    }

    private void UpdateTimerProgressTicks(double progress)
    {
        int segmentCount = TimerStopwatchLayoutHelper.GetProgressSegmentCount(_timerDuration);
        EnsureProgressTickCount(segmentCount);
        int visibleCount = (int)Math.Ceiling(Math.Clamp(progress, 0, 1) * segmentCount);

        for (int i = 0; i < TimerProgressTicks.Children.Count; i++)
        {
            TimerProgressTicks.Children[i].Opacity = i < visibleCount ? 1 : 0;
        }
    }

    private void EnsureProgressTickCount(int segmentCount)
    {
        if (_progressSegmentCount == segmentCount)
        {
            return;
        }

        TimerProgressTicks.Children.Clear();

        var accent = (System.Windows.Media.Brush)FindResource("AccentColor");
        var tickMetrics = TimerStopwatchLayoutHelper.GetProgressTickMetrics(segmentCount, TimerProgressTicks.ActualWidth);

        for (int i = 0; i < segmentCount; i++)
        {
            var tick = new System.Windows.Controls.Border
            {
                Width = tickMetrics.TickWidth,
                Height = tickMetrics.TickHeight,
                Background = accent,
                CornerRadius = new CornerRadius(tickMetrics.TickWidth / 2),
            };
            System.Windows.Controls.Canvas.SetLeft(tick, i * tickMetrics.Step);
            System.Windows.Controls.Canvas.SetTop(tick, (TimerProgressTicks.Height - tickMetrics.TickHeight) / 2);
            TimerProgressTicks.Children.Add(tick);
        }

        _progressSegmentCount = segmentCount;
    }

    private void UpdateSelectedPreset()
    {
        var presets = new (System.Windows.Controls.Button Button, TimeSpan Duration)[]
        {
            (BtnPreset1, TimeSpan.FromMinutes(1)),
            (BtnPreset3, TimeSpan.FromMinutes(3)),
            (BtnPreset5, TimeSpan.FromMinutes(5)),
            (BtnPreset10, TimeSpan.FromMinutes(10)),
            (BtnPreset15, TimeSpan.FromMinutes(15)),
            (BtnPreset30, TimeSpan.FromMinutes(30)),
            (BtnPreset45, TimeSpan.FromMinutes(45)),
            (BtnPreset60, TimeSpan.FromMinutes(60)),
            (BtnPreset90, TimeSpan.FromMinutes(90)),
            (BtnPreset120, TimeSpan.FromMinutes(120)),
        };

        foreach (var (button, duration) in presets)
        {
            bool selected = _timerDuration == duration;
            if (selected)
            {
                button.Background = (System.Windows.Media.Brush)FindResource("AccentColor");
                button.BorderBrush = (System.Windows.Media.Brush)FindResource("AccentColor");
                button.Foreground = System.Windows.Media.Brushes.White;
            }
            else
            {
                button.SetResourceReference(System.Windows.Controls.Control.BackgroundProperty, "FormControlBackground");
                button.SetResourceReference(System.Windows.Controls.Control.BorderBrushProperty, "FormControlBorderBrush");
                button.Foreground = (System.Windows.Media.Brush)FindResource("PrimaryText");
            }
        }
    }

    protected override void OnLocalizationChanged()
    {
        UpdateDisplay();
    }
}
