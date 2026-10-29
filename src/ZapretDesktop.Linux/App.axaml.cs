using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using ZapretDesktop.Linux.ViewModels;
using ZapretDesktop.Linux.Views;

namespace ZapretDesktop.Linux;

public partial class App : Application
{
    private MainViewModel? _viewModel;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            _viewModel = new MainViewModel();

            // Один экземпляр приложения: второй запуск просто показывает окно.
            desktop.MainWindow = new MainWindow { DataContext = _viewModel };
            desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;
            desktop.Exit += (_, _) => _viewModel.ShutdownEngine();
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleView)
        {
            _viewModel = new MainViewModel();
            singleView.MainView = new MainWindow { DataContext = _viewModel };
        }

        base.OnFrameworkInitializationCompleted();
    }

    public void OnTrayOpen(object? sender, EventArgs e)
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop &&
            desktop.MainWindow is { } window)
        {
            window.Show();
            window.WindowState = WindowState.Normal;
            window.Activate();
        }
    }

    public void OnTrayExit(object? sender, EventArgs e)
    {
        _viewModel?.ShutdownEngine();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.Shutdown();
    }
}
