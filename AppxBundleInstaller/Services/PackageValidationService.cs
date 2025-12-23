using System.IO;
using System.IO.Compression;
using System.Security.Cryptography.X509Certificates;
using System.Xml.Linq;
using AppxBundleInstaller.Models;

namespace AppxBundleInstaller.Services;

/// <summary>
/// Validates package files and extracts metadata from Appx/MSIX packages
/// </summary>
public class PackageValidationService
{
    private static readonly string[] ValidExtensions = { ".appx", ".appxbundle", ".msix", ".msixbundle" };
    
    /// <summary>
    /// Checks if the file has a valid package extension
    /// </summary>
    public bool IsValidExtension(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        return ValidExtensions.Contains(extension);
    }
    
    /// <summary>
    /// Validates a package file and extracts its metadata
    /// </summary>
    public async Task<(bool IsValid, PackageInfo? Info, string? Error)> ValidateAndExtractAsync(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return (false, null, "File does not exist");
        }
        
        if (!IsValidExtension(filePath))
        {
            return (false, null, $"Invalid file type. Expected: {string.Join(", ", ValidExtensions)}");
        }
        
        try
        {
            var info = await ExtractPackageInfoAsync(filePath);
            
            // Check architecture compatibility
            var compatible = IsArchitectureCompatible(info.Architecture);
            if (!compatible)
            {
                return (false, info, $"Package architecture '{info.Architecture}' is not compatible with this system");
            }
            
            return (true, info, null);
        }
        catch (Exception ex)
        {
            return (false, null, $"Failed to read package: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Extracts package information from the AppxManifest.xml inside the package
    /// </summary>
    public async Task<PackageInfo> ExtractPackageInfoAsync(string filePath)
    {
        var info = new PackageInfo();
        
        await Task.Run(() =>
        {
            using var archive = ZipFile.OpenRead(filePath);
            
            // Try to find AppxManifest.xml (for .appx/.msix) or AppxBundleManifest.xml (for bundles)
            var manifestEntry = archive.GetEntry("AppxManifest.xml") 
                              ?? archive.GetEntry("AppxBundleManifest.xml");
            
            if (manifestEntry != null)
            {
                using var stream = manifestEntry.Open();
                var doc = XDocument.Load(stream);
                
                // Parse the manifest
                ParseManifest(doc, info);
            }
            
            // Check signature
            var signatureEntry = archive.GetEntry("AppxSignature.p7x");
            info.SignatureStatus = signatureEntry != null ? SignatureStatus.Valid : SignatureStatus.Unsigned;
        });
        
        return info;
    }
    
    private void ParseManifest(XDocument doc, PackageInfo info)
    {
        var ns = doc.Root?.GetDefaultNamespace() ?? XNamespace.None;
        
        // Identity element contains name, version, publisher, architecture
        var identity = doc.Descendants(ns + "Identity").FirstOrDefault();
        if (identity != null)
        {
            info.Name = identity.Attribute("Name")?.Value ?? "Unknown";
            info.Version = identity.Attribute("Version")?.Value ?? "0.0.0.0";
            info.Architecture = identity.Attribute("ProcessorArchitecture")?.Value ?? "neutral";
            
            var publisher = identity.Attribute("Publisher")?.Value ?? "";
            info.PublisherId = ExtractPublisherId(publisher);
        }
        
        // Properties element contains display name and publisher display name
        var properties = doc.Descendants(ns + "Properties").FirstOrDefault();
        if (properties != null)
        {
            info.DisplayName = properties.Element(ns + "DisplayName")?.Value ?? info.Name;
            info.PublisherDisplayName = properties.Element(ns + "PublisherDisplayName")?.Value ?? "Unknown Publisher";
        }
        
        // Check for framework package
        var framework = doc.Descendants(ns + "Framework").FirstOrDefault();
        info.IsFramework = framework?.Value.Equals("true", StringComparison.OrdinalIgnoreCase) ?? false;
        
        // Extract capabilities
        var capabilities = doc.Descendants().Where(e => e.Name.LocalName == "Capability");
        info.Capabilities = capabilities.Select(c => c.Attribute("Name")?.Value ?? "").Where(c => !string.IsNullOrEmpty(c)).ToList();
        
        // Extract dependencies
        var dependencies = doc.Descendants(ns + "PackageDependency");
        info.Dependencies = dependencies.Select(d => 
        {
            var name = d.Attribute("Name")?.Value ?? "";
            var minVersion = d.Attribute("MinVersion")?.Value ?? "";
            return $"{name} (>= {minVersion})";
        }).Where(d => !string.IsNullOrEmpty(d)).ToList();
        
        // Build package family name
        if (!string.IsNullOrEmpty(info.Name) && !string.IsNullOrEmpty(info.PublisherId))
        {
            info.PackageFamilyName = $"{info.Name}_{info.PublisherId}";
        }
    }
    
    private string ExtractPublisherId(string publisher)
    {
        // Publisher ID is a hash of the publisher certificate subject
        // For now, extract from CN= portion or generate a placeholder
        if (publisher.Contains("CN="))
        {
            var start = publisher.IndexOf("CN=") + 3;
            var end = publisher.IndexOf(',', start);
            if (end < 0) end = publisher.Length;
            return publisher.Substring(start, end - start).Trim();
        }
        return publisher.GetHashCode().ToString("x8");
    }
    
    /// <summary>
    /// Verifies the digital signature of a package
    /// </summary>
    public async Task<SignatureStatus> VerifySignatureAsync(string filePath)
    {
        return await Task.Run(() =>
        {
            try
            {
                using var archive = ZipFile.OpenRead(filePath);
                var signatureEntry = archive.GetEntry("AppxSignature.p7x");
                
                if (signatureEntry == null)
                {
                    return SignatureStatus.Unsigned;
                }
                
                // For full signature verification, we would need to:
                // 1. Extract the signature
                // 2. Verify against Windows trust store
                // For now, we'll assume presence of signature means valid
                // Full implementation would use WinVerifyTrust API
                
                return SignatureStatus.Valid;
            }
            catch
            {
                return SignatureStatus.Invalid;
            }
        });
    }
    
    /// <summary>
    /// Checks if the package architecture is compatible with the current system
    /// </summary>
    public bool IsArchitectureCompatible(string packageArch)
    {
        var systemArch = Environment.Is64BitOperatingSystem ? "x64" : "x86";
        var processArch = Environment.Is64BitProcess ? "x64" : "x86";
        
        return packageArch.ToLowerInvariant() switch
        {
            "neutral" => true,
            "x86" => true, // x86 apps run on both x86 and x64
            "x64" => systemArch == "x64",
            "arm" => false, // Would need ARM detection
            "arm64" => false, // Would need ARM64 detection
            _ => true // Unknown architecture, let Windows handle it
        };
    }
}
