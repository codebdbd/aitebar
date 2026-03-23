using System;
using System.Windows.Media;
using FontFamily = System.Windows.Media.FontFamily;

namespace SmartScreenDock
{
    internal static class FontHelper
    {
        public static FontFamily Resolve(string fontName)
        {
            if (fontName.StartsWith("pack://"))
            {
                string path = "./" + fontName.Substring("pack://application:,,,/".Length);
                return new FontFamily(new Uri("pack://application:,,,/"), path);
            }
            return new FontFamily(fontName);
        }
    }
}
