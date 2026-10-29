using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Security.Principal;
using System.Text;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ZapretDesktop.Core;
using ZapretDesktop.Core.Engines;
using ZapretDesktop.Core.Models;
using ZapretDesktop.Core.Services;

namespace ZapretDesktop.Desktop.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private const int MaxLogLength = 20_000;

    private readonly WindowsZapretEngine _engine;
    private readonly PresetManager _presetManager;
    private readonly ListManager _listManager;
    private readonly AutoStrategyTester _autoTester;
    private readonly SettingsManager _settingsManager;
    private readonly FlowsealStrategyUpdater _flowsealUpdater;
    private readonly MtProtoProxyProvider _proxyProvider = new();
    private readonly StringBuilder _log = new();

    private CancellationTokenSource? _testCts;
    private FlowsealUpdateInfo? _pendingFlowsealUpdate;
    private bool _initialized;
    private bool _listContentLoaded;

    // ---------- Состояние ----------
    [ObservableProperty] private bool _isRunning;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _isTesting;
    [ObservableProperty] private bool _isInstalled;
    [ObservableProperty] private bool _isAdmin;
    [ObservableProperty] private string _statusText = "Обход выключен";
    [ObservableProperty] private string _engineVersion = "—";
    [ObservableProperty] private string _gameFilterHint = string.Empty;

    // ---------- Стратегии ----------
    [ObservableProperty] private ObservableCollection<Preset> _presets = new();
    [ObservableProperty] private Preset? _selectedPreset;

    // ---------- Тестирование ----------
    [ObservableProperty] private string _testLog = string.Empty;
    [ObservableProperty] private double _testProgress;
    [ObservableProperty] private string _testProgressText = string.Empty;
    [ObservableProperty] private ObservableCollection<StrategyTestResult> _testResults = new();

    // ---------- Лог ----------
    [ObservableProperty] private string _logText = string.Empty;

    // ---------- Telegram ----------
    [ObservableProperty] private ObservableCollection<ProxyCandidate> _proxies = new();
    [ObservableProperty] private ProxyCandidate? _selectedProxy;
    [ObservableProperty] private bool _isLoadingProxies;
    [ObservableProperty] private string _telegramProxyHost = string.Empty;
    [ObservableProperty] private string _telegramProxyPort = "443";
    [ObservableProperty] private string _telegramProxySecret = string.Empty;
    [ObservableProperty] private string _telegramProxyLink = string.Empty;
    [ObservableProperty] private string _telegramStatus = "Нажмите «Подобрать прокси» — приложение само найдёт рабочий сервер.";

    // ---------- Списки ----------
    [ObservableProperty] private ObservableCollection<string> _listFiles = new();
    [ObservableProperty] private string? _selectedListFile = "list-general-user.txt";
    [ObservableProperty] private string _listContent = string.Empty;
    [ObservableProperty] private string _domainsToAdd = string.Empty;

    // ---------- Настройки ----------
    [ObservableProperty] private bool _minimizeToTray;
    [ObservableProperty] private bool _startMinimized;
    [ObservableProperty] private bool _autoStartWithWindows;
    [ObservableProperty] private bool _enableLogging;
    [ObservableProperty] private bool _gameFilter;
    [ObservableProperty] private bool _autoStartZapret;
    [ObservableProperty] private bool _checkFlowsealUpdates = true;

    // ---------- Обновления Flowseal ----------
    [ObservableProperty] private bool _isFlowsealUpdateAvailable;
    [ObservableProperty] private bool _isUpdatingFlowseal;
    [ObservableProperty] private bool _isCheckingFlowseal;
    [ObservableProperty] private string _flowsealUpdateText = string.Empty;
    [ObservableProperty] private string _flowsealStatus = "Проверка обновлений ещё не выполнялась.";

    public MainViewModel()
    {
        _engine = new WindowsZapretEngine();
        _settingsManager = new SettingsManager();
        _presetManager = new PresetManager();
        _listManager = new ListManager();
        _autoTester = new AutoStrategyTester(_engine);
        _flowsealUpdater = new FlowsealStrategyUpdater();

        var settings = _settingsManager.Load();

        // Присваиваем поля напрямую, чтобы не срабатывали обработчики сохранения.
        _minimizeToTray = settings.MinimizeToTray;
        _startMinimized = settings.StartMinimized;
        _autoStartWithWindows = settings.AutoStartWithWindows;
        _enableLogging = settings.EnableLogging;
        _gameFilter = settings.GameFilter;
        _autoStartZapret = settings.AutoStartZapret;
        _checkFlowsealUpdates = settings.CheckFlowsealUpdates;
        _telegramProxyHost = settings.TelegramProxyHost;
        _telegramProxyPort = settings.TelegramProxyPort.ToString();
        _telegramProxySecret = settings.TelegramProxySecret;
        _selectedListFile = string.IsNullOrWhiteSpace(settings.SelectedListFile) ? "list-general.txt" : settings.SelectedListFile;

        _engine.LoggingEnabled = settings.EnableLogging;
        _engine.GameFilterEnabled = settings.GameFilter;

        _engine.LogMessage += OnEngineLog;
        _engine.StateChanged += OnEngineStateChanged;
        _autoTester.TestProgressLog += OnTestLog;
        _autoTester.ProgressChanged += OnTestProgress;
        _autoTester.StrategyTested += OnStrategyTested;
        _proxyProvider.Progress += OnProxyProgress;

        UpdateGameFilterHint();
        UpdateTelegramLink();
    }

    public AppSettings Settings => _settingsManager.Settings;

    public string AppVersion => typeof(MainViewModel).Assembly.GetName().Version?.ToString(3) ?? "1.0.0";

    public string EngineFolder => AppPaths.BinDir;

    // ================= Инициализация =================

    public async Task InitializeAsync()
    {
        if (_initialized) return;
        _initialized = true;

        IsAdmin = CheckIsAdmin();
        IsInstalled = _engine.IsInstalled;
        EngineVersion = await _engine.GetVersionAsync();

        AppendLog(IsAdmin
            ? "Приложение запущено с правами администратора."
            : "⚠ Приложение запущено БЕЗ прав администратора — winws может не запуститься.");

        await ReloadPresetsAsync();
        await ReloadListFilesAsync();

        var lastId = _settingsManager.Settings.LastPresetId;
        if (!string.IsNullOrEmpty(lastId))
        {
            var last = Presets.FirstOrDefault(p => p.Id == lastId);
            if (last != null) SelectedPreset = last;
        }

        if (AutoStartZapret && SelectedPreset != null)
        {
            AppendLog("Автозапуск обхода включён настройками.");
            await StartServiceAsync();
        }
    }

    private static bool CheckIsAdmin()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }

    // ================= Запуск / остановка =================

    [RelayCommand]
    private async Task ToggleServiceAsync()
    {
        if (IsBusy || IsTesting) return;

        if (IsRunning)
            await StopServiceAsync();
        else
            await StartServiceAsync();
    }

    private async Task StartServiceAsync()
    {
        if (SelectedPreset == null)
        {
            MessageBox.Show("Выберите стратегию перед запуском.", "Zapret Desktop",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (IsBusy || IsTesting) return;

        IsBusy = true;
        StatusText = "Запуск обхода...";

        var preset = SelectedPreset;

        try
        {
            if (!_engine.IsInstalled)
            {
                StatusText = "Проверка компонентов...";
                await _engine.EnsureInstalledAsync(new Progress<double>(p =>
                    StatusText = $"Загрузка winws... {(int)(p * 100)}%"));
                IsInstalled = _engine.IsInstalled;
            }

            await Task.Run(() => _engine.Start(preset.Arguments));

            preset.LastUsed = DateTime.UtcNow;
            preset.SuccessCount++;
            _settingsManager.Settings.LastPresetId = preset.Id;
            _settingsManager.Save();

            await _presetManager.UpdatePresetStatsAsync(preset.Id, true);

            StatusText = $"Обход включён: {preset.Name}";
        }
        catch (Exception ex)
        {
            StatusText = "Ошибка запуска";
            AppendLog($"[ERR] Не удалось запустить обход: {ex.Message}");
            MessageBox.Show(
                $"Не удалось запустить обход.\n\n{ex.Message}\n\n" +
                "Проверьте, что приложение запущено от имени администратора и папка bin содержит winws.exe, WinDivert.dll и WinDivert64.sys.",
                "Ошибка запуска", MessageBoxButton.OK, MessageBoxImage.Error);

            preset.FailCount++;
            await _presetManager.UpdatePresetStatsAsync(preset.Id, false);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task StopServiceAsync()
    {
        IsBusy = true;
        StatusText = "Остановка обхода...";

        try
        {
            await Task.Run(() => _engine.Stop());
            StatusText = "Обход выключен";
        }
        catch (Exception ex)
        {
            AppendLog($"[ERR] Ошибка остановки: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
            MemoryTrimmer.Trim();
        }
    }

    // ================= Автоподбор стратегий =================

    [RelayCommand]
    private async Task RunAutoTestAsync()
    {
        if (IsTesting || IsBusy) return;

        var candidates = Presets.ToList();
        if (candidates.Count == 0)
        {
            MessageBox.Show("Нет доступных стратегий для проверки.", "Zapret Desktop",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        IsTesting = true;
        TestLog = string.Empty;
        TestResults.Clear();
        TestProgress = 0;
        TestProgressText = $"0 / {candidates.Count}";
        StatusText = "Подбор стратегии...";
        _testCts = new CancellationTokenSource();

        AutoTestSummary? summary = null;

        try
        {
            AppendTestLog($"Запуск автоподбора: {candidates.Count} стратегий");
            summary = await _autoTester.TestStrategiesAsync(candidates, _testCts.Token);
        }
        catch (OperationCanceledException)
        {
            AppendTestLog("Проверка отменена.");
        }
        catch (Exception ex)
        {
            AppendTestLog($"Ошибка автоподбора: {ex.Message}");
        }
        finally
        {
            IsTesting = false;
            TestProgress = 0;
            _testCts?.Dispose();
            _testCts = null;
            MemoryTrimmer.Trim();
        }

        if (summary?.Best != null)
        {
            SelectedPreset = Presets.FirstOrDefault(p => p.Id == summary.Best.Preset.Id) ?? summary.Best.Preset;
            AppendTestLog($"Лучшая стратегия: {summary.Best.Preset.Name}");
            StatusText = $"Найдено: {summary.Best.Preset.Name}";
            await StartServiceAsync();
        }
        else if (summary is { Cancelled: false })
        {
            StatusText = "Рабочая стратегия не найдена";
            AppendTestLog("Ни одна стратегия не дала доступа. Проверьте интернет и списки доменов.");
        }
    }

    [RelayCommand]
    private void CancelAutoTest() => _testCts?.Cancel();

    [RelayCommand]
    private async Task TestSelectedPresetAsync()
    {
        if (SelectedPreset == null || IsTesting || IsBusy) return;

        var preset = SelectedPreset;
        IsTesting = true;
        TestResults.Clear();
        TestProgress = 0;
        TestProgressText = "0 / 1";
        StatusText = $"Проверка: {preset.Name}";
        _testCts = new CancellationTokenSource();

        StrategyTestResult? result = null;

        try
        {
            result = await _autoTester.TestSingleAsync(preset, _testCts.Token);
        }
        catch (OperationCanceledException)
        {
            AppendTestLog("Проверка отменена.");
        }
        catch (Exception ex)
        {
            AppendTestLog($"Ошибка проверки: {ex.Message}");
        }
        finally
        {
            IsTesting = false;
            TestProgress = 0;
            _testCts?.Dispose();
            _testCts = null;
        }

        if (result is { IsReachable: true })
        {
            AppendTestLog($"{preset.Name}: доступно {result.PassedUrls}/{result.TotalUrls}, отклик {result.LatencyMs} мс");
            await StartServiceAsync();
        }
        else if (result != null)
        {
            AppendTestLog($"{preset.Name}: стратегия не работает ({result.Details})");
            StatusText = "Стратегия не работает";
        }
    }

    // ================= Telegram =================

    [RelayCommand]
    private async Task FindProxiesAsync()
    {
        if (IsLoadingProxies) return;

        IsLoadingProxies = true;
        Proxies.Clear();
        SelectedProxy = null;
        TelegramStatus = "Загружаем список прокси...";

        try
        {
            var found = await _proxyProvider.FindBestProxiesAsync();

            foreach (var proxy in found) Proxies.Add(proxy);

            if (Proxies.Count > 0)
            {
                SelectedProxy = Proxies[0];
                TelegramStatus = $"Найдено {Proxies.Count} рабочих прокси. Выбран самый быстрый — нажмите «Подключить».";
            }
            else
            {
                TelegramProxyLink = string.Empty;
                TelegramStatus = "Рабочие прокси не найдены. Попробуйте ещё раз или введите данные вручную.";
            }
        }
        catch (Exception ex)
        {
            TelegramStatus = $"Не удалось подобрать прокси: {ex.Message}";
        }
        finally
        {
            IsLoadingProxies = false;
            MemoryTrimmer.Trim();
        }
    }

    [RelayCommand]
    private async Task TestTelegramAsync()
    {
        if (IsTesting) return;

        IsTesting = true;
        TelegramStatus = "Проверка Telegram...";
        var report = new StringBuilder();

        try
        {
            var targets = new (string Name, string Url)[]
            {
                ("Telegram Web", "https://web.telegram.org"),
                ("Telegram API", "https://api.telegram.org"),
                ("telegram.org", "https://telegram.org")
            };

            foreach (var (name, url) in targets)
            {
                var ok = await _engine.TestConnectionAsync(url);
                report.AppendLine($"{name}: {(ok ? "✅ доступен" : "❌ недоступен")}");
            }

            if (TryGetProxyPort(out var port) && !string.IsNullOrWhiteSpace(TelegramProxyHost))
            {
                var proxyOk = await ProbeTcpAsync(TelegramProxyHost, port);
                report.AppendLine($"MTProto-прокси {TelegramProxyHost}:{port}: {(proxyOk ? "✅ отвечает" : "❌ недоступен")}");
            }

            TelegramStatus = report.ToString().Trim();
        }
        catch (Exception ex)
        {
            TelegramStatus = $"Ошибка проверки: {ex.Message}";
        }
        finally
        {
            IsTesting = false;
        }
    }

    [RelayCommand]
    private void SaveTelegramProxy()
    {
        if (!TryGetProxyPort(out var port))
        {
            MessageBox.Show("Порт прокси должен быть числом от 1 до 65535.", "Zapret Desktop",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!TelegramProxyService.TryValidate(TelegramProxyHost, port, TelegramProxySecret, out var error))
        {
            MessageBox.Show(error, "Некорректные данные прокси",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _settingsManager.Settings.TelegramProxyHost = TelegramProxyHost.Trim();
        _settingsManager.Settings.TelegramProxyPort = port;
        _settingsManager.Settings.TelegramProxySecret = TelegramProxySecret.Trim();
        _settingsManager.Save();

        UpdateTelegramLink();
        TelegramStatus = "Прокси сохранён. Ссылку можно открыть в Telegram.";
    }

    [RelayCommand]
    private void CopyTelegramProxyLink()
    {
        if (string.IsNullOrWhiteSpace(TelegramProxyLink))
        {
            MessageBox.Show("Сначала укажите и сохраните параметры прокси.", "Zapret Desktop",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            Clipboard.SetText(TelegramProxyLink);
            TelegramStatus = "Ссылка скопирована в буфер обмена.";
        }
        catch (Exception ex)
        {
            TelegramStatus = $"Не удалось скопировать: {ex.Message}";
        }
    }

    [RelayCommand]
    private void OpenTelegramProxyLink()
    {
        if (string.IsNullOrWhiteSpace(TelegramProxyLink))
        {
            MessageBox.Show("Сначала укажите и сохраните параметры прокси.", "Zapret Desktop",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (!TelegramProxyService.TryOpen(TelegramProxyLink, out var error))
        {
            TelegramStatus = error;
        }
    }

    private bool TryGetProxyPort(out int port)
        => int.TryParse(TelegramProxyPort, out port) && port is >= 1 and <= 65535;

    private void UpdateTelegramLink()
    {
        if (TryGetProxyPort(out var port) &&
            TelegramProxyService.TryValidate(TelegramProxyHost, port, TelegramProxySecret, out _))
        {
            TelegramProxyLink = TelegramProxyService.BuildTgLink(TelegramProxyHost, port, TelegramProxySecret);
        }
        else
        {
            TelegramProxyLink = string.Empty;
        }
    }

    private static async Task<bool> ProbeTcpAsync(string host, int port)
    {
        try
        {
            using var client = new TcpClient();
            var connect = client.ConnectAsync(host, port);
            var completed = await Task.WhenAny(connect, Task.Delay(3000));
            if (completed != connect) return false;

            await connect;
            return client.Connected;
        }
        catch
        {
            return false;
        }
    }

    // ================= Списки =================

    [RelayCommand]
    private async Task SaveListAsync()
    {
        if (string.IsNullOrWhiteSpace(SelectedListFile)) return;

        try
        {
            await _listManager.SaveListAsync(SelectedListFile, ListContent);
            AppendLog($"Список {SelectedListFile} сохранён.");

            if (IsRunning && SelectedPreset != null)
            {
                AppendLog("Перезапуск обхода для применения списков...");
                await StopServiceAsync();
                await StartServiceAsync();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Не удалось сохранить список: {ex.Message}", "Ошибка",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private async Task ReloadListAsync() => await LoadSelectedListAsync();

    /// <summary>
    /// Содержимое списка грузится лениво — только при первом открытии вкладки «Списки».
    /// Это заметно ускоряет старт приложения.
    /// </summary>
    public async Task EnsureListContentLoadedAsync()
    {
        if (_listContentLoaded) return;
        await LoadSelectedListAsync();
    }

    [RelayCommand]
    private async Task AddDomainsAsync()
    {
        if (string.IsNullOrWhiteSpace(DomainsToAdd))
            return;

        var domains = DomainsToAdd.Split(
            new[] { '\r', '\n', ',', ';', ' ' },
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var added = await _listManager.AddDomainsAsync("list-general-user.txt", domains);
        DomainsToAdd = string.Empty;

        if (SelectedListFile == "list-general-user.txt")
            await LoadSelectedListAsync();
        else
            await ReloadListFilesAsync();

        AppendLog($"В list-general-user.txt добавлено доменов: {added}");

        if (added > 0 && IsRunning && SelectedPreset != null)
        {
            AppendLog("Перезапуск обхода для применения новых доменов...");
            await StopServiceAsync();
            await StartServiceAsync();
        }
    }

    private async Task LoadSelectedListAsync()
    {
        if (string.IsNullOrWhiteSpace(SelectedListFile)) return;

        _listContentLoaded = true;

        try
        {
            ListContent = await _listManager.ReadListAsync(SelectedListFile);

            _settingsManager.Settings.SelectedListFile = SelectedListFile;
            _settingsManager.Save();
        }
        catch (Exception ex)
        {
            AppendLog($"[ERR] Не удалось прочитать {SelectedListFile}: {ex.Message}");
        }
    }

    private Task ReloadListFilesAsync()
    {
        var files = _listManager.GetListFiles();

        ListFiles.Clear();
        foreach (var file in files) ListFiles.Add(file);

        if (ListFiles.Count == 0) return Task.CompletedTask;

        if (SelectedListFile == null || !ListFiles.Contains(SelectedListFile))
            SelectedListFile = ListFiles[0];

        return Task.CompletedTask;
    }

    // ================= Прочие команды =================

    [RelayCommand]
    private async Task RefreshPresetsAsync()
    {
        await ReloadPresetsAsync();
        StatusText = "Список стратегий обновлён";
    }

    [RelayCommand]
    private void ClearLog()
    {
        _log.Clear();
        LogText = string.Empty;
    }

    [RelayCommand]
    private void OpenLogsFolder() => OpenFolder(AppPaths.LogsDir);

    [RelayCommand]
    private void OpenListsFolder() => OpenFolder(AppPaths.ListsDir);

    [RelayCommand]
    private void OpenConfigFolder() => OpenFolder(AppPaths.ConfigDir);

    [RelayCommand]
    private void OpenBinFolder() => OpenFolder(AppPaths.BinDir);

    private static void OpenFolder(string path)
    {
        try
        {
            Directory.CreateDirectory(path);
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Не удалось открыть папку:\n{path}\n\n{ex.Message}", "Ошибка",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task ReloadPresetsAsync()
    {
        var all = await _presetManager.LoadPresetsAsync();

        Presets.Clear();
        foreach (var preset in all) Presets.Add(preset);

        if (SelectedPreset == null || Presets.All(p => p.Id != SelectedPreset.Id))
            SelectedPreset = Presets.FirstOrDefault();
    }

    // ================= Обновления стратегий Flowseal =================

    /// <summary>
    /// Фоновая проверка обновлений Flowseal. Запускается после показа окна,
    /// не блокирует старт и молча завершается при отсутствии сети.
    /// </summary>
    public async Task CheckFlowsealUpdatesInBackgroundAsync()
    {
        if (!CheckFlowsealUpdates) return;

        // Даём окну отрисоваться и завершить основную инициализацию.
        await Task.Delay(TimeSpan.FromSeconds(2));

        await CheckFlowsealUpdatesCoreAsync(manual: false);
    }

    [RelayCommand]
    private async Task CheckFlowsealUpdatesAsync() => await CheckFlowsealUpdatesCoreAsync(manual: true);

    private async Task CheckFlowsealUpdatesCoreAsync(bool manual)
    {
        if (IsCheckingFlowseal || IsUpdatingFlowseal) return;

        IsCheckingFlowseal = true;
        if (manual) FlowsealStatus = "Проверяем GitHub...";

        try
        {
            var info = await _flowsealUpdater.CheckForUpdateAsync();
            if (info == null)
            {
                if (manual)
                    FlowsealStatus = "Обновлений нет — стратегии актуальны.";
                else if (_flowsealUpdater.LastAppliedUtc is { } applied)
                    FlowsealStatus = $"Стратегии актуальны (обновлены {applied:dd.MM.yyyy HH:mm}).";
                return;
            }

            _pendingFlowsealUpdate = info;
            FlowsealUpdateText = $"Коммит {info.ShortSha} от {info.CommitDate:dd.MM.yyyy}: {info.Message}";
            IsFlowsealUpdateAvailable = true;
            FlowsealStatus = $"Доступно обновление: {info.ShortSha}";
            AppendLog($"Flowseal: доступно обновление стратегий ({info.ShortSha}).");
        }
        catch (Exception ex)
        {
            if (manual) FlowsealStatus = $"Не удалось проверить обновления: {ex.Message}";
        }
        finally
        {
            IsCheckingFlowseal = false;
        }
    }

    [RelayCommand]
    private async Task ApplyFlowsealUpdateAsync()
    {
        var info = _pendingFlowsealUpdate;
        if (info == null || IsUpdatingFlowseal) return;

        IsUpdatingFlowseal = true;
        FlowsealStatus = "Обновляем стратегии Flowseal...";

        var selectedId = SelectedPreset?.Id;
        var finished = false;

        try
        {
            var progress = new Progress<string>(message =>
            {
                if (!finished) FlowsealStatus = message;
            });

            var presets = await _flowsealUpdater.ApplyUpdateAsync(info, progress);
            finished = true;

            IsFlowsealUpdateAvailable = false;
            _pendingFlowsealUpdate = null;

            await ReloadPresetsAsync();

            if (selectedId != null)
                SelectedPreset = Presets.FirstOrDefault(p => p.Id == selectedId) ?? SelectedPreset;

            FlowsealStatus = $"Стратегии обновлены: {presets.Count} шт. ({info.ShortSha}).";
            AppendLog($"Flowseal: загружено {presets.Count} стратегий из коммита {info.ShortSha}.");
            StatusText = "Стратегии Flowseal обновлены";

            // Если обход уже работает, перезапускаем его на новых аргументах.
            if (IsRunning && SelectedPreset != null)
            {
                AppendLog("Перезапуск обхода для применения новых стратегий...");
                await StopServiceAsync();
                await StartServiceAsync();
            }
        }
        catch (Exception ex)
        {
            finished = true;
            FlowsealStatus = $"Не удалось обновить: {ex.Message}";
            AppendLog($"[ERR] Flowseal: {ex.Message}");
            MessageBox.Show(
                $"Не удалось обновить стратегии Flowseal.\n\n{ex.Message}",
                "Обновление стратегий", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            IsUpdatingFlowseal = false;
        }
    }

    [RelayCommand]
    private void SkipFlowsealUpdate()
    {
        if (_pendingFlowsealUpdate != null)
            _flowsealUpdater.Dismiss(_pendingFlowsealUpdate);

        _pendingFlowsealUpdate = null;
        IsFlowsealUpdateAvailable = false;
        FlowsealStatus = "Обновление пропущено. Напомним только после следующего коммита Flowseal.";
        AppendLog("Flowseal: обновление пропущено пользователем.");
    }

    // ================= Обработчики событий =================

    private void OnEngineLog(string message) => OnUi(() => AppendLog(message));

    private void OnEngineStateChanged(bool running) => OnUi(() =>
    {
        IsRunning = running;
        if (running)
        {
            foreach (var preset in Presets) preset.IsActive = preset.Id == SelectedPreset?.Id;
            if (!IsTesting) StatusText = $"Обход включён: {SelectedPreset?.Name}";
        }
        else
        {
            foreach (var preset in Presets) preset.IsActive = false;
            if (!IsTesting) StatusText = "Обход выключен";
        }
    });

    private void OnTestLog(string message)
    {
        OnUi(() => AppendTestLog(message));
        _engine.WriteExternalMessage($"[TEST] {message}");
    }

    private void OnTestProgress(int done, int total) => OnUi(() =>
    {
        TestProgress = total == 0 ? 0 : done * 100.0 / total;
        TestProgressText = $"{done} / {total}";
    });

    private void OnStrategyTested(StrategyTestResult result) => OnUi(() => TestResults.Add(result));

    private void OnProxyProgress(string message) => OnUi(() => TelegramStatus = message);

    private void AppendLog(string message)
    {
        if (_log.Length > MaxLogLength) _log.Clear();

        _log.Append('[').Append(DateTime.Now.ToString("HH:mm:ss")).Append("] ").AppendLine(message);
        LogText = _log.ToString();
    }

    private void AppendTestLog(string message)
    {
        if (TestLog.Length > 30_000) TestLog = string.Empty;
        TestLog += $"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}";
    }

    /// <summary>
    /// События движка приходят из фоновых потоков — перебрасываем в UI-поток.
    /// </summary>
    private static void OnUi(Action action)
    {
        try
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.CheckAccess())
                action();
            else
                dispatcher.BeginInvoke(action);
        }
        catch
        {
            // Приложение закрывается — обновлять UI не нужно.
        }
    }

    // ================= Сохранение настроек =================

    partial void OnSelectedListFileChanged(string? value) => _ = LoadSelectedListAsync();

    partial void OnTelegramProxyHostChanged(string value) => UpdateTelegramLink();

    partial void OnTelegramProxyPortChanged(string value) => UpdateTelegramLink();

    partial void OnTelegramProxySecretChanged(string value) => UpdateTelegramLink();

    partial void OnSelectedProxyChanged(ProxyCandidate? value)
    {
        if (value == null) return;

        TelegramProxyHost = value.Host;
        TelegramProxyPort = value.Port.ToString();
        TelegramProxySecret = value.Secret;
        TelegramProxyLink = value.TgLink;
    }

    partial void OnMinimizeToTrayChanged(bool value)
    {
        _settingsManager.Settings.MinimizeToTray = value;
        _settingsManager.Save();
    }

    partial void OnStartMinimizedChanged(bool value)
    {
        _settingsManager.Settings.StartMinimized = value;
        _settingsManager.Save();
    }

    partial void OnAutoStartWithWindowsChanged(bool value)
    {
        _settingsManager.Settings.AutoStartWithWindows = value;
        _settingsManager.Save();

        try
        {
            AutoStartManager.SetEnabled(value);
        }
        catch (Exception ex)
        {
            AppendLog($"[ERR] Не удалось изменить автозагрузку: {ex.Message}");
        }
    }

    partial void OnEnableLoggingChanged(bool value)
    {
        _engine.LoggingEnabled = value;
        _settingsManager.Settings.EnableLogging = value;
        _settingsManager.Save();
    }

    partial void OnGameFilterChanged(bool value)
    {
        _engine.GameFilterEnabled = value;
        _settingsManager.Settings.GameFilter = value;
        _settingsManager.Save();
        UpdateGameFilterHint();

        if (IsRunning)
            AppendLog("Настройка игрового фильтра применится после перезапуска обхода.");
    }

    partial void OnAutoStartZapretChanged(bool value)
    {
        _settingsManager.Settings.AutoStartZapret = value;
        _settingsManager.Save();
    }

    partial void OnCheckFlowsealUpdatesChanged(bool value)
    {
        _settingsManager.Settings.CheckFlowsealUpdates = value;
        _settingsManager.Save();
    }

    private void UpdateGameFilterHint()
        => GameFilterHint = GameFilter
            ? "Обход применяется ко всем TCP/UDP портам 1024-65535 (игры, голос)."
            : "Обход только для стандартных портов 80/443 и портов Discord.";

    public void ShutdownEngine()
    {
        try
        {
            _engine.Stop();
        }
        catch
        {
            // ignore
        }
    }
}
