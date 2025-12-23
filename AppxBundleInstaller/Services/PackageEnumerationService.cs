using AppxBundleInstaller.Models;
using Windows.Management.Deployment;

namespace AppxBundleInstaller.Services;

/// <summary>
/// Enumerates installed Appx/MSIX packages on the system
/// </summary>
public class PackageEnumerationService
{
    private readonly PackageManager _packageManager;
    
    public PackageEnumerationService()
    {
        _packageManager = new PackageManager();
    }
    
    /// <summary>
    /// Gets all packages installed for the current user
    /// </summary>
    public async Task<IEnumerable<PackageInfo>> GetInstalledPackagesAsync(PackageFilter? filter = null)
    {
        return await Task.Run(() =>
        {
            var packages = _packageManager.FindPackagesForUser(string.Empty);
            
            return packages
                .Select(ConvertToPackageInfo)
                .Where(p => MatchesFilter(p, filter ?? new PackageFilter()))
                .OrderBy(p => p.DisplayName)
                .ToList();
        });
    }
    
    /// <summary>
    /// Searches packages by name, family name, or publisher
    /// </summary>
    public async Task<IEnumerable<PackageInfo>> SearchPackagesAsync(string searchTerm, PackageFilter? filter = null)
    {
        var packages = await GetInstalledPackagesAsync(filter);
        
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return packages;
        }
        
        searchTerm = searchTerm.ToLowerInvariant();
        
