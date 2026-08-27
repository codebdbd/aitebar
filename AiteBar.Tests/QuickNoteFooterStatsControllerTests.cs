using System.Windows.Documents;
using AiteBar;

namespace AiteBar.Tests;

public sealed class QuickNoteFooterStatsControllerTests
{
    [Fact]
    public void CalculateStats_CalculatesWordsLinesAndCharactersCorrectly()
    {
        var doc = new FlowDocument();
        doc.Blocks.Add(new Paragraph(new Run("Hello world!\nSecond line of note.")));

        var stats = QuickNoteFooterStatsController.CalculateStats(doc);
        Assert.False(stats.IsEmpty);
        Assert.Equal(2, stats.LineCount);
        Assert.Equal(6, stats.WordCount);
        Assert.True(stats.CharacterCount > 0);
    }

    [Fact]
    public void CalculateStats_HandlesEmptyDocument()
    {
        var doc = new FlowDocument();
        var stats = QuickNoteFooterStatsController.CalculateStats(doc);
        Assert.True(stats.IsEmpty);
        Assert.Equal(0, stats.CharacterCount);
        Assert.Equal(0, stats.WordCount);
        Assert.Equal(0, stats.LineCount);
    }

    [Theory]
    [InlineData(QuickNoteStatusKind.Saving)]
    [InlineData(QuickNoteStatusKind.SavedAt)]
    [InlineData(QuickNoteStatusKind.SaveFailed)]
    [InlineData(QuickNoteStatusKind.Copied)]
    [InlineData(QuickNoteStatusKind.LinkHighlightPaused)]
    internal void FormatStatusText_ReturnsNonEmptyStrings(QuickNoteStatusKind kind)
    {
        string text = QuickNoteFooterStatsController.FormatStatusText(kind, "test.txt");
        Assert.False(string.IsNullOrWhiteSpace(text));
    }
}
