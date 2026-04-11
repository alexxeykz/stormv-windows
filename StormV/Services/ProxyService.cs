using Microsoft.Win32;

namespace StormV.Services;

/// <summary>
/// Устанавливает / снимает системный прокси Windows.
/// </summary>
public static class ProxyService
{
    private const string RegistryKey =
        @"Software\Microsoft\Windows\CurrentVersion\Internet Settings";

    public static void SetProxy(int port)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RegistryKey, writable: true);
        if (key == null) { Logger.Instance.Warning("Proxy", "Не удалось открыть реестр"); return; }
        key.SetValue("ProxyEnable", 1);
        key.SetValue("ProxyServer", $"127.0.0.1:{port}");
        RefreshIE();
        Logger.Instance.Info("Proxy", $"Системный прокси установлен: 127.0.0.1:{port}");
    }

    public static void ClearProxy()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RegistryKey, writable: true);
        if (key == null) return;
        key.SetValue("ProxyEnable", 0);
        key.DeleteValue("ProxyServer", throwOnMissingValue: false);
        RefreshIE();
        Logger.Instance.Info("Proxy", "Системный прокси снят");
    }

    [System.Runtime.InteropServices.DllImport("wininet.dll")]
    private static extern bool InternetSetOption(
        nint hInternet, int dwOption, nint lpBuffer, int dwBufferLength);

    private static void RefreshIE()
    {
        InternetSetOption(0, 39, 0, 0); // INTERNET_OPTION_SETTINGS_CHANGED
        InternetSetOption(0, 37, 0, 0); // INTERNET_OPTION_REFRESH
    }
}
