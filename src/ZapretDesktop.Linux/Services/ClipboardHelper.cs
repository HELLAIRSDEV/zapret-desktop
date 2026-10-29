using Avalonia.Controls.ApplicationLifetimes;

namespace ZapretDesktop.Linux.Services;

internal static class ClipboardHelper
{
    public static void SetText(string text)
    {
        var window = (Avalonia.Application.Current?.ApplicationLifetime
                      as IClassicDesktopStyleApplicationLifetime)?.MainWindow;

        var clipboard = window?.Clipboard
            ?? throw new InvalidOperationException("Буфер обмена недоступен.");

        clipboard.SetTextAsync(text).GetAwaiter().GetResult();
    }
}
