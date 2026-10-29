using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.Sockets;
using ZapretDesktop.Core.Models;

namespace ZapretDesktop.Core.Services;

/// <summary>
/// Загружает публичные списки MTProto-прокси с GitHub, проверяет доступность
/// TCP-подключением и сортирует по задержке.
/// </summary>
public class MtProtoProxyProvider
{
    private static readonly string[] Sources =
    {
        "https://raw.githubusercontent.com/tgmtproxy/telegram-mtproto-proxy-list/main/proxies.txt",
        "https://raw.githubusercontent.com/SoliSpirit/mtproto/master/all_proxies.txt"
    };

    private static readonly HttpClient Http = CreateClient();

    public event Action<string>? Progress;

    private static HttpClient CreateClient()
    {
        var client = new HttpClient(new SocketsHttpHandler
        {
            UseProxy = false,
            ConnectTimeout = TimeSpan.FromSeconds(8),
            PooledConnectionLifetime = TimeSpan.FromMinutes(2)
        })
        {
            Timeout = TimeSpan.FromSeconds(25)
        };

        client.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/120 Safari/537.36");

        return client;
    }

    /// <summary>
    /// Возвращает до <paramref name="maxResults"/> самых быстрых живых прокси.
    /// </summary>
    public async Task<IReadOnlyList<ProxyCandidate>> FindBestProxiesAsync(
        int maxResults = 40,
        int maxToCheck = 450,
        CancellationToken cancellationToken = default)
    {
        var candidates = new Dictionary<string, ProxyCandidate>(StringComparer.OrdinalIgnoreCase);

        foreach (var source in Sources)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                Progress?.Invoke("Загружаем список прокси...");
                var text = await Http.GetStringAsync(source, cancellationToken);

                foreach (var candidate in Parse(text))
                {
                    var key = $"{candidate.Host}:{candidate.Port}";
                    candidates.TryAdd(key, candidate);
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Progress?.Invoke($"Источник недоступен: {ex.Message}");
            }
        }

        if (candidates.Count == 0)
        {
            Progress?.Invoke("Списки прокси недоступны.");
            return Array.Empty<ProxyCandidate>();
        }

        var toCheck = candidates.Values.Take(maxToCheck).ToList();
        Progress?.Invoke($"Проверяем {toCheck.Count} прокси...");

        var alive = new ConcurrentBag<ProxyCandidate>();
        using var throttle = new SemaphoreSlim(80);

        var tasks = toCheck.Select(async candidate =>
        {
            await throttle.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var latency = await PingAsync(candidate.Host, candidate.Port, cancellationToken).ConfigureAwait(false);
                if (latency >= 0)
                {
                    candidate.LatencyMs = latency;
                    alive.Add(candidate);
                }
            }
            finally
            {
                throttle.Release();
            }
        });

        await Task.WhenAll(tasks).ConfigureAwait(false);

        var result = alive
            .OrderBy(c => c.LatencyMs)
            .Take(maxResults)
            .ToList();

        Progress?.Invoke(result.Count > 0
            ? $"Рабочих прокси: {alive.Count}. Лучший: {result[0].Host}:{result[0].Port} ({result[0].LatencyMs} мс)"
            : "Рабочих прокси не найдено.");

        return result;
    }

    private static IEnumerable<ProxyCandidate> Parse(string text)
    {
        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;

            var queryIndex = line.IndexOf('?');
            if (queryIndex < 0) continue;

            var query = line[(queryIndex + 1)..];
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var eq = pair.IndexOf('=');
                if (eq <= 0) continue;

                var key = pair[..eq];
                var value = Uri.UnescapeDataString(pair[(eq + 1)..]);
                values[key] = value;
            }

            if (!values.TryGetValue("server", out var host) ||
                !values.TryGetValue("port", out var portText) ||
                !values.TryGetValue("secret", out var secret))
            {
                continue;
            }

            host = host.Trim().TrimEnd('.').ToLowerInvariant();

            if (host.Length == 0 || host.Contains(' ') || host.Contains('/')) continue;
            if (!int.TryParse(portText, out var port) || port is < 1 or > 65535) continue;
            if (!TelegramProxyService.IsValidSecret(secret)) continue;
            if (host is "127.0.0.1" or "localhost") continue;

            yield return new ProxyCandidate { Host = host, Port = port, Secret = secret };
        }
    }

    private static async Task<int> PingAsync(string host, int port, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            using var client = new TcpClient();
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(2));

            await client.ConnectAsync(host, port, cts.Token).ConfigureAwait(false);
            stopwatch.Stop();

            return (int)stopwatch.ElapsedMilliseconds;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return -1;
        }
    }
}
