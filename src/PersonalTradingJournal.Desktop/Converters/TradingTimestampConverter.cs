using System.Globalization;
using System.Windows.Data;
using PersonalTradingJournal.Desktop.Formatting;

namespace PersonalTradingJournal.Desktop.Converters;

/// <summary>Display a New York timestamp with an explicit UTC offset, never an editable time or axis tick.</summary>
public sealed class TradingTimestampConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        DateTimeOffset timestamp => TradingTimestampFormatter.FormatNewYork(timestamp,
            parameter as string ?? TradingTimestampFormatter.DefaultClockFormat, culture),
        null => "—",
        _ => throw new ArgumentException("A DateTimeOffset value is required.", nameof(value)),
    };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
