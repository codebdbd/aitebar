using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace AiteBar;

internal static class AiModelSelectionHelper
{
    public static bool IsEligibleModel(AiModelDescriptor model) =>
        HasVisibleModelText(model.ModelId) &&
        !model.IsDeprecated &&
        (model.Capabilities & AiCapabilities.Text) == AiCapabilities.Text &&
        (model.CostStatus is AiCostStatus.VerifiedFree or AiCostStatus.FreeTierAvailable) &&
        TextProcessingModelPolicy.Classify(model) != TextProcessingModelTier.Unsupported;

    public static bool HasVisibleModelText(string? value) =>
        !string.IsNullOrEmpty(value) && value.Any(character =>
            !char.IsWhiteSpace(character) &&
            !char.IsControl(character) &&
            CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.Format);

    public static string NormalizeModelText(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }
        return new string(value.Where(character =>
            !char.IsControl(character) &&
            CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.Format).ToArray()).Trim();
    }

    public static IReadOnlyList<ModelItem> BuildLogicalModelItems(
        IEnumerable<AiModelDescriptor> models,
        IReadOnlySet<string>? preferredIdentities = null)
    {
        ArgumentNullException.ThrowIfNull(models);
        var grouped = new Dictionary<string, List<AiModelDescriptor>>(StringComparer.OrdinalIgnoreCase);
        foreach (AiModelDescriptor model in models.Where(IsEligibleModel))
        {
            string identity = CreateModelIdentity(model.ProviderId, model.ModelId);
            if (!grouped.TryGetValue(identity, out List<AiModelDescriptor>? routes))
            {
                routes = [];
                grouped.Add(identity, routes);
            }
            routes.Add(model);
        }

        var items = grouped.Select(pair =>
        {
            AiModelDescriptor first = pair.Value[0];
            string display = pair.Value
                .Select(model => NormalizeModelText(model.DisplayName))
                .FirstOrDefault(HasVisibleModelText) ?? NormalizeModelText(first.ModelId);
            int? contextLength = pair.Value.Any(model => !model.ContextLength.HasValue)
                ? null
                : pair.Value.Max(model => model.ContextLength);
            return new
            {
                Identity = pair.Key,
                Item = new ModelItem(
                    first.ProviderId,
                    first.ModelId,
                    display,
                    contextLength,
                    TextProcessingModelPolicy.Classify(first))
            };
        }).ToList();

        var duplicateDisplays = items
            .GroupBy(entry => entry.Item.Display, StringComparer.CurrentCultureIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToHashSet(StringComparer.CurrentCultureIgnoreCase);

        return items
            .Select(entry =>
            {
                string fullDisplay = duplicateDisplays.Contains(entry.Item.Display)
                    ? $"{entry.Item.Display} — {GetProviderDisplayName(entry.Item.ProviderId)}"
                    : entry.Item.Display;
                return new
                {
                    entry.Identity,
                    IsPreferred = preferredIdentities?.Contains(entry.Identity) == true,
                    Item = entry.Item with { FullDisplay = fullDisplay }
                };
            })
            .OrderByDescending(entry => entry.IsPreferred)
            .ThenBy(entry => entry.Item.FullDisplay, StringComparer.CurrentCultureIgnoreCase)
            .Select(entry => entry.Item)
            .ToArray();
    }

    public static string CreateModelIdentity(string? providerId, string? modelId) =>
        $"{providerId?.Trim()}\n{modelId?.Trim()}";

    public static string GetProviderDisplayName(string? providerId) =>
        providerId != null && AiProviderCatalog.TryGet(providerId, out AiProviderDefinition definition)
            ? definition.DisplayName
            : providerId ?? string.Empty;
}
