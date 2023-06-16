using System;
using System.Windows;
using System.Windows.Threading;

namespace DrawingWithCadLib;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    public bool WwwLicenseValidated { get; private set; }

    public App()
    {
        WwwLicenseValidated = false;
        Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() =>
        {
            CadLibService.Initialize();
            WwwLicenseValidated = true;
        }));
    }

    /// <summary>
    /// WPF lets you handle all unhandled exceptions globally, through the DispatcherUnhandledException event on the Application class.
    /// Visit: https://wpf-tutorial.com/wpf-application/handling-exceptions/
    /// </summary>
    private void Application_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        // Extract the exception that was raised while executing code
        Exception? ex = e.Exception;

        // Identify the line and method where the exception originated
        string sourceLine = "";
        if (ex.StackTrace is { Length: > 0 })
        {
            string[] stackTrace = ex.StackTrace.Split('\n');
            foreach (string stackItem in stackTrace)
            {
                sourceLine = stackItem.Trim();
                if (sourceLine.StartsWith("at") && sourceLine.Contains(":line ")) break;
            }
        }

        // Show exception message
        string title = $"Exception - {ex.GetType().FullName}";
        string text = $"An unhandled exception just occurred.\n\n{ex.Message}.\n\n{sourceLine}.";
        MessageBox.Show(text, title, MessageBoxButton.OK, MessageBoxImage.Error);

        // All is done. Prevent the base classes from doing any further handling of the event.
        e.Handled = true;
    }
}