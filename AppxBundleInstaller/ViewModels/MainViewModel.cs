using System.Collections.ObjectModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AppxBundleInstaller.Models;
using AppxBundleInstaller.Services;
using ModernWpf;

namespace AppxBundleInstaller.ViewModels;

/// <summary>
/// Main window view model - handles navigation and global state
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private readonly DiagnosticsService _diagnostics;
    private readonly PackageEnumerationService _packageEnumeration;
    private readonly PackageManagerService _packageManager;
    private readonly PackageValidationService _packageValidation;
    private readonly ElevationService _elevation;
    
    [ObservableProperty]
    private bool _isInstallViewActive = true;
    
    [ObservableProperty]
    private bool _isInstalledViewActive;
    
    [ObservableProperty]
    private bool _isDiagnosticsViewActive;
    
    [ObservableProperty]
    private bool _isDarkMode;
    
    // Shared state for child view models
    [ObservableProperty]
    private PackageInfo? _selectedPackage;
    
    [ObservableProperty]
    private string _statusMessage = "Ready";
    
    [ObservableProperty]
    private bool _isOperationInProgress;
    
    [ObservableProperty]
    private double _operationProgress;
    
    public ObservableCollection<LogEntry> Logs => _diagnostics.Logs;
    
    public MainViewModel()
    {
        _diagnostics = new DiagnosticsService();
        _packageEnumeration = new PackageEnumerationService();
        _packageManager = new PackageManagerService(_diagnostics);
        _packageValidation = new PackageValidationService();
        _elevation = new ElevationService();
        
        // Initialize theme from system
        _isDarkMode = ThemeManager.Current.ActualApplicationTheme == ApplicationTheme.Dark;
        
        _diagnostics.Log(LogLevel.Info, "AppxBundle Installer started");
        
        // Check sideloading status
        if (!_packageManager.IsSideloadingEnabled())
        {
            _diagnostics.Log(LogLevel.Warning, "Sideloading may not be enabled. Developer Mode recommended for installing unsigned packages.");
        }
        
        if (_elevation.IsElevated())
        {
            _diagnostics.Log(LogLevel.Info, "Running with administrator privileges");
        }
    }
    
    partial void OnIsDarkModeChanged(bool value)
    {
        ThemeManager.Current.ApplicationTheme = value ? ApplicationTheme.Dark : ApplicationTheme.Light;
    }
    
    partial void OnIsInstallViewActiveChanged(bool value)
    {
        if (value)
        {
            IsInstalledViewActive = false;
            IsDiagnosticsViewActive = false;
        }
    }
    
    partial void OnIsInstalledViewActiveChanged(bool value)
    {
        if (value)
        {
            IsInstallViewActive = false;
            IsDiagnosticsViewActive = false;
        }
    }
    
    partial void OnIsDiagnosticsViewActiveChanged(bool value)
    {
        if (value)
        {
            IsInstallViewActive = false;
            IsInstalledViewActive = false;
        }
    }
    
    // Services exposed for child view models
    public DiagnosticsService Diagnostics => _diagnostics;
    public PackageEnumerationService PackageEnumeration => _packageEnumeration;
    public PackageManagerService PackageManager => _packageManager;
    public PackageValidationService PackageValidation => _packageValidation;
    public ElevationService Elevation => _elevation;
    
    public void ReportProgress(double progress)
    {
        OperationProgress = progress;
    }
    
    public void SetStatus(string message)
    {
        StatusMessage = message;
    }
}
