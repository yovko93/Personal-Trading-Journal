using PersonalTradingJournal.Desktop.Navigation;
using System.Globalization;
using System.Windows.Data;

namespace PersonalTradingJournal.Desktop.Converters;

public sealed class NavigationSelectionConverter : IMultiValueConverter
{
    public object Convert(
        object[] values,
        Type targetType,
        object parameter,
        CultureInfo culture)
    {
        return values.Length >= 2
            && values[0] is NavigationDestination current
            && values[1] is NavigationDestination target
            && current == target;
    }

    public object[] ConvertBack(
        object value,
        Type[] targetTypes,
        object parameter,
        CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
