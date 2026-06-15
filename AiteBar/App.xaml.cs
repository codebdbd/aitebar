using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace AiteBar;

[SupportedOSPlatform("windows6.1")]
public partial class App : System.Windows.Application
{
    private static Mutex? _mutex;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        await TelemetryService.InitializeAsync();
        RegisterExceptionHandlers();
        RegisterUtilities();

        try
        {
            AppSettingsService settingsService = await LoadSettingsAndApplyCultureAsync();
            const string mutexName = "Global\\AiteBar_Mutex_Unique_String_123";
            _mutex = new Mutex(true, mutexName, out bool createdNew);

            if (!createdNew)
            {
                System.Windows.MessageBox.Show(
                    LocalizationService.Get("App_AlreadyRunning"),
                    LocalizationService.Get("Common_Info"),
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
                System.Windows.Application.Current.Shutdown();
                return;
            }

            TelemetryService.CaptureMessage("AiteBar started.");
            var mainWindow = new MainWindow(settingsService);
            MainWindow = mainWindow;
            ShutdownMode = ShutdownMode.OnLastWindowClose;
            mainWindow.Show();
        }
        catch (Exception ex)
        {
            Logger.Log(ex);
            System.Windows.Application.Current.Shutdown();
        }
    }

    private static async Task<AppSettingsService> LoadSettingsAndApplyCultureAsync()
    {
        PathHelper.EnsureDirectories();
        var settingsService = new AppSettingsService();
        await settingsService.LoadAsync();
        LocalizationService.ApplyCulture(settingsService.Settings.UiCulture);
        return settingsService;
    }

    private static void RegisterUtilities()
    {
        UtilityRegistry.RegisterAllFromAssembly(System.Reflection.Assembly.GetExecutingAssembly());
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_mutex != null)
        {
            _mutex.ReleaseMutex();
            _mutex.Dispose();
        }

        TelemetryService.Flush(TimeSpan.FromSeconds(2));
        TelemetryService.Shutdown();
        base.OnExit(e);
    }

    private void RegisterExceptionHandlers()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception ex)
            {
                TelemetryService.CaptureException(ex, "appdomain_unhandled");
                TelemetryService.Flush(TimeSpan.FromSeconds(2));
            }
        };

        DispatcherUnhandledException += (_, args) =>
        {
            TelemetryService.CaptureException(args.Exception, "dispatcher_unhandled");
            TelemetryService.Flush(TimeSpan.FromSeconds(2));
            args.Handled = true;
        };
    }
}
