using AiteBar;

namespace AiteBar.Tests;

public sealed class ZenEditorTextHelperTests
{
    [Theory]
    [InlineData("abc", "abXc", 2, 1, 0)]
    [InlineData("abc", "ac", 1, 0, 1)]
    [InlineData("abc", "aXYc", 1, 2, 1)]
    [InlineData("", "text", 0, 4, 0)]
    public void CalculateSingleChange_ReturnsPlainTextOffsets(
        string previous,
        string current,
        int offset,
        int added,
        int removed)
    {
        ZenEditorTextChange change =
            ZenEditorTextHelper.CalculateSingleChange(previous, current);

        Assert.Equal(offset, change.Offset);
        Assert.Equal(added, change.AddedLength);
        Assert.Equal(removed, change.RemovedLength);
    }

    [Theory]
    [InlineData("", "Новый документ")]
    [InlineData("\r\nВторая строка", "Новый документ")]
    [InlineData("  Заголовок\t  документа  \nТекст", "Заголовок документа")]
    public void GetDisplayTitle_NormalizesOnlyFirstPhysicalLine(string text, string expected)
    {
        Assert.Equal(expected, ZenEditorTextHelper.GetDisplayTitle(text, "Новый документ"));
    }

    [Fact]
    public void GetDisplayTitle_TruncatesToEightyCharactersWithEllipsis()
    {
        string result = ZenEditorTextHelper.GetDisplayTitle(new string('я', 100), "Новый документ");

        Assert.Equal(80, result.Length);
        Assert.EndsWith("…", result);
    }

    [Fact]
    public void GetDisplayTitle_IgnoresLargeDocumentBody()
    {
        string result = ZenEditorTextHelper.GetDisplayTitle(
            "  Название\t документа  \n" + new string('я', 2_000_000),
            "Новый документ");

        Assert.Equal("Название документа", result);
    }

    [Fact]
    public void CreateExportFileName_ReplacesInvalidCharacters()
    {
        string result = ZenEditorTextHelper.CreateExportFileName("Проект: план/черновик", "Новый документ");

        Assert.Equal("Проект- план-черновик.txt", result);
    }

    [Fact]
    public void NormalizeExportText_AddsOneBlankLineBetweenParagraphs()
    {
        Assert.Equal(
            "a\r\n\r\nb\r\n\r\nc",
            ZenEditorTextHelper.NormalizeExportText("a\nb\rc"));
    }

    [Theory]
    [InlineData("a\n\nb", "a\r\n\r\nb")]
    [InlineData("a\n\n\nb", "a\r\n\r\nb")]
    [InlineData("a\n   \nb", "a\r\n\r\nb")]
    [InlineData("\na\n", "\r\na\r\n")]
    [InlineData(" \n\t", "\r\n")]
    [InlineData("одна строка", "одна строка")]
    [InlineData("", "")]
    public void NormalizeExportText_DoesNotDuplicateExistingBlankLines(
        string input,
        string expected)
    {
        Assert.Equal(expected, ZenEditorTextHelper.NormalizeExportText(input));
    }

    [Fact]
    public void ClampSelection_KeepsPositionsWithinDocument()
    {
        Assert.Equal((5, 4, 1), ZenEditorTextHelper.ClampSelection(5, 20, 4, 20));
    }

    [Fact]
    public void ApplyTextChangeToStyles_InsertsCapturedFormattingWithoutScanningDocument()
    {
        IReadOnlyList<ZenEditorTextStyle> result = ZenEditorTextHelper.ApplyTextChangeToStyles(
            [new ZenEditorTextStyle(0, 4, true, false, false)],
            new ZenEditorTextChange(2, 1, 0),
            new ZenEditorTextStyle(2, 1, true, false, false),
            5);

        Assert.Equal([new ZenEditorTextStyle(0, 5, true, false, false)], result);
    }

    [Fact]
    public void ApplyTextChangeToStyles_LeavesPlainInsertionOutsideFormattedRange()
    {
        IReadOnlyList<ZenEditorTextStyle> result = ZenEditorTextHelper.ApplyTextChangeToStyles(
            [new ZenEditorTextStyle(0, 2, true, false, false)],
            new ZenEditorTextChange(2, 1, 0),
            insertedStyle: null,
            currentTextLength: 5);

        Assert.Equal([new ZenEditorTextStyle(0, 2, true, false, false)], result);
    }

    [Fact]
    public void ApplyTextChangeToStyles_DeletesAndMergesRemainingFormatting()
    {
        IReadOnlyList<ZenEditorTextStyle> result = ZenEditorTextHelper.ApplyTextChangeToStyles(
            [new ZenEditorTextStyle(0, 6, false, true, false)],
            new ZenEditorTextChange(2, 0, 2),
            insertedStyle: null,
            currentTextLength: 4);

        Assert.Equal([new ZenEditorTextStyle(0, 4, false, true, false)], result);
    }
}
