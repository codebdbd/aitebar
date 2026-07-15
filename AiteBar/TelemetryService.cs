using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Sentry;

namespace AiteBar;

internal static class TelemetryService
{
    private static readonly object SyncRoot = new();
    private static IDisposable? _sentryHandle;
    private static Task? _initializationTask;
    private static CancellationTokenSource? _initializationCts;

    public static bool IsEnabled { get; private set; }

    [Obsolete("Use InitializeAsync instead for better async behavior.")]
    public static void Initialize()
    {
        // Обратная совместимость для тестов
        InitializeAsync().GetAwaiter().GetResult();
    }

    public static Task InitializeAsync()
    {
        lock (SyncRoot)
        {
            if (_initializationTask != null)
            {
                return _initializationTask;
            }

            var cancellationSource = new CancellationTokenSource();
            var completionSource = new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);
            _initializationCts = cancellationSource;
            _initializationTask = completionSource.Task;
            _ = RunInitializationAsync(cancellationSource, completionSource);
            return _initializationTask;
        }
    }

    private static async Task RunInitializationAsync(
        CancellationTokenSource cancellationSource,
        TaskCompletionSource completionSource)
    {
        try
        {
            await InitializeCoreAsync(cancellationSource).ConfigureAwait(false);
            completionSource.TrySetResult();
        }
        catch (OperationCanceledException) when (cancellationSource.IsCancellationRequested)
        {
            completionSource.TrySetResult();
        }
        catch (Exception ex)
        {
            lock (SyncRoot)
            {
                if (ReferenceEquals(_initializationTask, completionSource.Task))
                {
                    _initializationTask = null;
                    _initializationCts = null;
                }
            }

            cancellationSource.Dispose();
            completionSource.TrySetException(ex);
        }
    }

    private static async Task InitializeCoreAsync(CancellationTokenSource cancellationSource)
    {
        CancellationToken cancellationToken = cancellationSource.Token;
        SentrySettings? settingsFromFile = await LoadSettingsFromFileAsync(cancellationToken).ConfigureAwait(false);
        string? dsn;
        string? environment;
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

        lock (SyncRoot)
        {
            if (cancellationToken.IsCancellationRequested ||
                !ReferenceEquals(_initializationCts, cancellationSource))
            {
                return;
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

    private static async Task<SentrySettings?> LoadSettingsFromFileAsync(CancellationToken cancellationToken)
    {
        string settingsPath = PathHelper.SettingsFile;
        if (!File.Exists(settingsPath))
        {
            return null;
        }

        for (int attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                string json = await File.ReadAllTextAsync(settingsPath, cancellationToken).ConfigureAwait(false);
                var settings = JsonSerializer.Deserialize<AppSettings>(json);
                return settings?.Sentry;
            }
            catch (IOException)
            {
                await Task.Delay(100 * (1 << attempt), cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                return null;
            }
        }
        return null;
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
            _ = SentrySdk.FlushAsync(timeout).ContinueWith(task =>
            {
                if (task.Exception != null)
                {
                    Logger.Log(task.Exception);
                }
            }, TaskContinuationOptions.OnlyOnFaulted);
        }
        catch (Exception ex)
        {
            Logger.Log(ex);
        }
    }

    public static void Shutdown()
    {
        CancellationTokenSource? cancellationSource;
        lock (SyncRoot)
        {
            cancellationSource = _initializationCts;
            _initializationCts = null;
            _initializationTask = null;
            _sentryHandle?.Dispose();
            _sentryHandle = null;
            IsEnabled = false;
        }

        cancellationSource?.Cancel();
        cancellationSource?.Dispose();
    }

    private static string GetAppVersion()
    {
        var assembly = Assembly.GetExecutingAssembly();
        return assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? assembly.GetName().Version?.ToString()
            ?? "unknown";
    }
}
