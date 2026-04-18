using System.Threading;
using System.Windows;

namespace AiteBar {
    public partial class App : System.Windows.Application 
    {
        private static Mutex? _mutex;

        protected override void OnStartup(StartupEventArgs e)
        {
            const string mutexName = "AiteBar_Mutex_Unique_String_123";
            _mutex = new Mutex(true, mutexName, out bool createdNew);

            if (!createdNew)
            {
                // Приложение уже запущено
                System.Windows.MessageBox.Show("AiteBar уже запущен.", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
                System.Windows.Application.Current.Shutdown();
                return;
            }

            base.OnStartup(e);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            if (_mutex != null)
            {
                _mutex.ReleaseMutex();
                _mutex.Dispose();
            }
            base.OnExit(e);
        }
    }
}
