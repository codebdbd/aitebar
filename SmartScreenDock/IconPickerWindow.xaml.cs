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
        public string SelectedFont { get; private set; } = "Segoe Fluent Icons";

        private readonly List<(Button btn, string searchKey)> _allButtons = new();
        private string _activeFont = "Segoe Fluent Icons";
        private static readonly Dictionary<int, string[]> FontAwesomeNameAliases = new()
        {
            // Все кодпоинты верифицированы по файлу шрифта Font Awesome 7 Brands-Regular-400.otf

            // Twitter / X
            [0xF099] = new[] { "twitter" },
            [0xE61B] = new[] { "twitter", "x", "x-twitter" },

            // Facebook / Meta
            [0xF09A] = new[] { "facebook", "meta" },

            // GitHub
            [0xF09B] = new[] { "github" },
            [0xF113] = new[] { "github-alt" },

            // GitLab
            [0xF296] = new[] { "gitlab" },

            // LinkedIn
            [0xF0E1] = new[] { "linkedin", "linkedin-in" },

            // Instagram
            [0xF16D] = new[] { "instagram" },

            // YouTube
            [0xF167] = new[] { "youtube" },

            // Google
            [0xF1A0] = new[] { "google" },

            // Apple
            [0xF179] = new[] { "apple", "ios" },

            // Windows / Microsoft
            [0xF17A] = new[] { "windows", "microsoft" },

            // WhatsApp
            [0xF232] = new[] { "whatsapp" },

            // Telegram
            [0xF2C6] = new[] { "telegram" },
            [0xF3FE] = new[] { "telegram" },

            // Discord
            [0xF392] = new[] { "discord" },

            // Docker
            [0xF395] = new[] { "docker" },

            // AWS
            [0xF375] = new[] { "aws", "amazon-web-services" },

            // Node.js
            [0xF3D3] = new[] { "node", "node-js" },
            [0xF419] = new[] { "node" },

            // Git
            [0xF841] = new[] { "git", "git-alt" },

            // Slack
            [0xF198] = new[] { "slack" },
            [0xF3EF] = new[] { "slack" },

            // Yandex
            [0xF413] = new[] { "yandex" },
            [0xF414] = new[] { "yandex-international" },

            // Twitch
            [0xF1E8] = new[] { "twitch" },

            // Reddit
            [0xF1A1] = new[] { "reddit" },
            [0xF281] = new[] { "reddit-alien" },

            // Spotify
            [0xF1BC] = new[] { "spotify" },

            // VK
            [0xF189] = new[] { "vk", "vkontakte" },

            // Odnoklassniki
            [0xF263] = new[] { "odnoklassniki", "ok" },

            // Figma
            [0xF799] = new[] { "figma" },

            // Notion
            [0xE7D9] = new[] { "notion" },

            // Bluesky
            [0xE671] = new[] { "bluesky" },

            // Threads
            [0xE618] = new[] { "threads" },

            // TikTok
            [0xE07B] = new[] { "tiktok" },
        };

        public IconPickerWindow()
        {
            InitializeComponent();
            SetActiveTab("Segoe Fluent Icons");
        }

        private void BtnTabSegoe_Click(object sender, RoutedEventArgs e)
            => SetActiveTab("Segoe Fluent Icons");

        private void BtnTabFontAwesome_Click(object sender, RoutedEventArgs e)
            => SetActiveTab("Font Awesome Brands");

        private void SetActiveTab(string fontName)
        {
            _activeFont = fontName;
            TxtSearch.Text = "";

            BtnTabSegoe.Foreground = fontName == "Segoe Fluent Icons"
                ? Brushes.White : new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xAA, 0xAA, 0xAA));
            BtnTabFontAwesome.Foreground = fontName == "Font Awesome Brands"
                ? Brushes.White : new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xAA, 0xAA, 0xAA));

            TxtSearchHint.Text = fontName == "Segoe Fluent Icons"
                ? "Поиск по коду, например: E721"
                : "Поиск по имени, например: github, twitter, youtube";

            LoadIcons(fontName);
        }

        private async void LoadIcons(string fontName)
        {
            try
            {
                IconPanel.Children.Clear();
                _allButtons.Clear();

                Style btnStyle = (Style)FindResource("IconBtnStyle");

                if (fontName == "Segoe Fluent Icons")
                    await LoadSegoeIcons(btnStyle);
                else
                    await LoadFontAwesomeIcons(btnStyle);
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
        }

        private async System.Threading.Tasks.Task LoadSegoeIcons(Style btnStyle)
        {
            int[] codes = new[]
            {
                Enumerable.Range(0xE700, 0x0200),
                Enumerable.Range(0xE900, 0x0200),
                Enumerable.Range(0xEB00, 0x0100),
                Enumerable.Range(0xEC00, 0x0100),
                Enumerable.Range(0xED00, 0x0100),
                Enumerable.Range(0xEE00, 0x0100),
                Enumerable.Range(0xEF00, 0x0100),
                Enumerable.Range(0xF000, 0x0500),
            }
            .SelectMany(x => x)
            .ToArray();

            var segoeFont = new FontFamily("Segoe Fluent Icons");
            GlyphTypeface? glyphTypeface = null;
            foreach (var tf in segoeFont.GetTypefaces())
            {
                if (tf.TryGetGlyphTypeface(out var gt)) { glyphTypeface = gt; break; }
            }

            const int batchSize = 50;
            for (int batch = 0; batch < codes.Length; batch += batchSize)
            {
                if (_activeFont != "Segoe Fluent Icons") return;

                int end = Math.Min(batch + batchSize, codes.Length);
                for (int i = batch; i < end; i++)
                {
                    int code = codes[i];
                    if (glyphTypeface == null || !glyphTypeface.CharacterToGlyphMap.ContainsKey(code))
                        continue;

                    string symbol = char.ConvertFromUtf32(code);
                    string searchKey = $"{code:X4}".ToLower();

                    var btn = CreateIconButton(btnStyle, symbol, segoeFont,
                        $"U+{code:X4}", searchKey);
                    IconPanel.Children.Add(btn);
                    _allButtons.Add((btn, searchKey));
                }
                await Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.Background);
            }
        }

        private async System.Threading.Tasks.Task LoadFontAwesomeIcons(Style btnStyle)
        {
            var faFont = new FontFamily(
                new Uri("pack://application:,,,/"),
                "./Resources/#Font Awesome 7 Brands Regular");
            GlyphTypeface? glyphTypeface = null;
            foreach (var tf in faFont.GetTypefaces())
            {
                if (tf.TryGetGlyphTypeface(out var gt)) { glyphTypeface = gt; break; }
            }

            if (glyphTypeface == null) return;

            var glyphMap = glyphTypeface.CharacterToGlyphMap;

            const int batchSize = 50;
            // Берём все кодпоинты из шрифта, кроме .notdef (glyph index 0).
            // FA 7 Brands размещает иконки как в F000–FFFF, так и в E000–EFFF (PUA),
            // поэтому фильтр по > 0xF000 неверен.
            // Иконки брендов лежат только в Private Use Area (U+E000+).
            // Кодпоинты ниже (0x20–0x7F и т.д.) — обычные буквы/цифры, не иконки.
            var codes = glyphMap.Keys
                .Where(c => glyphMap[c] != 0)
                .Where(c => c >= 0xE000 && c <= 0x10FFFF)
                .Where(c => c < 0xD800 || c > 0xDFFF)
                .OrderBy(c => c)
                .ToArray();

            for (int batch = 0; batch < codes.Length; batch += batchSize)
            {
                if (_activeFont != "Font Awesome Brands") return;

                int end = Math.Min(batch + batchSize, codes.Length);
                for (int i = batch; i < end; i++)
                {
                    int code = codes[i];
                    string symbol;
                    try { symbol = char.ConvertFromUtf32(code); }
                    catch { continue; }
                    string searchKey = BuildFontAwesomeSearchKey(code);

                    var btn = CreateIconButton(btnStyle, symbol, faFont, $"U+{code:X4}", searchKey);
                    IconPanel.Children.Add(btn);
                    _allButtons.Add((btn, searchKey));
                }
                await Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.Background);
            }
        }

        private Button CreateIconButton(Style btnStyle, string symbol, FontFamily fontFamily,
            string tooltip, string searchKey)
        {
            // TextBlock используется вместо Content = symbol, чтобы корректно отображать
            // суррогатные пары (кодпоинты > U+FFFF): ContentPresenter показывает только
            // первый char строки, TextBlock — полный глиф.
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
                SelectedFont = _activeFont == "Segoe Fluent Icons"
                    ? "Segoe Fluent Icons"
                    : "pack://application:,,,/Resources/#Font Awesome 7 Brands Regular";
                this.DialogResult = true;
            };
            return btn;
        }

        private static string BuildFontAwesomeSearchKey(int code)
        {
            var keyParts = new List<string> { $"{code:X4}".ToLowerInvariant() };
            if (FontAwesomeNameAliases.TryGetValue(code, out var aliases))
                keyParts.AddRange(aliases);
            return string.Join(" ", keyParts);
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
                btn.Visibility = searchKey.Contains(query) ? Visibility.Visible : Visibility.Collapsed;
        }
    }
}
