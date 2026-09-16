using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace PersonalTradingJournal.Desktop.Converters;

public sealed class NavigationIconConverter : IValueConverter
{
    public object Convert(
        object value,
        Type targetType,
        object parameter,
        CultureInfo culture)
    {
        if (value is not string resourceKey || string.IsNullOrWhiteSpace(resourceKey))
        {
            throw new InvalidOperationException("A navigation icon resource key is required.");
        }

        return System.Windows.Application.Current?.TryFindResource(resourceKey) is Geometry geometry
            ? geometry
            : throw new InvalidOperationException(
                $"Navigation icon resource '{resourceKey}' was not found.");
    }

    public object ConvertBack(
        object value,
        Type targetType,
        object parameter,
        CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
