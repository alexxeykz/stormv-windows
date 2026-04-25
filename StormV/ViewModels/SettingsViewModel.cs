namespace StormV.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly AppSettings _original;

    [ObservableProperty] private string _dnsPrimary;
    [ObservableProperty] private string _dnsSecondary;
    [ObservableProperty] private bool _autoConnectOnStart;
    [ObservableProperty] private string _bypassText;
    [ObservableProperty] private string _proxyDomainsText;

    // ── Версии и обновления ───────────────────────────────────────────────────

    [ObservableProperty] private string _appVersion;
    [ObservableProperty] private string _singBoxVersion = "...";
    [ObservableProperty] private string _updateStatusText = "";
    [ObservableProperty] private bool _isCheckingUpdates = false;
    [ObservableProperty] private bool _isUpdatingSingBox = false;
    [ObservableProperty] private int  _singBoxDownloadProgress = 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanUpdateSingBox))]
    private bool _singBoxUpdateAvailable = false;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanDownloadApp))]
    private bool _appUpdateAvailable = false;

    private string? _singBoxDownloadUrl;
    private string? _appDownloadUrl;
    private string  _singBoxPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "sing-box.exe");

    public bool CanUpdateSingBox => SingBoxUpdateAvailable && !IsUpdatingSingBox;
    public bool CanDownloadApp   => AppUpdateAvailable;

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
        _appVersion = $"StormV {UpdateService.GetCurrentAppVersion()}";

        _ = LoadSingBoxVersionAsync();
    }

    private async Task LoadSingBoxVersionAsync()
    {
        SingBoxVersion = await UpdateService.GetCurrentSingBoxVersionAsync(_singBoxPath);
    }

    [RelayCommand]
    private async Task CheckUpdates()
    {
        IsCheckingUpdates = true;
        SingBoxUpdateAvailable = false;
        AppUpdateAvailable = false;
        UpdateStatusText = "Проверяю обновления...";

        try
        {
            var sbTask  = UpdateService.CheckSingBoxUpdateAsync(_singBoxPath);
            var appTask = UpdateService.CheckAppUpdateAsync();
            await Task.WhenAll(sbTask, appTask);

            var sb  = await sbTask;
            var app = await appTask;

            if (sb != null)
            {
                _singBoxDownloadUrl  = sb.DownloadUrl;
                SingBoxUpdateAvailable = true;
                Logger.Instance.Info("Update", $"Доступна sing-box {sb.Version}");
            }
            if (app != null)
            {
                _appDownloadUrl   = app.DownloadUrl;
                AppUpdateAvailable  = true;
                Logger.Instance.Info("Update", $"Доступна StormV {app.Version}");
            }

            UpdateStatusText = (sb == null && app == null)
                ? "Все компоненты актуальны ✓"
                : $"Доступны обновления: " +
                  string.Join(", ", new[] {
                      sb  != null ? $"sing-box {sb.Version}" : null,
                      app != null ? $"StormV {app.Version}" : null
                  }.Where(x => x != null));
        }
        catch (Exception ex)
        {
            UpdateStatusText = $"Ошибка проверки: {ex.Message}";
        }
        finally
        {
            IsCheckingUpdates = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanUpdateSingBox))]
    private async Task UpdateSingBox()
    {
        if (_singBoxDownloadUrl == null) return;
        IsUpdatingSingBox = true;
        SingBoxDownloadProgress = 0;
        UpdateStatusText = "Скачиваю sing-box...";

        try
        {
            var progress = new Progress<int>(p =>
            {
                SingBoxDownloadProgress = p;
                UpdateStatusText = $"Скачиваю sing-box... {p}%";
            });

            await UpdateService.UpdateSingBoxAsync(_singBoxPath, _singBoxDownloadUrl, progress);

            SingBoxUpdateAvailable = false;
            SingBoxVersion = await UpdateService.GetCurrentSingBoxVersionAsync(_singBoxPath);
            UpdateStatusText = "sing-box обновлён ✓  Перезапустите VPN чтобы применить.";
        }
        catch (Exception ex)
        {
            UpdateStatusText = $"Ошибка обновления sing-box: {ex.Message}";
            Logger.Instance.Error("Update", ex.Message);
        }
        finally
        {
            IsUpdatingSingBox = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanDownloadApp))]
    private void DownloadApp()
    {
        if (_appDownloadUrl == null) return;
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(_appDownloadUrl) { UseShellExecute = true }); }
        catch { }
    }

    // ── Сохранение настроек ───────────────────────────────────────────────────

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
