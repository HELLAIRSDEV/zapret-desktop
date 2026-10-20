namespace ZapretDesktop.Core.Engines;

public interface IZapretEngine
{
    bool IsInstalled { get; }
    bool IsRunning { get; }

    bool GameFilterEnabled { get; set; }
    bool LoggingEnabled { get; set; }

    Task EnsureInstalledAsync(IProgress<double>? progress = null);

    void Start(string arguments);

    void Stop();

    /// <summary>
    /// Подставляет пути ({BIN}, {LISTS}, {WORK}) и настройки ({GAME_TCP}, {GAME_UDP}) в аргументы.
    /// </summary>
    string FormatArguments(string arguments);

    Task<string> GetVersionAsync();

    Task<bool> TestConnectionAsync(string url);

    event Action<string>? LogMessage;

    event Action<string>? ErrorMessage;

    event Action<int>? ProcessExited;

    event Action<bool>? StateChanged;
}