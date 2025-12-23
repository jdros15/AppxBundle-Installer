using System.Diagnostics;
using System.ComponentModel;

namespace AppxBundleInstaller.Services;

/// <summary>
/// Handles UAC elevation when required for certain operations
/// </summary>
public class ElevationService
{
    /// <summary>
    /// Checks if the current process is running with elevated privileges
    /// </summary>
    public bool IsElevated()
    {
        using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
        var principal = new System.Security.Principal.WindowsPrincipal(identity);
        return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
    }
    
    /// <summary>
    /// Restarts the application with administrator privileges
    /// </summary>
    /// <param name="arguments">Command line arguments to pass to the elevated process</param>
    /// <returns>True if elevation was successful, false if user cancelled UAC</returns>
    public bool RestartAsAdmin(string? arguments = null)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName,
                Arguments = arguments ?? string.Empty,
                Verb = "runas",
                UseShellExecute = true
            };
            
            Process.Start(startInfo);
            
            // Shutdown the current instance
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                System.Windows.Application.Current.Shutdown();
            });
            
            return true;
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223) // ERROR_CANCELLED
        {
            // User cancelled the UAC prompt
            return false;
        }
        catch
        {
            return false;
        }
    }
    
    /// <summary>
    /// Runs a specific operation in an elevated PowerShell process
    /// Used for operations that don't require full app elevation
    /// </summary>
    public async Task<(bool Success, string Output, string Error)> RunElevatedCommandAsync(string script)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{script}\"",
                Verb = "runas",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            
            using var process = Process.Start(startInfo);
            if (process == null)
            {
                return (false, "", "Failed to start elevated process");
            }
            
            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            
            return (process.ExitCode == 0, output, error);
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            return (false, "", "User cancelled elevation request");
        }
        catch (Exception ex)
        {
            return (false, "", ex.Message);
        }
    }
    
    /// <summary>
    /// Shows a consent dialog before requesting elevation
    /// </summary>
    public bool RequestElevationConsent(string operationDescription)
    {
        var result = System.Windows.MessageBox.Show(
            $"The following operation requires administrator privileges:\n\n{operationDescription}\n\nDo you want to continue? Windows will prompt for administrator approval.",
            "Elevation Required",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);
        
        return result == System.Windows.MessageBoxResult.Yes;
    }
}
