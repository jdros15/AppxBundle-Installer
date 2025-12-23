namespace AppxBundleInstaller.Models;

/// <summary>
/// Represents package metadata extracted from an Appx/MSIX file or from an installed package
/// </summary>
public class PackageInfo
{
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string PublisherDisplayName { get; set; } = string.Empty;
    public string PublisherId { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Architecture { get; set; } = string.Empty;
    public string PackageFamilyName { get; set; } = string.Empty;
    public string PackageFullName { get; set; } = string.Empty;
    public string InstallLocation { get; set; } = string.Empty;
    public string? LogoPath { get; set; }
    public DateTime? InstallDate { get; set; }
    
    /// <summary>
    /// Whether this is a Microsoft-published package
    /// </summary>
    public bool IsMicrosoft => PublisherDisplayName.Contains("Microsoft", StringComparison.OrdinalIgnoreCase)
                              || PublisherId.StartsWith("8wekyb3d8bbwe", StringComparison.OrdinalIgnoreCase);
    
    /// <summary>
    /// Whether this is a framework package (dependency)
    /// </summary>
    public bool IsFramework { get; set; }
    
    /// <summary>
    /// Whether this is a system-protected package
    /// </summary>
    public bool IsSystemProtected { get; set; }
    
    /// <summary>
    /// Whether this is a critical system app that if uninstalled could break Windows
    /// (e.g., Start Menu, Settings, Shell Experience, etc.)
    /// </summary>
    public bool IsCriticalSystemApp { get; set; }
    
    /// <summary>
    /// Digital signature status
    /// </summary>
    public SignatureStatus SignatureStatus { get; set; } = SignatureStatus.Unknown;
    
    /// <summary>
    /// The scope of the installation (User or Machine)
    /// </summary>
    public PackageScope Scope { get; set; } = PackageScope.User;
    
    /// <summary>
    /// Dependencies required by this package
    /// </summary>
    public List<string> Dependencies { get; set; } = new();
    
    /// <summary>
    /// Capabilities requested by this package
    /// </summary>
    public List<string> Capabilities { get; set; } = new();
    
    /// <summary>
    /// Gets the package type based on publisher and framework status
    /// </summary>
    public PackageType Type => IsFramework ? PackageType.Framework 
                              : IsMicrosoft ? PackageType.Microsoft 
                              : PackageType.ThirdParty;
}

/// <summary>
/// Package type classification for display
/// </summary>
public enum PackageType
{
    Microsoft,
    ThirdParty,
    Framework
}

public enum SignatureStatus
{
    Unknown,
    Valid,
    Invalid,
    Unsigned,
    Untrusted
}

public enum PackageScope
{
    User,
    Machine
}

public enum PackageSortOption
{
    DisplayNameAsc = 0,
    DisplayNameDesc = 1,
    InstallDateNewest = 2,
    InstallDateOldest = 3,
    Publisher = 4,

    Version = 5
}
