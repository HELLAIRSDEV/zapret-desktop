using System.Text.Json;
using ZapretDesktop.Core.Models;

namespace ZapretDesktop.Core.Services;

public class PresetManager
{
    private readonly string _configDir;
    private readonly string _presetsFilePath;
    private readonly string _statsFilePath;

    public PresetManager(string? customPath = null)
    {
        _configDir = customPath ?? AppPaths.ConfigDir;
        Directory.CreateDirectory(_configDir);
        _presetsFilePath = Path.Combine(_configDir, "presets.json");
        _statsFilePath = Path.Combine(_configDir, "presets_stats.json");
    }

    public List<Preset> GetDefaultPresets()
    {
        var presets = StrategyCatalog.GetDefaults();

        // Если стратегии Flowseal уже скачаны — они заменяют одноимённые встроенные.
        var flowseal = FlowsealStrategyUpdater.LoadSavedPresets(_configDir);
        if (flowseal.Count > 0)
        {
            var legacy = StrategyCatalog.FlowsealLegacyIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
            presets.RemoveAll(p => legacy.Contains(p.Id));
            presets.AddRange(flowseal);
        }

        return presets;
    }

    public async Task<List<Preset>> LoadPresetsAsync()
    {
        List<Preset> presets;

        if (!File.Exists(_presetsFilePath))
        {
            presets = GetDefaultPresets();
            await SavePresetsAsync(presets);
        }
        else
        {
            try
            {
                string json = await File.ReadAllTextAsync(_presetsFilePath);
                presets = JsonSerializer.Deserialize<List<Preset>>(json) ?? GetDefaultPresets();
            }
            catch
            {
                presets = GetDefaultPresets();
            }
        }

        // Обновляем встроенные стратегии до актуальных версий и добавляем новые.
        var builtIn = GetDefaultPresets();
        var builtInIds = builtIn.Select(p => p.Id).ToHashSet();

        presets.RemoveAll(p => p.IsBuiltIn && !builtInIds.Contains(p.Id));

        var changed = false;
        foreach (var definition in builtIn)
        {
            var existing = presets.FirstOrDefault(p => p.Id == definition.Id);
            if (existing == null)
            {
                presets.Add(definition);
                changed = true;
                continue;
            }

            if (existing.Name != definition.Name ||
                existing.Description != definition.Description ||
                existing.Category != definition.Category ||
                existing.Arguments != definition.Arguments)
            {
                existing.Name = definition.Name;
                existing.Description = definition.Description;
                existing.Category = definition.Category;
                existing.Arguments = definition.Arguments;
                existing.IsBuiltIn = true;
                changed = true;
            }
        }

        // Статистику загружаем после синхронизации, чтобы не затереть её нулями
        // и не потерять данные стратегий, пришедших из Flowseal.
        await LoadStatsAsync(presets);

        if (changed)
        {
            try { await SavePresetsAsync(presets); } catch { /* ignore */ }
        }

        return presets;
    }

    public async Task SavePresetsAsync(List<Preset> presets)
    {
        string json = JsonSerializer.Serialize(presets, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(_presetsFilePath, json);

        // Сохранить статистику отдельно
        await SaveStatsAsync(presets);
    }

    private async Task LoadStatsAsync(List<Preset> presets)
    {
        if (!File.Exists(_statsFilePath)) return;

        try
        {
            string json = await File.ReadAllTextAsync(_statsFilePath);
            var stats = JsonSerializer.Deserialize<List<PresetStats>>(json) ?? new List<PresetStats>();

            foreach (var preset in presets)
            {
                var stat = stats.FirstOrDefault(s => s.Id == preset.Id);
                if (stat != null)
                {
                    preset.LastUsed = stat.LastUsed;
                    preset.SuccessCount = stat.SuccessCount;
                    preset.FailCount = stat.FailCount;
                }
            }
        }
        catch
        {
            // Игнорировать ошибки загрузки статистики
        }
    }

    private async Task SaveStatsAsync(List<Preset> presets)
    {
        var stats = presets.Select(p => new PresetStats
        {
            Id = p.Id,
            LastUsed = p.LastUsed,
            SuccessCount = p.SuccessCount,
            FailCount = p.FailCount
        }).ToList();

        string json = JsonSerializer.Serialize(stats, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(_statsFilePath, json);
    }

    public async Task UpdatePresetStatsAsync(string presetId, bool success)
    {
        var presets = await LoadPresetsAsync();
        var preset = presets.FirstOrDefault(p => p.Id == presetId);

        if (preset == null) return;

        if (success)
            preset.SuccessCount++;
        else
            preset.FailCount++;

        preset.LastUsed = DateTime.UtcNow;
        await SaveStatsAsync(presets);
    }

    public async Task<List<Preset>> GetPresetsByCategoryAsync(string category)
    {
        var presets = await LoadPresetsAsync();
        return presets.Where(p => p.Category == category).ToList();
    }

    public async Task<List<Preset>> GetRecommendedPresetsAsync()
    {
        var presets = await LoadPresetsAsync();
        return presets
            .OrderByDescending(p => p.SuccessRate)
            .ThenByDescending(p => p.LastUsed)
            .ToList();
    }

    private class PresetStats
    {
        public string Id { get; set; } = string.Empty;
        public DateTime LastUsed { get; set; }
        public int SuccessCount { get; set; }
        public int FailCount { get; set; }
    }
}