using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SLSKDONET.Views
{
    public class ProgressToWidthConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double progress)
            {
                // Assuming parent width is 520 (600 - 80 margin)
                return progress * 5.2; // 520 / 100
            }
            return 0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
