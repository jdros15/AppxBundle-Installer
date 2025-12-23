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
    private PackageSortOption _sortOption;
    
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
        
        // Initialize sort option from settings
        _sortOption = SettingsService.Instance.SortOption;
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
                ? await _enumeration.GetInstalledPackagesAsync(filter, SortOption)
                : await _enumeration.SearchPackagesAsync(SearchText, filter, SortOption);
            
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
    
    partial void OnSortOptionChanged(PackageSortOption value)
    {
        SettingsService.Instance.SortOption = value;
        _ = LoadPackages();
    }
    
    [RelayCommand(CanExecute = nameof(CanUninstall))]
    private async Task UninstallPackage()
    {
        if (SelectedPackage == null)
            return;
        
        // Warning for system-protected packages - allow with explicit warning
        if (SelectedPackage.IsSystemProtected)
        {
            var protectedResult = MessageBox.Show(
                $"⛔ SYSTEM-PROTECTED PACKAGE ⛔\n\n" +
                $"'{SelectedPackage.DisplayName}' is marked as SYSTEM-PROTECTED by Windows.\n\n" +
                $"This means Windows considers this package essential for system operation.\n\n" +
                $"Uninstalling this package may:\n" +
                $"• Cause immediate system instability\n" +
                $"• Prevent Windows features from working\n" +
                $"• Require system recovery or reinstallation\n" +
                $"• Result in data loss\n\n" +
                $"Do you understand the risks and want to attempt uninstallation anyway?",
                "⛔ System-Protected Package Warning ⛔",
                MessageBoxButton.YesNo,
                MessageBoxImage.Stop);
            
            if (protectedResult != MessageBoxResult.Yes)
                return;
            
            // Second confirmation for system-protected
            var finalProtectedResult = MessageBox.Show(
                $"⛔ FINAL WARNING ⛔\n\n" +
                $"You are about to attempt uninstalling a SYSTEM-PROTECTED package.\n\n" +
                $"This operation may fail or cause serious problems.\n\n" +
                $"Click NO to cancel (STRONGLY RECOMMENDED).",
                "⛔ Confirm System-Protected Uninstall ⛔",
                MessageBoxButton.YesNo,
                MessageBoxImage.Stop);
            
            if (finalProtectedResult != MessageBoxResult.Yes)
                return;
        }
        
        // Enhanced warning for critical system apps
        if (SelectedPackage.IsCriticalSystemApp)
        {
            var criticalResult = MessageBox.Show(
                $"⚠️ CRITICAL WARNING ⚠️\n\n" +
                $"'{SelectedPackage.DisplayName}' is a CRITICAL SYSTEM APP.\n\n" +
                $"Uninstalling this app may:\n" +
                $"• Prevent Windows from starting properly\n" +
                $"• Cause the Start Menu to stop working\n" +
                $"• Break essential Windows functionality\n" +
                $"• Require a complete Windows reset to fix\n\n" +
                $"Are you ABSOLUTELY SURE you want to proceed?\n\n" +
                $"This action is EXTREMELY DANGEROUS and CANNOT be undone!",
                "⚠️ CRITICAL SYSTEM APP - DANGER ⚠️",
                MessageBoxButton.YesNo,
                MessageBoxImage.Stop);
            
            if (criticalResult != MessageBoxResult.Yes)
                return;
            
            // Second confirmation
            var finalResult = MessageBox.Show(
                $"FINAL WARNING:\n\n" +
                $"You are about to uninstall '{SelectedPackage.DisplayName}'.\n\n" +
                $"This is your LAST chance to cancel.\n\n" +
                $"Click YES only if you fully understand the consequences.",
                "Final Confirmation",
                MessageBoxButton.YesNo,
                MessageBoxImage.Stop);
            
            if (finalResult != MessageBoxResult.Yes)
                return;
        }
        else
        {
            // Regular confirmation dialog
            var result = MessageBox.Show(
                $"Are you sure you want to uninstall '{SelectedPackage.DisplayName}'?\n\n" +
                "This action cannot be undone.",
                "Confirm Uninstall",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            
            if (result != MessageBoxResult.Yes)
                return;
        }
        
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
