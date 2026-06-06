using System.Threading;
using System.Windows;
using System.Windows.Threading;

namespace AiteBar
{
    public partial class App : System.Windows.Application
    {
        private static Mutex? _mutex;

        protected override void OnStartup(StartupEventArgs e)
        {
            TelemetryService.Initialize();
            RegisterExceptionHandlers();

            LocalizationService.ApplyCulture(LocalizationService.AutoCulture);
            const string mutexName = "Global\\AiteBar_Mutex_Unique_String_123";
            _mutex = new Mutex(true, mutexName, out bool createdNew);

            if (!createdNew)
            {
                // Приложение уже запущено
                System.Windows.MessageBox.Show(
                    LocalizationService.Get("App_AlreadyRunning"),
                    LocalizationService.Get("Common_Info"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                System.Windows.Application.Current.Shutdown();
                return;
            }

            TelemetryService.CaptureMessage("AiteBar started.");
            base.OnStartup(e);
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
}
