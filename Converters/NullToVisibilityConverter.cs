using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SLSKDONET.Converters;

/// <summary>
/// Converts a value to Visibility: returns Collapsed when value is NULL, Visible when NOT NULL.
/// Used to show content only when a project is selected.
/// </summary>
public class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value == null ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
