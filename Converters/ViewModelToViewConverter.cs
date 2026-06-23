using System.Globalization;
using System.Windows.Data;
using Client.ViewModels;
using Client.Views;

namespace Client.Converters
{
    public class ViewModelToViewConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return value switch
            {
                DashboardViewModel => new DashboardView { DataContext = value },
                ModelViewModel => new ModelView { DataContext = value },
                HistoryViewModel => new HistoryView { DataContext = value },
                SettingsViewModel => new SettingsView { DataContext = value },
                ResultViewModel => new ResultView { DataContext = value },
                ItemsViewModel => new ItemsView { DataContext = value },
                _ => null
            };
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
