using System.Collections.Generic;
using AiteBar;
using Xunit;

namespace AiteBar.Tests;

public sealed class ContextStateHelperTests
{
    [Fact]
    public void NormalizeContexts_CreatesFourDefaultContexts()
    {
        List<PanelContext> contexts = ContextStateHelper.NormalizeContexts([]);

        Assert.Equal(4, contexts.Count);
        Assert.Equal("context-1", contexts[0].Id);
        Assert.Equal("Контекст 1", contexts[0].Name);
        Assert.Equal("context-4", contexts[3].Id);
        Assert.Equal("Контекст 4", contexts[3].Name);
    }

    [Fact]
    public void NormalizeActiveContextId_FallsBackToFirstContext()
    {
        List<PanelContext> contexts =
        [
            new() { Id = "context-1", Name = "Контекст 1" },
            new() { Id = "context-2", Name = "Контекст 2" }
        ];

        string activeContextId = ContextStateHelper.NormalizeActiveContextId("missing", contexts);

        Assert.Equal("context-1", activeContextId);
    }

    [Theory]
    [InlineData(4, 4, 0)]
    [InlineData(-1, 4, 3)]
    [InlineData(5, 4, 1)]
    public void WrapIndex_WrapsCyclically(int index, int count, int expected)
    {
        int actual = ContextStateHelper.WrapIndex(index, count);

        Assert.Equal(expected, actual);
    }
}
