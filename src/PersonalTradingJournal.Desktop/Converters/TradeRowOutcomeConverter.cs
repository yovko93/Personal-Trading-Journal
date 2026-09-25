using System.Globalization;
using System.Windows.Data;

namespace PersonalTradingJournal.Desktop.Converters;

/// <summary>
/// Uses known net P&amp;L for row styling, or a known gross loss when net is unavailable.
/// A gross gain alone cannot establish a net gain when costs are unknown.
/// </summary>
public sealed class TradeRowOutcomeConverter : IMultiValueConverter
{
    public object Convert(
        object[] values,
        Type targetType,
        object parameter,
        CultureInfo culture)
    {
        if (values.Length != 2)
        {
            return PnLOutcome.None;
        }

        if (values[0] is decimal net)
        {
            return net > 0m ? PnLOutcome.Positive :
                net < 0m ? PnLOutcome.Negative : PnLOutcome.Zero;
        }

        return values[1] is decimal gross && gross < 0m
            ? PnLOutcome.Negative
            : PnLOutcome.None;
    }

    public object[] ConvertBack(
        object value,
        Type[] targetTypes,
        object parameter,
        CultureInfo culture) => throw new NotSupportedException();
}
