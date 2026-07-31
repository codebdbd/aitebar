using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace AiteBar;

internal sealed class ZenParagraphEditor : System.Windows.Controls.RichTextBox
{
    private double _editorLineHeight = 30;
    private double _paragraphSpacing = 15;
    private string _plainText = string.Empty;
    private IReadOnlyList<ZenEditorTextChange> _lastPlainTextChanges = [];
    private bool _isSettingPlainText;
    private bool _canTransformLastTextStyles;
    private ZenEditorTextStyle? _lastInsertedTextStyle;
    private int _fullDocumentReadCount;
    private int _lastStyleCaptureInlineCount;

    public ZenParagraphEditor()
    {
        Document = new FlowDocument
        {
            PagePadding = new Thickness(0),
            ColumnGap = 0
        };
        ApplyDocumentStyle();
    }

    public string Text
    {
        get => _plainText;
        set => SetPlainText(value ?? string.Empty);
    }

    internal IReadOnlyList<ZenEditorTextChange> LastPlainTextChanges =>
        _lastPlainTextChanges;

    internal int FullDocumentReadCount => _fullDocumentReadCount;
    internal int LastStyleCaptureInlineCount => _lastStyleCaptureInlineCount;
    internal bool CanTransformLastTextStyles => _canTransformLastTextStyles;
    internal ZenEditorTextStyle? LastInsertedTextStyle => _lastInsertedTextStyle;

    public int CaretIndex
    {
        get => GetPlainTextIndex(CaretPosition);
        set => CaretPosition = GetTextPointer(value);
    }

    public int SelectionStart
    {
        get => GetPlainTextIndex(Selection.Start);
    }

    public int SelectionLength
    {
        get => Math.Max(0, GetPlainTextIndex(Selection.End) - GetPlainTextIndex(Selection.Start));
    }

    public string SelectedText
    {
        get => NormalizeLineEndings(new TextRange(Selection.Start, Selection.End).Text);
        set => new TextRange(Selection.Start, Selection.End).Text = value ?? string.Empty;
    }

    public double EditorLineHeight
    {
        get => _editorLineHeight;
        set
        {
            _editorLineHeight = value;
            ApplyDocumentStyle();
        }
    }

    public double ParagraphSpacing
    {
        get => _paragraphSpacing;
        set
        {
            _paragraphSpacing = value;
            ApplyDocumentStyle();
        }
    }

    public void Select(int start, int length)
    {
        int textLength = Text.Length;
        int safeStart = Math.Clamp(start, 0, textLength);
        int safeLength = Math.Clamp(length, 0, textLength - safeStart);
        Selection.Select(
            GetTextPointer(safeStart),
            GetTextPointer(safeStart + safeLength));
    }

    public Rect GetRectFromCharacterIndex(int index) =>
        GetTextPointer(index).GetCharacterRect(LogicalDirection.Forward);

    public Rect GetCaretRect() =>
        CaretPosition.GetCharacterRect(LogicalDirection.Forward);

    internal IReadOnlyList<ZenEditorTextStyle> CaptureTextStyles()
    {
        var styles = new List<ZenEditorTextStyle>();
        _lastStyleCaptureInlineCount = 0;
        int plainOffset = 0;
        bool firstParagraph = true;
        foreach (Paragraph paragraph in Document.Blocks.OfType<Paragraph>())
        {
            if (!firstParagraph)
            {
                plainOffset++;
            }

            firstParagraph = false;
            CaptureInlineStyles(paragraph.Inlines, ref plainOffset, styles);
        }

        return MergeAdjacentStyles(styles);
    }

