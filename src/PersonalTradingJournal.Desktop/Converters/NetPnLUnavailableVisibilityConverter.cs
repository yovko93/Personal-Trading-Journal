using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace PersonalTradingJournal.Desktop.Converters;

/// <summary>
/// Shows the unknown-cost explanation only for trades with a calculated gross
/// result but no calculated net result.
/// </summary>
public sealed class NetPnLUnavailableVisibilityConverter : IMultiValueConverter
{
    public object Convert(
        object[] values,
        Type targetType,
        object parameter,
        CultureInfo culture) =>
        values.Length == 2 && values[0] is decimal && values[1] is null
            ? Visibility.Visible
            : Visibility.Collapsed;

    public object[] ConvertBack(
        object value,
        Type[] targetTypes,
        object parameter,
        CultureInfo culture) => throw new NotSupportedException();
}
