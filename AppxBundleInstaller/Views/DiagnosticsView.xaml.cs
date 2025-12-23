using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AppxBundleInstaller.ViewModels;
using AppxBundleInstaller.Services;
using Microsoft.Win32;

namespace AppxBundleInstaller.Views;

public partial class DiagnosticsView : UserControl
{
    public DiagnosticsView()
    {
        InitializeComponent();
    }
    
    private MainViewModel? MainVm => Window.GetWindow(this)?.DataContext as MainViewModel;
    
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        LoadSystemInfo();
        
        // Bind to logs from MainViewModel
        if (MainVm != null)
        {
            LogListView.ItemsSource = MainVm.Diagnostics.Logs;
        }
    }
    
    private void LoadSystemInfo()
    {
        // Windows Version
        WindowsVersionText.Text = $"Windows {Environment.OSVersion.Version}";
        
        // Sideloading status
        var sideloadingEnabled = false;
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\AppModelUnlock");
            
            if (key != null)
            {
                var allowSideloading = key.GetValue("AllowAllTrustedApps");
                var developerMode = key.GetValue("AllowDevelopmentWithoutDevLicense");
                
                sideloadingEnabled = (allowSideloading is int s && s == 1) || 
                                    (developerMode is int d && d == 1);
            }
        }
        catch { }
        
        SideloadingStatusText.Text = sideloadingEnabled ? "✓ Enabled" : "✗ Disabled";
        SideloadingStatusText.Foreground = sideloadingEnabled 
            ? new SolidColorBrush(Color.FromRgb(16, 185, 129))  // Green
            : new SolidColorBrush(Color.FromRgb(245, 158, 11)); // Orange
        
        // Elevation status
        var isElevated = new ElevationService().IsElevated();
        ElevationStatusText.Text = isElevated ? "✓ Administrator" : "Standard User";
    }
    
    private void ExportLog_Click(object sender, RoutedEventArgs e)
    {
        if (MainVm == null) return;
        
        var dialog = new SaveFileDialog
        {
            Filter = "Text Files|*.txt",
            Title = "Export Diagnostic Log",
            FileName = $"appxbundle_log_{DateTime.Now:yyyyMMdd_HHmmss}"
        };
        
        if (dialog.ShowDialog() == true)
        {
            try
            {
                var content = MainVm.Diagnostics.ExportLogs();
                System.IO.File.WriteAllText(dialog.FileName, content);
                
                MessageBox.Show("Log exported successfully.", "Export Complete",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to export log: {ex.Message}", "Export Failed",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
    
    private void ClearLog_Click(object sender, RoutedEventArgs e)
    {
        MainVm?.Diagnostics.Clear();
    }
}
