namespace StormV.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly AppSettings _original;

    [ObservableProperty] private string _dnsPrimary;
    [ObservableProperty] private string _dnsSecondary;
    [ObservableProperty] private bool _autoConnectOnStart;
    [ObservableProperty] private string _bypassText;
    [ObservableProperty] private string _proxyDomainsText;

    public event Action? CloseRequested;
    public event Action? ExitRequested;

    public SettingsViewModel()
    {
        _original = SettingsService.Load();
        _dnsPrimary = _original.DnsPrimary;
        _dnsSecondary = _original.DnsSecondary;
        _autoConnectOnStart = _original.AutoConnectOnStart;
        _bypassText = string.Join("\n", _original.BypassList);
        _proxyDomainsText = string.Join("\n", _original.ProxyDomains);
    }

    [RelayCommand]
    private void Save()
    {
        var settings = new AppSettings
        {
            DnsPrimary = DnsPrimary.Trim(),
            DnsSecondary = DnsSecondary.Trim(),
            AutoConnectOnStart = AutoConnectOnStart,
            SubscriptionUrls = _original.SubscriptionUrls,
            BypassList = BypassText
                .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(l => l.Trim())
                .Where(l => l.Length > 0)
                .ToList(),
            ProxyDomains = ProxyDomainsText
                .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(l => l.Trim().ToLower())
                .Where(l => l.Length > 0)
                .Distinct()
                .ToList()
        };
        SettingsService.Save(settings);
        CloseRequested?.Invoke();
    }

    [RelayCommand]
    private void Cancel() => CloseRequested?.Invoke();

    [RelayCommand]
    private void Exit() => ExitRequested?.Invoke();

    [RelayCommand]
    private void ResetDefaults()
    {
        DnsPrimary = "8.8.8.8";
        DnsSecondary = "8.8.4.4";
        AutoConnectOnStart = false;
        BypassText = "192.168.0.0/16\n10.0.0.0/8\n172.16.0.0/12";
    }
}
