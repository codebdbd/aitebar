using System;
using System.IO;
using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;

namespace AiteBar.IptvPlayer;

[SupportedOSPlatform("windows6.1")]
public partial class IptvPlaylistDialog : DarkWindow
{
    private readonly IptvPlaylistSource _source;
    private readonly IptvPlayerStore _store;
    private readonly IptvPlaylistService _service;
    private string? _unmaskedSecretLocation;
    private string? _displayedSecretLocation;

    public IptvPlaylistSource ResultSource { get; private set; }
    public string? PendingSecretLocation { get; private set; }

    internal IptvPlaylistDialog(
        IptvPlaylistSource? existingSource,
        IptvPlayerStore store,
        IptvPlaylistService service,
        string? draftSecretLocation = null)
    {
        InitializeComponent();

        _store = store ?? throw new ArgumentNullException(nameof(store));
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _unmaskedSecretLocation = draftSecretLocation;

        if (existingSource != null)
        {
            _source = existingSource.Clone();
        }
        else
        {
            _source = new IptvPlaylistSource
            {
                Id = Guid.NewGuid().ToString("N"),
                SortOrder = 0
            };
        }

        ResultSource = _source;
        ApplyLocalization();
        PopulateForm();
    }

    private void ApplyLocalization()
    {
        Title = LocalizationService.Get("Iptv_DialogTitle") ?? "Плейлист IPTV";
        LblName.Text = LocalizationService.Get("Iptv_PlaylistName") ?? "Название плейлиста";
        LblKind.Text = LocalizationService.Get("Iptv_SourceKind") ?? "Тип источника";
        LblLocation.Text = LocalizationService.Get("Iptv_Location") ?? "Ссылка или путь";
        LblInlineContent.Text = LocalizationService.Get("Iptv_InlineContent") ?? "Содержимое M3U";
        BtnBrowse.Content = LocalizationService.Get("Common_Browse") ?? "Обзор...";
        ChkAutoFallbackVariants.Content = LocalizationService.Get("Iptv_AutoFallbackVariants") ?? "Автоматически группировать варианты качества";
        ChkCopyLocalFileToStore.Content = LocalizationService.Get("Iptv_CopyLocalFileToStore") ?? "Копировать локальный файл в профиль приложения";
        ChkIsEnabled.Content = LocalizationService.Get("Iptv_PlaylistEnabled") ?? "Плейлист включен";
        BtnCheck.Content = LocalizationService.Get("Iptv_CheckButton") ?? "Проверить";
        TxtCheckResult.Text = LocalizationService.Get("Iptv_CheckHint") ?? "Нажмите 'Проверить' для валидации потока";
        BtnSave.Content = LocalizationService.Get("Common_Save") ?? "Сохранить";
        BtnCancel.Content = LocalizationService.Get("Common_Cancel") ?? "Отмена";
    }

    private void PopulateForm()
    {
        TxtName.Text = _source.Name;

        int selectedIndex = _source.Kind switch
        {
            IptvSourceKind.Url => 0,
            IptvSourceKind.LocalFile => 1,
            IptvSourceKind.InlineText => 2,
            IptvSourceKind.SingleStream => 3,
            _ => 0
        };
        CmbKind.SelectedIndex = selectedIndex;

        string location = _source.Location;
        string? secret = _unmaskedSecretLocation ?? (IptvPlaylistParser.IsMaskedSensitiveUrl(location)
            ? _store.ReadSecretLocation(_source.SourceStorageId)
            : null);
        if (!string.IsNullOrWhiteSpace(secret))
        {
            _unmaskedSecretLocation = secret;
            location = IptvPlaylistParser.MaskSensitiveUrl(secret);
            _displayedSecretLocation = location;
        }

        TxtLocation.Text = location;
        TxtInlineContent.Text = _source.InlineContent ?? (_source.Kind == IptvSourceKind.InlineText
            ? _store.ReadSourceFileContent(_source.SourceStorageId)
            : null) ?? string.Empty;
        ChkAutoFallbackVariants.IsChecked = _source.AutoFallbackVariants;
        ChkCopyLocalFileToStore.IsChecked = _source.CopyLocalFileToStore;
        ChkIsEnabled.IsChecked = _source.IsEnabled;

        UpdateControlVisibility();
    }

