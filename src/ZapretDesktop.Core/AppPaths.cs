namespace ZapretDesktop.Core;

/// <summary>
/// Единые пути приложения.
///
/// Windows: всё лежит рядом с ZapretDesktop.exe (как и раньше).
/// Linux: бинарники (bin) могут быть в /usr/lib/zapret-desktop (read-only),
/// а изменяемые данные (lists/config/logs) — в ~/.local/share/ZapretDesktop
/// и ~/.config/ZapretDesktop.
/// </summary>
public static class AppPaths
{
    public static string Root { get; } = AppDomain.CurrentDomain.BaseDirectory;

    /// <summary>Каталог для изменяемых данных (списки, конфиг, логи).</summary>
    public static string DataRoot { get; } = ResolveDataRoot();

    /// <summary>Каталог с бинарниками nfqws/winws (может быть read-only в пакете).</summary>
    public static string BinDir { get; } = ResolveBinDir();

    /// <summary>Сюда движок складывается, если BinDir доступен только на чтение.</summary>
    public static string UserBinDir => Path.Combine(DataRoot, "bin");

    public static string ListsDir => Path.Combine(DataRoot, "lists");
    public static string ConfigDir => Path.Combine(DataRoot, "config");
    public static string LogsDir => Path.Combine(DataRoot, "logs");

    public static string WinwsExe => Path.Combine(BinDir, "winws.exe");

    public static void EnsureDirectories()
    {
        try { Directory.CreateDirectory(BinDir); } catch { /* read-only пакет */ }
        Directory.CreateDirectory(UserBinDir);
        Directory.CreateDirectory(ListsDir);
        Directory.CreateDirectory(ConfigDir);
        Directory.CreateDirectory(LogsDir);
    }

    private static string ResolveDataRoot()
    {
        // Явное переопределение (используется в тестах и в портативной сборке).
        var custom = Environment.GetEnvironmentVariable("ZAPRET_DESKTOP_HOME");
        if (!string.IsNullOrWhiteSpace(custom)) return custom;

        if (OperatingSystem.IsWindows()) return Root;

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        // Портативный запуск из распакованного архива: рядом с бинарником можно писать.
        if (IsWritable(Root)) return Root;

        var data = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
        if (string.IsNullOrWhiteSpace(data))
            data = Path.Combine(home, ".local", "share");

        return Path.Combine(data, "ZapretDesktop");
    }

    private static string ResolveBinDir()
    {
        var custom = Environment.GetEnvironmentVariable("ZAPRET_DESKTOP_BIN");
        if (!string.IsNullOrWhiteSpace(custom)) return custom;

        var local = Path.Combine(Root, "bin");
        if (Directory.Exists(local) && ContainsEngine(local)) return local;

        if (OperatingSystem.IsWindows()) return local;

        // Пакетная установка: /usr/lib/zapret-desktop/bin
        var shared = "/usr/lib/zapret-desktop/bin";
        if (Directory.Exists(shared)) return shared;

        return local;
    }

    private static bool ContainsEngine(string dir)
    {
        try
        {
            return File.Exists(Path.Combine(dir, "nfqws")) ||
                   File.Exists(Path.Combine(dir, "winws.exe")) ||
                   File.Exists(Path.Combine(dir, "zapret-ctl"));
        }
        catch
        {
            return false;
        }
    }

    private static bool IsWritable(string dir)
    {
        try
        {
            var probe = Path.Combine(dir, $".write-probe-{Environment.ProcessId}");
            using (File.Create(probe, 1, FileOptions.DeleteOnClose)) { }
            return true;
        }
        catch
        {
            return false;
        }
    }
}
