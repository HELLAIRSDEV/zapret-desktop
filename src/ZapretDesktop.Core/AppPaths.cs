namespace ZapretDesktop.Core;

/// <summary>
/// Единые пути приложения (относительно каталога, где лежит ZapretDesktop.exe).
/// </summary>
public static class AppPaths
{
    public static string Root { get; } = AppDomain.CurrentDomain.BaseDirectory;

    public static string BinDir => Path.Combine(Root, "bin");
    public static string ListsDir => Path.Combine(Root, "lists");
    public static string ConfigDir => Path.Combine(Root, "config");
    public static string LogsDir => Path.Combine(Root, "logs");

    public static string WinwsExe => Path.Combine(BinDir, "winws.exe");

    public static void EnsureDirectories()
    {
        Directory.CreateDirectory(BinDir);
        Directory.CreateDirectory(ListsDir);
        Directory.CreateDirectory(ConfigDir);
        Directory.CreateDirectory(LogsDir);
    }
}
