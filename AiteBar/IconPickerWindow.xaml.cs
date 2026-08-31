namespace AiteBar
{
    public partial class IconPickerWindow : DarkWindow
    {
        public string SelectedIcon { get; private set; } = "";
        public string SelectedFont { get; private set; } = FontHelper.FluentKey;
        public string SelectedImagePath { get; private set; } = "";

        private readonly List<(Button btn, string searchKey)> _allButtons = [];
        private string _activeFont = FontHelper.FluentKey;
        private static readonly IconCatalogService Catalog = new(OpenResourceStream);

        public IconPickerWindow()
        {
            InitializeComponent();
            SetActiveTab(FontHelper.FluentKey);
            Loaded += (_, _) => TxtSearch.Focus();
        }

        internal static void WarmupCatalogMetadata()
        {
            Catalog.Warmup();
        }

        private void BtnTabFluent_Click(object sender, RoutedEventArgs e)
            => SetActiveTab(FontHelper.FluentKey);

        private void BtnTabBrands_Click(object sender, RoutedEventArgs e)
            => SetActiveTab(FontHelper.BrandsKey);

        private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                DialogResult = false;
                Close();
                e.Handled = true;
            }
        }

        private void SetActiveTab(string fontName)
        {
            _activeFont = fontName;
            TxtSearch.Text = "";

            BtnTabFluent.Foreground = fontName == FontHelper.FluentKey
                ? Brushes.White : new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xAA, 0xAA, 0xAA));
            BtnTabBrands.Foreground = fontName == FontHelper.BrandsKey
                ? Brushes.White : new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xAA, 0xAA, 0xAA));

            BtnTabFluent.BorderBrush = fontName == FontHelper.FluentKey ? (Brush)FindResource("AccentColor") : Brushes.Transparent;
            BtnTabBrands.BorderBrush = fontName == FontHelper.BrandsKey ? (Brush)FindResource("AccentColor") : Brushes.Transparent;

            UpdateSearchHint();

            LoadIcons(fontName);
        }

        private void UpdateSearchHint()
        {
            TxtSearchHint.Text = _activeFont switch
            {
                FontHelper.BrandsKey => LocalizationService.Get("IconPicker_SearchBrandsHint"),
                FontHelper.FluentKey => LocalizationService.Get("IconPicker_SearchIconsHint"),
                _ => LocalizationService.Get("IconPicker_SearchCodeHint")
            };
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
                IReadOnlyList<IconCatalogEntry> entries = Catalog.BuildEntries(fontName, glyphMap);

                const int batchSize = 100;
                for (int batch = 0; batch < entries.Count; batch += batchSize)
                {
                    if (_activeFont != fontName) return; // Пользователь сменил вкладку

                    int end = Math.Min(batch + batchSize, entries.Count);
                    for (int i = batch; i < end; i++)
                    {
                        IconCatalogEntry entry = entries[i];
                        var btn = CreateIconButton(
                            btnStyle,
                            entry.Symbol,
                            fontFam,
                            entry.Tooltip,
                            fontName);
                        IconPanel.Children.Add(btn);
                        _allButtons.Add((btn, entry.SearchKey));
                    }

                    // Даём UI отрисоваться перед загрузкой следующей пачки
                    await Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.Background);
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
                new DarkDialog(LocalizationService.Format("IconPicker_LoadFailed", ex.Message)) { Owner = this }.ShowDialog();
            }
        }

        private static Stream OpenResourceStream(string packUri)
        {
            var resource = System.Windows.Application.GetResourceStream(new Uri(packUri, UriKind.Absolute));
            if (resource?.Stream == null)
                throw new FileNotFoundException(LocalizationService.Format("IconPicker_ResourceNotFound", packUri));
            return resource.Stream;
        }

        private Button CreateIconButton(Style btnStyle, string symbol, FontFamily fontFamily,
            string tooltip, string fontSrcKey)
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
                Width = 46,
                Height = 46,
                Margin = new Thickness(2),
                Style = btnStyle,
                ToolTip = tooltip
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

        protected override void OnLocalizationChanged()
        {
            UpdateSearchHint();
        }
    }
}
