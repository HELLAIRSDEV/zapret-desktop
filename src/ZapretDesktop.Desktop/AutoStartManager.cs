using System.Diagnostics;
using Microsoft.Win32;

namespace ZapretDesktop.Desktop;

/// <summary>
/// Автозапуск приложения вместе с Windows (HKCU\...\Run).
/// </summary>
public static class AutoStartManager
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "ZapretDesktop";

    public static bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
            return key?.GetValue(ValueName) is string;
        }
        catch
        {
            return false;
        }
    }

    public static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true)
                        ?? Registry.CurrentUser.CreateSubKey(RunKeyPath)
                        ?? throw new InvalidOperationException("Не удалось открыть раздел реестра автозагрузки.");

        if (!enabled)
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
            return;
        }

        var exePath = Environment.ProcessPath
                      ?? Process.GetCurrentProcess().MainModule?.FileName
                      ?? throw new InvalidOperationException("Не удалось определить путь к исполняемому файлу.");

        key.SetValue(ValueName, $"\"{exePath}\"", RegistryValueKind.String);
    }
}
