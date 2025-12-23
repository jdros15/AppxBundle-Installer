using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AppxBundleInstaller.Models;
using AppxBundleInstaller.Services;
using Microsoft.Win32;

namespace AppxBundleInstaller.ViewModels;

/// <summary>
/// ViewModel for the drag-and-drop installation view
/// </summary>
public partial class InstallViewModel : ObservableObject
{
    private readonly PackageValidationService _validation;
    private readonly PackageManagerService _packageManager;
    private readonly DiagnosticsService _diagnostics;
    private readonly ElevationService _elevation;
    
    [ObservableProperty]
    private bool _isDragOver;
    
    [ObservableProperty]
    private PackageInfo? _pendingPackage;
    
    [ObservableProperty]
    private string? _pendingFilePath;
    
    [ObservableProperty]
    private bool _isValidating;
    
    [ObservableProperty]
    private bool _isInstalling;
    
    [ObservableProperty]
    private double _installProgress;
    
    [ObservableProperty]
    private string? _validationError;
    
    [ObservableProperty]
    private string? _statusMessage;
    
    [ObservableProperty]
    private bool _showPackageDetails;
    
    [ObservableProperty]
    private OperationResult? _lastResult;
    
    public InstallViewModel(
        PackageValidationService validation,
        PackageManagerService packageManager,
        DiagnosticsService diagnostics,
        ElevationService elevation)
    {
        _validation = validation;
        _packageManager = packageManager;
        _diagnostics = diagnostics;
        _elevation = elevation;
    }
    
    [RelayCommand]
    private async Task BrowseForPackage()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Package Files|*.appx;*.appxbundle;*.msix;*.msixbundle|All Files|*.*",
            Title = "Select Package to Install"
        };
        
        if (dialog.ShowDialog() == true)
        {
            await ProcessDroppedFileAsync(dialog.FileName);
        }
    }
    
    public async Task ProcessDroppedFileAsync(string filePath)
    {
        Reset();
        PendingFilePath = filePath;
        IsValidating = true;
        StatusMessage = "Validating package...";
        
        _diagnostics.Log(LogLevel.Info, $"Validating: {Path.GetFileName(filePath)}");
        
        var (isValid, info, error) = await _validation.ValidateAndExtractAsync(filePath);
        
        IsValidating = false;
        
        if (!isValid)
        {
            ValidationError = error;
            StatusMessage = "Validation failed";
            _diagnostics.Log(LogLevel.Error, $"Validation failed: {error}");
            return;
        }
        
        PendingPackage = info;
        ShowPackageDetails = true;
        StatusMessage = "Ready to install";
        
        // Check signature
        var sigStatus = await _validation.VerifySignatureAsync(filePath);
        if (info != null)
        {
            info.SignatureStatus = sigStatus;
        }
        
        if (sigStatus == SignatureStatus.Unsigned)
        {
            _diagnostics.Log(LogLevel.Warning, "Package is unsigned. Developer Mode required.");
        }
        
        _diagnostics.Log(LogLevel.Info, $"Package validated: {info?.DisplayName} v{info?.Version}");
    }
    
    [RelayCommand(CanExecute = nameof(CanInstall))]
    private async Task InstallPackage()
    {
        if (PendingPackage == null || PendingFilePath == null)
            return;
        
        // Show confirmation for unsigned packages
        if (PendingPackage.SignatureStatus == SignatureStatus.Unsigned)
        {
            var result = MessageBox.Show(
                $"This package is not digitally signed.\n\n" +
                $"Installing unsigned packages may pose security risks. " +
                $"Only install packages from sources you trust.\n\n" +
                $"Do you want to continue?",
                "Unsigned Package",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            
            if (result != MessageBoxResult.Yes)
                return;
        }
        
        IsInstalling = true;
        InstallProgress = 0;
        StatusMessage = "Installing...";
        
        var progress = new Progress<double>(p =>
        {
            InstallProgress = p;
            StatusMessage = $"Installing... {p:F0}%";
        });
        
        LastResult = await _packageManager.InstallPackageAsync(
            PendingFilePath,
            PendingPackage,
            progress);
        
        IsInstalling = false;
        
        if (LastResult.Success)
        {
            StatusMessage = "Installation successful!";
            InstallProgress = 100;
            
            // Clear for next install after delay
            await Task.Delay(3000);
            Reset();
        }
        else
        {
            StatusMessage = LastResult.Message;
        }
    }
    
    private bool CanInstall() => PendingPackage != null && !IsInstalling && !IsValidating;
    
    [RelayCommand]
    private void Reset()
    {
        PendingPackage = null;
        PendingFilePath = null;
        ValidationError = null;
        StatusMessage = null;
        ShowPackageDetails = false;
        InstallProgress = 0;
        LastResult = null;
        IsDragOver = false;
    }
    
    public void HandleDragEnter(DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files.Length == 1 && _validation.IsValidExtension(files[0]))
            {
                IsDragOver = true;
                e.Effects = DragDropEffects.Copy;
                e.Handled = true;
                return;
            }
        }
        
        e.Effects = DragDropEffects.None;
        e.Handled = true;
    }
    
    public void HandleDragLeave()
    {
        IsDragOver = false;
    }
    
    public async void HandleDrop(DragEventArgs e)
    {
        IsDragOver = false;
        
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files.Length == 1)
            {
                await ProcessDroppedFileAsync(files[0]);
            }
        }
    }
}
