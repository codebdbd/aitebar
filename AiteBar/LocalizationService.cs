using System;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Resources;
using System.Windows.Data;
using System.Windows.Markup;

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
