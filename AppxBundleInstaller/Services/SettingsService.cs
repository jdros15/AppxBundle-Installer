using System.IO;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AppxBundleInstaller.Services;

public partial class SettingsService : ObservableObject
{
    private static SettingsService? _instance;
    public static SettingsService Instance => _instance ??= new SettingsService();

    private readonly string _settingsPath;

    [ObservableProperty]
    private string _downloadFolderPath;

    [ObservableProperty]
    private bool _autoInstall;

    [ObservableProperty]
    private bool _isDarkMode;

    [ObservableProperty]
    private bool _showAppIcons = true;

    [ObservableProperty]
    private bool _showCriticalApps;

    public bool IsLoaded { get; private set; }

    private SettingsService()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var appFolder = Path.Combine(appData, "AppxBundleInstaller");
        Directory.CreateDirectory(appFolder);
        _settingsPath = Path.Combine(appFolder, "settings.json");
        
        // Default download path
        string defaultDownload = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        _downloadFolderPath = defaultDownload;

        LoadSettings();
    }

    public void LoadSettings()
    {
        try
        {
            if (File.Exists(_settingsPath))
            {
                var json = File.ReadAllText(_settingsPath);
                var settings = JsonSerializer.Deserialize<SettingsData>(json);
                if (settings != null)
                {
                    DownloadFolderPath = settings.DownloadFolderPath ?? _downloadFolderPath;
                    AutoInstall = settings.AutoInstall;
                    IsDarkMode = settings.IsDarkMode;
                    ShowAppIcons = settings.ShowAppIcons;
                    ShowCriticalApps = settings.ShowCriticalApps;
                    IsLoaded = true;
                }
            }
        }
        catch { /* Ignore errors */ }
    }

    public void SaveSettings()
    {
        try
        {
            var settings = new SettingsData
            {
                DownloadFolderPath = DownloadFolderPath,
                AutoInstall = AutoInstall,
                IsDarkMode = IsDarkMode,
                ShowAppIcons = ShowAppIcons,
                ShowCriticalApps = ShowCriticalApps
            };
            var json = JsonSerializer.Serialize(settings);
            File.WriteAllText(_settingsPath, json);
        }
        catch { /* Ignore errors */ }
    }

    partial void OnDownloadFolderPathChanged(string value) => SaveSettings();
    partial void OnAutoInstallChanged(bool value) => SaveSettings();
    partial void OnIsDarkModeChanged(bool value) => SaveSettings();
    partial void OnShowAppIconsChanged(bool value) => SaveSettings();
    partial void OnShowCriticalAppsChanged(bool value) => SaveSettings();

    private class SettingsData
    {
        public string? DownloadFolderPath { get; set; }
        public bool AutoInstall { get; set; }
        public bool IsDarkMode { get; set; }
        public bool ShowAppIcons { get; set; }
        public bool ShowCriticalApps { get; set; }
    }
}
