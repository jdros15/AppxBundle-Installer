using AppxBundleInstaller.Models;
using Windows.Management.Deployment;

namespace AppxBundleInstaller.Services;

/// <summary>
/// Handles package installation and uninstallation using Windows Deployment API
/// </summary>
public class PackageManagerService
{
    private readonly PackageManager _packageManager;
    private readonly ErrorDecoderService _errorDecoder;
    private readonly DiagnosticsService _diagnostics;
    
    public PackageManagerService(DiagnosticsService diagnostics)
    {
        _packageManager = new PackageManager();
        _errorDecoder = new ErrorDecoderService();
        _diagnostics = diagnostics;
    }
    
    /// <summary>
    /// Installs a package from file
    /// </summary>
    public async Task<OperationResult> InstallPackageAsync(
        string filePath, 
        PackageInfo packageInfo,
        IProgress<double>? progress = null,
        bool forceAppShutdown = true)
    {
        _diagnostics.Log(LogLevel.Info, $"Starting installation: {packageInfo.DisplayName}");
        
        try
        {
            var packageUri = new Uri(filePath);
            var options = DeploymentOptions.None;
            
            if (forceAppShutdown)
            {
                options |= DeploymentOptions.ForceApplicationShutdown;
            }
            
            // Allow unsigned packages in developer mode (sideloading)
            if (packageInfo.SignatureStatus == SignatureStatus.Unsigned)
            {
                _diagnostics.Log(LogLevel.Warning, "Package is unsigned. Sideloading mode required.");
            }
            
            var deploymentOperation = _packageManager.AddPackageAsync(
                packageUri,
                null, // No dependency packages provided directly
                options);
            
            // Report progress
            if (progress != null)
            {
                deploymentOperation.Progress = (operation, progressInfo) =>
                {
                    progress.Report(progressInfo.percentage);
                };
            }
            
            var result = await deploymentOperation.AsTask();
            
            if (result.IsRegistered)
            {
                _diagnostics.Log(LogLevel.Success, $"Successfully installed: {packageInfo.DisplayName}");
                return OperationResult.Succeeded(
                    OperationType.Install,
                    packageInfo,
                    $"Successfully installed {packageInfo.DisplayName} v{packageInfo.Version}");
            }
            else
            {
                var errorMessage = _errorDecoder.DecodeError(result.ExtendedErrorCode.HResult);
                _diagnostics.Log(LogLevel.Error, $"Installation failed: {errorMessage}", result.ErrorText);
                
                return OperationResult.Failed(
                    OperationType.Install,
                    packageInfo,
                    errorMessage,
                    $"0x{result.ExtendedErrorCode.HResult:X8}",
                    result.ErrorText);
            }
        }
        catch (Exception ex)
        {
            var errorMessage = _errorDecoder.DecodeException(ex);
            _diagnostics.Log(LogLevel.Error, $"Installation exception: {errorMessage}", ex.ToString());
            
            return OperationResult.Failed(
                OperationType.Install,
                packageInfo,
                errorMessage,
                ex.HResult.ToString("X8"),
                ex.ToString());
        }
    }
    
    /// <summary>
    /// Uninstalls a package
    /// </summary>
    public async Task<OperationResult> UninstallPackageAsync(
        PackageInfo packageInfo,
        IProgress<double>? progress = null,
        bool removeProvisionedPackage = false)
    {
        _diagnostics.Log(LogLevel.Info, $"Starting uninstallation: {packageInfo.DisplayName}");
        
        if (packageInfo.IsSystemProtected)
        {
            _diagnostics.Log(LogLevel.Warning, "Cannot uninstall system-protected package");
            return OperationResult.Failed(
                OperationType.Uninstall,
                packageInfo,
                "This is a system-protected package and cannot be uninstalled.",
                null,
                "Package is signed as a system package and is protected by Windows.");
        }
        
        try
        {
            var deploymentOperation = _packageManager.RemovePackageAsync(
                packageInfo.PackageFullName,
                RemovalOptions.None);
            
            if (progress != null)
            {
                deploymentOperation.Progress = (operation, progressInfo) =>
                {
                    progress.Report(progressInfo.percentage);
                };
            }
            
            var result = await deploymentOperation.AsTask();
            
            _diagnostics.Log(LogLevel.Success, $"Successfully uninstalled: {packageInfo.DisplayName}");
            
            return OperationResult.Succeeded(
                OperationType.Uninstall,
                packageInfo,
                $"Successfully uninstalled {packageInfo.DisplayName}");
        }
        catch (Exception ex)
        {
            var errorMessage = _errorDecoder.DecodeException(ex);
            _diagnostics.Log(LogLevel.Error, $"Uninstallation failed: {errorMessage}", ex.ToString());
            
            return OperationResult.Failed(
                OperationType.Uninstall,
                packageInfo,
                errorMessage,
                ex.HResult.ToString("X8"),
                ex.ToString());
        }
    }
    
    /// <summary>
    /// Checks if sideloading is enabled on the system
    /// </summary>
    public bool IsSideloadingEnabled()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\AppModelUnlock");
            
            if (key != null)
            {
                var allowSideloading = key.GetValue("AllowAllTrustedApps");
                var developerMode = key.GetValue("AllowDevelopmentWithoutDevLicense");
                
                return (allowSideloading is int s && s == 1) || 
                       (developerMode is int d && d == 1);
            }
        }
        catch
        {
            // Cannot determine sideloading status
        }
        
        return false;
    }
    
    /// <summary>
    /// Checks if elevation is required for certain operations
    /// </summary>
    public bool RequiresElevation(PackageInfo packageInfo)
    {
        // Provisioned packages require elevation
        // Machine-scope packages require elevation
        return packageInfo.Scope == PackageScope.Machine;
    }
}
