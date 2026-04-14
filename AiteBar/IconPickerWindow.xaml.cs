using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Text.Json;

using Button = System.Windows.Controls.Button;
using FontFamily = System.Windows.Media.FontFamily;
using Brushes = System.Windows.Media.Brushes;
using Cursors = System.Windows.Input.Cursors;

namespace AiteBar
{
    public partial class IconPickerWindow : DarkWindow
    {
        public string SelectedIcon { get; private set; } = "";
        public string SelectedFont { get; private set; } = FontHelper.FluentKey;
        public string SelectedImagePath { get; private set; } = "";

        private readonly List<(Button btn, string searchKey)> _allButtons = new();
        private string _activeFont = FontHelper.FluentKey;
        private static Dictionary<int, string>? _fluentMap;
        private static Dictionary<int, string>? _materialMap;
        
        // Маппинг для Font Awesome Brands из старого кода остаётся:
        private static readonly Dictionary<int, string[]> FontAwesomeNameAliases = new()
        {
            [0xF099] = new[] { "twitter" },
            [0xE61B] = new[] { "twitter", "x", "x-twitter" },
            [0xF09A] = new[] { "facebook", "meta" },
            [0xF09B] = new[] { "github" },
            [0xF113] = new[] { "github-alt" },
            [0xF296] = new[] { "gitlab" },
            [0xF0E1] = new[] { "linkedin", "linkedin-in" },
            [0xF16D] = new[] { "instagram" },
            [0xF167] = new[] { "youtube" },
            [0xF1A0] = new[] { "google" },
            [0xF179] = new[] { "apple", "ios" },
            [0xF17A] = new[] { "windows", "microsoft" },
            [0xF232] = new[] { "whatsapp" },
            [0xF2C6] = new[] { "telegram" },
            [0xF3FE] = new[] { "telegram" },
            [0xF392] = new[] { "discord" },
            [0xF395] = new[] { "docker" },
            [0xF375] = new[] { "aws", "amazon-web-services" },
            [0xF3D3] = new[] { "node", "node-js" },
            [0xF419] = new[] { "node" },
            [0xF841] = new[] { "git", "git-alt" },
            [0xF198] = new[] { "slack" },
            [0xF3EF] = new[] { "slack" },
            [0xF413] = new[] { "yandex" },
            [0xF414] = new[] { "yandex-international" },
            [0xF1E8] = new[] { "twitch" },
            [0xF1A1] = new[] { "reddit" },
            [0xF281] = new[] { "reddit-alien" },
            [0xF1BC] = new[] { "spotify" },
            [0xF189] = new[] { "vk", "vkontakte" },
            [0xF263] = new[] { "odnoklassniki", "ok" },
            [0xF799] = new[] { "figma" },
            [0xE7D9] = new[] { "notion" },
            [0xE671] = new[] { "bluesky" },
            [0xE618] = new[] { "threads" },
            [0xE07B] = new[] { "tiktok" }
        };

        public IconPickerWindow()
        {
            InitializeComponent();
            SetActiveTab(FontHelper.FluentKey);
        }

        private void BtnTabMaterial_Click(object sender, RoutedEventArgs e)
            => SetActiveTab(FontHelper.MaterialKey);

        private void BtnTabFluent_Click(object sender, RoutedEventArgs e)
            => SetActiveTab(FontHelper.FluentKey);

        private void BtnTabBrands_Click(object sender, RoutedEventArgs e)
            => SetActiveTab(FontHelper.BrandsKey);

