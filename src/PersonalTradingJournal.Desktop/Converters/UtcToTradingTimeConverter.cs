using System.Globalization;
using System.Windows.Data;
using PersonalTradingJournal.Application.Common.Time;

namespace PersonalTradingJournal.Desktop.Converters;

/// <summary>
/// Projects canonical UTC Trade timestamps into the fixed New York trading timezone.
/// </summary>
public sealed class UtcToTradingTimeConverter : IValueConverter
{
    public object Convert(
        object? value,
        Type targetType,
        object? parameter,
        CultureInfo culture) => value switch
        {
            DateTimeOffset timestamp => TradingTimePolicy.ConvertUtcToTradingTime(timestamp),
            null => "—",
            _ => throw new ArgumentException(
                "A UTC DateTimeOffset value is required.",
                nameof(value)),
        };

    public object ConvertBack(
        object? value,
        Type targetType,
        object? parameter,
        CultureInfo culture) => throw new NotSupportedException();
}
