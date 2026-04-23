using System.Net.NetworkInformation;
using StormV.Models;

namespace StormV.Services;

public record ProtocolTestResult(ServerConfig Server, int? LatencyMs)
{
    public bool IsAvailable => LatencyMs.HasValue;
}

/// <summary>
/// Тестирует все серверы параллельно и возвращает отсортированный список.
/// TCP-протоколы: TCP connect. UDP-протоколы: ICMP ping к хосту.
/// </summary>
public static class ProtocolSelector
{
    private static readonly HashSet<Protocol> UdpProtocols = new()
    {
        Protocol.Hysteria2,
        Protocol.Tuic,
        Protocol.WireGuard,
    };

    public static async Task<List<ProtocolTestResult>> TestAllAsync(
        IEnumerable<ServerConfig> servers,
        int timeoutMs = 4000)
    {
        var tasks = servers.Select(s => TestOneAsync(s, timeoutMs));
        var results = await Task.WhenAll(tasks);
        return results
            .OrderBy(r => r.IsAvailable ? 0 : 1)
            .ThenBy(r => r.LatencyMs ?? int.MaxValue)
            .ToList();
    }

    public static async Task<ServerConfig?> BestAsync(
        IEnumerable<ServerConfig> servers,
        int timeoutMs = 4000)
    {
        var results = await TestAllAsync(servers, timeoutMs);
        return results.FirstOrDefault(r => r.IsAvailable)?.Server;
    }

    private static async Task<ProtocolTestResult> TestOneAsync(ServerConfig s, int timeoutMs)
    {
        var latency = UdpProtocols.Contains(s.Protocol)
            ? await IcmpPingAsync(s.Host, timeoutMs)
            : await PingService.PingAsync(s.Host, s.Port, timeoutMs);
        return new ProtocolTestResult(s, latency);
    }

    private static async Task<int?> IcmpPingAsync(string host, int timeoutMs)
    {
        try
        {
            using var ping = new Ping();
            var reply = await ping.SendPingAsync(host, timeoutMs);
            return reply.Status == IPStatus.Success ? (int)reply.RoundtripTime : null;
        }
        catch
        {
            return null;
        }
    }
}
