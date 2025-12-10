using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SLSKDONET.Converters;

/// <summary>
/// Converts a string value to Visibility by comparing with a parameter.
/// Used to show/hide views based on selected mode (Albums/Tracks).
/// </summary>
public class StringToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo? culture)
    {
        var valueStr = value?.ToString() ?? string.Empty;
        var paramStr = parameter?.ToString() ?? string.Empty;
        
        return string.Equals(valueStr, paramStr, StringComparison.OrdinalIgnoreCase) 
            ? Visibility.Visible 
            : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo? culture)
    {
        throw new NotSupportedException("StringToVisibilityConverter does not support reverse conversion.");
    }
}
