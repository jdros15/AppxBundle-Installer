namespace AppxBundleInstaller.Models;

/// <summary>
/// A log entry for the diagnostics panel
/// </summary>
public class LogEntry
{
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public LogLevel Level { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? Details { get; set; }
    public string? ErrorCode { get; set; }
    
    public string TimestampFormatted => Timestamp.ToString("HH:mm:ss");
    public string LevelIcon => Level switch
    {
        LogLevel.Info => "ℹ️",
        LogLevel.Warning => "⚠️",
        LogLevel.Error => "❌",
        LogLevel.Success => "✅",
        _ => "📝"
    };
}

public enum LogLevel
{
    Info,
    Warning,
    Error,
    Success
}
