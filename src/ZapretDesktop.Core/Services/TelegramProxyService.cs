using System.Diagnostics;
using System.Text.RegularExpressions;

namespace ZapretDesktop.Core.Services;

/// <summary>
/// Формирование и проверка ссылок MTProto-прокси Telegram.
/// Секрет прокси выдаёт @MTProxybot или владелец прокси.
/// </summary>
public static partial class TelegramProxyService
{
    [GeneratedRegex("^[a-zA-Z0-9.-]+$")]
    private static partial Regex HostRegex();

    [GeneratedRegex("^[0-9a-fA-F]+$")]
    private static partial Regex HexRegex();

    public static bool TryValidate(string? host, int port, string? secret, out string error)
    {
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(host) || !HostRegex().IsMatch(host))
        {
            error = "Укажите корректный адрес прокси (например, proxy.example.com).";
            return false;
        }

        if (port is < 1 or > 65535)
        {
            error = "Порт должен быть в диапазоне 1-65535.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(secret) || !IsValidSecret(secret))
        {
            error = "Секрет должен быть 32 hex-символа, 34 hex-символа (с префиксом dd) или base64 (22-24 символа).";
            return false;
        }

        return true;
    }

    public static bool IsValidSecret(string secret)
    {
        var value = secret.Trim();
        if (value.Length == 0) return false;

        if (HexRegex().IsMatch(value))
        {
            // 16 байт (32 hex), 17 байт с префиксом dd (34 hex),
            // а также FakeTLS-секреты с доменом внутри (кратно 2, длиннее 34).
            return value.Length is >= 32 and <= 256 && value.Length % 2 == 0;
        }

        // base64url вариант
        if (value.Length is 22 or 24)
            return value.All(c => char.IsLetterOrDigit(c) || c is '+' or '/' or '=' or '-' or '_');

        return false;
    }

    public static string BuildTgLink(string host, int port, string secret)
        => $"tg://proxy?server={Uri.EscapeDataString(host.Trim())}&port={port}&secret={Uri.EscapeDataString(secret.Trim())}";

    public static string BuildWebLink(string host, int port, string secret)
        => $"https://t.me/proxy?server={Uri.EscapeDataString(host.Trim())}&port={port}&secret={Uri.EscapeDataString(secret.Trim())}";

    public static bool TryOpen(string link, out string error)
    {
        error = string.Empty;

        try
        {
            Process.Start(new ProcessStartInfo(link) { UseShellExecute = true });
            return true;
        }
        catch (Exception ex)
        {
            error = $"Не удалось открыть ссылку: {ex.Message}";
            return false;
        }
    }
}
