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

    [Fact]
    public void Create_IdenticalStrings_ProducesNoChanges()
    {
        const string text = "Идентичная строка.";
        var segments = TextDiff.Create(text, text);
        Assert.Single(segments);
        Assert.Equal(TextDiffKind.Unchanged, segments[0].Kind);
        Assert.Equal(text, segments[0].Text);
    }

    [Fact]
    public void Create_CompletelyDifferentStrings_ProducesOnlyAddedRemoved()
    {
        var segments = TextDiff.Create("abc", "xyz");
        Assert.All(segments, s => Assert.NotEqual(TextDiffKind.Unchanged, s.Kind));
        Assert.Contains(segments, s => s.Kind == TextDiffKind.Removed);
        Assert.Contains(segments, s => s.Kind == TextDiffKind.Added);
    }

    [Fact]
    public void Create_EmptyInputs_HandledGracefully()
    {
        Assert.Empty(TextDiff.Create("", ""));
        Assert.Single(TextDiff.Create("", "new"));
        Assert.Single(TextDiff.Create("old", ""));
    }

    [Fact]
    public void Create_MultipleChanges_AllDetected()
    {
        var segments = TextDiff.Create("1 2 3", "1 4 5");
        Assert.Contains(segments, s => s.Kind == TextDiffKind.Removed && s.Text == "2");
        Assert.Contains(segments, s => s.Kind == TextDiffKind.Removed && s.Text == "3");
        Assert.Contains(segments, s => s.Kind == TextDiffKind.Added && s.Text == "4");
        Assert.Contains(segments, s => s.Kind == TextDiffKind.Added && s.Text == "5");
    }
}
