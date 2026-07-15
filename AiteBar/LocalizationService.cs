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
        private static string _appliedCulturePreference = AutoCulture;
        private static CultureInfo _resolvedCulture = ResolveCulture(AutoCulture);

        public static CultureInfo ResolvedCulture => _resolvedCulture;
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
            string normalizedPreference = NormalizeCultureName(savedCulture);
            CultureInfo resolvedCulture = ResolveCulture(normalizedPreference);

            if (string.Equals(_appliedCulturePreference, normalizedPreference, StringComparison.Ordinal) &&
                string.Equals(_resolvedCulture.Name, resolvedCulture.Name, StringComparison.OrdinalIgnoreCase))
            {
                ApplyResolvedCulture(_resolvedCulture);
                return;
            }

            _appliedCulturePreference = normalizedPreference;
            _resolvedCulture = resolvedCulture;
            ApplyResolvedCulture(_resolvedCulture);
            Strings.Refresh();
            CultureChanged?.Invoke(null, EventArgs.Empty);
        }

        public static void EnsureAppliedCulture()
        {
            ApplyResolvedCulture(_resolvedCulture);
        }

        public static void RefreshLocalizedBindings(DependencyObject root)
        {
            var visited = new HashSet<DependencyObject>();
            // First pass: scan main tree for Window, ContextMenu and ToolTip
            ScanMainTree(root, visited);
        }

        private static void ScanMainTree(DependencyObject root, HashSet<DependencyObject> visited)
        {
            if (!visited.Add(root))
            {
                return;
            }

            // Update Window title
            if (root is Window window)
            {
                UpdateBinding(window, Window.TitleProperty);
            }

            // Check for ContextMenu and ToolTip on FrameworkElements
            if (root is FrameworkElement frameworkElement)
            {
                UpdateBinding(frameworkElement, FrameworkElement.ToolTipProperty);

                // If ToolTip is a DependencyObject, walk its entire tree
                if (frameworkElement.ToolTip is DependencyObject toolTip)
                {
                    WalkDetachedTree(toolTip, visited);
                }

                // Walk entire ContextMenu tree
                if (frameworkElement.ContextMenu != null)
                {
                    WalkDetachedTree(frameworkElement.ContextMenu, visited);
                }
            }

            // Continue scanning main tree for other elements with ContextMenu/ToolTip
            if (root is Visual or System.Windows.Media.Media3D.Visual3D)
            {
                int visualChildren = VisualTreeHelper.GetChildrenCount(root);
                for (int i = 0; i < visualChildren; i++)
                {
                    ScanMainTree(VisualTreeHelper.GetChild(root, i), visited);
                }
            }

            foreach (object child in LogicalTreeHelper.GetChildren(root))
            {
                if (child is DependencyObject dependencyObject)
                {
                    ScanMainTree(dependencyObject, visited);
                }
            }
        }

        private static void WalkDetachedTree(DependencyObject root, HashSet<DependencyObject> visited)
        {
            if (!visited.Add(root))
            {
                return;
            }

            // Update all relevant properties on elements in detached trees (ContextMenu, ToolTip)
            if (root is FrameworkElement frameworkElement)
            {
                UpdateBinding(frameworkElement, FrameworkElement.ToolTipProperty);

                if (frameworkElement.ContextMenu != null)
                {
                    WalkDetachedTree(frameworkElement.ContextMenu, visited);
                }
            }

            switch (root)
            {
                case HeaderedItemsControl headeredItemsControl:
                    UpdateBinding(headeredItemsControl, HeaderedItemsControl.HeaderProperty);
                    break;
                case TextBlock textBlock:
                    UpdateBinding(textBlock, TextBlock.TextProperty);
                    break;
                case System.Windows.Controls.CheckBox checkBox:
                    UpdateBinding(checkBox, ContentControl.ContentProperty);
                    break;
                case System.Windows.Controls.RadioButton radioButton:
                    UpdateBinding(radioButton, ContentControl.ContentProperty);
                    break;
                case HeaderedContentControl headeredContentControl:
                    UpdateBinding(headeredContentControl, HeaderedContentControl.HeaderProperty);
                    UpdateBinding(headeredContentControl, ContentControl.ContentProperty);
                    break;
                case ContentControl contentControl:
                    UpdateBinding(contentControl, ContentControl.ContentProperty);
                    break;
            }

            // Walk all children of detached tree elements
            if (root is Visual or System.Windows.Media.Media3D.Visual3D)
            {
                int visualChildren = VisualTreeHelper.GetChildrenCount(root);
                for (int i = 0; i < visualChildren; i++)
                {
                    WalkDetachedTree(VisualTreeHelper.GetChild(root, i), visited);
                }
            }

            foreach (object child in LogicalTreeHelper.GetChildren(root))
            {
                if (child is DependencyObject dependencyObject)
                {
                    WalkDetachedTree(dependencyObject, visited);
                }
            }
        }

        private static void UpdateBinding(DependencyObject target, DependencyProperty property)
        {
            BindingOperations.GetBindingExpressionBase(target, property)?.UpdateTarget();
        }

        public static string Get(string key)
        {
            return Get(key, _resolvedCulture);
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
            Format(key, _resolvedCulture, args);

        public static string Format(string key, CultureInfo culture, params object?[] args) =>
            string.Format(culture, Get(key, culture), args);

        private static void ApplyResolvedCulture(CultureInfo culture)
        {
            // No longer mutate global CultureInfo properties to avoid side effects on other applications
            // All app code now uses LocalizationService.ResolvedCulture explicitly
        }
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
