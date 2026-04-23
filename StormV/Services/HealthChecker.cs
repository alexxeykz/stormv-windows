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
    /// Проверки идут параллельно — общий таймаут равен timeoutMs, не сумме.
    /// </summary>
    public static async Task<bool> IsWorkingAsync(int proxyPort = SingBoxService.MixedPort, int timeoutMs = 8000)
    {
        var handler = new HttpClientHandler
        {
            Proxy = new System.Net.WebProxy($"http://127.0.0.1:{proxyPort}"),
            UseProxy = true,
            ServerCertificateCustomValidationCallback = (_, _, _, _) => true
        };

        using var client = new HttpClient(handler) { Timeout = TimeSpan.FromMilliseconds(timeoutMs) };
        using var cts = new CancellationTokenSource(timeoutMs);

        var tasks = CheckUrls.Select(url => CheckOneAsync(client, url, cts.Token)).ToList();

        while (tasks.Count > 0)
        {
            var done = await Task.WhenAny(tasks);
            tasks.Remove(done);
            try
            {
                if (await done)
                {
                    cts.Cancel(); // останавливаем остальные
                    return true;
                }
            }
            catch { }
        }

        return false;
    }

    private static async Task<bool> CheckOneAsync(HttpClient client, string url, CancellationToken ct)
    {
        try
        {
            var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
            var ok = response.IsSuccessStatusCode || (int)response.StatusCode < 500;
            Logger.Instance.Debug("Health", ok ? $"OK: {url}" : $"Fail {(int)response.StatusCode}: {url}");
            return ok;
        }
        catch (Exception ex)
        {
            Logger.Instance.Debug("Health", $"Fail: {url} — {ex.Message}");
            return false;
        }
    }
}
