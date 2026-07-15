using System;
using System.Collections.Generic;
using System.Linq;

namespace AiteBar;

internal sealed class UnifiedButtonService
{
    private readonly AppSettingsService _settingsService;

    public UnifiedButtonService(AppSettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    public List<UnifiedButton> BuildUnifiedList(string activeContextId)
    {
        AppSettings settings = _settingsService.Settings;
        IReadOnlyList<CustomElement> elements = _settingsService.Elements;
        return BuildUnifiedList(activeContextId, settings, elements);
    }

    internal List<UnifiedButton> BuildUnifiedList(
        string activeContextId,
        AppSettings settings,
        IReadOnlyList<CustomElement> elements)
    {
        var result = new List<UnifiedButton>();
        string primaryContextId = settings.Contexts.FirstOrDefault()?.Id ?? ContextStateHelper.GetDefaultContextId(0);
        bool isPrimaryContext = string.Equals(activeContextId, primaryContextId, StringComparison.Ordinal);

        // Add utilities only in primary context
        if (isPrimaryContext)
        {
            // Get visible utility definitions, ordered by UtilityButtonOrder, then the rest
            var visibleUtilityDefs = UtilityButtonCatalog.All.Where(definition => definition.IsVisible(settings)).ToList();

            // Order by UtilityButtonOrder if exists
            var orderedUtilityDefs = new List<UtilityButtonDefinition>();
            var remainingUtilityDefs = new List<UtilityButtonDefinition>(visibleUtilityDefs);

            foreach (var id in settings.UtilityButtonOrder)
            {
                var def = remainingUtilityDefs.FirstOrDefault(d => d.Id == id);
                if (def != null)
                {
                    orderedUtilityDefs.Add(def);
                    remainingUtilityDefs.Remove(def);
                }
            }

            orderedUtilityDefs.AddRange(remainingUtilityDefs);

            foreach (var def in orderedUtilityDefs)
            {
                result.Add(new UnifiedButton
                {
                    Id = def.Id,
                    Name = LocalizationService.Get(def.TooltipKey),
                    Icon = def.Icon,
                    IconFont = FontHelper.FluentKey,
                    Color = def.Color,
                    Type = UnifiedButtonType.Utility,
                    Order = result.Count,
                    IsVisible = true
                });
            }
        }

        // Add user buttons
        var userElements = elements
            .Where(e => e.ContextId == activeContextId)
            .ToList();
        foreach (var el in userElements)
        {
            result.Add(new UnifiedButton
            {
                Id = el.Id,
                Name = el.Name,
                Icon = el.Icon,
                IconFont = el.IconFont,
                Color = el.Color,
                ImagePath = el.ImagePath,
                Type = UnifiedButtonType.User,
                Order = result.Count,
                SourceElement = el
            });
        }

        return result;
    }
}
