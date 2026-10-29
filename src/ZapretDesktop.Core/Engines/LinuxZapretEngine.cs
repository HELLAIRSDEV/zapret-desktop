using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace ZapretDesktop.Core.Engines;

/// <summary>
/// Linux-движок обхода DPI на базе nfqws (zapret от bol-van) + nftables.
///
/// Логика: приложение запускает вспомогательный скрипт zapret-ctl через pkexec
/// (или sudo -n), скрипт создаёт правила nftables и запускает nfqws.
/// Так GUI может работать от обычного пользователя, а root-права запрашиваются
/// pollkit-ом только на время старта/остановки обхода.
/// </summary>
public class LinuxZapretEngine : IZapretEngine, IDisposable
{
    private const int QueueNumber = 200;
    private const string HelperName = "zapret-ctl";

    private readonly object _sync = new();
    private Process? _process;
    private StreamWriter? _logWriter;
    private DateTime _logDate;

    public LinuxZapretEngine()
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

    /// <summary>Путь к nfqws: сначала bundled-бинарь рядом с приложением, потом PATH.</summary>
    public static string? NfqwsPath
    {
        get
        {
            var bundled = Path.Combine(AppPaths.BinDir, "nfqws");
            if (File.Exists(bundled)) return bundled;

            foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
                     .Split(':', StringSplitOptions.RemoveEmptyEntries))
            {
                var candidate = Path.Combine(dir, "nfqws");
                if (File.Exists(candidate)) return candidate;
            }

            return null;
        }
    }

    /// <summary>Скрипт-обёртка для nftables + nfqws.</summary>
    public static string HelperPath
    {
        get
        {
            var bundled = Path.Combine(AppPaths.BinDir, HelperName);
            if (File.Exists(bundled)) return bundled;

            foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
                     .Split(':', StringSplitOptions.RemoveEmptyEntries))
            {
                var candidate = Path.Combine(dir, HelperName);
                if (File.Exists(candidate)) return candidate;
            }

            return bundled;
        }
    }

    public bool IsInstalled => NfqwsPath != null && File.Exists(HelperPath);

    public bool IsRunning
    {
        get
        {
            lock (_sync)
            {
                try { return _process is { HasExited: false }; }
                catch { return false; }
            }
        }
    }

    /// <summary>
    /// Подставляет пути и порты в шаблон аргументов winws/nfqws.
    /// Дополнительно убирает Windows-специфичные --wf-tcp/--wf-udp
    /// (их роль играют правила nftables) и чинит обратные слэши.
    /// </summary>
    public string FormatArguments(string arguments)
    {
        var gamePorts = GameFilterEnabled ? "1024-65535" : "12";

        var text = arguments
            .Replace("{BIN}", BinDirectory)
            .Replace("{LISTS}", ListsDirectory)
            .Replace("{WORK}", AppPaths.Root)
            .Replace("{GAME_TCP}", gamePorts)
            .Replace("{GAME_UDP}", gamePorts);

        // --wf-tcp/--wf-udp понимает только winws (WinDivert). На Linux порты
        // заворачиваются в NFQUEUE правилами nftables, поэтому флаги вырезаем.
        text = Regex.Replace(text, @"--wf-(tcp|udp)=\S+", " ", RegexOptions.IgnoreCase);

        // Пути в стратегиях записаны по-виндовому: {BIN}\file.bin.
        text = text.Replace('\\', '/');

        return Regex.Replace(text, @"\s+", " ").Trim();
    }

    /// <summary>Порты из --wf-* (или дефолтные), которые надо завернуть в NFQUEUE.</summary>
    public static (string TcpPorts, string UdpPorts) ExtractPorts(string arguments, bool gameFilter)
    {
        string tcp = "80,443,2053,2083,2087,2096,8443";
        string udp = "443,19294-19344,50000-50100";

        var tcpMatch = Regex.Match(arguments, @"--wf-tcp=([^\s""]+)", RegexOptions.IgnoreCase);
        if (tcpMatch.Success) tcp = tcpMatch.Groups[1].Value;

        var udpMatch = Regex.Match(arguments, @"--wf-udp=([^\s""]+)", RegexOptions.IgnoreCase);
        if (udpMatch.Success) udp = udpMatch.Groups[1].Value;

        string Clean(string ports) => ports
            .Replace("{GAME_TCP}", gameFilter ? "1024-65535" : "12")
            .Replace("{GAME_UDP}", gameFilter ? "1024-65535" : "12");

        return (Clean(tcp), Clean(udp));
    }

    public void Start(string arguments)
    {
        if (string.IsNullOrWhiteSpace(arguments))
            throw new ArgumentException("Не заданы аргументы запуска стратегии.", nameof(arguments));

        Stop();

        var nfqws = NfqwsPath
            ?? throw new FileNotFoundException(
                "nfqws не найден. Установите zapret (пакет zapret / zapret-git из AUR) " +
                "или положите бинарь nfqws в папку bin рядом с приложением.", "nfqws");

        var helper = HelperPath;
        if (!File.Exists(helper))
            throw new FileNotFoundException($"Не найден вспомогательный скрипт {HelperName}.", helper);

        EnsureExecutable(nfqws);
        EnsureExecutable(helper);

        PrepareLists();

        var formatted = FormatArguments(arguments);
        var (tcpPorts, udpPorts) = ExtractPorts(arguments, GameFilterEnabled);

        Log("=== Запуск nfqws ===");
        Log($"Аргументы: {formatted}");
        Log($"NFQUEUE {QueueNumber}: TCP {tcpPorts} / UDP {udpPorts}");

        var psi = new ProcessStartInfo
        {
            FileName = helper,
            WorkingDirectory = BinDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        psi.ArgumentList.Add("start");
        psi.ArgumentList.Add("--qnum");
        psi.ArgumentList.Add(QueueNumber.ToString());
        psi.ArgumentList.Add("--tcp-ports");
        psi.ArgumentList.Add(tcpPorts);
        psi.ArgumentList.Add("--udp-ports");
        psi.ArgumentList.Add(udpPorts);
        psi.ArgumentList.Add("--nfqws");
        psi.ArgumentList.Add(nfqws);
        psi.ArgumentList.Add("--");

        foreach (var arg in SplitArguments(formatted))
            psi.ArgumentList.Add(arg);

        var process = new Process { StartInfo = psi, EnableRaisingEvents = true };

        process.OutputDataReceived += (_, e) =>
        {
            if (!string.IsNullOrWhiteSpace(e.Data)) Log($"[NFQWS] {e.Data}");
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (!string.IsNullOrWhiteSpace(e.Data)) Log($"[NFQWS-ERR] {e.Data}");
        };

        process.Exited += (_, _) => HandleExited(process);

        lock (_sync) { _process = process; }

        if (!process.Start())
        {
            lock (_sync)
            {
                if (ReferenceEquals(_process, process)) _process = null;
            }
            process.Dispose();
            throw new InvalidOperationException("Не удалось запустить nfqws.");
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        Log($"✅ nfqws запущен (PID {process.Id})");
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
                    Log("Останавливаем nfqws...");
                    process.Kill(entireProcessTree: true);
                    process.WaitForExit(3000);
                }
            }
            catch (Exception ex)
            {
                Log($"[WARN] Не удалось остановить nfqws: {ex.Message}");
            }
            finally
            {
                try { process.Dispose(); } catch { /* ignore */ }
            }
        }

        // Подчищаем правила nftables и осиротевшие процессы.
        CleanupHelper();

        CloseLog();
        StateChanged?.Invoke(false);
    }

    /// <summary>Shell-запуск helper-скрипта (pkexec → sudo -n → напрямую, если root).</summary>
    private void CleanupHelper()
    {
        var helper = HelperPath;
        if (!File.Exists(helper)) return;

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = helper,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            psi.ArgumentList.Add("stop");

            using var proc = Process.Start(psi);
            if (proc == null) return;
            var output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit(5000);
            if (!string.IsNullOrWhiteSpace(output)) Log($"[CTL] {output.Trim()}");
        }
        catch (Exception ex)
        {
            Log($"[WARN] Не удалось очистить правила nftables: {ex.Message}");
        }
    }

    public Task<string> GetVersionAsync()
    {
        var nfqws = NfqwsPath;
        if (nfqws == null) return Task.FromResult("Не установлен");

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = nfqws,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            psi.ArgumentList.Add("--version");

            using var proc = Process.Start(psi);
            if (proc == null) return Task.FromResult("неизвестно");

            var text = proc.StandardOutput.ReadToEnd() + proc.StandardError.ReadToEnd();
            proc.WaitForExit(3000);

            var line = text.Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim();
            if (!string.IsNullOrWhiteSpace(line)) return Task.FromResult(line);

            var date = File.GetLastWriteTime(nfqws);
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
                "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 Chrome/120 Safari/537.36");

            using var response = await client.GetAsync(url);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// На Linux nfqws собирается под конкретный дистрибутив, поэтому автоскачивание
    /// небезопасно. Метод проверяет наличие и объясняет, чего не хватает.
    /// </summary>
    public Task EnsureInstalledAsync(IProgress<double>? progress = null)
    {
        progress?.Report(0.5);

        if (NfqwsPath != null)
        {
            Log("nfqws уже установлен.");
            progress?.Report(1.0);
            return Task.CompletedTask;
        }

        Log("[ERR] nfqws не найден.");
        Log("Установите его одним из способов:");
        Log("  Arch:   yay -S zapret-git   (или pacman -S zapret)");
        Log("  Debian: sudo apt install zapret   (если есть в репозитории)");
        Log("  Fedora: sudo dnf install zapret");
        Log("  Или положите собранный nfqws в папку bin рядом с приложением.");
        progress?.Report(0.0);

        throw new FileNotFoundException("nfqws не найден в системе.");
    }

    private static IEnumerable<string> SplitArguments(string commandLine)
    {
        var current = new StringBuilder();
        var inQuotes = false;

        foreach (var ch in commandLine)
        {
            if (ch == '"')
            {
                inQuotes = !inQuotes;
                continue;
            }

            if (!inQuotes && char.IsWhiteSpace(ch))
            {
                if (current.Length > 0)
                {
                    yield return current.ToString();
                    current.Clear();
                }
                continue;
            }

            current.Append(ch);
        }

        if (current.Length > 0) yield return current.ToString();
    }

    private static void EnsureExecutable(string path)
    {
        try
        {
            var mode = File.GetUnixFileMode(path);
            if ((mode & (UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute)) == 0)
                File.SetUnixFileMode(path, mode | UnixFileMode.UserExecute | UnixFileMode.GroupExecute);
        }
        catch
        {
            // нет прав — пусть попробует запуститься как есть
        }
    }

    private void HandleExited(Process process)
    {
        var code = -1;
        try { code = process.ExitCode; } catch { /* ignore */ }

        Log(code == 0
            ? "[NFQWS] Процесс завершён (код 0)."
            : $"[NFQWS] Процесс завершён с кодом {code}.");

        bool exitedOnItsOwn;
        lock (_sync)
        {
            exitedOnItsOwn = ReferenceEquals(_process, process);
            if (exitedOnItsOwn) _process = null;
        }

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
                    _logWriter = new StreamWriter(
                        new FileStream(file, FileMode.Append, FileAccess.Write, FileShare.ReadWrite),
                        Encoding.UTF8)
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
            try { _logWriter?.Dispose(); } catch { }
            _logWriter = null;
        }
    }

    public void Dispose() => Stop();
}
