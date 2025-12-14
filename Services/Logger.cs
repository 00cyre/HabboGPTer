using System.Diagnostics;

namespace HabboGPTer.Services;

public enum LogCategory
{
    Debug,
    Info,
    Chat,
    AI,
    Send,
    API,
    Packet,
    Warning,
    Error
}

public class LogEntry
{
    public DateTime Timestamp { get; init; }
    public LogCategory Category { get; init; }
    public string Message { get; init; } = string.Empty;

    public override string ToString()
    {
        var categoryStr = Category switch
        {
            LogCategory.Debug => "DEBUG",
            LogCategory.Info => "INFO",
            LogCategory.Chat => "CHAT",
            LogCategory.AI => "AI",
            LogCategory.Send => "SEND",
            LogCategory.API => "API",
            LogCategory.Packet => "PACKET",
            LogCategory.Warning => "WARN",
            LogCategory.Error => "ERROR",
            _ => "LOG"
        };

        return $"[{Timestamp:HH:mm:ss.fff}] [{categoryStr,-7}] {Message}";
    }
}

public class Logger
{
    private static int _instanceCounter = 0;
    private static readonly string LogsFolder;

    private readonly List<LogEntry> _entries = new();
    private readonly object _lock = new();
    private const int MaxEntries = 2000;
    private readonly string? _logFilePath;
    private readonly StreamWriter? _logWriter;
    private readonly int _instanceId;

    public event Action<LogEntry>? OnLog;

    public event Action? OnClear;

    public int InstanceId => _instanceId;

    public string? LogFilePath => _logFilePath;

    static Logger()
    {
        LogsFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
        try
        {
            if (!Directory.Exists(LogsFolder))
            {
                Directory.CreateDirectory(LogsFolder);
            }
        }
        catch { }
    }

    public Logger(bool enableFileLogging = true)
    {
        _instanceId = Interlocked.Increment(ref _instanceCounter);

        if (enableFileLogging)
        {
            try
            {
                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                _logFilePath = Path.Combine(LogsFolder, $"habbogpter_{timestamp}_inst{_instanceId}.log");
                _logWriter = new StreamWriter(_logFilePath, append: false) { AutoFlush = true };
                _logWriter.WriteLine($"========================================");
                _logWriter.WriteLine($"  HabboGPTer - Instance {_instanceId}");
                _logWriter.WriteLine($"  Started: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                _logWriter.WriteLine($"  Log File: {_logFilePath}");
                _logWriter.WriteLine($"========================================");
                _logWriter.WriteLine();
            }
            catch
            {
                _logWriter = null;
            }
        }
    }

    public IReadOnlyList<LogEntry> Entries
    {
        get
        {
            lock (_lock)
            {
                return _entries.ToList();
            }
        }
    }

    public void Log(LogCategory category, string message)
    {
        var entry = new LogEntry
        {
            Timestamp = DateTime.Now,
            Category = category,
            Message = message
        };

        lock (_lock)
        {
            _entries.Add(entry);

            while (_entries.Count > MaxEntries)
            {
                _entries.RemoveAt(0);
            }

            if (_logWriter != null)
            {
                try
                {
                    _logWriter.WriteLine(entry.ToString());
                }
                catch { }
            }
        }

        OnLog?.Invoke(entry);
    }

    public void Clear()
    {
        lock (_lock)
        {
            _entries.Clear();
        }

        OnClear?.Invoke();
    }

    public void Debug(string message) => Log(LogCategory.Debug, message);
    public void Info(string message) => Log(LogCategory.Info, message);
    public void Chat(string message) => Log(LogCategory.Chat, message);
    public void AI(string message) => Log(LogCategory.AI, message);
    public void Send(string message) => Log(LogCategory.Send, message);
    public void API(string message) => Log(LogCategory.API, message);
    public void Packet(string message) => Log(LogCategory.Packet, message);
    public void Warning(string message) => Log(LogCategory.Warning, message);
    public void Error(string message) => Log(LogCategory.Error, message);
}
