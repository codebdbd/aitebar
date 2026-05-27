using System;
using System.Collections.Generic;
using System.Reflection;
using Sentry;

namespace AiteBar;

internal static class TelemetryService
{
    private static readonly object SyncRoot = new();
    private static IDisposable? _sentryHandle;
    private static bool _initialized;

    public static bool IsEnabled { get; private set; }

    public static void Initialize()
    {
        lock (SyncRoot)
        {
            if (_initialized)
            {
                return;
            }

            _initialized = true;
            string? dsn = Environment.GetEnvironmentVariable("AITEBAR_SENTRY_DSN");
            if (string.IsNullOrWhiteSpace(dsn))
            {
                dsn = Environment.GetEnvironmentVariable("SENTRY_DSN");
            }

            if (string.IsNullOrWhiteSpace(dsn))
            {
                return;
            }

            _sentryHandle = SentrySdk.Init(options =>
            {
                options.Dsn = dsn;
                options.Release = $"aitebar@{GetAppVersion()}";
                options.Environment = Environment.GetEnvironmentVariable("AITEBAR_ENVIRONMENT") ?? "production";
                options.SendDefaultPii = false;
                options.TracesSampleRate = 0.0;
            });

            IsEnabled = true;
        }
    }

    public static void CaptureMessage(string message)
    {
        if (!IsEnabled)
        {
            return;
        }

        try
        {
            SentrySdk.CaptureMessage(message, SentryLevel.Info);
        }
        catch (Exception ex)
        {
            Logger.Log(ex);
        }
    }

    public static void CaptureException(
        Exception exception,
        string? operation = null,
        IReadOnlyDictionary<string, string?>? context = null)
    {
        Logger.Log(exception);

        if (!IsEnabled)
        {
            return;
        }

        try
        {
            SentrySdk.CaptureException(exception, scope =>
            {
                if (!string.IsNullOrWhiteSpace(operation))
                {
                    scope.SetTag("operation", operation);
                }

                if (context != null)
                {
                    foreach (var item in context)
                    {
                        if (!string.IsNullOrWhiteSpace(item.Key) && !string.IsNullOrWhiteSpace(item.Value))
                        {
                            scope.SetTag(item.Key, item.Value);
                        }
                    }
                }
            });
        }
        catch (Exception sentryException)
        {
            Logger.Log(sentryException);
        }
    }

    public static void Flush(TimeSpan timeout)
    {
        if (!IsEnabled)
        {
            return;
        }

        try
        {
            SentrySdk.FlushAsync(timeout).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            Logger.Log(ex);
        }
    }

    public static void Shutdown()
    {
        lock (SyncRoot)
        {
            _sentryHandle?.Dispose();
            _sentryHandle = null;
            IsEnabled = false;
        }
    }

    private static string GetAppVersion()
    {
        var assembly = Assembly.GetExecutingAssembly();
        return assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? assembly.GetName().Version?.ToString()
            ?? "unknown";
    }
}