    private void CmbKind_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateControlVisibility();
    }

    private void UpdateControlVisibility()
    {
        if (CmbKind == null) return;

        var selectedItem = CmbKind.SelectedItem as ComboBoxItem;
        string kindTag = selectedItem?.Tag?.ToString() ?? "Url";

        switch (kindTag)
        {
            case "Url":
                PnlLocation.Visibility = Visibility.Visible;
                BtnBrowse.Visibility = Visibility.Collapsed;
                PnlInlineText.Visibility = Visibility.Collapsed;
                ChkCopyLocalFileToStore.Visibility = Visibility.Collapsed;
                ChkAutoFallbackVariants.Visibility = Visibility.Visible;
                LblLocation.Text = LocalizationService.Get("Iptv_UrlLabel") ?? "URL плейлиста (http / https)";
                break;

            case "LocalFile":
                PnlLocation.Visibility = Visibility.Visible;
                BtnBrowse.Visibility = Visibility.Visible;
                PnlInlineText.Visibility = Visibility.Collapsed;
                ChkCopyLocalFileToStore.Visibility = Visibility.Visible;
                ChkAutoFallbackVariants.Visibility = Visibility.Visible;
                LblLocation.Text = LocalizationService.Get("Iptv_FilePathLabel") ?? "Путь к файлу (.m3u, .m3u8)";
                break;

            case "InlineText":
                PnlLocation.Visibility = Visibility.Collapsed;
                PnlInlineText.Visibility = Visibility.Visible;
                ChkCopyLocalFileToStore.Visibility = Visibility.Collapsed;
                ChkAutoFallbackVariants.Visibility = Visibility.Visible;
                break;

            case "SingleStream":
                PnlLocation.Visibility = Visibility.Visible;
                BtnBrowse.Visibility = Visibility.Collapsed;
                PnlInlineText.Visibility = Visibility.Collapsed;
                ChkCopyLocalFileToStore.Visibility = Visibility.Collapsed;
                ChkAutoFallbackVariants.Visibility = Visibility.Collapsed;
                LblLocation.Text = LocalizationService.Get("Iptv_StreamUrlLabel") ?? "URL медиа-потока";
                break;
        }
    }

    private void BtnBrowse_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "M3U Playlists (*.m3u;*.m3u8)|*.m3u;*.m3u8|All Files (*.*)|*.*",
            Title = LocalizationService.Get("Iptv_SelectPlaylistFile") ?? "Выберите файл плейлиста"
        };

        if (dlg.ShowDialog(this) == true)
        {
            TxtLocation.Text = dlg.FileName;
            if (string.IsNullOrWhiteSpace(TxtName.Text))
            {
                TxtName.Text = Path.GetFileNameWithoutExtension(dlg.FileName);
            }
        }
    }

    private async void BtnCheck_Click(object sender, RoutedEventArgs e)
    {
        BtnCheck.IsEnabled = false;
        TxtCheckResult.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(200, 200, 200));
        TxtCheckResult.Text = LocalizationService.Get("Iptv_Checking") ?? "Проверка источника...";

        try
        {
            var probe = BuildSourceFromForm();
            var checkResult = await _service.CheckSourceAsync(probe, probe.Location);

            if (checkResult.IsSuccess)
            {
                TxtCheckResult.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(70, 200, 70));
                string sample = checkResult.FirstChannels.Count > 0 ? string.Join(", ", checkResult.FirstChannels.Take(3)) : string.Empty;
                TxtCheckResult.Text = $"OK: {checkResult.TotalCount} кан. ({checkResult.ElapsedMs} ms) [{sample}]";
            }
            else
            {
                TxtCheckResult.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(240, 80, 80));
                TxtCheckResult.Text = $"Ошибка: {checkResult.ErrorMessage}";
            }
        }
        catch (Exception ex)
        {
            TxtCheckResult.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(240, 80, 80));
            TxtCheckResult.Text = $"Ошибка: {ex.Message}";
        }
        finally
        {
            BtnCheck.IsEnabled = true;
        }
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        string name = TxtName.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            System.Windows.MessageBox.Show(this,
                LocalizationService.Get("Iptv_ErrorEmptyName") ?? "Укажите название плейлиста.",
                LocalizationService.Get("Common_Error") ?? "Ошибка",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var source = BuildSourceFromForm();
        if ((source.Kind is IptvSourceKind.Url or IptvSourceKind.SingleStream) &&
            IptvPlaylistParser.IsSensitiveUrl(source.Location) && !IptvPlaylistParser.IsMaskedSensitiveUrl(source.Location))
        {
            PendingSecretLocation = source.Location;
            source.Location = IptvPlaylistParser.MaskSensitiveUrl(source.Location);
        }
        ResultSource = source;
        DialogResult = true;
        Close();
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private IptvPlaylistSource BuildSourceFromForm()
    {
        var selectedItem = CmbKind.SelectedItem as ComboBoxItem;
        string kindTag = selectedItem?.Tag?.ToString() ?? "Url";
        var kind = kindTag switch
        {
            "LocalFile" => IptvSourceKind.LocalFile,
            "InlineText" => IptvSourceKind.InlineText,
            "SingleStream" => IptvSourceKind.SingleStream,
            _ => IptvSourceKind.Url
        };

        string locationText = TxtLocation.Text.Trim();
        string effectiveLocation = (kind is IptvSourceKind.Url or IptvSourceKind.SingleStream) && locationText == _displayedSecretLocation
            ? _unmaskedSecretLocation ?? locationText
            : locationText;

        return new IptvPlaylistSource
        {
            Id = _source.Id,
            Name = TxtName.Text.Trim(),
            Kind = kind,
            Location = kind == IptvSourceKind.InlineText ? string.Empty : effectiveLocation,
            InlineContent = kind == IptvSourceKind.InlineText ? TxtInlineContent.Text : null,
            ContentRevision = _source.ContentRevision,
            AutoFallbackVariants = ChkAutoFallbackVariants.IsChecked == true,
            CopyLocalFileToStore = ChkCopyLocalFileToStore.IsChecked == true,
            IsEnabled = ChkIsEnabled.IsChecked == true,
            SortOrder = _source.SortOrder,
            CreatedAtUtc = _source.CreatedAtUtc,
            LastRefreshedUtc = _source.LastRefreshedUtc,
            LastStatus = _source.LastStatus,
            LastErrorMessage = _source.LastErrorMessage,
            ChannelCount = _source.ChannelCount
        };
    }
}
