namespace AppxBundleInstaller.Services;

/// <summary>
/// Decodes Windows package deployment error codes into human-readable messages
/// </summary>
public class ErrorDecoderService
{
    private static readonly Dictionary<int, (string Title, string Description)> ErrorCodes = new()
    {
        // Package installation errors
        { unchecked((int)0x80073CF0), ("Package Already Installed", "A newer version of this package is already installed. Uninstall the existing version first or use update mode.") },
        { unchecked((int)0x80073CF1), ("Package Downgrade Blocked", "You cannot install an older version of a package when a newer version is already installed.") },
        { unchecked((int)0x80073CF2), ("Package Dependencies Missing", "This package requires dependencies that are not installed. Check the package requirements.") },
        { unchecked((int)0x80073CF3), ("Package Dependencies Not Satisfied", "One or more dependencies are missing or incompatible.") },
        { unchecked((int)0x80073CF6), ("Package Not Found", "The specified package was not found on the system.") },
        { unchecked((int)0x80073CF9), ("Package Update Failed", "The package update failed. The previous version may still be installed.") },
        { unchecked((int)0x80073CFA), ("Package In Use", "The package is currently in use. Close the application and try again.") },
        { unchecked((int)0x80073CFB), ("Package Requires Reboot", "A system restart is required to complete the operation.") },
        { unchecked((int)0x80073CFC), ("Bundle Installation Failed", "The bundle package could not be installed. One or more packages in the bundle failed.") },
        { unchecked((int)0x80073CFD), ("Package Registration Failed", "The package could not be registered with Windows.") },
        { unchecked((int)0x80073CFE), ("Package Already Exists", "A package with the same identity is already staged.") },
        { unchecked((int)0x80073CFF), ("Package Blocked by Policy", "This package is blocked by your organization's policies or Windows Defender Application Control.") },
        
        // Signature errors
        { unchecked((int)0x800B0100), ("Unsigned Package", "The package is not signed. Enable Developer Mode or sideloading to install unsigned packages.") },
        { unchecked((int)0x800B0101), ("Invalid Signature", "The package signature is invalid or has been tampered with.") },
        { unchecked((int)0x800B0109), ("Untrusted Certificate", "The package is signed with an untrusted certificate. Install the certificate first.") },
        { unchecked((int)0x800B010A), ("Certificate Chain Error", "The certificate chain could not be verified. Check certificate trust settings.") },
        
        // Access errors
        { unchecked((int)0x80070005), ("Access Denied", "Administrator privileges are required for this operation.") },
        { unchecked((int)0x80070057), ("Invalid Parameter", "The package contains invalid data or parameters.") },
        { unchecked((int)0x80070002), ("File Not Found", "The package file was not found or is inaccessible.") },
        { unchecked((int)0x80070003), ("Path Not Found", "The installation path is not accessible.") },
        { unchecked((int)0x80070020), ("File Locked", "The package file is locked by another process.") },
        { unchecked((int)0x80070070), ("Disk Full", "Not enough disk space to install the package.") },
        
        // Bundle-specific errors
        { unchecked((int)0x80073D00), ("Invalid Bundle", "The bundle package is invalid or corrupted.") },
        { unchecked((int)0x80073D01), ("Bundle Conflict", "A package in the bundle conflicts with an installed package.") },
        { unchecked((int)0x80073D02), ("Bundle Contains Invalid Package", "One or more packages in the bundle are invalid.") },
        
        // Store/Provisioning errors
        { unchecked((int)0x80073D05), ("Package Not Provisioned", "The package is not provisioned for this user.") },
        { unchecked((int)0x80073D06), ("Package Provisioning Error", "Failed to provision the package.") },
        
        // Framework errors
        { unchecked((int)0x80073D0A), ("Framework Missing", "A required framework package is not installed.") },
        { unchecked((int)0x80073D0B), ("Framework Version Mismatch", "The installed framework version does not match the requirement.") }
    };
    
    /// <summary>
    /// Decodes an HRESULT error code into a human-readable message
    /// </summary>
    public string DecodeError(int hResult)
    {
        if (ErrorCodes.TryGetValue(hResult, out var error))
        {
            return $"{error.Title}: {error.Description}";
        }
        
        return $"An error occurred (0x{hResult:X8}). Please check the diagnostics panel for details.";
    }
    
    /// <summary>
    /// Decodes an exception into a human-readable message
    /// </summary>
    public string DecodeException(Exception ex)
    {
        // Try to decode the HResult first
        var decoded = DecodeError(ex.HResult);
        if (!decoded.StartsWith("An error occurred"))
        {
            return decoded;
        }
        
        // Handle specific exception types
        return ex switch
        {
            UnauthorizedAccessException => "Access denied. You may need administrator privileges.",
            System.IO.FileNotFoundException => "The package file was not found.",
            System.IO.IOException io => $"File access error: {io.Message}",
            _ => ex.Message
        };
    }
    
    /// <summary>
    /// Gets the error title for display in UI
    /// </summary>
    public string GetErrorTitle(int hResult)
    {
        if (ErrorCodes.TryGetValue(hResult, out var error))
        {
            return error.Title;
        }
        return "Unknown Error";
    }
    
    /// <summary>
    /// Gets suggestions for resolving common errors
    /// </summary>
    public List<string> GetSuggestions(int hResult)
    {
        var suggestions = new List<string>();
        
        switch (hResult)
        {
            case unchecked((int)0x80073CF3):
            case unchecked((int)0x80073D0A):
                suggestions.Add("Download and install the required dependency packages first");
                suggestions.Add("Check if the Visual C++ Runtime or .NET Native Runtime is needed");
                break;
                
            case unchecked((int)0x800B0100):
            case unchecked((int)0x800B0101):
                suggestions.Add("Enable Developer Mode in Windows Settings > Update & Security > For developers");
                suggestions.Add("Or enable Sideloading in the same settings page");
                break;
                
            case unchecked((int)0x80070005):
                suggestions.Add("Run the application as Administrator");
                suggestions.Add("Or use the elevation option in the install dialog");
                break;
                
            case unchecked((int)0x80073CFA):
                suggestions.Add("Close the application that is using this package");
                suggestions.Add("Check Task Manager for running processes");
                break;
                
            case unchecked((int)0x80073CFF):
                suggestions.Add("Contact your system administrator to allow this package");
                suggestions.Add("Check Windows Defender Application Control policies");
                break;
        }
        
        return suggestions;
    }
}
