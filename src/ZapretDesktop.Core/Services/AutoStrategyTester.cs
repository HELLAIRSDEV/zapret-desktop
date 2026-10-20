using System.Diagnostics;
using System.Net;
using ZapretDesktop.Core.Engines;
using ZapretDesktop.Core.Models;

namespace ZapretDesktop.Core.Services;

/// <summary>
/// Перебирает стратегии: запускает winws, проверяет доступность целевых сервисов,
/// измеряет задержку и выбирает лучшую рабочую стратегию.
/// </summary>
public class AutoStrategyTester
{
    private static readonly HttpClient HttpClient = new(new SocketsHttpHandler
    {
        UseProxy = false,
        ConnectTimeout = TimeSpan.FromSeconds(5),
        AutomaticDecompression = DecompressionMethods.All
    })
    {
        Timeout = Timeout.InfiniteTimeSpan
    };

    private static readonly IReadOnlyList<string> DefaultTargets = new[]
    {
        "https://www.google.com/generate_204",
        "https://discord.com/api/v9/gateway",
        "https://discord.com",
        "https://www.youtube.com",
        "https://i.ytimg.com/vi/dQw4w9WgXcQ/default.jpg",
        "https://web.telegram.org"
    };

    private readonly IZapretEngine _engine;

    public AutoStrategyTester(IZapretEngine engine)
    {
        _engine = engine;
    }

    public event Action<string>? TestProgressLog;
    public event Action<StrategyTestResult>? StrategyTested;
    public event Action<int, int>? ProgressChanged;

    public IReadOnlyList<string> Targets => DefaultTargets;

    /// <summary>
    /// Проверяет все переданные стратегии и возвращает сводку. Движок после проверки остановлен.
    /// </summary>
    public async Task<AutoTestSummary> TestStrategiesAsync(
        IEnumerable<Preset> presets,
        CancellationToken cancellationToken = default)
    {
        var presetList = presets.ToList();
        var summary = new AutoTestSummary();

        if (presetList.Count == 0)
        {
            Log("Нет стратегий для проверки.");
            return summary;
        }

        Log($"=== Автоподбор стратегий: {presetList.Count} шт. ===");
        Log($"Цели: {string.Join(", ", DefaultTargets.Select(ShortUrl))}");

        var completed = 0;

        foreach (var preset in presetList)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                summary.Cancelled = true;
                Log("Проверка остановлена пользователем.");
                break;
            }

            completed++;
            ProgressChanged?.Invoke(completed - 1, presetList.Count);
            Log($"[{completed}/{presetList.Count}] Проверяем: {preset.Name}");

            var result = await TestSingleInternalAsync(preset, cancellationToken);
            summary.Results.Add(result);
            StrategyTested?.Invoke(result);

            if (result.IsReachable)
            {
                Log($"   ✅ Доступно {result.PassedUrls}/{result.TotalUrls} целей, средний отклик {result.LatencyMs} мс");
            }
            else
            {
                Log($"   ❌ Стратегия не дала доступа ({result.Details})");
            }

