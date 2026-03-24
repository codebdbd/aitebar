using System;
using System.Windows.Media;
using FontFamily = System.Windows.Media.FontFamily;

namespace SmartScreenDock
{
    internal static class FontHelper
    {
        // Ключи, хранящиеся в CustomElement.IconFont
        public const string SegoeKey    = "Segoe Fluent Icons";   // legacy — может отсутствовать на Win 10
        public const string MaterialKey = "Material Icons";
        public const string FluentKey   = "Fluent System Icons";
        public const string BrandsKey   = "Font Awesome Brands";

        private static readonly FontFamily _materialFont = new FontFamily(
            new Uri("pack://application:,,,/"),
            "./Resources/#Material Icons");

        private static readonly FontFamily _fluentSysFont = new FontFamily(
            new Uri("pack://application:,,,/"),
            "./Resources/#FluentSystemIcons-Regular");

        private static readonly FontFamily _brandsFont = new FontFamily(
            new Uri("pack://application:,,,/"),
            "./Resources/#Font Awesome 7 Brands");

        public static FontFamily Resolve(string fontName) => fontName switch
        {
            MaterialKey => _materialFont,
            FluentKey   => _fluentSysFont,
            BrandsKey   => _brandsFont,
            // Legаcy pack:// URI (старые кнопки, сохранённые раньше)
            _ when fontName.StartsWith("pack://") => ResolvePack(fontName),
            // Segoe Fluent Icons / любой системный шрифт
            _ => new FontFamily(fontName)
        };

        private static FontFamily ResolvePack(string packUri)
        {
            string path = "./" + packUri.Substring("pack://application:,,,/".Length);
            return new FontFamily(new Uri("pack://application:,,,/"), path);
        }
    }
}
