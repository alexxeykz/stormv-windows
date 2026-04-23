namespace StormV.Services;

public class ConfigService
{
    private static readonly string _configDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "StormV");

    // Зашифрованный файл серверов
    private static readonly string _serversFile = Path.Combine(_configDir, "servers.dat");
    // Старый незашифрованный — для миграции
    private static readonly string _legacyFile = Path.Combine(_configDir, "servers.json");

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = false,
        Converters = { new JsonStringEnumConverter() }
    };

    public static List<ServerConfig> LoadServers()
    {
        try
        {
            Directory.CreateDirectory(_configDir);

            // Миграция старого незашифрованного файла
            if (File.Exists(_legacyFile) && !File.Exists(_serversFile))
            {
                Logger.Instance.Info("Config", "Мигрируем незашифрованные конфиги → зашифрованные...");
                var legacy = File.ReadAllText(_legacyFile);
                var list = JsonSerializer.Deserialize<List<ServerConfig>>(legacy, _jsonOptions);
                if (list != null) SaveServers(list);
                File.Delete(_legacyFile);
                Logger.Instance.Info("Config", "Миграция завершена, старый файл удалён");
            }

            if (!File.Exists(_serversFile)) return new();

            var cipher = File.ReadAllText(_serversFile);
            var json = EncryptionService.Decrypt(cipher);
            return JsonSerializer.Deserialize<List<ServerConfig>>(json, _jsonOptions) ?? new();
        }
        catch (Exception ex)
        {
            Logger.Instance.Error("Config", $"Ошибка загрузки конфигов: {ex.Message}");
            return new();
        }
    }

    public static void SaveServers(IEnumerable<ServerConfig> servers)
    {
        try
        {
            Directory.CreateDirectory(_configDir);
            var json = JsonSerializer.Serialize(servers.ToList(), _jsonOptions);
            var cipher = EncryptionService.Encrypt(json);
            File.WriteAllText(_serversFile, cipher);
            Logger.Instance.Debug("Config", $"Сохранено {servers.Count()} серверов (зашифровано)");
        }
        catch (Exception ex)
        {
            Logger.Instance.Error("Config", $"Ошибка сохранения конфигов: {ex.Message}");
        }
    }

    private static readonly string _settingsFile = Path.Combine(_configDir, "settings.json");

    public static AppSettings LoadSettings()
    {
        try
        {
            if (!File.Exists(_settingsFile)) return new();
            var json = File.ReadAllText(_settingsFile);
            return JsonSerializer.Deserialize<AppSettings>(json, _jsonOptions) ?? new();
        }
        catch { return new(); }
    }

    public static void SaveSettings(AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(_configDir);
            File.WriteAllText(_settingsFile, JsonSerializer.Serialize(settings, _jsonOptions));
        }
        catch (Exception ex)
        {
            Logger.Instance.Error("Config", $"Ошибка сохранения настроек: {ex.Message}");
        }
    }

    public static string ConfigDir => _configDir;
}
