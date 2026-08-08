using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace AiteBar
{
    internal static class ContextStateHelper
    {
        public const int FixedContextCount = 10;
        public const string DefaultContextPrefix = "Panel ";
        private static readonly CultureInfo[] LocalizedCultures =
        [
            CultureInfo.GetCultureInfo("en"),
            CultureInfo.GetCultureInfo("de"),
            CultureInfo.GetCultureInfo("uk"),
            CultureInfo.GetCultureInfo("ru")
        ];
        
        // Фиксированные цвета для каждого контекста (хорошо видны белые цифры)
        private static readonly string[] DefaultContextColors =
        [
            "#3B82F6", "#22C55E", "#F97316", "#A855F7", "#06B6D4",
            "#F43F5E", "#84CC16", "#EAB308", "#EC4899", "#14B8A6"
        ];

        public static string GetDefaultContextId(int number) => $"context-{number}";

        public static string GetDefaultContextName(int index) => GetDefaultContextName(index, LocalizationService.ResolvedCulture);

        public static string GetDefaultContextName(int number, CultureInfo culture) => LocalizationService.Format("Panel_DefaultNameFormat", culture, number);

        // Получить фиксированный цвет для контекста по индексу
        public static string GetContextColor(int index) => DefaultContextColors[index % DefaultContextColors.Length];

        public static List<PanelContext> NormalizeContexts(IReadOnlyList<PanelContext>? source)
        {
            return NormalizeContexts(source, LocalizationService.ResolvedCulture);
        }

        public static List<PanelContext> NormalizeContexts(IReadOnlyList<PanelContext>? source, CultureInfo culture)
        {
            var normalized = new List<PanelContext>(FixedContextCount);
            var existingById = (source ?? []).Where(context => !string.IsNullOrWhiteSpace(context.Id))
                .GroupBy(context => context.Id, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

            for (int number = 0; number < FixedContextCount; number++)
            {
                string id = GetDefaultContextId(number);
                existingById.TryGetValue(id, out PanelContext? existing);
                bool isNameCustomized = DetermineIsNameCustomized(existing, number);
                string name = isNameCustomized
                    ? existing!.Name.Trim()
                    : GetDefaultContextName(number, culture);

                // Цвет всегда фиксированный для каждого индекса
                normalized.Add(new PanelContext
                {
                    Id = id,
                    Name = string.IsNullOrWhiteSpace(name) ? GetDefaultContextName(number, culture) : name,
                    IsNameCustomized = isNameCustomized,
                    IconGlyph = string.IsNullOrWhiteSpace(existing?.IconGlyph) ? "\uE8B7" : existing.IconGlyph,
                    IsEnabled = number == 0 || (existing?.IsEnabled ?? false),
                    Color = GetContextColor(number)
                });
            }

            return normalized;
        }

        public static string NormalizeActiveContextId(string? activeContextId, IReadOnlyList<PanelContext> contexts)
        {
            int activeIndex = FindEnabledContextIndex(contexts, activeContextId);
            if (activeIndex >= 0)
            {
                return activeContextId!;
            }

            return GetEnabledContextAt(contexts, 0)?.Id ?? GetDefaultContextId(0);
        }

        public static int CountEnabledContexts(IReadOnlyList<PanelContext> contexts)
        {
            int count = 0;
            for (int i = 0; i < contexts.Count; i++)
            {
                if (contexts[i].IsEnabled)
                {
                    count++;
                }
            }

            return count;
        }

        public static int FindEnabledContextIndex(IReadOnlyList<PanelContext> contexts, string? contextId)
        {
            int enabledIndex = 0;
            for (int i = 0; i < contexts.Count; i++)
            {
                PanelContext context = contexts[i];
                if (!context.IsEnabled)
                {
                    continue;
                }

                if (string.Equals(context.Id, contextId, StringComparison.Ordinal))
                {
                    return enabledIndex;
                }

                enabledIndex++;
            }

            return -1;
        }

        public static PanelContext? GetEnabledContextAt(IReadOnlyList<PanelContext> contexts, int enabledIndex)
        {
            if (enabledIndex < 0)
            {
                return null;
            }

            int currentEnabledIndex = 0;
            for (int i = 0; i < contexts.Count; i++)
            {
                PanelContext context = contexts[i];
                if (!context.IsEnabled)
                {
                    continue;
                }

                if (currentEnabledIndex == enabledIndex)
                {
                    return context;
                }

                currentEnabledIndex++;
            }

            return null;
        }

        public static string? GetRelativeEnabledContextId(string activeContextId, IReadOnlyList<PanelContext> contexts, int direction)
        {
            int enabledCount = CountEnabledContexts(contexts);
            if (enabledCount == 0)
            {
                return null;
            }

            int currentIndex = FindEnabledContextIndex(contexts, activeContextId);
            if (currentIndex < 0)
            {
                currentIndex = 0;
            }

            int nextIndex = WrapIndex(currentIndex + direction, enabledCount);
            return GetEnabledContextAt(contexts, nextIndex)?.Id;
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

        public static int GetContextNumber(string? contextId)
        {
            const string prefix = "context-";
            return contextId != null && contextId.StartsWith(prefix, StringComparison.Ordinal) &&
                   int.TryParse(contextId[prefix.Length..], out int number) && number is >= 0 and < FixedContextCount
                ? number
                : 0;
        }

        public static bool IsDefaultContextName(string? name, int index)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return true;
            }

            string trimmed = name.Trim();
            return LocalizedCultures.Any(culture =>
                string.Equals(trimmed, GetDefaultContextName(index, culture), StringComparison.CurrentCulture));
        }

        public static bool IsCustomizedContextNameInput(string? name, int index) =>
            !string.IsNullOrWhiteSpace(name) && !IsDefaultContextName(name, index);

        private static bool DetermineIsNameCustomized(PanelContext? existing, int index)
        {
            if (existing == null)
            {
                return false;
            }

            if (existing.IsNameCustomized)
            {
                return !string.IsNullOrWhiteSpace(existing.Name);
            }

            return IsCustomizedContextNameInput(existing.Name, index);
        }
    }
}
