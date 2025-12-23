using System.Windows;
using System.Windows.Controls;
using System.Collections.Generic;
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
        await LoadPackages();
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
                IncludeFrameworks = ShowFrameworks.IsChecked == true
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
        
        PackageList.ItemsSource = filtered.ToList();
        StatusText.Text = $"{PackageList.Items.Count} packages";
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
            
            UninstallButton.IsEnabled = !_selectedPackage.IsSystemProtected;
            if (_selectedPackage.IsSystemProtected)
            {
                UninstallButton.ToolTip = "System-protected package";
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
        
        if (_selectedPackage.IsSystemProtected)
        {
            MessageBox.Show("This is a system-protected package and cannot be uninstalled.", 
                "Cannot Uninstall", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        
        var result = MessageBox.Show(
            $"Are you sure you want to uninstall '{_selectedPackage.DisplayName}'?\n\nThis cannot be undone.",
            "Confirm Uninstall", MessageBoxButton.YesNo, MessageBoxImage.Question);
        
        if (result != MessageBoxResult.Yes) return;
        
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