    internal void ApplyTextStyles(IEnumerable<ZenEditorTextStyle>? styles)
    {
        if (styles is null)
        {
            return;
        }

        BeginChange();
        try
        {
            foreach (ZenEditorTextStyle style in styles)
            {
                int start = Math.Clamp(style.Start, 0, Text.Length);
                int length = Math.Clamp(style.Length, 0, Text.Length - start);
                if (length == 0)
                {
                    continue;
                }

                var range = new TextRange(
                    GetTextPointer(start),
                    GetTextPointer(start + length));
                if (style.Bold)
                {
                    range.ApplyPropertyValue(TextElement.FontWeightProperty, FontWeights.Bold);
                }

                if (style.Italic)
                {
                    range.ApplyPropertyValue(TextElement.FontStyleProperty, FontStyles.Italic);
                }

                if (style.Underline)
                {
                    range.ApplyPropertyValue(
                        Inline.TextDecorationsProperty,
                        TextDecorations.Underline);
                }
            }
        }
        finally
        {
            EndChange();
        }
    }

    protected override void OnTextChanged(TextChangedEventArgs e)
    {
        _canTransformLastTextStyles = false;
        _lastInsertedTextStyle = null;
        if (!_isSettingPlainText)
        {
            string previous = _plainText;
            if (!TryApplyLocalTextChange(e, previous, out string current, out ZenEditorTextChange change))
            {
                _fullDocumentReadCount++;
                current = GetPlainText(
                    Document.ContentStart,
                    Document.ContentEnd,
                    trimDocumentTerminator: true);
                change = ZenEditorTextHelper.CalculateSingleChange(previous, current);
            }

            _plainText = current;
            _lastPlainTextChanges = string.Equals(previous, current, StringComparison.Ordinal)
                ? []
                : [change];
        }

        base.OnTextChanged(e);
    }

    private void SetPlainText(string value)
    {
        string normalized = NormalizeLineEndings(value);
        string[] paragraphs = normalized.Split('\n', StringSplitOptions.None);

        _isSettingPlainText = true;
        BeginChange();
        try
        {
            Document.Blocks.Clear();
            foreach (string paragraphText in paragraphs)
            {
                var paragraph = new Paragraph();
                if (paragraphText.Length > 0)
                {
                    paragraph.Inlines.Add(new Run(paragraphText));
                }

                Document.Blocks.Add(paragraph);
            }
        }
        finally
        {
            EndChange();
            _isSettingPlainText = false;
        }

        _plainText = normalized;
        _lastPlainTextChanges = [];
    }

    private TextPointer GetTextPointer(int plainIndex)
    {
        int target = Math.Clamp(plainIndex, 0, Text.Length);
        int index = 0;
        Paragraph? lastParagraph = null;
        bool firstParagraph = true;

        foreach (Paragraph paragraph in Document.Blocks.OfType<Paragraph>())
        {
            lastParagraph = paragraph;
            if (!firstParagraph)
            {
                index++;
                if (target <= index)
                {
                    return paragraph.ContentStart.GetInsertionPosition(LogicalDirection.Forward);
                }
            }

            firstParagraph = false;
            int paragraphLength =
                GetInlinePlainTextLength(paragraph.ContentStart, paragraph.ContentEnd);
            if (target <= index + paragraphLength)
            {
                return GetParagraphTextPointer(paragraph, target - index);
            }

            index += paragraphLength;
        }

        return (lastParagraph?.ContentEnd ?? Document.ContentStart)
            .GetInsertionPosition(LogicalDirection.Backward);
    }

    private static TextPointer GetParagraphTextPointer(Paragraph paragraph, int plainOffset)
    {
        int target = Math.Max(0, plainOffset);
        TextPointer start = paragraph.ContentStart;
        TextPointer end = paragraph.ContentEnd;
        int low = 0;
        int high = Math.Max(0, start.GetOffsetToPosition(end));
        TextPointer best = start;

        while (low <= high)
        {
            int middle = low + ((high - low) / 2);
            TextPointer? candidate = start.GetPositionAtOffset(middle);
            if (candidate is null)
            {
                high = middle - 1;
                continue;
            }

            int candidateIndex = GetInlinePlainTextLength(start, candidate);
            if (candidateIndex <= target)
            {
                best = candidate;
                if (candidateIndex == target &&
                    candidate.GetPointerContext(LogicalDirection.Forward) == TextPointerContext.Text)
                {
                    return candidate;
                }

                low = middle + 1;
            }
            else
            {
                high = middle - 1;
            }
        }

        return best.GetInsertionPosition(LogicalDirection.Forward);
    }

