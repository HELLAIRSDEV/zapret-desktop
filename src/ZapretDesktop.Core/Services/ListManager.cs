namespace ZapretDesktop.Core.Services;

public class ListManager
{
    /// <summary>
    /// Порядок файлов в выпадающем списке: сначала те, что реально влияют на обход.
    /// </summary>
    private static readonly string[] PreferredOrder =
    {
        "list-general.txt",
        "list-general-user.txt",
        "list-google.txt",
        "list-exclude.txt",
        "list-exclude-user.txt",
        "ipset-all.txt",
        "ipset-telegram.txt",
        "ipset-exclude.txt",
        "ipset-exclude-user.txt"
    };

    private readonly string _listsDir;

    public ListManager(string? customDir = null)
    {
        _listsDir = customDir ?? AppPaths.ListsDir;
        Directory.CreateDirectory(_listsDir);
        EnsureDefaults();
    }

    public string ListsDirectory => _listsDir;

    public void EnsureDefaults()
    {
        Ensure("list-general-user.txt", "# Пользовательские домены (один на строку)\n");
        Ensure("list-exclude-user.txt", "# Пользовательские исключения\n");
        Ensure("ipset-exclude-user.txt", "# Пользовательские исключения IPSet\n");
    }

    public IReadOnlyList<string> GetListFiles()
    {
        if (!Directory.Exists(_listsDir)) return Array.Empty<string>();

        return Directory.GetFiles(_listsDir, "*.txt")
            .Select(Path.GetFileName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name!)
            .OrderBy(OrderOf)
            .ThenBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static int OrderOf(string fileName)
    {
        var index = Array.IndexOf(PreferredOrder, fileName);
        return index >= 0 ? index : int.MaxValue;
    }

    public async Task<string> ReadListAsync(string fileName)
    {
        var filePath = Path.Combine(_listsDir, fileName);
        if (!File.Exists(filePath)) return string.Empty;

        return await File.ReadAllTextAsync(filePath);
    }

    public async Task SaveListAsync(string fileName, string content)
    {
        var filePath = Path.Combine(_listsDir, fileName);
        await File.WriteAllTextAsync(filePath, content);
    }

    /// <summary>
    /// Добавляет домены в пользовательский список, пропуская пустые строки и дубликаты.
    /// </summary>
    public async Task<int> AddDomainsAsync(string fileName, IEnumerable<string> domains)
    {
        var existing = await ReadListAsync(fileName);
        var known = new HashSet<string>(
            existing.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Where(line => !line.StartsWith('#'))
                    .Select(line => line.ToLowerInvariant()),
            StringComparer.OrdinalIgnoreCase);

        var added = 0;
        var builder = new System.Text.StringBuilder(existing);
        if (builder.Length > 0 && builder[^1] != '\n') builder.AppendLine();

        foreach (var raw in domains)
        {
            var domain = NormalizeDomain(raw);
            if (domain.Length == 0) continue;
            if (!known.Add(domain)) continue;

            builder.AppendLine(domain);
            added++;
        }

        if (added > 0) await SaveListAsync(fileName, builder.ToString());
        return added;
    }

    private static string NormalizeDomain(string raw)
    {
        var value = raw.Trim();
        if (value.Length == 0) return string.Empty;
        if (value.StartsWith('#')) return string.Empty;

        value = value.Replace("https://", string.Empty, StringComparison.OrdinalIgnoreCase)
                     .Replace("http://", string.Empty, StringComparison.OrdinalIgnoreCase);

        var slash = value.IndexOfAny(new[] { '/', ' ', '\t' });
        if (slash >= 0) value = value[..slash];

        return value.Trim().ToLowerInvariant();
    }

    private void Ensure(string fileName, string content)
    {
        var path = Path.Combine(_listsDir, fileName);
        if (!File.Exists(path) || new FileInfo(path).Length == 0)
            File.WriteAllText(path, content);
    }
}