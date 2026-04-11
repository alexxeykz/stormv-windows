namespace StormV.Services;

/// <summary>
/// Скачивает подписку по URL и возвращает список серверов.
/// Поддерживает base64-encoded и plain-text форматы.
/// </summary>
public static class SubscriptionService
{
    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(15) };

    public static async Task<(List<ServerConfig> servers, string error)> FetchAsync(string url)
    {
        try
        {
            Logger.Instance.Info("Sub", $"Загрузка подписки: {url}");
            var raw = await _http.GetStringAsync(url);
            raw = raw.Trim();

            // Пробуем декодировать как base64
            var content = TryBase64Decode(raw) ?? raw;

            var servers = content
                .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(l => l.Trim())
                .Where(l => l.Contains("://"))
                .Select(l => UrlParser.Parse(l))
                .Where(s => s != null)
                .Cast<ServerConfig>()
                .ToList();

            if (servers.Count == 0)
                return (servers, "Не найдено ни одного сервера в подписке");

            Logger.Instance.Info("Sub", $"Загружено серверов: {servers.Count}");
            return (servers, string.Empty);
        }
        catch (Exception ex)
        {
            Logger.Instance.Error("Sub", $"Ошибка загрузки подписки: {ex.Message}");
            return (new List<ServerConfig>(), ex.Message);
        }
    }

    private static string? TryBase64Decode(string s)
    {
        try
        {
            var padded = s.PadRight((s.Length + 3) / 4 * 4, '=');
            var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(padded));
            return decoded.Contains("://") ? decoded : null;
        }
        catch { return null; }
    }
}
