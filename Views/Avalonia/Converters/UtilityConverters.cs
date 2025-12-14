using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace SLSKDONET.Views.Avalonia.Converters
{
    public class RepeatModeIconConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is string repeatMode)
            {
                return repeatMode switch
                {
                    "None" => "🔁",
                    "One" => "🔂",
                    "All" => "🔁",
                    _ => "🔁"
                };
            }
            return "🔁";
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class EqualityConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return value?.Equals(parameter) ?? false;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
