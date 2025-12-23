using System.Windows;
using ModernWpf;

namespace AppxBundleInstaller;

/// <summary>
/// Application entry point
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        
        // Set initial theme based on system preference
        ThemeManager.Current.ApplicationTheme = GetSystemTheme();
    }
    
    private static ApplicationTheme? GetSystemTheme()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            
            var value = key?.GetValue("AppsUseLightTheme");
            if (value is int useLightTheme)
            {
                return useLightTheme == 1 ? ApplicationTheme.Light : ApplicationTheme.Dark;
            }
        }
        catch
        {
            // Ignore registry access errors
        }
        
        return null; // Use system default
    }
}
