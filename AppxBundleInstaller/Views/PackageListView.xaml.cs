using System.Windows;
using System.Windows.Controls;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using AppxBundleInstaller.Models;
using AppxBundleInstaller.Services;
using AppxBundleInstaller.ViewModels;

namespace AppxBundleInstaller.Views;

public partial class PackageListView : UserControl
{
    private readonly PackageEnumerationService _enumeration = new();
    private List<PackageInfo> _allPackages = new();
    private PackageInfo? _selectedPackage;
    
    public PackageListView()
    {
        InitializeComponent();
    }
    
    private MainViewModel? MainVm => Window.GetWindow(this)?.DataContext as MainViewModel;
    
    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Subscribe to MainViewModel property changes to reload when settings change
        if (MainVm != null)
        {
            MainVm.PropertyChanged += MainVm_PropertyChanged;
        }
        await LoadPackages();
    }
    
    private async void MainVm_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // Reload packages when ShowCriticalApps or ShowAppIcons setting changes
        if (e.PropertyName == nameof(MainViewModel.ShowCriticalApps) ||
            e.PropertyName == nameof(MainViewModel.ShowAppIcons))
        {
            await LoadPackages();
        }
    }
    
    private async System.Threading.Tasks.Task LoadPackages()
    {
        LoadingPanel.Visibility = Visibility.Visible;
        PackageList.Visibility = Visibility.Collapsed;
        
        try
        {
            var filter = new PackageFilter
            {
                PublisherType = GetSelectedPublisherType(),
                IncludeFrameworks = ShowFrameworks.IsChecked == true,
                IncludeCriticalApps = MainVm?.ShowCriticalApps ?? false
            };
            
            _allPackages = (await _enumeration.GetInstalledPackagesAsync(filter)).ToList();
            ApplySearch();
            
            MainVm?.Diagnostics?.Log(LogLevel.Info, $"Loaded {_allPackages.Count} packages");
        }
        catch (Exception ex)
        {
            MainVm?.Diagnostics?.Log(LogLevel.Error, $"Failed to load packages: {ex.Message}");
            MessageBox.Show($"Error loading packages: {ex.Message}", "Error", 
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            LoadingPanel.Visibility = Visibility.Collapsed;
            PackageList.Visibility = Visibility.Visible;
        }
    }
    
    private void ApplySearch()
    {
        var search = SearchBox.Text?.ToLowerInvariant() ?? "";
        
        var filtered = string.IsNullOrWhiteSpace(search) 
            ? _allPackages 
            : _allPackages.Where(p => 
                p.DisplayName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                p.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                p.PublisherDisplayName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                p.PackageFamilyName.Contains(search, StringComparison.OrdinalIgnoreCase));
        
        // Apply sorting
        var sorted = ApplySorting(filtered);
        
        PackageList.ItemsSource = sorted.ToList();
        StatusText.Text = $"{PackageList.Items.Count} packages";
    }
    
    private IEnumerable<PackageInfo> ApplySorting(IEnumerable<PackageInfo> packages)
    {
        var sortIndex = SortOrder?.SelectedIndex ?? 0;
        
        return sortIndex switch
        {
            0 => packages.OrderBy(p => p.DisplayName),                                    // Name (A-Z)
            1 => packages.OrderByDescending(p => p.DisplayName),                          // Name (Z-A)
            2 => packages.OrderByDescending(p => p.InstallDate ?? DateTime.MinValue),     // Recently Installed
            3 => packages.OrderBy(p => p.InstallDate ?? DateTime.MaxValue),               // Oldest First
            4 => packages.OrderBy(p => p.PublisherDisplayName).ThenBy(p => p.DisplayName), // Publisher
            5 => packages.OrderByDescending(p => p.Version),                              // Version
            _ => packages.OrderBy(p => p.DisplayName)
        };
    }
    
    private void SortOrder_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (IsLoaded)
            ApplySearch();
    }
    
    private PublisherType GetSelectedPublisherType()
    {
        return PublisherFilter.SelectedIndex switch
        {
            1 => PublisherType.Microsoft,
            2 => PublisherType.ThirdParty,
            _ => PublisherType.All
        };
    }
    
    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        ApplySearch();
    }
    
    private async void PublisherFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (IsLoaded)
            await LoadPackages();
    }
    
    private async void Filter_Changed(object sender, RoutedEventArgs e)
    {
        if (IsLoaded)
            await LoadPackages();
    }
    
    private async void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        await LoadPackages();
    }
    
    private void PackageList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _selectedPackage = PackageList.SelectedItem as PackageInfo;
        
        if (_selectedPackage != null)
        {
            SelectedName.Text = _selectedPackage.DisplayName;
            SelectedFamily.Text = _selectedPackage.PackageFamilyName;
            ActionPanel.Visibility = Visibility.Visible;
            StatusText.Visibility = Visibility.Collapsed;
            
            // Always enable uninstall, but show warning tooltips
            UninstallButton.IsEnabled = true;
            
            if (_selectedPackage.IsSystemProtected)
            {
                UninstallButton.ToolTip = "⚠️ System-protected package - Uninstall at your own risk!";
            }
            else if (_selectedPackage.IsCriticalSystemApp)
            {
                UninstallButton.ToolTip = "⚠️ Critical system app - Uninstalling may break Windows!";
            }
            else
            {
                UninstallButton.ToolTip = null;
            }
        }
        else
        {
            ActionPanel.Visibility = Visibility.Collapsed;
            StatusText.Visibility = Visibility.Visible;
        }
    }
    
    private void OpenLocation_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedPackage != null && !string.IsNullOrEmpty(_selectedPackage.InstallLocation) 
            && _selectedPackage.InstallLocation != "N/A")
        {
            try
            {
                System.Diagnostics.Process.Start("explorer.exe", _selectedPackage.InstallLocation);
            }
            catch (Exception ex)
            {
                MainVm?.Diagnostics?.Log(LogLevel.Error, $"Failed to open location: {ex.Message}");
            }
        }
    }
    
    private async void Uninstall_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedPackage == null) return;
        
        // Warning for system-protected packages - allow with explicit warning
        if (_selectedPackage.IsSystemProtected)
        {
            var protectedWarningResult = MessageBox.Show(
                $"⛔ SYSTEM-PROTECTED PACKAGE ⛔\n\n" +
                $"'{_selectedPackage.DisplayName}' is marked as SYSTEM-PROTECTED by Windows.\n\n" +
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
            
            if (protectedWarningResult != MessageBoxResult.Yes)
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
        if (_selectedPackage.IsCriticalSystemApp)
        {
            var criticalWarningResult = MessageBox.Show(
                $"⚠️ CRITICAL WARNING ⚠️\n\n" +
                $"'{_selectedPackage.DisplayName}' is a CRITICAL SYSTEM APP.\n\n" +
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
            
            if (criticalWarningResult != MessageBoxResult.Yes)
                return;
            
            // Second confirmation - make it very clear
            var secondWarningResult = MessageBox.Show(
                $"⛔ FINAL WARNING ⛔\n\n" +
                $"You are about to uninstall:\n" +
                $"'{_selectedPackage.DisplayName}'\n\n" +
                $"This is a CRITICAL SYSTEM COMPONENT.\n\n" +
                $"If Windows becomes unusable after this, you may need to:\n" +
                $"• Boot into Safe Mode\n" +
                $"• Use System Restore\n" +
                $"• Perform a complete Windows Reset\n\n" +
                $"Are you 100% certain you want to continue?\n\n" +
                $"Click NO to cancel (RECOMMENDED).",
                "⛔ FINAL CONFIRMATION ⛔",
                MessageBoxButton.YesNo,
                MessageBoxImage.Stop);
            
            if (secondWarningResult != MessageBoxResult.Yes)
                return;
        }
        else
        {
            // Regular confirmation for non-critical apps
            var result = MessageBox.Show(
                $"Are you sure you want to uninstall '{_selectedPackage.DisplayName}'?\n\nThis cannot be undone.",
                "Confirm Uninstall", MessageBoxButton.YesNo, MessageBoxImage.Question);
            
            if (result != MessageBoxResult.Yes) return;
        }
        
        try
        {
            var packageManager = new PackageManagerService(MainVm?.Diagnostics ?? new DiagnosticsService());
            var uninstallResult = await packageManager.UninstallPackageAsync(_selectedPackage);
            
            if (uninstallResult.Success)
            {
                MainVm?.Diagnostics?.Log(LogLevel.Success, $"Uninstalled: {_selectedPackage.DisplayName}");
                MessageBox.Show($"Successfully uninstalled {_selectedPackage.DisplayName}!", "Success",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                await LoadPackages();
            }
            else
            {
                MainVm?.Diagnostics?.Log(LogLevel.Error, uninstallResult.Message);
                MessageBox.Show($"Failed to uninstall:\n\n{uninstallResult.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        catch (Exception ex)
        {
            MainVm?.Diagnostics?.Log(LogLevel.Error, ex.Message);
            MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
    
    private async void ExportButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "CSV Files|*.csv",
            Title = "Export Package List",
            FileName = $"installed_packages_{DateTime.Now:yyyyMMdd}"
        };
        
        if (dialog.ShowDialog() == true)
        {
            try
            {
                var lines = new List<string> { "Name,DisplayName,Publisher,Version,Architecture,PackageFamilyName" };
                foreach (var pkg in _allPackages)
                {
                    lines.Add($"\"{pkg.Name}\",\"{pkg.DisplayName}\",\"{pkg.PublisherDisplayName}\",\"{pkg.Version}\",\"{pkg.Architecture}\",\"{pkg.PackageFamilyName}\"");
                }
                await System.IO.File.WriteAllLinesAsync(dialog.FileName, lines);
                
                MainVm?.Diagnostics?.Log(LogLevel.Success, $"Exported {_allPackages.Count} packages");
                MessageBox.Show($"Exported {_allPackages.Count} packages.", "Export Complete",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Export failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
