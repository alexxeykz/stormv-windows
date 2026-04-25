using System.Diagnostics;

namespace StormV.Services;

public record HealthResult(bool IsOk, string? FailedService, int TelegramMs, int YoutubeMs);

/// <summary>
/// Проверяет доступность Telegram и YouTube через локальный прокси sing-box.
///
/// Что именно проверяем (и почему):
///   - api.telegram.org       — реальный API-шлюз Telegram (не просто сайт telegram.org).
///                              Возвращает 404 JSON, но это значит: сервер отвечает.
///   - i.ytimg.com thumbnail  — CDN YouTube, через который грузятся превью и видео.
///                              Если этот домен недоступен — видео на YouTube не загрузятся,
///                              даже если www.youtube.com открывается.
///
/// Оба сервиса должны ответить: AND-логика, не OR.
/// Порог задержки 4 сек — выше этого видео-стриминг нестабилен.
/// </summary>
public static class HealthChecker
{
    private const int LatencyMs = 4000;

    // Первое видео на YouTube (jNQXAC9IVRw = "Me at the zoo"), миниатюра 2-3 KB.
    // Тестирует именно видео-CDN, а не просто главную страницу.
    private const string TelegramUrl  = "https://api.telegram.org";
    private const string YoutubeUrl   = "https://i.ytimg.com/vi/jNQXAC9IVRw/default.jpg";

    public static async Task<HealthResult> CheckAsync(int proxyPort = SingBoxService.MixedPort)
    {
        var handler = new HttpClientHandler
        {
            Proxy = new System.Net.WebProxy($"http://127.0.0.1:{proxyPort}"),
            UseProxy = true,
            ServerCertificateCustomValidationCallback = (_, _, _, _) => true
        };

        using var client = new HttpClient(handler) { Timeout = TimeSpan.FromMilliseconds(LatencyMs + 1000) };

        // Проверяем параллельно — общее время = max(Telegram, YouTube), не сумма.
        var tgTask = MeasureAsync(client, TelegramUrl,  LatencyMs);
        var ytTask = MeasureAsync(client, YoutubeUrl,   LatencyMs);

        await Task.WhenAll(tgTask, ytTask);
        var (tgOk, tgMs) = tgTask.Result;
        var (ytOk, ytMs) = ytTask.Result;

        Logger.Instance.Debug("Health",
            $"Telegram {(tgOk ? "OK" : "FAIL")} {tgMs}ms | YouTube CDN {(ytOk ? "OK" : "FAIL")} {ytMs}ms");

        string? failed = !tgOk ? "Telegram API" : !ytOk ? "YouTube CDN" : null;
        return new HealthResult(failed == null, failed, tgMs, ytMs);
    }

    // Обратная совместимость с вызовами в AutoConnect.
    public static async Task<bool> IsWorkingAsync(int proxyPort = SingBoxService.MixedPort, int timeoutMs = 8000)
        => (await CheckAsync(proxyPort)).IsOk;

    private static async Task<(bool ok, int ms)> MeasureAsync(HttpClient client, string url, int timeoutMs)
    {
        using var cts = new CancellationTokenSource(timeoutMs);
        var sw = Stopwatch.StartNew();
        try
        {
            var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cts.Token);
            sw.Stop();
            var ok = (int)response.StatusCode < 500 && sw.ElapsedMilliseconds <= timeoutMs;
            return (ok, (int)sw.ElapsedMilliseconds);
        }
        catch
        {
            sw.Stop();
            return (false, (int)sw.ElapsedMilliseconds);
        }
    }
}
