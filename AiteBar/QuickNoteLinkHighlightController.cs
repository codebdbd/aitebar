using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Windows.Documents;
using System.Windows.Threading;

namespace AiteBar
{
    internal sealed class LinkMatchCacheEntry
    {
        public string Text { get; }
        public List<(Match Match, QuickNoteDocumentFormatting.LinkType Type)> Matches { get; }

        public LinkMatchCacheEntry(string text, List<(Match Match, QuickNoteDocumentFormatting.LinkType Type)> matches)
        {
            Text = text;
            Matches = matches;
        }
    }

    internal sealed class QuickNoteLinkHighlightController : IDisposable
    {
        internal const int MaxLinkScanParagraphLength = 10_000;
        private readonly ConditionalWeakTable<Paragraph, LinkMatchCacheEntry> _linkMatchCache = [];
        private readonly DispatcherTimer _debounceTimer;
        private readonly Action _onScheduledUpdate;
        private bool _disposed;

        public QuickNoteLinkHighlightController(Action onScheduledUpdate)
        {
            _onScheduledUpdate = onScheduledUpdate ?? throw new ArgumentNullException(nameof(onScheduledUpdate));
            _debounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
            _debounceTimer.Tick += DebounceTimer_Tick;
        }

        public void ScheduleUpdate()
        {
            if (_disposed)
            {
                return;
            }

            _debounceTimer.Stop();
            _debounceTimer.Start();
        }

        private void DebounceTimer_Tick(object? sender, EventArgs e)
        {
            _debounceTimer.Stop();
            if (_disposed)
            {
                return;
            }

            _onScheduledUpdate();
        }

        public void ClearCache()
        {
            _linkMatchCache.Clear();
        }

        public ConditionalWeakTable<Paragraph, LinkMatchCacheEntry> Cache => _linkMatchCache;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _debounceTimer.Stop();
            _debounceTimer.Tick -= DebounceTimer_Tick;
            _linkMatchCache.Clear();
        }
    }
}
