using System;
using System.Linq;

namespace AiteBar.Tests;

public sealed class UtilityButtonCatalogTests
{
    private static readonly string[] ExpectedIds =
    [
        "Search",
        "Screenshot",
        "Record",
        "Calc",
        "Explorer",
        "Downloads",
        "FileSorter",
        "IconConverter",
        "TimerStopwatch",
        "ColorPicker",
        "QuickNote",
        "QRCodeGenerator",
        "ClipboardManager",
        "ShowDesktop",
        "AppsFolder",
        "Copilot",
        "TextProcessing",
        "PromptBuilder",
        "ZenEditor"
    ];

    [Fact]
    public void All_DefinesUniqueStableIdsAndCompleteMetadataInFallbackOrder()
    {
        Assert.Equal(ExpectedIds, UtilityButtonCatalog.All.Select(definition => definition.Id));
        Assert.Equal(
            UtilityButtonCatalog.All.Count,
            UtilityButtonCatalog.All.Select(definition => definition.Id).Distinct(StringComparer.Ordinal).Count());

        Assert.All(UtilityButtonCatalog.All, definition =>
        {
            Assert.False(string.IsNullOrWhiteSpace(definition.Icon));
            Assert.Matches("^#[0-9A-Fa-f]{6}$", definition.Color);
            Assert.False(string.IsNullOrWhiteSpace(definition.TooltipKey));
            Assert.True(UtilityButtonCatalog.TryGet(definition.Id, out UtilityButtonDefinition? resolved));
            Assert.Same(definition, resolved);
        });

        Assert.False(UtilityButtonCatalog.TryGet("search", out _));
        Assert.False(UtilityButtonCatalog.TryGet("Unknown", out _));
    }

    [Fact]
    public void VisibilityAccessors_RoundTripEachDefinitionWithoutChangingAnotherUtility()
    {
        var settings = new AppSettings();

        foreach (UtilityButtonDefinition target in UtilityButtonCatalog.All)
        {
            bool original = target.IsVisible(settings);
            var otherValues = UtilityButtonCatalog.All
                .Where(definition => !ReferenceEquals(definition, target))
                .ToDictionary(definition => definition.Id, definition => definition.IsVisible(settings), StringComparer.Ordinal);

            target.SetVisible(settings, !original);

            Assert.Equal(!original, target.IsVisible(settings));
            foreach (UtilityButtonDefinition other in UtilityButtonCatalog.All.Where(definition => !ReferenceEquals(definition, target)))
            {
                Assert.Equal(otherValues[other.Id], other.IsVisible(settings));
            }

            target.SetVisible(settings, original);
        }
    }

    [Fact]
    public void PresetUtilityColors_FollowSemanticGroups()
    {
        AssertSameColor(
            UtilityIconColors.FolderAccess,
            UtilityButtonCatalog.Explorer,
            UtilityButtonCatalog.Downloads,
            UtilityButtonCatalog.FileSorter,
            UtilityButtonCatalog.ShowDesktop,
            UtilityButtonCatalog.AppsFolder);

        AssertSameColor(
            UtilityIconColors.ScreenCapture,
            UtilityButtonCatalog.Screenshot,
            UtilityButtonCatalog.Record,
            UtilityButtonCatalog.ColorPicker);

        AssertSameColor(
            UtilityIconColors.AssetCreation,
            UtilityButtonCatalog.IconConverter,
            UtilityButtonCatalog.QRCodeGenerator);

        AssertSameColor(
            UtilityIconColors.TextWorkspace,
            UtilityButtonCatalog.QuickNote,
            UtilityButtonCatalog.ClipboardManager,
            UtilityButtonCatalog.TextProcessing,
            UtilityButtonCatalog.ZenEditor);

        AssertSameColor(
            UtilityIconColors.AiTools,
            UtilityButtonCatalog.Copilot,
            UtilityButtonCatalog.PromptBuilder);

        AssertSameColor(
            UtilityIconColors.Productivity,
            UtilityButtonCatalog.Calculator,
            UtilityButtonCatalog.TimerStopwatch);
    }

