namespace StormV.Models;

public class AppSettings
{
    public string DnsPrimary { get; set; } = "8.8.8.8";
    public string DnsSecondary { get; set; } = "8.8.4.4";
    public bool AutoConnectOnStart { get; set; } = false;
    public string LastSelectedServerId { get; set; } = string.Empty;

    // Сохранённые URL подписок
    public List<string> SubscriptionUrls { get; set; } = new();

    // Пользовательские домены — через прокси
    public List<string> ProxyDomains { get; set; } = new();

    // Bypass: приложения/IP которые не идут через VPN
    public List<string> BypassList { get; set; } = new()
    {
        "192.168.0.0/16",
        "10.0.0.0/8",
        "172.16.0.0/12"
    };
}
