namespace StormV.Services;

public static class SettingsService
{
    private static readonly string _file =
        Path.Combine(ConfigService.ConfigDir, "settings.json");

    private static readonly JsonSerializerOptions _opts = new()
    {
        WriteIndented = true
    };

    public static AppSettings Load()
    {
        try
        {
            if (!File.Exists(_file)) return new();
            var json = File.ReadAllText(_file);
            return JsonSerializer.Deserialize<AppSettings>(json, _opts) ?? new();
        }
        catch { return new(); }
    }

    public static void Save(AppSettings settings)
    {
        Directory.CreateDirectory(ConfigService.ConfigDir);
        File.WriteAllText(_file, JsonSerializer.Serialize(settings, _opts));
    }
}
