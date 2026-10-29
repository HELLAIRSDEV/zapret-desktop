using System.IO;
using System.Windows;
using System.Windows.Threading;
using ZapretDesktop.Core;

namespace ZapretDesktop.Desktop;

public partial class App : Application
{
    private static Mutex? _singleInstanceMutex;

    public App()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) => LogCrash(e.ExceptionObject as Exception);
        DispatcherUnhandledException += OnDispatcherUnhandledException;
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        _singleInstanceMutex = new Mutex(true, @"Local\ZapretDesktop.SingleInstance", out var isFirstInstance);

        if (!isFirstInstance)
        {
            MessageBox.Show("Zapret Desktop уже запущен. Окно можно открыть из значка в трее.",
                "Zapret Desktop", MessageBoxButton.OK, MessageBoxImage.Information);

            Shutdown();
            return;
        }

        base.OnStartup(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            _singleInstanceMutex?.ReleaseMutex();
            _singleInstanceMutex?.Dispose();
        }
        catch
        {
            // ignore
        }

        base.OnExit(e);
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        LogCrash(e.Exception);

        MessageBox.Show(
            $"Произошла непредвиденная ошибка:\n\n{e.Exception.Message}\n\nПодробности записаны в logs\\crash.log.",
            "Zapret Desktop", MessageBoxButton.OK, MessageBoxImage.Error);

        e.Handled = true;
    }

    private static void LogCrash(Exception? exception)
    {
        if (exception == null) return;

        try
        {
            Directory.CreateDirectory(AppPaths.LogsDir);
            var path = Path.Combine(AppPaths.LogsDir, "crash.log");

            File.AppendAllText(path,
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {exception}{Environment.NewLine}{Environment.NewLine}");
        }
        catch
        {
            // нечего делать, если даже лог записать не удалось
        }
    }
}
