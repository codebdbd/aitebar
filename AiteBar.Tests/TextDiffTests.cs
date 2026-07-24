namespace AiteBar.Tests;

public sealed class TextDiffTests
{
    [Fact]
    public void Create_MarksChangedWord()
    {
        IReadOnlyList<TextDiffSegment> segments = TextDiff.Create(
            "Это ошбка в тексте.",
            "Это ошибка в тексте.");

        Assert.Contains(segments, segment =>
            segment.Kind == TextDiffKind.Removed && segment.Text.Contains("ошбка", StringComparison.Ordinal));
        Assert.Contains(segments, segment =>
            segment.Kind == TextDiffKind.Added && segment.Text.Contains("ошибка", StringComparison.Ordinal));
        Assert.Equal(
            "Это ошибка в тексте.",
            string.Concat(segments.Where(segment => segment.Kind != TextDiffKind.Removed).Select(segment => segment.Text)));
    }

    [Fact]
    public void Create_PreservesOriginalAndChangedText()
    {
        const string original = "Первая строка.\nВторая строка.";
        const string changed = "Первая новая строка.\nВторая строка!";

        IReadOnlyList<TextDiffSegment> segments = TextDiff.Create(original, changed);

        Assert.Equal(
            original,
            string.Concat(segments.Where(segment => segment.Kind != TextDiffKind.Added).Select(segment => segment.Text)));
        Assert.Equal(
            changed,
            string.Concat(segments.Where(segment => segment.Kind != TextDiffKind.Removed).Select(segment => segment.Text)));
    }
}
