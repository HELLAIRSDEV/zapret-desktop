using System.Diagnostics;
using ZapretDesktop.Core;

namespace ZapretDesktop.Linux.Services;

/// <summary>
/// Автозапуск через XDG autostart (~/.config/autostart/zapret-desktop.desktop).
/// Аналог HKCU\...\Run в Windows.
/// </summary>
public static class AutoStartManager
{
    private const string DesktopFileName = "zapret-desktop.desktop";

    private static string AutostartDir
    {
        get
        {
            var config = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
            if (string.IsNullOrWhiteSpace(config))
                config = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");

            return Path.Combine(config, "autostart");
        }
    }

    private static string DesktopFilePath => Path.Combine(AutostartDir, DesktopFileName);

    public static bool IsEnabled()
    {
        try { return File.Exists(DesktopFilePath); }
        catch { return false; }
    }

    public static void SetEnabled(bool enabled)
    {
        try
        {
            if (!enabled)
            {
                if (File.Exists(DesktopFilePath)) File.Delete(DesktopFilePath);
                return;
            }

            Directory.CreateDirectory(AutostartDir);

            var exe = Environment.ProcessPath ?? Path.Combine(AppPaths.Root, "ZapretDesktop");

            var content =
                "[Desktop Entry]\n" +
                "Type=Application\n" +
                "Name=Zapret Desktop\n" +
                "Comment=Обход DPI для Discord, YouTube и Telegram\n" +
                $"Exec=\"{exe}\" --minimized\n" +
                "Terminal=false\n" +
                "X-GNOME-Autostart-enabled=true\n";

            File.WriteAllText(DesktopFilePath, content);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AUTOSTART] {ex.Message}");
            throw;
        }
    }
}
