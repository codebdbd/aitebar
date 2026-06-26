using System;
using System.IO;
using System.Runtime.Versioning;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Forms = System.Windows.Forms;

namespace AiteBar;

[SupportedOSPlatform("windows6.1")]
public partial class QRCodeGeneratorWindow : DarkWindow
{
    private readonly QRCodeService _service = new();
    private CancellationTokenSource? _previewRequestCts;
    private QRCodeGenerationOptions? _lastOptions;
    private byte[]? _lastPngBytes;
    private string? _lastSvgContent;
    private bool _isInitialized;
    private bool _isApplyingPreset;

    public QRCodeGeneratorWindow()
    {
        InitializeComponent();

        // Initialize Content Type combo
        CmbContentType.Items.Add(new ComboItem<QRCodeContentType>(LocalizationService.Get("QRCodeGenerator_TypeText"), QRCodeContentType.Text));
        CmbContentType.Items.Add(new ComboItem<QRCodeContentType>(LocalizationService.Get("QRCodeGenerator_TypeUrl"), QRCodeContentType.Url));
        CmbContentType.Items.Add(new ComboItem<QRCodeContentType>(LocalizationService.Get("QRCodeGenerator_TypeWifi"), QRCodeContentType.Wifi));
        CmbContentType.SelectedIndex = 1;

        // Initialize Wifi Security combo
        CmbWifiSecurity.Items.Add(new ComboItem<QRCodeWifiSecurity>(LocalizationService.Get("QRCodeGenerator_WifiSecurityWpa"), QRCodeWifiSecurity.Wpa));
        CmbWifiSecurity.Items.Add(new ComboItem<QRCodeWifiSecurity>(LocalizationService.Get("QRCodeGenerator_WifiSecurityWep"), QRCodeWifiSecurity.Wep));
        CmbWifiSecurity.Items.Add(new ComboItem<QRCodeWifiSecurity>(LocalizationService.Get("QRCodeGenerator_WifiSecurityNone"), QRCodeWifiSecurity.None));
        CmbWifiSecurity.SelectedIndex = 0;

        // Initialize Quality Preset combo
        CmbQualityPreset.Items.Add(new ComboItem<QRCodeQualityPreset>(LocalizationService.Get("QRCodeGenerator_PresetScreen"), QRCodeQualityPreset.Screen));
        CmbQualityPreset.Items.Add(new ComboItem<QRCodeQualityPreset>(LocalizationService.Get("QRCodeGenerator_PresetScreenHD"), QRCodeQualityPreset.ScreenHD));
        CmbQualityPreset.Items.Add(new ComboItem<QRCodeQualityPreset>(LocalizationService.Get("QRCodeGenerator_PresetPrint"), QRCodeQualityPreset.Print));
        CmbQualityPreset.Items.Add(new ComboItem<QRCodeQualityPreset>(LocalizationService.Get("QRCodeGenerator_PresetPrintHigh"), QRCodeQualityPreset.PrintHigh));
        CmbQualityPreset.Items.Add(new ComboItem<QRCodeQualityPreset>(LocalizationService.Get("QRCodeGenerator_PresetLogo"), QRCodeQualityPreset.Logo));
        CmbQualityPreset.SelectedIndex = 0;

        // Initialize Output Size combo
        CmbOutputSize.Items.Add(new ComboItem<int>(LocalizationService.Get("QRCodeGenerator_Output512"), 512));
        CmbOutputSize.Items.Add(new ComboItem<int>(LocalizationService.Get("QRCodeGenerator_Output800"), 800));
        CmbOutputSize.Items.Add(new ComboItem<int>(LocalizationService.Get("QRCodeGenerator_Output1000"), 1000));
        CmbOutputSize.Items.Add(new ComboItem<int>(LocalizationService.Get("QRCodeGenerator_Output1200"), 1200));
        CmbOutputSize.Items.Add(new ComboItem<int>(LocalizationService.Get("QRCodeGenerator_Output1600"), 1600));
        CmbOutputSize.SelectedIndex = 1;

        // Initialize ECC Level combo
        CmbEccLevel.Items.Add(new ComboItem<QRCodeEccLevel>(LocalizationService.Get("QRCodeGenerator_EccLevelL"), QRCodeEccLevel.L));
        CmbEccLevel.Items.Add(new ComboItem<QRCodeEccLevel>(LocalizationService.Get("QRCodeGenerator_EccLevelM"), QRCodeEccLevel.M));
        CmbEccLevel.Items.Add(new ComboItem<QRCodeEccLevel>(LocalizationService.Get("QRCodeGenerator_EccLevelQ"), QRCodeEccLevel.Q));
        CmbEccLevel.Items.Add(new ComboItem<QRCodeEccLevel>(LocalizationService.Get("QRCodeGenerator_EccLevelH"), QRCodeEccLevel.H));
        CmbEccLevel.SelectedIndex = 2;

        // Initialize Module Shape combo
        CmbModuleShape.Items.Add(new ComboItem<QRCodeModuleShape>(LocalizationService.Get("QRCodeGenerator_ModuleSquare"), QRCodeModuleShape.Square));
        CmbModuleShape.Items.Add(new ComboItem<QRCodeModuleShape>(LocalizationService.Get("QRCodeGenerator_ModuleRounded"), QRCodeModuleShape.Rounded));
        CmbModuleShape.Items.Add(new ComboItem<QRCodeModuleShape>(LocalizationService.Get("QRCodeGenerator_ModuleCircle"), QRCodeModuleShape.Circle));
        CmbModuleShape.Items.Add(new ComboItem<QRCodeModuleShape>(LocalizationService.Get("QRCodeGenerator_ModuleDot"), QRCodeModuleShape.Dot));
        CmbModuleShape.Items.Add(new ComboItem<QRCodeModuleShape>(LocalizationService.Get("QRCodeGenerator_ModuleDiamond"), QRCodeModuleShape.Diamond));
        CmbModuleShape.SelectedIndex = 0;

        // Initialize Eye Style combo
        CmbEyeStyle.Items.Add(new ComboItem<QRCodeEyeStyle>(LocalizationService.Get("QRCodeGenerator_EyeSquare"), QRCodeEyeStyle.Square));
        CmbEyeStyle.Items.Add(new ComboItem<QRCodeEyeStyle>(LocalizationService.Get("QRCodeGenerator_EyeRounded"), QRCodeEyeStyle.Rounded));
        CmbEyeStyle.Items.Add(new ComboItem<QRCodeEyeStyle>(LocalizationService.Get("QRCodeGenerator_EyeCircle"), QRCodeEyeStyle.Circle));
        CmbEyeStyle.Items.Add(new ComboItem<QRCodeEyeStyle>(LocalizationService.Get("QRCodeGenerator_EyeDiamond"), QRCodeEyeStyle.Diamond));
        CmbEyeStyle.SelectedIndex = 0;

        TxtDarkColor.Text = "#000000";
        TxtLightColor.Text = "#FFFFFF";
        _isInitialized = true;
        UpdateInputMode();
        ApplyQualityPreset();
        SetEmptyState();
        TxtInput.Focus();
    }

