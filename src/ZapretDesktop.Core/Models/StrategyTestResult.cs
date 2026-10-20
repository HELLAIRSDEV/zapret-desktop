using ZapretDesktop.Core.Models;

namespace ZapretDesktop.Core.Models;

/// <summary>
/// Результат проверки одной стратегии.
/// </summary>
public class StrategyTestResult
{
    public required Preset Preset { get; init; }

    public bool IsReachable { get; init; }

    public int PassedUrls { get; init; }

    public int TotalUrls { get; init; }

    public int LatencyMs { get; init; }

    public double Score { get; init; }

    public string Details { get; init; } = string.Empty;

    public double SuccessPercent => TotalUrls == 0 ? 0 : PassedUrls * 100.0 / TotalUrls;

    public string StatusIcon => IsReachable ? "✅" : "❌";
}

public class AutoTestSummary
{
    public List<StrategyTestResult> Results { get; } = new();

    public StrategyTestResult? Best { get; set; }

    public bool Cancelled { get; set; }
}
