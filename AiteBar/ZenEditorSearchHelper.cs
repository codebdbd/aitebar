namespace AiteBar;

internal static class ZenEditorSearchHelper
{
    public static int Find(string? text, string? query, int startIndex, bool forward)
    {
        string source = text ?? string.Empty;
        string value = query ?? string.Empty;
        if (source.Length == 0 || value.Length == 0 || value.Length > source.Length)
        {
            return -1;
        }

        const StringComparison comparison = StringComparison.OrdinalIgnoreCase;
        int maximumStart = source.Length - value.Length;
        if (forward)
        {
            int safeStart = Math.Clamp(startIndex, 0, source.Length);
            int found = source.IndexOf(value, safeStart, comparison);
            return found >= 0 || safeStart == 0
                ? found
                : source.IndexOf(value, 0, safeStart, comparison);
        }

        int safeBackwardStart = startIndex < 0
            ? maximumStart
            : Math.Clamp(startIndex, 0, maximumStart);
        int backwardEndExclusive = safeBackwardStart + value.Length;
        int backward = source.LastIndexOf(
            value,
            backwardEndExclusive - 1,
            backwardEndExclusive,
            comparison);
        if (backward >= 0 || safeBackwardStart == maximumStart)
        {
            return backward;
        }

        return source.LastIndexOf(
            value,
            source.Length - 1,
            source.Length,
            comparison);
    }
}
