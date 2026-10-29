using System;
using Avalonia.Controls;
using ZapretDesktop.Linux.ViewModels;

namespace ZapretDesktop.Linux.Views;

public partial class MainWindow : Window
{
    private MainViewModel? ViewModel => DataContext as MainViewModel;

    public MainWindow()
    {
        InitializeComponent();
    }

    protected override async void OnOpened(EventArgs e)
    {
        base.OnOpened(e);

        if (ViewModel is null) return;

        await ViewModel.InitializeAsync();

        // Автозапуск в свёрнутом виде (--minimized или настройка).
        if (ViewModel.StartMinimized && ViewModel.MinimizeToTray)
            Hide();

        _ = ViewModel.CheckFlowsealUpdatesInBackgroundAsync();
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        // Крестик прячет окно в трей, если так настроено.
        if (ViewModel?.MinimizeToTray == true)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        ViewModel?.ShutdownEngine();
        base.OnClosing(e);
    }
}
