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
    
    [ObservableProperty]
    private bool _showCriticalApps;
    
    [ObservableProperty]
    private bool _showAppIcons = true;
    
    // Shared state for child view models
    [ObservableProperty]
    private PackageInfo? _selectedPackage;
    
    [ObservableProperty]
    private string _statusMessage = "Ready";
    
    [ObservableProperty]
    private bool _isOperationInProgress;
    
    [ObservableProperty]
    private double _operationProgress;

    [ObservableProperty]
    private string _downloadFolderPath;

    [ObservableProperty]
    private bool _autoInstall;
    
    public ObservableCollection<LogEntry> Logs => _diagnostics.Logs;
    
    public static MainViewModel? Current { get; private set; }

    public event EventHandler<(string Path, bool AutoInstall)>? InstallRequested;
    public event EventHandler? PackagesChanged;
    
    public void RequestInstall(string filePath, bool autoInstall = false)
    {
        // Switch to install view
        NavInstall = true; // This will trigger UI binding if properties are bound to RadioButtons, but we have IsInstallViewActive
        IsInstallViewActive = true; // Use the property
        
        // Notify subscribers (InstallViewModel)
        InstallRequested?.Invoke(this, (filePath, autoInstall));
    }

    public void NotifyPackagesChanged()
    {
        PackagesChanged?.Invoke(this, EventArgs.Empty);
    }
    
    // Helper property/command to set IsInstallViewActive from code if needed for binding
    [ObservableProperty]
    private bool _navInstall = true; // To sync with RadioButton if two-way bound

    public MainViewModel()
    {
        Current = this;
        _diagnostics = DiagnosticsService.Instance;
        _packageEnumeration = new PackageEnumerationService();
        _packageManager = new PackageManagerService(_diagnostics);
        _packageValidation = new PackageValidationService();
        _elevation = new ElevationService();

        // Load settings
        var settings = SettingsService.Instance;
        
        if (!settings.IsLoaded)
        {
            // First run or no settings file - default to system theme
            _isDarkMode = ThemeManager.Current.ActualApplicationTheme == ApplicationTheme.Dark;
            settings.IsDarkMode = _isDarkMode;
        }
        else
        {
            _isDarkMode = settings.IsDarkMode;
        }

        _showAppIcons = settings.ShowAppIcons;
        _showCriticalApps = settings.ShowCriticalApps;
        _downloadFolderPath = settings.DownloadFolderPath;
        _autoInstall = settings.AutoInstall;
        
        // Initialize theme from system or settings
        ThemeManager.Current.ApplicationTheme = _isDarkMode ? ApplicationTheme.Dark : ApplicationTheme.Light;
        
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
        SettingsService.Instance.IsDarkMode = value;
    }

    partial void OnShowAppIconsChanged(bool value) => SettingsService.Instance.ShowAppIcons = value;
    partial void OnShowCriticalAppsChanged(bool value) => SettingsService.Instance.ShowCriticalApps = value;
    partial void OnDownloadFolderPathChanged(string value) => SettingsService.Instance.DownloadFolderPath = value;
    partial void OnAutoInstallChanged(bool value) => SettingsService.Instance.AutoInstall = value;

    [RelayCommand]
    private void BrowseDownloadFolder()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Select Download Folder",
            InitialDirectory = DownloadFolderPath
        };

        if (dialog.ShowDialog() == true)
        {
            DownloadFolderPath = dialog.FolderName;
        }
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
