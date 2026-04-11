namespace StormV.Services;

/// <summary>
/// Парсит ссылки всех 7 поддерживаемых протоколов в ServerConfig.
/// </summary>
public static class UrlParser
{
    public static ServerConfig? Parse(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        url = url.Trim();

        return url switch
        {
            _ when url.StartsWith("vless://", StringComparison.OrdinalIgnoreCase) => ParseVless(url),
            _ when url.StartsWith("vmess://", StringComparison.OrdinalIgnoreCase) => ParseVmess(url),
            _ when url.StartsWith("ss://", StringComparison.OrdinalIgnoreCase) => ParseShadowsocks(url),
            _ when url.StartsWith("trojan://", StringComparison.OrdinalIgnoreCase) => ParseTrojan(url),
            _ when url.StartsWith("hysteria2://", StringComparison.OrdinalIgnoreCase) => ParseHysteria2(url),
            _ when url.StartsWith("hy2://", StringComparison.OrdinalIgnoreCase) => ParseHysteria2(url),
            _ when url.StartsWith("tuic://", StringComparison.OrdinalIgnoreCase) => ParseTuic(url),
            _ when url.StartsWith("wireguard://", StringComparison.OrdinalIgnoreCase) => ParseWireGuard(url),
            _ when url.StartsWith("wg://", StringComparison.OrdinalIgnoreCase) => ParseWireGuard(url),
            _ => null
        };
    }

    // ─── VLESS ───────────────────────────────────────────────────────────────
    // vless://uuid@host:port?encryption=none&security=reality&pbk=...&sid=...&fp=...&sni=...&flow=...#name
    private static ServerConfig? ParseVless(string url)
    {
        try
        {
            var uri = new Uri(url);
            var query = ParseQuery(uri.Query);
            var cfg = new ServerConfig
            {
                RawUrl = url,
                Protocol = Protocol.Vless,
                Uuid = uri.UserInfo,
                Host = uri.Host,
                Port = uri.Port,
                Name = Uri.UnescapeDataString(uri.Fragment.TrimStart('#')),
                Encryption = GetQuery(query, "encryption", "none"),
                Flow = GetQuery(query, "flow"),
                Network = GetQuery(query, "type", "tcp"),
                Security = GetQuery(query, "security", "none"),
                Sni = GetQuery(query, "sni"),
                Fingerprint = GetQuery(query, "fp", "chrome"),
                RealityPublicKey = GetQuery(query, "pbk"),
                RealityShortId = GetQuery(query, "sid"),
                SpiderX = GetQuery(query, "spx"),
                Path = Uri.UnescapeDataString(GetQuery(query, "path")),
                Host2 = GetQuery(query, "host"),
            };
            return cfg;
        }
        catch { return null; }
    }

    // ─── VMESS ───────────────────────────────────────────────────────────────
    // vmess://base64(json)
    private static ServerConfig? ParseVmess(string url)
    {
        try
        {
            var b64 = url[8..];
            var padded = b64.PadRight((b64.Length + 3) / 4 * 4, '=');
            var json = Encoding.UTF8.GetString(Convert.FromBase64String(padded));
            var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            string Get(string key, string def = "") =>
                root.TryGetProperty(key, out var v) ? v.GetString() ?? def : def;
            int GetInt(string key, int def = 0) =>
                root.TryGetProperty(key, out var v) && v.TryGetInt32(out var i) ? i : def;

            var cfg = new ServerConfig
            {
                RawUrl = url,
                Protocol = Protocol.Vmess,
                Name = Get("ps"),
                Host = Get("add"),
                Port = GetInt("port"),
                Uuid = Get("id"),
                AlterId = GetInt("aid"),
                Network = Get("net", "tcp"),
                Security = Get("tls") == "tls" ? "tls" : "none",
                Sni = Get("sni"),
                Path = Get("path"),
                Host2 = Get("host"),
            };
            return cfg;
        }
        catch { return null; }
    }

    // ─── SHADOWSOCKS ─────────────────────────────────────────────────────────
    // ss://base64(method:password)@host:port#name  OR
    // ss://base64(method:password@host:port)#name
    private static ServerConfig? ParseShadowsocks(string url)
    {
        try
        {
            var uri = new Uri(url);
            string method, password;

            if (!string.IsNullOrEmpty(uri.UserInfo))
            {
                // Decoded userinfo: method:password
                var decoded = TryBase64Decode(uri.UserInfo) ?? uri.UserInfo;
                var colon = decoded.IndexOf(':');
                method = decoded[..colon];
                password = decoded[(colon + 1)..];
            }
            else
            {
                // Old format: entire authority is base64
                var noScheme = url[5..].Split('#')[0];
                var decoded = TryBase64Decode(noScheme) ?? noScheme;
                // method:password@host:port
                var atIdx = decoded.LastIndexOf('@');
                var userPart = decoded[..atIdx];
                var hostPart = decoded[(atIdx + 1)..];
                var colon = userPart.IndexOf(':');
                method = userPart[..colon];
                password = userPart[(colon + 1)..];
                var lastColon = hostPart.LastIndexOf(':');
                return new ServerConfig
                {
                    RawUrl = url,
                    Protocol = Protocol.Shadowsocks,
                    Name = Uri.UnescapeDataString(url.Contains('#') ? url[(url.IndexOf('#') + 1)..] : ""),
                    Host = hostPart[..lastColon],
                    Port = int.Parse(hostPart[(lastColon + 1)..]),
                    Method = method,
                    Password = password
                };
            }

            return new ServerConfig
            {
                RawUrl = url,
                Protocol = Protocol.Shadowsocks,
                Name = Uri.UnescapeDataString(uri.Fragment.TrimStart('#')),
                Host = uri.Host,
                Port = uri.Port,
                Method = method,
                Password = password
            };
        }
        catch { return null; }
    }

