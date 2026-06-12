using System.Runtime.Versioning;
using System.Threading;
using System.Windows;
using System.Windows.Threading;

namespace AiteBar;

[SupportedOSPlatform("windows6.1")]
public partial class App : System.Windows.Application
{
    private static Mutex? _mutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        TelemetryService.Initialize();
        RegisterExceptionHandlers();
        RegisterUtilities();

        LocalizationService.ApplyCulture(LocalizationService.AutoCulture);
        const string mutexName = "Global\\AiteBar_Mutex_Unique_String_123";
        _mutex = new Mutex(true, mutexName, out bool createdNew);

        if (!createdNew)
        {
            // Приложение уже запущено
            System.Windows.MessageBox.Show(
                LocalizationService.Get("App_AlreadyRunning"),
                LocalizationService.Get("Common_Info"),
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
            System.Windows.Application.Current.Shutdown();
            return;
        }

        TelemetryService.CaptureMessage("AiteBar started.");
        base.OnStartup(e);
    }

    private static void RegisterUtilities()
    {
        // Регистрация существующих утилит
        UtilityRegistry.Register(new QuickNoteUtility());
        UtilityRegistry.Register(new TimerStopwatchUtility());
        UtilityRegistry.Register(new ColorPickerUtility());
        UtilityRegistry.Register(new FileSorterUtility());
        UtilityRegistry.Register(new IconConverterUtility());
        
        // Чтобы добавить новую утилиту, просто создайте класс, реализующий IUtility,
        // и добавьте его сюда: UtilityRegistry.Register(new YourNewUtility());
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
            args.Handled = false;
        };
    }
}
