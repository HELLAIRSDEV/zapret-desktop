namespace ZapretDesktop.Core.Models;

/// <summary>
/// Публичный MTProto-прокси, найденный автоматически.
/// </summary>
public class ProxyCandidate
{
    public required string Host { get; init; }
    public int Port { get; init; }
    public required string Secret { get; init; }
    public int LatencyMs { get; set; }

    public string HostPort => $"{Host}:{Port}";

    public string Display => $"{Host}:{Port} · {LatencyMs} мс";

    public string TgLink => Services.TelegramProxyService.BuildTgLink(Host, Port, Secret);
}
