namespace StormV.ViewModels;

/// <summary>
/// Обёртка вокруг ServerConfig с реактивным полем Ping.
/// Используется в списке серверов главного окна.
/// </summary>
public partial class ServerViewModel : ObservableObject
{
    public ServerConfig Config { get; }

    [ObservableProperty]
    private string _ping = "";

    [ObservableProperty]
    private bool _isActive = false;

    public ServerViewModel(ServerConfig config) => Config = config;
}
