using StormV.Models;

namespace StormV.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly SingBoxService _singBox = new();
    private AppSettings _settings = ConfigService.LoadSettings();
    private CancellationTokenSource? _monitorCts;
    private CancellationTokenSource? _clashPollCts;
    private ServerConfig? _lastWorkingServer;
    private int _healthFailCount;

    // Все серверы (включая скрытый IsAuto). Servers содержит только видимые.
    private List<ServerConfig> _allServers = new();

    [ObservableProperty]
    private ObservableCollection<ServerViewModel> _servers = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsConnected))]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    [NotifyPropertyChangedFor(nameof(ConnectButtonText))]
    [NotifyPropertyChangedFor(nameof(CanConnect))]
    [NotifyPropertyChangedFor(nameof(CanAutoConnect))]
    private ConnectionStatus _status = ConnectionStatus.Disconnected;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanConnect))]
    private ServerViewModel? _selectedServerVm;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private string _logText = string.Empty;

    // Тег активного сервера из Clash API (для строки "Сервер:" при авто-режиме)
    [ObservableProperty]
    private string _activeServerTag = string.Empty;

    public bool IsConnected => Status == ConnectionStatus.Connected;
    public bool CanConnect  => SelectedServerVm != null && Status != ConnectionStatus.Connecting;

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

    // Строка "Сервер:" — показывает активный из urltest или выбранный вручную
    public string SelectedServerDisplay
    {
        get
        {
            if (IsConnected && !string.IsNullOrEmpty(ActiveServerTag))
                return ActiveServerTag;
            return SelectedServerVm?.Config.DisplayName ?? "Не выбран";
        }
    }

    public event Action? AddServerRequested;

    public MainViewModel()
    {
        _allServers = ConfigService.LoadServers();
        foreach (var s in _allServers.Where(s => !s.IsAuto))
            Servers.Add(new ServerViewModel(s));
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
                    StopClashApiPolling();
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
        _allServers.Add(server);
        if (!server.IsAuto)
        {
            var vm = new ServerViewModel(server);
            Servers.Add(vm);
            SelectedServerVm ??= vm;
            _ = PingSingleServerAsync(vm);
        }
        ConfigService.SaveServers(_allServers);
    }

    public void AddSubscriptionServers(List<ServerConfig> servers, string subscriptionUrl = "")
    {
        if (!string.IsNullOrEmpty(subscriptionUrl))
        {
            // Удаляем старые серверы этой подписки
            var oldVms = Servers.Where(v => v.Config.SubscriptionUrl == subscriptionUrl).ToList();
            foreach (var v in oldVms) Servers.Remove(v);
            _allServers.RemoveAll(s => s.SubscriptionUrl == subscriptionUrl);

            foreach (var s in servers) s.SubscriptionUrl = subscriptionUrl;

            if (!_settings.SubscriptionUrls.Contains(subscriptionUrl))
            {
                _settings.SubscriptionUrls.Add(subscriptionUrl);
                ConfigService.SaveSettings(_settings);
            }
        }

        _allServers.AddRange(servers);

        foreach (var server in servers.Where(s => !s.IsAuto))
        {
            var vm = new ServerViewModel(server);
            Servers.Add(vm);
            _ = PingSingleServerAsync(vm);
        }

        ConfigService.SaveServers(_allServers);
        SelectedServerVm ??= Servers.FirstOrDefault();
        Logger.Instance.Info("UI", $"Подписка: добавлено {servers.Count(s => !s.IsAuto)} серверов");
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
        foreach (var url in _settings.SubscriptionUrls.ToList())
        {
            var (servers, error) = await SubscriptionService.FetchAsync(url);
            if (!string.IsNullOrEmpty(error))
            {
                Logger.Instance.Error("UI", $"Ошибка обновления: {error}");
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
        _allServers.Remove(vm.Config);
        ConfigService.SaveServers(_allServers);
        if (SelectedServerVm == vm) SelectedServerVm = Servers.FirstOrDefault();
    }

    // ── Ping ─────────────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task PingAllServers() => await PingAllServersAsync();

    private async Task PingAllServersAsync()
        => await Task.WhenAll(Servers.Select(PingSingleServerAsync));

    private async Task PingSingleServerAsync(ServerViewModel vm)
    {
        vm.Ping = "...";
        var result = await ProtocolSelector.TestOneAsync(vm.Config);
        vm.Ping = result.IsAvailable ? $"{result.LatencyMs} ms" : "—";
    }

    // ── Auto-connect ──────────────────────────────────────────────────────────

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

        // Если есть авто-сервер из подписки — подключаемся через него (urltest)
        var autoServer = _allServers.FirstOrDefault(s => s.IsAuto);
        if (autoServer != null)
        {
            Logger.Instance.Info("UI", "Auto-режим: urltest подберёт лучший сервер");
            await ConnectToAsync(autoServer);
            IsTesting = false;
            if (Status == ConnectionStatus.Connected) StartMonitor();
            return;
        }

        // Нет подписки — перебираем серверы вручную
        Logger.Instance.Info("UI", "Поиск лучшего протокола...");
        var configs = Servers.Select(v => v.Config).ToList();
        var results = await ProtocolSelector.TestAllAsync(configs);

        foreach (var r in results)
        {
            var vm = Servers.FirstOrDefault(v => v.Config.Id == r.Server.Id);
            if (vm != null) vm.Ping = r.IsAvailable ? $"{r.LatencyMs} ms" : "—";
        }

        IsTesting = false;

        var allToTry = results.Where(r => r.IsAvailable)
            .Concat(results.Where(r => !r.IsAvailable))
            .ToList();

        if (allToTry.Count == 0) { Status = ConnectionStatus.Error; ErrorMessage = "Нет серверов"; return; }

        foreach (var candidate in allToTry)
        {
            var vm = Servers.First(v => v.Config.Id == candidate.Server.Id);
            SelectedServerVm = vm;
            Logger.Instance.Info("UI", $"Пробуем: {candidate.Server.DisplayName}");

            await ConnectToAsync(candidate.Server);
            if (Status != ConnectionStatus.Connected) continue;

            Logger.Instance.Info("Health", "Проверка Telegram/YouTube...");
            var ok = await HealthChecker.IsWorkingAsync();
            if (ok)
            {
                _lastWorkingServer = candidate.Server;
                Logger.Instance.Info("Health", $"Работает: {candidate.Server.DisplayName}");
                StartMonitor();
                return;
            }

            Logger.Instance.Warning("Health", $"Не работает: {candidate.Server.DisplayName}");
            DisconnectInternal();
            await Task.Delay(500);
        }

        ErrorMessage = "Ни один протокол не обеспечивает доступ к Telegram/YouTube";
        Status = ConnectionStatus.Error;
    }

    // ── Background health monitor ─────────────────────────────────────────────

    private void StartMonitor()
    {
        _monitorCts?.Cancel();
        _monitorCts = new CancellationTokenSource();
        _healthFailCount = 0;
        _ = MonitorLoopAsync(_monitorCts.Token);
    }

    private void StopMonitor()
    {
        _monitorCts?.Cancel();
        _monitorCts = null;
    }

    private async Task MonitorLoopAsync(CancellationToken ct)
    {
        Logger.Instance.Info("Monitor", "Мониторинг запущен (30 сек, переключение после 2 неудач)");
        while (!ct.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(30), ct).ConfigureAwait(false);
            if (ct.IsCancellationRequested || Status != ConnectionStatus.Connected) break;

            var result = await HealthChecker.CheckAsync();
            if (result.IsOk)
            {
                _healthFailCount = 0;
                Logger.Instance.Debug("Monitor",
                    $"OK — Telegram {result.TelegramMs}ms, YouTube {result.YoutubeMs}ms");
                continue;
            }

            _healthFailCount++;
            Logger.Instance.Warning("Monitor",
                $"[{_healthFailCount}/2] Недоступно: {result.FailedService} " +
                $"(Telegram {result.TelegramMs}ms, YouTube {result.YoutubeMs}ms)");

            if (_healthFailCount < 2) continue;

            _healthFailCount = 0;
            Logger.Instance.Warning("Monitor", "Два сбоя подряд — переподключение...");
            await Application.Current.Dispatcher.InvokeAsync(async () =>
            {
                if (Status == ConnectionStatus.Connected)
                    await AutoConnectCommand.ExecuteAsync(null);
            });
            break;
        }
        Logger.Instance.Info("Monitor", "Мониторинг остановлен");
    }

    // ── Clash API polling (активный сервер urltest) ───────────────────────────

    private void StartClashApiPolling()
    {
        _clashPollCts?.Cancel();
        _clashPollCts = new CancellationTokenSource();
        _ = PollClashApiAsync(_clashPollCts.Token);
    }

    private void StopClashApiPolling()
    {
        _clashPollCts?.Cancel();
        _clashPollCts = null;
        Application.Current.Dispatcher.Invoke(() =>
        {
            foreach (var vm in Servers) vm.IsActive = false;
            ActiveServerTag = string.Empty;
            OnPropertyChanged(nameof(SelectedServerDisplay));
        });
    }

    private async Task PollClashApiAsync(CancellationToken ct)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(3000, ct);
                var json = await http.GetStringAsync(
                    $"http://127.0.0.1:{SingBoxService.ClashApiPort}/proxies/auto", ct);

                using var doc = JsonDocument.Parse(json);
                if (!doc.RootElement.TryGetProperty("now", out var now)) continue;

                var tag = now.GetString() ?? string.Empty;
                if (tag == ActiveServerTag) continue;

                Application.Current.Dispatcher.Invoke(() =>
                {
                    ActiveServerTag = tag;
                    foreach (var vm in Servers)
                        vm.IsActive = vm.Config.Name == tag;
                    OnPropertyChanged(nameof(SelectedServerDisplay));
                    Logger.Instance.Debug("ClashAPI", $"Активный сервер: {tag}");
                });
            }
            catch (OperationCanceledException) { break; }
            catch { /* sing-box ещё инициализируется */ }
        }
    }

    // ── Connect / Disconnect ──────────────────────────────────────────────────

    [RelayCommand]
    private async Task ToggleConnection()
    {
        if (Status is ConnectionStatus.Connected or ConnectionStatus.Connecting)
        {
            DisconnectInternal();
            return;
        }
        if (SelectedServerVm == null) return;
        Logger.Instance.Info("UI", $"Подключение → {SelectedServerVm.Config.DisplayName}");
        await ConnectToAsync(SelectedServerVm.Config);
    }

    private async Task ConnectToAsync(ServerConfig config)
    {
        // Если выбран сервер из подписки → используем скрытый авто-конфиг (urltest)
        var serverToUse = config.IsSubscription
            ? _allServers.FirstOrDefault(s => s.IsAuto) ?? config
            : config;

        Status = ConnectionStatus.Connecting;
        ErrorMessage = string.Empty;
        LogText = string.Empty;

        var (success, error) = await _singBox.StartAsync(serverToUse);

        if (success)
        {
            ProxyService.SetProxy(SingBoxService.MixedPort);
            Status = ConnectionStatus.Connected;
            Logger.Instance.Info("UI", $"Подключено [{(serverToUse.IsAuto ? "urltest" : serverToUse.DisplayName)}]");
            if (serverToUse.IsAuto) StartClashApiPolling();
            OnPropertyChanged(nameof(SelectedServerDisplay));
        }
        else
        {
            Status = ConnectionStatus.Error;
            ErrorMessage = error;
            ProxyService.ClearProxy();
            Logger.Instance.Error("UI", $"Ошибка: {error}");
        }
    }

    private void DisconnectInternal()
    {
        Logger.Instance.Info("UI", "Отключение...");
        StopMonitor();
        StopClashApiPolling();
        _singBox.Stop();
        ProxyService.ClearProxy();
        Status = ConnectionStatus.Disconnected;
        ErrorMessage = string.Empty;
        OnPropertyChanged(nameof(SelectedServerDisplay));
        Logger.Instance.Info("UI", "Отключено");
    }

    public void OnClosing()
    {
        Logger.Instance.Info("App", "Приложение закрывается");
        DisconnectInternal();
    }
}
