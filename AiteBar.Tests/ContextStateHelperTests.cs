using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AiteBar;
using Xunit;

namespace AiteBar.Tests;

public sealed class ContextStateHelperTests
{
    private static readonly CultureInfo EnglishCulture = CultureInfo.GetCultureInfo("en");

    [Fact]
    public void NormalizeContexts_CreatesEightDefaultContextsWithOnlyFirstEnabled()
    {
        List<PanelContext> contexts = ContextStateHelper.NormalizeContexts([], EnglishCulture);

        Assert.Equal(8, contexts.Count);
        Assert.Equal("context-1", contexts[0].Id);
        Assert.Equal("Panel 1", contexts[0].Name);
        Assert.True(contexts[0].IsEnabled);
        Assert.Equal("context-8", contexts[7].Id);
        Assert.Equal("Panel 8", contexts[7].Name);
        Assert.All(contexts.Skip(1), context => Assert.False(context.IsEnabled));
    }

    [Fact]
    public void NormalizeActiveContextId_FallsBackToFirstContext()
    {
        List<PanelContext> contexts =
        [
            new() { Id = "context-1", Name = "Контекст 1", IsEnabled = true },
            new() { Id = "context-2", Name = "Контекст 2", IsEnabled = true }
        ];

        string activeContextId = ContextStateHelper.NormalizeActiveContextId("missing", contexts);

        Assert.Equal("context-1", activeContextId);
    }

    [Fact]
    public void NormalizeActiveContextId_FallsBackWhenCurrentContextDisabled()
    {
        List<PanelContext> contexts =
        [
            new() { Id = "context-1", Name = "Контекст 1", IsEnabled = true },
            new() { Id = "context-2", Name = "Контекст 2", IsEnabled = false }
        ];

        string activeContextId = ContextStateHelper.NormalizeActiveContextId("context-2", contexts);

        Assert.Equal("context-1", activeContextId);
    }

    [Fact]
    public void NormalizeContexts_PreservesIconGlyph()
    {
        List<PanelContext> contexts =
        [
            new() { Id = "context-1", Name = "Контекст 1", IconGlyph = "\uE123" },
            new() { Id = "context-2", Name = "Контекст 2", IconGlyph = "\uE456" }
        ];

        List<PanelContext> normalized = ContextStateHelper.NormalizeContexts(contexts);

        Assert.Equal("\uE123", normalized[0].IconGlyph);
        Assert.Equal("\uE456", normalized[1].IconGlyph);
        Assert.Equal("\uE8B7", normalized[2].IconGlyph);
        Assert.Equal("\uE8B7", normalized[3].IconGlyph);
    }

    [Fact]
    public void GetRelativeEnabledContextId_SkipsDisabledContexts()
    {
        List<PanelContext> contexts =
        [
            new() { Id = "context-1", IsEnabled = true },
            new() { Id = "context-2", IsEnabled = false },
            new() { Id = "context-3", IsEnabled = true }
        ];

        string? nextContextId = ContextStateHelper.GetRelativeEnabledContextId("context-1", contexts, 1);

        Assert.Equal("context-3", nextContextId);
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
