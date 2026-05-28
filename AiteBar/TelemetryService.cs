using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;
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

            SentrySettings? settingsFromFile = LoadSettingsFromFile();
            string? dsn = null;
            string? environment = null;
            double tracesSampleRate = 0.0;
            bool sendDefaultPii = false;

            // Приоритет: сначала переменные окружения, потом файл настроек
            dsn = Environment.GetEnvironmentVariable("AITEBAR_SENTRY_DSN");
            if (string.IsNullOrWhiteSpace(dsn))
            {
                dsn = Environment.GetEnvironmentVariable("SENTRY_DSN");
            }

            if (string.IsNullOrWhiteSpace(dsn) && settingsFromFile?.IsEnabled == true)
            {
                dsn = settingsFromFile.Dsn;
            }

            if (string.IsNullOrWhiteSpace(dsn))
            {
                return;
            }

            // Дополнительные опции из переменных окружения
            environment = Environment.GetEnvironmentVariable("AITEBAR_ENVIRONMENT");
            if (string.IsNullOrWhiteSpace(environment))
            {
                environment = settingsFromFile?.Environment ?? "production";
            }

            if (double.TryParse(Environment.GetEnvironmentVariable("AITEBAR_TRACES_SAMPLE_RATE"), out double envTracesRate))
            {
                tracesSampleRate = envTracesRate;
            }
            else if (settingsFromFile != null)
            {
                tracesSampleRate = settingsFromFile.TracesSampleRate;
            }

            if (bool.TryParse(Environment.GetEnvironmentVariable("AITEBAR_SEND_PII"), out bool envSendPii))
            {
                sendDefaultPii = envSendPii;
            }
            else if (settingsFromFile != null)
            {
                sendDefaultPii = settingsFromFile.SendDefaultPii;
            }

            _sentryHandle = SentrySdk.Init(options =>
            {
                options.Dsn = dsn;
                options.Release = $"aitebar@{GetAppVersion()}";
                options.Environment = environment;
                options.SendDefaultPii = sendDefaultPii;
                options.TracesSampleRate = tracesSampleRate;
            });

            IsEnabled = true;
        }
    }

    private static SentrySettings? LoadSettingsFromFile()
    {
        try
        {
            string settingsPath = PathHelper.SettingsFile;
            if (!File.Exists(settingsPath))
            {
                return null;
            }

            string json = File.ReadAllText(settingsPath);
            var settings = JsonSerializer.Deserialize<AppSettings>(json);
            return settings?.Sentry;
        }
        catch
        {
            return null;
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
