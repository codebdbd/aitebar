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
}
