using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Resources;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Markup;
using System.Windows.Media;

namespace AiteBar
{
    public static class LocalizationService
    {
        public const string AutoCulture = "auto";
        public const string NeutralCulture = "en";

        private static readonly string[] SupportedCultures = [AutoCulture, "en", "de", "uk", "ru"];
        private static readonly ResourceManager Resources = new("AiteBar.Resources.Strings", Assembly.GetExecutingAssembly());
        private static readonly CultureInfo English = CultureInfo.GetCultureInfo(NeutralCulture);
        private static readonly CultureInfo OperatingSystemCulture = CultureInfo.CurrentUICulture;

        public static LocalizedStringProvider Strings { get; } = new();
        public static event EventHandler? CultureChanged;

        public static string NormalizeCultureName(string? cultureName)
        {
            if (string.IsNullOrWhiteSpace(cultureName))
            {
                return AutoCulture;
            }

            string value = cultureName.Trim();
            if (string.Equals(value, AutoCulture, StringComparison.OrdinalIgnoreCase))
            {
                return AutoCulture;
            }

            try
            {
                string language = CultureInfo.GetCultureInfo(value).TwoLetterISOLanguageName;
                return SupportedCultures.Contains(language, StringComparer.OrdinalIgnoreCase)
                    ? language
                    : AutoCulture;
            }
            catch (CultureNotFoundException)
            {
                return AutoCulture;
            }
        }

        public static CultureInfo ResolveCulture(string? savedCulture)
        {
            string normalized = NormalizeCultureName(savedCulture);
            if (normalized == AutoCulture)
            {
                string osLanguage = OperatingSystemCulture.TwoLetterISOLanguageName;
                normalized = SupportedCultures.Contains(osLanguage, StringComparer.OrdinalIgnoreCase)
                    ? osLanguage
                    : NeutralCulture;
            }

            return CultureInfo.GetCultureInfo(normalized);
        }

        public static void ApplyCulture(string? savedCulture)
        {
            CultureInfo culture = ResolveCulture(savedCulture);
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;
            CultureInfo.DefaultThreadCurrentCulture = culture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;
            Strings.Refresh();
            CultureChanged?.Invoke(null, EventArgs.Empty);
        }

        public static void RefreshLocalizedBindings(DependencyObject root)
        {
            var visited = new HashSet<DependencyObject>();
            RefreshLocalizedBindings(root, visited);
        }

        private static void RefreshLocalizedBindings(DependencyObject root, HashSet<DependencyObject> visited)
        {
            if (!visited.Add(root))
            {
                return;
            }

            if (root is FrameworkElement frameworkElement)
            {
                UpdateBinding(frameworkElement, FrameworkElement.ToolTipProperty);
            }

            switch (root)
            {
                case Window window:
                    UpdateBinding(window, Window.TitleProperty);
                    break;
                case TextBlock textBlock:
                    UpdateBinding(textBlock, TextBlock.TextProperty);
                    break;
                case HeaderedContentControl headeredContentControl:
                    UpdateBinding(headeredContentControl, HeaderedContentControl.HeaderProperty);
                    UpdateBinding(headeredContentControl, ContentControl.ContentProperty);
                    break;
                case ContentControl contentControl:
                    UpdateBinding(contentControl, ContentControl.ContentProperty);
                    break;
            }

            if (root is Visual or System.Windows.Media.Media3D.Visual3D)
            {
                int visualChildren = VisualTreeHelper.GetChildrenCount(root);
                for (int i = 0; i < visualChildren; i++)
                {
                    RefreshLocalizedBindings(VisualTreeHelper.GetChild(root, i), visited);
                }
            }

            foreach (object child in LogicalTreeHelper.GetChildren(root))
            {
                if (child is DependencyObject dependencyObject)
                {
                    RefreshLocalizedBindings(dependencyObject, visited);
                }
            }
        }

        private static void UpdateBinding(DependencyObject target, DependencyProperty property)
        {
            BindingOperations.GetBindingExpressionBase(target, property)?.UpdateTarget();
        }

        public static string Get(string key)
        {
            return Get(key, CultureInfo.CurrentUICulture);
        }

        public static string Get(string key, CultureInfo culture)
        {
            string? value = Resources.GetString(key, culture);
            if (!string.IsNullOrEmpty(value))
            {
                return value;
            }

            value = Resources.GetString(key, English);
            return string.IsNullOrEmpty(value) ? $"[[{key}]]" : value;
        }

        public static string Format(string key, params object?[] args) =>
            Format(key, CultureInfo.CurrentCulture, args);

        public static string Format(string key, CultureInfo culture, params object?[] args) =>
            string.Format(culture, Get(key, culture), args);
    }

    public sealed class LocalizedStringProvider : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        public string this[string key] => LocalizationService.Get(key);

        public void Refresh()
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
        }
    }

    [MarkupExtensionReturnType(typeof(string))]
    public sealed class LocExtension : MarkupExtension
    {
        public string ResourceKey { get; set; } = string.Empty;

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            if (string.IsNullOrWhiteSpace(ResourceKey))
            {
                return string.Empty;
            }

            return new System.Windows.Data.Binding($"[{ResourceKey}]")
            {
                Source = LocalizationService.Strings,
                Mode = BindingMode.OneWay
            }.ProvideValue(serviceProvider);
        }
    }
}
