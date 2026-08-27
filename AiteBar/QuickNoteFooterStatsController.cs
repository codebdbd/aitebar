using System;
using System.Linq;
using System.Runtime.Versioning;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Threading;
using WpfRichTextBox = System.Windows.Controls.RichTextBox;

namespace AiteBar
{
    internal sealed class QuickNoteStatsResult
    {
        public bool IsEmpty { get; set; } = true;
        public int CharacterCount { get; set; }
        public int WordCount { get; set; }
        public int LineCount { get; set; }
    }

    [SupportedOSPlatform("windows6.1")]
    internal sealed class QuickNoteFooterStatsController : IDisposable
    {
        private static readonly TimeSpan DebounceInterval = TimeSpan.FromMilliseconds(120);
        private static readonly Regex WordSplitRegex = new(@"\S+", RegexOptions.Compiled);

        private readonly WpfRichTextBox _editor;
        private readonly TextBlock _placeholder;
        private readonly TextBlock _statsText;
        private readonly DispatcherTimer _debounceTimer;

        private bool _dirty = true;
        private bool _isEmpty = true;
        private int _charCount;
        private int _wordCount;
        private int _lineCount;
        private bool _disposed;

        public QuickNoteFooterStatsController(WpfRichTextBox editor, TextBlock placeholder, TextBlock statsText)
        {
            _editor = editor ?? throw new ArgumentNullException(nameof(editor));
            _placeholder = placeholder ?? throw new ArgumentNullException(nameof(placeholder));
            _statsText = statsText ?? throw new ArgumentNullException(nameof(statsText));

            _debounceTimer = new DispatcherTimer { Interval = DebounceInterval };
            _debounceTimer.Tick += DebounceTimer_Tick;
        }

        public bool IsEmpty => _isEmpty;
        public int CharacterCount => _charCount;
        public int WordCount => _wordCount;
        public int LineCount => _lineCount;

        public void ScheduleUpdate()
        {
            if (_disposed) return;
            _dirty = true;
            _debounceTimer.Stop();
            _debounceTimer.Start();
        }

        public void Stop()
        {
            if (_disposed) return;
            _debounceTimer.Stop();
        }

        private void DebounceTimer_Tick(object? sender, EventArgs e)
        {
            _debounceTimer.Stop();
            UpdateUi();
        }

        public void UpdateUi()
        {
            if (_disposed) return;

            string selectedText = QuickNoteDocumentHelper.NormalizeLineEndings(_editor.Selection.Text);
            if (!string.IsNullOrEmpty(selectedText))
            {
                _placeholder.Visibility = Visibility.Collapsed;
                int selectedWithoutWs = selectedText.Count(c => !char.IsWhiteSpace(c));
                _statsText.Text = LocalizationService.Format("QuickNote_SelectedStats", selectedText.Length, selectedWithoutWs);
                return;
            }

            if (_dirty)
            {
                string text = GetEditorText();
                _isEmpty = string.IsNullOrWhiteSpace(text);
                _charCount = text.Length;
                _lineCount = string.IsNullOrEmpty(text) ? 0 : text.Count(c => c == '\n') + 1;
                _wordCount = string.IsNullOrEmpty(text)
                    ? 0
                    : WordSplitRegex.Matches(text).Count;
                _dirty = false;
            }

            _placeholder.Visibility = _isEmpty ? Visibility.Visible : Visibility.Collapsed;
            _statsText.Text = LocalizationService.Format("QuickNote_Stats", _charCount, _wordCount, _lineCount);
        }

        private string GetEditorText()
        {
            var range = new TextRange(_editor.Document.ContentStart, _editor.Document.ContentEnd);
            return QuickNoteDocumentHelper.NormalizeLineEndings(range.Text).TrimEnd('\r', '\n');
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _debounceTimer.Stop();
        }

        // ---- Static facade (kept for tests / backward compatibility) ----
        public static QuickNoteStatsResult CalculateStats(FlowDocument document)
        {
            var result = new QuickNoteStatsResult();
            if (document == null) return result;

            var range = new TextRange(document.ContentStart, document.ContentEnd);
            string raw = range.Text;
            string normalized = QuickNoteDocumentHelper.NormalizeLineEndings(raw).TrimEnd('\r', '\n');
            result.IsEmpty = string.IsNullOrEmpty(normalized);
            result.CharacterCount = normalized.Length;
            if (!result.IsEmpty)
            {
                result.WordCount = WordSplitRegex.Matches(normalized).Count;
                result.LineCount = normalized.Split('\n').Length;
            }
            return result;
        }

        public static string FormatStatusText(QuickNoteStatusKind kind, string? argument = null)
        {
            return kind switch
            {
                QuickNoteStatusKind.Saving => LocalizationService.Get("QuickNote_Saving"),
                QuickNoteStatusKind.SavedAt => LocalizationService.Format("QuickNote_SavedAt", DateTime.Now.ToString("HH:mm")),
                QuickNoteStatusKind.LoadFailed => LocalizationService.Get("QuickNote_LoadFailed"),
                QuickNoteStatusKind.SaveFailed => LocalizationService.Get("QuickNote_SaveFailed"),
                QuickNoteStatusKind.OpenFailed => LocalizationService.Get("QuickNote_OpenFailed"),
                QuickNoteStatusKind.Copied => LocalizationService.Get("QuickNote_Copied"),
                QuickNoteStatusKind.CopyFailed => LocalizationService.Get("QuickNote_CopyFailed"),
                QuickNoteStatusKind.ImageInsertFailed => LocalizationService.Get("QuickNote_ImageInsertFailed"),
                QuickNoteStatusKind.LinkHighlightPaused => LocalizationService.Get("QuickNote_LinkHighlightPaused"),
                QuickNoteStatusKind.ConflictCopySaved => LocalizationService.Format("QuickNote_ConflictCopySavedAt", argument ?? string.Empty),
                _ => string.Empty
            };
        }
    }
}
