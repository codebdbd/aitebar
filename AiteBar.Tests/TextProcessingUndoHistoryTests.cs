namespace AiteBar.Tests;

public sealed class TextProcessingUndoHistoryTests
{
    [Fact]
    public void UndoAndRedo_RestoreRecordedOperation()
    {
        var history = new TextProcessingUndoHistory();
        history.Record("before");

        Assert.True(history.TryUndo("after", out string before));
        Assert.Equal("before", before);
        Assert.True(history.TryRedo(before, out string after));
        Assert.Equal("after", after);
    }

    [Fact]
    public void NewRecord_ClearsRedo()
    {
        var history = new TextProcessingUndoHistory();
        history.Record("one");
        Assert.True(history.TryUndo("two", out _));

        history.Record("three");

        Assert.False(history.TryRedo("four", out _));
    }

    [Fact]
    public void Capacity_DropsOldestState()
    {
        var history = new TextProcessingUndoHistory(2);
        history.Record("one");
        history.Record("two");
        history.Record("three");

        Assert.True(history.TryUndo("four", out string three));
        Assert.Equal("three", three);
        Assert.True(history.TryUndo(three, out string two));
        Assert.Equal("two", two);
        Assert.False(history.TryUndo(two, out _));
    }
}
