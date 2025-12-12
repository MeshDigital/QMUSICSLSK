using System;
using System.Globalization;
using System.Windows.Media;
using System.Windows.Data;

namespace SLSKDONET.Converters;

/// <summary>
/// Converts IsSelected boolean to background color for track cards in import preview.
/// Selected tracks show a highlighted background, unselected show default.
/// </summary>
public class IsSelectedToBackgroundConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isSelected && isSelected)
        {
            return new SolidColorBrush(System.Windows.Media.Color.FromArgb(0x4C, 0x4C, 0xAF, 0x50));
        }
        return new SolidColorBrush(System.Windows.Media.Color.FromArgb(0xFF, 0x2D, 0x2D, 0x30));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts IsSelected boolean to border color for track cards.
/// Selected tracks show a green border, unselected show default.
/// </summary>
public class IsSelectedToBorderConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isSelected && isSelected)
        {
            return new SolidColorBrush(System.Windows.Media.Color.FromArgb(0xFF, 0x4C, 0xAF, 0x50));
        }
        return new SolidColorBrush(System.Windows.Media.Color.FromArgb(0xFF, 0x40, 0x40, 0x40));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
