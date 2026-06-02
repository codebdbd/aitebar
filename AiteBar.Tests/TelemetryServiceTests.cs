using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
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

    [Fact]
    public void Initialize_WithEnvironmentDsn_EnablesTelemetry()
    {
        Environment.SetEnvironmentVariable("AITEBAR_SENTRY_DSN", "https://public@example.com/1");
        Environment.SetEnvironmentVariable("AITEBAR_ENVIRONMENT", "qa");
        Environment.SetEnvironmentVariable("AITEBAR_TRACES_SAMPLE_RATE", "0.5");
        Environment.SetEnvironmentVariable("AITEBAR_SEND_PII", "true");

        TelemetryService.Initialize();

        Assert.True(TelemetryService.IsEnabled);
    }

    [Fact]
    public void Shutdown_AllowsTelemetryToBeInitializedAgain()
    {
        TelemetryService.Initialize();
        Assert.False(TelemetryService.IsEnabled);

        TelemetryService.Shutdown();
        Environment.SetEnvironmentVariable("AITEBAR_SENTRY_DSN", "https://public@example.com/1");

        TelemetryService.Initialize();

        Assert.True(TelemetryService.IsEnabled);
    }

    [Fact]
    public void Initialize_UsesLegacySentryDsnEnvironmentVariable()
    {
        Environment.SetEnvironmentVariable("SENTRY_DSN", "https://public@example.com/1");

        TelemetryService.Initialize();

        Assert.True(TelemetryService.IsEnabled);
    }

    [Fact]
    public void Initialize_UsesSettingsFileWhenEnabled()
    {
        WithSettingsFile(
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
            () =>
            {
                TelemetryService.Initialize();

                Assert.True(TelemetryService.IsEnabled);
            });
    }

    [Fact]
    public void Initialize_WithInvalidSettingsFile_DoesNotEnable()
    {
        WithRawSettingsFile("{ not valid json", () =>
        {
            TelemetryService.Initialize();

            Assert.False(TelemetryService.IsEnabled);
        });
    }

    [Fact]
    public void CaptureMessage_WhenEnabled_DoesNotThrow()
    {
        Environment.SetEnvironmentVariable("AITEBAR_SENTRY_DSN", "https://public@example.com/1");
        TelemetryService.Initialize();

        Exception? ex = Record.Exception(() => TelemetryService.CaptureMessage("enabled message"));

        Assert.Null(ex);
    }

    [Fact]
    public void CaptureException_WhenEnabled_WithSparseContext_DoesNotThrow()
    {
        Environment.SetEnvironmentVariable("AITEBAR_SENTRY_DSN", "https://public@example.com/1");
        TelemetryService.Initialize();

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
    public void Flush_WhenEnabled_DoesNotThrow()
    {
        Environment.SetEnvironmentVariable("AITEBAR_SENTRY_DSN", "https://public@example.com/1");
        TelemetryService.Initialize();

        Exception? ex = Record.Exception(() => TelemetryService.Flush(TimeSpan.Zero));

        Assert.Null(ex);
    }

    private static void WithSettingsFile(AppSettings settings, Action assertion) =>
        WithRawSettingsFile(JsonSerializer.Serialize(settings), assertion);

    private static void WithRawSettingsFile(string content, Action assertion)
    {
        string settingsPath = PathHelper.SettingsFile;
        string directory = Path.GetDirectoryName(settingsPath)!;
        string backupPath = Path.Combine(Path.GetTempPath(), "AiteBarTests", Guid.NewGuid().ToString("N"), "settings.backup.json");
        bool hadOriginal = File.Exists(settingsPath);

        Directory.CreateDirectory(directory);
        Directory.CreateDirectory(Path.GetDirectoryName(backupPath)!);

        try
        {
            if (hadOriginal)
            {
                File.Copy(settingsPath, backupPath, overwrite: true);
            }

            File.WriteAllText(settingsPath, content);
            assertion();
        }
        finally
        {
            TelemetryService.Shutdown();

            if (hadOriginal)
            {
                File.Copy(backupPath, settingsPath, overwrite: true);
                File.Delete(backupPath);
                Directory.Delete(Path.GetDirectoryName(backupPath)!, recursive: true);
            }
            else if (File.Exists(settingsPath))
            {
                File.Delete(settingsPath);
            }
        }
    }
}
