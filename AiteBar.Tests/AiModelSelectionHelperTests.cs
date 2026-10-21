using System;
using System.Collections.Generic;
using System.Linq;
using AiteBar;
using Xunit;

namespace AiteBar.Tests;

public sealed class AiModelSelectionHelperTests
{
    [Fact]
    public void IsEligibleModel_ValidFreeWritingModel_ReturnsTrue()
    {
        var model = new AiModelDescriptor(
            "test-provider",
            "gpt-4.1-mini",
            "GPT-4.1 Mini",
            AiCapabilities.Text,
            128_000,
            AiCostStatus.VerifiedFree);

        Assert.True(AiModelSelectionHelper.IsEligibleModel(model));
    }

    [Fact]
    public void IsEligibleModel_DeprecatedOrMissingCapabilities_ReturnsFalse()
    {
        var deprecatedModel = new AiModelDescriptor(
            "test",
            "old-model",
            "Old Model",
            AiCapabilities.Text,
            8192,
            AiCostStatus.VerifiedFree,
            IsDeprecated: true);

        var noTextModel = new AiModelDescriptor(
            "test",
            "audio-model",
            "Audio Model",
            AiCapabilities.None,
            8192,
            AiCostStatus.VerifiedFree);

        Assert.False(AiModelSelectionHelper.IsEligibleModel(deprecatedModel));
        Assert.False(AiModelSelectionHelper.IsEligibleModel(noTextModel));
    }

    [Fact]
    public void BuildLogicalModelItems_CollapsesSameProviderAndModelRoutes()
    {
        var models = new[]
        {
            new AiModelDescriptor(
                "provider-a",
                "model-1",
                "Model One",
                AiCapabilities.Text,
                8192,
                AiCostStatus.VerifiedFree),
            new AiModelDescriptor(
                "provider-a",
                "model-1",
                "Model One Alt",
                AiCapabilities.Text,
                16384,
                AiCostStatus.VerifiedFree)
        };

        IReadOnlyList<ModelItem> items = AiModelSelectionHelper.BuildLogicalModelItems(models);

        Assert.Single(items);
        Assert.Equal("provider-a", items[0].ProviderId);
        Assert.Equal("model-1", items[0].ModelId);
        Assert.Equal("Model One", items[0].Display);
        Assert.Equal(16384, items[0].ContextLength);
    }

    [Fact]
    public void BuildLogicalModelItems_DisambiguatesDuplicateNamesAcrossProviders()
    {
        var models = new[]
        {
            new AiModelDescriptor(
                "provider-1",
                "llama-3",
                "Llama 3",
                AiCapabilities.Text,
                8192,
                AiCostStatus.VerifiedFree),
            new AiModelDescriptor(
                "provider-2",
                "llama-3",
                "Llama 3",
                AiCapabilities.Text,
                8192,
                AiCostStatus.VerifiedFree)
        };

        IReadOnlyList<ModelItem> items = AiModelSelectionHelper.BuildLogicalModelItems(models);

        Assert.Equal(2, items.Count);
        Assert.Contains("—", items[0].FullDisplay ?? "");
        Assert.Contains("—", items[1].FullDisplay ?? "");
    }

    [Fact]
    public void NormalizeModelText_RemovesControlAndFormatCharacters()
    {
        string raw = "  Test\u200B\u0000Model  ";
        string normalized = AiModelSelectionHelper.NormalizeModelText(raw);

        Assert.Equal("TestModel", normalized);
    }
}
