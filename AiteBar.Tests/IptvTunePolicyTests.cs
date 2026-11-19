using System;
using AiteBar.IptvPlayer;
using Xunit;

namespace AiteBar.Tests;

public sealed class IptvTunePolicyTests
{
    [Fact]
    public void FallbackWrapsToEarlierVariantsAndVisitsEachOnce()
    {
        Assert.Equal(0, IptvTunePolicy.NextVariant(2, 3, 1, TimeSpan.Zero, retryable: false));
        Assert.Equal(1, IptvTunePolicy.NextVariant(0, 3, 2, TimeSpan.Zero, retryable: false));
        Assert.Null(IptvTunePolicy.NextVariant(1, 3, 3, TimeSpan.Zero, retryable: true));
    }

    [Fact]
    public void SingleStreamRetriesAtMostOnceAfterTimeoutOrInterruption()
    {
        Assert.Null(IptvTunePolicy.NextVariant(0, 1, 1, TimeSpan.Zero, retryable: false));
        Assert.Equal(0, IptvTunePolicy.NextVariant(0, 1, 1, TimeSpan.FromSeconds(6), retryable: true));
        Assert.Null(IptvTunePolicy.NextVariant(0, 1, 2, TimeSpan.FromSeconds(7), retryable: true));
    }

    [Fact]
    public void AllAttemptsShareTheSameBudget()
    {
        Assert.Equal(TimeSpan.FromSeconds(4), IptvTunePolicy.AttemptTimeout(TimeSpan.Zero, true, false));
        Assert.Equal(TimeSpan.FromSeconds(6), IptvTunePolicy.AttemptTimeout(TimeSpan.Zero, false, true));
        Assert.Equal(TimeSpan.FromSeconds(1), IptvTunePolicy.AttemptTimeout(TimeSpan.FromSeconds(11), false, false));
        Assert.Null(IptvTunePolicy.NextVariant(0, 3, 1, IptvTunePolicy.Budget, retryable: true));
    }
}
