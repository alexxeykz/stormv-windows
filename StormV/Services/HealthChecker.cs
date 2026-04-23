namespace StormV.Services;

/// <summary>
/// Проверяет доступность Telegram и YouTube через локальный прокси sing-box.
/// </summary>
public static class HealthChecker
{
    private static readonly string[] CheckUrls =
    {
        "https://telegram.org",
        "https://www.youtube.com",
    };

    /// <summary>
    /// Возвращает true если хотя бы один из CheckUrls отвечает через прокси.
    /// </summary>
    public static async Task<bool> IsWorkingAsync(int proxyPort = SingBoxService.MixedPort, int timeoutMs = 5000)
    {
        var handler = new HttpClientHandler
        {
            Proxy = new System.Net.WebProxy($"http://127.0.0.1:{proxyPort}"),
            UseProxy = true,
            ServerCertificateCustomValidationCallback = (_, _, _, _) => true
        };

        using var client = new HttpClient(handler) { Timeout = TimeSpan.FromMilliseconds(timeoutMs) };

        foreach (var url in CheckUrls)
        {
            try
            {
                var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                if (response.IsSuccessStatusCode || (int)response.StatusCode < 500)
                {
                    Logger.Instance.Debug("Health", $"OK: {url}");
                    return true;
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.Debug("Health", $"Fail: {url} — {ex.Message}");
            }
        }

        return false;
    }
}
