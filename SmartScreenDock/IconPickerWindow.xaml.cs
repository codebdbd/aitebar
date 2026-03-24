using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

using Button = System.Windows.Controls.Button;
using FontFamily = System.Windows.Media.FontFamily;
using Brushes = System.Windows.Media.Brushes;
using Cursors = System.Windows.Input.Cursors;

namespace SmartScreenDock
{
    public partial class IconPickerWindow : DarkWindow
    {
        public string SelectedIcon { get; private set; } = "";
        public string SelectedFont { get; private set; } = FontHelper.MaterialKey;

        private readonly List<(Button btn, string searchKey)> _allButtons = new();
        private string _activeFont = FontHelper.MaterialKey;
        
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
            SetActiveTab(FontHelper.MaterialKey);
        }

        private void BtnTabMaterial_Click(object sender, RoutedEventArgs e)
            => SetActiveTab(FontHelper.MaterialKey);

        private void BtnTabFluent_Click(object sender, RoutedEventArgs e)
            => SetActiveTab(FontHelper.FluentKey);

        private void BtnTabBrands_Click(object sender, RoutedEventArgs e)
            => SetActiveTab(FontHelper.BrandsKey);

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

            TxtSearchHint.Text = fontName == FontHelper.BrandsKey
                ? "Поиск по имени бренда (github, twitter) или коду"
                : "Поиск по коду, например: E871";

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

                // Для всех 3-х шрифтов иконки хранятся в Private Use Area (E000+)
                // Исключаем обычные буквы, цифры и служебные символы.
                var codes = glyphMap.Keys
                    .Where(c => glyphMap[c] != 0)
                    .Where(c => c >= 0xE000 && c <= 0x10FFFF)
                    .Where(c => c < 0xD800 || c > 0xDFFF) // Исключаем суррогатные пары напрямую
                    .OrderBy(c => c)
                    .ToArray();

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

                        string searchKey = BuildSearchKey(fontName, code);

                        var btn = CreateIconButton(btnStyle, symbol, fontFam, $"U+{code:X4}", searchKey, fontName);
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
            }
        }

        private static string BuildSearchKey(string fontName, int code)
        {
            var keyParts = new List<string> { $"{code:X4}".ToLowerInvariant() };
            if (fontName == FontHelper.BrandsKey && FontAwesomeNameAliases.TryGetValue(code, out var aliases))
            {
                keyParts.AddRange(aliases);
            }
            return string.Join(" ", keyParts);
        }

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
                Style = btnStyle, ToolTip = tooltip,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0), Cursor = Cursors.Hand
            };
            
            btn.Click += (s, e) =>
            {
                SelectedIcon = symbol;
                SelectedFont = fontSrcKey;
                this.DialogResult = true;
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
