using System.Collections.ObjectModel;
using AppxBundleInstaller.Models;

namespace AppxBundleInstaller.Services;

/// <summary>
/// Centralized logging service for the diagnostics panel
/// </summary>
public class DiagnosticsService
{
    private static DiagnosticsService? _instance;
    public static DiagnosticsService Instance => _instance ??= new DiagnosticsService();

    private readonly ObservableCollection<LogEntry> _logs = new();
    private const int MaxLogEntries = 1000;
    
    public ObservableCollection<LogEntry> Logs => _logs;
    
    public event EventHandler<LogEntry>? LogAdded;
    
    public void Log(LogLevel level, string message, string? details = null, string? errorCode = null)
    {
        var entry = new LogEntry
        {
            Level = level,
            Message = message,
            Details = details,
            ErrorCode = errorCode
        };
        
        // Ensure we're on the UI thread for the ObservableCollection
        if (System.Windows.Application.Current?.Dispatcher.CheckAccess() == false)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() => AddLog(entry));
        }
        else
        {
            AddLog(entry);
        }
    }
    
    private void AddLog(LogEntry entry)
    {
        _logs.Add(entry);
        
        // Trim old entries
        while (_logs.Count > MaxLogEntries)
        {
            _logs.RemoveAt(0);
        }
        
        LogAdded?.Invoke(this, entry);
    }
    
    public void Clear()
    {
        _logs.Clear();
    }
    
    public string ExportLogs()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("AppxBundle Installer - Diagnostic Log");
        sb.AppendLine($"Exported: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine(new string('-', 60));
        sb.AppendLine();
        
        foreach (var log in _logs)
        {
            sb.AppendLine($"[{log.Timestamp:HH:mm:ss}] [{log.Level}] {log.Message}");
            if (!string.IsNullOrEmpty(log.ErrorCode))
            {
                sb.AppendLine($"  Error Code: {log.ErrorCode}");
            }
            if (!string.IsNullOrEmpty(log.Details))
            {
                sb.AppendLine($"  Details: {log.Details}");
            }
            sb.AppendLine();
        }
        
        return sb.ToString();
    }
}