    private bool TryApplyLocalTextChange(
        TextChangedEventArgs eventArgs,
        string previous,
        out string current,
        out ZenEditorTextChange plainChange)
    {
        current = previous;
        plainChange = default;
        if (eventArgs.Changes.Count != 1)
        {
            return false;
        }

        TextChange change = eventArgs.Changes.First();

        TextPointer start = Document.ContentStart.GetPositionAtOffset(change.Offset)
            ?? Document.ContentStart;
        int plainOffset = Math.Clamp(GetPlainTextIndex(start), 0, previous.Length);

        if (change.RemovedLength == 0)
        {
            TextPointer end = start.GetPositionAtOffset(change.AddedLength)
                ?? start;
            string added = GetPlainText(start, end);
            current = previous.Insert(plainOffset, added);
            plainChange = new ZenEditorTextChange(plainOffset, added.Length, 0);
            _canTransformLastTextStyles = added == "\n"
                || TryGetUniformTextStyle(start, end, plainOffset, added.Length, out _lastInsertedTextStyle);
            return true;
        }

        if (change.AddedLength == 0
            && change.RemovedLength == 1
            && plainOffset < previous.Length)
        {
            current = previous.Remove(plainOffset, 1);
            plainChange = new ZenEditorTextChange(plainOffset, 0, 1);
            _canTransformLastTextStyles = true;
            return true;
        }

        return false;
    }

    private static bool TryGetUniformTextStyle(
        TextPointer start,
        TextPointer end,
        int plainOffset,
        int plainLength,
        out ZenEditorTextStyle? style)
    {
        style = null;
        if (plainLength <= 0)
        {
            return true;
        }

        var range = new TextRange(start, end);
        object weightValue = range.GetPropertyValue(TextElement.FontWeightProperty);
        object styleValue = range.GetPropertyValue(TextElement.FontStyleProperty);
        object decorationsValue = range.GetPropertyValue(Inline.TextDecorationsProperty);
        if (weightValue == DependencyProperty.UnsetValue
            || styleValue == DependencyProperty.UnsetValue
            || decorationsValue == DependencyProperty.UnsetValue)
        {
            return false;
        }

        bool bold = weightValue is FontWeight weight && weight >= FontWeights.Bold;
        bool italic = styleValue is System.Windows.FontStyle fontStyle
            && (fontStyle == FontStyles.Italic || fontStyle == FontStyles.Oblique);
        bool underline = decorationsValue is TextDecorationCollection decorations
            && decorations.Any(decoration => decoration.Location == TextDecorationLocation.Underline);
        if (bold || italic || underline)
        {
            style = new ZenEditorTextStyle(plainOffset, plainLength, bold, italic, underline);
        }

        return true;
    }

    private int GetPlainTextIndex(TextPointer position)
    {
        int index = 0;
        bool firstParagraph = true;
        foreach (Paragraph paragraph in Document.Blocks.OfType<Paragraph>())
        {
            if (!firstParagraph)
            {
                index++;
            }

            firstParagraph = false;
            if (position.CompareTo(paragraph.ContentStart) <= 0)
            {
                return index;
            }

            if (position.CompareTo(paragraph.ContentEnd) <= 0)
            {
                return index + GetInlinePlainTextLength(paragraph.ContentStart, position);
            }

            index += GetInlinePlainTextLength(paragraph.ContentStart, paragraph.ContentEnd);
        }

        return Math.Min(index, _plainText.Length);
    }

