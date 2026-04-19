using System;
using System.Collections.Generic;
using System.Linq;

namespace AiteBar
{
    internal static class ContextStateHelper
    {
        public const int FixedContextCount = 4;
        public const string DefaultContextPrefix = "Контекст ";

        public static string GetDefaultContextId(int index) => $"context-{index + 1}";

        public static string GetDefaultContextName(int index) => $"{DefaultContextPrefix}{index + 1}";

        public static List<PanelContext> NormalizeContexts(IReadOnlyList<PanelContext>? source)
        {
            var normalized = new List<PanelContext>(FixedContextCount);
            var usedIds = new HashSet<string>(StringComparer.Ordinal);

            for (int i = 0; i < FixedContextCount; i++)
            {
                PanelContext? existing = source != null && i < source.Count ? source[i] : null;
                string id = string.IsNullOrWhiteSpace(existing?.Id) ? GetDefaultContextId(i) : existing!.Id;
                string name = string.IsNullOrWhiteSpace(existing?.Name) ? GetDefaultContextName(i) : existing!.Name.Trim();

                if (!usedIds.Add(id) || !string.Equals(id, GetDefaultContextId(i), StringComparison.Ordinal))
                {
                    id = GetDefaultContextId(i);
                    usedIds.Add(id);
                }

                normalized.Add(new PanelContext
                {
                    Id = id,
                    Name = string.IsNullOrWhiteSpace(name) ? GetDefaultContextName(i) : name
                });
            }

            return normalized;
        }

        public static string NormalizeActiveContextId(string? activeContextId, IReadOnlyList<PanelContext> contexts)
        {
            if (contexts.Count == 0)
            {
                return GetDefaultContextId(0);
            }

            return contexts.Any(context => string.Equals(context.Id, activeContextId, StringComparison.Ordinal))
                ? activeContextId!
                : contexts[0].Id;
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