        return packages.Where(p =>
            p.DisplayName.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
            p.Name.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
            p.PackageFamilyName.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
            p.PublisherDisplayName.Contains(searchTerm, StringComparison.OrdinalIgnoreCase));
    }
    
    /// <summary>
    /// Gets a specific package by family name
    /// </summary>
    public PackageInfo? GetPackageByFamilyName(string familyName)
    {
        var packages = _packageManager.FindPackagesForUser(string.Empty, familyName);
        var package = packages.FirstOrDefault();
        
        return package != null ? ConvertToPackageInfo(package) : null;
    }
    
    /// <summary>
    /// Checks if a package is installed
    /// </summary>
    public bool IsPackageInstalled(string packageFamilyName)
    {
        var packages = _packageManager.FindPackagesForUser(string.Empty, packageFamilyName);
        return packages.Any();
    }
    
    private PackageInfo ConvertToPackageInfo(Windows.ApplicationModel.Package package)
    {
        try
        {
            return new PackageInfo
            {
                Name = package.Id.Name,
                DisplayName = GetDisplayName(package),
                PublisherDisplayName = GetPublisherDisplayName(package),
                PublisherId = package.Id.PublisherId,
                Version = $"{package.Id.Version.Major}.{package.Id.Version.Minor}.{package.Id.Version.Build}.{package.Id.Version.Revision}",
                Architecture = GetFriendlyArchitecture(package.Id.Architecture),
                PackageFamilyName = package.Id.FamilyName,
                PackageFullName = package.Id.FullName,
                InstallLocation = TryGetInstallLocation(package),
                InstallDate = TryGetInstallDate(package),
                IsFramework = package.IsFramework,
                IsSystemProtected = IsSystemProtectedPackage(package),
                IsCriticalSystemApp = IsCriticalSystemApp(package),
                SignatureStatus = ConvertSignatureKind(package.SignatureKind),
                Scope = package.InstalledLocation != null ? PackageScope.User : PackageScope.Machine,
                LogoPath = TryGetLogoPath(package)
            };
        }
        catch
        {
            // Some packages may throw when accessing properties
            return new PackageInfo
            {
                Name = package.Id.Name,
                DisplayName = package.Id.Name,
                PackageFamilyName = package.Id.FamilyName,
                PackageFullName = package.Id.FullName,
                Version = "Unknown",
                Architecture = "Unknown"
            };
        }
    }
    
    private string GetDisplayName(Windows.ApplicationModel.Package package)
    {
        try 
        { 
            var displayName = package.DisplayName;
            // If DisplayName is empty or whitespace, fall back to the package Name
            return string.IsNullOrWhiteSpace(displayName) ? package.Id.Name : displayName; 
        }
        catch { return package.Id.Name; }
    }
    
    private string GetFriendlyArchitecture(Windows.System.ProcessorArchitecture arch)
    {
        return arch switch
        {
            Windows.System.ProcessorArchitecture.X86 => "X86",
            Windows.System.ProcessorArchitecture.X64 => "X64",
            Windows.System.ProcessorArchitecture.Arm => "ARM",
            Windows.System.ProcessorArchitecture.Arm64 => "ARM64",
            Windows.System.ProcessorArchitecture.Neutral => "Any",  // More user-friendly than "Neutral"
            Windows.System.ProcessorArchitecture.Unknown => "Unknown",
            _ => arch.ToString()
        };
    }
    
    private DateTime? TryGetInstallDate(Windows.ApplicationModel.Package package)
    {
        try 
        { 
            return package.InstalledDate.DateTime;
        }
        catch { return null; }
    }
    
    private string GetPublisherDisplayName(Windows.ApplicationModel.Package package)
    {
        try { return package.PublisherDisplayName; }
        catch { return "Unknown Publisher"; }
    }
    
    private string TryGetInstallLocation(Windows.ApplicationModel.Package package)
    {
        try { return package.InstalledLocation?.Path ?? "N/A"; }
        catch { return "N/A"; }
    }
    
    private string? TryGetLogoPath(Windows.ApplicationModel.Package package)
    {
        try { return package.Logo?.LocalPath; }
        catch { return null; }
    }
    
    private bool IsSystemProtectedPackage(Windows.ApplicationModel.Package package)
    {
        // System-protected packages typically include:
        // - Packages signed by Microsoft with certain capabilities
        // - Packages that are part of Windows
        try
        {
            var signatureKind = package.SignatureKind;
            return signatureKind == Windows.ApplicationModel.PackageSignatureKind.System;
        }
        catch
        {
            return false;
        }
    }
    
    private SignatureStatus ConvertSignatureKind(Windows.ApplicationModel.PackageSignatureKind signatureKind)
    {
        return signatureKind switch
        {
            Windows.ApplicationModel.PackageSignatureKind.None => SignatureStatus.Unsigned,
            Windows.ApplicationModel.PackageSignatureKind.Developer => SignatureStatus.Valid,
            Windows.ApplicationModel.PackageSignatureKind.Enterprise => SignatureStatus.Valid,
            Windows.ApplicationModel.PackageSignatureKind.Store => SignatureStatus.Valid,
            Windows.ApplicationModel.PackageSignatureKind.System => SignatureStatus.Valid,
            _ => SignatureStatus.Unknown
        };
    }
    
    private bool MatchesFilter(PackageInfo package, PackageFilter filter)
    {
        // Publisher filter
        if (filter.PublisherType == PublisherType.Microsoft && !package.IsMicrosoft)
            return false;
        if (filter.PublisherType == PublisherType.ThirdParty && package.IsMicrosoft)
            return false;
        
        // Framework filter
        if (!filter.IncludeFrameworks && package.IsFramework)
            return false;
        
        // Critical apps filter - hide by default for safety
        if (!filter.IncludeCriticalApps && package.IsCriticalSystemApp)
            return false;
        
        return true;
    }
    
    /// <summary>
    /// List of package name patterns for critical system apps that could break Windows if uninstalled
    /// </summary>
    private static readonly string[] CriticalSystemAppPatterns = new[]
    {
        "Microsoft.Windows.StartMenuExperienceHost",   // Start Menu
        "Microsoft.Windows.ShellExperienceHost",       // Shell/Taskbar
        "windows.immersivecontrolpanel",               // Settings app
        "Microsoft.Windows.Search",                    // Windows Search
        "Microsoft.WindowsStore",                      // Microsoft Store
        "Microsoft.Windows.Cortana",                   // Cortana
        "Microsoft.AAD.BrokerPlugin",                  // Azure AD authentication
        "Microsoft.Windows.CloudExperienceHost",      // OOBE/First-run experience
        "Microsoft.Windows.ContentDeliveryManager",   // Content delivery
        "Microsoft.Windows.SecHealthUI",              // Windows Security
        "Microsoft.SecHealthUI",                       // Windows Security (alternate)
        "Microsoft.Windows.XGpuEjectDialog",          // GPU eject
        "Microsoft.Windows.PeopleExperienceHost",     // People bar
        "Microsoft.Windows.ParentalControls",         // Parental controls
        "Microsoft.LockApp",                           // Lock screen
        "Microsoft.Windows.AssignedAccessLockApp",    // Kiosk mode
        "Microsoft.Windows.NarratorQuickStart",       // Accessibility
        "Microsoft.Windows.OOBENetworkCaptivePortal", // Network captive portal
        "Microsoft.Windows.OOBENetworkConnectionFlow", // Network setup
        "Microsoft.Windows.PinningConfirmationDialog", // Pin confirmation
        "Microsoft.Windows.SecureAssessmentBrowser",  // Secure browser for tests
        "Microsoft.XboxGameCallableUI",               // Xbox integration
        "Microsoft.XboxIdentityProvider",             // Xbox identity
        "Microsoft.AccountsControl",                   // Account management
        "Microsoft.CredDialogHost",                    // Credential dialog
        "Microsoft.ECApp",                             // Eye control
        "Microsoft.BioEnrollment",                     // Biometric enrollment
        "Windows.PrintDialog",                         // Print dialog
        "Windows.CBSPreview",                          // CBS Preview
        "NcsiUwpApp",                                  // Network connectivity status
        "1527c705-839a-4832-9118-54d4Bd6a0c89",       // File Picker
        "c5e2524a-ea46-4f67-841f-6a9465d9d515",       // File Explorer AddSuggestedFoldersToLibrary
        "E2A4F912-2574-4A75-9BB0-0D023378592B",       // App Resolver UX
        "F46D4000-FD22-4DB4-AC8E-4E1DDDE828FE"        // Add Suggested Folders To Library
    };
    
    /// <summary>
    /// Determines if a package is a critical system app
    /// </summary>
    private bool IsCriticalSystemApp(Windows.ApplicationModel.Package package)
    {
        try
        {
            var packageName = package.Id.Name;
            var familyName = package.Id.FamilyName;
            
            // Check if the package matches any critical app pattern
            foreach (var pattern in CriticalSystemAppPatterns)
            {
                if (packageName.Contains(pattern, StringComparison.OrdinalIgnoreCase) ||
                    familyName.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            
            return false;
        }
        catch
        {
            return false;
        }
    }
}

/// <summary>
/// Filter options for package enumeration
/// </summary>
public class PackageFilter
{
    public PublisherType PublisherType { get; set; } = PublisherType.All;
    public bool IncludeFrameworks { get; set; } = false;
    /// <summary>
    /// Whether to include critical system apps (Start, Settings, Shell, etc.)
    /// Default is false for safety - these apps can break Windows if uninstalled
    /// </summary>
    public bool IncludeCriticalApps { get; set; } = false;
}

public enum PublisherType
{
    All,
    Microsoft,
    ThirdParty
}
