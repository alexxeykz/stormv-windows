namespace StormV.ViewModels;

public partial class AddServerViewModel : ObservableObject
{
    [ObservableProperty]
    private string _url = string.Empty;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private bool _isLoading = false;

    // Несколько серверов при загрузке подписки
    public List<ServerConfig>? Results { get; private set; }
    public ServerConfig? Result { get; private set; }
    public bool Confirmed { get; private set; }

    public event Action? CloseRequested;
    public event Action<List<ServerConfig>, string>? SubscriptionLoaded;

    [RelayCommand]
    private void PasteFromClipboard()
    {
        var text = Clipboard.GetText()?.Trim() ?? string.Empty;
        Url = text;
        ErrorMessage = string.Empty;
    }

    [RelayCommand]
    private void Confirm()
    {
        ErrorMessage = string.Empty;

        if (string.IsNullOrWhiteSpace(Url))
        {
            ErrorMessage = "Введите ссылку";
            return;
        }

        // Если это http/https — грузим как подписку
        if (Url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            Url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            _ = LoadSubscriptionAsync();
            return;
        }

        var server = UrlParser.Parse(Url);
        if (server == null)
        {
            ErrorMessage = "Не удалось распознать ссылку.\nПоддерживаются: vless, vmess, ss, trojan, hysteria2, tuic, wireguard";
            return;
        }

        Result = server;
        Confirmed = true;
        CloseRequested?.Invoke();
    }

    [RelayCommand]
    private async Task LoadSubscriptionAsync()
    {
        if (string.IsNullOrWhiteSpace(Url)) return;
        IsLoading = true;
        ErrorMessage = string.Empty;

        var (servers, error) = await SubscriptionService.FetchAsync(Url);

        IsLoading = false;

        if (!string.IsNullOrEmpty(error))
        {
            ErrorMessage = error;
            return;
        }

        Results = servers;
        Confirmed = true;
        SubscriptionLoaded?.Invoke(servers, Url);
        CloseRequested?.Invoke();
    }

    [RelayCommand]
    private void ClearUrl() => Url = string.Empty;

    [RelayCommand]
    private void Cancel() => CloseRequested?.Invoke();
}
