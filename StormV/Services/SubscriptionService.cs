namespace StormV.Services;

/// <summary>
/// Скачивает подписку и возвращает список серверов.
///
/// Sing-box JSON (User-Agent: StormV/1.0 sing-box):
///   - Возвращает [autoServer(IsAuto=true)] + [серверы(IsSubscription=true)]
///   - autoServer хранит полный SingboxConfig с urltest + Clash API
///   - Индивидуальные серверы — только для отображения в UI
///
/// Base64 / plain-text: разбирается как раньше в список ServerConfig.
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

            var singboxServers = TrySingboxParse(raw);
            if (singboxServers != null)
            {
                if (singboxServers.Count == 0)
                    return (singboxServers, "Не найдено ни одного сервера в подписке");
                var vis = singboxServers.Count(s => !s.IsAuto);
                Logger.Instance.Info("Sub", $"Singbox JSON: {vis} серверов + auto-конфиг");
                return (singboxServers, string.Empty);
            }

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

    // ── Sing-box JSON parsing ─────────────────────────────────────────────────

    private static readonly HashSet<string> VpnTypes = new(StringComparer.OrdinalIgnoreCase)
        { "vless", "vmess", "trojan", "shadowsocks", "hysteria2", "tuic", "wireguard" };

    private static List<ServerConfig>? TrySingboxParse(string raw)
    {
        if (!raw.StartsWith("{")) return null;
        try
        {
            using var doc = JsonDocument.Parse(raw);
            if (!doc.RootElement.TryGetProperty("outbounds", out var outbounds)) return null;

            var individualConfigs = new List<ServerConfig>();
            var serverTags        = new List<string>();
            var serverRawJsons    = new List<string>();

            foreach (var ob in outbounds.EnumerateArray())
            {
                if (!ob.TryGetProperty("type", out var typeProp)) continue;
                var type = typeProp.GetString() ?? "";
                if (!VpnTypes.Contains(type)) continue;

                var tag = ob.TryGetProperty("tag", out var tg) ? tg.GetString() ?? "" : "";
                if (string.IsNullOrEmpty(tag)) continue;

                var cfg = ParseSingboxOutbound(ob, type);
                if (cfg == null) continue;

                cfg.IsSubscription = true;
                individualConfigs.Add(cfg);
                serverTags.Add(tag);
                serverRawJsons.Add(ob.GetRawText());
            }

            if (individualConfigs.Count == 0) return null;

            var autoConfig = BuildAutoSingboxConfig(serverTags, serverRawJsons);
            var autoServer = new ServerConfig
            {
                Name          = $"Auto · {individualConfigs.Count} серв.",
                IsAuto        = true,
                SingboxConfig = autoConfig,
                ServerCount   = individualConfigs.Count,
                Protocol      = Protocol.Vless // заглушка для сериализации
            };

            return new List<ServerConfig> { autoServer }.Concat(individualConfigs).ToList();
        }
        catch (Exception ex)
        {
            Logger.Instance.Warning("Sub", $"TrySingboxParse error: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Строит полный sing-box конфиг с urltest, Clash API и domain-routing.
    /// Вызывается при каждом обновлении подписки.
    /// </summary>
    private static string BuildAutoSingboxConfig(List<string> serverTags, List<string> serverRawJsons)
    {
        var customDomains = SettingsService.Load().ProxyDomains;

        // ── Outbounds ────────────────────────────────────────────────────────
        var outboundsArr = new JsonArray();
        foreach (var json in serverRawJsons)
        {
            try { outboundsArr.Add(JsonNode.Parse(json)); }
            catch { /* пропускаем невалидные */ }
        }

        // urltest group — sing-box сам выбирает лучший сервер
        var tagsArr = new JsonArray();
        foreach (var t in serverTags) tagsArr.Add(t);

        outboundsArr.Add(new JsonObject
        {
            ["type"]      = "urltest",
            ["tag"]       = "auto",
            ["outbounds"] = tagsArr,
            ["url"]       = "http://www.gstatic.com/generate_204",
            ["interval"]  = "3m",
            ["tolerance"] = 50
        });
        outboundsArr.Add(JsonNode.Parse(@"{""type"":""direct"",""tag"":""direct""}"));
        outboundsArr.Add(JsonNode.Parse(@"{""type"":""block"",""tag"":""block""}"));

        // ── Routing rules ────────────────────────────────────────────────────
        var telegramDomains  = new[] { "telegram.org", "t.me", "telegram.me", "api.telegram.org", "cdn.telegram.org", "telegra.ph" };
        var youtubeDomains   = new[] { "youtube.com", "youtu.be", "googlevideo.com", "ytimg.com", "ggpht.com", "youtube-nocookie.com" };
        var whatsappDomains  = new[] { "whatsapp.com", "whatsapp.net" };
        var allProxyDomains  = telegramDomains.Concat(youtubeDomains).Concat(whatsappDomains).Concat(customDomains).ToArray();

        var rules = new JsonArray
        {
            new JsonObject { ["domain_suffix"] = ToJsonArray(allProxyDomains),
                             ["outbound"]      = "auto" },
            new JsonObject { ["ip_cidr"]   = ToJsonArray(new[] { "91.108.0.0/16", "91.105.192.0/23", "149.154.160.0/20", "185.76.151.0/24", "95.161.76.0/24" }),
                             ["outbound"]  = "auto" },
            new JsonObject { ["ip_cidr"]   = ToJsonArray(new[] { "10.0.0.0/8", "172.16.0.0/12", "192.168.0.0/16", "127.0.0.0/8", "169.254.0.0/16", "fc00::/7" }),
                             ["outbound"]  = "direct" }
        };

        // ── Итоговый конфиг ──────────────────────────────────────────────────
        var config = new JsonObject
        {
            ["log"] = new JsonObject { ["level"] = "info", ["timestamp"] = true },
            ["experimental"] = new JsonObject
            {
                ["clash_api"] = new JsonObject
                {
                    ["external_controller"] = $"127.0.0.1:{SingBoxService.ClashApiPort}"
                }
            },
            ["inbounds"] = new JsonArray
            {
                new JsonObject
                {
                    ["type"]        = "mixed",
                    ["tag"]         = "mixed-in",
                    ["listen"]      = "127.0.0.1",
                    ["listen_port"] = SingBoxService.MixedPort
                }
            },
            ["outbounds"] = outboundsArr,
            ["route"]     = new JsonObject { ["rules"] = rules, ["final"] = "direct" }
        };

        // ToJsonString() без параметров — в .NET 8 new JsonSerializerOptions { WriteIndented }
        // вызывает "TypeInfoResolver must be set before options are locked"
        return config.ToJsonString();
    }

    private static JsonArray ToJsonArray(IEnumerable<string> items)
    {
        var arr = new JsonArray();
        foreach (var item in items) arr.Add(item);
        return arr;
    }

    // ── Individual outbound parsing ───────────────────────────────────────────

    private static ServerConfig? ParseSingboxOutbound(JsonElement ob, string type)
    {
        var server = ob.TryGetProperty("server", out var s) ? s.GetString() ?? "" : "";
        var port   = ob.TryGetProperty("server_port", out var p) ? p.GetInt32() : 0;
        var tag    = ob.TryGetProperty("tag", out var t) ? t.GetString() ?? "" : "";
        if (string.IsNullOrEmpty(server) || port == 0) return null;

        var cfg = new ServerConfig { Name = tag, Host = server, Port = port };

        switch (type.ToLower())
        {
            case "vless":
                cfg.Protocol = Protocol.Vless;
                cfg.Uuid     = ob.TryGetProperty("uuid", out var u) ? u.GetString() ?? "" : "";
                cfg.Flow     = ob.TryGetProperty("flow", out var f) ? f.GetString() ?? "" : "";
                ParseTls(ob, cfg);
                break;

            case "trojan":
                cfg.Protocol = Protocol.Trojan;
                cfg.Password = ob.TryGetProperty("password", out var tp) ? tp.GetString() ?? "" : "";
                ParseTls(ob, cfg);
                break;

            case "shadowsocks":
                cfg.Protocol = Protocol.Shadowsocks;
                cfg.Method   = ob.TryGetProperty("method",   out var m)  ? m.GetString()  ?? "" : "";
                cfg.Password = ob.TryGetProperty("password", out var sp)  ? sp.GetString() ?? "" : "";
                break;

            case "hysteria2":
                cfg.Protocol = Protocol.Hysteria2;
                cfg.Password = ob.TryGetProperty("password", out var hp) ? hp.GetString() ?? "" : "";
                ParseTls(ob, cfg);
                break;

            case "vmess":
                cfg.Protocol = Protocol.Vmess;
                cfg.Uuid     = ob.TryGetProperty("uuid", out var vu) ? vu.GetString() ?? "" : "";
                break;

            default:
                return null;
        }
        return cfg;
    }

    private static void ParseTls(JsonElement ob, ServerConfig cfg)
    {
        if (!ob.TryGetProperty("tls", out var tls)) return;
        cfg.Sni           = tls.TryGetProperty("server_name", out var sn)  ? sn.GetString()  ?? "" : "";
        cfg.SkipCertVerify = tls.TryGetProperty("insecure",   out var ins) && ins.GetBoolean();
        if (tls.TryGetProperty("utls", out var utls) && utls.TryGetProperty("fingerprint", out var fp))
            cfg.Fingerprint = fp.GetString() ?? "chrome";
        if (tls.TryGetProperty("reality", out var reality))
        {
            cfg.Security        = "reality";
            cfg.RealityPublicKey = reality.TryGetProperty("public_key", out var pk)  ? pk.GetString()  ?? "" : "";
            cfg.RealityShortId  = reality.TryGetProperty("short_id",   out var sid) ? sid.GetString() ?? "" : "";
        }
        else if (tls.TryGetProperty("enabled", out var en) && en.GetBoolean())
        {
            cfg.Security = "tls";
        }
    }

    // ── Base64 ───────────────────────────────────────────────────────────────

    private static string? TryBase64Decode(string s)
    {
        try
        {
            var padded  = s.PadRight((s.Length + 3) / 4 * 4, '=');
            var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(padded));
            return decoded.Contains("://") ? decoded : null;
        }
        catch { return null; }
    }
}
