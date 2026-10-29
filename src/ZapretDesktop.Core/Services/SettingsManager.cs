using System.Text.Json;
using ZapretDesktop.Core.Models;

namespace ZapretDesktop.Core.Services;

public class SettingsManager
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string _settingsPath;

    public SettingsManager(string? configDirectory = null)
    {
        ConfigDirectory = configDirectory ?? AppPaths.ConfigDir;
        Directory.CreateDirectory(ConfigDirectory);
        _settingsPath = Path.Combine(ConfigDirectory, "settings.json");
    }

    public string ConfigDirectory { get; }
    public string SettingsFilePath => _settingsPath;

    public AppSettings Settings { get; private set; } = new();

    public AppSettings Load()
    {
        try
        {
            if (File.Exists(_settingsPath))
            {
                var json = File.ReadAllText(_settingsPath);
                Settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
            }
        }
        catch
        {
            Settings = new AppSettings();
        }

        return Settings;
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(ConfigDirectory);
            File.WriteAllText(_settingsPath, JsonSerializer.Serialize(Settings, JsonOptions));
        }
        catch
        {
            // Настройки не критичны — не роняем приложение из-за ошибки записи.
        }
    }
}
