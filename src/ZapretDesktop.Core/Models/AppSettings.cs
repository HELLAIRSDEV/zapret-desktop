namespace ZapretDesktop.Core.Models;

public class AppSettings
{
    // Поведение окна
    public bool MinimizeToTray { get; set; } = true;
    public bool StartMinimized { get; set; } = false;
    public bool AutoStartWithWindows { get; set; } = false;

    // Движок
    public bool EnableLogging { get; set; } = true;
    public bool GameFilter { get; set; } = false;
    public bool AutoStartZapret { get; set; } = false;

    // Telegram
    public bool TelegramBypass { get; set; } = false;
    public string TelegramProxyHost { get; set; } = string.Empty;
    public int TelegramProxyPort { get; set; } = 443;
    public string TelegramProxySecret { get; set; } = string.Empty;

    // Прочее
    public string LastPresetId { get; set; } = string.Empty;
    public string TestUrl { get; set; } = "https://discord.com";
    public string SelectedListFile { get; set; } = "list-general.txt";
}
