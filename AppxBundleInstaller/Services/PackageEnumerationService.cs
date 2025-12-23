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
                Architecture = package.Id.Architecture.ToString(),
                PackageFamilyName = package.Id.FamilyName,
                PackageFullName = package.Id.FullName,
                InstallLocation = TryGetInstallLocation(package),
                IsFramework = package.IsFramework,
                IsSystemProtected = IsSystemProtectedPackage(package),
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
                Version = "Unknown"
            };
        }
    }
    
    private string GetDisplayName(Windows.ApplicationModel.Package package)
    {
        try { return package.DisplayName; }
        catch { return package.Id.Name; }
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
        
        return true;
    }
}

/// <summary>
/// Filter options for package enumeration
/// </summary>
public class PackageFilter
{
    public PublisherType PublisherType { get; set; } = PublisherType.All;
    public bool IncludeFrameworks { get; set; } = false;
}

public enum PublisherType
{
    All,
    Microsoft,
    ThirdParty
}
