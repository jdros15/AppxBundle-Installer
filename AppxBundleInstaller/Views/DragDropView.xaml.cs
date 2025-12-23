using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.IO;
using Microsoft.Win32;
using AppxBundleInstaller.ViewModels;
using AppxBundleInstaller.Services;

namespace AppxBundleInstaller.Views;

public partial class DragDropView : UserControl
{
    private static readonly string[] ValidExtensions = { ".appx", ".appxbundle", ".msix", ".msixbundle" };
    private readonly PackageValidationService _validation = new();
    
    public DragDropView()
    {
        InitializeComponent();
    }
    
    private MainViewModel? MainVm => DataContext as MainViewModel 
                                    ?? (Parent as FrameworkElement)?.DataContext as MainViewModel
                                    ?? Window.GetWindow(this)?.DataContext as MainViewModel;
    
    private void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Package Files|*.appx;*.appxbundle;*.msix;*.msixbundle|All Files|*.*",
            Title = "Select Package to Install"
        };
        
        if (dialog.ShowDialog() == true)
        {
            ProcessFile(dialog.FileName);
        }
    }
    
    private void DropZone_DragEnter(object sender, DragEventArgs e)
    {
        if (IsValidDrop(e))
        {
            DropZone.BorderBrush = new SolidColorBrush(Color.FromRgb(59, 130, 246)); // Accent blue
            DropZone.Background = new SolidColorBrush(Color.FromArgb(30, 59, 130, 246));
            e.Effects = DragDropEffects.Copy;
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }
        e.Handled = true;
    }
    
    private void DropZone_DragLeave(object sender, DragEventArgs e)
    {
        ResetDropZoneStyle();
    }
    
    private void DropZone_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = IsValidDrop(e) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }
    
    private void DropZone_Drop(object sender, DragEventArgs e)
    {
        ResetDropZoneStyle();
        
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files.Length > 0)
            {
                ProcessFile(files[0]);
            }
        }
    }
    
    private bool IsValidDrop(DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files.Length == 1)
            {
                var ext = Path.GetExtension(files[0]).ToLowerInvariant();
                return ValidExtensions.Contains(ext);
            }
        }
        return false;
    }
    
    private void ResetDropZoneStyle()
    {
        DropZone.BorderBrush = (Brush)FindResource("SystemControlForegroundBaseMediumLowBrush");
        DropZone.Background = (Brush)FindResource("SystemControlBackgroundChromeMediumLowBrush");
    }
    
    private async void ProcessFile(string filePath)
    {
        MainVm?.Diagnostics?.Log(Models.LogLevel.Info, $"Processing: {Path.GetFileName(filePath)}");
        
        StatusPanel.Visibility = Visibility.Visible;
        DropZone.Visibility = Visibility.Collapsed;
        StatusText.Text = "Validating package...";
        
        try
        {
            var (isValid, info, error) = await _validation.ValidateAndExtractAsync(filePath);
            
            if (!isValid)
            {
                MainVm?.Diagnostics?.Log(Models.LogLevel.Error, $"Validation failed: {error}");
                MessageBox.Show($"Cannot install this package:\n\n{error}", "Validation Failed", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
                ResetView();
                return;
            }
            
            // Show package info and confirm
            var message = $"Package: {info!.DisplayName}\n" +
                         $"Version: {info.Version}\n" +
                         $"Publisher: {info.PublisherDisplayName}\n" +
                         $"Architecture: {info.Architecture}\n" +
                         $"Signature: {info.SignatureStatus}\n\n" +
                         "Do you want to install this package?";
            
            var result = MessageBox.Show(message, "Install Package", 
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            
            if (result == MessageBoxResult.Yes)
            {
                StatusText.Text = $"Installing {info.DisplayName}...";
                ProgressBar.IsIndeterminate = true;
                
                var packageManager = new PackageManagerService(MainVm?.Diagnostics ?? new DiagnosticsService());
                var installResult = await packageManager.InstallPackageAsync(filePath, info);
                
                if (installResult.Success)
                {
                    MainVm?.Diagnostics?.Log(Models.LogLevel.Success, $"Installed: {info.DisplayName}");
                    MessageBox.Show($"Successfully installed {info.DisplayName}!", "Success", 
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MainVm?.Diagnostics?.Log(Models.LogLevel.Error, installResult.Message);
                    MessageBox.Show($"Installation failed:\n\n{installResult.Message}", "Error", 
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
        catch (Exception ex)
        {
            MainVm?.Diagnostics?.Log(Models.LogLevel.Error, ex.Message);
            MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        
        ResetView();
    }
    
    private void ResetView()
    {
        StatusPanel.Visibility = Visibility.Collapsed;
        DropZone.Visibility = Visibility.Visible;
        ProgressBar.IsIndeterminate = false;
    }
}
