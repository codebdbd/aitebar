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
            "#2563EB",   // Синий
            "#059669",   // Зелёный
            "#D97706",   // Оранжевый
            "#7C3AED",   // Фиолетовый
            "#0891B2",   // Голубой (циан)
            "#BE123C",   // Красный
            "#4D7C0F",   // Тёмно-зелёный
            "#6D28D9"    // Тёмно-фиолетовый
        ];

        public static string GetDefaultContextId(int index) => $"context-{index + 1}";

        public static string GetDefaultContextName(int index) => GetDefaultContextName(index, LocalizationService.ResolvedCulture);

        public static string GetDefaultContextName(int index, CultureInfo culture) => LocalizationService.Format("Panel_DefaultNameFormat", culture, index + 1);

        // Получить фиксированный цвет для контекста по индексу
        public static string GetContextColor(int index) => DefaultContextColors[index % DefaultContextColors.Length];

        public static List<PanelContext> NormalizeContexts(IReadOnlyList<PanelContext>? source)
        {
            return NormalizeContexts(source, LocalizationService.ResolvedCulture);
        }

        public static List<PanelContext> NormalizeContexts(IReadOnlyList<PanelContext>? source, CultureInfo culture)
        {
            var normalized = new List<PanelContext>(FixedContextCount);
            var usedIds = new HashSet<string>(StringComparer.Ordinal);

            for (int i = 0; i < FixedContextCount; i++)
            {
                PanelContext? existing = source != null && i < source.Count ? source[i] : null;
                string id = string.IsNullOrWhiteSpace(existing?.Id) ? GetDefaultContextId(i) : existing!.Id;
                bool isNameCustomized = DetermineIsNameCustomized(existing, i);
                string name = isNameCustomized
                    ? existing!.Name.Trim()
                    : GetDefaultContextName(i, culture);

                if (!usedIds.Add(id) || !string.Equals(id, GetDefaultContextId(i), StringComparison.Ordinal))
                {
                    id = GetDefaultContextId(i);
                    usedIds.Add(id);
                }

                // Цвет всегда фиксированный для каждого индекса
                normalized.Add(new PanelContext
                {
                    Id = id,
                    Name = string.IsNullOrWhiteSpace(name) ? GetDefaultContextName(i, culture) : name,
                    IsNameCustomized = isNameCustomized,
                    IconGlyph = string.IsNullOrWhiteSpace(existing?.IconGlyph) ? "\uE8B7" : existing.IconGlyph,
                    IsEnabled = i == 0 || (existing?.IsEnabled ?? false),
                    Color = GetContextColor(i)
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
