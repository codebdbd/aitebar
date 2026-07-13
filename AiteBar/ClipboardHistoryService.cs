using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace AiteBar
{
    public enum ClipboardCopyMode
    {
        Original,
        SingleLine
    }

    [SupportedOSPlatform("windows6.1")]
    public sealed class ClipboardHistoryService : IDisposable
    {
        private static ClipboardHistoryService? _instance;
        private static readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = false };
        private static readonly string DefaultHistoryFile = Path.Combine(PathHelper.AppDataFolder, "clipboard_history.json");
        private const int StorageSchemaVersion = 2;
        private const int MaxEntries = 50;
        private const int MaxTextLength = 10 * 1024;
        private const int MaxImageBytes = 5 * 1024 * 1024;

        private readonly List<ClipboardHistoryEntry> _entries = [];
        private readonly string _historyFile;
        private HwndSource? _hwndSource;
        private IntPtr? _hwnd;
        private bool _suppressNextChange;
        private string? _suppressedText;
        private byte[]? _suppressedImageBytes;
        private DateTime _suppressedClipboardExpiresAtUtc;
        private int _suppressedClipboardNotificationBudget;
        private bool _disposed;
        private bool _persistHistory;

        public static ClipboardHistoryService Instance => _instance ??= new ClipboardHistoryService();

        public event EventHandler? HistoryChanged;
        public IReadOnlyList<ClipboardHistoryEntry> Entries => _entries.AsReadOnly();
        public bool PersistHistory => _persistHistory;

        private ClipboardHistoryService()
            : this(DefaultHistoryFile, ReadPersistHistoryEnabledFromSettings())
        {
        }

        internal ClipboardHistoryService(string historyFile, bool persistHistory)
        {
            _historyFile = historyFile;
            _persistHistory = persistHistory;
            LoadHistory();
        }

        public void Initialize(IntPtr hwnd)
        {
            if (_hwnd.HasValue)
            {
                return;
            }

            _hwnd = hwnd;
            _hwndSource = HwndSource.FromHwnd(hwnd);
            if (_hwndSource != null)
            {
                _hwndSource.AddHook(WndProc);
                NativeMethods.AddClipboardFormatListener(hwnd);
            }
        }

        public void ConfigurePersistence(bool persistHistory)
        {
            if (_persistHistory == persistHistory)
            {
                return;
            }

            _persistHistory = persistHistory;
            if (_persistHistory)
            {
                SaveHistory();
            }
            else
            {
                DeletePersistedHistoryFile();
            }

            HistoryChanged?.Invoke(this, EventArgs.Empty);
        }

        public void SuppressNextChange()
        {
            _suppressNextChange = true;
        }

        public void ClearHistory()
        {
            ClearUnpinnedHistory();
        }

        public void ClearUnpinnedHistory()
        {
            int removed = _entries.RemoveAll(entry => !entry.IsPinned);
            if (removed == 0)
            {
                return;
            }

            PersistAndNotify();
        }

        public void ClearAllHistory()
        {
            if (_entries.Count == 0)
            {
                DeletePersistedHistoryFile();
                return;
            }

            _entries.Clear();
            PersistAndNotify();
        }

        public bool DeleteEntry(string entryId)
        {
            int removed = _entries.RemoveAll(entry => string.Equals(entry.Id, entryId, StringComparison.Ordinal));
            if (removed == 0)
            {
                return false;
            }

            PersistAndNotify();
            return true;
        }

        public bool TogglePin(string entryId)
        {
            int index = _entries.FindIndex(entry => string.Equals(entry.Id, entryId, StringComparison.Ordinal));
            if (index < 0)
            {
                return false;
            }

            ClipboardHistoryEntry current = _entries[index];
            _entries[index] = current with { IsPinned = !current.IsPinned };
            ReorderEntries();
            PersistAndNotify();
            return true;
        }

        public bool CopyEntryToClipboard(ClipboardHistoryEntry entry, ClipboardCopyMode mode = ClipboardCopyMode.Original)
        {
            try
            {
                if (entry.IsImage && entry.ImageBytes != null)
                {
                    using var stream = new MemoryStream(entry.ImageBytes);
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.StreamSource = stream;
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    bitmap.Freeze();
                    RegisterSuppressedClipboardPayload(null, entry.ImageBytes);
                    SuppressNextChange();
                    Clipboard.SetImage(bitmap);
                    return true;
                }

                if (!string.IsNullOrEmpty(entry.Text))
                {
                    string text = mode == ClipboardCopyMode.SingleLine
                        ? ClipboardTextTransforms.ToSingleLine(entry.Text)
                        : entry.Text;

                    if (string.IsNullOrEmpty(text))
                    {
                        return false;
                    }

                    RegisterSuppressedClipboardPayload(text, null);
                    SuppressNextChange();
                    Clipboard.SetText(text);
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                ClearSuppressedClipboardPayload();
                Logger.Log(ex);
                return false;
            }
        }

        public bool CopyEntryAsSingleLine(string entryId)
        {
            ClipboardHistoryEntry? entry = _entries.FirstOrDefault(item => string.Equals(item.Id, entryId, StringComparison.Ordinal));
            return entry != null && CopyEntryToClipboard(entry, ClipboardCopyMode.SingleLine);
        }

        internal bool RecordClipboardData(string? text, byte[]? imageBytes, DateTime? timestamp = null)
        {
            string? normalizedText = NormalizeClipboardText(text);
            byte[]? normalizedImage = NormalizeClipboardImage(imageBytes);
            if (normalizedText == null && normalizedImage == null)
            {
                return false;
            }

            DateTime entryTime = timestamp ?? DateTime.Now;
            int existingIndex = FindDuplicateIndex(normalizedText, normalizedImage);

            if (existingIndex >= 0)
            {
                ClipboardHistoryEntry existing = _entries[existingIndex];
                _entries[existingIndex] = existing with
                {
                    Text = normalizedText ?? existing.Text,
                    ImageBytes = normalizedImage ?? existing.ImageBytes,
                    Timestamp = entryTime
                };
            }
            else
            {
                _entries.Add(new ClipboardHistoryEntry
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Text = normalizedText ?? string.Empty,
                    ImageBytes = normalizedImage,
                    Timestamp = entryTime,
                    IsPinned = false
                });
            }

            ReorderEntries();
            TrimEntriesToLimit();
            SaveHistory();
            HistoryChanged?.Invoke(this, EventArgs.Empty);
            return true;
        }

        private static string? NormalizeClipboardText(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            return text.Length > MaxTextLength ? text[..MaxTextLength] : text;
        }

        private static byte[]? NormalizeClipboardImage(byte[]? imageBytes)
        {
            if (imageBytes == null || imageBytes.Length == 0)
            {
                return null;
            }

            return imageBytes.Length <= MaxImageBytes ? imageBytes : null;
        }

        private int FindDuplicateIndex(string? text, byte[]? imageBytes)
        {
            for (int i = 0; i < _entries.Count; i++)
            {
                ClipboardHistoryEntry entry = _entries[i];
                if (imageBytes != null && entry.ImageBytes != null && entry.ImageBytes.SequenceEqual(imageBytes))
                {
                    return i;
                }

                if (text != null && !entry.IsImage && string.Equals(entry.Text, text, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
        }

        private void ReorderEntries()
        {
            _entries.Sort(static (left, right) =>
            {
                int pinCompare = right.IsPinned.CompareTo(left.IsPinned);
                if (pinCompare != 0)
                {
                    return pinCompare;
                }

                return right.Timestamp.CompareTo(left.Timestamp);
            });
        }

        private void TrimEntriesToLimit()
        {
            if (_entries.Count <= MaxEntries)
            {
                return;
            }

            List<ClipboardHistoryEntry> pinned = _entries.Where(entry => entry.IsPinned).Take(MaxEntries).ToList();
            int remaining = MaxEntries - pinned.Count;
            List<ClipboardHistoryEntry> regular = _entries.Where(entry => !entry.IsPinned).Take(remaining).ToList();

            _entries.Clear();
            _entries.AddRange(pinned);
            _entries.AddRange(regular);
        }

        private void LoadHistory()
        {
            _entries.Clear();
            if (!_persistHistory || !File.Exists(_historyFile))
            {
                return;
            }

            try
            {
                string json = File.ReadAllText(_historyFile);
                if (string.IsNullOrWhiteSpace(json))
                {
                    return;
                }

                List<PersistedClipboardEntry>? entries = TryReadVersionedEntries(json) ?? TryReadLegacyEntries(json);
                if (entries == null)
                {
                    return;
                }

                foreach (PersistedClipboardEntry entry in entries.Take(MaxEntries))
                {
                    string text = entry.Text ?? string.Empty;
                    byte[]? imageBytes = null;
                    if (!string.IsNullOrWhiteSpace(entry.ImageBase64))
                    {
                        imageBytes = Convert.FromBase64String(entry.ImageBase64);
                    }
                    imageBytes = NormalizeClipboardImage(imageBytes);

                    if (string.IsNullOrWhiteSpace(text) && imageBytes == null)
                    {
                        continue;
                    }

                    _entries.Add(new ClipboardHistoryEntry
                    {
                        Id = string.IsNullOrWhiteSpace(entry.Id) ? Guid.NewGuid().ToString("N") : entry.Id,
                        Text = text,
                        ImageBytes = imageBytes,
                        Timestamp = entry.Timestamp == default ? DateTime.Now : entry.Timestamp,
                        IsPinned = entry.IsPinned
                    });
                }

                ReorderEntries();
                TrimEntriesToLimit();
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
        }

        private static List<PersistedClipboardEntry>? TryReadVersionedEntries(string json)
        {
            try
            {
                PersistedClipboardHistoryDocument? document = JsonSerializer.Deserialize<PersistedClipboardHistoryDocument>(json, _jsonOptions);
                return document?.Entries;
            }
            catch
            {
                return null;
            }
        }

        private static List<PersistedClipboardEntry>? TryReadLegacyEntries(string json)
        {
            try
            {
                JsonNode? node = JsonNode.Parse(json);
                if (node is not JsonArray)
                {
                    return null;
                }

                return JsonSerializer.Deserialize<List<PersistedClipboardEntry>>(json, _jsonOptions);
            }
            catch
            {
                return null;
            }
        }

        private void SaveHistory()
        {
            try
            {
                if (!_persistHistory)
                {
                    DeletePersistedHistoryFile();
                    return;
                }

                PathHelper.EnsureDirectories();
                if (_entries.Count == 0)
                {
                    DeletePersistedHistoryFile();
                    return;
                }

                var document = new PersistedClipboardHistoryDocument
                {
                    Version = StorageSchemaVersion,
                    Entries = _entries.Take(MaxEntries).Select(entry => new PersistedClipboardEntry
                    {
                        Id = entry.Id,
                        Text = entry.Text,
                        ImageBase64 = entry.ImageBytes != null ? Convert.ToBase64String(entry.ImageBytes) : null,
                        Timestamp = entry.Timestamp,
                        IsPinned = entry.IsPinned
                    }).ToList()
                };

                string json = JsonSerializer.Serialize(document, _jsonOptions);
                string tempFile = _historyFile + ".tmp";
                File.WriteAllText(tempFile, json);
                File.Move(tempFile, _historyFile, overwrite: true);
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
        }

        private void DeletePersistedHistoryFile()
        {
            try
            {
                if (File.Exists(_historyFile))
                {
                    File.Delete(_historyFile);
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
        }

        private void PersistAndNotify()
        {
            ReorderEntries();
            TrimEntriesToLimit();
            SaveHistory();
            HistoryChanged?.Invoke(this, EventArgs.Empty);
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
            try
            {
                string? text = null;
                byte[]? imageBytes = null;

                if (Clipboard.ContainsText())
                {
                    text = Clipboard.GetText();
                }

                if (Clipboard.ContainsImage())
                {
                    try
                    {
                        BitmapSource? image = Clipboard.GetImage();
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
                    }
                }

                if (ShouldIgnoreClipboardPayload(text, imageBytes))
                {
                    return;
                }

                RecordClipboardData(text, imageBytes);
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
        }

        private void RegisterSuppressedClipboardPayload(string? text, byte[]? imageBytes)
        {
            _suppressNextChange = true;
            _suppressedText = text;
            _suppressedImageBytes = imageBytes;
            _suppressedClipboardExpiresAtUtc = DateTime.UtcNow.AddMilliseconds(500);
            _suppressedClipboardNotificationBudget = 2;
        }

        private bool ShouldIgnoreClipboardPayload(string? text, byte[]? imageBytes)
        {
            if (!_suppressNextChange || _suppressedClipboardExpiresAtUtc == default)
            {
                return false;
            }

            if (DateTime.UtcNow > _suppressedClipboardExpiresAtUtc)
            {
                ClearSuppressedClipboardPayload();
                return false;
            }

            bool sameText = string.Equals(_suppressedText, text, StringComparison.Ordinal);
            bool sameImage = (_suppressedImageBytes == null && imageBytes == null)
                || (_suppressedImageBytes != null && imageBytes != null && _suppressedImageBytes.SequenceEqual(imageBytes));

            if (!sameText || !sameImage)
            {
                return false;
            }

            _suppressedClipboardNotificationBudget--;
            if (_suppressedClipboardNotificationBudget <= 0)
            {
                ClearSuppressedClipboardPayload();
            }

            return true;
        }

        private void ClearSuppressedClipboardPayload()
        {
            _suppressNextChange = false;
            _suppressedText = null;
            _suppressedImageBytes = null;
            _suppressedClipboardExpiresAtUtc = default;
            _suppressedClipboardNotificationBudget = 0;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            SaveHistory();

            if (_hwnd.HasValue && _hwndSource != null)
            {
                NativeMethods.RemoveClipboardFormatListener(_hwnd.Value);
                _hwndSource.RemoveHook(WndProc);
                _hwndSource.Dispose();
                _hwndSource = null;
            }

            _disposed = true;
        }

        private static bool ReadPersistHistoryEnabledFromSettings()
        {
            try
            {
                if (!File.Exists(PathHelper.SettingsFile))
                {
                    return true;
                }

                JsonNode? node = JsonNode.Parse(File.ReadAllText(PathHelper.SettingsFile));
                return node?["ClipboardManagerPersistHistory"]?.GetValue<bool>() ?? true;
            }
            catch
            {
                return true;
            }
        }
    }

    public sealed record ClipboardHistoryEntry
    {
        public string Id { get; init; } = Guid.NewGuid().ToString("N");
        public string Text { get; init; } = string.Empty;
        public byte[]? ImageBytes { get; init; }
        public bool IsImage => ImageBytes != null;
        public DateTime Timestamp { get; init; }
        public bool IsPinned { get; init; }
        public string DisplayText => IsImage
            ? "Image"
            : ClipboardTextTransforms.ToDisplayText(Text);
    }

    internal sealed class PersistedClipboardHistoryDocument
    {
        public int Version { get; init; } = 2;
        public List<PersistedClipboardEntry> Entries { get; init; } = [];
    }

    internal sealed class PersistedClipboardEntry
    {
        public string? Id { get; init; }
        public string? Text { get; init; }
        public string? ImageBase64 { get; init; }
        public DateTime Timestamp { get; init; }
        public bool IsPinned { get; init; }
    }
}
