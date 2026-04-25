using System.Diagnostics;
using System.IO.Compression;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace StormV.Services;

public record ReleaseInfo(string Version, string DownloadUrl);

public static class UpdateService
{
    private const string SingBoxApiUrl  = "https://api.github.com/repos/SagerNet/sing-box/releases/latest";
    private const string AppApiUrl      = "https://api.github.com/repos/alexxeykz/stormv-windows/releases/latest";

    private static readonly HttpClient _http;

    static UpdateService()
    {
        _http = new HttpClient();
        _http.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue("StormV", GetCurrentAppVersion()));
        _http.Timeout = TimeSpan.FromSeconds(15);
    }

    // ── Версия приложения ────────────────────────────────────────────────────

    public static string GetCurrentAppVersion()
        => Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.0";

    public static async Task<ReleaseInfo?> CheckAppUpdateAsync()
    {
        try
        {
            using var doc = JsonDocument.Parse(await _http.GetStringAsync(AppApiUrl));
            var tag     = doc.RootElement.GetProperty("tag_name").GetString() ?? "";
            var version = tag.TrimStart('v');
            var url     = FindAssetUrl(doc, name => name.Contains("Setup") && name.EndsWith(".exe"));
            if (string.IsNullOrEmpty(url)) return null;
            return IsNewer(version, GetCurrentAppVersion()) ? new ReleaseInfo(version, url) : null;
        }
        catch { return null; }
    }

    // ── Версия sing-box ───────────────────────────────────────────────────────

    public static async Task<string> GetCurrentSingBoxVersionAsync(string singBoxPath)
    {
        if (!File.Exists(singBoxPath)) return "не найден";
        try
        {
            using var proc = Process.Start(new ProcessStartInfo(singBoxPath, "version")
            {
                UseShellExecute        = false,
                RedirectStandardOutput = true,
                CreateNoWindow         = true
            })!;
            var output = await proc.StandardOutput.ReadToEndAsync();
            await proc.WaitForExitAsync();
            var m = Regex.Match(output, @"sing-box version (\S+)");
            return m.Success ? m.Groups[1].Value : "неизвестна";
        }
        catch { return "ошибка"; }
    }

    public static async Task<ReleaseInfo?> CheckSingBoxUpdateAsync(string singBoxPath)
    {
        try
        {
            using var doc = JsonDocument.Parse(await _http.GetStringAsync(SingBoxApiUrl));
            var tag     = doc.RootElement.GetProperty("tag_name").GetString() ?? "";
            var version = tag.TrimStart('v');
            var url     = FindAssetUrl(doc, name => name.Contains("windows-amd64") && name.EndsWith(".zip"));
            if (string.IsNullOrEmpty(url)) return null;

            var current = await GetCurrentSingBoxVersionAsync(singBoxPath);
            return IsNewer(version, current) ? new ReleaseInfo(version, url) : null;
        }
        catch { return null; }
    }

    public static async Task UpdateSingBoxAsync(
        string singBoxPath,
        string downloadUrl,
        IProgress<int>? progress = null)
    {
        var tmp  = Path.Combine(Path.GetTempPath(), "stormv-singbox-update");
        var zip  = tmp + ".zip";
        try
        {
            // 1. Скачиваем
            using var resp = await _http.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);
            resp.EnsureSuccessStatusCode();
            var total = resp.Content.Headers.ContentLength ?? 0;
            await using var src = await resp.Content.ReadAsStreamAsync();
            await using var dst = File.Create(zip);
            var buf = new byte[65536];
            long done = 0; int read;
            while ((read = await src.ReadAsync(buf)) > 0)
            {
                await dst.WriteAsync(buf.AsMemory(0, read));
                done += read;
                if (total > 0) progress?.Report((int)(done * 100 / total));
            }
            progress?.Report(100);

            // 2. Распаковываем
            if (Directory.Exists(tmp)) Directory.Delete(tmp, true);
            ZipFile.ExtractToDirectory(zip, tmp);

            // 3. Заменяем бинарь
            var newExe = Directory.GetFiles(tmp, "sing-box.exe", SearchOption.AllDirectories)
                .FirstOrDefault() ?? throw new FileNotFoundException("sing-box.exe не найден в архиве");

            var backup = singBoxPath + ".bak";
            if (File.Exists(backup)) File.Delete(backup);
            if (File.Exists(singBoxPath)) File.Move(singBoxPath, backup);
            File.Copy(newExe, singBoxPath);
            if (File.Exists(backup)) File.Delete(backup);

            Logger.Instance.Info("Update", $"sing-box обновлён: {singBoxPath}");
        }
        finally
        {
            if (File.Exists(zip)) File.Delete(zip);
            if (Directory.Exists(tmp)) Directory.Delete(tmp, true);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string? FindAssetUrl(JsonDocument doc, Func<string, bool> namePredicate)
    {
        foreach (var asset in doc.RootElement.GetProperty("assets").EnumerateArray())
        {
            var name = asset.GetProperty("name").GetString() ?? "";
            if (namePredicate(name))
                return asset.GetProperty("browser_download_url").GetString();
        }
        return null;
    }

    private static bool IsNewer(string latest, string current)
    {
        // Убираем суффиксы вроде "-beta", берём только X.Y.Z
        static string clean(string v) => Regex.Match(v, @"[\d.]+").Value;
        if (Version.TryParse(clean(latest), out var l) &&
            Version.TryParse(clean(current), out var c))
            return l > c;
        return string.Compare(latest, current, StringComparison.OrdinalIgnoreCase) > 0;
    }
}
