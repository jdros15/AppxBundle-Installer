using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AppxBundleInstaller.Models;
using AppxBundleInstaller.Services;

namespace AppxBundleInstaller.ViewModels;

/// <summary>
/// ViewModel for the installed packages browser
/// </summary>
public partial class PackageListViewModel : ObservableObject
{
    private readonly PackageEnumerationService _enumeration;
    private readonly PackageManagerService _packageManager;
    private readonly DiagnosticsService _diagnostics;
    
    [ObservableProperty]
    private ObservableCollection<PackageInfo> _packages = new();
    
    [ObservableProperty]
    private PackageInfo? _selectedPackage;
    
    [ObservableProperty]
    private string _searchText = string.Empty;
    
    [ObservableProperty]
    private PublisherType _publisherFilter = PublisherType.All;
    
    [ObservableProperty]
    private bool _includeFrameworks = false;
    
    [ObservableProperty]
    private bool _isLoading;
    
    [ObservableProperty]
    private bool _isUninstalling;
    
    [ObservableProperty]
    private double _uninstallProgress;
    
    [ObservableProperty]
    private int _totalCount;
    
    [ObservableProperty]
    private OperationResult? _lastResult;
    
    public PackageListViewModel(
        PackageEnumerationService enumeration,
        PackageManagerService packageManager,
        DiagnosticsService diagnostics)
    {
        _enumeration = enumeration;
        _packageManager = packageManager;
        _diagnostics = diagnostics;
    }
    
    [RelayCommand]
    private async Task LoadPackages()
    {
        IsLoading = true;
        _diagnostics.Log(LogLevel.Info, "Loading installed packages...");
        
        try
        {
            var filter = new PackageFilter
            {
                PublisherType = PublisherFilter,
                IncludeFrameworks = IncludeFrameworks
            };
            
            var packages = string.IsNullOrWhiteSpace(SearchText)
                ? await _enumeration.GetInstalledPackagesAsync(filter)
                : await _enumeration.SearchPackagesAsync(SearchText, filter);
            
            Packages.Clear();
            foreach (var pkg in packages)
            {
                Packages.Add(pkg);
            }
            
            TotalCount = Packages.Count;
            _diagnostics.Log(LogLevel.Info, $"Loaded {TotalCount} packages");
        }
        catch (Exception ex)
        {
            _diagnostics.Log(LogLevel.Error, $"Failed to load packages: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }
    
    partial void OnSearchTextChanged(string value)
    {
        // Debounce would be ideal here, but for simplicity, just reload
        _ = LoadPackages();
    }
    
    partial void OnPublisherFilterChanged(PublisherType value)
    {
        _ = LoadPackages();
    }
    
    partial void OnIncludeFrameworksChanged(bool value)
    {
        _ = LoadPackages();
    }
    
    [RelayCommand(CanExecute = nameof(CanUninstall))]
    private async Task UninstallPackage()
    {
        if (SelectedPackage == null)
            return;
        
        // Show warning for system-protected packages
        if (SelectedPackage.IsSystemProtected)
        {
            MessageBox.Show(
                $"'{SelectedPackage.DisplayName}' is a system-protected package and cannot be uninstalled.\n\n" +
                "This package is required by Windows and removing it could cause system instability.",
                "Cannot Uninstall",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }
        
        // Confirmation dialog
        var result = MessageBox.Show(
            $"Are you sure you want to uninstall '{SelectedPackage.DisplayName}'?\n\n" +
            "This action cannot be undone.",
            "Confirm Uninstall",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        
        if (result != MessageBoxResult.Yes)
            return;
        
        IsUninstalling = true;
        UninstallProgress = 0;
        
        var progress = new Progress<double>(p => UninstallProgress = p);
        
        LastResult = await _packageManager.UninstallPackageAsync(SelectedPackage, progress);
        
        IsUninstalling = false;
        
        if (LastResult.Success)
        {
            Packages.Remove(SelectedPackage);
            SelectedPackage = null;
            TotalCount = Packages.Count;
        }
        else
        {
            MessageBox.Show(
                $"Failed to uninstall package:\n\n{LastResult.Message}",
                "Uninstall Failed",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }
    
    private bool CanUninstall() => SelectedPackage != null && !IsUninstalling;
    
    [RelayCommand]
    private async Task ExportPackageList()
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "CSV Files|*.csv|Text Files|*.txt",
            Title = "Export Package List",
            FileName = $"installed_packages_{DateTime.Now:yyyyMMdd}"
        };
        
        if (dialog.ShowDialog() == true)
        {
            try
            {
                var lines = new List<string>
                {
                    "Name,DisplayName,Publisher,Version,Architecture,PackageFamilyName,InstallLocation"
                };
                
                foreach (var pkg in Packages)
                {
                    lines.Add($"\"{pkg.Name}\",\"{pkg.DisplayName}\",\"{pkg.PublisherDisplayName}\",\"{pkg.Version}\",\"{pkg.Architecture}\",\"{pkg.PackageFamilyName}\",\"{pkg.InstallLocation}\"");
                }
                
                await System.IO.File.WriteAllLinesAsync(dialog.FileName, lines);
                
                _diagnostics.Log(LogLevel.Success, $"Exported {Packages.Count} packages to {dialog.FileName}");
                
                MessageBox.Show(
                    $"Successfully exported {Packages.Count} packages.",
                    "Export Complete",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                _diagnostics.Log(LogLevel.Error, $"Export failed: {ex.Message}");
                MessageBox.Show(
                    $"Failed to export: {ex.Message}",
                    "Export Failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
    }
    
    [RelayCommand]
    private void OpenInstallLocation()
    {
        if (SelectedPackage != null && !string.IsNullOrEmpty(SelectedPackage.InstallLocation) 
            && SelectedPackage.InstallLocation != "N/A")
        {
            try
            {
                System.Diagnostics.Process.Start("explorer.exe", SelectedPackage.InstallLocation);
            }
            catch (Exception ex)
            {
                _diagnostics.Log(LogLevel.Error, $"Failed to open location: {ex.Message}");
            }
        }
    }
}
