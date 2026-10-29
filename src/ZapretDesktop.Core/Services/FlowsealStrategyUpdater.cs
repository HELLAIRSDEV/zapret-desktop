using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using ZapretDesktop.Core.Models;

namespace ZapretDesktop.Core.Services;

/// <summary>
/// Информация о доступном обновлении стратегий Flowseal.
/// </summary>
public sealed class FlowsealUpdateInfo
{
    public string CommitSha { get; init; } = string.Empty;
    public DateTime CommitDate { get; init; }
    public string Message { get; init; } = string.Empty;

    public string ShortSha => CommitSha.Length >= 7 ? CommitSha[..7] : CommitSha;
}

/// <summary>
/// Одна стратегия, скачанная из репозитория Flowseal.
/// </summary>
public sealed class FlowsealStrategy
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = FlowsealStrategyUpdater.CategoryFlowseal;
    public string Arguments { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;

    public Preset ToPreset() => new()
    {
        Id = Id,
        Name = Name,
        Description = Description,
        Category = Category,
        Arguments = Arguments,
        IsBuiltIn = true
    };
}

/// <summary>
/// Состояние обновлений Flowseal (хранится в config\flowseal.json).
/// </summary>
internal sealed class FlowsealState
{
    public string LastCommitSha { get; set; } = string.Empty;
    public string ETag { get; set; } = string.Empty;
    public string DismissedSha { get; set; } = string.Empty;
    public DateTime? LastCheckedUtc { get; set; }
    public DateTime? LastAppliedUtc { get; set; }
    public List<FlowsealStrategy> Strategies { get; set; } = new();
}

/// <summary>
/// Лёгкая проверка GitHub-репозитория Flowseal и загрузка актуальных стратегий.
///
/// Проверка делается одним условным запросом (If-None-Match) и занимает
/// единицы килобайт трафика, поэтому её можно запускать при каждом старте.
/// Сами .bat-файлы скачиваются только после согласия пользователя.
/// </summary>
public sealed class FlowsealStrategyUpdater
{
    public const string CategoryFlowseal = "Flowseal";
    public const string Repository = "Flowseal/zapret-discord-youtube";
    public const string StateFileName = "flowseal.json";

    private const int MaxParallelDownloads = 4;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static readonly HttpClient Http = CreateClient();

    private readonly string _statePath;
    private readonly object _sync = new();
    private FlowsealState _state;

    public FlowsealStrategyUpdater(string? configDir = null)
    {
        var dir = configDir ?? AppPaths.ConfigDir;
        Directory.CreateDirectory(dir);
        _statePath = Path.Combine(dir, StateFileName);
        _state = LoadState(_statePath);
    }

    public DateTime? LastAppliedUtc
    {
        get { lock (_sync) return _state.LastAppliedUtc; }
    }

    public string LastCommitSha
    {
        get { lock (_sync) return _state.LastCommitSha; }
    }

    public int SavedStrategyCount
    {
        get { lock (_sync) return _state.Strategies.Count; }
    }

