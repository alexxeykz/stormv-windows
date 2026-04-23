using StormV.Models;

namespace StormV.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly SingBoxService _singBox = new();
    private AppSettings _settings = ConfigService.LoadSettings();

    [ObservableProperty]
    private ObservableCollection<ServerViewModel> _servers = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsConnected))]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    [NotifyPropertyChangedFor(nameof(ConnectButtonText))]
    [NotifyPropertyChangedFor(nameof(CanConnect))]
    private ConnectionStatus _status = ConnectionStatus.Disconnected;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanConnect))]
    private ServerViewModel? _selectedServerVm;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private string _logText = string.Empty;

    public bool IsConnected => Status == ConnectionStatus.Connected;
    public bool CanConnect => SelectedServerVm != null && Status != ConnectionStatus.Connecting;

    public string StatusText => Status switch
    {
        ConnectionStatus.Connected    => "Подключено",
        ConnectionStatus.Connecting   => "Подключение...",
        ConnectionStatus.Disconnected => "Отключено",
        ConnectionStatus.Error        => "Ошибка",
        _ => "Отключено"
    };

    public string ConnectButtonText => Status switch
    {
        ConnectionStatus.Connected  => "ОТКЛЮЧИТЬ",
        ConnectionStatus.Connecting => "ОТКЛЮЧИТЬ",
        _ => "ПОДКЛЮЧИТЬ"
    };

    public event Action? AddServerRequested;

    public MainViewModel()
    {
        var saved = ConfigService.LoadServers();
        foreach (var s in saved) Servers.Add(new ServerViewModel(s));
        SelectedServerVm = Servers.FirstOrDefault();

        _singBox.LogReceived += line =>
            Application.Current.Dispatcher.Invoke(() =>
            {
                LogText += line + "\n";
                if (LogText.Length > 10_000) LogText = LogText[^8_000..];
            });

        _singBox.StatusChanged += (running, err) =>
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (!running)
                {
                    Status = ConnectionStatus.Error;
                    ErrorMessage = err;
                    ProxyService.ClearProxy();
                }
            });

        _ = PingAllServersAsync();
    }

    [RelayCommand]
    private void AddServer() => AddServerRequested?.Invoke();

    [RelayCommand]
    private void SelectServer(ServerViewModel? vm)
    {
        if (vm != null) SelectedServerVm = vm;
    }

    public void AddServerConfig(ServerConfig server)
    {
        var vm = new ServerViewModel(server);
        Servers.Add(vm);
        ConfigService.SaveServers(Servers.Select(v => v.Config));
        SelectedServerVm ??= vm;
        _ = PingSingleServerAsync(vm);
    }

    public void AddSubscriptionServers(List<ServerConfig> servers, string subscriptionUrl = "")
    {
        // Помечаем откуда сервер и удаляем старые серверы этой подписки
        if (!string.IsNullOrEmpty(subscriptionUrl))
        {
            var old = Servers.Where(v => v.Config.SubscriptionUrl == subscriptionUrl).ToList();
            foreach (var v in old) Servers.Remove(v);

            foreach (var s in servers) s.SubscriptionUrl = subscriptionUrl;

            if (!_settings.SubscriptionUrls.Contains(subscriptionUrl))
            {
                _settings.SubscriptionUrls.Add(subscriptionUrl);
                ConfigService.SaveSettings(_settings);
            }
        }

        foreach (var server in servers)
        {
            var vm = new ServerViewModel(server);
            Servers.Add(vm);
            _ = PingSingleServerAsync(vm);
        }
        ConfigService.SaveServers(Servers.Select(v => v.Config));
        SelectedServerVm ??= Servers.FirstOrDefault();
        Logger.Instance.Info("UI", $"Подписка: добавлено {servers.Count} серверов");
    }

    // ── Refresh subscriptions ─────────────────────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSubscriptions))]
    private bool _isRefreshing = false;

    public bool HasSubscriptions => _settings.SubscriptionUrls.Count > 0;

    [RelayCommand]
    private async Task RefreshSubscriptions()
    {
        if (_settings.SubscriptionUrls.Count == 0) return;

        IsRefreshing = true;
        Logger.Instance.Info("UI", $"Обновление {_settings.SubscriptionUrls.Count} подписок...");

        foreach (var url in _settings.SubscriptionUrls.ToList())
        {
            var (servers, error) = await SubscriptionService.FetchAsync(url);
            if (!string.IsNullOrEmpty(error))
            {
                Logger.Instance.Error("UI", $"Ошибка обновления подписки: {error}");
                continue;
            }
            AddSubscriptionServers(servers, url);
        }

        IsRefreshing = false;
        Logger.Instance.Info("UI", "Подписки обновлены");
    }

    [RelayCommand]
    private void RemoveServer(ServerViewModel? vm)
    {
        if (vm == null) return;
        if (vm == SelectedServerVm && IsConnected) DisconnectInternal();
        Servers.Remove(vm);
        ConfigService.SaveServers(Servers.Select(v => v.Config));
        if (SelectedServerVm == vm) SelectedServerVm = Servers.FirstOrDefault();
    }

    // ── Ping ─────────────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task PingAllServers() => await PingAllServersAsync();

    private async Task PingAllServersAsync()
    {
        await Task.WhenAll(Servers.Select(PingSingleServerAsync));
    }

    private async Task PingSingleServerAsync(ServerViewModel vm)
    {
        vm.Ping = "...";
        var ms = await PingService.PingAsync(vm.Config.Host, vm.Config.Port);
        vm.Ping = ms.HasValue ? $"{ms} ms" : "—";
    }

    // ── Auto-select ───────────────────────────────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanAutoConnect))]
    private bool _isTesting = false;

    public bool CanAutoConnect => !IsTesting && Servers.Count > 0 &&
                                  Status is ConnectionStatus.Disconnected or ConnectionStatus.Error;

    [RelayCommand]
    private async Task AutoConnect()
    {
        if (IsConnected) DisconnectInternal();

        IsTesting = true;
        ErrorMessage = string.Empty;
        Logger.Instance.Info("UI", "Поиск лучшего протокола...");

        var configs = Servers.Select(v => v.Config).ToList();
        var results = await ProtocolSelector.TestAllAsync(configs);

        // обновляем ping в UI по результатам теста
        foreach (var r in results)
        {
            var vm = Servers.FirstOrDefault(v => v.Config.Id == r.Server.Id);
            if (vm != null) vm.Ping = r.IsAvailable ? $"{r.LatencyMs} ms" : "—";
        }

        var best = results.FirstOrDefault(r => r.IsAvailable);
        IsTesting = false;

        if (best == null)
        {
            ErrorMessage = "Нет доступных серверов";
            Status = ConnectionStatus.Error;
            Logger.Instance.Error("UI", "AutoConnect: нет доступных серверов");
            return;
        }

        var bestVm = Servers.First(v => v.Config.Id == best.Server.Id);
        SelectedServerVm = bestVm;
        Logger.Instance.Info("UI", $"Лучший: {best.Server.DisplayName} ({best.LatencyMs} ms)");

        await ConnectToAsync(bestVm.Config);
    }

    // ── Connect ───────────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task ToggleConnection()
    {
        if (Status is ConnectionStatus.Connected or ConnectionStatus.Connecting)
        {
            DisconnectInternal();
            return;
        }
        if (SelectedServerVm == null) return;
        Logger.Instance.Info("UI", $"Пользователь нажал ПОДКЛЮЧИТЬ → {SelectedServerVm.Config.DisplayName}");
        await ConnectToAsync(SelectedServerVm.Config);
    }

    private async Task ConnectToAsync(ServerConfig config)
    {
        Status = ConnectionStatus.Connecting;
        ErrorMessage = string.Empty;
        LogText = string.Empty;

        var (success, error) = await _singBox.StartAsync(config);

        if (success)
        {
            ProxyService.SetProxy(SingBoxService.MixedPort);
            Status = ConnectionStatus.Connected;
            Logger.Instance.Info("UI", "Статус: ПОДКЛЮЧЕНО");
        }
        else
        {
            Status = ConnectionStatus.Error;
            ErrorMessage = error;
            ProxyService.ClearProxy();
            Logger.Instance.Error("UI", $"Статус: ОШИБКА — {error}");
        }
    }

    private void DisconnectInternal()
    {
        Logger.Instance.Info("UI", "Отключение...");
        _singBox.Stop();
        ProxyService.ClearProxy();
        Status = ConnectionStatus.Disconnected;
        ErrorMessage = string.Empty;
        Logger.Instance.Info("UI", "Статус: ОТКЛЮЧЕНО");
    }

    public void OnClosing()
    {
        Logger.Instance.Info("App", "Приложение закрывается");
        DisconnectInternal();
    }
}
