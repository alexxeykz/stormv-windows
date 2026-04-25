namespace StormV.Models;

public class ServerConfig
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public Protocol Protocol { get; set; }
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; }
    public string RawUrl { get; set; } = string.Empty;

    // URL подписки, из которой добавлен сервер (пусто — если добавлен вручную)
    public string SubscriptionUrl { get; set; } = string.Empty;

    // VLESS / VMess
    public string Uuid { get; set; } = string.Empty;
    public string Flow { get; set; } = string.Empty;
    public string Encryption { get; set; } = "none";

    // Stream / Transport
    public string Network { get; set; } = "tcp";
    public string Security { get; set; } = "none";
    public string Sni { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string Host2 { get; set; } = string.Empty; // WS host header

    // REALITY
    public string RealityPublicKey { get; set; } = string.Empty;
    public string RealityShortId { get; set; } = string.Empty;
    public string Fingerprint { get; set; } = "chrome";
    public string SpiderX { get; set; } = string.Empty;

    // Shadowsocks
    public string Method { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;

    // VMess alterId
    public int AlterId { get; set; } = 0;

    // Trojan
    // uses Password field

    // Hysteria2
    public string Obfs { get; set; } = string.Empty;
    public string ObfsPassword { get; set; } = string.Empty;
    public bool SkipCertVerify { get; set; } = false;

    // TUIC
    public string CongestionControl { get; set; } = "bbr";
    public string Token { get; set; } = string.Empty;

    // WireGuard
    public string PrivateKey { get; set; } = string.Empty;
    public string PublicKey { get; set; } = string.Empty;
    public string PresharedKey { get; set; } = string.Empty;
    public string PeerEndpoint { get; set; } = string.Empty;
    public List<string> AllowedIPs { get; set; } = new() { "0.0.0.0/0", "::/0" };
    public string LocalAddress { get; set; } = string.Empty;
    public int Mtu { get; set; } = 1420;

    // ── Auto-режим (urltest через sing-box JSON подписки) ──────────────────────
    // IsAuto=true  — скрытый сервер, хранит SingboxConfig с urltest, не показывается в списке
    // IsSubscription=true — видимый сервер из подписки, при подключении → используется IsAuto-конфиг
    public bool IsAuto { get; set; } = false;
    public bool IsSubscription { get; set; } = false;
    public string SingboxConfig { get; set; } = string.Empty;
    public int ServerCount { get; set; } = 0;

    public string ProtocolLabel => IsAuto ? "AUTO" : Protocol switch
    {
        Protocol.Vless => "VLESS",
        Protocol.Vmess => "VMess",
        Protocol.Shadowsocks => "SS",
        Protocol.Trojan => "Trojan",
        Protocol.Hysteria2 => "Hy2",
        Protocol.Tuic => "TUIC",
        Protocol.WireGuard => "WG",
        _ => "?"
    };

    public string DisplayName => IsAuto
        ? $"Auto · {ServerCount} серв."
        : string.IsNullOrWhiteSpace(Name) ? $"{ProtocolLabel} · {Host}:{Port}" : Name;
}
