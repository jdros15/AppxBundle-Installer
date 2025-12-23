using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AppxBundleInstaller.Models;

namespace AppxBundleInstaller.Converters;

/// <summary>
/// Converts boolean to Visibility (true = Visible, false = Collapsed)
/// </summary>
public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is bool b && b ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is Visibility v && v == Visibility.Visible;
    }
}

/// <summary>
/// Converts boolean to Visibility (true = Collapsed, false = Visible)
/// </summary>
public class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is bool b && b ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is Visibility v && v == Visibility.Collapsed;
    }
}

/// <summary>
/// Converts null to Visibility (not null = Visible, null = Collapsed)
/// </summary>
public class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value != null ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts null to Visibility (null = Visible, not null = Collapsed)
/// </summary>
public class InverseNullToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value == null ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts count to Visibility (0 = Visible, >0 = Collapsed)
/// </summary>
public class ZeroToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is int count && count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts count to Visibility (>0 = Visible, 0 = Collapsed)
/// </summary>
public class CountToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is int count && count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts PackageType enum to user-friendly display string
/// </summary>
public class PackageTypeToStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is PackageType packageType)
        {
            return packageType switch
            {
                PackageType.Microsoft => "Microsoft",
                PackageType.ThirdParty => "Third Party",
                PackageType.Framework => "Framework",
                _ => "Unknown"
            };
        }
        return "Unknown";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts PackageType enum to a color brush for visual distinction (darker, more readable colors)
/// </summary>
public class PackageTypeToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is PackageType packageType)
        {
            return packageType switch
            {
                PackageType.Microsoft => new SolidColorBrush(Color.FromRgb(0, 99, 177)),     // Darker Microsoft Blue
                PackageType.ThirdParty => new SolidColorBrush(Color.FromRgb(46, 125, 50)),   // Forest Green
                PackageType.Framework => new SolidColorBrush(Color.FromRgb(191, 144, 0)),    // Dark Amber/Gold
                _ => new SolidColorBrush(Color.FromRgb(97, 97, 97))                          // Dark Gray
            };
        }
        return new SolidColorBrush(Color.FromRgb(97, 97, 97));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts a logo file path to a BitmapImage for display.
/// Returns null if the path is invalid or the file cannot be loaded.
/// </summary>
public class LogoPathToImageSourceConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not string logoPath || string.IsNullOrWhiteSpace(logoPath))
            return null;

        try
        {
            // Check if file exists
            if (!File.Exists(logoPath))
                return null;

            // Try to load the image
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(logoPath, UriKind.Absolute);
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.DecodePixelWidth = 36; // Optimize for display size
            bitmap.DecodePixelHeight = 36;
            bitmap.EndInit();
            bitmap.Freeze(); // Make it thread-safe

            return bitmap;
        }
        catch
        {
            // If loading fails for any reason, return null to show fallback
            return null;
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts a string to Visibility - shows element when string is null/empty (inverse of StringToVisibility)
/// Used to show fallback icon when no logo path is available
/// </summary>
public class StringEmptyToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var str = value as string;
        return string.IsNullOrWhiteSpace(str) ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts a string to Visibility - shows element when string is not null/empty
/// Used to show app icon when logo path is available
/// </summary>
public class StringNotEmptyToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var str = value as string;
        return !string.IsNullOrWhiteSpace(str) ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
