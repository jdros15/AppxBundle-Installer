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

    public StoreViewModel()
    {
        _httpClient = new HttpClient();
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

            StatusMessage = StoreItems.Count > 0 ? $"Found {StoreItems.Count} items." : "No items found.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void ParseResults(string html)
    {
        // Simple regex parsing for the table rows
        // Structure is approximately:
        // <tr style="...">
        //   <td><a href="(link)" ...>(name)</a></td> (sometimes name contains <br>)
        //   <td>(expire)</td>
        //   <td>(sha1)</td>
        //   <td>(size)</td>
        // </tr>

        // We target the <a> tag specifically to get the link and name
        // Then we try to find the size in the subsequent tds

        // Pattern to match the row
        var rowPattern = @"<tr.*?>(.*?)</tr>";
        var rows = Regex.Matches(html, rowPattern, RegexOptions.Singleline);

        foreach (Match row in rows)
        {
            var rowContent = row.Groups[1].Value;

            // Extract Link and Name
            var linkMatch = Regex.Match(rowContent, @"<a\s+href=""([^""]+)""[^>]*>(.*?)</a>", RegexOptions.Singleline);
            if (!linkMatch.Success) continue;

            string url = linkMatch.Groups[1].Value;
            string name = linkMatch.Groups[2].Value;

            // Clean up name (sometimes has <br> or other tags? Usually clean text)
            // But sometimes the site returns error messages in the table.
            
            // Extract Size (it's usually in the 4th td, but let's just grab all tds)
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

            // Parse version/arch from Name if possible?
            // Name format is typically: PackageName_Version_Arch_...
            // e.g. Microsoft.WindowsCalculator_11.2210.0.0_neutral_~_8wekyb3d8bbwe.msixbundle
            
            StoreItems.Add(item);
        }
    }

    [RelayCommand]
    private async Task DownloadAsync(StoreItem item)
    {
        if (item == null) return;

        try 
        {
            // For now, let's just open in browser as it is robust and handles large files/resume
            Process.Start(new ProcessStartInfo(item.DownloadUrl) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not start download: {ex.Message}");
        }
    }
}
