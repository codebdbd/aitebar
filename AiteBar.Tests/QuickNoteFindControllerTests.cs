using System.Windows.Controls;
using System.Windows.Documents;
using AiteBar;

namespace AiteBar.Tests;

public sealed class QuickNoteFindControllerTests
{
    [Fact]
    public void CountMatches_FindsCaseInsensitiveMatches()
    {
        var doc = new FlowDocument();
        doc.Blocks.Add(new Paragraph(new Run("Hello world, hello everyone! HELLO")));

        int count = QuickNoteFindController.CountMatches(doc, "hello", matchCase: false);
        Assert.Equal(3, count);
    }

    [Fact]
    public void CountMatches_RespectsCaseSensitivity()
    {
        var doc = new FlowDocument();
        doc.Blocks.Add(new Paragraph(new Run("Hello world, hello everyone! HELLO")));

        int count = QuickNoteFindController.CountMatches(doc, "hello", matchCase: true);
        Assert.Equal(1, count);

        var options = new FindReplaceOptions { CaseSensitive = true };
        Assert.Equal(1, QuickNoteFindController.CountMatches(doc, "hello", options));
    }

    [Fact]
    public void CountMatches_RespectsWholeWord()
    {
        var doc = new FlowDocument();
        doc.Blocks.Add(new Paragraph(new Run("cat catalog concatenate cat")));

        int count = QuickNoteFindController.CountMatches(doc, "cat", matchCase: false, wholeWord: true);
        Assert.Equal(2, count);

        var options = new FindReplaceOptions { WholeWord = true };
        Assert.Equal(2, QuickNoteFindController.CountMatches(doc, "cat", options));
    }

    [Fact]
    public void FindAllMatches_ReturnsEmptyForMissingQueryOrNullDoc()
    {
        Assert.Empty(QuickNoteFindController.FindAllMatches(null!, "test"));
        Assert.Empty(QuickNoteFindController.FindAllMatches(new FlowDocument(), ""));
    }
}
