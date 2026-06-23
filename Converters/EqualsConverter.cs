using System;
using System.Globalization;
using System.Windows.Data;

namespace Client.Converters
{
    /// <summary>
    /// Compares the bound value to ConverterParameter (string) and returns bool.
    /// ConvertBack returns ConverterParameter when IsChecked=true.
    /// </summary>
    public class EqualsConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            var left = value?.ToString() ?? string.Empty;
            var right = parameter?.ToString() ?? string.Empty;
            return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool b && b)
            {
                return parameter?.ToString() ?? string.Empty;
            }

            return Binding.DoNothing;
        }
    }
}

