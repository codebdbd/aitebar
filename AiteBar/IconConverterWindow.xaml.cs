using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Forms = System.Windows.Forms;

namespace AiteBar;

[SupportedOSPlatform("windows6.1")]
public partial class IconConverterWindow : DarkWindow
{
    private readonly IconConverterService _converterService = new();
    private CancellationTokenSource? _previewRequestCts;
    private CancellationTokenSource? _previewCts;
    private string? _sourcePath;
    private bool _canSave;
    private bool _isInitialized;

    public IconConverterWindow(AppSettingsService settingsService)
    {
        InitializeComponent();
        _isInitialized = true;
        UpdateBackgroundColorState();
        SetStatus(LocalizationService.Get("IconConverter_Ready"));
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
    }

    private async void BtnChoose_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = LocalizationService.Get("IconConverter_ChooseImage"),
            Filter = LocalizationService.Get("IconConverter_OpenFilter"),
            CheckFileExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog(this) == true)
        {
            await LoadSourceAsync(dialog.FileName);
        }
    }

    private async void Window_Drop(object sender, System.Windows.DragEventArgs e)
    {
        if (TryGetDroppedFile(e, out string? path))
        {
            await LoadSourceAsync(path!);
        }
    }

    private void Window_DragOver(object sender, System.Windows.DragEventArgs e)
    {
        e.Effects = TryGetDroppedFile(e, out _) ? System.Windows.DragDropEffects.Copy : System.Windows.DragDropEffects.None;
        e.Handled = true;
    }

    private async Task LoadSourceAsync(string path)
    {
        _sourcePath = path;
        _canSave = false;
        BtnSave.IsEnabled = false;
        TxtSource.Text = path;
        await QueuePreviewRefreshAsync(debounce: false);
    }

    private async Task QueuePreviewRefreshAsync(bool debounce)
    {
        if (!_isInitialized || string.IsNullOrWhiteSpace(_sourcePath))
        {
            return;
        }

        _previewRequestCts?.Cancel();
        _previewRequestCts?.Dispose();
        _previewRequestCts = new CancellationTokenSource();
        CancellationToken requestToken = _previewRequestCts.Token;

        try
        {
            if (debounce)
            {
                await Task.Delay(300, requestToken);
            }

            await RefreshPreviewAsync(requestToken);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task RefreshPreviewAsync(CancellationToken requestToken)
    {
        _previewCts?.Cancel();
        _previewCts?.Dispose();
        _previewCts = CancellationTokenSource.CreateLinkedTokenSource(requestToken);
        CancellationToken token = _previewCts.Token;

        try
        {
            SetStatus(LocalizationService.Get("IconConverter_Generating"));
            BtnSave.IsEnabled = false;
            IconConversionResult result = await _converterService.GeneratePreviewResultAsync(_sourcePath!, BuildOptions(), token);
            if (token.IsCancellationRequested)
            {
                return;
            }

            _canSave = true;
            ApplyPreviews(result.Previews);
            BtnSave.IsEnabled = true;
            string fileName = Path.GetFileName(_sourcePath!) ?? _sourcePath!;
            string summary = LocalizationService.Format("IconConverter_SourceFormat", fileName, result.SourceWidth, result.SourceHeight);
            SetStatus(result.Warnings.Count == 0 ? summary : $"{summary}  {string.Join(" ", result.Warnings)}");
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Logger.Log(ex);
            _canSave = false;
            BtnSave.IsEnabled = false;
            ClearPreviews();
            SetStatus(ex.Message);
        }
    }

    private IconConversionOptions BuildOptions()
    {
        return new IconConversionOptions
        {
            Sizes = GetSelectedSizes(),
            PaddingPercent = SldPadding.Value,
            BackgroundMode = RbSolid.IsChecked == true ? IconBackgroundMode.SolidColor : IconBackgroundMode.Transparent,
            BackgroundColor = TxtBackgroundColor.Text ?? "#000000",
            FitMode = RbFill.IsChecked == true ? IconFitMode.Fill : IconFitMode.Fit
        };
    }

    private IReadOnlyList<int> GetSelectedSizes()
    {
        var sizes = new List<int>();
        AddIfChecked(Chk16, 16);
        AddIfChecked(Chk20, 20);
        AddIfChecked(Chk24, 24);
        AddIfChecked(Chk32, 32);
        AddIfChecked(Chk40, 40);
        AddIfChecked(Chk48, 48);
        AddIfChecked(Chk64, 64);
        AddIfChecked(Chk128, 128);
        AddIfChecked(Chk256, 256);
        return sizes;

        void AddIfChecked(System.Windows.Controls.CheckBox checkBox, int size)
        {
            if (checkBox.IsChecked == true)
            {
                sizes.Add(size);
            }
        }
    }

    private void ApplyPreviews(IReadOnlyList<IconPreviewImage> previews)
    {
        SetPreview(ImgPreview16, previews, 16);
        SetPreview(ImgPreview32, previews, 32);
        SetPreview(ImgPreview48, previews, 48);
        SetPreview(ImgPreview256, previews, 256);
    }

    private static void SetPreview(System.Windows.Controls.Image image, IReadOnlyList<IconPreviewImage> previews, int size)
    {
        IconPreviewImage? preview = previews.FirstOrDefault(item => item.Size == size);
        image.Source = preview == null ? null : CreateBitmapImage(preview.PngBytes);
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

    private void ClearPreviews()
    {
        ImgPreview16.Source = null;
        ImgPreview32.Source = null;
        ImgPreview48.Source = null;
        ImgPreview256.Source = null;
    }

    private void Options_Changed(object sender, RoutedEventArgs e)
    {
        if (!_isInitialized)
        {
            return;
        }

        UpdateBackgroundColorState();
        TxtPadding.Text = $"{SldPadding.Value:0}%";
        if (!string.IsNullOrWhiteSpace(_sourcePath))
        {
            _canSave = false;
            BtnSave.IsEnabled = false;
            _ = QueuePreviewRefreshAsync(debounce: true);
        }
    }

    private void BackgroundColor_Changed(object sender, TextChangedEventArgs e)
    {
        if (!_isInitialized || RbSolid.IsChecked != true)
        {
            return;
        }

        Options_Changed(sender, e);
    }

    private void UpdateBackgroundColorState()
    {
        TxtBackgroundColor.IsEnabled = RbSolid.IsChecked == true;
    }

    private async void BtnPracticalPreset_Click(object sender, RoutedEventArgs e)
    {
        SetSizeChecks(new HashSet<int>(IconConversionOptions.DefaultSizes));
        await QueuePreviewRefreshAsync(debounce: false);
    }

    private async void BtnWindowsPreset_Click(object sender, RoutedEventArgs e)
    {
        SetSizeChecks(new HashSet<int>(IconConversionOptions.WindowsDpiSizes));
        await QueuePreviewRefreshAsync(debounce: false);
    }

    private void SetSizeChecks(IReadOnlySet<int> sizes)
    {
        Chk16.IsChecked = sizes.Contains(16);
        Chk20.IsChecked = sizes.Contains(20);
        Chk24.IsChecked = sizes.Contains(24);
        Chk32.IsChecked = sizes.Contains(32);
        Chk40.IsChecked = sizes.Contains(40);
        Chk48.IsChecked = sizes.Contains(48);
        Chk64.IsChecked = sizes.Contains(64);
        Chk128.IsChecked = sizes.Contains(128);
        Chk256.IsChecked = sizes.Contains(256);
    }

    private async void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        if (!_canSave || string.IsNullOrWhiteSpace(_sourcePath))
        {
            return;
        }

        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = LocalizationService.Get("IconConverter_SaveIco"),
            Filter = LocalizationService.Get("IconConverter_SaveFilter"),
            FileName = $"{Path.GetFileNameWithoutExtension(_sourcePath)}.ico",
            AddExtension = true,
            DefaultExt = ".ico",
            OverwritePrompt = false
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        if (File.Exists(dialog.FileName))
        {
            var confirm = new DarkDialog(LocalizationService.Format("IconConverter_OverwriteConfirm", dialog.FileName), isConfirm: true)
            {
                Owner = this
            };
            if (confirm.ShowDialog() != true)
            {
                return;
            }
        }

        try
        {
            BtnSave.IsEnabled = false;
            SetStatus(LocalizationService.Get("IconConverter_Generating"));
            IconConversionResult result = await _converterService.ConvertAsync(_sourcePath, BuildOptions());
            File.WriteAllBytes(dialog.FileName, result.IcoBytes);
            SetStatus(LocalizationService.Format("IconConverter_SavedFormat", dialog.FileName));
        }
        catch (Exception ex)
        {
            Logger.Log(ex);
            new DarkDialog(LocalizationService.Format("IconConverter_SaveFailed", ex.Message)) { Owner = this }.ShowDialog();
        }
        finally
        {
            BtnSave.IsEnabled = _canSave;
        }
    }

    private static bool TryGetDroppedFile(System.Windows.DragEventArgs e, out string? path)
    {
        path = null;
        if (!e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
        {
            return false;
        }

        var files = e.Data.GetData(System.Windows.DataFormats.FileDrop) as string[];
        if (files == null || files.Length != 1 || !File.Exists(files[0]))
        {
            return false;
        }

        path = files[0];
        return true;
    }

    private void SetStatus(string message)
    {
        TxtStatus.Text = message;
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
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
        _previewCts?.Cancel();
        _previewCts?.Dispose();
        _previewRequestCts?.Cancel();
        _previewRequestCts?.Dispose();
        base.OnClosed(e);
    }
}