    // ─── TROJAN ──────────────────────────────────────────────────────────────
    // trojan://password@host:port?sni=...#name
    private static ServerConfig? ParseTrojan(string url)
    {
        try
        {
            var uri = new Uri(url);
            var query = ParseQuery(uri.Query);
            return new ServerConfig
            {
                RawUrl = url,
                Protocol = Protocol.Trojan,
                Name = Uri.UnescapeDataString(uri.Fragment.TrimStart('#')),
                Host = uri.Host,
                Port = uri.Port,
                Password = uri.UserInfo,
                Security = "tls",
                Sni = GetQuery(query, "sni", uri.Host),
                Fingerprint = GetQuery(query, "fp", "chrome"),
                SkipCertVerify = GetQuery(query, "allowInsecure") == "1",
                Network = GetQuery(query, "type", "tcp"),
                Path = Uri.UnescapeDataString(GetQuery(query, "path")),
            };
        }
        catch { return null; }
    }

    // ─── HYSTERIA2 ───────────────────────────────────────────────────────────
    // hysteria2://password@host:port?obfs=salamander&obfs-password=...&sni=...#name
    private static ServerConfig? ParseHysteria2(string url)
    {
        try
        {
            var normalized = url.Replace("hy2://", "hysteria2://");
            var uri = new Uri(normalized);
            var query = ParseQuery(uri.Query);
            return new ServerConfig
            {
                RawUrl = url,
                Protocol = Protocol.Hysteria2,
                Name = Uri.UnescapeDataString(uri.Fragment.TrimStart('#')),
                Host = uri.Host,
                Port = uri.Port > 0 ? uri.Port : 443,
                Password = uri.UserInfo,
                Sni = GetQuery(query, "sni", uri.Host),
                Obfs = GetQuery(query, "obfs"),
                ObfsPassword = GetQuery(query, "obfs-password"),
                SkipCertVerify = GetQuery(query, "insecure") == "1",
            };
        }
        catch { return null; }
    }

    // ─── TUIC ────────────────────────────────────────────────────────────────
    // tuic://uuid:password@host:port?congestion_control=bbr&sni=...#name
    private static ServerConfig? ParseTuic(string url)
    {
        try
        {
            var uri = new Uri(url);
            var query = ParseQuery(uri.Query);
            var userParts = uri.UserInfo.Split(':');
            return new ServerConfig
            {
                RawUrl = url,
                Protocol = Protocol.Tuic,
                Name = Uri.UnescapeDataString(uri.Fragment.TrimStart('#')),
                Host = uri.Host,
                Port = uri.Port,
                Uuid = userParts.Length > 0 ? userParts[0] : "",
                Password = userParts.Length > 1 ? userParts[1] : "",
                Sni = GetQuery(query, "sni", uri.Host),
                CongestionControl = GetQuery(query, "congestion_control", "bbr"),
                SkipCertVerify = GetQuery(query, "allow_insecure") == "1",
            };
        }
        catch { return null; }
    }

    // ─── WIREGUARD ───────────────────────────────────────────────────────────
    // wireguard://privatekey@host:port?publickey=...&presharedkey=...&ip=...&mtu=...#name
    private static ServerConfig? ParseWireGuard(string url)
    {
        try
        {
            var normalized = url.Replace("wg://", "wireguard://");
            var uri = new Uri(normalized);
            var query = ParseQuery(uri.Query);
            var ip = GetQuery(query, "ip", "10.0.0.2/32");
            return new ServerConfig
            {
                RawUrl = url,
                Protocol = Protocol.WireGuard,
                Name = Uri.UnescapeDataString(uri.Fragment.TrimStart('#')),
                Host = uri.Host,
                Port = uri.Port > 0 ? uri.Port : 51820,
                PrivateKey = Uri.UnescapeDataString(uri.UserInfo),
                PublicKey = GetQuery(query, "publickey"),
                PresharedKey = GetQuery(query, "presharedkey"),
                LocalAddress = ip,
                Mtu = int.TryParse(GetQuery(query, "mtu"), out var mtu) ? mtu : 1420,
                PeerEndpoint = $"{uri.Host}:{(uri.Port > 0 ? uri.Port : 51820)}",
            };
        }
        catch { return null; }
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────
    private static Dictionary<string, string> ParseQuery(string query)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrEmpty(query)) return dict;
        foreach (var part in query.TrimStart('?').Split('&'))
        {
            var eq = part.IndexOf('=');
            if (eq < 0) continue;
            var key = Uri.UnescapeDataString(part[..eq]);
            var val = Uri.UnescapeDataString(part[(eq + 1)..]);
            dict[key] = val;
        }
        return dict;
    }

    private static string GetQuery(Dictionary<string, string> q, string key, string def = "")
        => q.TryGetValue(key, out var v) ? v : def;

    private static string? TryBase64Decode(string s)
    {
        try
        {
            var padded = s.PadRight((s.Length + 3) / 4 * 4, '=');
            return Encoding.UTF8.GetString(Convert.FromBase64String(padded));
        }
        catch { return null; }
    }
}
