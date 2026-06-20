using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

using System.Runtime.Versioning;

namespace AiteBar
{
    [SupportedOSPlatform("windows6.1")]
    public sealed class ClipboardHistoryService : IDisposable
    {
        public event EventHandler? HistoryChanged;
        public IReadOnlyList<ClipboardHistoryEntry> Entries => _entries.AsReadOnly();
        
        private readonly List<ClipboardHistoryEntry> _entries = new List<ClipboardHistoryEntry>();
        private const int MaxEntries = 50;
        private const int MaxTextLength = 10 * 1024;
        private Window? _listeningWindow;
        private HwndSource? _hwndSource;
        private bool _suppressNextChange = false;
        private bool _disposed = false;

        public ClipboardHistoryService()
        {
        }

        public void StartListening(Window window)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(ClipboardHistoryService));
            
            if (_listeningWindow == window) return;
            
            StopListening();
            
            _listeningWindow = window;
            
            // Wait for window to load to get valid HWND
            window.Loaded += OnWindowLoaded;
            window.Closed += OnWindowClosed;
        }

        private void OnWindowClosed(object? sender, EventArgs e)
        {
            StopListening();
        }

        private void OnWindowLoaded(object? sender, RoutedEventArgs e)
        {
            if (sender is Window window && _listeningWindow == window)
            {
                window.Loaded -= OnWindowLoaded;
                
                _hwndSource = HwndSource.FromHwnd(new WindowInteropHelper(window).Handle);
                if (_hwndSource != null)
                {
                    _hwndSource.AddHook(WndProc);
                    NativeMethods.AddClipboardFormatListener(_hwndSource.Handle);
                }
            }
        }

        public void StopListening()
        {
            if (_listeningWindow != null)
            {
                _listeningWindow.Loaded -= OnWindowLoaded;
                _listeningWindow.Closed -= OnWindowClosed;
            }
            
            if (_hwndSource != null)
            {
                NativeMethods.RemoveClipboardFormatListener(_hwndSource.Handle);
                _hwndSource.RemoveHook(WndProc);
                _hwndSource.Dispose();
                _hwndSource = null;
            }
            _listeningWindow = null;
        }

        public void SuppressNextChange()
        {
            _suppressNextChange = true;
        }

        public void ClearHistory()
        {
            _entries.Clear();
            HistoryChanged?.Invoke(this, EventArgs.Empty);
        }

        public void CopyEntryToClipboard(ClipboardHistoryEntry entry)
        {
            try
            {
                SuppressNextChange();
                
                if (entry.IsImage && entry.ImageBytes != null)
                {
                    using var stream = new MemoryStream(entry.ImageBytes);
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.StreamSource = stream;
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    bitmap.Freeze();
                    Clipboard.SetImage(bitmap);
                }
                else if (!string.IsNullOrEmpty(entry.Text))
                {
                    Clipboard.SetText(entry.Text);
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == NativeMethods.WM_CLIPBOARDUPDATE)
            {
                OnClipboardChanged();
                handled = true;
            }
            return IntPtr.Zero;
        }

        private void OnClipboardChanged()
        {
            if (_suppressNextChange)
            {
                _suppressNextChange = false;
                return;
            }

            try
            {
                string? text = null;
                byte[]? imageBytes = null;

                if (Clipboard.ContainsText())
                {
                    text = Clipboard.GetText();
                    if (text.Length > MaxTextLength)
                    {
                        text = null;
                    }
                }

                if (Clipboard.ContainsImage())
                {
                    try
                    {
                        var image = Clipboard.GetImage();
                        if (image != null)
                        {
                            using var stream = new MemoryStream();
                            var encoder = new PngBitmapEncoder();
                            encoder.Frames.Add(BitmapFrame.Create(image));
                            encoder.Save(stream);
                            imageBytes = stream.ToArray();
                        }
                    }
                    catch
                    {
                        // Ignore image errors
                    }
                }

                if (!string.IsNullOrEmpty(text) || imageBytes != null)
                {
                    // Check for duplicates
                    bool isDuplicate = false;
                    foreach (var entry in _entries)
                    {
                        if (!string.IsNullOrEmpty(text) && entry.Text == text)
                        {
                            isDuplicate = true;
                            break;
                        }
                        if (imageBytes != null && entry.ImageBytes != null && 
                            entry.ImageBytes.Length == imageBytes.Length)
                        {
                            // Simple check for duplicate images
                            isDuplicate = true;
                            break;
                        }
                    }

                    if (!isDuplicate)
                    {
                        var newEntry = new ClipboardHistoryEntry
                        {
                            Text = text ?? string.Empty,
                            ImageBytes = imageBytes,
                            Timestamp = DateTime.Now
                        };

                        _entries.Insert(0, newEntry);
                        
                        if (_entries.Count > MaxEntries)
                        {
                            _entries.RemoveAt(_entries.Count - 1);
                        }

                        HistoryChanged?.Invoke(this, EventArgs.Empty);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            
            StopListening();
            _disposed = true;
        }
    }

    public sealed class ClipboardHistoryEntry
    {
        public string Text { get; init; } = string.Empty;
        public byte[]? ImageBytes { get; init; }
        public bool IsImage => ImageBytes != null;
        public DateTime Timestamp { get; init; }
        public string DisplayText 
        { 
            get 
            {
                if (IsImage) return "📷 Image";
                if (Text.Length > 50) return Text.Substring(0, 50) + "...";
                return Text;
            } 
        }
    }
}
