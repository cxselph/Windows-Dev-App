using System.Windows;
using System.Windows.Threading;

namespace WindowsDevApp;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
    }

    // Without this, any unhandled exception on the UI thread silently kills the process
    // with no dialog and no log. Showing it keeps the app alive and makes failures diagnosable.
    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(e.Exception.ToString(), "Unexpected error", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }

    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            MessageBox.Show(ex.ToString(), "Fatal error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
