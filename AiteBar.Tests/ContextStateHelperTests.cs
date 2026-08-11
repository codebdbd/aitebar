using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AiteBar;
using Xunit;

namespace AiteBar.Tests;

[Collection("LocalizationStateTestCollection")]
public sealed class ContextStateHelperTests : IDisposable
{
    private readonly CultureInfo _originalCulture;
    private readonly CultureInfo _originalUICulture;
    private readonly string _originalResolvedCulture;

    public ContextStateHelperTests()
    {
        _originalCulture = CultureInfo.CurrentCulture;
        _originalUICulture = CultureInfo.CurrentUICulture;
        _originalResolvedCulture = LocalizationService.ResolvedCulture.Name;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en");
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en");
        LocalizationService.ApplyCulture("en");
    }

    public void Dispose()
    {
        CultureInfo.CurrentCulture = _originalCulture;
        CultureInfo.CurrentUICulture = _originalUICulture;
        LocalizationService.ApplyCulture(_originalResolvedCulture);
    }

    [Fact]
    public void NormalizeContexts_CreatesTenDefaultContextsWithOnlyZeroEnabled()
    {
        List<PanelContext> contexts = ContextStateHelper.NormalizeContexts([]);

        Assert.Equal(10, contexts.Count);
        Assert.Equal("context-0", contexts[0].Id);
        Assert.Equal("Panel 0", contexts[0].Name);
        Assert.True(contexts[0].IsEnabled);
        Assert.Equal("context-9", contexts[9].Id);
        Assert.Equal("Panel 9", contexts[9].Name);
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

        Assert.Equal("\uE8B7", normalized[0].IconGlyph);
        Assert.Equal("\uE123", normalized[1].IconGlyph);
        Assert.Equal("\uE456", normalized[2].IconGlyph);
        Assert.Equal("\uE8B7", normalized[3].IconGlyph);
    }

    [Fact]
    public void NormalizeContexts_UsesFixedPaletteColors()
    {
        List<PanelContext> contexts =
        [
            new() { Id = "context-1", Name = "Контекст 1", Color = "#FF0000" },
            new() { Id = "context-2", Name = "Контекст 2", Color = "#00FF00" }
        ];

        List<PanelContext> normalized = ContextStateHelper.NormalizeContexts(contexts);

        Assert.Equal("#3B82F6", normalized[0].Color);
        Assert.Equal("#22C55E", normalized[1].Color);
        Assert.Equal("#F97316", normalized[2].Color);
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

        Assert.Equal("Панель 0", normalized[0].Name);
        Assert.Equal("Панель 1", normalized[1].Name);
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

        Assert.Equal("Work", normalized[1].Name);
        Assert.True(normalized[1].IsNameCustomized);
    }

    [Fact]
    public void GetContextListDisplayName_AlwaysIncludesPanelNumber()
    {
        var defaultContext = new PanelContext { Id = "context-1", Name = "Panel 1", IsNameCustomized = false };
        var customContext = new PanelContext { Id = "context-4", Name = "Drawing", IsNameCustomized = true };

        Assert.Equal("Panel 1", ContextStateHelper.GetContextListDisplayName(defaultContext));
        Assert.Equal("Panel 4 · Drawing", ContextStateHelper.GetContextListDisplayName(customContext));
    }

    [Fact]
    public void ContextReorder_NormalizeContexts_PreservesInputOrderAndStableIds()
    {
        List<PanelContext> contexts =
        [
            new() { Id = "context-4", Name = "Design", IsNameCustomized = true, IsEnabled = true },
            new() { Id = "context-1", Name = "Panel 1", IsNameCustomized = false, IsEnabled = true },
            new() { Id = "context-0", Name = "Panel 0", IsNameCustomized = false, IsEnabled = true },
            new() { Id = "context-2", Name = "Panel 2", IsNameCustomized = false, IsEnabled = false },
            new() { Id = "context-3", Name = "Panel 3", IsNameCustomized = false, IsEnabled = false },
            new() { Id = "context-5", Name = "Panel 5", IsNameCustomized = false, IsEnabled = false },
            new() { Id = "context-6", Name = "Panel 6", IsNameCustomized = false, IsEnabled = false },
            new() { Id = "context-7", Name = "Panel 7", IsNameCustomized = false, IsEnabled = false },
            new() { Id = "context-8", Name = "Panel 8", IsNameCustomized = false, IsEnabled = false },
            new() { Id = "context-9", Name = "Panel 9", IsNameCustomized = false, IsEnabled = false }
        ];

        List<PanelContext> normalized = ContextStateHelper.NormalizeContexts(contexts);

        Assert.Equal(["context-4", "context-1", "context-0"], normalized.Take(3).Select(context => context.Id).ToArray());
        Assert.Equal("Design", normalized[0].Name);
        Assert.Equal("Panel 1", normalized[1].Name);
        Assert.True(normalized[0].IsEnabled);
        Assert.Equal("#3B82F6", normalized[0].Color);
        Assert.Equal("#22C55E", normalized[1].Color);
    }

    [Fact]
    public void ContextReorder_GetContextDisplayNumber_UsesListPosition()
    {
        List<PanelContext> contexts =
        [
            new() { Id = "context-4" },
            new() { Id = "context-1" },
            new() { Id = "context-0" }
        ];

        Assert.Equal(0, ContextStateHelper.GetContextDisplayNumber(contexts, "context-4"));
        Assert.Equal(2, ContextStateHelper.GetContextDisplayNumber(contexts, "context-0"));
    }

    [Theory]
    [InlineData("Panel 1", 1, true)]
    [InlineData("Панель 1", 1, true)]
    [InlineData("Leiste 1", 1, true)]
    [InlineData("Work", 1, false)]
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

    [Fact]
    public void EnabledContextQueries_DoNotAllocateTemporaryCollections()
    {
        PanelContext[] contexts =
        [
            new() { Id = "context-1", IsEnabled = true },
            new() { Id = "context-2", IsEnabled = false },
            new() { Id = "context-3", IsEnabled = true }
        ];

        for (int i = 0; i < 100; i++)
        {
            _ = ContextStateHelper.CountEnabledContexts(contexts);
            _ = ContextStateHelper.FindEnabledContextIndex(contexts, "context-3");
            _ = ContextStateHelper.GetEnabledContextAt(contexts, 1);
            _ = ContextStateHelper.GetRelativeEnabledContextId("context-1", contexts, 1);
        }

        long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1_000; i++)
        {
            _ = ContextStateHelper.CountEnabledContexts(contexts);
            _ = ContextStateHelper.FindEnabledContextIndex(contexts, "context-3");
            _ = ContextStateHelper.GetEnabledContextAt(contexts, 1);
            _ = ContextStateHelper.GetRelativeEnabledContextId("context-1", contexts, 1);
        }
        long allocatedAfter = GC.GetAllocatedBytesForCurrentThread();

        Assert.Equal(allocatedBefore, allocatedAfter);
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
