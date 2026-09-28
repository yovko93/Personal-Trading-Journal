using System.Globalization;
using System.Windows.Data;

namespace PersonalTradingJournal.Desktop.Converters;

/// <summary>
/// Uses known net P&amp;L for row styling, or the known gross sign when net is unavailable.
/// Gross coloring does not imply that unknown net P&amp;L has the same sign.
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

        return values[1] is decimal gross
            ? gross > 0m ? PnLOutcome.Positive :
                gross < 0m ? PnLOutcome.Negative : PnLOutcome.Zero
            : PnLOutcome.None;
    }

    public object[] ConvertBack(
        object value,
        Type[] targetTypes,
        object parameter,
        CultureInfo culture) => throw new NotSupportedException();
}
