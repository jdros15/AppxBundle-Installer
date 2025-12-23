namespace AppxBundleInstaller.Models;

/// <summary>
/// Result of a package operation (install/uninstall)
/// </summary>
public class OperationResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? ErrorCode { get; set; }
    public string? TechnicalDetails { get; set; }
    public OperationType OperationType { get; set; }
    public PackageInfo? Package { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.Now;
    
    public static OperationResult Succeeded(OperationType type, PackageInfo package, string message)
    {
        return new OperationResult
        {
            Success = true,
            OperationType = type,
            Package = package,
            Message = message
        };
    }
    
    public static OperationResult Failed(OperationType type, PackageInfo? package, string message, string? errorCode = null, string? technicalDetails = null)
    {
        return new OperationResult
        {
            Success = false,
            OperationType = type,
            Package = package,
            Message = message,
            ErrorCode = errorCode,
            TechnicalDetails = technicalDetails
        };
    }
}

public enum OperationType
{
    Install,
    Uninstall,
    Validation
}
