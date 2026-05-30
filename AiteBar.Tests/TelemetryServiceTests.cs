using AiteBar;
using Xunit;

namespace AiteBar.Tests;

public sealed class TelemetryServiceTests : IDisposable
{
    public TelemetryServiceTests()
    {
        TelemetryService.Shutdown();
        Environment.SetEnvironmentVariable("AITEBAR_SENTRY_DSN", null);
        Environment.SetEnvironmentVariable("SENTRY_DSN", null);
    }

    public void Dispose()
    {
        TelemetryService.Shutdown();
        Environment.SetEnvironmentVariable("AITEBAR_SENTRY_DSN", null);
        Environment.SetEnvironmentVariable("SENTRY_DSN", null);
    }

    [Fact]
    public void Initialize_WithoutDsn_DoesNotEnable()
    {
        TelemetryService.Initialize();
        Assert.False(TelemetryService.IsEnabled);
    }

    [Fact]
    public void CaptureMessage_WhenDisabled_DoesNotThrow()
    {
        TelemetryService.Shutdown();
        var ex = Record.Exception(() => TelemetryService.CaptureMessage("test message"));
        Assert.Null(ex);
    }

    [Fact]
    public void CaptureException_WhenDisabled_DoesNotThrow()
    {
        TelemetryService.Shutdown();
        var ex = Record.Exception(() => TelemetryService.CaptureException(new Exception("test exception")));
        Assert.Null(ex);
    }

    [Fact]
    public void CaptureException_WithOperation_WhenDisabled_DoesNotThrow()
    {
        TelemetryService.Shutdown();
        var ex = Record.Exception(() => TelemetryService.CaptureException(
            new Exception("test exception"),
            "test_operation"));
        Assert.Null(ex);
    }

    [Fact]
    public void CaptureException_WithContext_WhenDisabled_DoesNotThrow()
    {
        TelemetryService.Shutdown();
        var context = new System.Collections.Generic.Dictionary<string, string?>
        {
            { "key1", "value1" },
            { "key2", "value2" }
        };
        var ex = Record.Exception(() => TelemetryService.CaptureException(
            new Exception("test exception"),
            "test_operation",
            context));
        Assert.Null(ex);
    }

    [Fact]
    public void Flush_WhenDisabled_DoesNotThrow()
    {
        TelemetryService.Shutdown();
        var ex = Record.Exception(() => TelemetryService.Flush(System.TimeSpan.FromSeconds(1)));
        Assert.Null(ex);
    }

    [Fact]
    public void Shutdown_CanBeCalledMultipleTimes()
    {
        TelemetryService.Shutdown();
        TelemetryService.Shutdown();
        TelemetryService.Shutdown();
        Assert.False(TelemetryService.IsEnabled);
    }

    [Fact]
    public void Initialize_CanBeCalledMultipleTimes()
    {
        TelemetryService.Initialize();
        TelemetryService.Initialize();
        TelemetryService.Initialize();
        Assert.False(TelemetryService.IsEnabled);
    }
}
