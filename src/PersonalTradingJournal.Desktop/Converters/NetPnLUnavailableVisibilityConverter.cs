using System.Globalization;
using System.Windows;
using System.Windows.Data;
using PersonalTradingJournal.Application.Trades;
using PersonalTradingJournal.Domain.Trades;

namespace PersonalTradingJournal.Desktop.Converters;

/// <summary>
/// Shows the estimate explanation when authoritative Net is unavailable but a closed
/// Trade has Gross. Uses the same effective-Net policy as the displayed number.
/// </summary>
public sealed class NetPnLUnavailableVisibilityConverter : IMultiValueConverter
{
    public object Convert(
        object[] values,
        Type targetType,
        object parameter,
        CultureInfo culture) =>
        values.Length == 3 && values[0] is decimal && values[1] is null && values[2] is TradeStatus status &&
        EffectiveNetPnL.Resolve(status, values[0] as decimal?, values[1] as decimal?).IsEstimated
            ? Visibility.Visible
            : Visibility.Collapsed;

    public object[] ConvertBack(
        object value,
        Type[] targetTypes,
        object parameter,
        CultureInfo culture) => throw new NotSupportedException();
}
