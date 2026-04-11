namespace StormV.Services;

/// <summary>
/// TCP-пинг к хосту:порту сервера.
/// Возвращает задержку в мс или null если недоступен.
/// </summary>
public static class PingService
{
    public static async Task<int?> PingAsync(string host, int port, int timeoutMs = 3000)
    {
        try
        {
            using var client = new System.Net.Sockets.TcpClient();
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var connectTask = client.ConnectAsync(host, port);
            var delayTask = Task.Delay(timeoutMs);
            var winner = await Task.WhenAny(connectTask, delayTask);
            sw.Stop();
            if (winner == delayTask || !client.Connected) return null;
            return (int)sw.ElapsedMilliseconds;
        }
        catch
        {
            return null;
        }
    }
}
