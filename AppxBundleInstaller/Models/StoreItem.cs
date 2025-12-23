namespace AppxBundleInstaller.Models;

public class StoreItem
{
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Architecture { get; set; } = string.Empty;
    public string DownloadUrl { get; set; } = string.Empty;
    public string Expiration { get; set; } = string.Empty;
    public string FileSize { get; set; } = string.Empty;

    // Helper to determine if it is likely a main package or dependency
    public bool IsAppxBundle => Name.EndsWith(".appxbundle", StringComparison.OrdinalIgnoreCase);
    public bool IsMsixBundle => Name.EndsWith(".msixbundle", StringComparison.OrdinalIgnoreCase);
    public bool IsBlockMap => Name.EndsWith(".blockmap", StringComparison.OrdinalIgnoreCase);
}