    /// <summary>
    /// Быстрая проверка последнего коммита. Возвращает <c>null</c>, если обновлений нет,
    /// сети нет или запрос не удался (ошибки намеренно проглатываются).
    /// </summary>
    public async Task<FlowsealUpdateInfo?> CheckForUpdateAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            string? etag;
            lock (_sync) etag = _state.ETag;

            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"https://api.github.com/repos/{Repository}/commits?per_page=1");

            if (!string.IsNullOrEmpty(etag))
                request.Headers.TryAddWithoutValidation("If-None-Match", etag);

            using var response = await Http
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);

            if (response.StatusCode == HttpStatusCode.NotModified)
                return null;

            if (!response.IsSuccessStatusCode)
                return null;

            var newEtag = response.Headers.ETag?.ToString();
            var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Array || root.GetArrayLength() == 0)
                return null;

            var first = root[0];
            var sha = first.TryGetProperty("sha", out var shaElement) ? shaElement.GetString() : null;
            if (string.IsNullOrWhiteSpace(sha))
                return null;

            var commitDate = DateTime.UtcNow;
            var message = string.Empty;

            if (first.TryGetProperty("commit", out var commit))
            {
                if (commit.TryGetProperty("committer", out var committer) &&
                    committer.TryGetProperty("date", out var dateElement) &&
                    DateTime.TryParse(dateElement.GetString(), out var parsedDate))
                {
                    commitDate = parsedDate.ToLocalTime();
                }

                if (commit.TryGetProperty("message", out var messageElement))
                {
                    var full = messageElement.GetString() ?? string.Empty;
                    message = full.Split('\n')[0].Trim();
                }
            }

            bool known;
            lock (_sync)
            {
                known = string.Equals(sha, _state.LastCommitSha, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(sha, _state.DismissedSha, StringComparison.OrdinalIgnoreCase);

                _state.LastCheckedUtc = DateTime.UtcNow;

                // ETag запоминаем только для уже известных коммитов, иначе после перезапуска
                // GitHub ответит 304 и мы потеряем информацию о неподтверждённом обновлении.
                if (known && !string.IsNullOrEmpty(newEtag))
                    _state.ETag = newEtag;

                SaveState();
            }

            if (known)
                return null;

            return new FlowsealUpdateInfo
            {
                CommitSha = sha,
                CommitDate = commitDate,
                Message = message
            };
        }
        catch
        {
            // Нет сети / rate limit / таймаут — тихо пропускаем, приложение работает дальше.
            return null;
        }
    }

    /// <summary>
    /// Скачивает .bat-стратегии указанного коммита, сохраняет их и возвращает готовые пресеты.
    /// </summary>
    public async Task<List<Preset>> ApplyUpdateAsync(
        FlowsealUpdateInfo info,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        progress?.Report("Получаем список стратегий Flowseal...");
        var files = await GetStrategyFileNamesAsync(info.CommitSha, cancellationToken).ConfigureAwait(false);
        if (files.Count == 0)
            throw new InvalidOperationException("В репозитории Flowseal не найдено ни одного файла стратегии.");

        progress?.Report($"Скачиваем стратегии: 0 / {files.Count}...");
        var strategies = await DownloadAndParseAsync(info.CommitSha, files, progress, cancellationToken)
            .ConfigureAwait(false);

        if (strategies.Count == 0)
            throw new InvalidOperationException("Не удалось разобрать аргументы стратегий Flowseal.");

        lock (_sync)
        {
            _state.Strategies = strategies;
            _state.LastCommitSha = info.CommitSha;
            _state.LastAppliedUtc = DateTime.UtcNow;
            _state.LastCheckedUtc = DateTime.UtcNow;
            _state.DismissedSha = string.Empty;
            SaveState();
        }

        return strategies.Select(s => s.ToPreset()).ToList();
    }

    /// <summary>
    /// Пользователь отказался от обновления — не показываем его до следующего коммита.
    /// </summary>
    public void Dismiss(FlowsealUpdateInfo info)
    {
        lock (_sync)
        {
            _state.DismissedSha = info.CommitSha;
            _state.LastCheckedUtc = DateTime.UtcNow;
            SaveState();
        }
    }

    /// <summary>
    /// Чтение сохранённых стратегий без сетевых запросов (используется PresetManager при старте).
    /// </summary>
    public static List<Preset> LoadSavedPresets(string? configDir = null)
    {
        try
        {
            var path = Path.Combine(configDir ?? AppPaths.ConfigDir, StateFileName);
            if (!File.Exists(path)) return new List<Preset>();

            var state = LoadState(path);
            return state.Strategies.Select(s => s.ToPreset()).ToList();
        }
        catch
        {
            return new List<Preset>();
        }
    }

    // ================= Внутренняя реализация =================

    private static async Task<List<string>> GetStrategyFileNamesAsync(string sha, CancellationToken cancellationToken)
    {
        var url = $"https://api.github.com/repos/{Repository}/git/trees/{sha}?recursive=1";
        var json = await Http.GetStringAsync(url, cancellationToken).ConfigureAwait(false);

        using var document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("tree", out var tree) || tree.ValueKind != JsonValueKind.Array)
            return new List<string>();

        var files = new List<string>();
        foreach (var item in tree.EnumerateArray())
        {
            var type = item.TryGetProperty("type", out var typeElement) ? typeElement.GetString() : null;
            if (!string.Equals(type, "blob", StringComparison.OrdinalIgnoreCase)) continue;

            var path = item.TryGetProperty("path", out var pathElement) ? pathElement.GetString() : null;
            if (string.IsNullOrWhiteSpace(path)) continue;

            // Нас интересуют только .bat-файлы в корне репозитория.
            if (path.Contains('/') || path.Contains('\\')) continue;
            if (!path.EndsWith(".bat", StringComparison.OrdinalIgnoreCase)) continue;
            if (path.Equals("service.bat", StringComparison.OrdinalIgnoreCase)) continue;

            files.Add(path);
        }

        files.Sort(StringComparer.OrdinalIgnoreCase);
        return files;
    }

    private static async Task<List<FlowsealStrategy>> DownloadAndParseAsync(
        string sha,
        IReadOnlyList<string> files,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        var results = new List<FlowsealStrategy>(files.Count);
        var gate = new SemaphoreSlim(MaxParallelDownloads, MaxParallelDownloads);
        var completed = 0;
        var lockObj = new object();

        var tasks = files.Select(async fileName =>
        {
            await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var rawUrl = $"https://raw.githubusercontent.com/{Repository}/{sha}/{Uri.EscapeDataString(fileName)}";
                var content = await Http.GetStringAsync(rawUrl, cancellationToken).ConfigureAwait(false);

                FlowsealStrategy? strategy = null;
                var arguments = ExtractArguments(content);
                if (!string.IsNullOrWhiteSpace(arguments))
                {
                    var displayName = ToDisplayName(fileName);
                    strategy = new FlowsealStrategy
                    {
                        Id = ToPresetId(fileName),
                        Name = displayName,
                        Description = $"Стратегия Flowseal «{displayName}» (файл {fileName}).",
                        Category = CategoryFlowseal,
                        Arguments = arguments,
                        FileName = fileName
                    };
                }

                int done;
                lock (lockObj)
                {
                    completed++;
                    done = completed;
                }

                progress?.Report($"Скачиваем стратегии: {done} / {files.Count}...");
                return strategy;
            }
            finally
            {
                gate.Release();
            }
        });

        var parsed = await Task.WhenAll(tasks).ConfigureAwait(false);
        results.AddRange(parsed.Where(s => s != null).Select(s => s!));
        return results;
    }

    /// <summary>
    /// Достаёт аргументы winws из .bat-файла Flowseal и переводит их в шаблон
    /// с плейсхолдерами {BIN}, {LISTS}, {GAME_TCP}, {GAME_UDP}.
    /// </summary>
    internal static string? ExtractArguments(string batContent)
    {
        if (string.IsNullOrWhiteSpace(batContent)) return null;

        var builder = new StringBuilder(batContent.Length);
        foreach (var rawLine in batContent.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0) continue;

            // Строка-продолжение может оказаться отдельной строкой "^".
            if (line == "^") continue;
            if (line.EndsWith("^", StringComparison.Ordinal))
                line = line[..^1].TrimEnd();

            builder.Append(line).Append(' ');
        }

        var text = builder.ToString();

        var exeIndex = text.LastIndexOf("winws.exe", StringComparison.OrdinalIgnoreCase);
        if (exeIndex < 0) return null;

        var rest = text[(exeIndex + "winws.exe".Length)..];

        var argStart = rest.IndexOf("--wf-", StringComparison.OrdinalIgnoreCase);
        if (argStart < 0) argStart = rest.IndexOf("--filter-", StringComparison.OrdinalIgnoreCase);
        if (argStart < 0) argStart = rest.IndexOf("--", StringComparison.Ordinal);
        if (argStart < 0) return null;

        rest = rest[argStart..];

        foreach (var terminator in new[] { " exit", " pause", " goto ", " if ", " echo " })
        {
            var index = rest.IndexOf(terminator, StringComparison.OrdinalIgnoreCase);
            if (index > 0) rest = rest[..index];
        }

        rest = rest
            .Replace("%~dp0bin\\", "{BIN}\\", StringComparison.OrdinalIgnoreCase)
            .Replace("%~dp0lists\\", "{LISTS}\\", StringComparison.OrdinalIgnoreCase)
            .Replace("%BIN%", "{BIN}\\", StringComparison.OrdinalIgnoreCase)
            .Replace("%LISTS%", "{LISTS}\\", StringComparison.OrdinalIgnoreCase)
            .Replace("%GameFilterTCP%", "{GAME_TCP}", StringComparison.OrdinalIgnoreCase)
            .Replace("%GameFilterUDP%", "{GAME_UDP}", StringComparison.OrdinalIgnoreCase);

        rest = Regex.Replace(rest, @"\s+", " ").Trim();

        return rest.Contains("--", StringComparison.Ordinal) ? rest : null;
    }

    private static string ToDisplayName(string fileName)
    {
        var name = Path.GetFileNameWithoutExtension(fileName).Trim();
        if (name.Length == 0) return fileName;
        return char.ToUpperInvariant(name[0]) + name[1..];
    }

    private static string ToPresetId(string fileName)
    {
        var source = Path.GetFileNameWithoutExtension(fileName).ToLowerInvariant();
        var builder = new StringBuilder(source.Length);

        foreach (var ch in source)
        {
            if (char.IsLetterOrDigit(ch)) builder.Append(ch);
            else if (ch is '-' or '_') builder.Append(ch);
            else if (builder.Length > 0 && builder[^1] != '-') builder.Append('-');
        }

        var slug = builder.ToString().Trim('-');
        return "flowseal-" + (slug.Length == 0 ? "strategy" : slug);
    }

    private static FlowsealState LoadState(string path)
    {
        try
        {
            if (!File.Exists(path)) return new FlowsealState();

            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<FlowsealState>(json, JsonOptions) ?? new FlowsealState();
        }
        catch
        {
            return new FlowsealState();
        }
    }

    private void SaveState()
    {
        try
        {
            var json = JsonSerializer.Serialize(_state, JsonOptions);
            var temp = _statePath + ".tmp";
            File.WriteAllText(temp, json);
            File.Move(temp, _statePath, overwrite: true);
        }
        catch
        {
            // Состояние не критично — при следующем запуске просто проверим GitHub заново.
        }
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient(new SocketsHttpHandler
        {
            UseProxy = false,
            ConnectTimeout = TimeSpan.FromSeconds(5),
            AutomaticDecompression = DecompressionMethods.All,
            PooledConnectionLifetime = TimeSpan.FromMinutes(10)
        })
        {
            Timeout = TimeSpan.FromSeconds(20)
        };

        client.DefaultRequestHeaders.UserAgent.ParseAdd("ZapretDesktop/1.0");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return client;
    }
}
