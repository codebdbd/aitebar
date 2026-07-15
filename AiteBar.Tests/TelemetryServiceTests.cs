using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
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
        Environment.SetEnvironmentVariable("AITEBAR_ENVIRONMENT", null);
        Environment.SetEnvironmentVariable("AITEBAR_TRACES_SAMPLE_RATE", null);
        Environment.SetEnvironmentVariable("AITEBAR_SEND_PII", null);
    }

    public void Dispose()
    {
        TelemetryService.Shutdown();
        Environment.SetEnvironmentVariable("AITEBAR_SENTRY_DSN", null);
        Environment.SetEnvironmentVariable("SENTRY_DSN", null);
        Environment.SetEnvironmentVariable("AITEBAR_ENVIRONMENT", null);
        Environment.SetEnvironmentVariable("AITEBAR_TRACES_SAMPLE_RATE", null);
        Environment.SetEnvironmentVariable("AITEBAR_SEND_PII", null);
    }

    [Fact]
    public async Task Initialize_WithoutDsn_DoesNotEnable()
    {
        await TelemetryService.InitializeAsync();
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
    public async Task Initialize_CanBeCalledMultipleTimes()
    {
        await TelemetryService.InitializeAsync();
        await TelemetryService.InitializeAsync();
        await TelemetryService.InitializeAsync();
        Assert.False(TelemetryService.IsEnabled);
    }

    [Fact]
    public async Task Initialize_ConcurrentCallersAwaitTheSamePendingInitialization()
    {
        await WithSettingsFile(
            new AppSettings
            {
                Sentry = new SentrySettings
                {
                    IsEnabled = true,
                    Dsn = "https://public@example.com/1"
                }
            },
            async () =>
            {
                Task firstInitialization;
                Task secondInitialization;
                using (File.Open(PathHelper.SettingsFile, FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    firstInitialization = TelemetryService.InitializeAsync();
                    await Task.Delay(25);
                    secondInitialization = TelemetryService.InitializeAsync();

                    Assert.Same(firstInitialization, secondInitialization);
                    Assert.False(secondInitialization.IsCompleted);
                }

                await Task.WhenAll(firstInitialization, secondInitialization);
                Assert.True(TelemetryService.IsEnabled);
            });
    }

    [Fact]
    public async Task Shutdown_DuringPendingInitializationPreventsLateEnablement()
    {
        await WithSettingsFile(
            new AppSettings
            {
                Sentry = new SentrySettings
                {
                    IsEnabled = true,
                    Dsn = "https://public@example.com/1"
                }
            },
            async () =>
            {
                Task initialization;
                using (File.Open(PathHelper.SettingsFile, FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    initialization = TelemetryService.InitializeAsync();
                    await Task.Delay(25);
                    Assert.False(initialization.IsCompleted);

                    TelemetryService.Shutdown();
                }

                await initialization;
                Assert.False(TelemetryService.IsEnabled);
            });
    }

    [Fact]
    public async Task Initialize_WithEnvironmentDsn_EnablesTelemetry()
    {
        Environment.SetEnvironmentVariable("AITEBAR_SENTRY_DSN", "https://public@example.com/1");
        Environment.SetEnvironmentVariable("AITEBAR_ENVIRONMENT", "qa");
        Environment.SetEnvironmentVariable("AITEBAR_TRACES_SAMPLE_RATE", "0.5");
        Environment.SetEnvironmentVariable("AITEBAR_SEND_PII", "true");

        await TelemetryService.InitializeAsync();

        Assert.True(TelemetryService.IsEnabled);
    }

    [Fact]
    public async Task Shutdown_AllowsTelemetryToBeInitializedAgain()
    {
        await TelemetryService.InitializeAsync();
        Assert.False(TelemetryService.IsEnabled);

        TelemetryService.Shutdown();
        Environment.SetEnvironmentVariable("AITEBAR_SENTRY_DSN", "https://public@example.com/1");

        await TelemetryService.InitializeAsync();

        Assert.True(TelemetryService.IsEnabled);
    }

    [Fact]
    public async Task Initialize_UsesLegacySentryDsnEnvironmentVariable()
    {
        Environment.SetEnvironmentVariable("SENTRY_DSN", "https://public@example.com/1");

        await TelemetryService.InitializeAsync();

        Assert.True(TelemetryService.IsEnabled);
    }

    [Fact]
    public async Task Initialize_UsesSettingsFileWhenEnabled()
    {
        await WithSettingsFile(
            new AppSettings
            {
                Sentry = new SentrySettings
                {
                    IsEnabled = true,
                    Dsn = "https://public@example.com/1",
                    Environment = "staging",
                    TracesSampleRate = 0.25,
                    SendDefaultPii = true
                }
            },
            async () =>
            {
                await TelemetryService.InitializeAsync();

                Assert.True(TelemetryService.IsEnabled);
            });
    }

    [Fact]
    public async Task Initialize_WithInvalidSettingsFile_DoesNotEnable()
    {
        await WithRawSettingsFile("{ not valid json", async () =>
        {
            await TelemetryService.InitializeAsync();

            Assert.False(TelemetryService.IsEnabled);
        });
    }

    [Fact]
    public async Task CaptureMessage_WhenEnabled_DoesNotThrow()
    {
        Environment.SetEnvironmentVariable("AITEBAR_SENTRY_DSN", "https://public@example.com/1");
        await TelemetryService.InitializeAsync();

        Exception? ex = Record.Exception(() => TelemetryService.CaptureMessage("enabled message"));

        Assert.Null(ex);
    }

    [Fact]
    public async Task CaptureException_WhenEnabled_WithSparseContext_DoesNotThrow()
    {
        Environment.SetEnvironmentVariable("AITEBAR_SENTRY_DSN", "https://public@example.com/1");
        await TelemetryService.InitializeAsync();

        var context = new Dictionary<string, string?>
        {
            ["feature"] = "coverage",
            ["ignored-empty"] = "",
            [""] = "ignored-key"
        };

        Exception? ex = Record.Exception(() => TelemetryService.CaptureException(
            new Exception("enabled exception"),
            "telemetry_test",
            context));

        Assert.Null(ex);
    }

    [Fact]
    public async Task Flush_WhenEnabled_DoesNotThrow()
    {
        Environment.SetEnvironmentVariable("AITEBAR_SENTRY_DSN", "https://public@example.com/1");
        await TelemetryService.InitializeAsync();

        Exception? ex = Record.Exception(() => TelemetryService.Flush(TimeSpan.Zero));

        Assert.Null(ex);
    }

    private static async Task WithSettingsFile(AppSettings settings, Func<Task> assertion) =>
        await WithRawSettingsFile(JsonSerializer.Serialize(settings), assertion);

    private static async Task WithRawSettingsFile(string content, Func<Task> assertion)
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), "AiteBarTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        PathHelper.SetAppDataFolderOverride(tempRoot);

        try
        {
            File.WriteAllText(PathHelper.SettingsFile, content);
            await assertion();
        }
        finally
        {
            TelemetryService.Shutdown();
            PathHelper.ClearAppDataFolderOverride();
            try
            {
                Directory.Delete(tempRoot, recursive: true);
            }
            catch
            {
            }
        }
    }
}