    private static int GetInlinePlainTextLength(TextPointer start, TextPointer end)
    {
        int length = 0;
        TextPointer? position = start;
        while (position is not null && position.CompareTo(end) < 0)
        {
            TextPointerContext context = position.GetPointerContext(LogicalDirection.Forward);
            if (context == TextPointerContext.Text)
            {
                int runLength = position.GetTextRunLength(LogicalDirection.Forward);
                int remainingSymbols = Math.Max(0, position.GetOffsetToPosition(end));
                int consumed = Math.Min(runLength, remainingSymbols);
                length += consumed;
                position = position.GetPositionAtOffset(consumed);
                continue;
            }

            if (context == TextPointerContext.ElementStart
                && position.GetAdjacentElement(LogicalDirection.Forward) is LineBreak)
            {
                length++;
            }

            position = position.GetNextContextPosition(LogicalDirection.Forward);
        }

        return length;
    }

    private void CaptureInlineStyles(
        InlineCollection inlines,
        ref int plainOffset,
        List<ZenEditorTextStyle> styles)
    {
        foreach (Inline inline in inlines)
        {
            _lastStyleCaptureInlineCount++;
            if (inline is Run run)
            {
                int length = run.Text?.Length ?? 0;
                bool bold = run.FontWeight >= FontWeights.Bold;
                bool italic = run.FontStyle == FontStyles.Italic
                    || run.FontStyle == FontStyles.Oblique;
                bool underline = run.TextDecorations?.Contains(TextDecorations.Underline[0]) == true;
                if (length > 0 && (bold || italic || underline))
                {
                    styles.Add(new ZenEditorTextStyle(
                        plainOffset,
                        length,
                        bold,
                        italic,
                        underline));
                }

                plainOffset += length;
            }
            else if (inline is LineBreak)
            {
                plainOffset++;
            }
            else if (inline is Span span)
            {
                CaptureInlineStyles(span.Inlines, ref plainOffset, styles);
            }
        }
    }

    private static IReadOnlyList<ZenEditorTextStyle> MergeAdjacentStyles(
        IReadOnlyList<ZenEditorTextStyle> styles)
    {
        if (styles.Count < 2)
        {
            return styles;
        }

        var merged = new List<ZenEditorTextStyle>(styles.Count);
        foreach (ZenEditorTextStyle style in styles.OrderBy(style => style.Start))
        {
            if (merged.Count > 0
                && merged[^1].Start + merged[^1].Length == style.Start
                && merged[^1].Bold == style.Bold
                && merged[^1].Italic == style.Italic
                && merged[^1].Underline == style.Underline)
            {
                ZenEditorTextStyle previous = merged[^1];
                merged[^1] = previous with { Length = previous.Length + style.Length };
            }
            else
            {
                merged.Add(style);
            }
        }

        return merged;
    }

    private void ApplyDocumentStyle()
    {
        if (Document is null)
        {
            return;
        }

        var paragraphStyle = new Style(typeof(Paragraph));
        paragraphStyle.Setters.Add(new Setter(
            Block.MarginProperty,
            new Thickness(0, 0, 0, _paragraphSpacing)));
        paragraphStyle.Setters.Add(new Setter(
            Block.LineHeightProperty,
            _editorLineHeight));
        paragraphStyle.Setters.Add(new Setter(
            Block.LineStackingStrategyProperty,
            LineStackingStrategy.BlockLineHeight));
        paragraphStyle.Setters.Add(new Setter(
            Block.TextAlignmentProperty,
            TextAlignment.Left));
        Document.Resources[typeof(Paragraph)] = paragraphStyle;
    }

    private static string GetPlainText(
        TextPointer start,
        TextPointer end,
        bool trimDocumentTerminator = false)
    {
        string text = NormalizeLineEndings(new TextRange(start, end).Text);
        if (trimDocumentTerminator && text.EndsWith('\n'))
        {
            text = text[..^1];
        }

        return text;
    }

    private static string NormalizeLineEndings(string value) =>
        value.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
}
