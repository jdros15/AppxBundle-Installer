using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
using AppxBundleInstaller.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.IO;
using AppxBundleInstaller.Services;

namespace AppxBundleInstaller.ViewModels;

public partial class StoreViewModel : ObservableObject
{
    private readonly HttpClient _httpClient;

    [ObservableProperty]
    private string _searchUrl = string.Empty;

    [ObservableProperty]
    private string _selectedType = "url";

    [ObservableProperty]
    private string _selectedRing = "Retail";

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public ObservableCollection<StoreItem> StoreItems { get; } = new();

    public List<string> SearchTypes { get; } = new() { "url", "ProductId", "PackageFamilyName", "CategoryId" };
    public List<string> Rings { get; } = new() { "Retail", "RP", "WIS", "WIF" };

    [ObservableProperty]
    private bool _isAutoInstall;
    
    public StoreViewModel()
    {
        _httpClient = new HttpClient();
        
        // Settings Sync
        IsAutoInstall = SettingsService.Instance.AutoInstall;
        SettingsService.Instance.PropertyChanged += (s, e) => 
        {
            if (e.PropertyName == nameof(SettingsService.AutoInstall))
            {
                IsAutoInstall = SettingsService.Instance.AutoInstall;
            }
        };
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
        if (string.IsNullOrWhiteSpace(SearchUrl))
        {
            StatusMessage = "Please enter a value.";
            return;
        }

        IsLoading = true;
        StatusMessage = "Searching...";
        DiagnosticsService.Instance.Log(LogLevel.Info, $"Searching Store for: {SearchUrl} ({SelectedType})");
        StoreItems.Clear();

        try
        {
            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("type", SelectedType),
                new KeyValuePair<string, string>("url", SearchUrl),
                new KeyValuePair<string, string>("ring", SelectedRing),
                new KeyValuePair<string, string>("lang", "en-US")
            });

            var response = await _httpClient.PostAsync("https://store.rg-adguard.net/api/GetFiles", content);
            response.EnsureSuccessStatusCode();

            var html = await response.Content.ReadAsStringAsync();
            ParseResults(html);

            var msg = StoreItems.Count > 0 ? $"Found {StoreItems.Count} items." : "No items found.";
            StatusMessage = msg;
            DiagnosticsService.Instance.Log(LogLevel.Info, $"Search completed. {msg}");
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
            DiagnosticsService.Instance.Log(LogLevel.Error, "Search failed", ex.Message);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void ParseResults(string html)
    {
        var rowPattern = @"<tr.*?>(.*?)</tr>";
        var rows = Regex.Matches(html, rowPattern, RegexOptions.Singleline);

        foreach (Match row in rows)
        {
            var rowContent = row.Groups[1].Value;

            var linkMatch = Regex.Match(rowContent, @"<a\s+href=""([^""]+)""[^>]*>(.*?)</a>", RegexOptions.Singleline);
            if (!linkMatch.Success) continue;

            string url = linkMatch.Groups[1].Value;
            string name = linkMatch.Groups[2].Value;

            var tds = Regex.Matches(rowContent, @"<td.*?>(.*?)</td>", RegexOptions.Singleline);
            
            string expire = "";
            string sha1 = "";
            string size = "";

            if (tds.Count >= 2) expire = tds[1].Groups[1].Value;
            if (tds.Count >= 3) sha1 = tds[2].Groups[1].Value;
            if (tds.Count >= 4) size = tds[3].Groups[1].Value;

            var item = new StoreItem
            {
                Name = name,
                DownloadUrl = url,
                Expiration = expire,
                FileSize = size
            };

            StoreItems.Add(item);
        }
    }

    [RelayCommand]
    private async Task DownloadAsync(StoreItem item)
    {
        if (item == null) return;

        try 
        {
            IsLoading = true;
            StatusMessage = $"Downloading {item.Name}...";
            DiagnosticsService.Instance.Log(LogLevel.Info, $"Starting download: {item.Name}", $"URL: {item.DownloadUrl}");

            string downloadFolder = SettingsService.Instance.DownloadFolderPath;
            if (!Directory.Exists(downloadFolder))
            {
                Directory.CreateDirectory(downloadFolder);
            }

            string fileName = item.Name;
            foreach(char c in Path.GetInvalidFileNameChars())
            {
                fileName = fileName.Replace(c, '_');
            }
            
            string filePath = Path.Combine(downloadFolder, fileName);
            
            // Overwrite if exists
            using (var stream = await _httpClient.GetStreamAsync(item.DownloadUrl))
            using (var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await stream.CopyToAsync(fileStream);
            }

            if (!File.Exists(filePath)) throw new FileNotFoundException("File not found after download", filePath);

            bool autoInstall = SettingsService.Instance.AutoInstall;
            StatusMessage = $"Downloaded to {filePath}";
            DiagnosticsService.Instance.Log(LogLevel.Success, "Download successful", filePath);

            if (autoInstall)
            {
                string ext = Path.GetExtension(fileName).ToLower();
                var supported = new[] { ".appx", ".msix", ".appxbundle", ".msixbundle", ".eappx", ".emsix", ".eappxbundle", ".emsixbundle" };

                if (supported.Contains(ext))
                {
                   StatusMessage = "Switching to Install...";
                   DiagnosticsService.Instance.Log(LogLevel.Info, "Auto-install enabled. Switching to installer view...");
                   
                   Application.Current.Dispatcher.Invoke(() => 
                   {
                       MainViewModel.Current?.RequestInstall(filePath, true);
                   });
                   
                   StatusMessage = "Ready";
                }
                else
                {
                     MessageBox.Show($"File downloaded to:\n{filePath}\n\nAuto-install is not supported for this file type.", "Download Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                     DiagnosticsService.Instance.Log(LogLevel.Warning, "Auto-install skipped: Unsupported file type", ext);
                     StatusMessage = "Ready";
                }
            }
            else
            {
                StatusMessage = "Download Complete";
                MessageBox.Show($"File downloaded to:\n{filePath}", "Download Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Download failed. Error: {ex.Message}";
            MessageBox.Show($"Could not download: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            DiagnosticsService.Instance.Log(LogLevel.Error, "Download failed", ex.Message);
        }
        finally
        {
            IsLoading = false;
        }
    }
}