    public void ShowNearPanel(AppSettingsService settingsService)
    {
        var settings = settingsService.Settings;
        var screens = Forms.Screen.AllScreens;
        var screen = settings.MonitorIndex >= 0 && settings.MonitorIndex < screens.Length
            ? screens[settings.MonitorIndex]
            : Forms.Screen.PrimaryScreen;
        var work = screen?.WorkingArea ?? Forms.Screen.PrimaryScreen?.WorkingArea ?? new System.Drawing.Rectangle(0, 0, 1280, 720);

        Measure(new System.Windows.Size(Width, double.PositiveInfinity));
        double windowHeight = DesiredSize.Height > 0 ? DesiredSize.Height : 520;

        var (_, _, shownX, shownY) = QuickNoteLayoutHelper.GetSlideCoordinates(settings.Edge, work, Width, windowHeight);
        Left = shownX;
        Top = shownY;
        Show();
        Activate();
        FocusCurrentInput();
    }

    private void TxtInput_TextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateInputPlaceholder();
        UpdateColorSwatches();
        QueuePreviewRefresh();
    }

    private void ContentType_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!_isInitialized)
        {
            return;
        }

        UpdateInputMode();
        QueuePreviewRefresh();
    }

    private void QualityPreset_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!_isInitialized)
        {
            return;
        }

        ApplyQualityPreset();
        QueuePreviewRefresh();
    }

    private void Options_Changed(object sender, RoutedEventArgs e)
    {
        if (!_isInitialized || _isApplyingPreset)
        {
            return;
        }

        UpdateMarginValue();
        QueuePreviewRefresh();
    }

    private void Options_Changed(object sender, EventArgs e)
    {
        if (!_isInitialized || _isApplyingPreset)
        {
            return;
        }

        UpdateMarginValue();
        QueuePreviewRefresh();
    }

    private void QueuePreviewRefresh()
    {
        if (!_isInitialized)
        {
            return;
        }

        _previewRequestCts?.Cancel();
        _previewRequestCts?.Dispose();
        _previewRequestCts = new CancellationTokenSource();
        CancellationToken token = _previewRequestCts.Token;

        _ = RefreshPreviewAsync(token);
    }

    private async Task RefreshPreviewAsync(CancellationToken token)
    {
        try
        {
            await Task.Delay(180, token);
            if (!HasRequiredInput())
            {
                SetEmptyState();
                return;
            }

            QRCodeGenerationOptions options = BuildOptions();
            QRCodeGenerationResult result = await _service.GenerateAsync(options, token);

            ImgPreview.Source = CreateBitmapImage(result.PngBytes);
            _lastOptions = options;
            _lastPngBytes = result.PngBytes;
            _lastSvgContent = result.SvgContent;

            string status = LocalizationService.Format("QRCodeGenerator_StatusDetailed", result.Version, result.ModuleCount, result.PixelWidth);
            if (result.Warnings.Count > 0)
            {
                status = $"{status}. {string.Join(' ', result.Warnings)}";
            }

            SetPreviewState(status, LocalizationService.Format("QRCodeGenerator_ContrastStatus", result.ContrastRatio.ToString("0.0")));
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Logger.Log(ex);
            ImgPreview.Source = null;
            _lastOptions = null;
            _lastPngBytes = null;
            _lastSvgContent = null;
            SetActionsEnabled(false);
            EmptyPreviewSurface.Visibility = Visibility.Visible;
            PreviewSurface.Visibility = Visibility.Collapsed;
            TxtStatus.Visibility = Visibility.Visible;
            TxtContrast.Visibility = Visibility.Collapsed;
            TxtStatus.Text = ex is ArgumentException or FileNotFoundException or InvalidDataException
                ? ex.Message
                : LocalizationService.Get("QRCodeGenerator_ErrorGeneric");
        }
    }

    private QRCodeGenerationOptions BuildOptions()
    {
        return new QRCodeGenerationOptions
        {
            Text = TxtInput.Text,
            ContentType = GetSelectedValue(CmbContentType, QRCodeContentType.Url),
            WifiSsid = TxtWifiSsid.Text,
            WifiPassword = TxtWifiPassword.Text,
            WifiSecurity = GetSelectedValue(CmbWifiSecurity, QRCodeWifiSecurity.Wpa),
            WifiHidden = ChkWifiHidden.IsChecked == true,
            QualityPreset = GetSelectedValue(CmbQualityPreset, QRCodeQualityPreset.Screen),
            OutputSize = GetSelectedValue(CmbOutputSize, 800),
            Margin = (int)SliderMargin.Value,
            EccLevel = GetSelectedValue(CmbEccLevel, QRCodeEccLevel.Q),
            DarkColor = TxtDarkColor.Text,
            LightColor = TxtLightColor.Text,
            ModuleShape = GetSelectedValue(CmbModuleShape, QRCodeModuleShape.Square),
            EyeStyle = GetSelectedValue(CmbEyeStyle, QRCodeEyeStyle.Square),
            LogoPath = string.IsNullOrWhiteSpace(TxtLogoPath.Text) ? null : TxtLogoPath.Text,
            LogoSizePercent = (int)SliderLogoSize.Value
        };
    }

    private async Task EnsurePngAsync()
    {
        if (_lastOptions == null)
        {
            throw new InvalidOperationException(LocalizationService.Get("QRCodeGenerator_ErrorEmptyText"));
        }

        if (_lastPngBytes == null)
        {
            _lastPngBytes = await _service.GeneratePngAsync(_lastOptions);
        }
    }

    private async Task EnsureSvgAsync()
    {
        if (_lastOptions == null)
        {
            throw new InvalidOperationException(LocalizationService.Get("QRCodeGenerator_ErrorEmptyText"));
        }

        if (_lastSvgContent == null)
        {
            _lastSvgContent = await _service.GenerateSvgAsync(_lastOptions);
        }
    }

    private async void BtnCopyPng_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await EnsurePngAsync();
            Clipboard.SetImage(CreateBitmapImage(_lastPngBytes!));
            TxtStatus.Text = LocalizationService.Get("QRCodeGenerator_Copied");
        }
        catch (Exception ex)
        {
            Logger.Log(ex);
            TxtStatus.Text = ex.Message;
        }
    }

    private async void BtnCopySvg_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await EnsureSvgAsync();
            Clipboard.SetText(_lastSvgContent!);
            TxtStatus.Text = LocalizationService.Get("QRCodeGenerator_CopiedSvg");
        }
        catch (Exception ex)
        {
            Logger.Log(ex);
            TxtStatus.Text = ex.Message;
        }
    }

    private async void BtnSavePng_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = LocalizationService.Get("QRCodeGenerator_SavePngTitle"),
            Filter = "PNG (*.png)|*.png",
            FileName = GenerateFileName(".png"),
            AddExtension = true,
            DefaultExt = ".png",
            OverwritePrompt = true
        };

        if (dialog.ShowDialog(this) == true)
        {
            await SaveAsync(dialog.FileName, saveSvg: false);
        }
    }

    private async void BtnSaveSvg_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = LocalizationService.Get("QRCodeGenerator_SaveSvgTitle"),
            Filter = "SVG (*.svg)|*.svg",
            FileName = GenerateFileName(".svg"),
            AddExtension = true,
            DefaultExt = ".svg",
            OverwritePrompt = true
        };

        if (dialog.ShowDialog(this) == true)
        {
            await SaveAsync(dialog.FileName, saveSvg: true);
        }
    }

    private string GenerateFileName(string extension)
    {
        var contentType = GetSelectedValue(CmbContentType, QRCodeContentType.Url);
        string baseName = "qr-code";

        if (contentType == QRCodeContentType.Url && !string.IsNullOrWhiteSpace(TxtInput.Text))
        {
            string url = TxtInput.Text.Trim();
            if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
                url = url.Substring(7);
            else if (url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                url = url.Substring(8);

            int slashIndex = url.IndexOf('/');
            if (slashIndex > 0)
                url = url.Substring(0, slashIndex);

            baseName = SanitizeFileName(url);
        }
        else if (contentType == QRCodeContentType.Wifi && !string.IsNullOrWhiteSpace(TxtWifiSsid.Text))
        {
            baseName = SanitizeFileName("wifi-" + TxtWifiSsid.Text);
        }

        return baseName + extension;
    }

    private string SanitizeFileName(string fileName)
    {
        char[] invalidChars = Path.GetInvalidFileNameChars();
        foreach (char c in invalidChars)
        {
            fileName = fileName.Replace(c, '_');
        }
        if (fileName.Length > 50)
            fileName = fileName.Substring(0, 50);
        return fileName;
    }

    private void BtnChooseLogo_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = LocalizationService.Get("QRCodeGenerator_ChooseLogoTitle"),
            Filter = LocalizationService.Get("QRCodeGenerator_LogoFilter"),
            CheckFileExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog(this) == true)
        {
            TxtLogoPath.Text = dialog.FileName;
            UpdateLogoPreview();
            SetSelectedValue(CmbQualityPreset, QRCodeQualityPreset.Logo);
            QueuePreviewRefresh();
        }
    }

    private void BtnClearLogo_Click(object sender, RoutedEventArgs e)
    {
        TxtLogoPath.Text = string.Empty;
        LogoPreviewBorder.Visibility = Visibility.Collapsed;
        LogoPreviewImage.Source = null;
        QueuePreviewRefresh();
    }

    private void DarkColorSwatch_Click(object sender, MouseButtonEventArgs e)
    {
        ShowColorPicker(TxtDarkColor, DarkColorSwatch);
    }

    private void LightColorSwatch_Click(object sender, MouseButtonEventArgs e)
    {
        ShowColorPicker(TxtLightColor, LightColorSwatch);
    }

    private void ShowColorPicker(TextBox colorTextBox, Border colorSwatch)
    {
        if (TryParseColor(colorTextBox.Text, out var initialColor))
        {
        }
        else
        {
            initialColor = Colors.Black;
        }
        
        var dialog = new ColorPickerDialog(initialColor)
        {
            Owner = this
        };

        if (dialog.ShowDialog() == true)
        {
            string hex = $"#{dialog.SelectedColor.R:X2}{dialog.SelectedColor.G:X2}{dialog.SelectedColor.B:X2}";
            colorTextBox.Text = hex;
            UpdateColorSwatches();
            QueuePreviewRefresh();
        }
    }

    private void UpdateColorSwatches()
    {
        if (TryParseColor(TxtDarkColor.Text, out var darkColor))
        {
            DarkColorSwatch.Background = new SolidColorBrush(darkColor);
        }

        if (TryParseColor(TxtLightColor.Text, out var lightColor))
        {
            LightColorSwatch.Background = new SolidColorBrush(lightColor);
        }
    }

    private bool TryParseColor(string hex, out System.Windows.Media.Color color)
    {
        color = System.Windows.Media.Colors.Black;
        try
        {
            if (hex.StartsWith("#") && hex.Length == 7)
            {
                byte r = Convert.ToByte(hex.Substring(1, 2), 16);
                byte g = Convert.ToByte(hex.Substring(3, 2), 16);
                byte b = Convert.ToByte(hex.Substring(5, 2), 16);
                color = System.Windows.Media.Color.FromRgb(r, g, b);
                return true;
            }
        }
        catch
        {
        }
        return false;
    }

    private void UpdateLogoPreview()
    {
        if (File.Exists(TxtLogoPath.Text))
        {
            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(TxtLogoPath.Text);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                bitmap.Freeze();
                LogoPreviewImage.Source = bitmap;
                LogoPreviewBorder.Visibility = Visibility.Visible;
            }
            catch
            {
                LogoPreviewBorder.Visibility = Visibility.Collapsed;
            }
        }
        else
        {
            LogoPreviewBorder.Visibility = Visibility.Collapsed;
        }
    }

    private void UpdateMarginValue()
        {
            TxtMarginValue.Text = ((int)SliderMargin.Value).ToString();
            TxtLogoSizeValue.Text = $"{(int)SliderLogoSize.Value}%";
        }

    private async Task SaveAsync(string path, bool saveSvg)
    {
        try
        {
            SetActionsEnabled(false);
            if (saveSvg)
            {
                await EnsureSvgAsync();
                await File.WriteAllTextAsync(path, _lastSvgContent!, Encoding.UTF8);
            }
            else
            {
                await EnsurePngAsync();
                await File.WriteAllBytesAsync(path, _lastPngBytes!);
            }

            TxtStatus.Text = LocalizationService.Get("QRCodeGenerator_SaveSuccess");
        }
        catch (Exception ex)
        {
            Logger.Log(ex);
            new DarkDialog(LocalizationService.Format("QRCodeGenerator_SaveFailed", ex.Message)) { Owner = this }.ShowDialog();
        }
        finally
        {
            SetActionsEnabled(_lastOptions != null);
        }
    }

    private static BitmapImage CreateBitmapImage(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.StreamSource = stream;
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }

    private void SetEmptyState()
    {
        ImgPreview.Source = null;
        _lastOptions = null;
        _lastPngBytes = null;
        _lastSvgContent = null;
        PreviewSurface.Visibility = Visibility.Collapsed;
        EmptyPreviewSurface.Visibility = Visibility.Visible;
        TxtStatus.Visibility = Visibility.Collapsed;
        TxtContrast.Visibility = Visibility.Collapsed;
        TxtStatus.Text = string.Empty;
        TxtContrast.Text = string.Empty;
        SetActionsEnabled(false);
    }

    private void SetPreviewState(string status, string contrast)
    {
        PreviewSurface.Visibility = Visibility.Visible;
        EmptyPreviewSurface.Visibility = Visibility.Collapsed;
        TxtStatus.Visibility = Visibility.Visible;
        TxtContrast.Visibility = Visibility.Visible;
        TxtStatus.Text = status;
        TxtContrast.Text = contrast;
        SetActionsEnabled(true);
    }

    private void SetActionsEnabled(bool enabled)
    {
        BtnCopyPng.IsEnabled = enabled;
        BtnCopySvg.IsEnabled = enabled;
        BtnSavePng.IsEnabled = enabled;
        BtnSaveSvg.IsEnabled = enabled;
    }

    private void UpdateInputMode()
    {
        bool isWifi = GetSelectedValue(CmbContentType, QRCodeContentType.Url) == QRCodeContentType.Wifi;
        InputTextPanel.Visibility = isWifi ? Visibility.Collapsed : Visibility.Visible;
        WifiPanel.Visibility = isWifi ? Visibility.Visible : Visibility.Collapsed;
        UpdateInputPlaceholder();
        FocusCurrentInput();
    }

    private void UpdateInputPlaceholder()
    {
        TxtInputPlaceholder.Visibility = string.IsNullOrEmpty(TxtInput.Text)
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void ApplyQualityPreset()
    {
        _isApplyingPreset = true;
        try
        {
            QRCodeQualityPreset preset = GetSelectedValue(CmbQualityPreset, QRCodeQualityPreset.Screen);
            int outputSize = preset.GetOutputSize();
            SetSelectedValue(CmbOutputSize, outputSize);

            if (preset == QRCodeQualityPreset.Logo)
            {
                SetSelectedValue(CmbEccLevel, QRCodeEccLevel.H);
                CmbEccLevel.IsEnabled = false;
            }
            else
            {
                CmbEccLevel.IsEnabled = true;
            }
        }
        finally
        {
            _isApplyingPreset = false;
        }
    }

    private bool HasRequiredInput()
    {
        return GetSelectedValue(CmbContentType, QRCodeContentType.Url) == QRCodeContentType.Wifi
            ? !string.IsNullOrWhiteSpace(TxtWifiSsid.Text)
            : !string.IsNullOrWhiteSpace(TxtInput.Text);
    }

    private void FocusCurrentInput()
    {
        if (!_isInitialized)
        {
            return;
        }

        if (GetSelectedValue(CmbContentType, QRCodeContentType.Url) == QRCodeContentType.Wifi)
        {
            TxtWifiSsid.Focus();
        }
        else
        {
            TxtInput.Focus();
        }
    }

    private static T GetSelectedValue<T>(ComboBox comboBox, T fallback)
    {
        if (comboBox.SelectedItem is ComboItem<T> item)
        {
            return item.Value;
        }
        return fallback;
    }

    private static void SetSelectedValue<T>(ComboBox comboBox, T value)
    {
        foreach (object option in comboBox.Items)
        {
            if (option is ComboItem<T> item && Equals(item.Value, value))
            {
                comboBox.SelectedItem = item;
                return;
            }
        }
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _previewRequestCts?.Cancel();
        _previewRequestCts?.Dispose();
        base.OnClosed(e);
    }

    protected override void OnLocalizationChanged()
    {
        if (_lastOptions == null)
        {
            SetEmptyState();
        }
        else
        {
            QueuePreviewRefresh();
        }

        UpdateInputPlaceholder();
    }
}
