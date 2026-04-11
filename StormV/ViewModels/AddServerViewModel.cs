namespace StormV.ViewModels;

public partial class AddServerViewModel : ObservableObject
{
    [ObservableProperty]
    private string _url = string.Empty;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    public ServerConfig? Result { get; private set; }
    public bool Confirmed { get; private set; }

    public event Action? CloseRequested;

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
    private void ClearUrl() => Url = string.Empty;

    [RelayCommand]
    private void Cancel() => CloseRequested?.Invoke();
}
