using AiteBar;

namespace AiteBar.Tests;

public sealed class TextProcessingEditorBehaviorTests
{
    [Fact]
    public void InsertAtSelection_InsertsAtCaretWithoutReplacingEditor()
    {
        (string text, int caretIndex) = TextProcessingWindow.InsertAtSelection(
            "before after",
            7,
            0,
            "new ");

        Assert.Equal("before new after", text);
        Assert.Equal(11, caretIndex);
    }

    [Fact]
    public void InsertAtSelection_ReplacesOnlySelectedText()
    {
        (string text, int caretIndex) = TextProcessingWindow.InsertAtSelection(
            "before old after",
            7,
            3,
            "new");

        Assert.Equal("before new after", text);
        Assert.Equal(10, caretIndex);
    }
}
