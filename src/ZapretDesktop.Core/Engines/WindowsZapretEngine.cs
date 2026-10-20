using System.Diagnostics;
using System.IO.Compression;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace ZapretDesktop.Core.Engines;

public class WindowsZapretEngine : IZapretEngine, IDisposable
{
    private readonly object _sync = new();
    private Process? _process;
    private StreamWriter? _logWriter;
    private DateTime _logDate;

    public WindowsZapretEngine()
    {
        AppPaths.EnsureDirectories();
    }

    public event Action<string>? LogMessage;
    public event Action<string>? ErrorMessage;
    public event Action<int>? ProcessExited;
    public event Action<bool>? StateChanged;

    public bool LoggingEnabled { get; set; } = true;
    public bool GameFilterEnabled { get; set; }

    public string BinDirectory => AppPaths.BinDir;
    public string ListsDirectory => AppPaths.ListsDir;
    public string LogsDirectory => AppPaths.LogsDir;

    public bool IsInstalled =>
        File.Exists(Path.Combine(BinDirectory, "winws.exe")) &&
        File.Exists(Path.Combine(BinDirectory, "WinDivert.dll")) &&
        File.Exists(Path.Combine(BinDirectory, "WinDivert64.sys"));

    public bool IsRunning
    {
        get
        {
            lock (_sync)
            {
                try
                {
                    return _process is { HasExited: false };
                }
                catch
                {
                    return false;
                }
            }
        }
    }

    /// <summary>
    /// Подставляет реальные пути и настройки в шаблон аргументов winws.
    /// </summary>
    public string FormatArguments(string arguments)
    {
        var gamePorts = GameFilterEnabled ? "1024-65535" : "12";

        return arguments
            .Replace("{BIN}", BinDirectory)
            .Replace("{LISTS}", ListsDirectory)
            .Replace("{WORK}", AppPaths.Root)
            .Replace("{GAME_TCP}", gamePorts)
            .Replace("{GAME_UDP}", gamePorts);
    }

