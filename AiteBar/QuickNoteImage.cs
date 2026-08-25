using System;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace AiteBar
{
    public class QuickNoteImage : Image
    {
        public static readonly DependencyProperty PngBase64Property = DependencyProperty.Register(
            nameof(PngBase64),
            typeof(string),
            typeof(QuickNoteImage),
            new PropertyMetadata(null, OnPngBase64Changed));

        static QuickNoteImage()
        {
            // Override the default style key if we wanted custom templates,
            // but we just inherit standard Image rendering.
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public new ImageSource? Source
        {
            get => base.Source;
            set => base.Source = value;
        }

        public string PngBase64
        {
            get => (string)GetValue(PngBase64Property);
            set => SetValue(PngBase64Property, value);
        }

        private static void OnPngBase64Changed(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is QuickNoteImage quickImage && e.NewValue is string base64 && !string.IsNullOrEmpty(base64))
            {
                if (quickImage.Source != null)
                {
                    return;
                }
                try
                {
                    byte[] bytes = Convert.FromBase64String(base64);
                    using var stream = new MemoryStream(bytes, writable: false);
                    var frame = BitmapFrame.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                    if (frame.CanFreeze)
                    {
                        frame.Freeze();
                    }
                    quickImage.Source = frame;
                }
                catch (Exception ex)
                {
                    Logger.Log(ex);
                }
            }
        }
    }
}
