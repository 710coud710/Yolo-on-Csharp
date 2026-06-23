using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Client.Converters
{
    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                bool invert = parameter?.ToString() == "Invert";
                bool finalValue = invert ? !boolValue : boolValue;
                return finalValue ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Visible;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is Visibility visibility)
            {
                bool boolValue = visibility == Visibility.Visible;
                bool invert = parameter?.ToString() == "Invert";
                return invert ? !boolValue : boolValue;
            }
            return false;
        }
    }
}
