using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace ZapretDesktop.Core.Models;

public class Preset : INotifyPropertyChanged
{
    private int _successCount;
    private int _failCount;
    private DateTime _lastUsed;
    private bool _isActive;
    private string _lastTestStatus = string.Empty;
    private int _lastLatencyMs;

    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = "General";
    public string Arguments { get; set; } = string.Empty;
    public bool IsBuiltIn { get; set; }

    [JsonIgnore]
    public bool IsActive
    {
        get => _isActive;
        set => Set(ref _isActive, value);
    }

    [JsonIgnore]
    public DateTime LastUsed
    {
        get => _lastUsed;
        set => Set(ref _lastUsed, value);
    }

    [JsonIgnore]
    public int SuccessCount
    {
        get => _successCount;
        set
        {
            if (Set(ref _successCount, value)) OnPropertyChanged(nameof(SuccessRate));
        }
    }

    [JsonIgnore]
    public int FailCount
    {
        get => _failCount;
        set
        {
            if (Set(ref _failCount, value)) OnPropertyChanged(nameof(SuccessRate));
        }
    }

    [JsonIgnore]
    public string LastTestStatus
    {
        get => _lastTestStatus;
        set => Set(ref _lastTestStatus, value);
    }

    [JsonIgnore]
    public int LastLatencyMs
    {
        get => _lastLatencyMs;
        set => Set(ref _lastLatencyMs, value);
    }

    [JsonIgnore]
    public double SuccessRate => SuccessCount + FailCount == 0 ? 0 : (SuccessCount * 100.0) / (SuccessCount + FailCount);

    public event PropertyChangedEventHandler? PropertyChanged;

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}