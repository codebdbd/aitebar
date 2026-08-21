namespace AiteBar.Tests;

public sealed class ZenEditorUndoHistoryTests
{
    [Fact]
    public void SequentialTyping_IsGroupedAsOneUndoOperation()
    {
        var history = new ZenEditorUndoHistory();
        DateTime now = DateTime.UtcNow;
        history.Record("", "a", [new(0, 1, 0)], now);
        history.Record("a", "ab", [new(1, 1, 0)], now.AddMilliseconds(100));
        history.Record("ab", "abc", [new(2, 1, 0)], now.AddMilliseconds(200));

        Assert.True(history.TryUndo("abc", out string text, out int caret));
        Assert.Equal("", text);
        Assert.Equal(0, caret);
        Assert.False(history.CanUndo);
    }

    [Fact]
    public void UndoRedo_RoundTripsReplacement()
    {
        var history = new ZenEditorUndoHistory();
        history.Record("hello world", "hello zen", [new(6, 3, 5)], DateTime.UtcNow);

        Assert.True(history.TryUndo("hello zen", out string original, out _));
        Assert.Equal("hello world", original);
        Assert.True(history.TryRedo(original, out string replacement, out _));
        Assert.Equal("hello zen", replacement);
    }

    [Fact]
    public void HistoriesRemainIndependentPerDocument()
    {
        var first = new ZenEditorUndoHistory();
        var second = new ZenEditorUndoHistory();
        first.Record("one", "one!", [new(3, 1, 0)], DateTime.UtcNow);
        second.Record("two", "two?", [new(3, 1, 0)], DateTime.UtcNow);

        Assert.True(first.TryUndo("one!", out string firstText, out _));
        Assert.Equal("one", firstText);
        Assert.True(second.CanUndo);
        Assert.True(second.TryUndo("two?", out string secondText, out _));
        Assert.Equal("two", secondText);
    }

    [Fact]
    public void Capacity_DropsOldestOperation()
    {
        var history = new ZenEditorUndoHistory(2);
        DateTime now = DateTime.UtcNow;
        history.Record("", "a", [new(0, 1, 0)], now);
        history.Record("a", "a b", [new(1, 2, 0)], now.AddSeconds(1));
        history.Record("a b", "a b c", [new(3, 2, 0)], now.AddSeconds(2));

        Assert.True(history.TryUndo("a b c", out string one, out _));
        Assert.Equal("a b", one);
        Assert.True(history.TryUndo(one, out string two, out _));
        Assert.Equal("a", two);
        Assert.False(history.CanUndo);
    }

    [Fact]
    public void PayloadBudget_DropsOldestLargeOperations()
    {
        var history = new ZenEditorUndoHistory();
        string first = new('a', 3_000_000);
        string second = first + new string('b', 3_000_000);
        history.Record(first, second, [new(first.Length, 3_000_000, 0)], DateTime.UtcNow);
        history.Record(second, second + new string('c', 3_000_000), [new(second.Length, 3_000_000, 0)], DateTime.UtcNow.AddSeconds(1));

        Assert.True(history.EstimatedPayloadBytes <= 8 * 1024 * 1024);
        Assert.True(history.CanUndo);
    }

    [Fact]
    public void FormattingOnlyEdit_RoundTripsStyles()
    {
        var history = new ZenEditorUndoHistory();
        ZenEditorTextStyle bold = new(0, 3, Bold: true, Italic: false, Underline: false);
        history.Record(
            "abc",
            "abc",
            [],
            [bold],
            [],
            DateTime.UtcNow);

        Assert.True(history.TryUndo(
            "abc",
            out string undoText,
            out IReadOnlyList<ZenEditorTextStyle> undoStyles,
            out _));
        Assert.Equal("abc", undoText);
        Assert.Empty(undoStyles);

        Assert.True(history.TryRedo(
            undoText,
            out string redoText,
            out IReadOnlyList<ZenEditorTextStyle> redoStyles,
            out _));
        Assert.Equal("abc", redoText);
        Assert.Equal([bold], redoStyles);
    }

    [Fact]
    public void TextEdit_RestoresStyleStateOnUndoAndRedo()
    {
        var history = new ZenEditorUndoHistory();
        ZenEditorTextStyle before = new(0, 3, Bold: true, Italic: false, Underline: false);
        ZenEditorTextStyle after = new(0, 4, Bold: true, Italic: false, Underline: false);
        history.Record(
            "abc",
            "abcd",
            [before],
            [after],
            [new ZenEditorTextChange(3, 1, 0)],
            DateTime.UtcNow);

        Assert.True(history.TryUndo(
            "abcd",
            out string undoText,
            out IReadOnlyList<ZenEditorTextStyle> undoStyles,
            out _));
        Assert.Equal("abc", undoText);
        Assert.Equal([before], undoStyles);

        Assert.True(history.TryRedo(
            undoText,
            out string redoText,
            out IReadOnlyList<ZenEditorTextStyle> redoStyles,
            out _));
        Assert.Equal("abcd", redoText);
        Assert.Equal([after], redoStyles);
    }
}