        private void BtnTabCustom_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Изображения|*.png;*.jpg;*.jpeg;*.bmp;*.ico",
                Title = "Выберите иконку"
            };
            if (dlg.ShowDialog() == true)
            {
                try
                {
                    string appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Codebdbd", "Aite Bar", "Icons");
                    if (!Directory.Exists(appData)) Directory.CreateDirectory(appData);
                    
                    string fileName = Guid.NewGuid().ToString() + Path.GetExtension(dlg.FileName);
                    string destPath = Path.Combine(appData, fileName);
                    File.Copy(dlg.FileName, destPath);
                    
                    SelectedIcon = "";
                    SelectedFont = "";
                    SelectedImagePath = destPath;
                    this.DialogResult = true;
                    this.Close();
                }
                catch (Exception ex)
                {
                    Logger.Log(ex);
                    new DarkDialog("Ошибка при копировании файла.") { Owner = this }.ShowDialog();
                }
            }
        }

        private void SetActiveTab(string fontName)
        {
            _activeFont = fontName;
            TxtSearch.Text = "";

            BtnTabMaterial.Foreground = fontName == FontHelper.MaterialKey
                ? Brushes.White : new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xAA, 0xAA, 0xAA));
            BtnTabFluent.Foreground   = fontName == FontHelper.FluentKey
                ? Brushes.White : new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xAA, 0xAA, 0xAA));
            BtnTabBrands.Foreground   = fontName == FontHelper.BrandsKey
                ? Brushes.White : new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xAA, 0xAA, 0xAA));
            BtnTabCustom.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xAA, 0xAA, 0xAA));

            TxtSearchHint.Text = fontName switch
            {
                FontHelper.BrandsKey   => "Поиск по имени бренда (github, twitter) или коду",
                FontHelper.FluentKey   => "Поиск по имени иконки (add, home, search) или коду",
                FontHelper.MaterialKey => "Поиск по имени иконки (add, home, search) или коду",
                _ => "Поиск по коду"
            };

            LoadIcons(fontName);
        }

        private async void LoadIcons(string fontName)
        {
            try
            {
                IconPanel.Children.Clear();
                _allButtons.Clear();

                Style btnStyle = (Style)FindResource("IconBtnStyle");
                var fontFam = FontHelper.Resolve(fontName);

                GlyphTypeface? glyphTypeface = null;
                foreach (var tf in fontFam.GetTypefaces())
                {
                    if (tf.TryGetGlyphTypeface(out var gt)) { glyphTypeface = gt; break; }
                }

                if (glyphTypeface == null) return;
                
                var glyphMap = glyphTypeface.CharacterToGlyphMap;
                var namedIcons = GetNamedIcons(fontName);
                var codes = GetDisplayCodes(fontName, glyphMap, namedIcons);

                const int batchSize = 100;
                for (int batch = 0; batch < codes.Length; batch += batchSize)
                {
                    if (_activeFont != fontName) return; // Пользователь сменил вкладку

                    int end = Math.Min(batch + batchSize, codes.Length);
                    for (int i = batch; i < end; i++)
                    {
                        int code = codes[i];
                        string symbol;
                        try { symbol = char.ConvertFromUtf32(code); }
                        catch { continue; }

                        string? iconName = null;
                        namedIcons?.TryGetValue(code, out iconName);
                        string searchKey = BuildSearchKey(fontName, code, iconName);
                        string tooltip = iconName != null ? $"{iconName}  U+{code:X4}" : $"U+{code:X4}";

                        var btn = CreateIconButton(btnStyle, symbol, fontFam, tooltip, searchKey, fontName);
                        IconPanel.Children.Add(btn);
                        _allButtons.Add((btn, searchKey));
                    }
                    
                    // Даём UI отрисоваться перед загрузкой следующей пачки
                    await Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.Background);
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
                new DarkDialog($"Не удалось загрузить каталог иконок:\n{ex.Message}") { Owner = this }.ShowDialog();
            }
        }

        private static int[] GetDisplayCodes(string fontName, IDictionary<int, ushort> glyphMap, Dictionary<int, string>? namedIcons)
        {
            if (namedIcons != null)
            {
                return namedIcons.Keys
                    .Where(code => glyphMap.TryGetValue(code, out var glyphIndex) && glyphIndex != 0)
                    .OrderBy(code => code)
                    .ToArray();
            }

            return glyphMap.Keys
                .Where(code => glyphMap[code] != 0)
                .Where(code => code >= 0xE000 && code <= 0x10FFFF)
                .Where(code => code < 0xD800 || code > 0xDFFF)
                .OrderBy(code => code)
                .ToArray();
        }

        private static Dictionary<int, string>? GetNamedIcons(string fontName) => fontName switch
        {
            FontHelper.FluentKey => LoadFluentMap(),
            FontHelper.MaterialKey => LoadMaterialMap(),
            _ => null
        };

        private static string BuildSearchKey(string fontName, int code, string? iconName)
        {
            var keyParts = new List<string> { $"{code:X4}".ToLowerInvariant() };
            if (!string.IsNullOrWhiteSpace(iconName))
                keyParts.Add(iconName.ToLowerInvariant());
            if (fontName == FontHelper.BrandsKey && FontAwesomeNameAliases.TryGetValue(code, out var aliases))
            {
                keyParts.AddRange(aliases);
            }
            return string.Join(" ", keyParts);
        }

        private static Dictionary<int, string> LoadFluentMap()
        {
            if (_fluentMap != null) return _fluentMap;

            using var stream = OpenResourceStream(FontHelper.FluentCodepointsResource);
            using var reader = new StreamReader(stream);
            var json = reader.ReadToEnd();
            var raw = JsonSerializer.Deserialize<Dictionary<string, int>>(json)
                ?? throw new InvalidOperationException("Failed to parse Fluent icon metadata.");

            _fluentMap = raw
                .Where(kv => kv.Key.EndsWith("_24_regular", StringComparison.Ordinal))
                .GroupBy(kv => kv.Value)
                .Select(group => group.OrderBy(kv => kv.Key, StringComparer.Ordinal).First())
                .ToDictionary(kv => kv.Value, kv => FormatFluentName(kv.Key));

            return _fluentMap;
        }

        private static Dictionary<int, string> LoadMaterialMap()
        {
            if (_materialMap != null) return _materialMap;

            using var stream = OpenResourceStream(FontHelper.MaterialCodepointsResource);
            using var reader = new StreamReader(stream);
            _materialMap = reader
                .ReadToEnd()
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                .Where(parts => parts.Length == 2)
                .GroupBy(parts => Convert.ToInt32(parts[1], 16))
                .Select(group => group.First())
                .ToDictionary(
                    parts => Convert.ToInt32(parts[1], 16),
                    parts => parts[0].Replace("_", " "));

            return _materialMap;
        }

        private static Stream OpenResourceStream(string packUri)
        {
            var resource = System.Windows.Application.GetResourceStream(new Uri(packUri, UriKind.Absolute));
            if (resource?.Stream == null)
                throw new FileNotFoundException($"Resource not found: {packUri}");
            return resource.Stream;
        }

        private static string FormatFluentName(string rawName)
            => rawName
                .Replace("ic_fluent_", "", StringComparison.Ordinal)
                .Replace("_24_regular", "", StringComparison.Ordinal)
                .Replace("_", " ");

        private Button CreateIconButton(Style btnStyle, string symbol, FontFamily fontFamily,
            string tooltip, string searchKey, string fontSrcKey)
        {
            // TextBlock используется вместо просто Content = symbol,
            // чтобы корректно отображать символы > U+FFFF
            var tb = new TextBlock
            {
                Text = symbol,
                FontFamily = fontFamily,
                FontSize = 24,
                Foreground = Brushes.White,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                VerticalAlignment = System.Windows.VerticalAlignment.Center
            };
            
            var btn = new Button
            {
                Content = tb,
                Width = 46, Height = 46, Margin = new Thickness(2),
                Style = btnStyle, ToolTip = tooltip
            };
            
            btn.Click += (s, e) =>
            {
                SelectedIcon = symbol;
                SelectedFont = fontSrcKey;
                SelectedImagePath = ""; // Сбрасываем путь к картинке, если выбрана шрифтовая иконка
                this.DialogResult = true;
                this.Close();
            };
            return btn;
        }

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            string query = TxtSearch.Text.Trim().ToLowerInvariant();

            if (string.IsNullOrEmpty(query))
            {
                foreach (var (btn, _) in _allButtons)
                    btn.Visibility = Visibility.Visible;
                return;
            }

            foreach (var (btn, searchKey) in _allButtons)
            {
                btn.Visibility = searchKey.Contains(query) ? Visibility.Visible : Visibility.Collapsed;
            }
        }
    }
}