    public void Start(string arguments)
    {
        if (string.IsNullOrWhiteSpace(arguments))
            throw new ArgumentException("Не заданы аргументы запуска стратегии.", nameof(arguments));

        Stop();
        StopOrphans();

        var exePath = Path.Combine(BinDirectory, "winws.exe");
        if (!File.Exists(exePath))
            throw new FileNotFoundException("Файл winws.exe не найден. Проверьте папку bin рядом с программой.", exePath);

        PrepareLists();

        var formattedArguments = FormatArguments(arguments);

        Log("=== Запуск winws ===");
        Log($"Аргументы: {formattedArguments}");

        var psi = new ProcessStartInfo
        {
            FileName = exePath,
            Arguments = formattedArguments,
            WorkingDirectory = BinDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        var process = new Process { StartInfo = psi, EnableRaisingEvents = true };

        process.OutputDataReceived += (_, e) =>
        {
            if (!string.IsNullOrWhiteSpace(e.Data)) Log($"[WINWS] {e.Data}");
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (!string.IsNullOrWhiteSpace(e.Data)) Log($"[WINWS-ERR] {e.Data}");
        };

        process.Exited += (_, _) => HandleExited(process);

        lock (_sync)
        {
            _process = process;
        }

        if (!process.Start())
        {
            lock (_sync)
            {
                if (ReferenceEquals(_process, process)) _process = null;
            }
            process.Dispose();
            throw new InvalidOperationException("Не удалось запустить процесс winws.exe.");
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        // winws должен работать с обычным приоритетом, даже если приложение свёрнуто в трей.
        try { process.PriorityClass = ProcessPriorityClass.Normal; } catch { }

        Log($"✅ winws запущен (PID {process.Id})");
        StateChanged?.Invoke(true);
    }

    public void Stop()
    {
        Process? process;
        lock (_sync)
        {
            process = _process;
            _process = null;
        }

        if (process != null)
        {
            try
            {
                if (!process.HasExited)
                {
                    Log("Останавливаем winws...");
                    process.Kill(entireProcessTree: true);
                    process.WaitForExit(3000);
                }
            }
            catch (Exception ex)
            {
                Log($"[WARN] Не удалось остановить winws: {ex.Message}");
            }
            finally
            {
                try { process.Dispose(); } catch { /* ignore */ }
            }
        }

        CloseLog();
        StateChanged?.Invoke(false);
    }

    /// <summary>
    /// Убивает "осиротевшие" winws, запущенные именно из папки этого приложения.
    /// Чужие установки zapret не затрагиваются.
    /// </summary>
    public int StopOrphans()
    {
        var killed = 0;
        var ownExe = Path.Combine(BinDirectory, "winws.exe");

        foreach (var proc in Process.GetProcessesByName("winws"))
        {
            try
            {
                var path = proc.MainModule?.FileName;
                if (path != null && string.Equals(path, ownExe, StringComparison.OrdinalIgnoreCase))
                {
                    proc.Kill(entireProcessTree: true);
                    proc.WaitForExit(2000);
                    killed++;
                }
            }
            catch
            {
                // Процесс уже завершён или нет прав — игнорируем.
            }
            finally
            {
                proc.Dispose();
            }
        }

        if (killed > 0) Log($"Очищено зависших процессов winws: {killed}");
        return killed;
    }

    public Task<string> GetVersionAsync()
    {
        var exePath = Path.Combine(BinDirectory, "winws.exe");
        if (!File.Exists(exePath)) return Task.FromResult("Не установлен");

        try
        {
            var version = FileVersionInfo.GetVersionInfo(exePath);
            var text = version.FileVersion ?? version.ProductVersion;
            if (!string.IsNullOrWhiteSpace(text) && text != "0.0.0.0")
                return Task.FromResult(text);

            var date = File.GetLastWriteTime(exePath);
            return Task.FromResult($"сборка от {date:dd.MM.yyyy}");
        }
        catch
        {
            return Task.FromResult("неизвестно");
        }
    }

    public async Task<bool> TestConnectionAsync(string url)
    {
        try
        {
            using var client = new HttpClient(new HttpClientHandler { UseProxy = false })
            {
                Timeout = TimeSpan.FromSeconds(6)
            };
            client.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/120 Safari/537.36");

            using var response = await client.GetAsync(url);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task EnsureInstalledAsync(IProgress<double>? progress = null)
    {
        if (IsInstalled)
        {
            Log("winws уже установлен.");
            return;
        }

        Log("Файлы winws не найдены, пробуем скачать с GitHub...");

        using var client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("ZapretDesktop", "1.0"));
        client.Timeout = TimeSpan.FromMinutes(5);

        const string apiUrl = "https://api.github.com/repos/bol-van/zapret/releases/latest";

        try
        {
            progress?.Report(0.1);
            var responseJson = await client.GetStringAsync(apiUrl);
            progress?.Report(0.3);

            using var doc = JsonDocument.Parse(responseJson);
            var assets = doc.RootElement.GetProperty("assets");

            string? downloadUrl = null;

            // Сначала ищем windows-сборку, иначе — любой zip.
            foreach (var asset in assets.EnumerateArray())
            {
                var name = asset.GetProperty("name").GetString() ?? string.Empty;
                if (!name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) continue;

                if (name.Contains("win", StringComparison.OrdinalIgnoreCase))
                {
                    downloadUrl = asset.GetProperty("browser_download_url").GetString();
                    break;
                }

                downloadUrl ??= asset.GetProperty("browser_download_url").GetString();
            }

            if (string.IsNullOrEmpty(downloadUrl))
                throw new InvalidOperationException("В последнем релизе zapret не найден ZIP-архив.");

            Log($"Скачиваем: {downloadUrl}");
            progress?.Report(0.4);

            var zipPath = Path.Combine(Path.GetTempPath(), "zapret_desktop_download.zip");
            var extractDir = Path.Combine(Path.GetTempPath(), "zapret_desktop_extract");

            try
            {
                var bytes = await client.GetByteArrayAsync(downloadUrl);
                progress?.Report(0.6);
                await File.WriteAllBytesAsync(zipPath, bytes);

                if (Directory.Exists(extractDir)) Directory.Delete(extractDir, true);
                ZipFile.ExtractToDirectory(zipPath, extractDir);
                progress?.Report(0.8);

                var winwsFile = Directory.GetFiles(extractDir, "winws.exe", SearchOption.AllDirectories).FirstOrDefault()
                    ?? throw new FileNotFoundException("В скачанном архиве нет winws.exe. Установите winws вручную в папку bin.");

                var winwsDir = Path.GetDirectoryName(winwsFile)!;

                foreach (var file in Directory.GetFiles(winwsDir))
                    File.Copy(file, Path.Combine(BinDirectory, Path.GetFileName(file)), overwrite: true);

                foreach (var file in Directory.GetFiles(extractDir, "*.bin", SearchOption.AllDirectories))
                    File.Copy(file, Path.Combine(BinDirectory, Path.GetFileName(file)), overwrite: true);

                foreach (var file in Directory.GetFiles(extractDir, "*WinDivert*", SearchOption.AllDirectories))
                    File.Copy(file, Path.Combine(BinDirectory, Path.GetFileName(file)), overwrite: true);

                progress?.Report(1.0);
                Log("✅ winws установлен.");
            }
            finally
            {
                try { if (File.Exists(zipPath)) File.Delete(zipPath); } catch { }
                try { if (Directory.Exists(extractDir)) Directory.Delete(extractDir, true); } catch { }
            }
        }
        catch (Exception ex)
        {
            Log($"[ERR] Ошибка установки winws: {ex.Message}");
            throw;
        }
    }

    private void HandleExited(Process process)
    {
        var code = -1;
        try
        {
            code = process.ExitCode;
        }
        catch
        {
            // ignore
        }

        Log(code == 0
            ? "[WINWS] Процесс завершён (код 0)."
            : $"[WINWS] Процесс завершён с кодом {code}.");

        bool exitedOnItsOwn;
        lock (_sync)
        {
            exitedOnItsOwn = ReferenceEquals(_process, process);
            if (exitedOnItsOwn) _process = null;
        }

        // Если процесс убивает Stop(), освобождает его сам Stop —
        // иначе получаем гонку "No process is associated with this object".
        if (exitedOnItsOwn)
        {
            try { process.Dispose(); } catch { }
        }

        StateChanged?.Invoke(false);
        ProcessExited?.Invoke(code);
    }

    private void PrepareLists()
    {
        Directory.CreateDirectory(ListsDirectory);

        EnsureList("list-general.txt", "# Домены для обхода (по одному на строку)\n");
        EnsureList("list-general-user.txt", "# Пользовательские домены (один на строку)\n");
        EnsureList("list-google.txt", "# Домены Google / YouTube\n");
        EnsureList("list-exclude.txt", "# Домены-исключения\n");
        EnsureList("list-exclude-user.txt", "# Пользовательские исключения\n");
        EnsureList("ipset-all.txt", "# IP/CIDR, требующие обхода\n");
        EnsureList("ipset-telegram.txt", GetTelegramIpSet());
        EnsureList("ipset-exclude.txt", "# Исключения IPSet\n");
        EnsureList("ipset-exclude-user.txt", "# Пользовательские исключения IPSet\n");
    }

    private void EnsureList(string fileName, string defaultContent)
    {
        var path = Path.Combine(ListsDirectory, fileName);
        if (!File.Exists(path) || new FileInfo(path).Length == 0)
            File.WriteAllText(path, defaultContent);
    }

    private static string GetTelegramIpSet() =>
        "# IPv4 Telegram DC1-DC5\n" +
        "91.108.4.0/22\n91.108.8.0/22\n91.108.12.0/22\n91.108.16.0/22\n91.108.20.0/22\n" +
        "91.108.56.0/22\n149.154.160.0/20\n185.76.151.0/24\n\n" +
        "# IPv6\n2001:67c:4e8::/48\n2001:b28:f23d::/48\n2a0a:f280::/32\n";

    /// <summary>
    /// Записывает сообщение только в файл лога (без событий UI).
    /// Используется тестами стратегий для диагностики.
    /// </summary>
    public void WriteExternalMessage(string message)
    {
        if (!LoggingEnabled) return;
        WriteToFile(message);
    }

    private void Log(string message)
    {
        try
        {
            LogMessage?.Invoke(message);

            if (message.Contains("[ERR", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("Ошибка", StringComparison.OrdinalIgnoreCase))
            {
                ErrorMessage?.Invoke(message);
            }
        }
        catch
        {
            // подписчик упал — не роняем движок
        }

        if (!LoggingEnabled) return;

        WriteToFile(message);
    }

    private void WriteToFile(string message)
    {
        lock (_sync)
        {
            try
            {
                var now = DateTime.Now;
                if (_logWriter == null || _logDate.Date != now.Date)
                {
                    _logWriter?.Dispose();
                    Directory.CreateDirectory(LogsDirectory);
                    var file = Path.Combine(LogsDirectory, $"zapret-{now:yyyy-MM-dd}.log");
                    _logWriter = new StreamWriter(new FileStream(file, FileMode.Append, FileAccess.Write, FileShare.ReadWrite), Encoding.UTF8)
                    {
                        AutoFlush = true
                    };
                    _logDate = now;
                }

                _logWriter.WriteLine($"[{now:HH:mm:ss}] {message}");
            }
            catch
            {
                // Логирование не должно ломать работу.
            }
        }
    }

    private void CloseLog()
    {
        lock (_sync)
        {
            try
            {
                _logWriter?.Dispose();
            }
            catch
            {
                // ignore
            }
            _logWriter = null;
        }
    }

    public void Dispose()
    {
        Stop();
    }
}
