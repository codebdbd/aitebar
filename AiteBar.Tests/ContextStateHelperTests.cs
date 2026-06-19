using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AiteBar;
using Xunit;

namespace AiteBar.Tests;

public sealed class ContextStateHelperTests : IDisposable
{
    private readonly CultureInfo _originalCulture;
    private readonly CultureInfo _originalUICulture;

    public ContextStateHelperTests()
    {
        _originalCulture = CultureInfo.CurrentCulture;
        _originalUICulture = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en");
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en");
    }

    public void Dispose()
    {
        CultureInfo.CurrentCulture = _originalCulture;
        CultureInfo.CurrentUICulture = _originalUICulture;
    }

    [Fact]
    public void NormalizeContexts_CreatesEightDefaultContextsWithOnlyFirstEnabled()
    {
        List<PanelContext> contexts = ContextStateHelper.NormalizeContexts([]);

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
    public void NormalizeContexts_PreservesColor()
    {
        List<PanelContext> contexts =
        [
            new() { Id = "context-1", Name = "Контекст 1", Color = "#FF0000" },
            new() { Id = "context-2", Name = "Контекст 2", Color = "#00FF00" }
        ];

        List<PanelContext> normalized = ContextStateHelper.NormalizeContexts(contexts);

        Assert.Equal("#FF0000", normalized[0].Color);
        Assert.Equal("#00FF00", normalized[1].Color);
        Assert.Equal("#2A9CFF", normalized[2].Color); // Default color for new contexts
    }

    [Fact]
    public void NormalizeContexts_LocalizesLegacyDefaultNamesToCurrentCulture()
    {
        List<PanelContext> contexts =
        [
            new() { Id = "context-1", Name = "Panel 1", IsEnabled = true },
            new() { Id = "context-2", Name = "Panel 2", IsEnabled = true }
        ];

        List<PanelContext> normalized = ContextStateHelper.NormalizeContexts(contexts, CultureInfo.GetCultureInfo("ru"));

        Assert.Equal("Панель 1", normalized[0].Name);
        Assert.Equal("Панель 2", normalized[1].Name);
        Assert.False(normalized[0].IsNameCustomized);
        Assert.False(normalized[1].IsNameCustomized);
    }

    [Fact]
    public void NormalizeContexts_PreservesCustomNamesAcrossCultures()
    {
        List<PanelContext> contexts =
        [
            new() { Id = "context-1", Name = "Work", IsNameCustomized = true, IsEnabled = true }
        ];

        List<PanelContext> normalized = ContextStateHelper.NormalizeContexts(contexts, CultureInfo.GetCultureInfo("ru"));

        Assert.Equal("Work", normalized[0].Name);
        Assert.True(normalized[0].IsNameCustomized);
    }

    [Theory]
    [InlineData("Panel 1", 0, true)]
    [InlineData("Панель 1", 0, true)]
    [InlineData("Leiste 1", 0, true)]
    [InlineData("Work", 0, false)]
    public void IsDefaultContextName_DetectsLocalizedDefaults(string value, int index, bool expected)
    {
        Assert.Equal(expected, ContextStateHelper.IsDefaultContextName(value, index));
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

    [Fact]
    public void NormalizeActiveContextId_KeepsValidEnabledContext()
    {
        List<PanelContext> contexts =
        [
            new() { Id = "context-1", IsEnabled = true },
            new() { Id = "context-2", IsEnabled = true }
        ];

        string activeContextId = ContextStateHelper.NormalizeActiveContextId("context-2", contexts);

        Assert.Equal("context-2", activeContextId);
    }

    [Fact]
    public void GetRelativeEnabledContextId_WrapsFromLastToFirst()
    {
        List<PanelContext> contexts =
        [
            new() { Id = "context-1", IsEnabled = true },
            new() { Id = "context-2", IsEnabled = true },
            new() { Id = "context-3", IsEnabled = false }
        ];

        string? nextContextId = ContextStateHelper.GetRelativeEnabledContextId("context-2", contexts, 1);

        Assert.Equal("context-1", nextContextId);
    }

    [Fact]
    public void GetRelativeEnabledContextId_WrapsFromFirstToLast()
    {
        List<PanelContext> contexts =
        [
            new() { Id = "context-1", IsEnabled = true },
            new() { Id = "context-2", IsEnabled = false },
            new() { Id = "context-3", IsEnabled = true }
        ];

        string? previousContextId = ContextStateHelper.GetRelativeEnabledContextId("context-1", contexts, -1);

        Assert.Equal("context-3", previousContextId);
    }
}
