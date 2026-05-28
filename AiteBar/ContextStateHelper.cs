using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace AiteBar
{
    internal static class ContextStateHelper
    {
        public const int FixedContextCount = 8;
        public const string DefaultContextPrefix = "Panel ";

        public static string GetDefaultContextId(int index) => $"context-{index + 1}";

        public static string GetDefaultContextName(int index) => GetDefaultContextName(index, CultureInfo.CurrentUICulture);

        public static string GetDefaultContextName(int index, CultureInfo culture) => LocalizationService.Format("Panel_DefaultNameFormat", culture, index + 1);

        public static List<PanelContext> NormalizeContexts(IReadOnlyList<PanelContext>? source)
        {
            return NormalizeContexts(source, CultureInfo.CurrentUICulture);
        }

        public static List<PanelContext> NormalizeContexts(IReadOnlyList<PanelContext>? source, CultureInfo culture)
        {
            var normalized = new List<PanelContext>(FixedContextCount);
            var usedIds = new HashSet<string>(StringComparer.Ordinal);

            for (int i = 0; i < FixedContextCount; i++)
            {
                PanelContext? existing = source != null && i < source.Count ? source[i] : null;
                string id = string.IsNullOrWhiteSpace(existing?.Id) ? GetDefaultContextId(i) : existing!.Id;
                string name = string.IsNullOrWhiteSpace(existing?.Name) ? GetDefaultContextName(i, culture) : existing!.Name.Trim();

                if (!usedIds.Add(id) || !string.Equals(id, GetDefaultContextId(i), StringComparison.Ordinal))
                {
                    id = GetDefaultContextId(i);
                    usedIds.Add(id);
                }

                normalized.Add(new PanelContext
                {
                    Id = id,
                    Name = string.IsNullOrWhiteSpace(name) ? GetDefaultContextName(i, culture) : name,
                    IconGlyph = string.IsNullOrWhiteSpace(existing?.IconGlyph) ? "\uE8B7" : existing.IconGlyph,
                    IsEnabled = i == 0 || (existing?.IsEnabled ?? false)
                });
            }

            return normalized;
        }

        public static string NormalizeActiveContextId(string? activeContextId, IReadOnlyList<PanelContext> contexts)
        {
            var enabledContexts = GetEnabledContexts(contexts);
            if (enabledContexts.Count == 0)
            {
                return GetDefaultContextId(0);
            }

            return enabledContexts.Any(context => string.Equals(context.Id, activeContextId, StringComparison.Ordinal))
                ? activeContextId!
                : enabledContexts[0].Id;
        }

        public static IReadOnlyList<PanelContext> GetEnabledContexts(IReadOnlyList<PanelContext> contexts) =>
            contexts.Where(context => context.IsEnabled).ToList();

        public static string? GetRelativeEnabledContextId(string activeContextId, IReadOnlyList<PanelContext> contexts, int direction)
        {
            var enabledContexts = GetEnabledContexts(contexts);
            if (enabledContexts.Count == 0)
            {
                return null;
            }

            int currentIndex = enabledContexts.ToList().FindIndex(context => string.Equals(context.Id, activeContextId, StringComparison.Ordinal));
            if (currentIndex < 0)
            {
                currentIndex = 0;
            }

            int nextIndex = WrapIndex(currentIndex + direction, enabledContexts.Count);
            return enabledContexts[nextIndex].Id;
        }

        public static int WrapIndex(int index, int count)
        {
            if (count == 0)
            {
                return 0;
            }

            int wrapped = index % count;
            return wrapped < 0 ? wrapped + count : wrapped;
        }
    }
}