            ProgressChanged?.Invoke(completed, presetList.Count);
        }

        summary.Best = summary.Results
            .Where(r => r.IsReachable)
            .OrderByDescending(r => r.Score)
            .FirstOrDefault();

        if (summary.Best != null)
            Log($"=== Лучшая стратегия: {summary.Best.Preset.Name} (отклик {summary.Best.LatencyMs} мс) ===");
        else
            Log("=== Рабочая стратегия не найдена ===");

        return summary;
    }

    /// <summary>
    /// Проверяет одну стратегию.
    /// </summary>
    public async Task<StrategyTestResult> TestSingleAsync(Preset preset, CancellationToken cancellationToken = default)
    {
        Log($"=== Проверка стратегии: {preset.Name} ===");
        var result = await TestSingleInternalAsync(preset, cancellationToken);
        StrategyTested?.Invoke(result);

        return result;
    }

    private async Task<StrategyTestResult> TestSingleInternalAsync(Preset preset, CancellationToken ct)
    {
        try
        {
            if (_engine.IsRunning) await Task.Run(() => _engine.Stop(), ct);
            await DelayAsync(400, ct);

            await Task.Run(() => _engine.Start(preset.Arguments), ct);
            await DelayAsync(2000, ct); // даём winws загрузить ipset/hostlist и поднять фильтры

            if (!_engine.IsRunning)
            {
                return CreateResult(preset, 0, DefaultTargets.Count, 0, "winws завершился сразу после запуска");
            }

            // Проверяем цели параллельно: каждая — в новом соединении.
            var probes = await Task.WhenAll(DefaultTargets.Select(url => ProbeWithRetryAsync(url, ct)));

            var passed = probes.Count(p => p.Ok);

            // Считаем отклик только по успешным целям: таймауты заблокированных
            // запросов иначе раздувают среднее до тысяч миллисекунд.
            var successfulLatencies = probes.Where(p => p.Ok).Select(p => p.LatencyMs).ToList();
            var responseTime = successfulLatencies.Count > 0
                ? (int)successfulLatencies.Average()
                : 0;

            var successRate = passed * 100.0 / DefaultTargets.Count;

            // Рабочая — если доступна хотя бы половина целей.
            var reachable = successRate >= 50;

            // Больше доступных целей и ниже отклик — лучше.
            var score = successRate - responseTime / 20.0;

            var details = string.Join(", ", DefaultTargets.Select((url, i) =>
                $"{ShortUrl(url)}:{(probes[i].Ok ? "ok" : "fail")}"));

            Log($"   {details}");

            return CreateResult(preset, passed, DefaultTargets.Count, responseTime, details, reachable, score);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return CreateResult(preset, 0, DefaultTargets.Count, 0, ex.Message);
        }
        finally
        {
            try
            {
                if (_engine.IsRunning) await Task.Run(() => _engine.Stop());
            }
            catch
            {
                // ignore
            }
        }
    }

    private static StrategyTestResult CreateResult(
        Preset preset,
        int passed,
        int total,
        int latency,
        string details,
        bool? reachable = null,
        double score = 0)
    {
        var successRate = total == 0 ? 0 : passed * 100.0 / total;

        return new StrategyTestResult
        {
            Preset = preset,
            PassedUrls = passed,
            TotalUrls = total,
            LatencyMs = latency,
            IsReachable = reachable ?? successRate >= 60,
            Score = score,
            Details = details
        };
    }

    private static async Task<(bool Ok, int LatencyMs)> ProbeAsync(string url, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(4));

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.UserAgent.ParseAdd(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/120 Safari/537.36");
            // Новое TCP-соединение на каждую проверку, иначе можно переиспользовать
            // соединение, установленное в обход тестируемой стратегии.
            request.Headers.ConnectionClose = true;

            using var response = await HttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token);
            sw.Stop();

            return (response.IsSuccessStatusCode, (int)sw.ElapsedMilliseconds);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            sw.Stop();
            return (false, (int)sw.ElapsedMilliseconds);
        }
    }

    private static async Task<(bool Ok, int LatencyMs)> ProbeWithRetryAsync(string url, CancellationToken ct)
    {
        var first = await ProbeAsync(url, ct);
        if (first.Ok) return first;

        // Одна повторная попытка — соединение могло не успеть установиться.
        await DelayAsync(250, ct);
        var second = await ProbeAsync(url, ct);

        return second.Ok ? second : first;
    }

    private static async Task DelayAsync(int ms, CancellationToken ct)
    {
        if (ms <= 0) return;
        await Task.Delay(ms, ct);
    }

    private static string ShortUrl(string url)
        => Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri.Host : url;

    private void Log(string message) => TestProgressLog?.Invoke(message);
}
