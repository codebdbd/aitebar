using System;
using System.IO;
using System.Runtime.Versioning;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using QRCoder;
using QRCoder.Xaml;
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

    public QRCodeGeneratorWindow()
    {
        InitializeComponent();
        _isInitialized = true;
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

        var (_, _, shownX, shownY) = QuickNoteLayoutHelper.GetSlideCoordinates(settings.Edge, work, Width, Height);
        Left = shownX;
        Top = shownY;
        Show();
        Activate();
        TxtInput.Focus();
    }

    private void TxtInput_TextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateInputPlaceholder();
        QueuePreviewRefresh();
    }

    private void Options_Changed(object sender, RoutedEventArgs e)
    {
        if (!_isInitialized)
        {
            return;
        }

        TxtPixelSize.Text = $"{(int)SldPixelSize.Value}";
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
            string text = TxtInput.Text;
            if (string.IsNullOrWhiteSpace(text))
            {
                SetEmptyState();
                return;
            }

            QRCodeGenerationOptions options = BuildOptions(text);
            using QRCodeData qrData = _service.GenerateQrData(options.Text, options.EccLevel);
            token.ThrowIfCancellationRequested();

            var renderer = new XamlQRCode(qrData);
            ImgPreview.Source = renderer.GetGraphic(
                options.PixelSize,
                options.DarkColor,
                options.LightColor,
                drawQuietZones: options.Margin > 0);

            int moduleCount = qrData.ModuleMatrix.Count;
            int version = QRCodeService.GetVersion(qrData);
            _lastOptions = options;
            _lastPngBytes = null;
            _lastSvgContent = null;
            SetPreviewState(LocalizationService.Format("QRCodeGenerator_Status", version, moduleCount));
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
            TxtEmptyHint.Visibility = Visibility.Visible;
            PreviewSurface.Visibility = Visibility.Collapsed;
            TxtStatus.Text = ex.Message;
        }
    }

    private QRCodeGenerationOptions BuildOptions(string text)
    {
        QRCodeEccLevel eccLevel = QRCodeEccLevel.Q;
        if (CmbEccLevel.SelectedItem is ComboBoxItem item &&
            Enum.TryParse(item.Tag?.ToString(), out QRCodeEccLevel parsed))
        {
            eccLevel = parsed;
        }

        return new QRCodeGenerationOptions
        {
            Text = text,
            PixelSize = (int)SldPixelSize.Value,
            Margin = 4,
            EccLevel = eccLevel
        };
    }

    private async Task EnsureRenderedArtifactsAsync()
    {
        if (_lastOptions == null)
        {
            throw new InvalidOperationException(LocalizationService.Get("QRCodeGenerator_ErrorEmptyText"));
        }

        if (_lastPngBytes != null && _lastSvgContent != null)
        {
            return;
        }

        QRCodeGenerationResult result = await _service.GenerateAsync(_lastOptions);
        _lastPngBytes = result.PngBytes;
        _lastSvgContent = result.SvgContent;
    }

    private async void BtnCopyPng_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await EnsureRenderedArtifactsAsync();
            Clipboard.SetImage(CreateBitmapImage(_lastPngBytes!));
            TxtStatus.Text = LocalizationService.Get("QRCodeGenerator_Copied");
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
            FileName = "qr-code.png",
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
            FileName = "qr-code.svg",
            AddExtension = true,
            DefaultExt = ".svg",
            OverwritePrompt = true
        };

        if (dialog.ShowDialog(this) == true)
        {
            await SaveAsync(dialog.FileName, saveSvg: true);
        }
    }

    private async Task SaveAsync(string path, bool saveSvg)
    {
        try
        {
            SetActionsEnabled(false);
            await EnsureRenderedArtifactsAsync();
            if (saveSvg)
            {
                await File.WriteAllTextAsync(path, _lastSvgContent!, Encoding.UTF8);
            }
            else
            {
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
        TxtEmptyHint.Visibility = Visibility.Visible;
        TxtStatus.Visibility = Visibility.Collapsed;
        TxtStatus.Text = string.Empty;
        SetActionsEnabled(false);
    }

    private void SetPreviewState(string status)
    {
        PreviewSurface.Visibility = Visibility.Visible;
        TxtEmptyHint.Visibility = Visibility.Collapsed;
        TxtStatus.Visibility = Visibility.Visible;
        TxtStatus.Text = status;
        SetActionsEnabled(true);
    }

    private void SetActionsEnabled(bool enabled)
    {
        BtnCopyPng.IsEnabled = enabled;
        BtnSavePng.IsEnabled = enabled;
        BtnSaveSvg.IsEnabled = enabled;
    }

    private void UpdateInputPlaceholder()
    {
        TxtInputPlaceholder.Visibility = string.IsNullOrEmpty(TxtInput.Text)
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
        }
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
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
