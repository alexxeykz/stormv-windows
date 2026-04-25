namespace StormV.Services;

/// <summary>
/// Скачивает подписку по URL и возвращает список серверов.
/// Поддерживает base64-encoded и plain-text форматы.
/// </summary>
public static class SubscriptionService
{
    private static readonly HttpClient _http = new()
    {
        Timeout = TimeSpan.FromSeconds(15),
        DefaultRequestHeaders = { { "User-Agent", "StormV/1.0 sing-box" } }
    };

    public static async Task<(List<ServerConfig> servers, string error)> FetchAsync(string url)
    {
        try
        {
            Logger.Instance.Info("Sub", $"Загрузка подписки: {url}");
            var raw = await _http.GetStringAsync(url);
            raw = raw.Trim();

            // Пробуем как sing-box JSON
            var singboxServers = TrySingboxParse(raw);
            if (singboxServers != null)
            {
                if (singboxServers.Count == 0)
                    return (singboxServers, "Не найдено ни одного сервера в подписке");
                Logger.Instance.Info("Sub", $"Загружено серверов (singbox): {singboxServers.Count}");
                return (singboxServers, string.Empty);
            }

            // Пробуем декодировать как base64
            var content = TryBase64Decode(raw) ?? raw;

            var servers = content
                .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(l => l.Trim())
                .Where(l => l.Contains("://"))
                .Select(l => UrlParser.Parse(l))
                .Where(s => s != null)
                .Cast<ServerConfig>()
                .ToList();

            if (servers.Count == 0)
                return (servers, "Не найдено ни одного сервера в подписке");

            Logger.Instance.Info("Sub", $"Загружено серверов: {servers.Count}");
            return (servers, string.Empty);
        }
        catch (Exception ex)
        {
            Logger.Instance.Error("Sub", $"Ошибка загрузки подписки: {ex.Message}");
            return (new List<ServerConfig>(), ex.Message);
        }
    }

    private static readonly HashSet<string> VpnTypes = new(StringComparer.OrdinalIgnoreCase)
        { "vless", "vmess", "trojan", "shadowsocks", "hysteria2", "tuic", "wireguard" };

    private static List<ServerConfig>? TrySingboxParse(string raw)
    {
        if (!raw.StartsWith("{")) return null;
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(raw);
            if (!doc.RootElement.TryGetProperty("outbounds", out var outbounds)) return null;

            var result = new List<ServerConfig>();
            foreach (var ob in outbounds.EnumerateArray())
            {
                if (!ob.TryGetProperty("type", out var typeProp)) continue;
                var type = typeProp.GetString() ?? "";
                if (!VpnTypes.Contains(type)) continue;

                var cfg = ParseSingboxOutbound(ob, type);
                if (cfg != null) result.Add(cfg);
            }
            return result;
        }
        catch { return null; }
    }

    private static ServerConfig? ParseSingboxOutbound(System.Text.Json.JsonElement ob, string type)
    {
        var server = ob.TryGetProperty("server", out var s) ? s.GetString() ?? "" : "";
        var port = ob.TryGetProperty("server_port", out var p) ? p.GetInt32() : 0;
        var tag = ob.TryGetProperty("tag", out var t) ? t.GetString() ?? "" : "";
        if (string.IsNullOrEmpty(server) || port == 0) return null;

        var cfg = new ServerConfig { Name = tag, Host = server, Port = port };

        switch (type.ToLower())
        {
            case "vless":
                cfg.Protocol = Protocol.VLESS;
                cfg.Uuid = ob.TryGetProperty("uuid", out var u) ? u.GetString() ?? "" : "";
                cfg.Flow = ob.TryGetProperty("flow", out var f) ? f.GetString() ?? "" : "";
                ParseTls(ob, cfg);
                break;

            case "trojan":
                cfg.Protocol = Protocol.Trojan;
                cfg.Password = ob.TryGetProperty("password", out var tp) ? tp.GetString() ?? "" : "";
                ParseTls(ob, cfg);
                break;

            case "shadowsocks":
                cfg.Protocol = Protocol.Shadowsocks;
                cfg.Method = ob.TryGetProperty("method", out var m) ? m.GetString() ?? "" : "";
                cfg.Password = ob.TryGetProperty("password", out var sp) ? sp.GetString() ?? "" : "";
                break;

            case "hysteria2":
                cfg.Protocol = Protocol.Hysteria2;
                cfg.Password = ob.TryGetProperty("password", out var hp) ? hp.GetString() ?? "" : "";
                ParseTls(ob, cfg);
                break;

            case "vmess":
                cfg.Protocol = Protocol.VMess;
                cfg.Uuid = ob.TryGetProperty("uuid", out var vu) ? vu.GetString() ?? "" : "";
                break;

            default:
                return null;
        }
        return cfg;
    }

    private static void ParseTls(System.Text.Json.JsonElement ob, ServerConfig cfg)
    {
        if (!ob.TryGetProperty("tls", out var tls)) return;
        cfg.Sni = tls.TryGetProperty("server_name", out var sn) ? sn.GetString() ?? "" : "";
        cfg.SkipCertVerify = tls.TryGetProperty("insecure", out var ins) && ins.GetBoolean();
        if (tls.TryGetProperty("utls", out var utls) && utls.TryGetProperty("fingerprint", out var fp))
            cfg.Fingerprint = fp.GetString() ?? "chrome";
        if (tls.TryGetProperty("reality", out var reality))
        {
            cfg.Security = "reality";
            cfg.RealityPublicKey = reality.TryGetProperty("public_key", out var pk) ? pk.GetString() ?? "" : "";
            cfg.RealityShortId = reality.TryGetProperty("short_id", out var sid) ? sid.GetString() ?? "" : "";
        }
        else if (tls.TryGetProperty("enabled", out var en) && en.GetBoolean())
        {
            cfg.Security = "tls";
        }
    }

    private static string? TryBase64Decode(string s)
    {
        try
        {
            var padded = s.PadRight((s.Length + 3) / 4 * 4, '=');
            var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(padded));
            return decoded.Contains("://") ? decoded : null;
        }
        catch { return null; }
    }
}
