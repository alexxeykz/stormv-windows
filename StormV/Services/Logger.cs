namespace StormV.Services;

public enum LogLevel { Debug, Info, Warning, Error }

public record LogEntry(DateTime Time, LogLevel Level, string Tag, string Message)
{
    public string Formatted =>
        $"[{Time:HH:mm:ss.fff}] [{Level.ToString().ToUpper(),-7}] [{Tag}] {Message}";

    public string LevelLabel => Level switch
    {
        LogLevel.Debug   => "DBG",
        LogLevel.Info    => "INF",
        LogLevel.Warning => "WRN",
        LogLevel.Error   => "ERR",
        _ => "???"
    };
}

/// <summary>
/// Singleton-логгер: пишет в память + файл.
/// Потокобезопасный, файл ротируется при > 5 МБ.
/// </summary>
public sealed class Logger
{
    public static readonly Logger Instance = new();

    private readonly List<LogEntry> _entries = new();
    private readonly object _lock = new();
    private readonly string _logFile;
    private const int MaxMemoryEntries = 2000;
    private const long MaxFileSize = 5 * 1024 * 1024; // 5 MB

    public event Action<LogEntry>? EntryAdded;

    private Logger()
    {
        var dir = ConfigService.ConfigDir;
        Directory.CreateDirectory(dir);
        _logFile = Path.Combine(dir, "stormv.log");
        RotateIfNeeded();

        Write(LogLevel.Info, "App", "StormV started");
    }

    public void Debug(string tag, string msg)   => Write(LogLevel.Debug, tag, msg);
    public void Info(string tag, string msg)    => Write(LogLevel.Info, tag, msg);
    public void Warning(string tag, string msg) => Write(LogLevel.Warning, tag, msg);
    public void Error(string tag, string msg)   => Write(LogLevel.Error, tag, msg);

    public void Write(LogLevel level, string tag, string message)
    {
        // В памяти и UI — маскируем IP/UUID
        var maskedMessage = EncryptionService.MaskSensitive(message);
        var entry = new LogEntry(DateTime.Now, level, tag, maskedMessage);

        lock (_lock)
        {
            _entries.Add(entry);
            if (_entries.Count > MaxMemoryEntries)
                _entries.RemoveRange(0, 200);

            // В файл — тоже маскированная версия (не храним IP в открытом виде)
            try { File.AppendAllText(_logFile, entry.Formatted + Environment.NewLine); }
            catch { /* не роняем приложение из-за лога */ }
        }
        EntryAdded?.Invoke(entry);
    }

    public IReadOnlyList<LogEntry> GetAll()
    {
        lock (_lock) return _entries.ToList();
    }

    public IReadOnlyList<LogEntry> GetFiltered(LogLevel minLevel, string? search = null)
    {
        lock (_lock)
        {
            var q = _entries.Where(e => e.Level >= minLevel);
            if (!string.IsNullOrWhiteSpace(search))
                q = q.Where(e => e.Message.Contains(search, StringComparison.OrdinalIgnoreCase)
                               || e.Tag.Contains(search, StringComparison.OrdinalIgnoreCase));
            return q.ToList();
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _entries.Clear();
            try { File.WriteAllText(_logFile, string.Empty); } catch { }
        }
    }

    public string LogFilePath => _logFile;

    private void RotateIfNeeded()
    {
        try
        {
            if (File.Exists(_logFile) && new FileInfo(_logFile).Length > MaxFileSize)
            {
                var backup = _logFile + ".old";
                if (File.Exists(backup)) File.Delete(backup);
                File.Move(_logFile, backup);
            }
        }
        catch { }
    }
}
