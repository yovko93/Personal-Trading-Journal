using System.Globalization;
using System.Windows.Data;

namespace PersonalTradingJournal.Desktop.Converters;

public enum PnLOutcome
{
    None = 0,
    Negative = 1,
    Zero = 2,
    Positive = 3,
}

/// <summary>
/// Classifies a nullable monetary P&amp;L value for Desktop presentation triggers.
/// </summary>
public sealed class PnLOutcomeConverter : IValueConverter
{
    public object Convert(
        object? value,
        Type targetType,
        object? parameter,
        CultureInfo culture) => value switch
        {
            decimal amount when amount > 0m => PnLOutcome.Positive,
            decimal amount when amount < 0m => PnLOutcome.Negative,
            decimal => PnLOutcome.Zero,
            _ => PnLOutcome.None,
        };

    public object ConvertBack(
        object? value,
        Type targetType,
        object? parameter,
        CultureInfo culture) => throw new NotSupportedException();
}
