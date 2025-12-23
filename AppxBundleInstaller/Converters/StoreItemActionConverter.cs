using System.Globalization;
using System.IO;
using System.Windows.Data;

namespace AppxBundleInstaller.Converters;

public class StoreItemActionConverter : IMultiValueConverter
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".appx", ".msix", ".appxbundle", ".msixbundle", 
        ".eappx", ".emsix", ".eappxbundle", ".emsixbundle"
    };

    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 2) return "Download";

        string fileName = values[0] as string ?? string.Empty;
        bool autoInstall = values[1] is bool b && b;

        if (!autoInstall) return "Download";

        string ext = Path.GetExtension(fileName);
        if (string.IsNullOrEmpty(ext)) return "Download";

        return SupportedExtensions.Contains(ext) ? "Install" : "Download";
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
