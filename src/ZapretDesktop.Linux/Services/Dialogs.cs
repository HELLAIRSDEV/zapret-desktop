using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace ZapretDesktop.Linux.Services;

/// <summary>Простые модальные диалоги вместо Windows MessageBox.</summary>
public static class Dialogs
{
    public static void Info(string message) => Show("Zapret Desktop", message);
    public static void Warning(string message) => Show("Предупреждение", message);
    public static void Error(string message) => Show("Ошибка", message);

    private static void Show(string title, string message)
    {
        if (Dispatcher.UIThread.CheckAccess())
            _ = ShowAsync(title, message);
        else
            Dispatcher.UIThread.Post(() => _ = ShowAsync(title, message));
    }

    private static async Task ShowAsync(string title, string message)
    {
        var owner = (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;

        var window = new Window
        {
            Title = title,
            Width = 460,
            SizeToContent = SizeToContent.Height,
            CanResize = false,
            WindowStartupLocation = owner != null
                ? WindowStartupLocation.CenterOwner
                : WindowStartupLocation.CenterScreen,
            ShowInTaskbar = false,
            Background = new SolidColorBrush(Color.Parse("#1E1E1E"))
        };

        var text = new TextBlock
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(20, 20, 20, 10)
        };

        var ok = new Button
        {
            Content = "OK",
            Width = 90,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(20, 0, 20, 20),
            IsDefault = true
        };
        ok.Click += (_, _) => window.Close();

        var panel = new StackPanel();
        panel.Children.Add(text);
        panel.Children.Add(ok);
        window.Content = panel;

        if (owner != null && owner.IsVisible)
            await window.ShowDialog(owner);
        else
            window.Show();
    }
}
