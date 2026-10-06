using System.Configuration;
using System.Data;
using System.Windows;

namespace IdCardPrintShop;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        var logPath = System.IO.Path.Combine(AppContext.BaseDirectory, "startup.log");
        System.IO.File.WriteAllText(logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] OnStartup started\n");

        DispatcherUnhandledException += (s, args) =>
        {
            try
            {
                System.IO.File.AppendAllText(logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] DispatcherUnhandledException: {args.Exception}\n");
            }
            catch { }
        };

        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            try
            {
                System.IO.File.AppendAllText(logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] UnhandledException: {args.ExceptionObject}\n");
            }
            catch { }
        };

        try
        {
            System.IO.File.AppendAllText(logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Calling base.OnStartup...\n");
            base.OnStartup(e);
            System.IO.File.AppendAllText(logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Instantiating MainWindow...\n");

            var mainWindow = new MainWindow();
            MainWindow = mainWindow;
            System.IO.File.AppendAllText(logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] MainWindow instantiated. Calling Show()...\n");
            mainWindow.Show();
            System.IO.File.AppendAllText(logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] MainWindow.Show() completed successfully!\n");
        }
        catch (Exception ex)
        {
            System.IO.File.AppendAllText(logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] CRITICAL ERROR in OnStartup: {ex}\n");
            try
            {
                MessageBox.Show($"Startup Error:\n{ex}", "Application Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch { }
            throw;
        }
    }
}