    [Fact]
    public void UtilityImplementations_ReuseCatalogGroupColors()
    {
        Assert.Equal(UtilityButtonCatalog.FileSorter.Color, new FileSorterUtility().IconColor);
        Assert.Equal(UtilityButtonCatalog.IconConverter.Color, new IconConverterUtility().IconColor);
        Assert.Equal(UtilityButtonCatalog.TimerStopwatch.Color, new TimerStopwatchUtility().IconColor);
        Assert.Equal(UtilityButtonCatalog.ColorPicker.Color, new ColorPickerUtility().IconColor);
        Assert.Equal(UtilityButtonCatalog.QuickNote.Color, new QuickNoteUtility().IconColor);
        Assert.Equal(UtilityButtonCatalog.QRCodeGenerator.Color, new QRCodeGeneratorUtility().IconColor);
        Assert.Equal(UtilityButtonCatalog.ClipboardManager.Color, new ClipboardManagerUtility().IconColor);
        Assert.Equal(UtilityButtonCatalog.TextProcessing.Color, new TextProcessingUtility().IconColor);
        Assert.Equal(UtilityButtonCatalog.PromptBuilder.Color, new PromptBuilderUtility().IconColor);
        Assert.Equal(UtilityButtonCatalog.ZenEditor.Color, new ZenEditorUtility().IconColor);
    }

    [Fact]
    public void UnifiedButtonService_UsesCatalogVisibilityAndStableOrderingOnlyInPrimaryContext()
    {
        var settingsService = new AppSettingsService();
        settingsService.UpdateSettings(settings =>
        {
            foreach (UtilityButtonDefinition definition in UtilityButtonCatalog.All)
            {
                definition.SetVisible(settings, false);
            }

            UtilityButtonCatalog.Search.SetVisible(settings, true);
            UtilityButtonCatalog.IconConverter.SetVisible(settings, true);
            settings.UtilityButtonOrder = ["IconConverter", "Search"];
        });

        var service = new UnifiedButtonService(settingsService);
        string primaryContextId = settingsService.GetPrimaryContextId();

        Assert.Equal(
            ["IconConverter", "Search"],
            service.BuildUnifiedList(primaryContextId).Select(button => button.Id));
        Assert.Empty(service.BuildUnifiedList("context-2"));
    }

    [Fact]
    public void UnifiedButtonService_SnapshotOverloadUsesOnlyProvidedSettingsAndElements()
    {
        var settingsService = new AppSettingsService();
        settingsService.UpdateSettings(settings =>
        {
            foreach (UtilityButtonDefinition definition in UtilityButtonCatalog.All)
            {
                definition.SetVisible(settings, false);
            }
        });

        AppSettings snapshot = settingsService.Settings;
        snapshot.Contexts = ContextStateHelper.NormalizeContexts(snapshot.Contexts);
        foreach (UtilityButtonDefinition definition in UtilityButtonCatalog.All)
        {
            definition.SetVisible(snapshot, false);
        }

        UtilityButtonCatalog.Search.SetVisible(snapshot, true);
        string primaryContextId = snapshot.Contexts[0].Id;
        CustomElement snapshotElement = new()
        {
            Id = "snapshot-button",
            Name = "Snapshot button",
            ContextId = primaryContextId
        };

        var service = new UnifiedButtonService(settingsService);

        Assert.Equal(
            ["Search", "snapshot-button"],
            service.BuildUnifiedList(primaryContextId, snapshot, [snapshotElement]).Select(button => button.Id));
    }

    [Fact]
    public void AppSettingsService_SetUtilityVisibilityUsesStableIdAndIgnoresUnknownId()
    {
        var service = new AppSettingsService();

        service.SetUtilityVisibility("Search", false);
        Assert.False(service.Settings.ShowPresetSearch);

        AppSettings beforeUnknown = service.Settings;
        service.SetUtilityVisibility("ShowPresetSearch", true);

        Assert.Equal(beforeUnknown.ShowPresetSearch, service.Settings.ShowPresetSearch);
        Assert.Equal(beforeUnknown.ShowPresetIconConverter, service.Settings.ShowPresetIconConverter);
    }

    private static void AssertSameColor(string expected, params UtilityButtonDefinition[] definitions)
    {
        Assert.All(definitions, definition => Assert.Equal(expected, definition.Color));
    }
}
