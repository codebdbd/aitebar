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
            var enabledContexts = GetEnabledContexts(contexts);
            if (enabledContexts.Count == 0)
            {
                return GetDefaultContextId(0);
            }

            return enabledContexts.Any(context => string.Equals(context.Id, activeContextId, StringComparison.Ordinal))
                ? activeContextId!
                : enabledContexts[0].Id;
        }

        public static IReadOnlyList<PanelContext> GetEnabledContexts(IReadOnlyList<PanelContext> contexts)
        {
            var enabled = new List<PanelContext>(FixedContextCount);
            foreach (var context in contexts)
            {
                if (context.IsEnabled)
                {
                    enabled.Add(context);
                }
            }
            return enabled;
        }

        public static string? GetRelativeEnabledContextId(string activeContextId, IReadOnlyList<PanelContext> contexts, int direction)
        {
            var enabledContexts = GetEnabledContexts(contexts);
            if (enabledContexts.Count == 0)
            {
                return null;
            }

            int currentIndex = -1;
            for (int i = 0; i < enabledContexts.Count; i++)
            {
                if (string.Equals(enabledContexts[i].Id, activeContextId, StringComparison.Ordinal))
                {
                    currentIndex = i;
                    break;
                }
            }
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
