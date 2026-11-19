namespace AiteBar.IptvPlayer;

internal static class IptvTunePolicy
{
    internal static readonly TimeSpan Budget = TimeSpan.FromSeconds(12);

    internal static TimeSpan AttemptTimeout(TimeSpan elapsed, bool hasAlternative, bool canRetry)
    {
        var remaining = Budget - elapsed;
        var limit = hasAlternative ? TimeSpan.FromSeconds(4) :
            canRetry ? TimeSpan.FromSeconds(6) : Budget;
        return remaining < limit ? remaining : limit;
    }

    internal static int? NextVariant(int index, int count, int attempts, TimeSpan elapsed, bool retryable)
    {
        if (elapsed >= Budget) return null;
        if (attempts < count) return (index + 1) % count;
        return retryable && count <= 1 && attempts == 1 ? index : null;
    }
}
